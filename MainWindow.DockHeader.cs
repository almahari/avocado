using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Avocado;

public sealed record DockDay(DateOnly Date, string DayName, string DayNumber, bool IsToday, bool IsSelected);

public partial class MainWindow
{
    private DispatcherTimer? _worldClockTimer;
    private DateOnly? _dockToday;
    private DateOnly? _selectedDockDate;
    private HashSet<Guid>? _dockDateTaskIds;
    public ObservableCollection<WorldClockReading> WorldClocks { get; } = [];
    public ObservableCollection<DockDay> DockDays { get; } = [];
    public string DockDateText => DateTime.Now.ToString("dddd, MMMM d, yyyy");
    public string DockTaskHeading => _selectedDockDate is DateOnly date ? $"To Do · {date:d MMM}" : "To Do";
    public string DockEmptyMessage => _selectedDockDate is DateOnly date
        ? $"No tasks for {date:d MMM}.\nChoose All tasks to see everything."
        : "No tasks yet. Use + to add one.";

    private void StartWorldClocks()
    {
        RefreshWorldClocks();
        _worldClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _worldClockTimer.Tick += (_, _) => RefreshWorldClocks();
        _worldClockTimer.Start();
    }

    private void RefreshWorldClocks()
    {
        UpdateMascotAnimation();
        var readings = WorldClockLogic.GetReadings(DateTimeOffset.UtcNow);
        for (var index = 0; index < readings.Count; index++)
        {
            if (index >= WorldClocks.Count) WorldClocks.Add(readings[index]);
            else if (WorldClocks[index] != readings[index]) WorldClocks[index] = readings[index];
        }
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_dockToday == today) return;
        _dockToday = today;
        OnPropertyChanged(nameof(DockDateText));
        RefreshDockDays();
        if (_selectedDockDate is not null) RefreshTaskView();
    }

    private void RefreshDockDays()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        DockDays.Clear();
        var first = today.AddDays(-2);
        for (var index = 0; index < 7; index++)
        {
            var date = first.AddDays(index);
            DockDays.Add(new DockDay(date, date.ToString("ddd"), date.Day.ToString(), date == today, date == _selectedDockDate));
        }
    }

    private void DockDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: DateOnly day }) return;
        _selectedDockDate = _selectedDockDate == day ? null : day;
        CalendarPanel.Visibility = Visibility.Collapsed;
        RefreshDockDays();
        RefreshTaskView();
        TaskScrollViewer.ScrollToTop();
    }

    private void ClearDockDateFilter_Click(object sender, RoutedEventArgs e)
    {
        _selectedDockDate = null;
        RefreshDockDays();
        RefreshTaskView();
        TaskScrollViewer.ScrollToTop();
    }

    private void RefreshDockDateFilter()
    {
        _dockDateTaskIds = IsSideDockLayout && _selectedDockDate is DateOnly date
            ? CalendarLogic.GetOccurrences(_tasks, date, 1, DateTime.Now).Select(item => item.Task.Id).ToHashSet()
            : null;
        ClearDockDateFilterButton.Visibility = _selectedDockDate is null ? Visibility.Collapsed : Visibility.Visible;
        OnPropertyChanged(nameof(DockTaskHeading));
        OnPropertyChanged(nameof(DockEmptyMessage));
    }

    private TodoItem? GetCalendarDropTask(System.Windows.DragEventArgs e) =>
        (e.AllowedEffects & System.Windows.DragDropEffects.Move) != 0 &&
        e.Data.GetDataPresent(typeof(TodoItem)) &&
        e.Data.GetData(typeof(TodoItem)) is TodoItem task && _tasks.Contains(task) && !task.IsCompleted
            ? task : null;

    private void DockDay_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        var valid = sender is System.Windows.Controls.Button { Tag: DateOnly } && GetCalendarDropTask(e) is not null;
        e.Effects = valid ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button button) return;
        if (valid)
        {
            button.SetResourceReference(BackgroundProperty, "ButtonHoverBrush");
            button.SetResourceReference(ForegroundProperty, "CreamBrush");
        }
        else ClearCalendarDropHighlight(button);
    }

    private static void ClearCalendarDropHighlight(System.Windows.Controls.Button button)
    {
        button.ClearValue(BackgroundProperty);
        button.ClearValue(ForegroundProperty);
    }

    private void DockDay_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button) ClearCalendarDropHighlight(button);
        e.Handled = true;
    }

    private void DockDay_Drop(object sender, System.Windows.DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = System.Windows.DragDropEffects.None;
        if (sender is not System.Windows.Controls.Button button) return;
        ClearCalendarDropHighlight(button);
        if (button.Tag is not DateOnly date || GetCalendarDropTask(e) is not TodoItem task) return;
        // Crossing task rows previews a reorder; a calendar drop changes only the schedule.
        if (_taskOrderChangedDuringDrag)
        {
            RestoreOriginalTaskOrder();
            _taskOrderChangedDuringDrag = false;
        }
        CalendarLogic.MoveToDate(task, date);
        ResolveAlertingTask(task);
        RefreshTaskView();
        SaveState();
        RefreshAdaptivePersonality();
        e.Effects = System.Windows.DragDropEffects.Move;
    }

    private void UpdateDockHeaderLayout()
    {
        var dock = IsSideDockLayout;
        DockHeader.Visibility = dock ? Visibility.Visible : Visibility.Collapsed;
        FruitHeader.Visibility = dock ? Visibility.Collapsed : Visibility.Visible;
        DockCatScene.Visibility = _currentTheme.Kind == FruitThemeKind.Cat ? Visibility.Visible : Visibility.Collapsed;
        AdaptiveFacePanel.Visibility = _state.AdaptivePersonalityEnabled && !dock && !FruitThemes.IsAnimal(_currentTheme.Kind)
            ? Visibility.Visible : Visibility.Collapsed;
        AwakeContent.Margin = new Thickness(0);
        SortButton.Margin = dock ? new Thickness(0, 242, 128, 0) : new Thickness(82, 114, 0, 0);
        SortButton.HorizontalAlignment = dock ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        CalendarButton.Margin = dock ? new Thickness(0, 242, 92, 0) : new Thickness(118, 114, 0, 0);
        CalendarButton.HorizontalAlignment = dock ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        AddButton.Margin = dock ? new Thickness(0, 242, 20, 0) : new Thickness(0, 114, 84, 0);
        SearchButton.Margin = dock ? new Thickness(0, 242, 56, 0) : new Thickness(0, 114, 120, 0);
        AddPanel.Margin = dock ? new Thickness(0, 280, 0, 0) : new Thickness(0, 157, 0, 0);
        FilterPanel.Margin = dock ? new Thickness(0, 280, 0, 0) : new Thickness(0, 157, 0, 0);
        CalendarPanel.Margin = dock ? new Thickness(0, 280, 0, 0) : new Thickness(0, 148, 0, 0);
        SortPanel.Margin = dock ? new Thickness(112, 280, 0, 0) : new Thickness(112, 151, 0, 0);
        ArchivePanel.Margin = dock ? new Thickness(0, 280, 0, 0) : new Thickness(0, 148, 0, 0);
        SnoozePanel.Margin = dock ? new Thickness(0, 396, 0, 0) : new Thickness(0, 366, 0, 0);
        if (dock) RefreshWorldClocks();
    }
}
