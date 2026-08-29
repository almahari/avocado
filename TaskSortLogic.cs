namespace Avocado;

public static class TaskSortLogic
{
    public static IReadOnlyList<TodoItem> ByPriority(IEnumerable<TodoItem> tasks) =>
        tasks.OrderByDescending(task => task.IsPinned)
            .ThenByDescending(task => task.Priority).ToList();

    public static IReadOnlyList<TodoItem> ByTime(IEnumerable<TodoItem> tasks) =>
        tasks.OrderByDescending(task => task.IsPinned)
            .ThenBy(task => task.DueAt is null && task.ReminderTime is null)
            .ThenBy(task => task.DueAt ?? (task.ReminderTime is TimeSpan time
                ? DateTime.Today.Add(time)
                : DateTime.MaxValue))
            .ToList();
}
