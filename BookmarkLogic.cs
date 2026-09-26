namespace Avocado;

public sealed class BookmarkFolder
{
    public string Name { get; set; } = string.Empty;
    public List<BookmarkFolder> Folders { get; set; } = [];
    public List<BookmarkWebsite> Websites { get; set; } = [];
}

public sealed class BookmarkWebsite
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed record BookmarkWebsiteMatch(
    BookmarkWebsite Website,
    string FolderPath,
    int Score);

public static class BookmarkLogic
{
    public static int CountWebsites(IEnumerable<BookmarkFolder> folders) =>
        folders.Sum(folder => folder.Websites.Count + CountWebsites(folder.Folders));

    public static IReadOnlyList<BookmarkWebsiteMatch> Search(
        IEnumerable<BookmarkFolder> folders,
        string query,
        int limit = 20)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];

        var matches = new List<BookmarkWebsiteMatch>();
        foreach (var folder in folders)
            CollectMatches(folder, [], trimmed, ancestorMatched: false, matches);

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Website.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.FolderPath, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public static string BuildPath(IEnumerable<BookmarkFolder> folders) =>
        string.Join(" / ", folders.Select(folder => folder.Name));

    private static void CollectMatches(
        BookmarkFolder folder,
        IReadOnlyList<string> parentPath,
        string query,
        bool ancestorMatched,
        ICollection<BookmarkWebsiteMatch> matches)
    {
        if (string.IsNullOrWhiteSpace(folder.Name)) return;
        var path = parentPath.Append(folder.Name).ToList();
        var folderMatched = ancestorMatched || Contains(folder.Name, query);

        foreach (var website in folder.Websites)
        {
            if (string.IsNullOrWhiteSpace(website.Name) || string.IsNullOrWhiteSpace(website.Url)) continue;
            var nameMatched = Contains(website.Name, query);
            var urlMatched = Contains(website.Url, query);
            if (!folderMatched && !nameMatched && !urlMatched) continue;
            var score = nameMatched ? 300 : folderMatched ? 200 : 100;
            matches.Add(new BookmarkWebsiteMatch(website, string.Join(" / ", path), score));
        }

        foreach (var child in folder.Folders)
            CollectMatches(child, path, query, folderMatched, matches);
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
