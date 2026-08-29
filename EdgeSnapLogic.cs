namespace Avocado;

public readonly record struct WorkArea(double Left, double Top, double Width, double Height);

public static class EdgeSnapLogic
{
    public const double Threshold = 18;

    public static AppPosition Snap(
        double left,
        double top,
        double width,
        double height,
        WorkArea area,
        double threshold = Threshold)
    {
        var right = area.Left + area.Width;
        var bottom = area.Top + area.Height;
        var snappedLeft = Math.Abs(left - area.Left) <= threshold
            ? area.Left
            : Math.Abs(left + width - right) <= threshold
                ? right - width
                : left;
        var snappedTop = Math.Abs(top - area.Top) <= threshold
            ? area.Top
            : Math.Abs(top + height - bottom) <= threshold
                ? bottom - height
                : top;
        return new AppPosition(snappedLeft, snappedTop);
    }
}
