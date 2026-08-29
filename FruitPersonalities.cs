namespace Avocado;

public enum ReminderMotion
{
    Horizontal,
    Vertical,
    Diagonal
}

public sealed record FruitPersonality(string Eyes, string Mouth, ReminderMotion ReminderMotion);

public static class FruitPersonalities
{
    private static readonly IReadOnlyDictionary<FruitThemeKind, FruitPersonality> Items =
        new Dictionary<FruitThemeKind, FruitPersonality>
        {
            [FruitThemeKind.Avocado] = new("─", "ᴗ", ReminderMotion.Horizontal),
            [FruitThemeKind.Strawberry] = new("⌒", "ᴗ", ReminderMotion.Diagonal),
            [FruitThemeKind.Orange] = new("•", "◡", ReminderMotion.Vertical),
            [FruitThemeKind.Blueberry] = new("×", "﹏", ReminderMotion.Horizontal),
            [FruitThemeKind.Watermelon] = new("─", "o", ReminderMotion.Vertical),
            [FruitThemeKind.Kiwi] = new("˘", "ᴗ", ReminderMotion.Diagonal),
            [FruitThemeKind.Papaya] = new("⌁", "◡", ReminderMotion.Vertical),
            [FruitThemeKind.Apple] = new("⌒", "w", ReminderMotion.Horizontal),
            [FruitThemeKind.Mango] = new("˘", "~", ReminderMotion.Diagonal),
            [FruitThemeKind.Lemon] = new("－", "ᴗ", ReminderMotion.Horizontal),
            [FruitThemeKind.Tomato] = new("u", "o", ReminderMotion.Vertical),
            [FruitThemeKind.Pumpkin] = new("^", "ᴗ", ReminderMotion.Diagonal),
            [FruitThemeKind.Potato] = new("·", "﹏", ReminderMotion.Horizontal),
            [FruitThemeKind.Onion] = new("⌒", "°", ReminderMotion.Vertical)
        };

    public static FruitPersonality Get(FruitThemeKind kind) =>
        Items.TryGetValue(kind, out var personality) ? personality : Items[FruitThemeKind.Avocado];
}
