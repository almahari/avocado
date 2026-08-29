namespace Avocado;

public static class FruitGrowthLogic
{
    public const double MinimumScale = 0.86;

    public static double Progress(int activeTasks, int completedToday)
    {
        var completed = Math.Max(0, completedToday);
        var total = Math.Max(0, activeTasks) + completed;
        return total == 0 ? 0 : (double)completed / total;
    }

    public static double Scale(int activeTasks, int completedToday) =>
        MinimumScale + (1 - MinimumScale) * Progress(activeTasks, completedToday);
}
