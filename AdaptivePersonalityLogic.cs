namespace Avocado;

public enum AdaptiveMood
{
    Calm,
    Happy,
    Focused,
    Worried,
    Tired
}

public sealed record AdaptiveExpression(string Eyes, string Mouth, string Label);

public static class AdaptivePersonalityLogic
{
    public static AdaptiveMood DetermineMood(
        int activeTaskCount,
        int overdueTaskCount,
        bool timerRunning,
        bool recentlyCompleted)
    {
        if (recentlyCompleted) return AdaptiveMood.Happy;
        if (timerRunning) return AdaptiveMood.Focused;
        if (overdueTaskCount > 0) return AdaptiveMood.Worried;
        if (activeTaskCount > 5) return AdaptiveMood.Tired;
        return AdaptiveMood.Calm;
    }

    public static AdaptiveExpression GetExpression(AdaptiveMood mood) => mood switch
    {
        AdaptiveMood.Happy => new("^     ^", "ᴗ", "Happy — task completed"),
        AdaptiveMood.Focused => new("•     •", "─", "Focused — timer running"),
        AdaptiveMood.Worried => new("⌒     ⌒", "﹏", "Worried — tasks are overdue"),
        AdaptiveMood.Tired => new("·     ·", "︵", "Tired — more than five tasks remain"),
        _ => new(string.Empty, string.Empty, "Calm")
    };
}
