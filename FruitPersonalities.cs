namespace Avocado;

public enum ReminderMotion
{
    Horizontal,
    Vertical,
    Diagonal
}

public sealed record FruitPersonality(
    string Eyes,
    string Mouth,
    ReminderMotion ReminderMotion,
    double MotionStrength,
    int MotionMilliseconds);

public static class FruitPersonalities
{
    private static readonly IReadOnlyDictionary<FruitThemeKind, FruitPersonality> Items =
        new Dictionary<FruitThemeKind, FruitPersonality>
        {
            [FruitThemeKind.Avocado] = new("─", "ᴗ", ReminderMotion.Horizontal, 3.0, 520),
            [FruitThemeKind.Strawberry] = new("⌒", "ᴗ", ReminderMotion.Diagonal, 4.0, 390),
            [FruitThemeKind.Orange] = new("•", "◡", ReminderMotion.Vertical, 3.5, 460),
            [FruitThemeKind.Blueberry] = new("×", "﹏", ReminderMotion.Horizontal, 2.5, 610),
            [FruitThemeKind.Watermelon] = new("─", "o", ReminderMotion.Vertical, 2.5, 560),
            [FruitThemeKind.Kiwi] = new("˘", "ᴗ", ReminderMotion.Diagonal, 2.8, 500),
            [FruitThemeKind.Papaya] = new("⌁", "◡", ReminderMotion.Vertical, 3.8, 440),
            [FruitThemeKind.Apple] = new("⌒", "w", ReminderMotion.Horizontal, 3.2, 420),
            [FruitThemeKind.Mango] = new("˘", "~", ReminderMotion.Diagonal, 4.2, 370),
            [FruitThemeKind.Lemon] = new("－", "ᴗ", ReminderMotion.Horizontal, 2.2, 580),
            [FruitThemeKind.Tomato] = new("u", "o", ReminderMotion.Vertical, 3.6, 400),
            [FruitThemeKind.Pumpkin] = new("^", "ᴗ", ReminderMotion.Diagonal, 3.1, 540),
            [FruitThemeKind.Potato] = new("·", "﹏", ReminderMotion.Horizontal, 1.8, 680),
            [FruitThemeKind.Onion] = new("⌒", "°", ReminderMotion.Vertical, 2.7, 490)
        };

    public static FruitPersonality Get(FruitThemeKind kind) =>
        Items.TryGetValue(kind, out var personality) ? personality : Items[FruitThemeKind.Avocado];
}
