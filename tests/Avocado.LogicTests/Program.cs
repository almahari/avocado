using Avocado;

var tasks = Enumerable.Range(1, 8).Select(number => $"Task {number}").ToList();
var visible = TodoListLogic.TopItems(tasks, 5).ToList();

Assert(visible.SequenceEqual(tasks.Take(5)), "TopItems must preserve order and return only fitting tasks.");
Assert(TodoListLogic.HiddenCount(8, 5) == 3, "Eight tasks with room for five must show +3 more.");
Assert(TodoListLogic.HiddenCount(3, 5) == 0, "A short list must have no overflow.");
Assert(TodoListLogic.TopItems(tasks, 0).Count() == 0, "A zero-size view must be empty.");

var reordered = new List<string> { "A", "B", "C", "D" };
Assert(TodoListLogic.Reorder(reordered, "A", "C", insertAfter: true), "A valid drag must reorder tasks.");
Assert(reordered.SequenceEqual(["B", "C", "A", "D"]), "Dropping on the lower half must insert after the target.");
Assert(TodoListLogic.Reorder(reordered, "D", "B", insertAfter: false), "Upward dragging must reorder tasks.");
Assert(reordered.SequenceEqual(["D", "B", "C", "A"]), "Dropping on the upper half must insert before the target.");
Assert(!TodoListLogic.Reorder(reordered, "D", "B", insertAfter: false),
    "Hovering an already-adjacent insertion point must not repeatedly reorder the list.");

var linkSegments = LinkParser.Parse("Read https://example.com/docs. Then visit www.openai.com");
Assert(linkSegments.Count(segment => segment.Uri is not null) == 2, "HTTP and www links inside tasks must be detected.");
Assert(linkSegments.First(segment => segment.Uri is not null).Uri!.AbsoluteUri == "https://example.com/docs",
    "Sentence punctuation must not become part of a task link.");
Assert(linkSegments.Last(segment => segment.Uri is not null).Uri!.AbsoluteUri == "https://www.openai.com/",
    "A www link must open securely through HTTPS.");
Assert(LinkParser.Parse("ftp://example.com").All(segment => segment.Uri is null),
    "Only safe HTTP and HTTPS task links may be clickable.");
var labeledLink = LinkParser.Parse("https://example.com/guide : Read the travel guide");
Assert(labeledLink.Count == 1 && labeledLink[0].Text == "Read the travel guide",
    "The URL : label format must display only its label.");
Assert(labeledLink[0].Uri!.AbsoluteUri == "https://example.com/guide",
    "A labeled task must keep its original URL as the click target.");
var labeledWwwLink = LinkParser.Parse("www.example.com : Example site");
Assert(labeledWwwLink.Count == 1 && labeledWwwLink[0].Text == "Example site" &&
       labeledWwwLink[0].Uri!.Scheme == "https",
    "A labeled www task must display its label and open securely through HTTPS.");

var statePath = Path.Combine(Path.GetTempPath(), $"avocado-tests-{Guid.NewGuid():N}", "state.json");
var store = new AppStateStore(statePath);
var originalState = new AppState
{
    AlwaysOnTop = true,
    SmallSize = true,
    ResizeWhenInactive = true,
    SleepTime = SleepTimeOption.OneMinute,
    SleepResizeAnchor = SleepResizeAnchor.BottomRight,
    ReminderSound = ReminderSoundMode.FruitSpecific,
    Theme = FruitThemeKind.Blueberry,
    Left = 123,
    Top = 456,
    Tasks = [new TodoItem
    {
        Text = "Persist me",
        IsCompleted = true,
        IsExpanded = true,
        ReminderTime = new TimeSpan(17, 50, 0),
        Recurrence = TaskRecurrence.Monday,
        LastReminderDate = new DateOnly(2026, 8, 28),
        SnoozedUntil = new DateTime(2026, 8, 29, 18, 0, 0),
        ElapsedTicks = TimeSpan.FromMinutes(12).Ticks
    }],
    ArchivedTasks = [new TodoItem { Text = "Finished task", IsCompleted = true }]
};
store.Save(originalState);
var loadedState = store.Load();
Assert(loadedState.AlwaysOnTop, "The selected window mode must persist.");
Assert(loadedState.SmallSize, "The selected app size must persist.");
Assert(loadedState.ResizeWhenInactive, "The resize-when-inactive option must persist.");
Assert(loadedState.SleepTime == SleepTimeOption.OneMinute, "The selected sleep time must persist.");
Assert(loadedState.SleepResizeAnchor == SleepResizeAnchor.BottomRight,
    "The selected sleep resize anchor must persist.");
