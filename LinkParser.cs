using System.Text.RegularExpressions;

namespace Avocado;

public readonly record struct LinkSegment(string Text, Uri? Uri);

public static class LinkParser
{
    private static readonly Regex LinkPattern = new(
        @"(?:https?://|www\.)[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LabeledLinkPattern = new(
        @"^\s*(?<link>(?:https?://|www\.)\S+)\s+:\s+(?<label>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly char[] TrailingPunctuation = ['.', ',', ';', '!', ')', ']', '}', '"', '\''];

    public static IReadOnlyList<LinkSegment> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var labeledLink = LabeledLinkPattern.Match(text);
        if (labeledLink.Success &&
            TryCreateWebUri(labeledLink.Groups["link"].Value, out var labeledUri))
            return [new LinkSegment(labeledLink.Groups["label"].Value, labeledUri)];

        var segments = new List<LinkSegment>();
        var cursor = 0;
        foreach (Match match in LinkPattern.Matches(text))
        {
            if (match.Index > cursor)
                segments.Add(new LinkSegment(text[cursor..match.Index], null));

            var displayLink = match.Value.TrimEnd(TrailingPunctuation);
            var trailingText = match.Value[displayLink.Length..];
            if (TryCreateWebUri(displayLink, out var uri))
                segments.Add(new LinkSegment(displayLink, uri));
            else
                segments.Add(new LinkSegment(displayLink, null));

            if (trailingText.Length > 0)
                segments.Add(new LinkSegment(trailingText, null));
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
            segments.Add(new LinkSegment(text[cursor..], null));
        return segments;
    }

    private static bool TryCreateWebUri(string link, out Uri? uri)
    {
        var normalizedLink = link.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? $"https://{link}"
            : link;
        if (Uri.TryCreate(normalizedLink, UriKind.Absolute, out uri) &&
            uri.Scheme is "http" or "https") return true;
        uri = null;
        return false;
    }
}
