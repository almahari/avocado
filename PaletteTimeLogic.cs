using System.Globalization;

namespace Avocado;

public sealed record PaletteTimeConversion(string SourceLabel, IReadOnlyList<WorldClockReading> Readings, string? Error = null);

public static class PaletteTimeLogic
{
    public static PaletteTimeConversion? Convert(string input, DateTimeOffset now, TimeZoneInfo sourceZone)
    {
        var text = input.Trim();
        if (text.Length != 5 || text[2] != ':' ||
            !TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return null;

        var current = TimeZoneInfo.ConvertTime(now, sourceZone);
        var local = DateTime.SpecifyKind(current.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);
        var source = $"{text} · {sourceZone.StandardName}";
        if (sourceZone.IsInvalidTime(local))
            return new PaletteTimeConversion(source, [], "This local time is skipped by daylight saving today. Choose another time.");

        var offset = sourceZone.GetUtcOffset(local);
        var ambiguous = sourceZone.IsAmbiguousTime(local);
        if (ambiguous)
        {
            var offsets = sourceZone.GetAmbiguousTimeOffsets(local);
            offset = offsets.Contains(current.Offset) ? current.Offset : offsets.Max();
        }
        var instant = new DateTimeOffset(local, offset);
        var label = $"From {source} · {WorldClockLogic.FormatUtcOffset(offset)}" + (ambiguous ? " (repeated hour)" : string.Empty);
        return new PaletteTimeConversion(label, WorldClockLogic.GetConversionReadings(instant));
    }
}
