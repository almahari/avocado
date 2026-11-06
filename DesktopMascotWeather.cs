using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Avocado;

public enum DesktopMascotWeatherKind { Clear, Cloudy, Fog, Drizzle, Rain, Snow, Storm }

/// <summary>One weather reading for the mascot's location.</summary>
public sealed record DesktopMascotWeatherReading(double TemperatureC, int Code, bool IsDay, DateTime FetchedAt)
{
    public DesktopMascotWeatherKind Kind => DesktopMascotWeather.Classify(Code);
}

/// <summary>Pure weather rules: WMO code classification, outfit choice and the mascot's comments.</summary>
public static class DesktopMascotWeather
{
    /// <summary>Weather is refreshed this often while the feature is on.</summary>
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(30);

    /// <summary>A reading older than this is ignored (e.g. after the PC was offline for a while).</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(3);

    /// <summary>The approximate location is looked up again after this long.</summary>
    public static readonly TimeSpan LocationMaxAge = TimeSpan.FromDays(1);

    public const double ColdBelowC = 12;
    public const double SunnyFromC = 24;
    public const double HotFromC = 32;

    /// <summary>Maps a WMO weather interpretation code (as used by Open-Meteo) to a simple kind.</summary>
    public static DesktopMascotWeatherKind Classify(int code) => code switch
    {
        0 or 1 => DesktopMascotWeatherKind.Clear,
        2 or 3 => DesktopMascotWeatherKind.Cloudy,
        45 or 48 => DesktopMascotWeatherKind.Fog,
        >= 51 and <= 57 => DesktopMascotWeatherKind.Drizzle,
        (>= 61 and <= 67) or (>= 80 and <= 82) => DesktopMascotWeatherKind.Rain,
        (>= 71 and <= 77) or 85 or 86 => DesktopMascotWeatherKind.Snow,
        >= 95 and <= 99 => DesktopMascotWeatherKind.Storm,
        _ => DesktopMascotWeatherKind.Cloudy
    };

    public static bool IsFresh(DesktopMascotWeatherReading? reading, DateTime now) =>
        reading is not null && now - reading.FetchedAt <= MaxAge && now >= reading.FetchedAt - TimeSpan.FromMinutes(5);

    /// <summary>The outfit the weather calls for, or null when the weather is unremarkable.</summary>
    public static DesktopMascotOutfit? OutfitFor(DesktopMascotWeatherReading reading) => reading.Kind switch
    {
        DesktopMascotWeatherKind.Rain or DesktopMascotWeatherKind.Storm or DesktopMascotWeatherKind.Drizzle => DesktopMascotOutfit.Umbrella,
        DesktopMascotWeatherKind.Snow => DesktopMascotOutfit.SnowGear,
        _ when reading.TemperatureC < ColdBelowC => DesktopMascotOutfit.WinterScarf,
        _ when reading.IsDay && (reading.TemperatureC >= HotFromC ||
                                 reading.Kind == DesktopMascotWeatherKind.Clear && reading.TemperatureC >= SunnyFromC) => DesktopMascotOutfit.Sunglasses,
        _ => null
    };

    /// <summary>A grouping used to decide when the weather changed enough to comment on it.</summary>
    public static string Mood(DesktopMascotWeatherReading reading) => OutfitFor(reading)?.ToString() ?? reading.Kind.ToString();

    public static string Icon(DesktopMascotWeatherReading reading) => reading.Kind switch
    {
        DesktopMascotWeatherKind.Clear => reading.IsDay ? "☀️" : "🌙",
        DesktopMascotWeatherKind.Cloudy => "☁️",
        DesktopMascotWeatherKind.Fog => "🌫️",
        DesktopMascotWeatherKind.Drizzle => "🌦️",
        DesktopMascotWeatherKind.Rain => "🌧️",
        DesktopMascotWeatherKind.Snow => "❄️",
        DesktopMascotWeatherKind.Storm => "⛈️",
        _ => "🌡️"
    };

    public static string Summary(DesktopMascotWeatherReading reading) =>
        $"{Icon(reading)} {Math.Round(reading.TemperatureC).ToString("0", CultureInfo.InvariantCulture)}°C, {Describe(reading.Kind, reading.IsDay)}";

    public static string Describe(DesktopMascotWeatherKind kind, bool isDay) => kind switch
    {
        DesktopMascotWeatherKind.Clear => isDay ? "sunny" : "clear night",
        DesktopMascotWeatherKind.Cloudy => "cloudy",
        DesktopMascotWeatherKind.Fog => "foggy",
        DesktopMascotWeatherKind.Drizzle => "drizzling",
        DesktopMascotWeatherKind.Rain => "raining",
        DesktopMascotWeatherKind.Snow => "snowing",
        DesktopMascotWeatherKind.Storm => "thunderstorm",
        _ => "mixed"
    };

