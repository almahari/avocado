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

    IEnumerable<TodoItem> IDesktopMascotHost.OpenTasks => _tasks.Where(task => !task.IsCompleted).ToList();

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

    private void NotifyDesktopMascotTheme() => _desktopMascot?.ApplyTheme();

    private void NotifyDesktopMascotReminder(IReadOnlyList<TodoItem> tasks) => _desktopMascot?.ShowReminder(tasks);

    private void NotifyDesktopMascotReminderResolved() => _desktopMascot?.DismissReminder();

    private void CloseDesktopMascot()
    {
        _desktopMascot?.CloseMascot();
        _desktopMascot = null;
    }
}
