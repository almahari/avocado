namespace Avocado;

public enum ReminderSoundMode
{
    Silent,
    Soft,
    FruitSpecific
}

public sealed record ReminderSoundChoice(ReminderSoundMode Mode, string DisplayName);

public static class ReminderSoundSettings
{
    public static IReadOnlyList<ReminderSoundChoice> Choices { get; } =
    [
        new(ReminderSoundMode.Silent, "Silent"),
        new(ReminderSoundMode.Soft, "Soft"),
        new(ReminderSoundMode.FruitSpecific, "Fruit-specific")
    ];

    public const ReminderSoundMode Default = ReminderSoundMode.Soft;

    public static ReminderSoundMode Normalize(ReminderSoundMode mode) =>
        Choices.Any(choice => choice.Mode == mode) ? mode : Default;
}
