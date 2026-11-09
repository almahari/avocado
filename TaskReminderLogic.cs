using System.Globalization;
using System.Text.RegularExpressions;

namespace Avocado;

public enum TaskRecurrence
{
    None,
    Daily,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
    Monthly
}

public enum TaskPriority
{
    None,
    Low,
    Medium,
    High
}

public readonly record struct ParsedTaskInput(
    string Text,
    TimeSpan? ReminderTime,
    TaskRecurrence Recurrence = TaskRecurrence.None,
    TaskPriority Priority = TaskPriority.None,
    DateTime? DueAt = null,
    int? MonthDay = null);

public static partial class TaskReminderLogic
{
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromSeconds(2);

    /// <summary>Stored <see cref="TodoItem.MonthDay"/> value meaning "the last day of every month".</summary>
    public const int LastDayOfMonth = -1;
    public const double SleepShakeMinimumDistance = 14;

    public static IReadOnlyList<ParsedTaskInput> ParseMany(string input, DateTime? referenceTime = null)
    {
        var tasks = new List<ParsedTaskInput>();
        foreach (var segment in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parsed = Parse(segment, referenceTime);
            if (parsed.Text.Length > 0) tasks.Add(parsed);
        }
        return tasks;
    }

    public static ParsedTaskInput Parse(string input, DateTime? referenceTime = null)
    {
        var trimmed = input.Trim();
        var now = referenceTime ?? DateTime.Now;
        var recurringMatch = RecurringPrefix().Match(trimmed);
        if (recurringMatch.Success)
        {
            var recurrence = ParseRecurrence(recurringMatch.Groups["recurrence"].Value);
            return WithPriority(
                recurringMatch.Groups["text"].Value.Trim(),
                ParseTime(recurringMatch.Groups["time"].Value),
                recurrence);
        }

        var monthlyMatch = MonthlyPrefix().Match(trimmed);
        if (monthlyMatch.Success && TryParseFlexibleTime(monthlyMatch.Groups["time"].Value, out var monthlyTime))
        {
            var dayText = monthlyMatch.Groups["day"].Value;
            var monthDay = dayText.Length == 0 ? 1
                : dayText.Equals("last", StringComparison.OrdinalIgnoreCase) ? LastDayOfMonth
                : int.Parse(dayText.TrimEnd('s', 'S', 't', 'T', 'n', 'N', 'd', 'D', 'r', 'R', 'h', 'H'), CultureInfo.InvariantCulture);
            return WithPriority(monthlyMatch.Groups["text"].Value.Trim(), monthlyTime, TaskRecurrence.Monthly, monthDay: monthDay);
        }

        var exactDateMatch = ExactDatePrefix().Match(trimmed);
        if (exactDateMatch.Success &&
            DateOnly.TryParseExact(exactDateMatch.Groups["date"].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate) &&
            TryParseFlexibleTime(exactDateMatch.Groups["time"].Value, out var exactTime))
        {
            return WithPriority(exactDateMatch.Groups["text"].Value.Trim(), null,
                TaskRecurrence.None, exactDate.ToDateTime(TimeOnly.FromTimeSpan(exactTime)));
        }

        var relativeMatch = RelativeDatePrefix().Match(trimmed);
        if (relativeMatch.Success && TryParseOptionalTime(relativeMatch.Groups["time"].Value, out var relativeTime))
        {
            var date = DateOnly.FromDateTime(now).AddDays(
                relativeMatch.Groups["day"].Value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            return WithPriority(relativeMatch.Groups["text"].Value.Trim(), null,
                TaskRecurrence.None, date.ToDateTime(TimeOnly.FromTimeSpan(relativeTime)));
        }

        var weekdayMatch = NaturalWeekdayPrefix().Match(trimmed);
        if (weekdayMatch.Success && TryParseOptionalTime(weekdayMatch.Groups["time"].Value, out var weekdayTime))
        {
            var requestedDay = Enum.Parse<DayOfWeek>(weekdayMatch.Groups["day"].Value, ignoreCase: true);
            var daysAhead = ((int)requestedDay - (int)now.DayOfWeek + 7) % 7;
            if (daysAhead == 0 || weekdayMatch.Groups["next"].Success) daysAhead += 7;
            var date = DateOnly.FromDateTime(now).AddDays(daysAhead);
            return WithPriority(weekdayMatch.Groups["text"].Value.Trim(), null,
                TaskRecurrence.None, date.ToDateTime(TimeOnly.FromTimeSpan(weekdayTime)));
        }

        var match = ReminderPrefix().Match(trimmed);
        if (!match.Success) return WithPriority(trimmed, null, TaskRecurrence.None);
        return WithPriority(
            match.Groups["text"].Value.Trim(),
            ParseTime(match.Groups["time"].Value),
            TaskRecurrence.None);
    }

    public static bool IsDue(DateTime dueAt, DateTime now, DateOnly? lastTriggeredDate) =>
        lastTriggeredDate is null && now >= dueAt;

    public static bool IsDue(
        TimeSpan reminderTime,
        DateTime now,
        DateOnly? lastTriggeredDate,
        TaskRecurrence recurrence = TaskRecurrence.None) =>
        reminderTime.Hours == now.Hour &&
        reminderTime.Minutes == now.Minute &&
        (recurrence == TaskRecurrence.None
            ? lastTriggeredDate is null
            : lastTriggeredDate != DateOnly.FromDateTime(now));

    /// <summary>
    /// Time-of-day reminder check that also catches up: besides the exact minute, a reminder whose time today fell
    /// inside (<paramref name="windowStart"/>, <paramref name="now"/>] fires now — e.g. the app was closed or the PC
    /// was asleep at that time. Tasks created after their reminder time don't fire retroactively.
    /// </summary>
    public static bool IsDue(
        TimeSpan reminderTime,
        DateTime windowStart,
        DateTime now,
        DateOnly? lastTriggeredDate,
        TaskRecurrence recurrence,
        DateTime createdAt)
    {
        var notTriggeredYet = recurrence == TaskRecurrence.None
            ? lastTriggeredDate is null
            : lastTriggeredDate != DateOnly.FromDateTime(now);
        if (!notTriggeredYet) return false;
        if (reminderTime.Hours == now.Hour && reminderTime.Minutes == now.Minute) return true;
        return WasMissedWhileAway(reminderTime, windowStart, now, createdAt);
    }

    public static bool WasMissedWhileAway(TimeSpan reminderTime, DateTime windowStart, DateTime now, DateTime createdAt)
    {
        var at = now.Date + reminderTime;
        return at > windowStart && at <= now && createdAt < at;
    }

    /// <summary>The most recent day before <paramref name="today"/> on which a recurring task happened.</summary>
    public static DateOnly? PreviousOccurrence(TodoItem task, DateOnly today)
    {
        for (var day = today.AddDays(-1); day >= today.AddDays(-62); day = day.AddDays(-1))
            if (OccursOn(task, day)) return day;
        return null;
    }

    /// <summary>
    /// The day a recurring task was missed on: its last day passed without it being ticked off, and its next day
    /// hasn't come yet. Null when it isn't missed (daily tasks never are: today is always their day).
    /// </summary>
    public static DateOnly? MissedOccurrence(TodoItem task, DateOnly today)
    {
        if (task.IsCompleted || task.Recurrence == TaskRecurrence.None || task.ReminderTime is not TimeSpan time) return null;
        if (IsHiddenOn(task.HiddenUntil, today) || OccursOn(task, today)) return null;
        if (PreviousOccurrence(task, today) is not DateOnly previous) return null;
        if (task.CreatedAt >= previous.ToDateTime(TimeOnly.FromTimeSpan(time))) return null;
        if (task.LastDoneDate is DateOnly done && done >= previous) return null;
        return previous;
    }

    /// <summary>"Monday" for a date in the last week, otherwise "Nov 3".</summary>
    public static string MissedDayName(DateOnly missed, DateOnly today) =>
        today.DayNumber - missed.DayNumber < 7
            ? missed.DayOfWeek.ToString()
            : missed.ToString("MMM d", CultureInfo.InvariantCulture);

    public static bool IsSnoozeDue(DateTime? snoozedUntil, DateTime now) =>
        snoozedUntil is DateTime due && now >= due;

    /// <summary>The first day after <paramref name="after"/> on which a recurring task happens again.</summary>
    public static DateOnly NextOccurrence(TaskRecurrence recurrence, DateOnly after, int? monthDay = null)
    {
        for (var day = after.AddDays(1); day <= after.AddDays(62); day = day.AddDays(1))
            if (OccursOn(recurrence, monthDay, day)) return day;
        return after.AddDays(1);
    }

    public static DateOnly NextOccurrence(TodoItem task, DateOnly after) => NextOccurrence(task.Recurrence, after, task.MonthDay);

    /// <summary>
    /// True when a reminder with this recurrence happens on <paramref name="date"/>. Monthly tasks happen on their
    /// day of the month; a day the month doesn't have (e.g. the 31st in April) falls on that month's last day.
    /// </summary>
    public static bool OccursOn(TaskRecurrence recurrence, int? monthDay, DateOnly date) => recurrence switch
    {
        TaskRecurrence.Monthly => date.Day == MonthlyDayIn(monthDay, date.Year, date.Month),
        _ => MatchesDay(recurrence, date.DayOfWeek)
    };

    public static bool OccursOn(TodoItem task, DateOnly date) => OccursOn(task.Recurrence, task.MonthDay, date);

    /// <summary>The actual day number a monthly task falls on in the given month.</summary>
    public static int MonthlyDayIn(int? monthDay, int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var day = monthDay ?? 1;
        return day == LastDayOfMonth || day > days ? days : Math.Max(1, day);
    }

    /// <summary>"1st", "2nd", "23rd", "last".</summary>
    public static string MonthDayName(int? monthDay)
    {
        var day = monthDay ?? 1;
        if (day == LastDayOfMonth) return "last";
        var suffix = (day % 100) is 11 or 12 or 13 ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return day.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>Plain-language recurrence, e.g. "daily", "every Monday", "monthly on the 1st", "monthly on the last day".</summary>
    public static string Describe(TaskRecurrence recurrence, int? monthDay = null) => recurrence switch
    {
        TaskRecurrence.None => string.Empty,
        TaskRecurrence.Daily => "daily",
        TaskRecurrence.Monthly => monthDay == LastDayOfMonth ? "monthly on the last day" : $"monthly on the {MonthDayName(monthDay)}",
        _ => $"every {recurrence}"
    };

    /// <summary>True while a recurring task that was marked done is still waiting for its next occurrence.</summary>
    public static bool IsHiddenOn(DateOnly? hiddenUntil, DateOnly date) => hiddenUntil is DateOnly until && date < until;

    public static bool MatchesDay(TaskRecurrence recurrence, DayOfWeek dayOfWeek) =>
        recurrence is TaskRecurrence.None or TaskRecurrence.Daily ||
        recurrence.ToString().Equals(dayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase);

    public static string Prefix(TaskRecurrence recurrence, int? monthDay = null) => recurrence switch
    {
        TaskRecurrence.None => string.Empty,
        TaskRecurrence.Daily => "daily",
        TaskRecurrence.Monthly => monthDay == LastDayOfMonth ? "monthly last" : $"monthly {monthDay ?? 1}",
        _ => recurrence.ToString().ToLowerInvariant()
    };

    public static string Label(TaskRecurrence recurrence, int? monthDay = null) => recurrence switch
    {
        TaskRecurrence.None => string.Empty,
        TaskRecurrence.Daily => "D",
        TaskRecurrence.Monthly => "M" + (monthDay == LastDayOfMonth ? "-LAST" : (monthDay ?? 1).ToString(CultureInfo.InvariantCulture)),
        _ => recurrence.ToString()[..3].ToUpperInvariant()
    };

    public static string PriorityPrefix(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "!",
        TaskPriority.Medium => "!!",
        TaskPriority.High => "!!!",
        _ => string.Empty
    };

    public static string FormatDueLabel(DateTime dueAt) =>
        dueAt.ToString("MMM dd HH:mm", CultureInfo.InvariantCulture).ToUpperInvariant();

    private static ParsedTaskInput WithPriority(
        string text, TimeSpan? time, TaskRecurrence recurrence, DateTime? dueAt = null, int? monthDay = null)
    {
        var match = PriorityPrefixPattern().Match(text);
        if (!match.Success) return new ParsedTaskInput(text, time, recurrence, DueAt: dueAt, MonthDay: monthDay);
        var priority = match.Groups["priority"].Value.Length switch
        {
            1 => TaskPriority.Low,
            2 => TaskPriority.Medium,
            _ => TaskPriority.High
        };
        return new ParsedTaskInput(match.Groups["text"].Value.Trim(), time, recurrence, priority, dueAt, monthDay);
    }

    private static TimeSpan ParseTime(string value) =>
        TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);

    private static bool TryParseOptionalTime(string value, out TimeSpan time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = TimeSpan.FromHours(9);
            return true;
        }
        return TryParseFlexibleTime(value, out time);
    }

