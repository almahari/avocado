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

public readonly record struct ParsedTaskInput(
    string Text,
    TimeSpan? ReminderTime,
    TaskRecurrence Recurrence = TaskRecurrence.None);

public static partial class TaskReminderLogic
{
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromSeconds(10);

    public static ParsedTaskInput Parse(string input)
    {
        var trimmed = input.Trim();
        var recurringMatch = RecurringPrefix().Match(trimmed);
        if (recurringMatch.Success)
        {
            var recurrence = ParseRecurrence(recurringMatch.Groups["recurrence"].Value);
            return new ParsedTaskInput(
                recurringMatch.Groups["text"].Value.Trim(),
                ParseTime(recurringMatch.Groups["time"].Value),
                recurrence);
        }
        var match = ReminderPrefix().Match(trimmed);
        if (!match.Success) return new ParsedTaskInput(trimmed, null);
        return new ParsedTaskInput(
            match.Groups["text"].Value.Trim(),
            ParseTime(match.Groups["time"].Value),
            TaskRecurrence.Daily);
    }

    public static bool IsDue(TimeSpan reminderTime, DateTime now, DateOnly? lastTriggeredDate) =>
        reminderTime.Hours == now.Hour &&
        reminderTime.Minutes == now.Minute &&
        lastTriggeredDate != DateOnly.FromDateTime(now);

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

    private static TimeSpan ParseTime(string value) =>
        TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);

    private static TaskRecurrence ParseRecurrence(string value) =>
        Enum.Parse<TaskRecurrence>(value, ignoreCase: true);

    [GeneratedRegex(@"^(?<time>(?:[01]\d|2[0-3]):[0-5]\d)\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReminderPrefix();

    [GeneratedRegex(@"^(?<recurrence>daily|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(?<time>(?:[01]\d|2[0-3]):[0-5]\d)\s+(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecurringPrefix();
}
