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
    Sunday
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
    DateTime? DueAt = null);

public static partial class TaskReminderLogic
{
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromSeconds(2);

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

    public static bool IsSnoozeDue(DateTime? snoozedUntil, DateTime now) =>
        snoozedUntil is DateTime due && now >= due;

    public static bool MatchesDay(TaskRecurrence recurrence, DayOfWeek dayOfWeek) =>
        recurrence is TaskRecurrence.None or TaskRecurrence.Daily ||
        recurrence.ToString().Equals(dayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase);

    public static string Prefix(TaskRecurrence recurrence) => recurrence switch
    {
        TaskRecurrence.None => string.Empty,
        TaskRecurrence.Daily => "daily",
        _ => recurrence.ToString().ToLowerInvariant()
    };

    public static string Label(TaskRecurrence recurrence) => recurrence switch
    {
        TaskRecurrence.None => string.Empty,
        TaskRecurrence.Daily => "D",
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
        string text, TimeSpan? time, TaskRecurrence recurrence, DateTime? dueAt = null)
    {
        var match = PriorityPrefixPattern().Match(text);
        if (!match.Success) return new ParsedTaskInput(text, time, recurrence, DueAt: dueAt);
        var priority = match.Groups["priority"].Value.Length switch
        {
            1 => TaskPriority.Low,
            2 => TaskPriority.Medium,
            _ => TaskPriority.High
        };
        return new ParsedTaskInput(match.Groups["text"].Value.Trim(), time, recurrence, priority, dueAt);
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

    [GeneratedRegex(@"^(?<priority>!{1,3})\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PriorityPrefixPattern();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm)))\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactDatePrefix();

    [GeneratedRegex(@"^(?<day>today|tomorrow)(?:\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm))))?\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelativeDatePrefix();

    [GeneratedRegex(@"^(?<next>next\s+)?(?<day>monday|tuesday|wednesday|thursday|friday|saturday|sunday)(?:\s+(?<time>(?:(?:[01]?\d|2[0-3]):[0-5]\d|(?:1[0-2]|0?[1-9])(?::[0-5]\d)?\s?(?:am|pm))))?\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NaturalWeekdayPrefix();
}