    private static bool TryParseFlexibleTime(string value, out TimeSpan time)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (TimeSpan.TryParseExact(normalized, @"h\:mm", CultureInfo.InvariantCulture, out time) ||
            TimeSpan.TryParseExact(normalized, @"hh\:mm", CultureInfo.InvariantCulture, out time))
            return time < TimeSpan.FromDays(1);

        if (DateTime.TryParseExact(normalized, ["htt", "h:mmtt", "h tt", "h:mm tt"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }
        time = default;
        return false;
    }

    private static TaskRecurrence ParseRecurrence(string value) =>
        Enum.Parse<TaskRecurrence>(value, ignoreCase: true);

    [GeneratedRegex(@"^(?<time>(?:[01]\d|2[0-3]):[0-5]\d)\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReminderPrefix();

    [GeneratedRegex(@"^(?<recurrence>daily|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(?<time>(?:[01]\d|2[0-3]):[0-5]\d)\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecurringPrefix();

    // monthly 1 09:00 Pay rent · monthly 15th 9am Invoice · monthly last 17:00 Close books · monthly 09:00 (= the 1st)
    [GeneratedRegex(@"^monthly(?:\s+(?<day>last|(?:3[01]|[12]\d|0?[1-9])(?:st|nd|rd|th)?))?\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm)))\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthlyPrefix();

    [GeneratedRegex(@"^(?<priority>!{1,3})\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PriorityPrefixPattern();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm)))\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactDatePrefix();

    [GeneratedRegex(@"^(?<day>today|tomorrow)(?:\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm))))?\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelativeDatePrefix();

    [GeneratedRegex(@"^(?<next>next\s+)?(?<day>monday|tuesday|wednesday|thursday|friday|saturday|sunday)(?:\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm))))?\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NaturalWeekdayPrefix();
}
