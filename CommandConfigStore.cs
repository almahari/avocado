using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Avocado;

public sealed class CommandConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Path { get; }

    public CommandConfigStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Avocado",
            "commands.json");
    }

    public IReadOnlyList<CommandDefinition> Load(out string? error)
    {
        error = null;
        try
        {
            EnsureExists();
            return JsonSerializer.Deserialize<List<CommandDefinition>>(File.ReadAllText(Path), JsonOptions) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return [];
        }
    }

    public void OpenInEditor()
    {
        EnsureExists();
        Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
    }

    private void EnsureExists()
    {
        if (File.Exists(Path)) return;
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);
        var defaults = new List<CommandDefinition>
        {
            new() { Command = "google", Action = "open-browser", Parameter = "https://www.google.com" },
            new() { Command = "j%1", Action = "open-browser", Parameter = "https://jira.com/%1" },
            new() { Command = "regex:^gh\\s+(.+)$", Action = "open-browser", Parameter = "https://github.com/search?q=%1" },
            new() { Command = "echo %1", Action = "run-bash", Parameter = "echo \"%1\"" }
        };
        File.WriteAllText(Path, JsonSerializer.Serialize(defaults, JsonOptions));
    }
}
