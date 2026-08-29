namespace Avocado;

public enum TaskFilterMode
{
    Active,
    Scheduled,
    RunningTimer
}

public static class TaskFilterLogic
{
    public static bool Matches(TodoItem task, string searchText, TaskFilterMode mode, string? category = null)
    {
        if (!string.IsNullOrWhiteSpace(searchText) &&
            !task.Text.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!TaskCategoryLogic.Matches(task.Text, category)) return false;

        return mode switch
        {
            TaskFilterMode.Scheduled => task.ReminderTime is not null || task.DueAt is not null,
            TaskFilterMode.RunningTimer => task.IsTimerRunning,
            _ => true
        };
    }
}
