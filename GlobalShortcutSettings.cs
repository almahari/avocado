namespace Avocado;

[Flags]
public enum GlobalShortcutModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4
}

public readonly record struct GlobalShortcutGesture(GlobalShortcutModifiers Modifiers, int VirtualKey)
{
    public bool IsDisabled => VirtualKey == 0;
}

public static class GlobalShortcutSettings
{
    public static GlobalShortcutGesture QuickAddDefault { get; } =
        new(GlobalShortcutModifiers.Control | GlobalShortcutModifiers.Alt, 'N');

    public static GlobalShortcutGesture ClipboardTaskDefault { get; } =
        new(GlobalShortcutModifiers.Control | GlobalShortcutModifiers.Alt, 'V');

    public static GlobalShortcutGesture Disabled => default;

    public static bool IsValid(GlobalShortcutGesture gesture) =>
        gesture.IsDisabled ||
        gesture.Modifiers != GlobalShortcutModifiers.None && IsSupportedKey(gesture.VirtualKey);

    public static GlobalShortcutGesture Normalize(
        GlobalShortcutGesture gesture,
        GlobalShortcutGesture fallback) => IsValid(gesture) ? gesture : fallback;

    public static string DisplayName(GlobalShortcutGesture gesture)
    {
        if (gesture.IsDisabled) return "Disabled";
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(GlobalShortcutModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(GlobalShortcutModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(GlobalShortcutModifiers.Shift)) parts.Add("Shift");
        parts.Add(KeyName(gesture.VirtualKey));
        return string.Join("+", parts);
    }

    public static bool IsSupportedKey(int virtualKey) =>
        virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or
            >= 0x70 and <= 0x87 or 0x20;

    private static string KeyName(int virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
        0x20 => "Space",
        _ => $"Key {virtualKey:X2}"
    };
}
