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
            CollectMatches(folder, [], trimmed, ancestorScore: 0, matches);

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
        int ancestorScore,
        ICollection<BookmarkWebsiteMatch> matches)
    {
        if (string.IsNullOrWhiteSpace(folder.Name)) return;
        var path = parentPath.Append(folder.Name).ToList();
        var folderScore = Math.Max(ancestorScore, PaletteSearchLogic.Score(query, folder.Name));

        foreach (var website in folder.Websites)
        {
            if (string.IsNullOrWhiteSpace(website.Name) || string.IsNullOrWhiteSpace(website.Url)) continue;
            var nameScore = PaletteSearchLogic.Score(query, website.Name);
            var urlScore = PaletteSearchLogic.Score(query, website.Url);
            var score = Math.Max(nameScore + (nameScore > 0 ? 200 : 0),
                Math.Max(folderScore + (folderScore > 0 ? 100 : 0), urlScore));
            if (score == 0) continue;
            matches.Add(new BookmarkWebsiteMatch(website, string.Join(" / ", path), score));
        }

        foreach (var child in folder.Folders)
            CollectMatches(child, path, query, folderScore, matches);
    }
}
