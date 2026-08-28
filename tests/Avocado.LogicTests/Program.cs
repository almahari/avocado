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
    Left = 123,
    Top = 456,
    Tasks = [new TodoItem
    {
        Text = "Persist me",
        IsCompleted = true,
        IsExpanded = true,
        ElapsedTicks = TimeSpan.FromMinutes(12).Ticks
    }]
};
store.Save(originalState);
var loadedState = store.Load();
Assert(loadedState.AlwaysOnTop, "The selected window mode must persist.");
Assert(loadedState.SmallSize, "The selected app size must persist.");
Assert(loadedState.ResizeWhenInactive, "The resize-when-inactive option must persist.");
Assert(loadedState.Left == 123 && loadedState.Top == 456, "Window position must persist.");
Assert(loadedState.Tasks.Count == 1 && loadedState.Tasks[0].Text == "Persist me" && loadedState.Tasks[0].IsCompleted,
    "Tasks and completion state must persist.");
Assert(loadedState.Tasks[0].ElapsedTicks == TimeSpan.FromMinutes(12).Ticks,
    "A task's accumulated timer duration must persist.");
Assert(!loadedState.Tasks[0].IsExpanded, "Temporary task expansion state must not persist.");
Directory.Delete(Path.GetDirectoryName(statePath)!, recursive: true);

var normalSize = AppSizeLogic.Get(small: false);
var smallSize = AppSizeLogic.Get(small: true);
Assert(normalSize == new AppSize(420, 540, 1), "Normal size must preserve the current window dimensions.");
Assert(smallSize == new AppSize(210, 270, 0.5), "Small size must be exactly half of normal size.");
Assert(InactivitySettings.Timeout == TimeSpan.FromMinutes(2), "The sleep timeout must be exactly two minutes.");
Assert(TaskTimerLogic.Format(TimeSpan.Zero) == "00:00:00", "A new task timer must start at zero.");
Assert(TaskTimerLogic.Format(new TimeSpan(1, 2, 3, 4)) == "26:03:04",
    "Task timer formatting must preserve total hours beyond one day.");

Console.WriteLine("All Avocado logic checks passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
