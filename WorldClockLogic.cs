namespace Avocado;

public sealed record WorldClockReading(string Name, string Time, string Date, string Description, string UtcOffset = "");

public static class WorldClockLogic
{
    private static readonly TimeZoneInfo Bahrain = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
    private static readonly TimeZoneInfo UnitedKingdom = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
    private static readonly TimeZoneInfo Singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static IReadOnlyList<WorldClockReading> GetReadings(DateTimeOffset utcNow) =>
    [
        Reading("Bahrain", TimeZoneInfo.ConvertTime(utcNow, Bahrain), "Bahrain · UTC+3"),
        Reading("UK", TimeZoneInfo.ConvertTime(utcNow, UnitedKingdom), "United Kingdom · GMT / BST"),
        Reading("EDT", utcNow.ToOffset(TimeSpan.FromHours(-4)), "Eastern Daylight Time · UTC−4 (fixed)"),
        Reading("Singapore", TimeZoneInfo.ConvertTime(utcNow, Singapore), "Singapore · UTC+8")
    ];

    public static IReadOnlyList<WorldClockReading> GetConversionReadings(DateTimeOffset instant) =>
    [
        Reading("Bahrain", TimeZoneInfo.ConvertTime(instant, Bahrain), "Bahrain"),
        Reading("UK", TimeZoneInfo.ConvertTime(instant, UnitedKingdom),
            UnitedKingdom.IsDaylightSavingTime(instant) ? "British Summer Time" : "Greenwich Mean Time"),
        Reading("EDT", instant.ToOffset(TimeSpan.FromHours(-4)), "Eastern Daylight Time (fixed)"),
        Reading("EST", instant.ToOffset(TimeSpan.FromHours(-5)), "Eastern Standard Time (fixed)"),
        Reading("Singapore", TimeZoneInfo.ConvertTime(instant, Singapore), "Singapore"),
        Reading("India", TimeZoneInfo.ConvertTime(instant, India), "India Standard Time")
    ];

    public static string FormatUtcOffset(TimeSpan offset) =>
        $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";

    private static WorldClockReading Reading(string name, DateTimeOffset local, string description) =>
        new(name, local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            local.ToString("ddd, d MMM", System.Globalization.CultureInfo.InvariantCulture), description, FormatUtcOffset(local.Offset));
}
