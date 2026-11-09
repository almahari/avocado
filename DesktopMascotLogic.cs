using System.Globalization;

namespace Avocado;

public enum DesktopMascotPose { Idle, Wave, Play, Sleep }

public enum DesktopMascotIdleAction { Bounce, Sway, Wave, Cheer, Love, LookAround, Stretch }

public sealed record DesktopMascotNag(TodoItem Task, string Title, string Summary);

public sealed record DesktopMascotDigestEntry(TodoItem Task, string When, bool IsLate = false);

public sealed record DesktopMascotDigest(
    string Date,
    string Time,
    IReadOnlyList<DesktopMascotDigestEntry> Today,
    IReadOnlyList<DesktopMascotDigestEntry> Old,
    IReadOnlyList<DesktopMascotDigestEntry> Tomorrow);

public static class DesktopMascotLogic
{
    public const double SpriteSize = 150;
    public static readonly TimeSpan StaleTaskAge = TimeSpan.FromDays(2);
    public static readonly TimeSpan NagInterval = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan NagRepeatPerTask = TimeSpan.FromHours(6);
    public static readonly TimeSpan BubbleDuration = TimeSpan.FromSeconds(14);

    private static readonly DesktopMascotIdleAction[] IdleActions = Enum.GetValues<DesktopMascotIdleAction>();

    private static readonly string[] PokeLines =
    [
        "Hehe, that tickles!", "Boop!", "Hey! I'm working here 🐾", "Need a snack break?",
        "You've got this!", "Wheee!", "Pet me again 😊"
    ];

    public static string FramePath(FruitThemeKind theme, string frame) =>
        $"Assets/desktop-mascot/{theme.ToString().ToLowerInvariant()}-{frame}.png";

    public static string SpritePath(FruitThemeKind theme, DesktopMascotPose pose) =>
        $"Assets/desktop-mascot/{theme.ToString().ToLowerInvariant()}-{pose.ToString().ToLowerInvariant()}.png";

    /// <summary>Random idle delay so the mascot does not feel mechanical.</summary>
    public static TimeSpan NextIdleDelay(Random random) => TimeSpan.FromSeconds(random.Next(10, 25));

    public static DesktopMascotIdleAction PickIdleAction(Random random, DesktopMascotIdleAction? previous)
    {
        var action = IdleActions[random.Next(IdleActions.Length)];
        // Avoid repeating the same trick twice in a row.
        return action == previous ? IdleActions[((int)action + 1) % IdleActions.Length] : action;
    }

    public static string PickPokeLine(Random random) => PokeLines[random.Next(PokeLines.Length)];

    /// <summary>
    /// Finds the oldest open task worth nagging about: overdue tasks first, then tasks
    /// left untouched for longer than <see cref="StaleTaskAge"/>. Tasks nagged recently are skipped.
    /// </summary>
    public static DesktopMascotNag? FindNag(
        IEnumerable<TodoItem> tasks, DateTime now, IReadOnlyDictionary<Guid, DateTime>? lastNagged = null)
    {
        bool Eligible(TodoItem task) =>
            !task.IsCompleted && (task.SnoozedUntil is not DateTime snoozed || snoozed <= now) &&
            (lastNagged is null || !lastNagged.TryGetValue(task.Id, out var at) || now - at >= NagRepeatPerTask);

        var open = tasks.Where(Eligible).ToList();
        var overdue = open.Where(task => task.DueAt is DateTime due && due < now)
            .OrderBy(task => task.DueAt).FirstOrDefault();
        if (overdue is not null)
            return new DesktopMascotNag(overdue, "Overdue task!",
                $"{overdue.Text}\nWas due {DescribeAge(now - overdue.DueAt!.Value)} ago.");

        var today = DateOnly.FromDateTime(now);
        var missed = open.Select(task => (Task: task, Day: TaskReminderLogic.MissedOccurrence(task, today)))
            .Where(item => item.Day is not null)
            .OrderBy(item => item.Day).ThenByDescending(item => item.Task.Priority).FirstOrDefault();
        if (missed.Task is not null)
            return new DesktopMascotNag(missed.Task, "Missed one!",
                $"{missed.Task.Text}\nIt was due {TaskReminderLogic.MissedDayName(missed.Day!.Value, today)} at {missed.Task.ReminderTime:hh\\:mm}.");

        // Recurring tasks are expected to stay in the list, so they never count as stale.
        var stale = open.Where(task => task.Recurrence == TaskRecurrence.None && now - task.CreatedAt >= StaleTaskAge)
            .OrderByDescending(task => task.Priority).ThenBy(task => task.CreatedAt).FirstOrDefault();
        return stale is null ? null : new DesktopMascotNag(stale, "Remember this one?",
            $"{stale.Text}\nWaiting for {DescribeAge(now - stale.CreatedAt)}.");
    }

