using Forms = System.Windows.Forms;

namespace Avocado;

public sealed class ShortcutCaptureDialog : Forms.Form
{
    private readonly Forms.Label _shortcutLabel;
    private GlobalShortcutGesture _selected;
    private bool _hasResult;

    private ShortcutCaptureDialog(string actionName, GlobalShortcutGesture current)
    {
        _selected = current;
        Text = $"Avocado — {actionName} shortcut";
        Width = 390;
        Height = 205;
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = Forms.FormStartPosition.CenterScreen;
        TopMost = true;
        KeyPreview = true;
        Font = new System.Drawing.Font("Consolas", 10);

        var instructions = new Forms.Label
        {
            Text = "Press Ctrl, Alt, or Shift plus a letter, number,\nfunction key, or Space.",
            Left = 18,
            Top = 18,
            Width = 340,
            Height = 42
        };
        _shortcutLabel = new Forms.Label
        {
            Text = GlobalShortcutSettings.DisplayName(current),
            Left = 18,
            Top = 68,
            Width = 340,
            Height = 30,
            BorderStyle = Forms.BorderStyle.FixedSingle,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new System.Drawing.Font("Consolas", 12, System.Drawing.FontStyle.Bold)
        };
        var saveButton = new Forms.Button { Text = "Save", Left = 116, Top = 112, Width = 74 };
        var disableButton = new Forms.Button { Text = "Disable", Left = 196, Top = 112, Width = 78 };
        var cancelButton = new Forms.Button { Text = "Cancel", Left = 280, Top = 112, Width = 74 };
        saveButton.Click += (_, _) => Accept(_selected);
        disableButton.Click += (_, _) => Accept(GlobalShortcutSettings.Disabled);
        cancelButton.Click += (_, _) => Close();
        Controls.AddRange([instructions, _shortcutLabel, saveButton, disableButton, cancelButton]);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        KeyDown += CaptureKeyDown;
    }

    public static GlobalShortcutGesture? Show(string actionName, GlobalShortcutGesture current)
    {
        using var dialog = new ShortcutCaptureDialog(actionName, current);
        dialog.ShowDialog();
        return dialog._hasResult ? dialog._selected : null;
    }

    private void CaptureKeyDown(object? sender, Forms.KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        if (e.KeyCode is Forms.Keys.ControlKey or Forms.Keys.Menu or Forms.Keys.ShiftKey) return;
        var modifiers = GlobalShortcutModifiers.None;
        if (e.Control) modifiers |= GlobalShortcutModifiers.Control;
        if (e.Alt) modifiers |= GlobalShortcutModifiers.Alt;
        if (e.Shift) modifiers |= GlobalShortcutModifiers.Shift;
        var candidate = new GlobalShortcutGesture(modifiers, (int)e.KeyCode);
        if (!GlobalShortcutSettings.IsValid(candidate) || candidate.IsDisabled)
        {
            _shortcutLabel.Text = "Add at least one modifier";
            return;
        }
        _selected = candidate;
        _shortcutLabel.Text = GlobalShortcutSettings.DisplayName(candidate);
    }

    private void Accept(GlobalShortcutGesture gesture)
    {
        _selected = gesture;
        _hasResult = true;
        Close();
    }
}
