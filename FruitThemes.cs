namespace Avocado;

public enum FruitThemeKind
{
    Avocado,
    Strawberry,
    Orange,
    Blueberry,
    Watermelon
}

public sealed record FruitThemePalette(
    FruitThemeKind Kind,
    string DisplayName,
    string Ink,
    string Cream,
    string Button,
    string ButtonBorder,
    string ButtonHover,
    string ButtonPressed,
    string Outer,
    string Middle,
    string Flesh,
    string Highlight,
    string Seed,
    string SeedHighlight,
    string MutedInk,
    string Task);

public static class FruitThemes
{
    public static IReadOnlyList<FruitThemePalette> All { get; } =
    [
        new(FruitThemeKind.Avocado, "Avocado", "#173D2B", "#FFF8D9", "#214F34", "#102A1E",
            "#2E6842", "#153522", "#173D2B", "#4F872D", "#9BC63E", "#B9DA58",
            "#713F25", "#9A5C32", "#315A2D", "#DFF08A"),
        new(FruitThemeKind.Strawberry, "Strawberry", "#4A1020", "#FFF1E8", "#7D1735", "#350916",
            "#A6264B", "#591026", "#681329", "#C92850", "#EF4F70", "#FF8FA3",
            "#D8A51D", "#F7D76A", "#7B2941", "#FFD1D9"),
        new(FruitThemeKind.Orange, "Orange", "#4A2408", "#FFF4D6", "#7A3608", "#351703",
            "#A94B0A", "#572605", "#6A2C05", "#D95C08", "#F28C18", "#FFC14D",
            "#6B3714", "#9B5B26", "#7A4517", "#FFD993"),
        new(FruitThemeKind.Blueberry, "Blueberry", "#171B4B", "#F1F0FF", "#29306D", "#101333",
            "#3F4895", "#1D2252", "#202657", "#3C48A0", "#6675D9", "#98A5F2",
            "#4A255F", "#74408A", "#353D77", "#CFD5FF"),
        new(FruitThemeKind.Watermelon, "Watermelon", "#173B2B", "#FFF1E9", "#21563A", "#0E291C",
            "#34734D", "#173D29", "#174B30", "#59A447", "#EE5A68", "#FF8B94",
            "#481C25", "#77313B", "#376746", "#FFC8CD")
    ];

    public static FruitThemePalette Default => All[0];

    public static FruitThemePalette Get(FruitThemeKind kind) =>
        All.FirstOrDefault(theme => theme.Kind == kind) ?? Default;
}
