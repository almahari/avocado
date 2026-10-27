using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Avocado;

public partial class MainWindow
{
    private bool _sideDockCollapsed;
    private SavedWindowPosition? _floatingPosition;

    public bool IsSideDockLayout => _state.SideDockLayout;
    public bool IsSideDockCollapsed => IsSideDockLayout && _sideDockCollapsed;
    public string? CurrentSideDockMonitor => _state.SideDockMonitor;
    public SideDockEdge CurrentSideDockEdge => _state.SideDockEdge == SideDockEdge.Left ? SideDockEdge.Left : SideDockEdge.Right;

    public void SetSideDockMonitor(string monitor)
    {
        if (!Forms.Screen.AllScreens.Any(s => s.DeviceName == monitor)) return;
        _state.SideDockMonitor = monitor;
        PositionSideDock();
        SaveState();
    }

    public void SetSideDockEdge(SideDockEdge edge)
    {
        _state.SideDockEdge = edge == SideDockEdge.Left ? SideDockEdge.Left : SideDockEdge.Right;
        PositionSideDock();
        SaveState();
    }

    public void SetSideDockLayout(bool enabled, bool persist = true)
    {
        StopShaking();
        CollapseExpandedTask();
        WakeFromInactivity();
        if (enabled && !IsSideDockLayout && IsLoaded)
        {
            SaveLocationNow();
            _floatingPosition = new SavedWindowPosition(Left, Top);
        }
        _state.SideDockLayout = enabled;
        OnPropertyChanged(nameof(IsSideDockLayout));
        _sideDockCollapsed = false;
        _inactivityTimer.Stop();
        _sizeAnimationVersion++;
        foreach (var property in new[] { WidthProperty, HeightProperty, LeftProperty, TopProperty })
            BeginAnimation(property, null);
        Root.LayoutTransform = Transform.Identity;
        FruitArtwork.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        DockSurface.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        DockCloseButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        DockTab.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        Root.Visibility = Visibility.Visible;
        Root.Margin = new Thickness(0);
        TaskListBorder.Width = enabled ? double.NaN : 278;
        TaskListBorder.HorizontalAlignment = enabled ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Center;
        UpdateDockHeaderLayout();
        OverflowText.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        ShowInTaskbar = true;
        Topmost = _state.AlwaysOnTop;
        if (enabled)
        {
            _state.SideDockMonitor ??= Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).DeviceName;
            PositionSideDock();
        }
        else
        {
            ApplySizeImmediately(AppSizeLogic.Get(_state.SmallSize));
            var position = _floatingPosition ??
                (_state.Left is double left && _state.Top is double top ? new SavedWindowPosition(left, top) : null);
            if (position is not null) { Left = position.Left; Top = position.Top; }
            KeepWindowOnVirtualScreen();
            SetFilterPanelVisible(FilterPanel.Visibility == Visibility.Visible);
            RestartInactivityTimer();
        }
        RefreshTaskView();
        UpdateMascotAnimation(force: true);
        if (persist) SaveState();
    }

    public void CollapseSideDock()
    {
        if (!IsSideDockLayout) return;
        StopShaking();
        _sideDockCollapsed = true;
        UpdateMascotAnimation(force: true);
        Root.Visibility = DockSurface.Visibility = DockCloseButton.Visibility = Visibility.Collapsed;
        ShowInTaskbar = false;
        Topmost = true;
        PositionSideDock();
        if (!IsVisible) Show();
    }

    public void ExpandSideDock()
    {
        if (!IsSideDockLayout) return;
        _sideDockCollapsed = false;
        UpdateMascotAnimation(force: true);
        Root.Visibility = DockSurface.Visibility = DockCloseButton.Visibility = Visibility.Visible;
        ShowInTaskbar = true;
        Topmost = _state.AlwaysOnTop;
        PositionSideDock();
        if (!IsVisible) Show();
        Activate();
    }

    private void PositionSideDock()
    {
        if (!IsSideDockLayout || !IsLoaded) return;
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _state.SideDockMonitor)
            ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = screen.WorkingArea;
        var top = area.Top / dpi.DpiScaleY;
        var height = area.Height / dpi.DpiScaleY;
        Width = _sideDockCollapsed ? 32 : Math.Min(452, area.Width / dpi.DpiScaleX);
        Height = _sideDockCollapsed ? Math.Min(144, height) : height;
        var leftSide = CurrentSideDockEdge == SideDockEdge.Left;
        Left = leftSide ? area.Left / dpi.DpiScaleX : area.Right / dpi.DpiScaleX - Width;
        Top = _sideDockCollapsed ? top + Math.Min(96, Math.Max(0, height - Height)) : top;
        DockTab.Margin = new Thickness(0, _sideDockCollapsed ? 0 : Math.Min(96, Math.Max(0, height - 144)), 0, 0);
        DockTab.HorizontalAlignment = leftSide ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        var panelMargin = leftSide ? new Thickness(0, 0, 32, 0) : new Thickness(32, 0, 0, 0);
        Root.Margin = DockSurface.Margin = panelMargin;
        DockSurface.CornerRadius = leftSide ? new CornerRadius(0, 16, 16, 0) : new CornerRadius(16, 0, 0, 16);
        DockWallpaper.CornerRadius = leftSide ? new CornerRadius(0, 12, 12, 0) : new CornerRadius(12, 0, 0, 12);
        DockCloseButton.Margin = new Thickness(0, 12, leftSide ? 44 : 12, 0);
        UpdateDockTaskArea();
    }

    private void UpdateDockTaskArea()
    {
        if (!IsSideDockLayout || _sideDockCollapsed) return;
        var filtering = FilterPanel.Visibility == Visibility.Visible;
        TaskListBorder.BeginAnimation(HeightProperty, null);
        TaskListBorder.BeginAnimation(MarginProperty, null);
        var top = filtering ? 396 : 284;
        var illustrationHeight = Math.Min(320, Height * 0.32);
        DockCatScene.Height = Math.Min(440, Height * 0.44);
        DockThemeScene.Height = DockCatScene.Height;
        DockMascotEffects.Height = DockCatScene.Height;
        DockMascotGround.Height = DockCatScene.Height;
        TaskListBorder.Margin = new Thickness(20, top, 20, 0);
        TaskListBorder.Height = Math.Max(80, Height - top - illustrationHeight - 12);
        AdaptiveFacePanel.BeginAnimation(MarginProperty, null);
        AdaptiveFacePanel.Margin = new Thickness(0, Height - 62, 0, 0);
        AdaptiveFacePanel.Visibility = Visibility.Collapsed;
    }

    private void DockCloseButton_Click(object sender, RoutedEventArgs e) => CollapseSideDock();
    private void DockTab_Click(object sender, RoutedEventArgs e)
    {
        if (_sideDockCollapsed) ExpandSideDock();
        else CollapseSideDock();
    }

    private IntPtr SideDockWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Display, working-area, and per-monitor DPI changes are applied after WPF processes the message.
        if (IsSideDockLayout && message is 0x007E or 0x001A or 0x02E0)
            Dispatcher.BeginInvoke(PositionSideDock, DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }
}
