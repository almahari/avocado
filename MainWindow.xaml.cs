using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
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
    private readonly ObservableCollection<TodoItem> _archivedTasks = [];
    private readonly ICollectionView _tasksView;
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
    private SeasonalSkin _currentSeasonalSkin = SeasonalSkins.Default;
    private bool _isShaking;
    private double _shakeOriginalLeft;
    private double _shakeOriginalTop;
    private DispatcherTimer? _pendingShakeTimer;
    private GlobalQuickAddHotkey? _globalQuickAddHotkey;
    private readonly List<TodoItem> _alertingTasks = [];
    private string _alertTaskLabel = string.Empty;
    private string _taskSearchText = string.Empty;
    private TaskFilterMode _taskFilterMode = TaskFilterMode.Active;
    private string _selectedCategoryFilter = "All";
    private bool _refreshingCategoryFilters;
    private string _sleepEyes = "─";
    private string _sleepMouth = "ᴗ";
    private string _sleepTimerText = string.Empty;
    private string _adaptiveEyes = string.Empty;
    private string _adaptiveMouth = string.Empty;
    private string _adaptiveStatus = string.Empty;
    private DateTime _happyUntil = DateTime.MinValue;
    private AdaptiveMood? _adaptiveMood;
    private DateOnly? _lastArchiveCleanupDate;
    private DateOnly _calendarAnchor = DateOnly.FromDateTime(DateTime.Now);
    private CalendarViewMode _calendarMode = CalendarViewMode.Day;
    private string _calendarTitle = string.Empty;
    private string _calendarEmptyText = string.Empty;

    public ObservableCollection<TodoItem> Tasks => _tasks;
    public ICollectionView TasksView => _tasksView;
    public ObservableCollection<TodoItem> ArchivedTasks => _archivedTasks;
    public ObservableCollection<string> AvailableCategoryFilters { get; } = ["All"];
    public ObservableCollection<CalendarDisplayItem> CalendarEntries { get; } = [];
    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (_refreshingCategoryFilters) return;
            var normalized = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (_selectedCategoryFilter == normalized) return;
            _selectedCategoryFilter = normalized;
            OnPropertyChanged();
            RefreshTaskView();
        }
    }
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
    public string SleepEyes
    {
        get => _sleepEyes;
        private set { _sleepEyes = value; OnPropertyChanged(); }
    }
    public string SleepMouth
    {
        get => _sleepMouth;
        private set { _sleepMouth = value; OnPropertyChanged(); }
    }
    public string SleepTimerText
    {
        get => _sleepTimerText;
        private set { _sleepTimerText = value; OnPropertyChanged(); }
    }
    public string SleepTaskCountText
    {
        get
        {
            var count = _tasks.Count(task => !task.IsCompleted);
            return count.ToString();
        }
    }
    public string AdaptiveEyes
    {
        get => _adaptiveEyes;
        private set { _adaptiveEyes = value; OnPropertyChanged(); }
    }
    public string AdaptiveMouth
    {
        get => _adaptiveMouth;
        private set { _adaptiveMouth = value; OnPropertyChanged(); }
    }
    public string AdaptiveStatus
    {
        get => _adaptiveStatus;
        private set { _adaptiveStatus = value; OnPropertyChanged(); }
    }
    public string CalendarTitle
    {
        get => _calendarTitle;
        private set { _calendarTitle = value; OnPropertyChanged(); }
    }
    public string CalendarEmptyText
    {
        get => _calendarEmptyText;
        private set { _calendarEmptyText = value; OnPropertyChanged(); }
    }
    public bool IsAlwaysOnTop => _state.AlwaysOnTop;
    public bool IsSmallSize => _state.SmallSize;
    public bool IsResizeWhenInactive => _state.ResizeWhenInactive;
    public SleepTimeOption CurrentSleepTime => InactivitySettings.Get(_state.SleepTime).Option;
    public SleepFruitSize CurrentSleepFruitSize => SleepFruitSizeLogic.Normalize(_state.SleepFruitSize);
    public SleepResizeAnchor CurrentSleepResizeAnchor => SleepResizeLogic.Normalize(_state.SleepResizeAnchor);
    public ReminderSoundMode CurrentReminderSound => ReminderSoundSettings.Normalize(_state.ReminderSound);
    public DoNotDisturbMode CurrentDoNotDisturb => DoNotDisturbSettings.Normalize(_state.DoNotDisturb);
    public ArchiveRetentionOption CurrentArchiveRetention =>
        ArchiveRetentionSettings.Get(_state.ArchiveRetention).Option;
    public bool IsAdaptivePersonalityEnabled => _state.AdaptivePersonalityEnabled;
    public FruitThemeKind CurrentTheme => _currentTheme.Kind;
    public SeasonalSkinKind CurrentSeasonalSkin => _currentSeasonalSkin.Kind;
    public GlobalShortcutGesture CurrentQuickAddShortcut =>
        GlobalShortcutSettings.Normalize(_state.QuickAddShortcut, GlobalShortcutSettings.QuickAddDefault);
    public GlobalShortcutGesture CurrentClipboardTaskShortcut =>
        GlobalShortcutSettings.Normalize(_state.ClipboardTaskShortcut, GlobalShortcutSettings.ClipboardTaskDefault);
    public GlobalShortcutGesture CurrentSleepNowShortcut =>
        GlobalShortcutSettings.Normalize(_state.SleepNowShortcut, GlobalShortcutSettings.SleepNowDefault);

    public event EventHandler? HideRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        _state = _store.Load();
        foreach (var item in _state.Tasks) _tasks.Add(item);
        foreach (var item in _state.ArchivedTasks) _archivedTasks.Add(item);
        _tasksView = CollectionViewSource.GetDefaultView(_tasks);
        _tasksView.Filter = item => item is TodoItem task &&
                                    TaskFilterLogic.Matches(task, _taskSearchText, _taskFilterMode,
                                        _selectedCategoryFilter);
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
        RefreshCategoryFilters();
        SetAdaptivePersonality(_state.AdaptivePersonalityEnabled, persist: false);
        SetSleepTime(_state.SleepTime, persist: false);
        SetSleepFruitSize(_state.SleepFruitSize, persist: false);
        SetSleepResizeAnchor(_state.SleepResizeAnchor, persist: false);
        SetReminderSound(_state.ReminderSound, persist: false);
        SetArchiveRetention(_state.ArchiveRetention, persist: false);
        SetTheme(_state.Theme, persist: false);
        SetSeasonalSkin(_state.SeasonalSkin, persist: false);
        _state.QuickAddShortcut = CurrentQuickAddShortcut;
        _state.ClipboardTaskShortcut = CurrentClipboardTaskShortcut;
        _state.SleepNowShortcut = CurrentSleepNowShortcut;
        RefreshShortcutToolTip();
        RefreshOverflow();
        if (_state.NeedsMigration)
        {
            _state.NeedsMigration = false;
            SaveState();
        }
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

    public void SetSleepFruitSize(SleepFruitSize size, bool persist = true)
    {
        _state.SleepFruitSize = SleepFruitSizeLogic.Normalize(size);
        if (_isSleeping) AnimateToSize(SleepFruitSizeLogic.Get(_state.SleepFruitSize));
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

    public void SetDoNotDisturb(DoNotDisturbMode mode, bool persist = true)
    {
        _state.DoNotDisturb = DoNotDisturbSettings.Normalize(mode);
        if (persist) SaveState();
    }

    public void SetArchiveRetention(ArchiveRetentionOption option, bool persist = true)
    {
        _state.ArchiveRetention = ArchiveRetentionSettings.Get(option).Option;
        var removed = CleanupArchivedTasks(DateTime.Now);
        if (persist || removed > 0) SaveState();
    }

    private int CleanupArchivedTasks(DateTime now)
    {
        _lastArchiveCleanupDate = DateOnly.FromDateTime(now);
        var removed = ArchiveRetentionSettings.RemoveExpired(_archivedTasks, _state.ArchiveRetention, now);
        if (removed == 0) return 0;
        ArchivePanel.Visibility = _archivedTasks.Count == 0 ? Visibility.Collapsed : ArchivePanel.Visibility;
        RefreshAdaptivePersonality();
        return removed;
    }

    public void SetAdaptivePersonality(bool enabled, bool persist = true)
    {
        _state.AdaptivePersonalityEnabled = enabled;
        AdaptiveFacePanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        RefreshAdaptivePersonality();
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
        var personality = FruitPersonalities.Get(_currentTheme.Kind);
        SleepEyes = personality.Eyes;
        SleepMouth = personality.Mouth;
        RefreshAdaptivePersonality();
        if (_activeTimerTask is null) HeaderText = _currentTheme.DisplayName.ToUpperInvariant();
        if (persist) SaveState();
    }

    public void SetSeasonalSkin(SeasonalSkinKind kind, bool persist = true)
    {
        _currentSeasonalSkin = SeasonalSkins.Get(kind);
        _state.SeasonalSkin = _currentSeasonalSkin.Kind;
        HalloweenPumpkinSkin.Visibility = _currentSeasonalSkin.Kind == SeasonalSkinKind.HalloweenPumpkin
            ? Visibility.Visible : Visibility.Collapsed;
        WinterCapSkin.Visibility = _currentSeasonalSkin.Kind == SeasonalSkinKind.WinterCap
            ? Visibility.Visible : Visibility.Collapsed;
        SpringBlossomSkin.Visibility = _currentSeasonalSkin.Kind == SeasonalSkinKind.SpringBlossom
            ? Visibility.Visible : Visibility.Collapsed;
        SummerShadesSkin.Visibility = _currentSeasonalSkin.Kind == SeasonalSkinKind.SummerShades
            ? Visibility.Visible : Visibility.Collapsed;
        if (persist) SaveState();
    }

    private void RefreshAdaptivePersonality()
    {
        if (!_state.AdaptivePersonalityEnabled) return;
        var now = DateTime.Now;
        var overdueCount = _tasks.Count(task =>
            !task.IsCompleted && task.DueAt is DateTime dueAt && dueAt < now);
        var mood = AdaptivePersonalityLogic.DetermineMood(
            _tasks.Count, overdueCount, _activeTimerTask is not null, now < _happyUntil);
        if (mood == AdaptiveMood.Calm)
        {
            var personality = FruitPersonalities.Get(_currentTheme.Kind);
            AdaptiveEyes = $"{personality.Eyes}     {personality.Eyes}";
            AdaptiveMouth = personality.Mouth;
            AdaptiveStatus = "Calm";
        }
        else
        {
            var expression = AdaptivePersonalityLogic.GetExpression(mood);
            AdaptiveEyes = expression.Eyes;
            AdaptiveMouth = expression.Mouth;
            AdaptiveStatus = expression.Label;
        }
        if (_adaptiveMood == mood) return;
        _adaptiveMood = mood;
        AdaptiveFacePanel.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(180)));
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

    private void EnterSleepMode(bool force = false)
    {
        _inactivityTimer.Stop();
        if ((!_state.ResizeWhenInactive && !force) || _isSleeping) return;
        StopShaking();
        _isSleeping = true;
        CollapseExpandedTask();
        AwakeContent.Visibility = Visibility.Collapsed;
        SleepOverlay.Visibility = Visibility.Visible;
        SleepOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        AnimateToSize(SleepFruitSizeLogic.Get(_state.SleepFruitSize));
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
        RegisterGlobalShortcuts();
    }

    public bool TrySetQuickAddShortcut(GlobalShortcutGesture shortcut) =>
        TrySetGlobalShortcut(shortcut, GlobalShortcutAction.QuickAdd);

    public bool TrySetClipboardTaskShortcut(GlobalShortcutGesture shortcut) =>
        TrySetGlobalShortcut(shortcut, GlobalShortcutAction.ClipboardTask);

    public bool TrySetSleepNowShortcut(GlobalShortcutGesture shortcut) =>
        TrySetGlobalShortcut(shortcut, GlobalShortcutAction.SleepNow);

    private bool TrySetGlobalShortcut(GlobalShortcutGesture shortcut, GlobalShortcutAction action)
    {
        if (!GlobalShortcutSettings.IsValid(shortcut)) return false;
        var previousQuickAdd = CurrentQuickAddShortcut;
        var previousClipboard = CurrentClipboardTaskShortcut;
        var previousSleepNow = CurrentSleepNowShortcut;
        switch (action)
        {
            case GlobalShortcutAction.QuickAdd:
                _state.QuickAddShortcut = shortcut;
                break;
            case GlobalShortcutAction.ClipboardTask:
                _state.ClipboardTaskShortcut = shortcut;
                break;
            case GlobalShortcutAction.SleepNow:
                _state.SleepNowShortcut = shortcut;
                break;
        }

        if (_globalQuickAddHotkey is not null && !RegisterGlobalShortcuts())
        {
            _state.QuickAddShortcut = previousQuickAdd;
            _state.ClipboardTaskShortcut = previousClipboard;
            _state.SleepNowShortcut = previousSleepNow;
            RegisterGlobalShortcuts();
            return false;
        }

        RefreshShortcutToolTip();
        SaveState();
        return true;
    }

    private bool RegisterGlobalShortcuts()
    {
        _globalQuickAddHotkey?.Dispose();
        _globalQuickAddHotkey = new GlobalQuickAddHotkey(
            this,
            OpenFromGlobalQuickAdd,
            CreateTaskFromClipboard,
            SleepNowFromGlobalShortcut,
            CurrentQuickAddShortcut,
            CurrentClipboardTaskShortcut,
            CurrentSleepNowShortcut);
        return _globalQuickAddHotkey.AllAvailable;
    }

    private void RefreshShortcutToolTip()
    {
        if (!IsInitialized) return;
        AddButton.ToolTip =
            $"Add task (Ctrl+N / {GlobalShortcutSettings.DisplayName(CurrentQuickAddShortcut)}) • " +
            $"Clipboard task ({GlobalShortcutSettings.DisplayName(CurrentClipboardTaskShortcut)})";
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

    private void CreateTaskFromClipboard()
    {
        var wasSleeping = _isSleeping;
        string clipboardText;
        try
        {
            clipboardText = System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)
                ? System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText).Trim()
                : string.Empty;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return;
        }

        if (clipboardText.Length == 0) return;
        if (clipboardText.Length > 500) clipboardText = clipboardText[..500];
        if (AddNewTasks(TaskReminderLogic.ParseMany(clipboardText)) && wasSleeping)
        {
            ShakeWindow();
        }
    }

    private void SleepNowFromGlobalShortcut() => EnterSleepMode(force: true);

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
        var savedPosition = _state.LastMonitor is string monitor &&
                            _state.MonitorPositions.TryGetValue(monitor, out var monitorPosition)
            ? monitorPosition
            : _state.Left is double savedLeft && _state.Top is double savedTop
                ? new SavedWindowPosition(savedLeft, savedTop)
                : null;
        if (savedPosition is not null && IsOnVirtualScreen(savedPosition.Left, savedPosition.Top))
        {
            Left = savedPosition.Left;
            Top = savedPosition.Top;
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
        var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        _state.LastMonitor = screen.DeviceName;
        _state.MonitorPositions[screen.DeviceName] = new SavedWindowPosition(Left, Top);
        SaveState();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (e.ButtonState != MouseButtonState.Pressed ||
            HasActionControlAncestor(source) || IsWithinTaskRow(source)) return;
        try
        {
            DragMove();
            SnapToScreenEdges();
        }
        catch (InvalidOperationException) { }
    }

    private void SnapToScreenEdges()
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var dpi = VisualTreeHelper.GetDpi(this);
        var workArea = new WorkArea(
            screen.WorkingArea.Left / dpi.DpiScaleX,
            screen.WorkingArea.Top / dpi.DpiScaleY,
            screen.WorkingArea.Width / dpi.DpiScaleX,
            screen.WorkingArea.Height / dpi.DpiScaleY);
        var position = EdgeSnapLogic.Snap(Left, Top, ActualWidth, ActualHeight, workArea);
        Left = position.Left;
        Top = position.Top;
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
        var source = e.OriginalSource as DependencyObject;
        if (!IsWithinElement(source, SortPanel) && !IsWithinElement(source, SortButton))
            SortPanel.Visibility = Visibility.Collapsed;
        if (!IsWithinElement(source, CalendarPanel) && !IsWithinElement(source, CalendarButton))
            CalendarPanel.Visibility = Visibility.Collapsed;
        if (!IsWithinElement(source, FruitContextMenu))
            CloseFruitContextMenu();
        if (!IsWithinTaskRow(source)) CollapseExpandedTask();
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
        StopShaking();

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        NotifyInteraction();
        ShowFruitContextMenu(e.GetPosition(this));
    }

    private void ShowFruitContextMenu(System.Windows.Point position)
    {
        FruitContextMenu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var menuWidth = FruitContextMenu.DesiredSize.Width;
        var menuHeight = FruitContextMenu.DesiredSize.Height;
        var x = Math.Max(0, Math.Min(position.X, ActualWidth - menuWidth));
        var y = Math.Max(0, Math.Min(position.Y, ActualHeight - menuHeight));
        FruitContextMenu.Margin = new Thickness(x, y, 0, 0);
        FruitContextMenu.Visibility = Visibility.Visible;
    }

    private void CloseFruitContextMenu() => FruitContextMenu.Visibility = Visibility.Collapsed;

    private void FruitSleepMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CloseFruitContextMenu();
        EnterSleepMode(force: true);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var wasSleeping = _isSleeping;
        NotifyInteraction();
        if (wasSleeping) e.Handled = true;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        CollapseExpandedTask();
        CloseTaskActions();
        SortPanel.Visibility = Visibility.Collapsed;
        CalendarPanel.Visibility = Visibility.Collapsed;
        CloseFruitContextMenu();
    }

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

    private static bool IsWithinElement(DependencyObject? source, DependencyObject target)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, target)) return true;
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

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        var shouldOpen = SortPanel.Visibility != Visibility.Visible;
        SortPanel.Visibility = shouldOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!shouldOpen) return;
        AddPanel.Visibility = Visibility.Collapsed;
        SetFilterPanelVisible(false);
        CalendarPanel.Visibility = Visibility.Collapsed;
    }

    private void SortByPriorityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SortPanel.Visibility = Visibility.Collapsed;
        ApplySortedTaskOrder(TaskSortLogic.ByPriority(_tasks));
    }

    private void SortByTimeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SortPanel.Visibility = Visibility.Collapsed;
        ApplySortedTaskOrder(TaskSortLogic.ByTime(_tasks));
    }

    private void ApplySortedTaskOrder(IReadOnlyList<TodoItem> sortedTasks)
    {
        if (_tasks.SequenceEqual(sortedTasks)) return;
        var previousPositions = CaptureTaskRowPositions();
        _tasks.Clear();
        foreach (var task in sortedTasks) _tasks.Add(task);
        Dispatcher.BeginInvoke(
            () => AnimateTaskRows(previousPositions),
            DispatcherPriority.Render);
        SaveState();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SortPanel.Visibility = Visibility.Collapsed;
        CalendarPanel.Visibility = Visibility.Collapsed;
        var shouldOpen = FilterPanel.Visibility != Visibility.Visible;
        SetFilterPanelVisible(shouldOpen);
        if (shouldOpen) TaskSearchBox.Focus();
    }

    private void CloseFilterButton_Click(object sender, RoutedEventArgs e) =>
        SetFilterPanelVisible(false);

    private void SetFilterPanelVisible(bool visible)
    {
        FilterPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        AnimateLayout(TaskListBorder, visible ? new Thickness(0, 270, 0, 0) : new Thickness(0, 166, 0, 0));
        AnimateHeight(TaskListBorder, visible ? 108 : 180);
        AnimateLayout(OverflowText, visible ? new Thickness(0, 382, 0, 0) : new Thickness(0, 350, 0, 0));
        AnimateLayout(AdaptiveFacePanel, visible ? new Thickness(0, 407, 0, 0) : new Thickness(0, 378, 0, 0));
        RefreshOverflow();
    }

    private static void AnimateLayout(FrameworkElement element, Thickness target)
    {
        var start = element.Margin;
        element.BeginAnimation(MarginProperty, null);
        element.Margin = target;
        element.BeginAnimation(MarginProperty, new ThicknessAnimation(start, target,
            TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    private static void AnimateHeight(FrameworkElement element, double target)
    {
        var start = element.ActualHeight > 0 ? element.ActualHeight : element.Height;
        element.BeginAnimation(HeightProperty, null);
        element.Height = target;
        element.BeginAnimation(HeightProperty, new DoubleAnimation(start, target,
            TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var shouldOpen = CalendarPanel.Visibility != Visibility.Visible;
        CalendarPanel.Visibility = shouldOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!shouldOpen) return;
        AddPanel.Visibility = Visibility.Collapsed;
        SetFilterPanelVisible(false);
        SortPanel.Visibility = Visibility.Collapsed;
        RefreshCalendar();
    }

    private void CloseCalendarButton_Click(object sender, RoutedEventArgs e) =>
        CalendarPanel.Visibility = Visibility.Collapsed;

    private void CalendarModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string mode }) return;
        if (mode == "Today")
            _calendarAnchor = DateOnly.FromDateTime(DateTime.Now);
        else if (Enum.TryParse<CalendarViewMode>(mode, out var parsedMode))
        {
            _calendarMode = parsedMode;
            if (_calendarMode == CalendarViewMode.Week)
                _calendarAnchor = CalendarLogic.StartOfWeek(_calendarAnchor);
        }
        RefreshCalendar();
    }

    private void CalendarNavigateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string offsetText } ||
            !int.TryParse(offsetText, out var direction)) return;
        _calendarAnchor = _calendarAnchor.AddDays(
            direction * (_calendarMode == CalendarViewMode.Day ? 1 : 7));
        RefreshCalendar();
    }

    private void RefreshCalendar()
    {
        var start = _calendarMode == CalendarViewMode.Day
            ? _calendarAnchor
            : CalendarLogic.StartOfWeek(_calendarAnchor);
        var dayCount = _calendarMode == CalendarViewMode.Day ? 1 : 7;
        CalendarTitle = _calendarMode == CalendarViewMode.Day
            ? start.ToString("ddd, MMM dd").ToUpperInvariant()
            : $"{start:MMM dd} – {start.AddDays(6):MMM dd}".ToUpperInvariant();
        var occurrences = CalendarLogic.GetOccurrences(_tasks, start, dayCount, DateTime.Now);
        CalendarEntries.Clear();
        foreach (var occurrence in occurrences)
        {
            var priority = TaskReminderLogic.PriorityPrefix(occurrence.Task.Priority);
            CalendarEntries.Add(new CalendarDisplayItem(
                occurrence.Task,
                occurrence.At.ToString("ddd MMM dd").ToUpperInvariant(),
                occurrence.At.ToString("HH:mm"),
                priority.Length == 0 ? occurrence.Task.Text : $"{priority} {occurrence.Task.Text}"));
        }
        CalendarEmptyText = CalendarEntries.Count == 0 ? "NO SCHEDULED TASKS" : string.Empty;
    }

    private void CalendarTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        CalendarPanel.Visibility = Visibility.Collapsed;
        _taskFilterMode = TaskFilterMode.Active;
        _taskSearchText = string.Empty;
        TaskSearchBox.Text = string.Empty;
        SelectedCategoryFilter = "All";
        RefreshTaskView();
        Dispatcher.BeginInvoke(() =>
        {
            if (FindTaskRow(task) is not FrameworkElement row) return;
            row.BringIntoView();
            row.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1,
                TimeSpan.FromMilliseconds(420)) { FillBehavior = FillBehavior.Stop });
        }, DispatcherPriority.Loaded);
    }

    private void TaskSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _taskSearchText = TaskSearchBox.Text;
        RefreshTaskView();
    }

    private void TaskFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string filterName }) return;
        if (filterName == "Completed")
        {
            ShowArchive();
            return;
        }
        _taskFilterMode = Enum.Parse<TaskFilterMode>(filterName);
        RefreshTaskView();
    }

    private void RefreshTaskView()
    {
        RefreshCategoryFilters();
        _tasksView.Refresh();
        RefreshOverflow();
        if (CalendarPanel.Visibility == Visibility.Visible) RefreshCalendar();
    }

    private void RefreshCategoryFilters()
    {
        var categories = _tasks
            .SelectMany(task => TaskCategoryLogic.Extract(task.Text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .Prepend("All")
            .ToList();
        _refreshingCategoryFilters = true;
        try
        {
            if (!categories.Contains(_selectedCategoryFilter, StringComparer.OrdinalIgnoreCase))
                _selectedCategoryFilter = "All";
            if (!AvailableCategoryFilters.SequenceEqual(categories, StringComparer.OrdinalIgnoreCase))
            {
                AvailableCategoryFilters.Clear();
                foreach (var category in categories) AvailableCategoryFilters.Add(category);
            }
        }
        finally
        {
            _refreshingCategoryFilters = false;
        }
        OnPropertyChanged(nameof(SelectedCategoryFilter));
    }

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
        SortPanel.Visibility = Visibility.Collapsed;
        SetFilterPanelVisible(false);
        CalendarPanel.Visibility = Visibility.Collapsed;
        _editingTask = task;
        var priorityPrefix = task is null ? string.Empty : TaskReminderLogic.PriorityPrefix(task.Priority);
        var taskText = task is null
            ? string.Empty
            : priorityPrefix.Length == 0 ? task.Text : $"{priorityPrefix} {task.Text}";
        TaskInput.Text = task?.DueAt is DateTime dueAt
            ? $"{dueAt:yyyy-MM-dd HH:mm} {taskText}"
            : task?.ReminderTime is TimeSpan reminderTime
                ? $"{TaskReminderLogic.Prefix(task.Recurrence)} {reminderTime:hh\\:mm} {taskText}".TrimStart()
                : taskText;
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
        if (_editingTask is TodoItem editingTask)
        {
            var parsed = TaskReminderLogic.Parse(TaskInput.Text);
            if (parsed.Text.Length == 0) return;
            editingTask.Text = parsed.Text;
            editingTask.ReminderTime = parsed.ReminderTime;
            editingTask.Recurrence = parsed.Recurrence;
            editingTask.Priority = parsed.Priority;
            editingTask.DueAt = parsed.DueAt;
            _editingTask = null;
            AddPanel.Visibility = Visibility.Collapsed;
            SaveState();
            RefreshAdaptivePersonality();
            RefreshTaskView();
            return;
        }
        _ = AddNewTasks(TaskReminderLogic.ParseMany(TaskInput.Text));
    }

    private bool AddNewTasks(IEnumerable<ParsedTaskInput> parsedTasks)
    {
        var addedAny = false;
        foreach (var parsed in parsedTasks)
        {
            if (parsed.Text.Length == 0) continue;
            _tasks.Add(new TodoItem
            {
                Text = parsed.Text,
                ReminderTime = parsed.ReminderTime,
                Recurrence = parsed.Recurrence,
                Priority = parsed.Priority,
                DueAt = parsed.DueAt
            });
            addedAny = true;
        }
        if (!addedAny) return false;
        AddPanel.Visibility = Visibility.Collapsed;
        RefreshTaskView();
        SaveState();
        RefreshAdaptivePersonality();
        Dispatcher.BeginInvoke(TaskScrollViewer.ScrollToEnd, DispatcherPriority.Loaded);
        return true;
    }

    private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: TodoItem task, IsChecked: true })
        {
            ArchiveCompletedTask(task);
            return;
        }
        SaveState();
    }

    private void ArchiveCompletedTask(TodoItem task)
    {
        if (ReferenceEquals(_activeTimerTask, task)) PauseActiveTimer(persist: false);
        if (ReferenceEquals(_expandedTask, task)) _expandedTask = null;
        task.CompletedAt = DateTime.Now;
        _happyUntil = DateTime.Now.AddSeconds(4);
        _tasks.Remove(task);
        _archivedTasks.Insert(0, task);
        RefreshTaskView();
        SaveState();
        RefreshAdaptivePersonality();
    }

    public void ShowArchive()
    {
        NotifyInteraction();
        ArchivePanel.Visibility = Visibility.Visible;
    }

    private void CloseArchiveButton_Click(object sender, RoutedEventArgs e) =>
        ArchivePanel.Visibility = Visibility.Collapsed;

    private void RestoreArchivedTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        task.IsCompleted = false;
        task.CompletedAt = null;
        _archivedTasks.Remove(task);
        if (task.IsPinned) _tasks.Insert(0, task);
        else _tasks.Add(task);
        ArchivePanel.Visibility = _archivedTasks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshTaskView();
        SaveState();
        RefreshAdaptivePersonality();
    }

    private void DeleteArchivedTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        _archivedTasks.Remove(task);
        ArchivePanel.Visibility = _archivedTasks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SaveState();
        RefreshAdaptivePersonality();
    }

    private void TaskTimerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        task.IsActionsOpen = false;

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
        RefreshAdaptivePersonality();
        RefreshTaskView();
        SaveState();
    }

    private void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        task.IsActionsOpen = false;
        CollapseExpandedTask();
        OpenTaskEditor(task);
    }

    private void DuplicateTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        task.IsActionsOpen = false;
        var duplicate = TodoListLogic.Duplicate(task);
        var sourceIndex = _tasks.IndexOf(task);
        _tasks.Insert(sourceIndex < 0 ? _tasks.Count : sourceIndex + 1, duplicate);
        RefreshTaskView();
        SaveState();
        RefreshAdaptivePersonality();
    }

    private void TaskActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        var open = !task.IsActionsOpen;
        CloseTaskActions();
        task.IsActionsOpen = open;
        e.Handled = true;
    }

    private void CloseTaskActions()
    {
        foreach (var task in _tasks.Where(task => task.IsActionsOpen))
            task.IsActionsOpen = false;
    }

    private void PinTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: TodoItem task }) return;
        task.IsActionsOpen = false;
        var previousPositions = CaptureTaskRowPositions();
        var oldIndex = _tasks.IndexOf(task);
        task.IsPinned = !task.IsPinned;
        _tasks.RemoveAt(oldIndex);
        var targetIndex = task.IsPinned ? 0 : _tasks.Count(item => item.IsPinned);
        _tasks.Insert(targetIndex, task);
        Dispatcher.BeginInvoke(() => AnimateTaskRows(previousPositions), DispatcherPriority.Render);
        SaveState();
    }

    private void RefreshActiveTimer()
    {
        if (_activeTimerTask is not TodoItem task) return;
        var sessionElapsed = Stopwatch.GetElapsedTime(_activeTimerStartedAt);
        task.ElapsedTicks = _activeTimerBaseTicks + sessionElapsed.Ticks;
        var timerText = TaskTimerLogic.Format(TimeSpan.FromTicks(task.ElapsedTicks));
        HeaderText = timerText;
        SleepTimerText = timerText;
    }

    private void PauseActiveTimer(bool persist = true)
    {
        if (_activeTimerTask is not TodoItem task) return;
        RefreshActiveTimer();
        _taskTimer.Stop();
        task.IsTimerRunning = false;
        _activeTimerTask = null;
        SleepTimerText = string.Empty;
        HeaderText = _currentTheme.DisplayName.ToUpperInvariant();
        RefreshAdaptivePersonality();
        RefreshTaskView();
        if (persist) SaveState();
    }

    private void CheckTaskReminders()
    {
        var now = DateTime.Now;
        if (_lastArchiveCleanupDate != DateOnly.FromDateTime(now) && CleanupArchivedTasks(now) > 0)
            SaveState();
        RefreshAdaptivePersonality();
        var dueTasks = _tasks
            .Where(task => !task.IsCompleted &&
                           (TaskReminderLogic.IsSnoozeDue(task.SnoozedUntil, now) ||
                            task.DueAt is DateTime dueAt &&
                            TaskReminderLogic.IsDue(dueAt, now, task.LastReminderDate) ||
                            task.ReminderTime is TimeSpan reminderTime &&
                            TaskReminderLogic.MatchesDay(task.Recurrence, now.DayOfWeek) &&
                            TaskReminderLogic.IsDue(
                                reminderTime, now, task.LastReminderDate, task.Recurrence)))
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
        if (DoNotDisturbSettings.IsActive(CurrentDoNotDisturb, now)) return;
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
        _shakeOriginalTop = Top;
        _isShaking = true;
        var personality = FruitPersonalities.Get(_currentTheme.Kind);
        var distance = 4 + personality.MotionStrength;
        var beat = TimeSpan.FromMilliseconds(Math.Max(55, personality.MotionMilliseconds / 6));
        var horizontalAnimation = new DoubleAnimation(_shakeOriginalLeft - distance, _shakeOriginalLeft + distance, beat)
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(TaskReminderLogic.ShakeDuration)
        };
        horizontalAnimation.Completed += (_, _) => StopShaking();
        var verticalAnimation = new DoubleAnimation(_shakeOriginalTop - distance, _shakeOriginalTop + distance, beat)
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(TaskReminderLogic.ShakeDuration)
        };
        verticalAnimation.Completed += (_, _) => StopShaking();
        if (personality.ReminderMotion is ReminderMotion.Horizontal or ReminderMotion.Diagonal)
            BeginAnimation(LeftProperty, horizontalAnimation, HandoffBehavior.SnapshotAndReplace);
        if (personality.ReminderMotion is ReminderMotion.Vertical or ReminderMotion.Diagonal)
            BeginAnimation(TopProperty, verticalAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StopShaking()
    {
        _pendingShakeTimer?.Stop();
        _pendingShakeTimer = null;
        if (!_isShaking) return;
        _isShaking = false;
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Left = _shakeOriginalLeft;
        Top = _shakeOriginalTop;
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TodoItem item })
        {
            item.IsActionsOpen = false;
            if (ReferenceEquals(_editingTask, item))
            {
                _editingTask = null;
                AddPanel.Visibility = Visibility.Collapsed;
            }
            if (ReferenceEquals(_activeTimerTask, item)) PauseActiveTimer(persist: false);
            if (ReferenceEquals(_expandedTask, item)) _expandedTask = null;
            _tasks.Remove(item);
            RefreshTaskView();
            SaveState();
            RefreshAdaptivePersonality();
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
        if (moving.IsPinned != target.IsPinned) return false;
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
        var visibleLimit = FilterPanel.Visibility == Visibility.Visible ? 3 : VisibleTaskLimit;
        var hidden = TodoListLogic.HiddenCount(_tasksView.Cast<object>().Count(), visibleLimit);
        OverflowLabel = hidden > 0 ? $"+{hidden} more" : string.Empty;
        OnPropertyChanged(nameof(SleepTaskCountText));
    }

    private void SaveState()
    {
        _state.Tasks = _tasks.ToList();
        _state.ArchivedTasks = _archivedTasks.ToList();
        _store.Save(_state);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record CalendarDisplayItem(
    TodoItem Task,
    string DayLabel,
    string TimeLabel,
    string Text);
