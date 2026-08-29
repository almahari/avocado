using System.Text.RegularExpressions;

namespace Avocado;

public static partial class TaskCategoryLogic
{
    public static IReadOnlyList<string> Extract(string text) => CategoryPattern()
        .Matches(text ?? string.Empty)
        .Select(match => $"#{match.Groups["tag"].Value.ToLowerInvariant()}")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool Matches(string text, string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || category.Equals("All", StringComparison.OrdinalIgnoreCase))
            return true;
        var normalized = category.StartsWith('#') ? category : $"#{category}";
        return Extract(text).Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?:^|\s)#(?<tag>[\p{L}\p{N}_-]+)")]
    private static partial Regex CategoryPattern();
}
