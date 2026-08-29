using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Avocado;

public sealed class TodoItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isCompleted;
    private bool _isDragging;
    private bool _isExpanded;
    private bool _isActionsOpen;
    private long _elapsedTicks;
    private bool _isTimerRunning;
    private TimeSpan? _reminderTime;
    private DateOnly? _lastReminderDate;
    private DateTime? _snoozedUntil;
    private TaskRecurrence _recurrence;
    private TaskPriority _priority;
    private DateTime? _dueAt;
    private bool _isPinned;
    private DateTime? _completedAt;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }
    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }
    public long ElapsedTicks
    {
        get => _elapsedTicks;
        set
        {
            _elapsedTicks = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimerToolTip));
        }
    }
    public TimeSpan? ReminderTime
    {
        get => _reminderTime;
        set
        {
            _reminderTime = value;
            _lastReminderDate = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderLabel));
        }
    }
    public TaskRecurrence Recurrence
    {
        get => _recurrence;
        set
        {
            _recurrence = value;
            _lastReminderDate = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderLabel));
        }
    }
    public TaskPriority Priority
    {
        get => _priority;
        set
        {
            _priority = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PriorityLabel));
        }
    }
    public DateTime? DueAt
    {
        get => _dueAt;
        set
        {
            _dueAt = value;
            _lastReminderDate = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderLabel));
        }
    }
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PinIcon));
            OnPropertyChanged(nameof(PinToolTip));
        }
    }
    public DateTime? CompletedAt
    {
        get => _completedAt;
        set { _completedAt = value; OnPropertyChanged(); }
    }
    [JsonIgnore]
    public string PriorityLabel => TaskReminderLogic.PriorityPrefix(Priority);
    [JsonIgnore]
    public string ReminderLabel => DueAt is DateTime dueAt
        ? TaskReminderLogic.FormatDueLabel(dueAt)
        : ReminderTime is TimeSpan time
            ? $"{TaskReminderLogic.Label(Recurrence)} {time:hh\\:mm}".TrimStart()
            : string.Empty;
    [JsonIgnore]
    public string PinIcon => IsPinned ? "◆" : "◇";
    [JsonIgnore]
    public string PinToolTip => IsPinned ? "Unpin task" : "Pin task";
    public DateOnly? LastReminderDate
    {
        get => _lastReminderDate;
        set => _lastReminderDate = value;
    }
    public DateTime? SnoozedUntil
    {
        get => _snoozedUntil;
        set => _snoozedUntil = value;
    }
    [JsonIgnore]
    public bool IsDragging
    {
        get => _isDragging;
        set { _isDragging = value; OnPropertyChanged(); }
    }
    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }
    [JsonIgnore]
    public bool IsActionsOpen
    {
        get => _isActionsOpen;
        set { _isActionsOpen = value; OnPropertyChanged(); }
    }
    [JsonIgnore]
    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        set
        {
            _isTimerRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimerIcon));
            OnPropertyChanged(nameof(TimerToolTip));
        }
    }
    [JsonIgnore]
    public string TimerIcon => IsTimerRunning ? "Ⅱ" : "◷";
    [JsonIgnore]
    public string TimerToolTip =>
        $"Task time: {TaskTimerLogic.Format(TimeSpan.FromTicks(ElapsedTicks))} • " +
        (IsTimerRunning ? "Pause timer" : "Start timer");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