    /// <summary>
    /// Builds the "What's up!" digest: today's work, old (overdue or stale) tasks and tomorrow's plan.
    /// </summary>
    public static DesktopMascotDigest BuildDigest(IEnumerable<TodoItem> tasks, DateTime now)
    {
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var todayItems = new List<(DesktopMascotDigestEntry Entry, TimeSpan Sort)>();
        var oldItems = new List<(DesktopMascotDigestEntry Entry, DateTime Since)>();
        var tomorrowItems = new List<(DesktopMascotDigestEntry Entry, TimeSpan Sort)>();
        var anytime = TimeSpan.FromDays(1);

        var todayDate = DateOnly.FromDateTime(today);
        var tomorrowDate = DateOnly.FromDateTime(tomorrow);
        foreach (var task in tasks.Where(task => !task.IsCompleted))
        {
            var hiddenToday = TaskReminderLogic.IsHiddenOn(task.HiddenUntil, todayDate);
            var hiddenTomorrow = TaskReminderLogic.IsHiddenOn(task.HiddenUntil, tomorrowDate);
            if (hiddenToday && hiddenTomorrow) continue;
            if (task.DueAt is DateTime due)
            {
                if (due.Date < today)
                    oldItems.Add((new(task, $"was due {due.ToString("MMM d", CultureInfo.InvariantCulture)} · {DescribeAge(now - due)} late", true), due));
                else if (due.Date == today)
                    todayItems.Add((new(task, due < now ? $"late · {due:HH:mm}" : due.ToString("HH:mm", CultureInfo.InvariantCulture), due < now), due.TimeOfDay));
                else if (due.Date == tomorrow)
                    tomorrowItems.Add((new(task, due.ToString("HH:mm", CultureInfo.InvariantCulture)), due.TimeOfDay));
                continue;
            }

            if (task.ReminderTime is TimeSpan time)
            {
                var clock = $"{time:hh\\:mm}";
                if (task.Recurrence == TaskRecurrence.None)
                {
                    // One-off time reminders that already fired on an earlier day are left-overs.
                    if (task.LastReminderDate is DateOnly fired && fired < DateOnly.FromDateTime(today))
                        oldItems.Add((new(task, $"reminded {fired.ToString("MMM d", CultureInfo.InvariantCulture)}", true), fired.ToDateTime(TimeOnly.FromTimeSpan(time))));
                    else
                        todayItems.Add((new(task, clock), time));
                    continue;
                }
                var label = task.Recurrence switch
                {
                    TaskRecurrence.Daily => "daily",
                    TaskRecurrence.Monthly => task.MonthDay == TaskReminderLogic.LastDayOfMonth ? "monthly, last day" : $"monthly, {TaskReminderLogic.MonthDayName(task.MonthDay)}",
                    _ => TaskReminderLogic.Label(task.Recurrence).ToLowerInvariant()
                };
                if (TaskReminderLogic.MissedOccurrence(task, todayDate) is DateOnly missedDay)
                    oldItems.Add((new(task, $"missed {TaskReminderLogic.MissedDayName(missedDay, todayDate)} · {clock} · {label}", true),
                        missedDay.ToDateTime(TimeOnly.FromTimeSpan(time))));
                if (!hiddenToday && TaskReminderLogic.OccursOn(task, todayDate))
                    todayItems.Add((new(task, $"{clock} · {label}"), time));
                if (!hiddenTomorrow && TaskReminderLogic.OccursOn(task, tomorrowDate))
                    tomorrowItems.Add((new(task, $"{clock} · {label}"), time));
                continue;
            }

            if (now - task.CreatedAt >= StaleTaskAge)
                oldItems.Add((new(task, $"open for {DescribeAge(now - task.CreatedAt)}"), task.CreatedAt));
            else
                todayItems.Add((new(task, "anytime"), anytime));
        }

        return new DesktopMascotDigest(
            now.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            now.ToString("HH:mm", CultureInfo.InvariantCulture),
            todayItems.OrderBy(item => item.Sort).ThenByDescending(item => item.Entry.Task.Priority).Select(item => item.Entry).ToList(),
            oldItems.OrderBy(item => item.Since).Select(item => item.Entry).ToList(),
            tomorrowItems.OrderBy(item => item.Sort).ThenByDescending(item => item.Entry.Task.Priority).Select(item => item.Entry).ToList());
    }

    public static string DigestGreeting(DateTime now) => now.Hour switch
    {
        < 5 => "Up late? Here's the plan",
        < 12 => "Good morning! Here's your day",
        < 17 => "Good afternoon! Here's what's up",
        < 21 => "Good evening! Here's what's left",
        _ => "Winding down? Here's what's up"
    };

    public static string ReminderTitle(int count) => count == 1 ? "Reminder!" : $"{count} reminders!";

    public static string ReminderSummary(IReadOnlyList<TodoItem> tasks, int maxLines = 3)
    {
        var lines = tasks.Take(maxLines).Select(task =>
        {
            var priority = TaskReminderLogic.PriorityPrefix(task.Priority);
            var when = task.ReminderLabel;
            return $"• {(priority.Length > 0 ? priority + " " : string.Empty)}{task.Text}" +
                   (when.Length > 0 ? $"  ({when})" : string.Empty);
        }).ToList();
        if (tasks.Count > maxLines) lines.Add($"+{tasks.Count - maxLines} more");
        return string.Join("\n", lines);
    }

    public static string DescribeAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
        {
            var days = (int)age.TotalDays;
            return days == 1 ? "1 day" : $"{days.ToString(CultureInfo.InvariantCulture)} days";
        }
        if (age.TotalHours >= 1)
        {
            var hours = (int)age.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours.ToString(CultureInfo.InvariantCulture)} hours";
        }
        var minutes = Math.Max(1, (int)age.TotalMinutes);
        return minutes == 1 ? "1 minute" : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";
    }

    /// <summary>Keeps the mascot window fully inside the given work area.</summary>
    public static (double Left, double Top) ClampToArea(
        double left, double top, double width, double height,
        double areaLeft, double areaTop, double areaWidth, double areaHeight) =>
        (Math.Clamp(left, areaLeft, Math.Max(areaLeft, areaLeft + areaWidth - width)),
         Math.Clamp(top, areaTop, Math.Max(areaTop, areaTop + areaHeight - height)));
}
