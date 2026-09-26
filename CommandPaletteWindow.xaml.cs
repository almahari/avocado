using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Avocado;

public partial class CommandPaletteWindow : Window
{
    private readonly CommandConfigStore _store;
    private readonly BookmarkConfigStore _bookmarkStore;
    private IReadOnlyList<CommandDefinition> _commands = [];
    private IReadOnlyList<BookmarkFolder> _bookmarks = [];
    private readonly List<BookmarkFolder> _bookmarkPath = [];
    private string? _loadError;
    private string? _bookmarkLoadError;
    private bool _isBrowsingBookmarks;
    private bool _dismissOnDeactivate;

    public CommandPaletteWindow(
        CommandConfigStore store,
        BookmarkConfigStore bookmarkStore,
        FruitThemePalette theme)
    {
        _store = store;
        _bookmarkStore = bookmarkStore;
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
        _dismissOnDeactivate = false;
        _commands = _store.Load(out _loadError);
        _bookmarks = _bookmarkStore.Load(out _bookmarkLoadError);
        _bookmarkPath.Clear();
        _isBrowsingBookmarks = false;
        CommandBox.Text = string.Empty;
        RefreshResults();

        if (!IsVisible) Show();
        CenterOnPointerScreen();
        Activate();
        CommandBox.Focus();
        Keyboard.Focus(CommandBox);
        ScheduleOutsideClickDismissal();
    }

    private void RefreshResults()
    {
        var query = CommandBox.Text.Trim();
        var entries = new List<PaletteEntry>();

        if (_isBrowsingBookmarks)
        {
            if (query.Length == 0)
            {
                var folders = _bookmarkPath.Count == 0 ? _bookmarks : _bookmarkPath[^1].Folders;
                IEnumerable<BookmarkWebsite> websites = _bookmarkPath.Count == 0
                    ? Array.Empty<BookmarkWebsite>()
                    : _bookmarkPath[^1].Websites;
                entries.AddRange(folders
                    .Where(folder => !string.IsNullOrWhiteSpace(folder.Name))
                    .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(CreateFolderEntry));
                entries.AddRange(websites
                    .Where(website => !string.IsNullOrWhiteSpace(website.Name) &&
                                      !string.IsNullOrWhiteSpace(website.Url))
                    .OrderBy(website => website.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(website => CreateWebsiteEntry(website, BookmarkLogic.BuildPath(_bookmarkPath))));
            }
            else
            {
                entries.AddRange(BookmarkLogic.Search(_bookmarks, query).Select(match =>
                    CreateWebsiteEntry(match.Website, match.FolderPath)));
            }
        }
        else
        {
            if (query.Length == 0 || "bookmarks".Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                var count = BookmarkLogic.CountWebsites(_bookmarks);
                entries.Add(new PaletteEntry(
                    "Bookmarks",
                    $"{count} website{(count == 1 ? string.Empty : "s")}",
                    "folder >",
                    PaletteEntryKind.BookmarkRoot));
            }

            entries.AddRange(CommandPaletteLogic.Search(_commands, query).Select(CreateCommandEntry));
            if (query.Length > 0)
                entries.AddRange(BookmarkLogic.Search(_bookmarks, query).Select(match =>
                    CreateWebsiteEntry(match.Website, match.FolderPath)));
        }

        ResultsList.ItemsSource = entries;
        ResultsList.SelectedIndex = ResultsList.Items.Count > 0 ? 0 : -1;
        StatusText.Text = GetStatusText(entries.Count);
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not PaletteEntry selected) return;
        if (selected.Kind == PaletteEntryKind.BookmarkRoot)
        {
            _isBrowsingBookmarks = true;
            _bookmarkPath.Clear();
            ClearSearchAndRefresh();
            return;
        }
        if (selected.Kind == PaletteEntryKind.Folder && selected.Folder is not null)
        {
            _isBrowsingBookmarks = true;
            _bookmarkPath.Add(selected.Folder);
            ClearSearchAndRefresh();
            return;
        }

        CommandMatch? resolved = null;
        if (selected.Kind == PaletteEntryKind.Command && selected.Command is not null)
        {
            resolved = CommandPaletteLogic.Resolve(selected.Command.Definition, CommandBox.Text);
            if (resolved is null)
            {
                StatusText.Text = "Complete the command parameters first";
                return;
            }
        }
        else if (selected.Kind == PaletteEntryKind.Website && selected.Website is not null)
        {
            var definition = new CommandDefinition
            {
                Command = selected.Website.Name,
                Action = "open-browser",
                Parameter = selected.Website.Url
            };
            resolved = new CommandMatch(definition, selected.Website.Url, true, 1000);
        }
        if (resolved is null) return;

        try
        {
            HidePalette();
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
            HidePalette();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Back && CommandBox.Text.Length == 0 && _isBrowsingBookmarks)
        {
            NavigateBack();
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

    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            System.Windows.Controls.ItemsControl.ContainerFromElement(ResultsList, source) is not
                System.Windows.Controls.ListBoxItem item) return;
        ResultsList.SelectedItem = item.DataContext;
        ExecuteSelected();
        e.Handled = true;
    }

