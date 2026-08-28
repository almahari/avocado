namespace Avocado;

public static class TaskTimerLogic
{
    public static string Format(TimeSpan elapsed)
    {
        var safeElapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        var totalHours = (long)safeElapsed.TotalHours;
        return $"{totalHours:00}:{safeElapsed.Minutes:00}:{safeElapsed.Seconds:00}";
    }
}
