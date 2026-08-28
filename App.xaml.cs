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
    private readonly Dictionary<FruitThemeKind, Forms.ToolStripMenuItem> _themeItems = [];
    private MainWindow? _window;
    private Icon? _trayThemeIcon;
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
        SetResizeWhenInactive(_window.IsResizeWhenInactive, persist: false);
        SetTheme(_window.CurrentTheme, persist: false);
        ShowWindow();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show avocado", null, (_, _) => ToggleWindow());
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
        var themesItem = new Forms.ToolStripMenuItem("Themes");
        foreach (var theme in FruitThemes.All)
        {
            var themeItem = new Forms.ToolStripMenuItem(
                theme.DisplayName, null, (_, _) => SetTheme(theme.Kind));
            _themeItems[theme.Kind] = themeItem;
            themesItem.DropDownItems.Add(themeItem);
        }
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        menu.Items.Add(showItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_normalItem);
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add(sizeItem);
        menu.Items.Add(_resizeWhenInactiveItem);
        menu.Items.Add(themesItem);
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
        var handle = bitmap.GetHicon();
        using var temporary = Icon.FromHandle(handle);
        var icon = (Icon)temporary.Clone();
        DestroyIcon(handle);
        return icon;
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

    private void ToggleWindow()
    {
        if (_window?.IsVisible == true) HideWindow();
        else ShowWindow();
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
