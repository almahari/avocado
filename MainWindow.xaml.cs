using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Avocado;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int VisibleTaskLimit = 5;
    private const double CollapsedTaskHeight = 33;
    private readonly ObservableCollection<TodoItem> _tasks = [];
    private readonly AppStateStore _store = new();
    private readonly DispatcherTimer _locationSaveTimer;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly DispatcherTimer _taskTimer;
    private readonly DispatcherTimer _reminderTimer;
    private AppState _state;
    private bool _allowClose;
    private string _overflowLabel = string.Empty;
    private System.Windows.Point _taskDragStart;
    private TodoItem? _draggedTask;
    private List<TodoItem>? _dragOriginalOrder;
    private bool _taskOrderChangedDuringDrag;
    private bool _taskDragStarted;
    private TodoItem? _expandedTask;
    private TodoItem? _editingTask;
    private bool _isSleeping;
    private int _sizeAnimationVersion;
    private TodoItem? _activeTimerTask;
    private long _activeTimerBaseTicks;
    private long _activeTimerStartedAt;
    private string _headerText = "AVOCADO";
    private FruitThemePalette _currentTheme = FruitThemes.Default;
    private bool _isShaking;
    private double _shakeOriginalLeft;
    private DispatcherTimer? _pendingShakeTimer;
    private GlobalQuickAddHotkey? _globalQuickAddHotkey;
    private readonly List<TodoItem> _alertingTasks = [];
    private string _alertTaskLabel = string.Empty;

    public ObservableCollection<TodoItem> Tasks => _tasks;
    public string OverflowLabel
    {
        get => _overflowLabel;
        private set { _overflowLabel = value; OnPropertyChanged(); }
    }
    public string HeaderText
    {
        get => _headerText;
        private set { _headerText = value; OnPropertyChanged(); }
    }
    public string AlertTaskLabel
    {
        get => _alertTaskLabel;
        private set { _alertTaskLabel = value; OnPropertyChanged(); }
    }
    public bool IsAlwaysOnTop => _state.AlwaysOnTop;
    public bool IsSmallSize => _state.SmallSize;
    public bool IsResizeWhenInactive => _state.ResizeWhenInactive;
    public SleepTimeOption CurrentSleepTime => InactivitySettings.Get(_state.SleepTime).Option;
    public SleepResizeAnchor CurrentSleepResizeAnchor => SleepResizeLogic.Normalize(_state.SleepResizeAnchor);
    public ReminderSoundMode CurrentReminderSound => ReminderSoundSettings.Normalize(_state.ReminderSound);
    public FruitThemeKind CurrentTheme => _currentTheme.Kind;

    public event EventHandler? HideRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        _state = _store.Load();
        foreach (var item in _state.Tasks) _tasks.Add(item);
        _locationSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _locationSaveTimer.Tick += (_, _) => SaveLocationNow();
        _inactivityTimer = new DispatcherTimer();
        _inactivityTimer.Tick += (_, _) => EnterSleepMode();
        _taskTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _taskTimer.Tick += (_, _) => RefreshActiveTimer();
        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _reminderTimer.Tick += (_, _) => CheckTaskReminders();
        InitializeComponent();
        DataContext = this;
        SetSleepTime(_state.SleepTime, persist: false);
        SetSleepResizeAnchor(_state.SleepResizeAnchor, persist: false);
        SetReminderSound(_state.ReminderSound, persist: false);
        SetTheme(_state.Theme, persist: false);
        RefreshOverflow();
        _reminderTimer.Start();
    }

    public void SetAlwaysOnTop(bool value, bool persist = true)
    {
        Topmost = value;
        _state.AlwaysOnTop = value;
        if (persist) SaveState();
    }

    public void SetSmallSize(bool small, bool persist = true)
    {
        _state.SmallSize = small;
        if (_isSleeping) WakeFromInactivity();
        else ApplySizeImmediately(AppSizeLogic.Get(small));
        NotifyInteraction();
        if (persist) SaveState();
    }

    public void SetResizeWhenInactive(bool enabled, bool persist = true)
    {
        _state.ResizeWhenInactive = enabled;
        if (enabled) RestartInactivityTimer();
        else
        {
            _inactivityTimer.Stop();
            WakeFromInactivity();
        }
        if (persist) SaveState();
    }

    public void SetSleepTime(SleepTimeOption option, bool persist = true)
    {
        var choice = InactivitySettings.Get(option);
        _state.SleepTime = choice.Option;
        if (choice.Duration is TimeSpan duration)
        {
            _inactivityTimer.Interval = duration;
            if (_state.ResizeWhenInactive) RestartInactivityTimer();
        }
        else
        {
            _inactivityTimer.Stop();
            WakeFromInactivity();
        }
        if (persist) SaveState();
    }

    public void SetSleepResizeAnchor(SleepResizeAnchor anchor, bool persist = true)
    {
        _state.SleepResizeAnchor = SleepResizeLogic.Normalize(anchor);
        if (persist) SaveState();
    }

    public void SetReminderSound(ReminderSoundMode mode, bool persist = true)
    {
        _state.ReminderSound = ReminderSoundSettings.Normalize(mode);
        if (persist) SaveState();
    }

    public void SetTheme(FruitThemeKind kind, bool persist = true)
    {
        _currentTheme = FruitThemes.Get(kind);
        _state.Theme = _currentTheme.Kind;
        SetThemeBrush("InkBrush", _currentTheme.Ink);
        SetThemeBrush("CreamBrush", _currentTheme.Cream);
        SetThemeBrush("ButtonBrush", _currentTheme.Button);
        SetThemeBrush("ButtonBorderBrush", _currentTheme.ButtonBorder);
        SetThemeBrush("ButtonHoverBrush", _currentTheme.ButtonHover);
        SetThemeBrush("ButtonPressedBrush", _currentTheme.ButtonPressed);
        SetThemeBrush("OuterBrush", _currentTheme.Outer);
        SetThemeBrush("MiddleBrush", _currentTheme.Middle);
        SetThemeBrush("FleshBrush", _currentTheme.Flesh);
        SetThemeBrush("HighlightBrush", _currentTheme.Highlight);
        SetThemeBrush("AccentBrush", _currentTheme.Accent);
        SetThemeBrush("SeedBrush", _currentTheme.Seed);
        SetThemeBrush("SeedHighlightBrush", _currentTheme.SeedHighlight);
        SetThemeBrush("MutedInkBrush", _currentTheme.MutedInk);
        SetThemeBrush("TaskBrush", _currentTheme.Task);
        AvocadoShape.Visibility = _currentTheme.Kind == FruitThemeKind.Avocado ? Visibility.Visible : Visibility.Collapsed;
        StrawberryShape.Visibility = _currentTheme.Kind == FruitThemeKind.Strawberry ? Visibility.Visible : Visibility.Collapsed;
        OrangeShape.Visibility = _currentTheme.Kind == FruitThemeKind.Orange ? Visibility.Visible : Visibility.Collapsed;
        BlueberryShape.Visibility = _currentTheme.Kind == FruitThemeKind.Blueberry ? Visibility.Visible : Visibility.Collapsed;
        WatermelonShape.Visibility = _currentTheme.Kind == FruitThemeKind.Watermelon ? Visibility.Visible : Visibility.Collapsed;
        KiwiShape.Visibility = _currentTheme.Kind == FruitThemeKind.Kiwi ? Visibility.Visible : Visibility.Collapsed;
        PapayaShape.Visibility = _currentTheme.Kind == FruitThemeKind.Papaya ? Visibility.Visible : Visibility.Collapsed;
        AppleShape.Visibility = _currentTheme.Kind == FruitThemeKind.Apple ? Visibility.Visible : Visibility.Collapsed;
        MangoShape.Visibility = _currentTheme.Kind == FruitThemeKind.Mango ? Visibility.Visible : Visibility.Collapsed;
        LemonShape.Visibility = _currentTheme.Kind == FruitThemeKind.Lemon ? Visibility.Visible : Visibility.Collapsed;
        TomatoShape.Visibility = _currentTheme.Kind == FruitThemeKind.Tomato ? Visibility.Visible : Visibility.Collapsed;
        PumpkinShape.Visibility = _currentTheme.Kind == FruitThemeKind.Pumpkin ? Visibility.Visible : Visibility.Collapsed;
        PotatoShape.Visibility = _currentTheme.Kind == FruitThemeKind.Potato ? Visibility.Visible : Visibility.Collapsed;
        OnionShape.Visibility = _currentTheme.Kind == FruitThemeKind.Onion ? Visibility.Visible : Visibility.Collapsed;
        if (_activeTimerTask is null) HeaderText = _currentTheme.DisplayName.ToUpperInvariant();
        if (persist) SaveState();
    }

    private void SetThemeBrush(string resourceKey, string colorValue)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorValue));
        brush.Freeze();
        Resources[resourceKey] = brush;
    }

    public void NotifyInteraction()
    {
        WakeFromInactivity();
        RestartInactivityTimer();
    }

    private void RestartInactivityTimer()
    {
        _inactivityTimer.Stop();
        if (!_state.ResizeWhenInactive ||
            InactivitySettings.Get(_state.SleepTime).Duration is null) return;
        _inactivityTimer.Start();
    }

    private void EnterSleepMode()
    {
        _inactivityTimer.Stop();
        if (!_state.ResizeWhenInactive || _isSleeping) return;
        _isSleeping = true;
        CollapseExpandedTask();
        AwakeContent.Visibility = Visibility.Collapsed;
        SleepOverlay.Visibility = Visibility.Visible;
        SleepOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        AnimateToSize(AppSizeLogic.Sleeping);
    }

    private void WakeFromInactivity()
    {
        if (!_isSleeping) return;
        _isSleeping = false;
        SleepOverlay.BeginAnimation(OpacityProperty, null);
        SleepOverlay.Visibility = Visibility.Collapsed;
        AwakeContent.Visibility = Visibility.Visible;
        AwakeContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(200)));
        AnimateToSize(AppSizeLogic.Get(_state.SmallSize));
    }

    private void ApplySizeImmediately(AppSize size)
    {
        _sizeAnimationVersion++;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Root.LayoutTransform = size.Scale == 1
            ? Transform.Identity
            : new ScaleTransform(size.Scale, size.Scale);
        Width = size.Width;
        Height = size.Height;
        KeepWindowOnVirtualScreen();
    }

    private void AnimateToSize(AppSize target)
    {
        var version = ++_sizeAnimationVersion;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        var startingWidth = ActualWidth > 0 ? ActualWidth : Width;
        var startingHeight = ActualHeight > 0 ? ActualHeight : Height;
        var startingSize = new AppSize(startingWidth, startingHeight, 1);
        var targetPosition = SleepResizeLogic.GetTargetPosition(
            Left, Top, startingSize, target, _state.SleepResizeAnchor);
        var startingScale = Root.LayoutTransform is ScaleTransform currentScale
            ? currentScale.ScaleX
            : startingWidth / AppSizeLogic.Get(small: false).Width;
        var scale = new ScaleTransform(startingScale, startingScale);
        Root.LayoutTransform = scale;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        BeginAnimation(WidthProperty,
            new DoubleAnimation(startingWidth, target.Width, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing });
        BeginAnimation(LeftProperty,
            new DoubleAnimation(Left, targetPosition.Left, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing });
        BeginAnimation(TopProperty,
            new DoubleAnimation(Top, targetPosition.Top, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing });
        var heightAnimation = new DoubleAnimation(startingHeight, target.Height, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing };
        heightAnimation.Completed += (_, _) =>
        {
            if (version != _sizeAnimationVersion) return;
            ApplySizeImmediately(target);
            Left = targetPosition.Left;
            Top = targetPosition.Top;
            KeepWindowOnVirtualScreen();
        };
        BeginAnimation(HeightProperty, heightAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(startingScale, target.Scale, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(startingScale, target.Scale, InactivitySettings.ResizeAnimationDuration)
            { EasingFunction = easing });
    }

    private void KeepWindowOnVirtualScreen()
    {
        if (!IsLoaded) return;
        var maximumLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width;
        var maximumTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;
        Left = Math.Clamp(Left, SystemParameters.VirtualScreenLeft, maximumLeft);
        Top = Math.Clamp(Top, SystemParameters.VirtualScreenTop, maximumTop);
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _globalQuickAddHotkey = new GlobalQuickAddHotkey(this, OpenFromGlobalQuickAdd);
    }

    protected override void OnClosed(EventArgs e)
    {
        _globalQuickAddHotkey?.Dispose();
        _globalQuickAddHotkey = null;
        base.OnClosed(e);
    }

    private void OpenFromGlobalQuickAdd()
    {
        NotifyInteraction();
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        OpenTaskEditor();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        PauseActiveTimer(persist: false);
        SaveLocationNow();
        base.OnClosing(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Topmost = _state.AlwaysOnTop;
        if (_state.Left is double left && _state.Top is double top && IsOnVirtualScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = SystemParameters.WorkArea.Right - Width - 28;
            Top = SystemParameters.WorkArea.Bottom - Height - 28;
        }
    }

    private bool IsOnVirtualScreen(double left, double top) =>
        left + Width > SystemParameters.VirtualScreenLeft &&
        left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
        top + Height > SystemParameters.VirtualScreenTop &&
        top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;
        _locationSaveTimer.Stop();
        _locationSaveTimer.Start();
    }

    private void SaveLocationNow()
    {
        _locationSaveTimer.Stop();
        _state.Left = Left;
        _state.Top = Top;
        SaveState();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (e.ButtonState != MouseButtonState.Pressed ||
            HasActionControlAncestor(source) || IsWithinTaskRow(source)) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StopShaking();
        var wasSleeping = _isSleeping;
        NotifyInteraction();
        if (wasSleeping)
        {
            e.Handled = true;
            return;
        }
        if (!IsWithinTaskRow(e.OriginalSource as DependencyObject)) CollapseExpandedTask();
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var wasSleeping = _isSleeping;
        NotifyInteraction();
        if (wasSleeping) e.Handled = true;
    }

    private void Window_Deactivated(object? sender, EventArgs e) => CollapseExpandedTask();

    private static bool HasActionControlAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button or System.Windows.Controls.TextBox or
                System.Windows.Controls.CheckBox or Hyperlink) return true;
            source = GetUiParent(source);
        }
        return false;
    }

    private static bool IsWithinTaskRow(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Name: "TaskRow" }) return true;
            source = GetUiParent(source);
        }
        return false;
    }

    private static DependencyObject? GetUiParent(DependencyObject source)
    {
        if (source is ContentElement content)
            return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;
        return source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
        => OpenTaskEditor();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var wasSleeping = _isSleeping;
        NotifyInteraction();
        if (wasSleeping)
        {
            e.Handled = true;
            return;
        }
        if (e.Key != Key.N || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        OpenTaskEditor();
        e.Handled = true;
    }

    private void OpenTaskEditor(TodoItem? task = null)
    {
        _editingTask = task;
        TaskInput.Text = task?.ReminderTime is TimeSpan reminderTime
            ? $"{TaskReminderLogic.Prefix(task.Recurrence)} {reminderTime:hh\\:mm} {task.Text}".TrimStart()
            : task?.Text ?? string.Empty;
        AddPanel.Visibility = Visibility.Visible;
        TaskInput.Focus();
        TaskInput.SelectAll();
    }

    private void ConfirmAddButton_Click(object sender, RoutedEventArgs e) => AddTaskFromInput();

    private void TaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddTaskFromInput();
        else if (e.Key == Key.Escape)
        {
            _editingTask = null;
            AddPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void AddTaskFromInput()
    {
        var parsed = TaskReminderLogic.Parse(TaskInput.Text);
        if (parsed.Text.Length == 0) return;
        if (_editingTask is TodoItem editingTask)
        {
            editingTask.Text = parsed.Text;
            editingTask.ReminderTime = parsed.ReminderTime;
            editingTask.Recurrence = parsed.Recurrence;
            _editingTask = null;
            AddPanel.Visibility = Visibility.Collapsed;
            SaveState();
            return;
        }
        _tasks.Add(new TodoItem
        {
            Text = parsed.Text,
            ReminderTime = parsed.ReminderTime,
            Recurrence = parsed.Recurrence
        });
        AddPanel.Visibility = Visibility.Collapsed;
        RefreshOverflow();
        SaveState();
        Dispatcher.BeginInvoke(TaskScrollViewer.ScrollToEnd, DispatcherPriority.Loaded);
    }

    private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        SaveState();
    }

    private void TaskTimerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;

        if (ReferenceEquals(_activeTimerTask, task))
        {
            PauseActiveTimer();
            return;
        }

        PauseActiveTimer(persist: false);
        _activeTimerTask = task;
        _activeTimerBaseTicks = task.ElapsedTicks;
        _activeTimerStartedAt = Stopwatch.GetTimestamp();
        task.IsTimerRunning = true;
        _taskTimer.Start();
        RefreshActiveTimer();
        SaveState();
    }

    private void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        CollapseExpandedTask();
        OpenTaskEditor(task);
    }

    private void RefreshActiveTimer()
    {
        if (_activeTimerTask is not TodoItem task) return;
        var sessionElapsed = Stopwatch.GetElapsedTime(_activeTimerStartedAt);
        task.ElapsedTicks = _activeTimerBaseTicks + sessionElapsed.Ticks;
        HeaderText = TaskTimerLogic.Format(TimeSpan.FromTicks(task.ElapsedTicks));
    }

    private void PauseActiveTimer(bool persist = true)
    {
        if (_activeTimerTask is not TodoItem task) return;
        RefreshActiveTimer();
        _taskTimer.Stop();
        task.IsTimerRunning = false;
        _activeTimerTask = null;
        HeaderText = _currentTheme.DisplayName.ToUpperInvariant();
        if (persist) SaveState();
    }

    private void CheckTaskReminders()
    {
        var now = DateTime.Now;
        var dueTasks = _tasks
            .Where(task => !task.IsCompleted &&
                           (TaskReminderLogic.IsSnoozeDue(task.SnoozedUntil, now) ||
                            task.ReminderTime is TimeSpan reminderTime &&
                            TaskReminderLogic.MatchesDay(task.Recurrence, now.DayOfWeek) &&
                            TaskReminderLogic.IsDue(reminderTime, now, task.LastReminderDate)))
            .ToList();
        if (dueTasks.Count == 0) return;

        var today = DateOnly.FromDateTime(now);
        foreach (var task in dueTasks)
        {
            task.LastReminderDate = today;
            task.SnoozedUntil = null;
        }
        _alertingTasks.Clear();
        _alertingTasks.AddRange(dueTasks);
        AlertTaskLabel = dueTasks.Count == 1
            ? dueTasks[0].Text
            : $"{dueTasks.Count} tasks are due";
        SnoozePanel.Visibility = Visibility.Visible;
        SaveState();
        var wasSleeping = _isSleeping;
        NotifyInteraction();
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (wasSleeping) ScheduleShakeAfterWake();
        else ShakeWindow();
        PlayReminderSound();
    }

    private void PlayReminderSound()
    {
        switch (CurrentReminderSound)
        {
            case ReminderSoundMode.Silent:
                return;
            case ReminderSoundMode.Soft:
                SystemSounds.Beep.Play();
                return;
            default:
                var sound = ((int)_currentTheme.Kind % 4) switch
                {
                    0 => SystemSounds.Asterisk,
                    1 => SystemSounds.Exclamation,
                    2 => SystemSounds.Question,
                    _ => SystemSounds.Hand
                };
                sound.Play();
                return;
        }
    }

    private void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string minutesText } ||
            !int.TryParse(minutesText, out var minutes)) return;
        var snoozedUntil = DateTime.Now.AddMinutes(minutes);
        foreach (var task in _alertingTasks) task.SnoozedUntil = snoozedUntil;
        CloseReminderPanel();
        SaveState();
    }

    private void DismissReminderButton_Click(object sender, RoutedEventArgs e)
    {
        CloseReminderPanel();
        SaveState();
    }

    private void CloseReminderPanel()
    {
        StopShaking();
        _alertingTasks.Clear();
        SnoozePanel.Visibility = Visibility.Collapsed;
    }

    private void ScheduleShakeAfterWake()
    {
        StopShaking();
        var timer = new DispatcherTimer
        {
            Interval = InactivitySettings.ResizeAnimationDuration + TimeSpan.FromMilliseconds(25)
        };
        _pendingShakeTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(_pendingShakeTimer, timer)) return;
            _pendingShakeTimer = null;
            ShakeWindow();
        };
        timer.Start();
    }

    private void ShakeWindow()
    {
        StopShaking();
        _shakeOriginalLeft = Left;
        _isShaking = true;
        var animation = new DoubleAnimation(_shakeOriginalLeft - 6, _shakeOriginalLeft + 6, TimeSpan.FromMilliseconds(80))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(TaskReminderLogic.ShakeDuration)
        };
        animation.Completed += (_, _) => StopShaking();
        BeginAnimation(LeftProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StopShaking()
    {
        _pendingShakeTimer?.Stop();
        _pendingShakeTimer = null;
        if (!_isShaking) return;
        _isShaking = false;
        BeginAnimation(LeftProperty, null);
        Left = _shakeOriginalLeft;
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TodoItem item })
        {
            if (ReferenceEquals(_editingTask, item))
            {
                _editingTask = null;
                AddPanel.Visibility = Visibility.Collapsed;
            }
            if (ReferenceEquals(_activeTimerTask, item)) PauseActiveTimer(persist: false);
            if (ReferenceEquals(_expandedTask, item)) _expandedTask = null;
            _tasks.Remove(item);
            RefreshOverflow();
            SaveState();
        }
    }

    private void TaskRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HasActionControlAncestor(e.OriginalSource as DependencyObject))
        {
            _draggedTask = null;
            return;
        }

        _taskDragStart = e.GetPosition(this);
        _draggedTask = (sender as FrameworkElement)?.DataContext as TodoItem;
        _taskDragStarted = false;
    }

    private void TaskRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _taskDragStarted ||
            HasActionControlAncestor(e.OriginalSource as DependencyObject) ||
            sender is not FrameworkElement { DataContext: TodoItem task } row) return;

        if (task.IsExpanded)
        {
            CollapseTask(task, row);
            return;
        }

        if (!TaskTextIsClipped(row)) return;
        CollapseExpandedTask();
        ExpandTask(task, row);
        e.Handled = true;
    }

    private void TaskRow_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTask is null) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _taskDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _taskDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _taskDragStarted = true;
        var task = _draggedTask;
        _dragOriginalOrder = _tasks.ToList();
        _taskOrderChangedDuringDrag = false;
        task.IsDragging = true;

        try
        {
            var result = System.Windows.DragDrop.DoDragDrop(
                (DependencyObject)sender, task, System.Windows.DragDropEffects.Move);

            if (result == System.Windows.DragDropEffects.None && _taskOrderChangedDuringDrag)
                RestoreOriginalTaskOrder();
            else if (_taskOrderChangedDuringDrag)
                SaveState();
        }
        finally
        {
            task.IsDragging = false;
            _draggedTask = null;
            _dragOriginalOrder = null;
            _taskOrderChangedDuringDrag = false;
        }
    }

    private void TaskRow_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TodoItem))) return;
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;

        var position = e.GetPosition(TaskScrollViewer);
        if (position.Y < 24) TaskScrollViewer.LineUp();
        else if (position.Y > TaskScrollViewer.ViewportHeight - 24) TaskScrollViewer.LineDown();

        if (sender is FrameworkElement { DataContext: TodoItem target } row &&
            e.Data.GetData(typeof(TodoItem)) is TodoItem moving)
        {
            var insertAfter = e.GetPosition(row).Y > row.ActualHeight / 2;
            if (ReorderTaskWithAnimation(moving, target, insertAfter))
                _taskOrderChangedDuringDrag = true;
        }
    }

    private void TaskRow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TodoItem target } row ||
            e.Data.GetData(typeof(TodoItem)) is not TodoItem moving) return;

        var insertAfter = e.GetPosition(row).Y > row.ActualHeight / 2;
        if (ReorderTaskWithAnimation(moving, target, insertAfter))
            _taskOrderChangedDuringDrag = true;
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private bool ReorderTaskWithAnimation(TodoItem moving, TodoItem target, bool insertAfter)
    {
        var previousPositions = CaptureTaskRowPositions();
        if (!TodoListLogic.Reorder(_tasks, moving, target, insertAfter)) return false;

        Dispatcher.BeginInvoke(
            () => AnimateTaskRows(previousPositions),
            DispatcherPriority.Render);
        return true;
    }

    private Dictionary<Guid, double> CaptureTaskRowPositions()
    {
        TaskItemsControl.UpdateLayout();
        var positions = new Dictionary<Guid, double>();
        foreach (var task in _tasks)
        {
            if (FindTaskRow(task) is not FrameworkElement row || !row.IsVisible) continue;
            positions[task.Id] = row.TransformToAncestor(TaskItemsControl)
                .Transform(new System.Windows.Point()).Y;
        }
        return positions;
    }

    private void AnimateTaskRows(IReadOnlyDictionary<Guid, double> previousPositions)
    {
        TaskItemsControl.UpdateLayout();
        foreach (var task in _tasks)
        {
            if (task.IsDragging || !previousPositions.TryGetValue(task.Id, out var oldY) ||
                FindTaskRow(task) is not FrameworkElement row || !row.IsVisible) continue;

            var newY = row.TransformToAncestor(TaskItemsControl)
                .Transform(new System.Windows.Point()).Y;
            var offset = oldY - newY;
            if (Math.Abs(offset) < 0.5) continue;

            var transform = new TranslateTransform();
            row.RenderTransform = transform;
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                },
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private FrameworkElement? FindTaskRow(TodoItem task)
    {
        if (TaskItemsControl.ItemContainerGenerator.ContainerFromItem(task) is not ContentPresenter presenter)
            return null;
        presenter.ApplyTemplate();
        return presenter.ContentTemplate?.FindName("TaskRow", presenter) as FrameworkElement;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is T descendant) return descendant;
        }
        return null;
    }

    private static bool TaskTextIsClipped(FrameworkElement row)
    {
        if (FindVisualChild<LinkTextBlock>(row) is not LinkTextBlock text || text.ActualWidth <= 0) return false;
        text.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        return text.DesiredSize.Width > text.ActualWidth + 1;
    }

    private void ExpandTask(TodoItem task, FrameworkElement row)
    {
        var startingHeight = Math.Max(CollapsedTaskHeight, row.ActualHeight);
        task.IsExpanded = true;
        _expandedTask = task;

        row.BeginAnimation(HeightProperty, null);
        row.Height = double.NaN;
        row.Measure(new System.Windows.Size(Math.Max(1, row.ActualWidth), double.PositiveInfinity));
        var expandedHeight = Math.Max(CollapsedTaskHeight, row.DesiredSize.Height);
        row.Height = startingHeight;

        var animation = new DoubleAnimation(startingHeight, expandedHeight, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) =>
        {
            row.BeginAnimation(HeightProperty, null);
            if (task.IsExpanded) row.Height = double.NaN;
        };
        row.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void CollapseExpandedTask()
    {
        if (_expandedTask is not TodoItem task) return;
        if (FindTaskRow(task) is FrameworkElement row) CollapseTask(task, row);
        else task.IsExpanded = false;
        _expandedTask = null;
    }

    private void CollapseTask(TodoItem task, FrameworkElement row)
    {
        var startingHeight = Math.Max(CollapsedTaskHeight, row.ActualHeight);
        task.IsExpanded = false;
        if (ReferenceEquals(_expandedTask, task)) _expandedTask = null;

        row.BeginAnimation(HeightProperty, null);
        row.Height = startingHeight;
        var animation = new DoubleAnimation(startingHeight, CollapsedTaskHeight, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) =>
        {
            row.BeginAnimation(HeightProperty, null);
            if (!task.IsExpanded) row.Height = CollapsedTaskHeight;
        };
        row.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void RestoreOriginalTaskOrder()
    {
        if (_dragOriginalOrder is null) return;
        var previousPositions = CaptureTaskRowPositions();
        _tasks.Clear();
        foreach (var task in _dragOriginalOrder) _tasks.Add(task);
        Dispatcher.BeginInvoke(
            () => AnimateTaskRows(previousPositions),
            DispatcherPriority.Render);
    }

    private void RefreshOverflow()
    {
        var hidden = TodoListLogic.HiddenCount(_tasks.Count, VisibleTaskLimit);
        OverflowLabel = hidden > 0 ? $"+{hidden} more" : string.Empty;
    }

    private void SaveState()
    {
        _state.Tasks = _tasks.ToList();
        _store.Save(_state);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