Assert(loadedState.ReminderSound == ReminderSoundMode.FruitSpecific,
    "The selected reminder sound mode must persist.");
Assert(loadedState.Theme == FruitThemeKind.Blueberry, "The selected fruit theme must persist.");
Assert(loadedState.Left == 123 && loadedState.Top == 456, "Window position must persist.");
Assert(loadedState.Tasks.Count == 1 && loadedState.Tasks[0].Text == "Persist me" && loadedState.Tasks[0].IsCompleted,
    "Tasks and completion state must persist.");
Assert(loadedState.ArchivedTasks.Count == 1 && loadedState.ArchivedTasks[0].Text == "Finished task" &&
       loadedState.ArchivedTasks[0].IsCompleted,
    "Completed task history must persist.");
Assert(loadedState.Tasks[0].ElapsedTicks == TimeSpan.FromMinutes(12).Ticks,
    "A task's accumulated timer duration must persist.");
Assert(loadedState.Tasks[0].ReminderTime == new TimeSpan(17, 50, 0),
    "A task's reminder time must persist.");
Assert(loadedState.Tasks[0].Recurrence == TaskRecurrence.Monday,
    "A task's recurrence must persist.");
Assert(loadedState.Tasks[0].LastReminderDate == new DateOnly(2026, 8, 28),
    "A task's last reminder date must persist to prevent duplicate alerts after a restart.");
Assert(loadedState.Tasks[0].SnoozedUntil == new DateTime(2026, 8, 29, 18, 0, 0),
    "A snoozed reminder must persist across restarts.");
Assert(!loadedState.Tasks[0].IsExpanded, "Temporary task expansion state must not persist.");
Directory.Delete(Path.GetDirectoryName(statePath)!, recursive: true);

var normalSize = AppSizeLogic.Get(small: false);
var smallSize = AppSizeLogic.Get(small: true);
var sleepingSize = AppSizeLogic.Sleeping;
Assert(normalSize == new AppSize(420, 540, 1), "Normal size must preserve the current window dimensions.");
Assert(smallSize == new AppSize(210, 270, 0.5), "Small size must be exactly half of normal size.");
Assert(sleepingSize == new AppSize(126, 162, 0.3), "Sleeping size must be 30% of normal size.");
Assert(sleepingSize.Width < smallSize.Width && sleepingSize.Height < smallSize.Height,
    "The sleeping app must be smaller than the user-selected Small size.");
Assert(SleepResizeLogic.Choices.Count == 4, "All four sleep resize anchors must be available.");
Assert(SleepResizeLogic.Normalize((SleepResizeAnchor)999) == SleepResizeAnchor.TopLeft,
    "Unknown saved resize anchors must safely fall back to Top left.");
Assert(SleepResizeLogic.GetTargetPosition(100, 200, normalSize, sleepingSize, SleepResizeAnchor.TopLeft)
       == new AppPosition(100, 200), "Top-left resizing must keep the top-left corner fixed.");
Assert(SleepResizeLogic.GetTargetPosition(100, 200, normalSize, sleepingSize, SleepResizeAnchor.TopRight)
       == new AppPosition(394, 200), "Top-right resizing must keep the top-right corner fixed.");
Assert(SleepResizeLogic.GetTargetPosition(100, 200, normalSize, sleepingSize, SleepResizeAnchor.BottomLeft)
       == new AppPosition(100, 578), "Bottom-left resizing must keep the bottom-left corner fixed.");
Assert(SleepResizeLogic.GetTargetPosition(100, 200, normalSize, sleepingSize, SleepResizeAnchor.BottomRight)
       == new AppPosition(394, 578), "Bottom-right resizing must keep the bottom-right corner fixed.");
Assert(InactivitySettings.Default == SleepTimeOption.TwoMinutes, "The default sleep time must remain two minutes.");
Assert(InactivitySettings.Choices.Select(choice => choice.Duration).SequenceEqual(
        [null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)]),
    "Sleep time choices must be Never, 30 seconds, one minute, and two minutes.");
