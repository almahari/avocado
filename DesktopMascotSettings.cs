using System.Globalization;
using System.Text.RegularExpressions;

namespace Avocado;

/// <summary>Every optional desktop mascot behaviour; each one has its own tray toggle.</summary>
public enum DesktopMascotFeature
{
    MorningBriefing,
    DayWrapUp,
    DropToAdd,
    QuickAdd,
    Mood,
    FeedAndPet,
    TimeOfDay,
    SeasonalOutfits,
    ScreenAwareness,
    FriendVisits,
    SmartReminders,
    MeetingHeadsUp,
    HealthNudges,
    FrameAnimation,
    SoundEffects,
    SeeThrough,
    WeatherOutfits
}

public enum DesktopMascotSize { Small, Medium, Large }

public enum DesktopMascotMood { Happy, Content, Worried }

public enum DesktopMascotOutfit { None, SantaHat, WinterScarf, FlowerCrown, Sunglasses, PumpkinHat, Umbrella, SnowGear }

public enum DesktopMascotNudge { Stretch, Water, EyeRest }

public sealed record DesktopMascotFeatureInfo(DesktopMascotFeature Feature, string DisplayName, bool DefaultEnabled);

public sealed class DesktopMascotSettings
{
    public Dictionary<DesktopMascotFeature, bool> Features { get; set; } = [];
    public DesktopMascotSize Size { get; set; } = DesktopMascotSize.Medium;
    public List<int> SnoozeHistory { get; set; } = [];
    public DateOnly? LastBriefingDate { get; set; }
    public DateOnly? LastWrapUpDate { get; set; }
    public int WrapUpHour { get; set; } = 18;
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public DateTime? WeatherLocationAt { get; set; }

    public bool IsEnabled(DesktopMascotFeature feature) =>
        Features.TryGetValue(feature, out var enabled) ? enabled : DesktopMascotFeatures.Get(feature).DefaultEnabled;
}

public static partial class DesktopMascotFeatures
{
    public static IReadOnlyList<DesktopMascotFeatureInfo> All { get; } =
    [
        new(DesktopMascotFeature.MorningBriefing, "Morning briefing", true),
        new(DesktopMascotFeature.DayWrapUp, "End-of-day wrap-up", true),
        new(DesktopMascotFeature.DropToAdd, "Drop text to add task", true),
        new(DesktopMascotFeature.QuickAdd, "Quick add from bubble", true),
        new(DesktopMascotFeature.Mood, "Mood reacts to tasks", true),
        new(DesktopMascotFeature.FeedAndPet, "Feed && pet", true),
        new(DesktopMascotFeature.TimeOfDay, "Time-of-day behaviour", true),
        new(DesktopMascotFeature.SeasonalOutfits, "Seasonal outfits", true),
        new(DesktopMascotFeature.ScreenAwareness, "Watch cursor && dodge windows", true),
        new(DesktopMascotFeature.FriendVisits, "Friend visits", true),
        new(DesktopMascotFeature.SmartReminders, "Smart reminder escalation", true),
        new(DesktopMascotFeature.MeetingHeadsUp, "5-minute heads-up", true),
        new(DesktopMascotFeature.HealthNudges, "Break && health nudges", true),
        new(DesktopMascotFeature.FrameAnimation, "Frame-by-frame animation", true),
        new(DesktopMascotFeature.SoundEffects, "Sound effects", false),
        new(DesktopMascotFeature.SeeThrough, "See-through when not hovered", false),
        new(DesktopMascotFeature.WeatherOutfits, "Weather outfits (uses internet)", false)
    ];

    public static DesktopMascotFeatureInfo Get(DesktopMascotFeature feature) => All.First(info => info.Feature == feature);

    public static double SpriteSize(DesktopMascotSize size) => size switch
    {
        DesktopMascotSize.Small => 110,
        DesktopMascotSize.Large => 200,
        _ => 150
    };

    // ---------- Mood (#6) ----------

