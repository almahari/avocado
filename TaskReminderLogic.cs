using System.Globalization;
using System.Text.RegularExpressions;

namespace Avocado;

public readonly record struct ParsedTaskInput(string Text, TimeSpan? ReminderTime);

public static partial class TaskReminderLogic
{
    public static readonly TimeSpan ShakeDuration = TimeSpan.FromSeconds(10);

    public static ParsedTaskInput Parse(string input)
    {
        var trimmed = input.Trim();
        var match = ReminderPrefix().Match(trimmed);
        if (!match.Success) return new ParsedTaskInput(trimmed, null);

        var time = TimeSpan.ParseExact(
            match.Groups["time"].Value,
            @"hh\:mm",
            CultureInfo.InvariantCulture);
        return new ParsedTaskInput(match.Groups["text"].Value.Trim(), time);
    }

    public static bool IsDue(TimeSpan reminderTime, DateTime now, DateOnly? lastTriggeredDate) =>
        reminderTime.Hours == now.Hour &&
        reminderTime.Minutes == now.Minute &&
        lastTriggeredDate != DateOnly.FromDateTime(now);

    [GeneratedRegex(@"^(?<time>(?:[01]\d|2[0-3]):[0-5]\d)\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReminderPrefix();
}
