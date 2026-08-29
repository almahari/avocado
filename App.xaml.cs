using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Avocado;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _normalItem;
    private Forms.ToolStripMenuItem? _alwaysOnTopItem;
    private Forms.ToolStripMenuItem? _normalSizeItem;
    private Forms.ToolStripMenuItem? _smallSizeItem;
    private Forms.ToolStripMenuItem? _resizeWhenInactiveItem;
    private Forms.ToolStripMenuItem? _openOnStartupItem;
    private Forms.ToolStripMenuItem? _adaptivePersonalityItem;
    private readonly Dictionary<SleepTimeOption, Forms.ToolStripMenuItem> _sleepTimeItems = [];
    private readonly Dictionary<SleepResizeAnchor, Forms.ToolStripMenuItem> _sleepResizeAnchorItems = [];
    private readonly Dictionary<ReminderSoundMode, Forms.ToolStripMenuItem> _reminderSoundItems = [];
    private readonly Dictionary<DoNotDisturbMode, Forms.ToolStripMenuItem> _doNotDisturbItems = [];
    private readonly Dictionary<FruitThemeKind, Forms.ToolStripMenuItem> _themeItems = [];
    private MainWindow? _window;
    private Icon? _trayThemeIcon;
    private readonly StartupRegistration _startupRegistration = new();
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _window = new MainWindow();
        MainWindow = _window;
        _window.HideRequested += (_, _) => HideWindow();
        CreateTrayIcon();
        SetWindowMode(_window.IsAlwaysOnTop, persist: false);
        SetSizeMode(_window.IsSmallSize, persist: false);
        SetSleepTime(_window.CurrentSleepTime, persist: false);
        SetSleepResizeAnchor(_window.CurrentSleepResizeAnchor, persist: false);
        SetReminderSound(_window.CurrentReminderSound, persist: false);
        SetDoNotDisturb(_window.CurrentDoNotDisturb, persist: false);
        SetAdaptivePersonality(_window.IsAdaptivePersonalityEnabled, persist: false);
        SetResizeWhenInactive(_window.IsResizeWhenInactive, persist: false);
        SetTheme(_window.CurrentTheme, persist: false);
        ShowWindow();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show avocado", null, (_, _) => ToggleWindow());
        var archiveItem = new Forms.ToolStripMenuItem("Completed archive", null, (_, _) => ShowArchive());
        _normalItem = new Forms.ToolStripMenuItem("Normal window", null, (_, _) => SetWindowMode(false));
        _alwaysOnTopItem = new Forms.ToolStripMenuItem("Always on top", null, (_, _) => SetWindowMode(true));
        var sizeItem = new Forms.ToolStripMenuItem("Size");
        _normalSizeItem = new Forms.ToolStripMenuItem("Normal", null, (_, _) => SetSizeMode(false));
        _smallSizeItem = new Forms.ToolStripMenuItem("Small", null, (_, _) => SetSizeMode(true));
        sizeItem.DropDownItems.Add(_normalSizeItem);
        sizeItem.DropDownItems.Add(_smallSizeItem);
        _resizeWhenInactiveItem = new Forms.ToolStripMenuItem(
            "Resize when inactive", null,
            (_, _) => SetResizeWhenInactive(!_window!.IsResizeWhenInactive));
        var sleepTimeItem = new Forms.ToolStripMenuItem("Sleep time");
        foreach (var choice in InactivitySettings.Choices)
        {
            var choiceItem = new Forms.ToolStripMenuItem(
                choice.DisplayName, null, (_, _) => SetSleepTime(choice.Option));
            _sleepTimeItems[choice.Option] = choiceItem;
            sleepTimeItem.DropDownItems.Add(choiceItem);
        }
        var sleepResizeAnchorItem = new Forms.ToolStripMenuItem("Sleep resize anchor");
        foreach (var choice in SleepResizeLogic.Choices)
        {
            var choiceItem = new Forms.ToolStripMenuItem(
                choice.DisplayName, null, (_, _) => SetSleepResizeAnchor(choice.Anchor));
            _sleepResizeAnchorItems[choice.Anchor] = choiceItem;
            sleepResizeAnchorItem.DropDownItems.Add(choiceItem);
        }
        var themesItem = new Forms.ToolStripMenuItem("Themes");
        foreach (var theme in FruitThemes.All)
        {
            var themeItem = new Forms.ToolStripMenuItem(
                theme.DisplayName, null, (_, _) => SetTheme(theme.Kind));
            _themeItems[theme.Kind] = themeItem;
            themesItem.DropDownItems.Add(themeItem);
        }
        var reminderSoundItem = new Forms.ToolStripMenuItem("Reminder sound");
        foreach (var choice in ReminderSoundSettings.Choices)
        {
            var choiceItem = new Forms.ToolStripMenuItem(
                choice.DisplayName, null, (_, _) => SetReminderSound(choice.Mode));
            _reminderSoundItems[choice.Mode] = choiceItem;
            reminderSoundItem.DropDownItems.Add(choiceItem);
        }
        var doNotDisturbItem = new Forms.ToolStripMenuItem("Do not disturb");
        foreach (var choice in DoNotDisturbSettings.Choices)
        {
            var choiceItem = new Forms.ToolStripMenuItem(
                choice.DisplayName, null, (_, _) => SetDoNotDisturb(choice.Mode));
            _doNotDisturbItems[choice.Mode] = choiceItem;
            doNotDisturbItem.DropDownItems.Add(choiceItem);
        }
        _adaptivePersonalityItem = new Forms.ToolStripMenuItem(
            "Adaptive personality", null,
            (_, _) => SetAdaptivePersonality(!_window!.IsAdaptivePersonalityEnabled));
        _openOnStartupItem = new Forms.ToolStripMenuItem(
            "Open on Windows startup", null,
            (_, _) => SetOpenOnStartup(!_startupRegistration.IsEnabled()))
        {
            Checked = _startupRegistration.IsEnabled()
        };
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        menu.Items.Add(showItem);
        menu.Items.Add(archiveItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_normalItem);
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add(sizeItem);
        menu.Items.Add(_openOnStartupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_resizeWhenInactiveItem);
        menu.Items.Add(sleepTimeItem);
        menu.Items.Add(sleepResizeAnchorItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(themesItem);
        menu.Items.Add(_adaptivePersonalityItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(reminderSoundItem);
        menu.Items.Add(doNotDisturbItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayThemeIcon = CreateFruitIcon(FruitThemes.Get(_window!.CurrentTheme));
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayThemeIcon,
            Text = "Avocado todo list",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ToggleWindow();
    }

    private static Icon CreateFruitIcon(FruitThemePalette theme)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var skin = new SolidBrush(ColorTranslator.FromHtml(theme.Outer));
        using var flesh = new SolidBrush(ColorTranslator.FromHtml(theme.Flesh));
        using var seed = new SolidBrush(ColorTranslator.FromHtml(theme.Seed));
        using var accent = new SolidBrush(ColorTranslator.FromHtml(theme.Accent));
        switch (theme.Kind)
        {
            case FruitThemeKind.Strawberry:
                DrawStrawberryIcon(graphics, skin, flesh, seed, accent);
                break;
            case FruitThemeKind.Orange:
                DrawOrangeIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Blueberry:
                DrawBlueberryIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Watermelon:
                DrawWatermelonIcon(graphics, skin, flesh, seed, accent);
                break;
            case FruitThemeKind.Kiwi:
                DrawKiwiIcon(graphics, skin, flesh, seed);
                break;
            case FruitThemeKind.Papaya:
                DrawPapayaIcon(graphics, skin, flesh, seed);
                break;
            case FruitThemeKind.Apple:
                DrawAppleIcon(graphics, skin, flesh, seed, accent);
                break;
            case FruitThemeKind.Mango:
                DrawMangoIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Lemon:
                DrawLemonIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Tomato:
                DrawTomatoIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Pumpkin:
                DrawPumpkinIcon(graphics, skin, flesh, accent);
                break;
            case FruitThemeKind.Potato:
                DrawPotatoIcon(graphics, skin, flesh, seed);
                break;
            case FruitThemeKind.Onion:
                DrawOnionIcon(graphics, skin, flesh, accent);
                break;
            default:
                DrawAvocadoIcon(graphics, skin, flesh, seed);
                break;
        }
        var handle = bitmap.GetHicon();
        using var temporary = Icon.FromHandle(handle);
        var icon = (Icon)temporary.Clone();
        DestroyIcon(handle);
        return icon;
    }

    private static void DrawAvocadoIcon(Graphics graphics, Brush skin, Brush flesh, Brush seed)
    {
        graphics.FillRectangle(skin, 13, 1, 6, 5);
        graphics.FillRectangle(skin, 9, 5, 14, 4);
        graphics.FillRectangle(skin, 6, 9, 20, 6);
        graphics.FillRectangle(skin, 3, 15, 26, 10);
        graphics.FillRectangle(skin, 6, 25, 20, 4);
        graphics.FillRectangle(skin, 10, 29, 12, 2);
        graphics.FillRectangle(flesh, 10, 8, 12, 4);
        graphics.FillRectangle(flesh, 7, 12, 18, 11);
        graphics.FillRectangle(flesh, 10, 23, 12, 4);
        graphics.FillRectangle(seed, 12, 17, 9, 9);
    }

    private static void DrawStrawberryIcon(Graphics graphics, Brush skin, Brush flesh, Brush seed, Brush leaf)
    {
        graphics.FillRectangle(leaf, 8, 2, 16, 5);
        graphics.FillRectangle(leaf, 12, 0, 4, 9);
        graphics.FillRectangle(leaf, 20, 0, 4, 9);
        graphics.FillRectangle(skin, 5, 7, 22, 13);
        graphics.FillRectangle(skin, 8, 20, 16, 5);
        graphics.FillRectangle(skin, 11, 25, 10, 4);
        graphics.FillRectangle(skin, 14, 29, 4, 2);
        graphics.FillRectangle(flesh, 7, 9, 18, 10);
        graphics.FillRectangle(flesh, 10, 19, 12, 5);
        graphics.FillRectangle(seed, 9, 12, 2, 2);
        graphics.FillRectangle(seed, 20, 14, 2, 2);
        graphics.FillRectangle(seed, 14, 21, 2, 2);
    }

    private static void DrawOrangeIcon(Graphics graphics, Brush skin, Brush flesh, Brush leaf)
    {
        graphics.FillRectangle(leaf, 16, 1, 4, 6);
        graphics.FillRectangle(leaf, 20, 1, 7, 3);
        graphics.FillRectangle(skin, 9, 5, 14, 3);
        graphics.FillRectangle(skin, 5, 8, 22, 4);
        graphics.FillRectangle(skin, 2, 12, 28, 12);
        graphics.FillRectangle(skin, 5, 24, 22, 4);
        graphics.FillRectangle(skin, 9, 28, 14, 3);
        graphics.FillRectangle(flesh, 7, 10, 18, 16);
        graphics.FillRectangle(flesh, 4, 14, 24, 8);
    }

    private static void DrawBlueberryIcon(Graphics graphics, Brush skin, Brush flesh, Brush crown)
    {
        graphics.FillRectangle(crown, 10, 2, 4, 7);
        graphics.FillRectangle(crown, 18, 2, 4, 7);
        graphics.FillRectangle(crown, 14, 5, 4, 5);
        graphics.FillRectangle(skin, 8, 7, 16, 3);
        graphics.FillRectangle(skin, 4, 10, 24, 5);
        graphics.FillRectangle(skin, 2, 15, 28, 10);
        graphics.FillRectangle(skin, 6, 25, 20, 4);
        graphics.FillRectangle(skin, 10, 29, 12, 2);
        graphics.FillRectangle(flesh, 6, 12, 20, 14);
        graphics.FillRectangle(flesh, 4, 16, 24, 7);
    }

    private static void DrawWatermelonIcon(Graphics graphics, Brush skin, Brush flesh, Brush seed, Brush rind)
    {
        graphics.FillRectangle(skin, 1, 7, 30, 10);
        graphics.FillRectangle(skin, 4, 17, 24, 5);
        graphics.FillRectangle(skin, 7, 22, 18, 4);
        graphics.FillRectangle(skin, 11, 26, 10, 3);
        graphics.FillRectangle(rind, 3, 9, 26, 4);
        graphics.FillRectangle(flesh, 4, 13, 24, 4);
        graphics.FillRectangle(flesh, 7, 17, 18, 4);
        graphics.FillRectangle(flesh, 10, 21, 12, 4);
        graphics.FillRectangle(seed, 8, 14, 2, 3);
        graphics.FillRectangle(seed, 22, 14, 2, 3);
        graphics.FillRectangle(seed, 15, 19, 2, 3);
    }

    private static void DrawKiwiIcon(Graphics graphics, Brush skin, Brush flesh, Brush seed)
    {
        graphics.FillRectangle(skin, 7, 2, 18, 3);
        graphics.FillRectangle(skin, 3, 5, 26, 5);
        graphics.FillRectangle(skin, 1, 10, 30, 12);
        graphics.FillRectangle(skin, 4, 22, 24, 6);
        graphics.FillRectangle(skin, 9, 28, 14, 3);
        graphics.FillRectangle(flesh, 5, 7, 22, 19);
        graphics.FillRectangle(flesh, 3, 12, 26, 8);
        graphics.FillRectangle(seed, 7, 11, 2, 3);
        graphics.FillRectangle(seed, 23, 11, 2, 3);
        graphics.FillRectangle(seed, 8, 20, 2, 3);
        graphics.FillRectangle(seed, 22, 20, 2, 3);
    }

    private static void DrawPapayaIcon(Graphics graphics, Brush skin, Brush flesh, Brush seed)
    {
        graphics.FillRectangle(skin, 11, 1, 10, 4);
        graphics.FillRectangle(skin, 7, 5, 18, 5);
        graphics.FillRectangle(skin, 4, 10, 24, 13);
        graphics.FillRectangle(skin, 7, 23, 18, 5);
        graphics.FillRectangle(skin, 11, 28, 10, 3);
        graphics.FillRectangle(flesh, 7, 7, 18, 19);
        graphics.FillRectangle(seed, 13, 8, 6, 17);
        graphics.FillRectangle(seed, 11, 12, 10, 9);
    }

    private static void DrawAppleIcon(Graphics graphics, Brush skin, Brush flesh, Brush stem, Brush leaf)
    {
        graphics.FillRectangle(stem, 15, 0, 3, 7);
        graphics.FillRectangle(leaf, 18, 1, 8, 4);
        graphics.FillRectangle(skin, 6, 6, 9, 3);
        graphics.FillRectangle(skin, 18, 6, 8, 3);
        graphics.FillRectangle(skin, 3, 9, 26, 15);
        graphics.FillRectangle(skin, 6, 24, 20, 5);
        graphics.FillRectangle(skin, 10, 29, 12, 2);
        graphics.FillRectangle(flesh, 5, 11, 22, 11);
        graphics.FillRectangle(flesh, 8, 22, 16, 5);
    }

    private static void DrawMangoIcon(Graphics graphics, Brush skin, Brush flesh, Brush leaf)
    {
        graphics.FillRectangle(leaf, 5, 2, 10, 4);
        graphics.FillRectangle(skin, 6, 5, 18, 4);
        graphics.FillRectangle(skin, 3, 9, 25, 10);
        graphics.FillRectangle(skin, 5, 19, 24, 7);
        graphics.FillRectangle(skin, 9, 26, 17, 4);
        graphics.FillRectangle(skin, 14, 30, 8, 2);
        graphics.FillRectangle(flesh, 6, 8, 18, 16);
        graphics.FillRectangle(flesh, 9, 22, 16, 5);
    }

    private static void DrawLemonIcon(Graphics graphics, Brush skin, Brush flesh, Brush leaf)
    {
        graphics.FillRectangle(leaf, 20, 2, 8, 3);
        graphics.FillRectangle(skin, 5, 8, 22, 4);
        graphics.FillRectangle(skin, 1, 12, 30, 9);
        graphics.FillRectangle(skin, 5, 21, 22, 4);
        graphics.FillRectangle(flesh, 5, 10, 22, 13);
        graphics.FillRectangle(flesh, 2, 14, 28, 5);
    }

    private static void DrawTomatoIcon(Graphics graphics, Brush skin, Brush flesh, Brush crown)
    {
        graphics.FillRectangle(crown, 7, 3, 18, 4);
        graphics.FillRectangle(crown, 14, 0, 4, 10);
        graphics.FillRectangle(skin, 5, 7, 22, 4);
        graphics.FillRectangle(skin, 2, 11, 28, 13);
        graphics.FillRectangle(skin, 5, 24, 22, 5);
        graphics.FillRectangle(flesh, 5, 12, 22, 13);
    }

    private static void DrawPumpkinIcon(Graphics graphics, Brush skin, Brush flesh, Brush stem)
    {
        graphics.FillRectangle(stem, 14, 0, 5, 8);
        graphics.FillRectangle(skin, 5, 7, 22, 4);
        graphics.FillRectangle(skin, 1, 11, 30, 14);
        graphics.FillRectangle(skin, 5, 25, 22, 5);
        graphics.FillRectangle(flesh, 4, 12, 24, 12);
        graphics.FillRectangle(flesh, 7, 9, 18, 18);
        graphics.FillRectangle(skin, 10, 10, 3, 17);
        graphics.FillRectangle(skin, 19, 10, 3, 17);
    }

    private static void DrawPotatoIcon(Graphics graphics, Brush skin, Brush flesh, Brush eye)
    {
        graphics.FillRectangle(skin, 7, 4, 18, 3);
        graphics.FillRectangle(skin, 3, 7, 25, 7);
        graphics.FillRectangle(skin, 1, 14, 30, 10);
        graphics.FillRectangle(skin, 5, 24, 23, 5);
        graphics.FillRectangle(skin, 11, 29, 13, 2);
        graphics.FillRectangle(flesh, 5, 9, 21, 17);
        graphics.FillRectangle(flesh, 3, 15, 26, 7);
        graphics.FillRectangle(eye, 8, 12, 3, 2);
        graphics.FillRectangle(eye, 22, 17, 3, 2);
        graphics.FillRectangle(eye, 12, 23, 3, 2);
    }

    private static void DrawOnionIcon(Graphics graphics, Brush skin, Brush flesh, Brush sprout)
    {
        graphics.FillRectangle(sprout, 13, 0, 3, 8);
        graphics.FillRectangle(sprout, 18, 0, 3, 8);
        graphics.FillRectangle(skin, 10, 6, 12, 4);
        graphics.FillRectangle(skin, 6, 10, 20, 5);
        graphics.FillRectangle(skin, 3, 15, 26, 10);
        graphics.FillRectangle(skin, 7, 25, 18, 4);
        graphics.FillRectangle(skin, 12, 29, 8, 2);
        graphics.FillRectangle(flesh, 8, 11, 16, 16);
        graphics.FillRectangle(flesh, 5, 16, 22, 7);
        graphics.FillRectangle(skin, 14, 11, 3, 16);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private void SetWindowMode(bool alwaysOnTop, bool persist = true)
    {
        if (_window is null) return;
        _window.SetAlwaysOnTop(alwaysOnTop, persist);
        if (_normalItem is not null) _normalItem.Checked = !alwaysOnTop;
        if (_alwaysOnTopItem is not null) _alwaysOnTopItem.Checked = alwaysOnTop;
    }

    private void SetSizeMode(bool small, bool persist = true)
    {
        if (_window is null) return;
        _window.SetSmallSize(small, persist);
        if (_normalSizeItem is not null) _normalSizeItem.Checked = !small;
        if (_smallSizeItem is not null) _smallSizeItem.Checked = small;
    }

    private void SetResizeWhenInactive(bool enabled, bool persist = true)
    {
        if (_window is null) return;
        _window.SetResizeWhenInactive(enabled, persist);
        if (_resizeWhenInactiveItem is not null) _resizeWhenInactiveItem.Checked = enabled;
    }

    private void SetSleepTime(SleepTimeOption option, bool persist = true)
    {
        if (_window is null) return;
        _window.SetSleepTime(option, persist);
        foreach (var (sleepTime, menuItem) in _sleepTimeItems)
            menuItem.Checked = sleepTime == _window.CurrentSleepTime;
    }

    private void SetSleepResizeAnchor(SleepResizeAnchor anchor, bool persist = true)
    {
        if (_window is null) return;
        _window.SetSleepResizeAnchor(anchor, persist);
        foreach (var (resizeAnchor, menuItem) in _sleepResizeAnchorItems)
            menuItem.Checked = resizeAnchor == _window.CurrentSleepResizeAnchor;
    }

    private void SetReminderSound(ReminderSoundMode mode, bool persist = true)
    {
        if (_window is null) return;
        _window.SetReminderSound(mode, persist);
        foreach (var (soundMode, menuItem) in _reminderSoundItems)
            menuItem.Checked = soundMode == _window.CurrentReminderSound;
    }

    private void SetDoNotDisturb(DoNotDisturbMode mode, bool persist = true)
    {
        if (_window is null) return;
        _window.SetDoNotDisturb(mode, persist);
        foreach (var (doNotDisturbMode, menuItem) in _doNotDisturbItems)
            menuItem.Checked = doNotDisturbMode == _window.CurrentDoNotDisturb;
    }

    private void SetAdaptivePersonality(bool enabled, bool persist = true)
    {
        if (_window is null) return;
        _window.SetAdaptivePersonality(enabled, persist);
        if (_adaptivePersonalityItem is not null)
            _adaptivePersonalityItem.Checked = _window.IsAdaptivePersonalityEnabled;
    }

    private void SetTheme(FruitThemeKind kind, bool persist = true)
    {
        if (_window is null) return;
        _window.SetTheme(kind, persist);
        foreach (var (themeKind, menuItem) in _themeItems)
            menuItem.Checked = themeKind == _window.CurrentTheme;

        if (_trayIcon is null) return;
        var nextIcon = CreateFruitIcon(FruitThemes.Get(_window.CurrentTheme));
        _trayIcon.Icon = nextIcon;
        _trayThemeIcon?.Dispose();
        _trayThemeIcon = nextIcon;
    }

    private void SetOpenOnStartup(bool enabled)
    {
        if (!_startupRegistration.TrySetEnabled(enabled))
        {
            Forms.MessageBox.Show(
                "Windows could not update the startup setting.",
                "Avocado",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Warning);
        }
        if (_openOnStartupItem is not null)
            _openOnStartupItem.Checked = _startupRegistration.IsEnabled();
    }

    private void ToggleWindow()
    {
        if (_window?.IsVisible == true) HideWindow();
        else ShowWindow();
    }

    private void ShowArchive()
    {
        ShowWindow();
        _window?.ShowArchive();
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _window.NotifyInteraction();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void HideWindow() => _window?.Hide();

    private void ExitApplication()
    {
        _isExiting = true;
        _window?.AllowClose();
        _window?.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _trayThemeIcon?.Dispose();
        _trayThemeIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_isExiting)
        {
            _trayIcon?.Dispose();
            _trayThemeIcon?.Dispose();
        }
        base.OnExit(e);
    }
}
