using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Avocado;

public partial class MainWindow
{
    private readonly Dictionary<FruitThemeKind, ImageSource> _dockSceneCache = [];

    private ImageSource GetMascotScene(FruitThemeKind theme)
    {
        if (_dockSceneCache.TryGetValue(theme, out var scene)) return scene;
        var path = theme == FruitThemeKind.Cat ? "Assets/cat-scene.png" : $"Assets/theme-scenes/{theme.ToString().ToLowerInvariant()}-scene.png";
        var bitmap = LoadMascotBitmap(path);
        _dockSceneCache[theme] = bitmap;
        return bitmap;
    }

    private static BitmapImage LoadMascotBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/Avocado;component/{path}");
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1024;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void UpdateDockWallpaper()
    {
        DockWallpaper.Background = null;
        var cat = _currentTheme.Kind == FruitThemeKind.Cat;
        // Cat keeps its existing artwork, palette, and placement.
        DockSurface.SetResourceReference(BackgroundProperty, cat ? "FleshBrush" : "CreamBrush");
        DockThemeScene.Visibility = cat ? Visibility.Collapsed : Visibility.Visible;
        if (cat) DockCatScene.Source = GetMascotScene(FruitThemeKind.Cat);
        else DockThemeScene.Source = GetMascotScene(_currentTheme.Kind);
    }
}
