namespace Avocado;

public enum SeasonalSkinKind
{
    None = 0,
    HalloweenPumpkin = 1,
    WinterCap = 2,
    SpringBlossom = 3,
    SummerShades = 4
}

public sealed record SeasonalSkin(SeasonalSkinKind Kind, string DisplayName);

public static class SeasonalSkins
{
    public static IReadOnlyList<SeasonalSkin> All { get; } =
    [
        new(SeasonalSkinKind.None, "None"),
        new(SeasonalSkinKind.HalloweenPumpkin, "Halloween pumpkin"),
        new(SeasonalSkinKind.WinterCap, "Winter cap"),
        new(SeasonalSkinKind.SpringBlossom, "Spring blossom"),
        new(SeasonalSkinKind.SummerShades, "Summer shades")
    ];

    public static SeasonalSkin Default => All[0];

    public static SeasonalSkin Get(SeasonalSkinKind kind) =>
        All.FirstOrDefault(skin => skin.Kind == kind) ?? Default;
}
