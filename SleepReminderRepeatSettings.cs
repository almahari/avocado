namespace Avocado;

public enum SleepReminderRepeatOption
{
    FiveMinutes,
    TenMinutes,
    TwentyMinutes,
    ThirtyMinutes
}

public sealed record SleepReminderRepeatChoice(
    SleepReminderRepeatOption Option,
    string DisplayName,
    TimeSpan Interval);

public static class SleepReminderRepeatSettings
{
    public const SleepReminderRepeatOption Default = SleepReminderRepeatOption.TenMinutes;

    public static IReadOnlyList<SleepReminderRepeatChoice> Choices { get; } =
    [
        new(SleepReminderRepeatOption.FiveMinutes, "5 minutes", TimeSpan.FromMinutes(5)),
        new(SleepReminderRepeatOption.TenMinutes, "10 minutes", TimeSpan.FromMinutes(10)),
        new(SleepReminderRepeatOption.TwentyMinutes, "20 minutes", TimeSpan.FromMinutes(20)),
        new(SleepReminderRepeatOption.ThirtyMinutes, "30 minutes", TimeSpan.FromMinutes(30))
    ];

    public static SleepReminderRepeatChoice Get(SleepReminderRepeatOption option) =>
        Choices.FirstOrDefault(choice => choice.Option == option) ??
        Choices.First(choice => choice.Option == Default);
}
