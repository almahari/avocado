namespace Avocado;

public static class TaskPaletteLogic
{
    private const string CreatePrefix = "task ";
    private const string SchedulePlaceholder = "__avocado_task__";

    public static string? GetCreateText(string input)
    {
        var trimmed = input.Trim();
        if (!trimmed.StartsWith(CreatePrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var taskText = trimmed[CreatePrefix.Length..].Trim();
        return taskText.Length == 0 ? null : taskText;
    }

    public static IReadOnlyList<TodoItem> Search(
        IEnumerable<TodoItem> tasks,
        string input,
        int limit = 8)
    {
        var query = input.Trim();
        return tasks
            .Where(task => !task.IsCompleted &&
                           (query.Length == 0 || task.Text.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(task => task.IsPinned)
            .ThenBy(task => task.Text, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public static bool TryParseSchedule(
        string input,
        DateTime referenceTime,
        out ParsedTaskInput schedule)
    {
        schedule = TaskReminderLogic.Parse($"{input.Trim()} {SchedulePlaceholder}", referenceTime);
        return schedule.Text == SchedulePlaceholder &&
               (schedule.ReminderTime is not null || schedule.DueAt is not null);
    }
}
