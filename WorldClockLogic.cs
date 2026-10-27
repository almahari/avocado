namespace Avocado;

public sealed record WorldClockReading(string Name, string Time, string Date, string Description);

public static class WorldClockLogic
{
    private static readonly TimeZoneInfo Bahrain = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
    private static readonly TimeZoneInfo UnitedKingdom = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
    private static readonly TimeZoneInfo Singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    public static IReadOnlyList<WorldClockReading> GetReadings(DateTimeOffset utcNow) =>
    [
        Reading("Bahrain", TimeZoneInfo.ConvertTime(utcNow, Bahrain), "Bahrain · UTC+3"),
        Reading("UK", TimeZoneInfo.ConvertTime(utcNow, UnitedKingdom), "United Kingdom · GMT / BST"),
        Reading("EDT", utcNow.ToOffset(TimeSpan.FromHours(-4)), "Eastern Daylight Time · UTC−4 (fixed)"),
        Reading("Singapore", TimeZoneInfo.ConvertTime(utcNow, Singapore), "Singapore · UTC+8")
    ];

    private static WorldClockReading Reading(string name, DateTimeOffset local, string description) =>
        new(name, local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            local.ToString("ddd, d MMM", System.Globalization.CultureInfo.InvariantCulture), description);
}
