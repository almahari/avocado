namespace Avocado;

public enum DoNotDisturbMode
{
    Off,
    Always,
    TenPmToSevenAm,
    ElevenPmToSevenAm,
    MidnightToEightAm
}

public sealed record DoNotDisturbChoice(
    DoNotDisturbMode Mode,
    string DisplayName,
    TimeOnly? Start,
    TimeOnly? End);

public static class DoNotDisturbSettings
{
    public static IReadOnlyList<DoNotDisturbChoice> Choices { get; } =
    [
        new(DoNotDisturbMode.Off, "Off", null, null),
        new(DoNotDisturbMode.Always, "Always", null, null),
        new(DoNotDisturbMode.TenPmToSevenAm, "22:00 – 07:00", new TimeOnly(22, 0), new TimeOnly(7, 0)),
        new(DoNotDisturbMode.ElevenPmToSevenAm, "23:00 – 07:00", new TimeOnly(23, 0), new TimeOnly(7, 0)),
        new(DoNotDisturbMode.MidnightToEightAm, "00:00 – 08:00", new TimeOnly(0, 0), new TimeOnly(8, 0))
    ];

    public static DoNotDisturbMode Normalize(DoNotDisturbMode mode) =>
        Choices.Any(choice => choice.Mode == mode) ? mode : DoNotDisturbMode.Off;

    public static bool IsActive(DoNotDisturbMode mode, DateTime now)
    {
        var choice = Choices.First(choice => choice.Mode == Normalize(mode));
        if (choice.Mode == DoNotDisturbMode.Always) return true;
        if (choice.Start is not TimeOnly start || choice.End is not TimeOnly end) return false;
        var current = TimeOnly.FromDateTime(now);
        return start < end
            ? current >= start && current < end
            : current >= start || current < end;
    }
}
