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
    SleepFruitSize = SleepFruitSize.Small,
    SleepResizeAnchor = SleepResizeAnchor.BottomRight,
    ReminderSound = ReminderSoundMode.FruitSpecific,
    DoNotDisturb = DoNotDisturbMode.TenPmToSevenAm,
    ArchiveRetention = ArchiveRetentionOption.ThirtyDays,
    AdaptivePersonalityEnabled = false,
    Theme = FruitThemeKind.Blueberry,
    SeasonalSkin = SeasonalSkinKind.WinterCap,
    QuickAddShortcut = new GlobalShortcutGesture(
        GlobalShortcutModifiers.Alt | GlobalShortcutModifiers.Shift, 'Q'),
    ClipboardTaskShortcut = GlobalShortcutSettings.Disabled,
    SleepNowShortcut = new GlobalShortcutGesture(GlobalShortcutModifiers.Control, 0x70),
    Left = 123,
    Top = 456,
    LastMonitor = "DISPLAY1",
    MonitorPositions = new Dictionary<string, SavedWindowPosition>
    {
        ["DISPLAY1"] = new SavedWindowPosition(123, 456)
    },
    Tasks = [new TodoItem
    {
        Text = "Persist me",
        CreatedAt = new DateTime(2026, 8, 20, 10, 15, 0),
        IsCompleted = true,
        IsExpanded = true,
        IsActionsOpen = true,
        ReminderTime = new TimeSpan(17, 50, 0),
        Recurrence = TaskRecurrence.Monday,
        Priority = TaskPriority.High,
        IsPinned = true,
        DueAt = new DateTime(2026, 8, 31, 9, 30, 0),
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
Assert(loadedState.SleepFruitSize == SleepFruitSize.Small, "The selected sleeping fruit size must persist.");
Assert(loadedState.SleepResizeAnchor == SleepResizeAnchor.BottomRight,
    "The selected sleep resize anchor must persist.");
Assert(loadedState.ReminderSound == ReminderSoundMode.FruitSpecific,
    "The selected reminder sound mode must persist.");
Assert(loadedState.DoNotDisturb == DoNotDisturbMode.TenPmToSevenAm,
    "The selected Do Not Disturb schedule must persist.");
Assert(loadedState.ArchiveRetention == ArchiveRetentionOption.ThirtyDays,
    "The selected archive retention period must persist.");
Assert(!loadedState.AdaptivePersonalityEnabled,
    "The Adaptive personality tray option must persist.");
Assert(loadedState.Theme == FruitThemeKind.Blueberry, "The selected fruit theme must persist.");
Assert(loadedState.SeasonalSkin == SeasonalSkinKind.WinterCap, "The selected seasonal skin must persist.");
Assert(loadedState.QuickAddShortcut == originalState.QuickAddShortcut &&
       loadedState.ClipboardTaskShortcut.IsDisabled &&
       loadedState.SleepNowShortcut == originalState.SleepNowShortcut,
    "Customized and disabled global shortcuts must persist.");
Assert(loadedState.Left == 123 && loadedState.Top == 456, "Window position must persist.");
Assert(loadedState.LastMonitor == "DISPLAY1" &&
       loadedState.MonitorPositions["DISPLAY1"] == new SavedWindowPosition(123, 456),
    "Window positions must persist per monitor.");
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
Assert(loadedState.Tasks[0].Priority == TaskPriority.High, "A task's priority must persist.");
Assert(loadedState.Tasks[0].IsPinned, "A task's pinned state must persist.");
Assert(loadedState.Tasks[0].DueAt == new DateTime(2026, 8, 31, 9, 30, 0),
    "A task's natural-language due date must persist.");
Assert(loadedState.Tasks[0].CreatedAt == new DateTime(2026, 8, 20, 10, 15, 0) &&
       loadedState.Tasks[0].CreatedToolTip == "Created Aug 20, 2026 10:15",
    "A task's creation timestamp must persist and appear in its tooltip.");
Assert(loadedState.Tasks[0].LastReminderDate == new DateOnly(2026, 8, 28),
    "A task's last reminder date must persist to prevent duplicate alerts after a restart.");
Assert(loadedState.Tasks[0].SnoozedUntil == new DateTime(2026, 8, 29, 18, 0, 0),
    "A snoozed reminder must persist across restarts.");
Assert(!loadedState.Tasks[0].IsExpanded, "Temporary task expansion state must not persist.");
Assert(!loadedState.Tasks[0].IsActionsOpen, "The temporary task action palette must not persist.");
Directory.Delete(Path.GetDirectoryName(statePath)!, recursive: true);

var cleanupReference = new DateTime(2026, 8, 29, 12, 0, 0);
var cleanupArchive = new List<TodoItem>
{
    new() { Text = "Old", CompletedAt = cleanupReference.AddDays(-8) },
    new() { Text = "Recent", CompletedAt = cleanupReference.AddDays(-2) },
    new() { Text = "Legacy without date" }
};
Assert(ArchiveRetentionSettings.RemoveExpired(
        cleanupArchive, ArchiveRetentionOption.SevenDays, cleanupReference) == 1 &&
       cleanupArchive.Select(task => task.Text).SequenceEqual(["Recent", "Legacy without date"]),
    "Seven-day archive cleanup must remove only dated completions older than seven days.");
Assert(ArchiveRetentionSettings.RemoveExpired(
        cleanupArchive, ArchiveRetentionOption.Never, cleanupReference) == 0,
    "Never must disable automatic archive cleanup.");
Assert(ArchiveRetentionSettings.Get((ArchiveRetentionOption)999).Option == ArchiveRetentionOption.Never,
    "Unknown archive retention values must safely fall back to Never.");

var normalSize = AppSizeLogic.Get(small: false);
var smallSize = AppSizeLogic.Get(small: true);
var sleepingSize = AppSizeLogic.Sleeping;
var smallSleepingSize = SleepFruitSizeLogic.Get(SleepFruitSize.Small);
Assert(normalSize == new AppSize(420, 540, 1), "Normal size must preserve the current window dimensions.");
Assert(smallSize == new AppSize(210, 270, 0.5), "Small size must be exactly half of normal size.");
Assert(sleepingSize == new AppSize(126, 162, 0.3), "Sleeping size must be 30% of normal size.");
Assert(SleepFruitSizeLogic.Get(SleepFruitSize.Normal) == sleepingSize,
    "Normal sleeping fruit size must preserve the current sleeping dimensions.");
Assert(smallSleepingSize == new AppSize(63, 81, 0.15),
    "Small sleeping fruit size must be exactly half of the current sleeping dimensions.");
Assert(SleepFruitSizeLogic.Choices.Count == 2, "Both sleeping fruit size choices must be available.");
Assert(SleepFruitSizeLogic.Normalize((SleepFruitSize)999) == SleepFruitSize.Normal,
    "Unknown sleeping fruit sizes must safely fall back to Normal.");
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
Assert(FruitThemes.All.Select(theme => FruitPersonalities.Get(theme.Kind)).Distinct().Count() >= 10,
    "Fruit themes must provide varied sleeping faces and reminder motions.");
Assert(FruitThemes.All.Select(theme => FruitPersonalities.Get(theme.Kind)).Distinct().Count() == FruitThemes.All.Count,
    "Every fruit theme must have a distinct face and reminder profile.");
Assert(FruitPersonalities.Get((FruitThemeKind)999) == FruitPersonalities.Get(FruitThemeKind.Avocado),
    "Unknown personality values must safely fall back to Avocado.");
Assert(SeasonalSkins.All.Count == 5, "The tray must offer no skin and four seasonal skins.");
Assert(SeasonalSkins.Default.Kind == SeasonalSkinKind.None, "Seasonal skins must be opt-in.");
Assert(SeasonalSkins.All.Select(skin => skin.Kind).Distinct().Count() == SeasonalSkins.All.Count,
    "Every seasonal skin must have a unique selection value.");
Assert(SeasonalSkins.Get((SeasonalSkinKind)999) == SeasonalSkins.Default,
    "Unknown saved seasonal skins must safely fall back to None.");

var timedTask = TaskReminderLogic.Parse("17:50 task 1");
Assert(timedTask.Text == "task 1" && timedTask.ReminderTime == new TimeSpan(17, 50, 0),
    "A leading 24-hour time must be separated from the task text.");
Assert(timedTask.Recurrence == TaskRecurrence.None,
    "A plain timed task must be a one-time reminder.");
var batchTasks = TaskReminderLogic.ParseMany(" task 1 ; 12:00 task 2 ;; ");
Assert(batchTasks.Count == 2 && batchTasks[0].Text == "task 1" &&
       batchTasks[1].Text == "task 2" && batchTasks[1].ReminderTime == new TimeSpan(12, 0, 0),
    "Semicolon-separated input must create independently parsed tasks and ignore empty segments.");
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
var priorityTask = TaskReminderLogic.Parse("daily 09:00 !!! Ship release");
Assert(priorityTask.Text == "Ship release" && priorityTask.Priority == TaskPriority.High,
    "Three exclamation marks must create a high-priority task.");
Assert(TaskReminderLogic.Parse("!! Review").Priority == TaskPriority.Medium &&
       TaskReminderLogic.Parse("! Later").Priority == TaskPriority.Low,
    "One and two exclamation marks must create low- and medium-priority tasks.");
var untimedTask = TaskReminderLogic.Parse("25:50 task 1");
Assert(untimedTask.Text == "25:50 task 1" && untimedTask.ReminderTime is null,
    "An invalid time prefix must remain ordinary task text.");
var parsingReference = new DateTime(2026, 8, 29, 12, 0, 0);
var tomorrowTask = TaskReminderLogic.Parse("tomorrow 9am Call Ali", parsingReference);
Assert(tomorrowTask.Text == "Call Ali" && tomorrowTask.DueAt == new DateTime(2026, 8, 30, 9, 0, 0),
    "Tomorrow plus a 12-hour time must create a one-time dated reminder.");
var fridayTask = TaskReminderLogic.Parse("Friday Submit report", parsingReference);
Assert(fridayTask.Text == "Submit report" && fridayTask.DueAt == new DateTime(2026, 9, 4, 9, 0, 0),
    "A natural weekday must target its next occurrence and default to 09:00.");
var exactDateTask = TaskReminderLogic.Parse("2026-09-03 08:15 !! Release build", parsingReference);
Assert(exactDateTask.Text == "Release build" && exactDateTask.Priority == TaskPriority.Medium &&
       exactDateTask.DueAt == new DateTime(2026, 9, 3, 8, 15, 0),
    "An exact date, time, and priority must parse together.");
Assert(TaskReminderLogic.FormatDueLabel(new DateTime(2026, 9, 3, 8, 15, 0)) == "SEP 03 08:15",
    "Displayed due dates must use a two-digit day for alignment.");
var reminderMoment = new DateTime(2026, 8, 29, 17, 50, 30);
Assert(TaskReminderLogic.IsDue(new TimeSpan(17, 50, 0), reminderMoment, null),
    "A reminder must become due during its matching minute.");
Assert(!TaskReminderLogic.IsDue(new TimeSpan(17, 50, 0), reminderMoment, new DateOnly(2026, 8, 29)),
    "A reminder must fire only once per day.");
Assert(!TaskReminderLogic.IsDue(new TimeSpan(17, 50, 0), reminderMoment, new DateOnly(2026, 8, 28)),
    "A one-time reminder must not fire again on a later day.");
Assert(TaskReminderLogic.IsDue(
        new TimeSpan(17, 50, 0), reminderMoment, new DateOnly(2026, 8, 28), TaskRecurrence.Daily),
    "An explicit daily reminder must become eligible again on a later day.");
Assert(TaskReminderLogic.ShakeDuration == TimeSpan.FromSeconds(2),
    "A due reminder must shake the app for exactly two seconds.");
Assert(TaskReminderLogic.IsSnoozeDue(reminderMoment.AddMinutes(-1), reminderMoment),
    "A reminder must fire when its snooze time is reached.");
Assert(!TaskReminderLogic.IsSnoozeDue(reminderMoment.AddMinutes(1), reminderMoment),
    "A future snooze time must not fire early.");
Assert(TaskReminderLogic.IsDue(reminderMoment.AddMinutes(-1), reminderMoment, null) &&
       !TaskReminderLogic.IsDue(reminderMoment.AddMinutes(-1), reminderMoment, new DateOnly(2026, 8, 29)),
    "A dated reminder must fire once when its due moment arrives.");
Assert(StartupRegistration.BuildCommand(@"C:\Apps\Avocado.exe") == "\"C:\\Apps\\Avocado.exe\"",
    "The Windows startup command must quote the executable path.");
Assert(ReminderSoundSettings.Choices.Count == 3 &&
       ReminderSoundSettings.Default == ReminderSoundMode.Soft,
    "Reminder sounds must offer Silent, Soft, and Fruit-specific modes with Soft as default.");
Assert(DoNotDisturbSettings.IsActive(DoNotDisturbMode.TenPmToSevenAm, new DateTime(2026, 8, 29, 23, 0, 0)) &&
       DoNotDisturbSettings.IsActive(DoNotDisturbMode.TenPmToSevenAm, new DateTime(2026, 8, 29, 6, 30, 0)) &&
       !DoNotDisturbSettings.IsActive(DoNotDisturbMode.TenPmToSevenAm, new DateTime(2026, 8, 29, 12, 0, 0)),
    "Do Not Disturb quiet hours must work across midnight.");
Assert(DoNotDisturbSettings.IsActive(DoNotDisturbMode.Always, DateTime.Now) &&
       !DoNotDisturbSettings.IsActive(DoNotDisturbMode.Off, DateTime.Now),
    "Do Not Disturb must support both Always and Off.");
Assert(AdaptivePersonalityLogic.DetermineMood(3, 0, false, true)
       == AdaptiveMood.Happy, "A recent completion must make the fruit happy.");
Assert(AdaptivePersonalityLogic.DetermineMood(3, 1, true, false)
       == AdaptiveMood.Focused, "An active timer must take the focused expression.");
Assert(AdaptivePersonalityLogic.DetermineMood(3, 1, false, false)
       == AdaptiveMood.Worried, "An overdue task must make the fruit worried.");
Assert(AdaptivePersonalityLogic.DetermineMood(6, 0, false, false)
       == AdaptiveMood.Tired, "More than five active tasks must make the fruit tired at any time.");
Assert(AdaptivePersonalityLogic.DetermineMood(5, 0, false, false)
       == AdaptiveMood.Calm, "Exactly five active tasks must not make the fruit tired.");
Assert(AdaptivePersonalityLogic.DetermineMood(2, 0, false, false)
       == AdaptiveMood.Calm, "The fruit must remain calm when no adaptive condition applies.");
Assert(AdaptivePersonalityLogic.GetExpression(AdaptiveMood.Worried).Label.Contains("overdue"),
    "Adaptive expressions must explain why the fruit changed mood.");
var filterTask = new TodoItem { Text = "Write release notes", ReminderTime = TimeSpan.FromHours(9) };
Assert(TaskFilterLogic.Matches(filterTask, "release", TaskFilterMode.Active),
    "Task search must be case-insensitive and match text fragments.");
Assert(TaskFilterLogic.Matches(filterTask, string.Empty, TaskFilterMode.Scheduled),
    "The scheduled filter must include tasks with reminder times.");
Assert(TaskFilterLogic.Matches(new TodoItem { DueAt = DateTime.Now.AddDays(1) }, string.Empty,
        TaskFilterMode.Scheduled),
    "The scheduled filter must include naturally dated tasks.");
Assert(!TaskFilterLogic.Matches(filterTask, string.Empty, TaskFilterMode.RunningTimer),
    "The running-timer filter must exclude tasks without an active timer.");
var categorizedTask = new TodoItem { Text = "Prepare report #Work #urgent" };
Assert(TaskCategoryLogic.Extract(categorizedTask.Text).SequenceEqual(["#urgent", "#work"]),
    "Task categories must be extracted uniquely and case-insensitively.");
Assert(TaskFilterLogic.Matches(categorizedTask, string.Empty, TaskFilterMode.Active, "#work") &&
       !TaskFilterLogic.Matches(categorizedTask, string.Empty, TaskFilterMode.Active, "#personal"),
    "Category filtering must include only tasks with the selected hashtag.");
var workArea = new WorkArea(0, 0, 1920, 1080);
Assert(EdgeSnapLogic.Snap(12, 11, 420, 540, workArea) == new AppPosition(0, 0),
    "Dragging near the top-left must snap to both edges.");
Assert(EdgeSnapLogic.Snap(1490, 535, 420, 540, workArea) == new AppPosition(1500, 540),
    "Dragging near the bottom-right must snap to both edges.");
Assert(EdgeSnapLogic.Snap(500, 300, 420, 540, workArea) == new AppPosition(500, 300),
    "Dragging away from an edge must preserve the position.");
var sortableTasks = new List<TodoItem>
{
    new() { Text = "Plain" },
    new() { Text = "Later", Priority = TaskPriority.Low, ReminderTime = TimeSpan.FromHours(18) },
    new() { Text = "Urgent", Priority = TaskPriority.High, ReminderTime = TimeSpan.FromHours(9) },
    new() { Text = "Pinned", IsPinned = true }
};
var duplicateSource = new TodoItem
{
    Text = "Copy me #work",
    ReminderTime = TimeSpan.FromHours(12),
    Priority = TaskPriority.High,
    IsPinned = true,
    IsCompleted = true,
    ElapsedTicks = TimeSpan.FromMinutes(4).Ticks
};
var duplicatedTask = TodoListLogic.Duplicate(duplicateSource);
Assert(duplicatedTask.Id != duplicateSource.Id && duplicatedTask.Text == duplicateSource.Text &&
       duplicatedTask.ReminderTime == duplicateSource.ReminderTime &&
       duplicatedTask.Priority == duplicateSource.Priority && duplicatedTask.IsPinned &&
       !duplicatedTask.IsCompleted && duplicatedTask.ElapsedTicks == 0,
    "Duplicating must copy task details into a fresh, incomplete task without timer history.");
Assert(TaskSortLogic.ByPriority(sortableTasks).Select(task => task.Text)
        .SequenceEqual(["Pinned", "Urgent", "Later", "Plain"]),
    "Priority sorting must keep pinned tasks first, then place high-priority tasks first.");
Assert(TaskSortLogic.ByTime(sortableTasks).Select(task => task.Text)
        .SequenceEqual(["Pinned", "Urgent", "Later", "Plain"]),
    "Time sorting must keep pinned tasks first, then order scheduled tasks.");

var calendarReference = new DateTime(2026, 8, 30, 12, 0, 0);
var calendarTasks = new List<TodoItem>
{
    new() { Text = "Today", ReminderTime = TimeSpan.FromHours(14) },
    new() { Text = "Daily", ReminderTime = TimeSpan.FromHours(9), Recurrence = TaskRecurrence.Daily },
    new() { Text = "Monday", ReminderTime = TimeSpan.FromHours(18), Recurrence = TaskRecurrence.Monday },
    new() { Text = "Exact", DueAt = new DateTime(2026, 9, 1, 10, 30, 0) },
    new() { Text = "Done", IsCompleted = true, DueAt = new DateTime(2026, 9, 1, 8, 0, 0) }
};
var calendarWeek = CalendarLogic.GetOccurrences(
    calendarTasks, new DateOnly(2026, 8, 30), 7, calendarReference);
Assert(calendarWeek.Count(item => item.Task.Text == "Daily") == 7,
    "The week calendar must show every daily recurrence.");
Assert(calendarWeek.Any(item => item.Task.Text == "Monday" && item.At.DayOfWeek == DayOfWeek.Monday) &&
       calendarWeek.Any(item => item.Task.Text == "Exact" && item.At == new DateTime(2026, 9, 1, 10, 30, 0)) &&
       calendarWeek.All(item => item.Task.Text != "Done"),
    "The calendar must resolve weekly and exact reminders while excluding completed tasks.");
Assert(CalendarLogic.StartOfWeek(new DateOnly(2026, 9, 3)) == new DateOnly(2026, 8, 31),
    "The week calendar must begin on Monday.");
Assert(GlobalShortcutSettings.DisplayName(GlobalShortcutSettings.QuickAddDefault) == "Ctrl+Alt+N" &&
       GlobalShortcutSettings.DisplayName(GlobalShortcutSettings.SleepNowDefault) == "Ctrl+Alt+S" &&
       GlobalShortcutSettings.DisplayName(GlobalShortcutSettings.Disabled) == "Disabled",
    "Global shortcuts must have clear tray-menu labels.");
Assert(GlobalShortcutSettings.IsValid(new GlobalShortcutGesture(GlobalShortcutModifiers.Control, '8')) &&
       GlobalShortcutSettings.IsValid(new GlobalShortcutGesture(GlobalShortcutModifiers.Alt, 0x70)) &&
       !GlobalShortcutSettings.IsValid(new GlobalShortcutGesture(GlobalShortcutModifiers.None, 'N')),
    "Global shortcut capture must accept supported modified keys and reject unmodified keys.");

Console.WriteLine("All Avocado logic checks passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