Assert(InactivitySettings.Get(SleepTimeOption.Never).Duration is null,
    "The Never sleep-time option must disable the inactivity timeout.");
Assert(InactivitySettings.Get((SleepTimeOption)999).Option == SleepTimeOption.TwoMinutes,
    "Unknown saved sleep times must safely fall back to two minutes.");
Assert(TaskTimerLogic.Format(TimeSpan.Zero) == "00:00:00", "A new task timer must start at zero.");
Assert(TaskTimerLogic.Format(new TimeSpan(1, 2, 3, 4)) == "26:03:04",
    "Task timer formatting must preserve total hours beyond one day.");
Assert(FruitThemes.All.Count == 14, "The tray must offer all fourteen fruit themes.");
Assert(FruitThemes.Default.Kind == FruitThemeKind.Avocado, "Avocado must remain the default theme.");
Assert(FruitThemes.All.Select(theme => theme.Kind).Distinct().Count() == FruitThemes.All.Count,
    "Every fruit theme must have a unique selection value.");
Assert(FruitThemes.Get((FruitThemeKind)999) == FruitThemes.Default,
    "Unknown saved themes must safely fall back to Avocado.");

var timedTask = TaskReminderLogic.Parse("17:50 task 1");
Assert(timedTask.Text == "task 1" && timedTask.ReminderTime == new TimeSpan(17, 50, 0),
    "A leading 24-hour time must be separated from the task text.");
Assert(timedTask.Recurrence == TaskRecurrence.Daily,
    "A plain timed task must retain the existing daily reminder behavior.");
var weeklyTask = TaskReminderLogic.Parse("monday 18:00 Gym");
Assert(weeklyTask.Text == "Gym" && weeklyTask.ReminderTime == new TimeSpan(18, 0, 0) &&
       weeklyTask.Recurrence == TaskRecurrence.Monday,
    "A weekday prefix must create a weekly recurring reminder.");
var dailyTask = TaskReminderLogic.Parse("daily 09:00 Drink water");
Assert(dailyTask.Text == "Drink water" && dailyTask.Recurrence == TaskRecurrence.Daily,
    "The daily prefix must create a daily recurring reminder.");
Assert(TaskReminderLogic.MatchesDay(TaskRecurrence.Monday, DayOfWeek.Monday) &&
       !TaskReminderLogic.MatchesDay(TaskRecurrence.Monday, DayOfWeek.Tuesday),
    "A weekly reminder must only match its selected weekday.");
var untimedTask = TaskReminderLogic.Parse("25:50 task 1");
Assert(untimedTask.Text == "25:50 task 1" && untimedTask.ReminderTime is null,
    "An invalid time prefix must remain ordinary task text.");
var reminderMoment = new DateTime(2026, 8, 29, 17, 50, 30);
Assert(TaskReminderLogic.IsDue(new TimeSpan(17, 50, 0), reminderMoment, null),
    "A reminder must become due during its matching minute.");
Assert(!TaskReminderLogic.IsDue(new TimeSpan(17, 50, 0), reminderMoment, new DateOnly(2026, 8, 29)),
    "A reminder must fire only once per day.");
Assert(TaskReminderLogic.ShakeDuration == TimeSpan.FromSeconds(10),
    "A due reminder must shake the app for exactly ten seconds.");
Assert(TaskReminderLogic.IsSnoozeDue(reminderMoment.AddMinutes(-1), reminderMoment),
    "A reminder must fire when its snooze time is reached.");
Assert(!TaskReminderLogic.IsSnoozeDue(reminderMoment.AddMinutes(1), reminderMoment),
    "A future snooze time must not fire early.");
Assert(StartupRegistration.BuildCommand(@"C:\Apps\Avocado.exe") == "\"C:\\Apps\\Avocado.exe\"",
    "The Windows startup command must quote the executable path.");
Assert(ReminderSoundSettings.Choices.Count == 3 &&
       ReminderSoundSettings.Default == ReminderSoundMode.Soft,
    "Reminder sounds must offer Silent, Soft, and Fruit-specific modes with Soft as default.");

Console.WriteLine("All Avocado logic checks passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
