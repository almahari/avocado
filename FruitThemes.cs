namespace Avocado;

public enum FruitThemeKind
{
    Avocado,
    Strawberry,
    Orange,
    Blueberry,
    Watermelon,
    Kiwi,
    Papaya,
    Apple,
    Mango,
    Lemon,
    Tomato,
    Pumpkin,
    Potato,
    Onion
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
    string Accent,
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
            "#4F872D", "#713F25", "#9A5C32", "#315A2D", "#DFF08A"),
        new(FruitThemeKind.Strawberry, "Strawberry", "#4A1020", "#FFF1E8", "#7D1735", "#350916",
            "#A6264B", "#591026", "#681329", "#C92850", "#EF4F70", "#FF8FA3",
            "#3F7A36", "#D8A51D", "#F7D76A", "#7B2941", "#FFD1D9"),
        new(FruitThemeKind.Orange, "Orange", "#4A2408", "#FFF4D6", "#7A3608", "#351703",
            "#A94B0A", "#572605", "#6A2C05", "#D95C08", "#F28C18", "#FFC14D",
            "#4F7D2D", "#6B3714", "#9B5B26", "#7A4517", "#FFD993"),
        new(FruitThemeKind.Blueberry, "Blueberry", "#171B4B", "#F1F0FF", "#29306D", "#101333",
            "#3F4895", "#1D2252", "#202657", "#3C48A0", "#6675D9", "#98A5F2",
            "#5362B8", "#4A255F", "#74408A", "#353D77", "#CFD5FF"),
        new(FruitThemeKind.Watermelon, "Watermelon", "#173B2B", "#FFF1E9", "#21563A", "#0E291C",
            "#34734D", "#173D29", "#174B30", "#59A447", "#EE5A68", "#FF8B94",
            "#2F7D4A", "#481C25", "#77313B", "#376746", "#FFC8CD"),
        new(FruitThemeKind.Kiwi, "Kiwi", "#263514", "#FFF9DE", "#3F5724", "#19220D",
            "#587834", "#2D3E1A", "#4E341E", "#76502D", "#8FC34A", "#B7DB72",
            "#2A1B10", "#1B130C", "#5E3F24", "#51662D", "#D5EAA3"),
        new(FruitThemeKind.Papaya, "Papaya", "#4A2811", "#FFF3D9", "#754019", "#321A0B",
            "#9B5923", "#512C12", "#31592A", "#E57922", "#F7A23B", "#FFC86A",
            "#477438", "#25170E", "#5C3A1D", "#765126", "#FFD69A"),
        new(FruitThemeKind.Apple, "Apple", "#46131B", "#FFF1E6", "#741D2A", "#310B12",
            "#9B2938", "#53141F", "#651723", "#B92839", "#E64A57", "#FF7B83",
            "#3E7737", "#552B18", "#85502D", "#762B34", "#FFC8CC"),
        new(FruitThemeKind.Mango, "Mango", "#4B2B08", "#FFF5D8", "#78450A", "#321C03",
            "#A7620C", "#552F06", "#B85A0E", "#E88414", "#F4B52C", "#FFD867",
            "#477B35", "#8B3D0B", "#B9651B", "#795119", "#FFE18A"),
        new(FruitThemeKind.Lemon, "Lemon", "#3E3907", "#FFFCE1", "#69600B", "#2B2703",
            "#8F8410", "#4B4507", "#8E800C", "#C9B914", "#E8D82D", "#FFF171",
            "#62833A", "#756712", "#A39425", "#71691C", "#FFF4A3"),
        new(FruitThemeKind.Tomato, "Tomato", "#4A1114", "#FFF0E8", "#791C21", "#32090C",
            "#A62930", "#571318", "#71161C", "#BD2930", "#E3484D", "#FF777A",
            "#3F7A3A", "#6A231A", "#9A4934", "#792D31", "#FFC6C4"),
        new(FruitThemeKind.Pumpkin, "Pumpkin", "#48230A", "#FFF3DA", "#733A0D", "#301703",
            "#9D5012", "#512908", "#713108", "#C75B0B", "#EC7D18", "#FFA944",
            "#47713A", "#64300E", "#94501F", "#754119", "#FFD19A"),
        new(FruitThemeKind.Potato, "Potato", "#3D2B1A", "#FFF7E5", "#62482C", "#291C10",
            "#80603B", "#46321E", "#5C4228", "#8C6840", "#B58B58", "#D5AE78",
            "#755130", "#4A301B", "#76502D", "#67513A", "#E5C79D"),
        new(FruitThemeKind.Onion, "Onion", "#3E183F", "#FFF2F4", "#672969", "#2A0E2B",
            "#87388A", "#491D4A", "#5D245F", "#944F96", "#D7A7D6", "#F0D0E8",
            "#6D8B48", "#774D2D", "#A77A52", "#735176", "#F1D8EA")
    ];

    public static FruitThemePalette Default => All[0];

    public static FruitThemePalette Get(FruitThemeKind kind) =>
        All.FirstOrDefault(theme => theme.Kind == kind) ?? Default;
}
