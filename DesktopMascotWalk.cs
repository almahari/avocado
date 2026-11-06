namespace Avocado;

/// <summary>A screen's work area in WPF units; strolls never leave it.</summary>
public sealed record DesktopMascotWalkArea(double Left, double Right, double Top, double Bottom)
{
    public bool Contains(double x) => x >= Left && x < Right;
}

/// <summary>Pure planning rules for the mascot's little strolls: a few steps to one side, a pause, then back home.</summary>
public static class DesktopMascotWalk
{
    /// <summary>Walking speed in WPF units per second.</summary>
    public const double Speed = 55;

    /// <summary>Steps per second, used for the bobbing gait.</summary>
    public const double StepsPerSecond = 2.2;

    /// <summary>How far a stroll goes from home (a few steps).</summary>
    public const double MinimumDistance = 40;
    public const double MaximumDistance = 110;

    public static TimeSpan NextWalkDelay(Random random) => TimeSpan.FromSeconds(random.Next(3 * 60, 7 * 60));

    /// <summary>How long the mascot stands at the far point before turning back.</summary>
    public static TimeSpan PauseAtTurn(Random random) => TimeSpan.FromMilliseconds(random.Next(700, 1500));

    /// <summary>Index of the area under <paramref name="x"/> (nearest one when x is between screens).</summary>
    public static int AreaAt(IReadOnlyList<DesktopMascotWalkArea> areas, double x)
    {
        if (areas.Count == 0) return -1;
        for (var i = 0; i < areas.Count; i++)
            if (areas[i].Contains(x)) return i;
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < areas.Count; i++)
        {
            var distance = Math.Min(Math.Abs(areas[i].Left - x), Math.Abs(areas[i].Right - x));
            if (distance < bestDistance) { best = i; bestDistance = distance; }
        }
        return best;
    }

    /// <summary>
    /// The turnaround point of a stroll from <paramref name="homeX"/>: a few steps left or right (random side, or the side
    /// with room), staying on the current screen. Null when there is no room on either side.
    /// </summary>
    public static double? PlanStroll(IReadOnlyList<DesktopMascotWalkArea> areas, double homeX, double halfWidth, Random random)
    {
        var index = AreaAt(areas, homeX);
        if (index < 0) return null;
        var area = areas[index];
        var min = area.Left + halfWidth;
        var max = area.Right - halfWidth;
        var distance = MinimumDistance + random.NextDouble() * (MaximumDistance - MinimumDistance);
        var sides = random.Next(2) == 0 ? new[] { -1, 1 } : [1, -1];
        foreach (var side in sides)
        {
            var room = side < 0 ? homeX - min : max - homeX;
            if (room >= MinimumDistance) return homeX + side * Math.Min(distance, room);
        }
        return null;
    }

    /// <summary>Gait offsets for a walking phase in radians: (vertical bob, rocking angle).</summary>
    public static (double Bob, double Rock) Gait(double phase) => (-Math.Abs(Math.Sin(phase)) * 4, Math.Sin(phase) * 3.5);
}
