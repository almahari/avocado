namespace Avocado;

public partial class MainWindow : IDesktopMascotHost
{
    private DesktopMascotWindow? _desktopMascot;

    /// <summary>Raised when the mascot asks to open the main window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Raised when the mascot is shown or hidden so the tray menu can stay in sync.</summary>
    public event EventHandler? DesktopMascotChanged;

    public bool IsDesktopMascotEnabled => _state.DesktopMascotEnabled;

    public void SetDesktopMascot(bool enabled, bool persist = true)
    {
        _state.DesktopMascotEnabled = enabled;
        if (enabled && _desktopMascot is null)
        {
            _desktopMascot = new DesktopMascotWindow(this, _state.DesktopMascotLeft, _state.DesktopMascotTop);
            _desktopMascot.Show();
        }
        else if (!enabled && _desktopMascot is not null)
        {
            _desktopMascot.CloseMascot();
            _desktopMascot = null;
        }
        if (persist) SaveState();
        DesktopMascotChanged?.Invoke(this, EventArgs.Empty);
    }

    IEnumerable<TodoItem> IDesktopMascotHost.OpenTasks => _tasks.Where(task => !task.IsCompleted && !IsHiddenToday(task)).ToList();

    IEnumerable<TodoItem> IDesktopMascotHost.DigestTasks => _tasks.Where(task => !task.IsCompleted).ToList();

    FruitThemePalette IDesktopMascotHost.MascotTheme => _currentTheme;

    bool IDesktopMascotHost.IsMascotQuiet => DoNotDisturbSettings.IsActive(_state.DoNotDisturb, DateTime.Now) || _isSleeping;

    void IDesktopMascotHost.CompleteMascotTask(TodoItem task) => CompletePaletteTask(task);

    void IDesktopMascotHost.SnoozeMascotTask(TodoItem task, int minutes) => SnoozePaletteTask(task, minutes);

    void IDesktopMascotHost.ShowMainFromMascot() => ShowRequested?.Invoke(this, EventArgs.Empty);

    void IDesktopMascotHost.SaveMascotPosition(double left, double top)
    {
        _state.DesktopMascotLeft = left;
        _state.DesktopMascotTop = top;
        SaveState();
    }

    void IDesktopMascotHost.HideMascotFromMascot() => SetDesktopMascot(false);

    // ---------- Mascot feature settings (tray toggles) ----------

    public DesktopMascotSettings DesktopMascotSettings => _state.DesktopMascot;

    public bool IsDesktopMascotFeatureEnabled(DesktopMascotFeature feature) => _state.DesktopMascot.IsEnabled(feature);

    public void SetDesktopMascotFeature(DesktopMascotFeature feature, bool enabled)
    {
        _state.DesktopMascot.Features[feature] = enabled;
        SaveState();
        _desktopMascot?.ApplySettings();
    }

    public void SetDesktopMascotSize(DesktopMascotSize size)
    {
        _state.DesktopMascot.Size = size;
        SaveState();
        _desktopMascot?.ApplySettings();
    }

    DesktopMascotSettings IDesktopMascotHost.MascotSettings => _state.DesktopMascot;

    void IDesktopMascotHost.SaveMascotSettings() => SaveState();

    int IDesktopMascotHost.AddMascotTasks(string input)
    {
        var parsed = TaskReminderLogic.ParseMany(input).Where(task => task.Text.Length > 0).ToList();
        return CreatePaletteTask(input) ? parsed.Count : 0;
    }

    int IDesktopMascotHost.CompletedTodayCount =>
        _archivedTasks.Count(task => task.CompletedAt is DateTime at && at.Date == DateTime.Today);

    void IDesktopMascotHost.MoveMascotTaskToTomorrow(TodoItem task)
    {
        if (!_tasks.Contains(task) || task.Recurrence != TaskRecurrence.None) return;
        var tomorrow = DateTime.Today.AddDays(1);
        var time = task.DueAt?.TimeOfDay ?? task.ReminderTime ?? TimeSpan.FromHours(9);
        task.DueAt = tomorrow + time;
        task.ReminderTime = null;
        task.SnoozedUntil = null;
        task.LastReminderDate = null;
        SaveState();
        RefreshTaskView();
    }

    ReminderSoundMode IDesktopMascotHost.MascotSoundMode => CurrentReminderSound;

    SeasonalSkinKind IDesktopMascotHost.MascotSeasonalSkin => CurrentSeasonalSkin;

    private void NotifyDesktopMascotTaskCompleted() => _desktopMascot?.OnTaskCompleted();

    private void NotifyDesktopMascotTheme() => _desktopMascot?.ApplyTheme();

    private void NotifyDesktopMascotReminder(IReadOnlyList<TodoItem> tasks) => _desktopMascot?.ShowReminder(tasks);

    private void NotifyDesktopMascotReminderResolved() => _desktopMascot?.DismissReminder();

    private void CloseDesktopMascot()
    {
        _desktopMascot?.CloseMascot();
        _desktopMascot = null;
    }
}
