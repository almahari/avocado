namespace Avocado;

public readonly record struct AppSize(double Width, double Height, double Scale);
public readonly record struct AppPosition(double Left, double Top);

public enum SleepFruitSize
{
    Normal,
    Small
}

public sealed record SleepFruitSizeChoice(SleepFruitSize Size, string DisplayName);

public static class SleepFruitSizeLogic
{
    public const SleepFruitSize Default = SleepFruitSize.Normal;

    public static IReadOnlyList<SleepFruitSizeChoice> Choices { get; } =
    [
        new(SleepFruitSize.Normal, "Normal"),
        new(SleepFruitSize.Small, "Small")
    ];

    public static SleepFruitSize Normalize(SleepFruitSize size) =>
        Choices.Any(choice => choice.Size == size) ? size : Default;

    public static AppSize Get(SleepFruitSize size)
    {
        var normal = AppSizeLogic.Sleeping;
        return Normalize(size) == SleepFruitSize.Small
            ? new AppSize(normal.Width / 2, normal.Height / 2, normal.Scale / 2)
            : normal;
    }
}

public enum SleepResizeAnchor
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public sealed record SleepResizeAnchorChoice(SleepResizeAnchor Anchor, string DisplayName);

public static class SleepResizeLogic
{
    public static IReadOnlyList<SleepResizeAnchorChoice> Choices { get; } =
    [
        new(SleepResizeAnchor.TopLeft, "Top left"),
        new(SleepResizeAnchor.TopRight, "Top right"),
        new(SleepResizeAnchor.BottomLeft, "Bottom left"),
        new(SleepResizeAnchor.BottomRight, "Bottom right")
    ];

    public static SleepResizeAnchor Normalize(SleepResizeAnchor anchor) =>
        Choices.Any(choice => choice.Anchor == anchor) ? anchor : SleepResizeAnchor.TopLeft;

    public static AppPosition GetTargetPosition(
        double left,
        double top,
        AppSize current,
        AppSize target,
        SleepResizeAnchor anchor)
    {
        var normalized = Normalize(anchor);
        var targetLeft = normalized is SleepResizeAnchor.TopRight or SleepResizeAnchor.BottomRight
            ? left + current.Width - target.Width
            : left;
        var targetTop = normalized is SleepResizeAnchor.BottomLeft or SleepResizeAnchor.BottomRight
            ? top + current.Height - target.Height
            : top;
        return new AppPosition(targetLeft, targetTop);
    }
}

public static class AppSizeLogic
{
    public static readonly AppSize Sleeping = new(126, 162, 0.3);

    public static AppSize Get(bool small) => small
        ? new AppSize(210, 270, 0.5)
        : new AppSize(420, 540, 1.0);
}
