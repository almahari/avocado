namespace Avocado;

public static class PaletteSearchLogic
{
    public static int Score(string query, string candidate)
    {
        var needle = query.Trim().ToLowerInvariant();
        var value = candidate.Trim().ToLowerInvariant();
        if (needle.Length == 0 || value.Length == 0) return 0;

        var best = ScoreCore(needle, value);
        var words = value.Split(
            value.Where(character => !char.IsLetterOrDigit(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var word in words)
            best = Math.Max(best, Math.Max(0, ScoreCore(needle, word) - 30));

        var initials = new string(words.Where(word => word.Length > 0).Select(word => word[0]).ToArray());
        if (initials.StartsWith(needle, StringComparison.Ordinal)) best = Math.Max(best, 620);
        return best;
    }

    public static int BestScore(string query, IEnumerable<string> candidates) =>
        candidates.Select(candidate => Score(query, candidate)).DefaultIfEmpty().Max();

    private static int ScoreCore(string query, string candidate)
    {
        if (candidate.Equals(query, StringComparison.Ordinal)) return 900;
        if (candidate.StartsWith(query, StringComparison.Ordinal)) return 800;
        if (candidate.Contains(query, StringComparison.Ordinal)) return 700;
        if (query.Length < 3) return 0;

        if (IsSubsequence(query, candidate, out var skipped))
            return Math.Max(460, 590 - skipped * 4);

        var maximumDistance = Math.Min(3, Math.Max(1, query.Length / 3));
        var distance = LevenshteinDistance(query, candidate, maximumDistance);
        return distance <= maximumDistance ? 440 - distance * 40 : 0;
    }

    private static bool IsSubsequence(string query, string candidate, out int skipped)
    {
        var queryIndex = 0;
        var firstMatch = -1;
        var lastMatch = -1;
        for (var index = 0; index < candidate.Length && queryIndex < query.Length; index++)
        {
            if (candidate[index] != query[queryIndex]) continue;
            if (firstMatch < 0) firstMatch = index;
            lastMatch = index;
            queryIndex++;
        }
        skipped = queryIndex == query.Length ? lastMatch - firstMatch + 1 - query.Length : int.MaxValue;
        return queryIndex == query.Length;
    }

    private static int LevenshteinDistance(string left, string right, int cutoff)
    {
        if (Math.Abs(left.Length - right.Length) > cutoff) return cutoff + 1;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            var rowMinimum = current[0];
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitution = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitution);
                rowMinimum = Math.Min(rowMinimum, current[rightIndex]);
            }
            if (rowMinimum > cutoff) return cutoff + 1;
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
