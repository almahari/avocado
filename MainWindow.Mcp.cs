using System.Windows.Threading;

namespace Avocado;

public partial class MainWindow : IMcpTaskStore
{
    public bool IsMcpServerEnabled => _state.McpServerEnabled;

    public void SetMcpServerEnabled(bool enabled)
    {
        _state.McpServerEnabled = enabled;
        SaveState();
    }

    public Task<IReadOnlyList<McpTask>> ListMcpTasks(bool includeCompleted, CancellationToken cancellationToken) =>
        Dispatcher.InvokeAsync<IReadOnlyList<McpTask>>(() =>
            (includeCompleted ? _tasks.Concat(_archivedTasks) : _tasks)
            .Select(McpTask.From).ToList(), DispatcherPriority.Normal, cancellationToken).Task;

    public Task<McpTask> CreateMcpTask(string input, bool isPinned, CancellationToken cancellationToken) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Task input must be nonempty.");
            var parsed = TaskReminderLogic.Parse(input);
            if (parsed.Text.Length == 0) throw new ArgumentException("Task text must be nonempty.");
            var task = new TodoItem
            {
                Text = parsed.Text, ReminderTime = parsed.ReminderTime, Recurrence = parsed.Recurrence,
                Priority = parsed.Priority, DueAt = parsed.DueAt, IsPinned = isPinned
            };
            if (isPinned) _tasks.Insert(0, task);
            else _tasks.Add(task);
            RefreshTaskView();
            SaveState();
            RefreshAdaptivePersonality();
            return McpTask.From(task);
        }, DispatcherPriority.Normal, cancellationToken).Task;

    public Task<McpTask> UpdateMcpTask(Guid id, string? text, string? priority, string? schedule,
        bool? isPinned, bool? isCompleted, CancellationToken cancellationToken) =>
        Dispatcher.InvokeAsync(() =>
        {
            var task = _tasks.Concat(_archivedTasks).FirstOrDefault(item => item.Id == id)
                ?? throw new ArgumentException($"Task {id} was not found.");
            // Validate every field before mutating anything.
            if (text is not null && string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Task text must be nonempty.");
            var parsedPriority = task.Priority;
            if (priority is not null && (!Enum.TryParse(priority, true, out parsedPriority) ||
                !Enum.IsDefined(parsedPriority) || !Enum.GetNames<TaskPriority>().Any(
                    name => name.Equals(priority, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("Priority must be None, Low, Medium, or High.");
            ParsedTaskInput parsedSchedule = default;
            if (!string.IsNullOrWhiteSpace(schedule) &&
                !TaskPaletteLogic.TryParseSchedule(schedule, DateTime.Now, out parsedSchedule))
                throw new ArgumentException("Schedule must be a reminder expression such as 'tomorrow 9am' or 'daily 09:00'.");

            if (text is not null) task.Text = text.Trim();
            if (priority is not null) task.Priority = parsedPriority;
            if (schedule is not null)
            {
                task.ReminderTime = parsedSchedule.ReminderTime;
                task.Recurrence = parsedSchedule.Recurrence;
                task.DueAt = parsedSchedule.DueAt;
                task.SnoozedUntil = null;
                task.LastReminderDate = null;
                ResolveAlertingTask(task);
            }
            if (isPinned is bool pinned && pinned != task.IsPinned)
            {
                task.IsPinned = pinned;
                if (_tasks.Remove(task))
                {
                    if (pinned) _tasks.Insert(0, task);
                    else _tasks.Add(task);
                }
            }
            if (isCompleted is bool completed && completed != task.IsCompleted)
            {
                if (completed) CompletePaletteTask(task);
                else RestoreArchivedTask(task);
            }
            RefreshTaskView();
            SaveState();
            RefreshAdaptivePersonality();
            return McpTask.From(task);
        }, DispatcherPriority.Normal, cancellationToken).Task;
}
