using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Avocado;

public sealed class LinkTextBlock : TextBlock
{
    private static readonly System.Windows.Media.Brush LinkBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 90, 130));

    public static readonly DependencyProperty LinkTextProperty = DependencyProperty.Register(
        nameof(LinkText),
        typeof(string),
        typeof(LinkTextBlock),
        new FrameworkPropertyMetadata(string.Empty, OnLinkTextChanged));

    public string LinkText
    {
        get => (string)GetValue(LinkTextProperty);
        set => SetValue(LinkTextProperty, value);
    }

    private static void OnLinkTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((LinkTextBlock)dependencyObject).RenderText(e.NewValue as string);

    private void RenderText(string? text)
    {
        Inlines.Clear();
        foreach (var segment in LinkParser.Parse(text))
        {
            if (segment.Uri is null)
            {
                Inlines.Add(new Run(segment.Text));
                continue;
            }

            var hyperlink = new Hyperlink(new Run(segment.Text))
            {
                NavigateUri = segment.Uri,
                Foreground = LinkBrush,
                TextDecorations = System.Windows.TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Open {segment.Uri.Host}"
            };
            hyperlink.RequestNavigate += OpenLink;
            Inlines.Add(hyperlink);
        }
    }

    private static void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri.Scheme is not ("http" or "https")) return;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (InvalidOperationException)
        {
            // Windows has no registered browser; keep the task usable.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The default browser could not be opened; keep the task usable.
        }
        e.Handled = true;
    }
}
