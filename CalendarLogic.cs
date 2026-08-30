namespace Avocado;

public enum CalendarViewMode
{
    Day,
    Week
}

public sealed record CalendarOccurrence(TodoItem Task, DateTime At);

public static class CalendarLogic
{
    public static IReadOnlyList<CalendarOccurrence> GetOccurrences(
        IEnumerable<TodoItem> tasks,
        DateOnly start,
        int dayCount,
        DateTime referenceTime)
    {
        if (dayCount <= 0) return [];
        var end = start.AddDays(dayCount);
        var today = DateOnly.FromDateTime(referenceTime);
        var occurrences = new List<CalendarOccurrence>();

        foreach (var task in tasks.Where(task => !task.IsCompleted))
        {
            if (task.SnoozedUntil is DateTime snoozedUntil)
            {
                AddIfInRange(task, snoozedUntil, start, end, occurrences);
                continue;
            }

            if (task.DueAt is DateTime dueAt)
            {
                AddIfInRange(task, dueAt, start, end, occurrences);
                continue;
            }

            if (task.ReminderTime is not TimeSpan reminderTime) continue;
            if (task.Recurrence == TaskRecurrence.None)
            {
                if (task.LastReminderDate is null && today >= start && today < end)
                    occurrences.Add(new CalendarOccurrence(
                        task, today.ToDateTime(TimeOnly.FromTimeSpan(reminderTime))));
                continue;
            }

            for (var offset = 0; offset < dayCount; offset++)
            {
                var date = start.AddDays(offset);
                if (TaskReminderLogic.MatchesDay(task.Recurrence, date.DayOfWeek))
                    occurrences.Add(new CalendarOccurrence(
                        task, date.ToDateTime(TimeOnly.FromTimeSpan(reminderTime))));
            }
        }

        return occurrences
            .OrderBy(item => item.At)
            .ThenByDescending(item => item.Task.Priority)
            .ThenBy(item => item.Task.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static void AddIfInRange(
        TodoItem task,
        DateTime at,
        DateOnly start,
        DateOnly end,
        ICollection<CalendarOccurrence> occurrences)
    {
        var date = DateOnly.FromDateTime(at);
        if (date >= start && date < end) occurrences.Add(new CalendarOccurrence(task, at));
    }
}
