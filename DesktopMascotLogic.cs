using System.Globalization;

namespace Avocado;

public enum DesktopMascotPose { Idle, Wave, Play, Sleep }

public enum DesktopMascotIdleAction { Bounce, Sway, Wave, Cheer, Love, LookAround, Stretch }

public sealed record DesktopMascotNag(TodoItem Task, string Title, string Summary);

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

        var stale = open.Where(task => now - task.CreatedAt >= StaleTaskAge)
            .OrderByDescending(task => task.Priority).ThenBy(task => task.CreatedAt).FirstOrDefault();
        return stale is null ? null : new DesktopMascotNag(stale, "Remember this one?",
            $"{stale.Text}\nWaiting for {DescribeAge(now - stale.CreatedAt)}.");
    }

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
