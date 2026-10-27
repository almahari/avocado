using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Avocado;

public partial class MainWindow
{
    private readonly Dictionary<FruitThemeKind, ImageSource> _dockSceneCache = [];

    private void UpdateDockWallpaper()
    {
        DockWallpaper.Background = null;
        var cat = _currentTheme.Kind == FruitThemeKind.Cat;
        // Cat keeps its existing artwork, palette, and placement.
        DockSurface.SetResourceReference(BackgroundProperty, cat ? "FleshBrush" : "CreamBrush");
        DockThemeScene.Visibility = cat ? Visibility.Collapsed : Visibility.Visible;
        if (cat) return;
        if (!_dockSceneCache.TryGetValue(_currentTheme.Kind, out var scene))
        {
            var name = _currentTheme.Kind.ToString().ToLowerInvariant();
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Avocado;component/Assets/theme-scenes/{name}-scene.png");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 1024;
            bitmap.EndInit();
            bitmap.Freeze();
            _dockSceneCache[_currentTheme.Kind] = scene = bitmap;
        }
        DockThemeScene.Source = scene;
    }
}
