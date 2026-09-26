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
            .Where(task => !task.IsCompleted)
            .Select(task => (Task: task, Score: query.Length == 0 ? 1 : PaletteSearchLogic.Score(query, task.Text)))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Task.IsPinned)
            .ThenBy(match => match.Task.Text, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(match => match.Task)
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
