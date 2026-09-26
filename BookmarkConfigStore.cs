using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Avocado;

public sealed class BookmarkConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Path { get; }

    public BookmarkConfigStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Avocado",
            "bookmarks.json");
    }

    public IReadOnlyList<BookmarkFolder> Load(out string? error)
    {
        error = null;
        try
        {
            EnsureExists();
            return JsonSerializer.Deserialize<List<BookmarkFolder>>(File.ReadAllText(Path), JsonOptions) ?? [];
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
        var defaults = new List<BookmarkFolder>
        {
            new()
            {
                Name = "Folder 1",
                Folders =
                [
                    new BookmarkFolder
                    {
                        Name = "Subfolder 1",
                        Websites =
                        [
                            new BookmarkWebsite { Name = "Website 1", Url = "https://example.com" }
                        ]
                    }
                ]
            }
        };
        File.WriteAllText(Path, JsonSerializer.Serialize(defaults, JsonOptions));
    }
}