    private void Window_Activated(object? sender, EventArgs e) => ScheduleOutsideClickDismissal();

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_dismissOnDeactivate) return;
        HidePalette();
    }

    private void ScheduleOutsideClickDismissal()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (IsVisible && IsActive) _dismissOnDeactivate = true;
        }, DispatcherPriority.ApplicationIdle);
    }

    private void HidePalette()
    {
        _dismissOnDeactivate = false;
        Hide();
    }

    private void NavigateBack()
    {
        if (_bookmarkPath.Count > 0)
            _bookmarkPath.RemoveAt(_bookmarkPath.Count - 1);
        else
            _isBrowsingBookmarks = false;
        RefreshResults();
        CommandBox.Focus();
    }

    private void ClearSearchAndRefresh()
    {
        CommandBox.Text = string.Empty;
        RefreshResults();
        CommandBox.Focus();
        Keyboard.Focus(CommandBox);
    }

    private string GetStatusText(int resultCount)
    {
        if (_loadError is not null) return $"Command config error: {_loadError}";
        if (_bookmarkLoadError is not null) return $"Bookmark config error: {_bookmarkLoadError}";
        if (_isBrowsingBookmarks)
        {
            var path = BookmarkLogic.BuildPath(_bookmarkPath);
            return path.Length == 0 ? "Bookmarks" : $"Bookmarks / {path}";
        }
        return resultCount == 0
            ? "No matching command or bookmark"
            : $"{resultCount} result{(resultCount == 1 ? string.Empty : "s")}";
    }

    private static PaletteEntry CreateCommandEntry(CommandMatch match) => new(
        match.Definition.Command,
        match.ExpandedParameter,
        match.Definition.ShowWindow ? "run-bash / visible" : match.Definition.Action,
        PaletteEntryKind.Command,
        Command: match);

    private static PaletteEntry CreateFolderEntry(BookmarkFolder folder)
    {
        var count = folder.Websites.Count + BookmarkLogic.CountWebsites(folder.Folders);
        return new PaletteEntry(
            folder.Name,
            $"{count} website{(count == 1 ? string.Empty : "s")}",
            "folder >",
            PaletteEntryKind.Folder,
            Folder: folder);
    }

    private static PaletteEntry CreateWebsiteEntry(BookmarkWebsite website, string folderPath) => new(
        website.Name,
        folderPath.Length == 0 ? website.Url : $"{folderPath} | {website.Url}",
        "website",
        PaletteEntryKind.Website,
        Website: website);

    private void SetBrush(string key, string value, double opacity = 1)
    {
        Resources[key] = new SolidColorBrush(ParseColor(value)) { Opacity = opacity };
    }

    private static System.Windows.Media.Color ParseColor(string value) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value)!;

}

public enum PaletteEntryKind
{
    Command,
    BookmarkRoot,
    Folder,
    Website
}

public sealed record PaletteEntry(
    string Title,
    string Subtitle,
    string Badge,
    PaletteEntryKind Kind,
    CommandMatch? Command = null,
    BookmarkFolder? Folder = null,
    BookmarkWebsite? Website = null);
