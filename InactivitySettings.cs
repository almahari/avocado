namespace Avocado;

public enum SleepTimeOption
{
    Never = 0,
    ThirtySeconds = 30,
    OneMinute = 60,
    TwoMinutes = 120
}

public sealed record SleepTimeChoice(SleepTimeOption Option, string DisplayName, TimeSpan? Duration);

public static class InactivitySettings
{
    public static IReadOnlyList<SleepTimeChoice> Choices { get; } =
    [
        new(SleepTimeOption.Never, "Never", null),
        new(SleepTimeOption.ThirtySeconds, "30 seconds", TimeSpan.FromSeconds(30)),
        new(SleepTimeOption.OneMinute, "1 minute", TimeSpan.FromMinutes(1)),
        new(SleepTimeOption.TwoMinutes, "2 minutes", TimeSpan.FromMinutes(2))
    ];

    public const SleepTimeOption Default = SleepTimeOption.TwoMinutes;
    public static readonly TimeSpan ResizeAnimationDuration = TimeSpan.FromMilliseconds(280);

    public static SleepTimeChoice Get(SleepTimeOption option) =>
        Choices.FirstOrDefault(choice => choice.Option == option) ?? Get(Default);
}
