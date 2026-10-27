using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Avocado;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--cat-clipping")) { VerifyCatClipping(); return; }
        if (args.Contains("--mascot-animations")) { VerifyMascotAnimations(); return; }
        if (args.Contains("--palette-time")) { VerifyPaletteTime(); return; }
        var directory = Path.Combine(Path.GetTempPath(), "Avocado-layout-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var store = new AppStateStore(Path.Combine(directory, "state.json"));
        store.Save(new AppState
        {
            Tasks = Enumerable.Range(1, 100).Select(i => new TodoItem { Text = $"Layout test task {i}" }).ToList()
        });
        var app = new LayoutTestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(store);
        void InvokeApp(string method) => typeof(App).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, null);
        typeof(App).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, window);
        window.HideRequested += (_, _) => InvokeApp("HideWindow");
        window.Show();
        Pump();
        var floatingLeft = window.Left;
        var floatingTop = window.Top;
        InvokeApp("CreateTrayIcon");
        var tray = (Forms.NotifyIcon)typeof(App).GetField("_trayIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var toggle = tray.ContextMenuStrip!.Items.OfType<Forms.ToolStripMenuItem>().Single(i => i.Text == "Side dock layout");
        try
        {
            tray.ContextMenuStrip.Show(16, 16);
            Pump();
            var mascotToggle = tray.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "Mascot animations");
            Check(mascotToggle.Checked, "mascot animations are enabled in the tray by default");
            mascotToggle.PerformClick();
            Check(!window.IsMascotAnimationsEnabled && !store.Load().MascotAnimationsEnabled && !mascotToggle.Checked,
                "tray toggle disables and saves mascot animations");
            mascotToggle.PerformClick();
            Check(window.IsMascotAnimationsEnabled && store.Load().MascotAnimationsEnabled && mascotToggle.Checked,
                "tray toggle restores mascot animations");
            toggle.PerformClick();
            Pump();
            Check(toggle.Checked && window.IsSideDockLayout && store.Load().SideDockLayout, "tray enables and saves layout");
            var dpi = VisualTreeHelper.GetDpi(window);
            var screen = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            Check(Near(window.Top, screen.WorkingArea.Top / dpi.DpiScaleY) &&
                  Near(window.Top + window.Height, screen.WorkingArea.Bottom / dpi.DpiScaleY) &&
                  Near(window.Left + window.Width, screen.WorkingArea.Right / dpi.DpiScaleX), "panel fills right working edge");
            var list = (Border)window.FindName("TaskListBorder");
            Check(Near(list.ActualWidth, window.Width - 32 - 40), "task rows fill panel width with small gutters");
            Check(window.WorldClocks.Count == 4 && window.WorldClocks.Select(clock => clock.Name).SequenceEqual(new[] { "Bahrain", "UK", "EDT", "Singapore" }),
                "four live world clocks appear above tasks");
            Check(((FrameworkElement)window.FindName("DockHeader")).IsVisible && window.DockDays.Count == 7,
                "dock header shows date and seven-day calendar strip");
            var strip = (ItemsControl)window.FindName("DockDayStrip");
            var dayPresenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(6);
            var dayTarget = (System.Windows.Controls.Button)strip.ItemTemplate.FindName("DockDayDropTarget", dayPresenter);
            var dragged = window.Tasks[0];
            dragged.DueAt = DateTime.Today.AddHours(17).AddMinutes(45);
            dragged.SnoozedUntil = DateTime.Today.AddDays(5);
            dragged.LastReminderDate = DateOnly.FromDateTime(DateTime.Today);
            var draggedOrder = window.Tasks.ToArray();
            var dragOver = CalendarDragArgs(dayTarget, dragged, UIElement.DragOverEvent);
            dayTarget.RaiseEvent(dragOver);
            Check(dragOver.Handled && dragOver.Effects == System.Windows.DragDropEffects.Move &&
                  ((SolidColorBrush)dayTarget.Background).Color == (Color)ColorConverter.ConvertFromString(FruitThemes.Default.ButtonHover),
                "date strip accepts and highlights a dragged task");
            var drop = CalendarDragArgs(dayTarget, dragged, UIElement.DropEvent);
            dayTarget.RaiseEvent(drop);
            var expectedDate = ((DateOnly)dayTarget.Tag).ToDateTime(new TimeOnly(17, 45));
            Check(drop.Handled && drop.Effects == System.Windows.DragDropEffects.Move && dragged.DueAt == expectedDate &&
                  store.Load().Tasks.Single(task => task.Id == dragged.Id).DueAt == expectedDate,
                "dropping on a date reschedules and saves the task while preserving its time");
            Check(dragged.SnoozedUntil is null && dragged.LastReminderDate is null && window.Tasks.SequenceEqual(draggedOrder),
                "date drop clears stale reminder state and preserves task order");
            Check(dayTarget.ReadLocalValue(System.Windows.Controls.Control.BackgroundProperty) == DependencyProperty.UnsetValue,
                "date drop clears target highlighting");
            var foreignTask = new TodoItem { Text = "Not from this window" };
            var invalidDrop = CalendarDragArgs(dayTarget, foreignTask, UIElement.DropEvent);
            dayTarget.RaiseEvent(invalidDrop);
            Check(invalidDrop.Effects == System.Windows.DragDropEffects.None && foreignTask.DueAt is null,
                "date drop rejects foreign task payloads");
            var emptyPresenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(0);
            var emptyTarget = (System.Windows.Controls.Button)strip.ItemTemplate.FindName("DockDayDropTarget", emptyPresenter);
            emptyTarget.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Pump();
            Check(window.TasksView.IsEmpty && ((FrameworkElement)window.FindName("DockEmptyState")).IsVisible,
                "empty dates show a clear message and hide undated tasks");
            Click(window, "ClearDockDateFilterButton");
            Pump();
            var recurring = window.Tasks[2];
            recurring.ReminderTime = TimeSpan.FromHours(14);
            recurring.Recurrence = TaskRecurrence.Daily;
            var snoozed = window.Tasks[3];
            snoozed.ReminderTime = TimeSpan.FromHours(16);
            snoozed.SnoozedUntil = expectedDate.AddHours(1);
            dayTarget.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Pump();
            Check(window.TasksView.Cast<TodoItem>().Select(task => task.Id).ToHashSet().SetEquals(new[] { dragged.Id, recurring.Id, snoozed.Id }) &&
                  ((FrameworkElement)window.FindName("CalendarPanel")).Visibility == Visibility.Collapsed,
                "date click filters dated, recurring, and snoozed tasks without a popup");
            Check(window.DockDays.Single(day => day.IsSelected).Date == DateOnly.FromDateTime(expectedDate) &&
                  ((FrameworkElement)window.FindName("ClearDockDateFilterButton")).IsVisible,
                "selected day is highlighted and All tasks is available");
            var otherDayPresenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(5);
            var otherDayTarget = (System.Windows.Controls.Button)strip.ItemTemplate.FindName("DockDayDropTarget", otherDayPresenter);
            otherDayTarget.RaiseEvent(CalendarDragArgs(otherDayTarget, dragged, UIElement.DropEvent));
            Check(!window.TasksView.Cast<TodoItem>().Contains(dragged), "rescheduling out of a selected day updates its filtered list");
            Click(window, "ClearDockDateFilterButton");
            Pump();
            Check(window.TasksView.Cast<TodoItem>().Count() == 100 && !window.DockDays.Any(day => day.IsSelected),
                "All tasks restores the complete list and clears selection");
            var selectedPresenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(6);
            var selectedTarget = (System.Windows.Controls.Button)strip.ItemTemplate.FindName("DockDayDropTarget", selectedPresenter);
            selectedTarget.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Pump();
            selectedPresenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(6);
            selectedTarget = (System.Windows.Controls.Button)strip.ItemTemplate.FindName("DockDayDropTarget", selectedPresenter);
            selectedTarget.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Pump();
            Check(window.TasksView.Cast<TodoItem>().Count() == 100, "clicking the selected day again clears the date filter");
            Check(list.ActualHeight > 180 && ((ScrollViewer)window.FindName("TaskScrollViewer")).ScrollableHeight > 0,
                "task list uses panel height and scrolls");
            var sheetHeight = Math.Ceiling(FruitThemes.All.Count / 5.0) * 240;
            var sceneSheet = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Width = 1000, Height = sheetHeight };
            foreach (var theme in FruitThemes.All)
            {
                window.SetTheme(theme.Kind);
                var surface = (Border)window.FindName("DockSurface");
                Check(((SolidColorBrush)surface.Background).Color == (Color)ColorConverter.ConvertFromString(theme.Kind == FruitThemeKind.Cat ? theme.Flesh : theme.Cream),
                    $"live {theme.DisplayName} panel theme");
                var wallpaper = (Border)window.FindName("DockWallpaper");
                if (theme.Kind == FruitThemeKind.Cat)
                {
                    var scene = (System.Windows.Controls.Image)window.FindName("DockCatScene");
                    Check(scene.IsVisible && scene.Source is BitmapSource { PixelWidth: > 0 } && wallpaper.Background is null,
                        "Cat theme uses generated illustration on a cream background");
                }
                else
                {
                    var scene = (System.Windows.Controls.Image)window.FindName("DockThemeScene");
                    Check(scene.IsVisible && scene.Source is BitmapImage { PixelWidth: > 0, IsFrozen: true } bitmap &&
                          bitmap.UriSource.AbsoluteUri.EndsWith($"/{theme.Kind.ToString().ToLowerInvariant()}-scene.png") &&
                          wallpaper.Background is null, $"{theme.DisplayName} uses its own illustrated background scene");
                }
                var hiddenForPreview = window.Tasks.Skip(3).ToArray();
                foreach (var task in hiddenForPreview) window.Tasks.Remove(task);
                window.UpdateLayout();
                Capture(window, Path.Combine(directory, $"theme-{theme.Kind}.png"));
                foreach (var task in hiddenForPreview) window.Tasks.Add(task);
                var sceneCard = new DockPanel { Background = surface.Background, Width = 200, Height = 240 };
                var sceneLabel = new TextBlock { Text = theme.DisplayName, FontSize = 17, Margin = new Thickness(10),
                    Foreground = (System.Windows.Media.Brush)window.Resources["InkBrush"] };
                DockPanel.SetDock(sceneLabel, Dock.Top);
                sceneCard.Children.Add(sceneLabel);
                sceneCard.Children.Add(new System.Windows.Controls.Image
                {
                    Source = ((System.Windows.Controls.Image)window.FindName(theme.Kind == FruitThemeKind.Cat ? "DockCatScene" : "DockThemeScene")).Source,
                    Stretch = Stretch.Uniform
                });
                sceneSheet.Children.Add(sceneCard);
            }
            sceneSheet.Measure(new System.Windows.Size(1000, sheetHeight));
            sceneSheet.Arrange(new Rect(0, 0, 1000, sheetHeight));
            sceneSheet.UpdateLayout();
            Capture(sceneSheet, Path.Combine(directory, "scenes-contact-sheet.png"));
            window.SetTheme(FruitThemeKind.Avocado);
            Capture(window, Path.Combine(directory, "expanded.png"));
            Click(window, "SearchButton");
            Pump();
            Check(list.ActualHeight > 180, "filter preserves expanded task area");
            Click(window, "SearchButton");
            Click(window, "DockCloseButton");
            Pump();
            Check(window.IsVisible && window.IsSideDockCollapsed && Near(window.Width, 32) &&
                  !window.ShowInTaskbar && window.Topmost, "close leaves visible edge tab");
            Check(Near(window.Left + window.Width, screen.WorkingArea.Right / dpi.DpiScaleX), "tab sticks to right edge");
            Capture(window, Path.Combine(directory, "collapsed.png"));
            window.SetTheme(FruitThemeKind.Strawberry);
            var tab = (System.Windows.Controls.Button)window.FindName("DockTab");
            Check(((SolidColorBrush)tab.Background).Color == (Color)ColorConverter.ConvertFromString(FruitThemes.Get(FruitThemeKind.Strawberry).Button),
                "collapsed tab follows theme");
            Click(window, "DockTab");
            Pump();
            Check(!window.IsSideDockCollapsed && window.Width > 32, "tab reopens panel");
            window.Close();
            Pump();
            Check(window.IsVisible && window.IsSideDockCollapsed, "window close collapses instead of exiting");
            InvokeApp("ToggleWindow");
            Pump();
            Check(!window.IsSideDockCollapsed, "tray reopens collapsed panel");

            var dockSettings = tray.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>().Single(i => i.Text == "Side dock settings");
            var sideMenu = dockSettings.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(i => i.Text == "Side");
            tray.ContextMenuStrip.Show(16, 16);
            dockSettings.ShowDropDown();
            sideMenu.ShowDropDown();
            sideMenu.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(i => i.Text == "Left").PerformClick();
            Pump();
            Check(window.CurrentSideDockEdge == SideDockEdge.Left && store.Load().SideDockEdge == SideDockEdge.Left &&
                  Near(window.Left, screen.WorkingArea.Left / dpi.DpiScaleX), "tray selects and saves left edge");
            Check(((FrameworkElement)window.FindName("DockTab")).HorizontalAlignment == System.Windows.HorizontalAlignment.Right,
                "left panel tab faces inward");
            Capture(window, Path.Combine(directory, "left-expanded.png"));
            window.CollapseSideDock();
            Pump();
            Check(Near(window.Left, screen.WorkingArea.Left / dpi.DpiScaleX), "collapsed tab sticks to left edge");
            window.ExpandSideDock();

            var screenMenu = dockSettings.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(i => i.Text == "Screen");
            foreach (var target in Forms.Screen.AllScreens)
            {
                tray.ContextMenuStrip.Show(16, 16);
                dockSettings.ShowDropDown();
                screenMenu.ShowDropDown();
                screenMenu.DropDownItems.OfType<Forms.ToolStripMenuItem>().Single(i => (string?)i.Tag == target.DeviceName).PerformClick();
                Pump();
                var targetDpi = VisualTreeHelper.GetDpi(window);
                Check(window.CurrentSideDockMonitor == target.DeviceName && store.Load().SideDockMonitor == target.DeviceName &&
                      Near(window.Left, target.WorkingArea.Left / targetDpi.DpiScaleX) &&
                      Near(window.Top, target.WorkingArea.Top / targetDpi.DpiScaleY), $"tray selects and saves monitor {target.DeviceName}");
            }
            tray.ContextMenuStrip.Close();
            toggle.PerformClick();
            Pump();
            Check(!toggle.Checked && !window.IsSideDockLayout && !store.Load().SideDockLayout &&
                  Near(window.Width, 420) && Near(window.Height, 540) &&
                  Near(window.Left, floatingLeft) && Near(window.Top, floatingTop), "floating layout and position restored");
            Check(window.Tasks.Count == 100 && store.Load().Tasks.Count == 100, "tasks survive layout and theme switching");
            window.SetSideDockLayout(true);
            window.SetTheme(FruitThemeKind.Cat);
            window.AllowClose();
            window.Close();
            window = new MainWindow(store);
            window.Show();
            Pump();
            Check(window.IsSideDockLayout && window.Height > 540, "saved layout restored at startup");
            Check(window.CurrentTheme == FruitThemeKind.Cat && ((System.Windows.Controls.Image)window.FindName("DockCatScene")).IsVisible,
                "Cat theme and artwork restored at startup");
            Check(window.CurrentSideDockEdge == SideDockEdge.Left &&
                  window.CurrentSideDockMonitor == store.Load().SideDockMonitor, "saved side and monitor restored at startup");
            window.AllowClose();
            window.Close();
            var disconnectedState = store.Load();
            disconnectedState.SideDockMonitor = "disconnected-monitor";
            store.Save(disconnectedState);
            window = new MainWindow(store);
            window.Show();
            Pump();
            var fallbackDpi = VisualTreeHelper.GetDpi(window);
            Check(Near(window.Left, Forms.Screen.PrimaryScreen!.WorkingArea.Left / fallbackDpi.DpiScaleX),
                "disconnected monitor falls back to primary screen");
            Console.WriteLine($"Layout checks passed. Rendered previews: {directory}");
        }
        finally
        {
            tray.Dispose();
            window.AllowClose();
            window.Close();
            app.Shutdown();
        }
    }

    private sealed class LayoutTestApp : App
    {
        // Exercise the real tray handlers with an isolated store, without launching the user's app.
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private static void VerifyCatClipping()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Avocado-cat-clipping-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var store = new AppStateStore(Path.Combine(directory, "state.json"));
        store.Save(new AppState { Theme = FruitThemeKind.Cat, AdaptivePersonalityEnabled = false });
        var app = new LayoutTestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(store);
        try
        {
            window.Show();
            Pump();
            foreach (var theme in FruitThemes.All.Where(theme => FruitThemes.IsAnimal(theme.Kind)).Select(theme => theme.Kind))
            foreach (var small in new[] { false, true })
            {
                window.SetTheme(theme);
                window.SetSmallSize(small);
                Pump();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var scale = small ? 0.5 : 1;
                var pixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect((int)(50 * scale), (int)(500 * scale), 1, 1), pixel, 4, 0);
                Capture(window, Path.Combine(directory, $"{theme}-{(small ? "small" : "normal")}.png"));
                Check(pixel[3] == 0, $"{theme} artwork stays inside the rounded floating outline ({(small ? "small" : "normal")})");
                bitmap.CopyPixels(new Int32Rect((int)(210 * scale), (int)(480 * scale), 1, 1), pixel, 4, 0);
                Check(pixel[3] > 0, $"{theme} artwork remains visible inside the outline");
            }
            Console.WriteLine($"Clipping previews: {directory}");
        }
        finally { window.AllowClose(); window.Close(); app.Shutdown(); }
    }

    private static void VerifyMascotAnimations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Avocado-mascots-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var store = new AppStateStore(Path.Combine(directory, "state.json"));
        store.Save(new AppState { Theme = FruitThemeKind.Lion, Tasks = [new() { Text = "One" }, new() { Text = "Two" }] });
        var app = new LayoutTestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(store);
        try
        {
            window.Show();
            window.SetSideDockLayout(true);
            Pump();
            var tasks = (Border)window.FindName("TaskListBorder");
            var taskBounds = new System.Windows.Size(tasks.ActualWidth, tasks.ActualHeight);
            var effects = (Grid)window.FindName("DockMascotEffects");
            var image = (System.Windows.Controls.Image)window.FindName("DockThemeScene");
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still, "idle mascots remain still");
            window.CompletePaletteTask(window.Tasks[0]);
            Pump(180);
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Celebrating && effects.Children.Count == 4,
                "task completion celebrates with a badge and three sparkles");
            Check(new System.Windows.Size(tasks.ActualWidth, tasks.ActualHeight) == taskBounds &&
                  !tasks.RenderTransform.HasAnimatedProperties, "celebrations keep task controls stationary");
            var transforms = (TransformGroup)image.RenderTransform;
            Check(transforms.Children.Any(transform => transform.HasAnimatedProperties) == SystemParameters.ClientAreaAnimation,
                "mascot motion follows the Windows animation preference");
            Capture(window, Path.Combine(directory, "celebrating.png"));
            window.CompletePaletteTask(window.Tasks[0]);
            Check(effects.Children.Count == 4, "rapid completions replace effects without stacking them");
            typeof(MainWindow).GetField("_happyUntil", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, DateTime.MinValue);
            Pump(1200);
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still && effects.Children.Count == 0 &&
                  image.RenderTransform == Transform.Identity, "celebrations automatically return to the still pose");

            window.SetDoNotDisturb(DoNotDisturbMode.Always);
            var present = typeof(MainWindow).GetMethod("ApplyMascotAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            present.Invoke(window, [MascotAnimationState.Sleeping, false, image]);
            Check(((TransformGroup)image.RenderTransform).Children.All(transform => !transform.HasAnimatedProperties) &&
                  effects.Children.Cast<FrameworkElement>().All(child => !child.RenderTransform.HasAnimatedProperties) &&
                  ((BitmapImage)image.Source).UriSource.AbsoluteUri.EndsWith("lion-sleep.png"),
                "reduced motion keeps sleeping artwork without breathing or drifting");
            present.Invoke(window, [MascotAnimationState.Celebrating, false, image]);
            Check(((TransformGroup)image.RenderTransform).Children.All(transform => !transform.HasAnimatedProperties) &&
                  effects.Children.Count == 4 && effects.Children.Cast<FrameworkElement>().All(child => child.Opacity == 1),
                "reduced motion uses static celebration feedback");
            window.SetDoNotDisturb(DoNotDisturbMode.Always);
            var sheetHeight = Math.Ceiling(FruitThemes.All.Count / 4.0) * 240;
            var sheet = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Width = 800, Height = sheetHeight };
            foreach (var theme in FruitThemes.All)
            {
                window.SetTheme(theme.Kind);
                Pump(40);
                var active = (System.Windows.Controls.Image)window.FindName(theme.Kind == FruitThemeKind.Cat ? "DockCatScene" : "DockThemeScene");
                Check(window.CurrentMascotAnimationState == MascotAnimationState.Sleeping && active.Source is BitmapImage bitmap &&
                      bitmap.UriSource.AbsoluteUri.EndsWith($"/{theme.Kind.ToString().ToLowerInvariant()}-sleep.png"),
                    $"{theme.DisplayName} uses its sleeping illustration during quiet hours");
                var card = new DockPanel { Background = ((Border)window.FindName("DockSurface")).Background, Width = 200, Height = 240 };
                var label = new TextBlock { Text = theme.DisplayName, Margin = new Thickness(10), FontSize = 17,
                    Foreground = (System.Windows.Media.Brush)window.Resources["InkBrush"] };
                DockPanel.SetDock(label, Dock.Top);
                card.Children.Add(label);
                card.Children.Add(new System.Windows.Controls.Image { Source = active.Source, Stretch = Stretch.Uniform });
                sheet.Children.Add(card);
            }
            sheet.Measure(new System.Windows.Size(800, sheetHeight));
            sheet.Arrange(new Rect(0, 0, 800, sheetHeight));
            sheet.UpdateLayout();
            Capture(sheet, Path.Combine(directory, "sleeping-mascots.png"));
            Capture(window, Path.Combine(directory, "sleeping.png"));
            window.SetMascotAnimations(false);
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still && effects.Children.Count == 0 &&
                  !store.Load().MascotAnimationsEnabled, "saved mascot toggle disables poses and motion");
            window.SetMascotAnimations(true);
            window.CollapseSideDock();
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still && effects.Children.Count == 0,
                "collapsed panels stop all mascot effects");
            window.ExpandSideDock();
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Sleeping, "expanded panels restore the quiet-hours pose");
            window.SetTheme(FruitThemeKind.Lion);
            window.SetSideDockLayout(false);
            foreach (var theme in FruitThemes.All.Where(theme => FruitThemes.IsAnimal(theme.Kind)))
            {
                window.SetTheme(theme.Kind);
                var floating = (System.Windows.Controls.Image)window.FindName($"{theme.Kind}MascotImage");
                Check(window.CurrentMascotAnimationState == MascotAnimationState.Sleeping &&
                      ((BitmapImage)floating.Source).UriSource.AbsoluteUri.EndsWith($"{theme.Kind.ToString().ToLowerInvariant()}-sleep.png") &&
                      !((FrameworkElement)window.FindName("AdaptiveFacePanel")).IsVisible,
                    $"floating {theme.DisplayName} sleeps without an extra face overlay");
                Pump(40);
                Capture(window, Path.Combine(directory, $"floating-{theme.Kind}-sleep.png"));
            }
            window.SetTheme(FruitThemeKind.Avocado);
            Check(window.AdaptiveEyes == "─     ─", "floating pixel fruit displays sleepy eyes during quiet hours");
            window.SetDoNotDisturb(DoNotDisturbMode.Off);
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still &&
                  ((Grid)window.FindName("FloatingMascotEffects")).Children.Count == 0, "ending quiet hours wakes the mascot");
            window.Hide();
            Check(window.CurrentMascotAnimationState == MascotAnimationState.Still, "hidden windows remain still");
            Console.WriteLine($"Mascot previews: {directory}");
        }
        finally { window.AllowClose(); window.Close(); app.Shutdown(); }
    }

    private static void VerifyPaletteTime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Avocado-palette-time-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var store = new AppStateStore(Path.Combine(directory, "state.json"));
        store.Save(new AppState { Theme = FruitThemeKind.Lion, Tasks = [new() { Text = "Meeting 14:30" }] });
        var app = new LayoutTestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var host = new MainWindow(store);
        var palette = new CommandPaletteWindow(new CommandConfigStore(Path.Combine(directory, "commands.json")),
            new BookmarkConfigStore(Path.Combine(directory, "bookmarks.json")), host, FruitThemes.Get(FruitThemeKind.Lion));
        try
        {
            host.Show();
            palette.Open();
            var input = (System.Windows.Controls.TextBox)palette.FindName("CommandBox");
            var results = (System.Windows.Controls.ListBox)palette.FindName("ResultsList");
            input.Text = "14:30";
            Pump();
            var entries = results.Items.Cast<PaletteEntry>().ToArray();
            Check(entries.Length == 6 && entries.All(entry => entry.Kind == PaletteEntryKind.TimeConversion && entry.CopyText is not null) &&
                  entries.Select(entry => entry.Title.Split('·')[0].Trim()).SequenceEqual(new[] { "Bahrain", "UK", "EDT", "EST", "Singapore", "India" }),
                "bare HH:MM displays six time conversions instead of search results");
            var last = (FrameworkElement)results.ItemContainerGenerator.ContainerFromIndex(5);
            Check(last.TransformToAncestor(results).Transform(new System.Windows.Point(0, last.ActualHeight)).Y <= results.ActualHeight + 2,
                "all six time conversions fit in the palette");
            Check(((TextBlock)palette.FindName("StatusText")).Text.Contains(TimeZoneInfo.Local.StandardName) &&
                  ((TextBlock)palette.FindName("HintText")).Text.Contains("copy time"), "palette labels the source time zone and copy action");
            Capture(palette, Path.Combine(directory, "time-conversions.png"));
            input.Text = "24:90";
            Pump();
            Check(results.Items.Cast<PaletteEntry>().All(entry => entry.Kind != PaletteEntryKind.TimeConversion) && palette.Height <= 460,
                "invalid time restores normal palette search and height");
            input.Text = "task tomorrow 09:00 Test conversion guard";
            Check(results.Items.Cast<PaletteEntry>().Single().Kind == PaletteEntryKind.CreateTask, "task creation keeps its scheduling syntax");
            typeof(CommandPaletteWindow).GetField("_selectedTask", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(palette, host.Tasks[0]);
            typeof(CommandPaletteWindow).GetField("_isReschedulingTask", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(palette, true);
            input.Text = "15:45";
            Check(results.Items.Cast<PaletteEntry>().Single().TaskAction == PaletteTaskAction.ApplyReschedule,
                "HH:MM in task rescheduling remains a schedule instead of a conversion");
            Check(host.Tasks.Count == 1 && !host.Tasks[0].IsCompleted, "typing times does not modify tasks");
            Console.WriteLine($"Palette time preview: {directory}");
        }
        finally { palette.Close(); host.AllowClose(); host.Close(); app.Shutdown(); }
    }

    private static void Click(MainWindow window, string name) =>
        ((System.Windows.Controls.Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

    private static System.Windows.DragEventArgs CalendarDragArgs(DependencyObject target, TodoItem task, RoutedEvent routedEvent)
    {
        var constructor = typeof(System.Windows.DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(System.Windows.IDataObject), typeof(System.Windows.DragDropKeyStates), typeof(System.Windows.DragDropEffects),
                typeof(DependencyObject), typeof(System.Windows.Point)], null)!;
        var args = (System.Windows.DragEventArgs)constructor.Invoke([
            new System.Windows.DataObject(typeof(TodoItem), task), System.Windows.DragDropKeyStates.LeftMouseButton,
            System.Windows.DragDropEffects.Move, target, new System.Windows.Point(1, 1)]);
        args.RoutedEvent = routedEvent;
        return args;
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 2;
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }

    private static void Pump(int milliseconds = 250)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Capture(FrameworkElement window, string path)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}