    /// <summary>What the mascot says about the weather.</summary>
    public static string Comment(DesktopMascotWeatherReading reading) => reading.Kind switch
    {
        DesktopMascotWeatherKind.Storm => "Thunder outside! I'm hiding under my umbrella ☂️",
        DesktopMascotWeatherKind.Rain or DesktopMascotWeatherKind.Drizzle => "It's raining — good thing I brought my umbrella ☂️",
        DesktopMascotWeatherKind.Snow => "Snow! I put on my warm hat ☃️",
        _ when reading.TemperatureC < ColdBelowC => "Brr, it's chilly. Scarf on! 🧣",
        _ when reading.IsDay && reading.TemperatureC >= HotFromC => "It's hot out there! Shades on, stay hydrated 😎",
        _ when OutfitFor(reading) == DesktopMascotOutfit.Sunglasses => "Sunny day! Shades on 😎",
        DesktopMascotWeatherKind.Fog => "Foggy out there… I can barely see my paws 🌫️",
        DesktopMascotWeatherKind.Clear when !reading.IsDay => "Clear skies tonight 🌙",
        _ => "Nice and calm outside."
    };

    /// <summary>Parses an Open-Meteo <c>current=temperature_2m,weather_code,is_day</c> response.</summary>
    public static DesktopMascotWeatherReading? ParseOpenMeteo(string json, DateTime fetchedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("current", out var current)) return null;
            if (!current.TryGetProperty("temperature_2m", out var temperature) || temperature.ValueKind != JsonValueKind.Number) return null;
            if (!current.TryGetProperty("weather_code", out var code) || code.ValueKind != JsonValueKind.Number) return null;
            var isDay = !current.TryGetProperty("is_day", out var day) || day.ValueKind != JsonValueKind.Number || day.GetInt32() != 0;
            return new DesktopMascotWeatherReading(temperature.GetDouble(), code.GetInt32(), isDay, fetchedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses an approximate-location response that has numeric <c>latitude</c> and <c>longitude</c>.</summary>
    public static (double Latitude, double Longitude)? ParseLocation(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False) return null;
            if (!TryNumber(root, "latitude", out var latitude) || !TryNumber(root, "longitude", out var longitude)) return null;
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || latitude == 0 && longitude == 0) return null;
            return (latitude, longitude);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryNumber(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var element)) return false;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDouble(out value),
            // Some services return coordinates as strings.
            JsonValueKind.String => double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    public static string ForecastUrl(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude:0.###}&longitude={longitude:0.###}&current=temperature_2m,weather_code,is_day&timezone=auto");
}

/// <summary>
/// Fetches the current weather from Open-Meteo (free, no account). The location is approximated
/// from the public IP address and rounded, then cached in the mascot settings for a day.
/// </summary>
public static class DesktopMascotWeatherService
{
    private static readonly string[] LocationServices = ["https://ipwho.is/", "https://get.geojs.io/v1/ip/geo.json"];
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Avocado-Desktop-Mascot/1.0");
        return client;
    }

    public static async Task<DesktopMascotWeatherReading?> FetchAsync(DesktopMascotSettings settings, CancellationToken cancellation = default)
    {
        var now = DateTime.Now;
        if (settings.WeatherLatitude is not double latitude || settings.WeatherLongitude is not double longitude ||
            settings.WeatherLocationAt is not DateTime locatedAt || now - locatedAt > DesktopMascotWeather.LocationMaxAge)
        {
            var location = await LocateAsync(cancellation);
            if (location is null)
            {
                // Keep using a stale location rather than nothing.
                if (settings.WeatherLatitude is null || settings.WeatherLongitude is null) return null;
            }
            else
            {
                // Two decimals (~1 km) is plenty for weather and avoids storing a precise location.
                settings.WeatherLatitude = Math.Round(location.Value.Latitude, 2);
                settings.WeatherLongitude = Math.Round(location.Value.Longitude, 2);
                settings.WeatherLocationAt = now;
            }
            latitude = settings.WeatherLatitude!.Value;
            longitude = settings.WeatherLongitude!.Value;
        }

        try
        {
            var json = await Http.GetStringAsync(DesktopMascotWeather.ForecastUrl(latitude, longitude), cancellation);
            return DesktopMascotWeather.ParseOpenMeteo(json, DateTime.Now);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<(double Latitude, double Longitude)?> LocateAsync(CancellationToken cancellation)
    {
        foreach (var url in LocationServices)
        {
            try
            {
                var location = DesktopMascotWeather.ParseLocation(await Http.GetStringAsync(url, cancellation));
                if (location is not null) return location;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
            }
        }
        return null;
    }
}