    public static DesktopMascotMood GetMood(int overdue, int stale, int completedToday)
    {
        var pressure = overdue * 2 + stale - completedToday;
        if (overdue >= 3 || pressure >= 6) return DesktopMascotMood.Worried;
        return completedToday > 0 && pressure <= 1 || overdue == 0 && stale == 0
            ? DesktopMascotMood.Happy : DesktopMascotMood.Content;
    }

    public static string MoodLine(DesktopMascotMood mood) => mood switch
    {
        DesktopMascotMood.Happy => "Feeling great! 😊",
        DesktopMascotMood.Worried => "A bit worried… some tasks are piling up 😟",
        _ => "Doing okay 🙂"
    };

    // ---------- Smart reminders (#13) ----------

    /// <summary>The snooze length the user picks most often (ties go to the most recent), default 10.</summary>
    public static int PreferredSnooze(IReadOnlyList<int> history)
    {
        if (history.Count == 0) return 10;
        return history.Select((minutes, index) => (minutes, index))
            .GroupBy(entry => entry.minutes)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(entry => entry.index))
            .First().Key;
    }

    public static void RecordSnooze(List<int> history, int minutes)
    {
        history.Add(minutes);
        if (history.Count > 20) history.RemoveRange(0, history.Count - 20);
    }

    /// <summary>Escalation level for an unanswered reminder: 1 wave, 2 tap on the glass, 3 big bubble.</summary>
    public static int EscalationLevel(TimeSpan unanswered) =>
        unanswered >= TimeSpan.FromMinutes(5) ? 3 : unanswered >= TimeSpan.FromMinutes(2) ? 2 : 1;

    // ---------- Heads-up (#14) ----------

    public static IReadOnlyList<TodoItem> UpcomingSoon(IEnumerable<TodoItem> tasks, DateTime now, ISet<Guid> alreadyWarned, int minutes = 5) =>
        tasks.Where(task => !task.IsCompleted && !alreadyWarned.Contains(task.Id) &&
                            NextOccurrence(task, now) is DateTime at &&
                            at > now && at - now <= TimeSpan.FromMinutes(minutes))
            .ToList();

    private static DateTime? NextOccurrence(TodoItem task, DateTime now)
    {
        if (task.DueAt is DateTime due) return due;
        if (task.ReminderTime is not TimeSpan time) return null;
        if (!TaskReminderLogic.MatchesDay(task.Recurrence, now.DayOfWeek)) return null;
        return now.Date + time;
    }

    // ---------- Time of day (#9) ----------

    public static string? TimeOfDayProp(DateTime now) => now.Hour switch
    {
        >= 22 or < 5 => "yawn",
        _ => null
    };

    public static bool IsTooLate(DateTime now) => now.Hour >= 23 || now.Hour < 4;

    // ---------- Seasonal outfits (#10) ----------

    public static DesktopMascotOutfit OutfitFor(DateTime now, SeasonalSkinKind skin) => skin switch
    {
        SeasonalSkinKind.HalloweenPumpkin => DesktopMascotOutfit.PumpkinHat,
        SeasonalSkinKind.WinterCap => DesktopMascotOutfit.SantaHat,
        SeasonalSkinKind.SpringBlossom => DesktopMascotOutfit.FlowerCrown,
        SeasonalSkinKind.SummerShades => DesktopMascotOutfit.Sunglasses,
        _ => now.Month switch
        {
            12 => DesktopMascotOutfit.SantaHat,
            1 or 2 => DesktopMascotOutfit.WinterScarf,
            3 or 4 or 5 => DesktopMascotOutfit.FlowerCrown,
            6 or 7 or 8 => DesktopMascotOutfit.Sunglasses,
            10 => DesktopMascotOutfit.PumpkinHat,
            _ => DesktopMascotOutfit.None
        }
    };

    // ---------- Health nudges (#15) ----------

    public static readonly TimeSpan NudgeEvery = TimeSpan.FromMinutes(60);

    public static (string Title, string Body) NudgeText(DesktopMascotNudge nudge) => nudge switch
    {
        DesktopMascotNudge.Water => ("Water break 💧", "You've been at it for an hour. Grab a glass of water with me!"),
        DesktopMascotNudge.EyeRest => ("Eye rest 👀", "20-20-20: look at something 20 feet away for 20 seconds."),
        _ => ("Stretch time 🙆", "Stand up and stretch with me — arms up high!")
    };

    // ---------- Treats (#8) ----------

    public static string TreatFor(FruitThemeKind theme) => theme switch
    {
        FruitThemeKind.Cat => "🐟",
        FruitThemeKind.Lion => "🍖",
        FruitThemeKind.Giraffe => "🍃",
        FruitThemeKind.Elephant => "🥜",
        FruitThemeKind.RedPanda => "🎋",
        FruitThemeKind.Fox => "🫐",
        FruitThemeKind.Koala => "🌿",
        FruitThemeKind.Avocado => "🥑",
        FruitThemeKind.Strawberry => "🍓",
        FruitThemeKind.Orange => "🍊",
        FruitThemeKind.Blueberry => "🫐",
        FruitThemeKind.Watermelon => "🍉",
        FruitThemeKind.Kiwi => "🥝",
        FruitThemeKind.Apple => "🍎",
        FruitThemeKind.Mango => "🥭",
        FruitThemeKind.Lemon => "🍋",
        FruitThemeKind.Tomato => "🍅",
        FruitThemeKind.Pumpkin => "🎃",
        FruitThemeKind.Potato => "🥔",
        FruitThemeKind.Onion => "🧅",
        FruitThemeKind.Papaya => "🍈",
        _ => "🍪"
    };

    /// <summary>Counts direction reversals of the cursor; enough quick back-and-forth means petting.</summary>
    public static bool IsPetting(IReadOnlyList<(DateTime At, double X)> samples, DateTime now)
    {
        var recent = samples.Where(sample => now - sample.At <= TimeSpan.FromSeconds(1.5)).ToList();
        var reversals = 0;
        var lastDirection = 0;
        for (var i = 1; i < recent.Count; i++)
        {
            var delta = recent[i].X - recent[i - 1].X;
            if (Math.Abs(delta) < 3) continue;
            var direction = Math.Sign(delta);
            if (lastDirection != 0 && direction != lastDirection) reversals++;
            lastDirection = direction;
        }
        return reversals >= 4;
    }

    // ---------- Quick add (#4) & drop to add (#3) ----------

    /// <summary>
    /// Turns friendly phrases like "remind me in 20 min to stretch" into the app's task syntax.
    /// Everything else is passed through for the normal task parser.
    /// </summary>
    public static string NormalizeQuickAdd(string input, DateTime now)
    {
        var text = input.Trim();
        var relative = RelativeReminder().Match(text);
        if (relative.Success)
        {
            var amount = int.Parse(relative.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var unit = relative.Groups["unit"].Value.ToLowerInvariant();
            var at = unit.StartsWith('h') ? now.AddHours(amount) : now.AddMinutes(amount);
            return $"{at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} {relative.Groups["text"].Value.Trim()}";
        }
        var plain = RemindMePrefix().Match(text);
        return plain.Success ? plain.Groups["text"].Value.Trim() : text;
    }

    /// <summary>Builds task input from dropped text, links or files.</summary>
    public static string? TaskFromDrop(string? text, string[]? files)
    {
        if (files is { Length: > 0 })
            return files.Length == 1
                ? $"Check {System.IO.Path.GetFileName(files[0])} : {files[0]}"
                : $"Check {files.Length} files: " + string.Join(", ", files.Select(System.IO.Path.GetFileName));
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        var firstLine = trimmed.Split('\n')[0].Trim();
        if (Uri.TryCreate(firstLine, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return $"{firstLine} : Read {uri.Host}";
        return firstLine.Length > 160 ? firstLine[..160].TrimEnd() + "…" : firstLine;
    }

    [GeneratedRegex(@"^(?:remind\s+me\s+)?in\s+(?<amount>\d{1,4})\s*(?<unit>m|min|mins|minute|minutes|h|hr|hrs|hour|hours)\s+(?:to\s+)?(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelativeReminder();

    [GeneratedRegex(@"^remind\s+me\s+(?:to\s+)?(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RemindMePrefix();
}
