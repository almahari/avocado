using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Avocado;

public partial class CommandPaletteWindow : Window
{
    private readonly CommandConfigStore _store;
    private IReadOnlyList<CommandDefinition> _commands = [];
    private string? _loadError;

    public CommandPaletteWindow(CommandConfigStore store, FruitThemePalette theme)
    {
        _store = store;
        InitializeComponent();
        ApplyTheme(theme);
    }

    public void ApplyTheme(FruitThemePalette theme)
    {
        SetBrush("PaletteInk", theme.Ink);
        SetBrush("PaletteCream", theme.Cream);
        SetBrush("PaletteLeaf", theme.Highlight);
        SetBrush("PaletteBackground", theme.Outer, 0.95);
        SetBrush("PaletteBorder", theme.Flesh);
        SetBrush("PaletteInputBorder", theme.ButtonBorder);
        SetBrush("PaletteResults", theme.ButtonPressed, 0.78);
        SetBrush("PaletteRowBorder", theme.ButtonHover);
        SetBrush("PaletteSelected", theme.MutedInk);
        SetBrush("PaletteHover", theme.ButtonHover);
        SetBrush("PaletteBadge", theme.Button);
        SetBrush("PaletteMuted", theme.Task);
        PaletteShadow.Color = ParseColor(theme.ButtonBorder);
    }

    public void Open()
    {
        _commands = _store.Load(out _loadError);
        CommandBox.Text = string.Empty;
        RefreshResults();

        if (!IsVisible) Show();
        CenterOnPointerScreen();
        Activate();
        CommandBox.Focus();
        Keyboard.Focus(CommandBox);
    }

    private void RefreshResults()
    {
        var matches = CommandPaletteLogic.Search(_commands, CommandBox.Text);
        ResultsList.ItemsSource = matches;
        ResultsList.SelectedIndex = ResultsList.Items.Count > 0 ? 0 : -1;
        StatusText.Text = _loadError is not null
            ? $"Config error: {_loadError}"
            : matches.Count == 0
                ? "No matching command"
                : $"{matches.Count} command{(matches.Count == 1 ? string.Empty : "s")}";
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not CommandMatch selected) return;
        var resolved = CommandPaletteLogic.Resolve(selected.Definition, CommandBox.Text);
        if (resolved is null)
        {
            StatusText.Text = "Complete the command parameters first";
            return;
        }

        try
        {
            Hide();
            CommandActionExecutor.Execute(resolved);
        }
        catch (Exception exception)
        {
            Show();
            Activate();
            CommandBox.Focus();
            StatusText.Text = $"Could not run command: {exception.Message}";
        }
    }

    private void CenterOnPointerScreen()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = screen.WorkingArea.Left / dpi.DpiScaleX;
        var top = screen.WorkingArea.Top / dpi.DpiScaleY;
        var width = screen.WorkingArea.Width / dpi.DpiScaleX;
        var height = screen.WorkingArea.Height / dpi.DpiScaleY;
        Left = left + (width - Width) / 2;
        Top = top + Math.Max(36, (height - Height) * 0.38);
    }

    private void CommandBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsInitialized) return;
        RefreshResults();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Min(ResultsList.Items.Count - 1, ResultsList.SelectedIndex + 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Up && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Max(0, ResultsList.SelectedIndex - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void SetBrush(string key, string value, double opacity = 1)
    {
        Resources[key] = new SolidColorBrush(ParseColor(value)) { Opacity = opacity };
    }

    private static System.Windows.Media.Color ParseColor(string value) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value)!;

}
