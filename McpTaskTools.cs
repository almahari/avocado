using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Avocado;

public sealed record McpTask(
    Guid Id, string Text, bool IsCompleted, bool IsPinned, string Priority,
    DateTime CreatedAt, DateTime? CompletedAt, DateTime? DueAt,
    string? ReminderTime, string Recurrence, DateTime? SnoozedUntil, long ElapsedTicks)
{
    public static McpTask From(TodoItem task) => new(
        task.Id, task.Text, task.IsCompleted, task.IsPinned, task.Priority.ToString(),
        task.CreatedAt, task.CompletedAt, task.DueAt,
        task.ReminderTime?.ToString(@"hh\:mm"), task.Recurrence.ToString(),
        task.SnoozedUntil, task.ElapsedTicks);
}

public interface IMcpTaskStore
{
    Task<IReadOnlyList<McpTask>> ListMcpTasks(bool includeCompleted, CancellationToken cancellationToken);
    Task<McpTask> CreateMcpTask(string input, bool isPinned, CancellationToken cancellationToken);
    Task<McpTask> UpdateMcpTask(Guid id, string? text, string? priority, string? schedule,
        bool? isPinned, bool? isCompleted, CancellationToken cancellationToken);
}

[McpServerToolType]
public sealed class McpTaskTools(IMcpTaskStore store)
{
    [McpServerTool(Name = "list_tasks", ReadOnly = true, OpenWorld = false)]
    [Description("List Avocado tasks with stable IDs. By default returns active tasks; includeCompleted also returns the completed archive.")]
    public Task<IReadOnlyList<McpTask>> ListTasks(
        [Description("Include completed archived tasks.")] bool includeCompleted = false,
        CancellationToken cancellationToken = default) =>
        store.ListMcpTasks(includeCompleted, cancellationToken);

    [McpServerTool(Name = "create_task", OpenWorld = false)]
    [Description("Create one Avocado task. Input supports existing task syntax, e.g. 'tomorrow 9am !! Review report #work' or 'daily 09:00 Drink water'. Semicolons remain part of this single task.")]
    public Task<McpTask> CreateTask(
        [Description("Nonempty task input, optionally prefixed with a reminder and !, !!, or !!! priority.")] string input,
        bool isPinned = false, CancellationToken cancellationToken = default) =>
        store.CreateMcpTask(input, isPinned, cancellationToken);

    [McpServerTool(Name = "update_task", OpenWorld = false)]
    [Description("Update a task by its ID from list_tasks. Omitted or null fields are unchanged. Can complete active tasks or restore archived tasks. Text is literal; use schedule to change reminders.")]
    public Task<McpTask> UpdateTask(
        Guid id,
        [Description("Replacement task text; must be nonempty.")] string? text = null,
        [Description("None, Low, Medium, or High (case insensitive).")] string? priority = null,
        [Description("Reminder expression such as 'tomorrow 9am' or 'daily 09:00'. Empty string clears the reminder. Times use the computer's local timezone.")] string? schedule = null,
        bool? isPinned = null, bool? isCompleted = null,
        CancellationToken cancellationToken = default) =>
        store.UpdateMcpTask(id, text, priority, schedule, isPinned, isCompleted, cancellationToken);
}
