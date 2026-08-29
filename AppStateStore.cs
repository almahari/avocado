using System.Text.Json;
using System.IO;

namespace Avocado;

public sealed class AppState
{
    public List<TodoItem> Tasks { get; set; } = [];
    public List<TodoItem> ArchivedTasks { get; set; } = [];
    public bool AlwaysOnTop { get; set; }
    public bool SmallSize { get; set; }
    public bool ResizeWhenInactive { get; set; }
    public SleepTimeOption SleepTime { get; set; } = InactivitySettings.Default;
    public SleepResizeAnchor SleepResizeAnchor { get; set; } = SleepResizeAnchor.TopLeft;
    public ReminderSoundMode ReminderSound { get; set; } = ReminderSoundSettings.Default;
    public DoNotDisturbMode DoNotDisturb { get; set; } = DoNotDisturbMode.Off;
    public bool AdaptivePersonalityEnabled { get; set; } = true;
    public FruitThemeKind Theme { get; set; } = FruitThemeKind.Avocado;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public string? LastMonitor { get; set; }
    public Dictionary<string, SavedWindowPosition> MonitorPositions { get; set; } = [];
}

public sealed record SavedWindowPosition(double Left, double Top);

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public AppStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Avocado",
            "state.json");
    }

    public AppState Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppState();
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(_path), JsonOptions) ?? new AppState();
        }
        catch (JsonException)
        {
            return new AppState();
        }
        catch (IOException)
        {
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(tempPath, _path, overwrite: true);
    }
}
