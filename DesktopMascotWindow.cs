using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Path = System.Windows.Shapes.Path;

namespace Avocado;

public interface IDesktopMascotHost
{
    IEnumerable<TodoItem> OpenTasks { get; }
    /// <summary>Open tasks including recurring ones done for today (so the digest can list them for tomorrow).</summary>
    IEnumerable<TodoItem> DigestTasks { get; }
    FruitThemePalette MascotTheme { get; }
    bool IsMascotQuiet { get; }
    void CompleteMascotTask(TodoItem task);
    void SnoozeMascotTask(TodoItem task, int minutes);
    void ShowMainFromMascot();
    void SaveMascotPosition(double left, double top);
    void HideMascotFromMascot();
    DesktopMascotSettings MascotSettings { get; }
    void SaveMascotSettings();
    int AddMascotTasks(string input);
    int CompletedTodayCount { get; }
    void MoveMascotTaskToTomorrow(TodoItem task);
    ReminderSoundMode MascotSoundMode { get; }
    SeasonalSkinKind MascotSeasonalSkin { get; }
}

/// <summary>
/// A transparent, always-on-top, draggable desktop mascot that plays while idle,
/// reminds the user about old tasks and pops a speech bubble when a reminder fires.
/// </summary>
public sealed partial class DesktopMascotWindow : Window
{
    private double _spriteSize = DesktopMascotLogic.SpriteSize;
    private double WindowWidth => Math.Max(300, _spriteSize + 150);
    private double WindowHeight => _spriteSize + 210;
    private const double LargeWidth = 440;
    private double LargeHeight => _spriteSize + 610;

    private readonly IDesktopMascotHost _host;
    private readonly Random _random = new();
    private readonly Dictionary<string, ImageSource?> _spriteCache = [];
    private readonly Dictionary<Guid, DateTime> _lastNagged = [];
    private readonly DispatcherTimer _idleTimer = new();
    private readonly DispatcherTimer _poseTimer = new();
    private readonly DispatcherTimer _bubbleTimer = new();
    private readonly DispatcherTimer _nagTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _digestClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private TextBlock? _digestClockText;
    private TextBlock? _digestDateText;
    private bool _isLarge;
    private Image _spriteFront = NewSpriteLayer(1);
    private Image _spriteBack = NewSpriteLayer(0);
    private readonly Canvas _outfit = new() { IsHitTestVisible = false };
    private readonly RotateTransform _look = new();
    private readonly TranslateTransform _lookShift = new();
    private readonly Grid _root = new();
    private readonly Grid _actor = new();
    private readonly Grid _spriteHost = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.Hand, Background = null };
    // "Alive" layer (always running) and "trick" layer (one-off actions) are separate so they blend.
    private readonly ScaleTransform _breath = new(1, 1);
    private readonly RotateTransform _sway = new();
    private readonly TranslateTransform _bob = new();
    private readonly ScaleTransform _squash = new(1, 1);
    private readonly RotateTransform _tilt = new();
    private readonly TranslateTransform _bounce = new();
    private readonly Border _bubble = new() { Visibility = Visibility.Collapsed };
    private readonly Path _bubbleTail = new() { Visibility = Visibility.Collapsed };
    private readonly Grid _effects = new() { IsHitTestVisible = false };
    private DesktopMascotPose _pose = DesktopMascotPose.Idle;
    private DesktopMascotIdleAction? _lastAction;
    private DateTime _nextNagAt = DateTime.Now.AddMinutes(2);
    private bool _bubbleIsReminder;
    private bool _isDragging;
    private bool _sleeping;
    private bool _closing;

    public DesktopMascotWindow(IDesktopMascotHost host, double? left, double? top)
    {
        _host = host;
        _spriteSize = DesktopMascotFeatures.SpriteSize(host.MascotSettings.Size);
        Title = "Avocado mascot";
        Width = WindowWidth;
        Height = WindowHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var area = SystemParameters.WorkArea;
        var (x, y) = DesktopMascotLogic.ClampToArea(
            left ?? area.Right - WindowWidth - 24, top ?? area.Bottom - WindowHeight,
            WindowWidth, WindowHeight, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = x;
        Top = y;

        Content = BuildLayout();
        ApplyTheme();
        SetPose(DesktopMascotPose.Idle, instant: true);

        _idleTimer.Tick += (_, _) => PerformIdleAction();
        _poseTimer.Tick += (_, _) => { _poseTimer.Stop(); ReturnToRest(); };
        _bubbleTimer.Tick += (_, _) => { _bubbleTimer.Stop(); HideBubble(); };
        _nagTimer.Tick += (_, _) => Tick();
        _digestClock.Tick += (_, _) => UpdateDigestClock();
        SourceInitialized += (_, _) => HideFromAltTab();
        Loaded += (_, _) =>
        {
            StartBreathing();
            ScheduleIdle();
            _nagTimer.Start();
            Tick();
            Greet();
            StartFeatures();
        };
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    private UIElement BuildLayout()
    {
        var root = _root;

        // Sprite stack: the host carries the always-on "alive" motion, the actor carries tricks,
        // and two image layers cross-fade so poses blend instead of popping.
        _spriteHost.RenderTransformOrigin = new System.Windows.Point(0.5, 1);
        var alive = new TransformGroup();
        alive.Children.Add(_breath);
        alive.Children.Add(_sway);
        alive.Children.Add(_bob);
        alive.Children.Add(_look);
        alive.Children.Add(_lookShift);
        _spriteHost.RenderTransform = alive;
        _spriteHost.Margin = new Thickness(0, 0, 0, 6);
        var tricks = new TransformGroup();
        tricks.Children.Add(_squash);
        tricks.Children.Add(_tilt);
        tricks.Children.Add(_bounce);
        _actor.RenderTransformOrigin = new System.Windows.Point(0.5, 1);
        _actor.RenderTransform = tricks;
        _actor.Children.Add(_spriteBack);
        _actor.Children.Add(_spriteFront);
        _actor.Children.Add(_outfit);
        _spriteHost.Children.Add(_actor);
        AttachInteraction();
        _spriteHost.MouseLeftButtonDown += Sprite_MouseLeftButtonDown;
        _spriteHost.MouseRightButtonUp += (_, e) => { ShowMenu(); e.Handled = true; };
        _spriteHost.MouseEnter += (_, _) => { if (!_isDragging && !_sleeping && _pose == DesktopMascotPose.Idle && _bubble.Visibility != Visibility.Visible) Wiggle(); };
        _spriteHost.ToolTip = "Drag me anywhere • double-click to open Avocado • right-click for options";

        _effects.VerticalAlignment = VerticalAlignment.Bottom;
        _effects.Height = _spriteSize + 30;

        _bubble.CornerRadius = new CornerRadius(16);
        _bubble.BorderThickness = new Thickness(2.5);
        _bubble.Padding = new Thickness(14, 10, 14, 12);
        _bubble.MaxWidth = WindowWidth - 12;
        _bubble.HorizontalAlignment = HorizontalAlignment.Center;
        _bubble.VerticalAlignment = VerticalAlignment.Bottom;
        _bubble.Margin = new Thickness(6, 0, 6, _spriteSize + 18);
        _bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.28, Color = Colors.Black };
        _bubble.RenderTransformOrigin = new System.Windows.Point(0.5, 1);

        _bubbleTail.Data = Geometry.Parse("M 0,0 L 22,0 L 6,16 Z");
        _bubbleTail.StrokeThickness = 2.5;
        _bubbleTail.HorizontalAlignment = HorizontalAlignment.Center;
        _bubbleTail.VerticalAlignment = VerticalAlignment.Bottom;
        _bubbleTail.Margin = new Thickness(0, 0, 30, _spriteSize + 4);

        root.Children.Add(_effects);
        root.Children.Add(_spriteHost);
        root.Children.Add(_bubbleTail);
        root.Children.Add(_bubble);
        return root;
    }

    // ---------- Theme & sprites ----------

    public void ApplyTheme()
    {
        _spriteCache.Clear();
        var theme = _host.MascotTheme;
        _bubble.Background = BrushFrom(theme.Cream);
        _bubble.BorderBrush = BrushFrom(theme.Ink);
        _bubbleTail.Fill = BrushFrom(theme.Cream);
        _bubbleTail.Stroke = BrushFrom(theme.Ink);
        if (_bubble.Child is FrameworkElement content) RestyleBubble(content);
        SetPose(_pose, instant: true);
        UpdateOutfit();
    }

    private static Image NewSpriteLayer(double opacity)
    {
        var image = new Image
        {
            Width = DesktopMascotLogic.SpriteSize, Height = DesktopMascotLogic.SpriteSize,
            Stretch = Stretch.Uniform, Opacity = opacity
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    /// <summary>Cross-fades to the new pose so changes read as motion instead of a cut.</summary>
    private void SetPose(DesktopMascotPose pose, bool instant = false)
    {
        var previous = _pose;
        _pose = pose;
        var source = LoadSprite(_host.MascotTheme.Kind, pose);
        if (ReferenceEquals(_spriteFront.Source, source)) return;
        _frameQueue.Clear();
        // With frame animation, pass through the in-between drawing (e.g. arm half raised).
        if (!instant && MotionAllowed && Feature(DesktopMascotFeature.FrameAnimation) &&
            InBetweenFrame(previous, pose) is ImageSource middle)
        {
            ShowSource(middle, 150);
            _frameQueue.Enqueue((source, 190, 0));
            StartFrameQueue(150);
            return;
        }
        ShowSource(source, instant ? 0 : pose == DesktopMascotPose.Sleep ? 700 : 340);
    }

    /// <summary>Displays an image, cross-fading from the current one over <paramref name="blendMs"/>.</summary>
    private void ShowSource(ImageSource? source, int blendMs)
    {
        if (ReferenceEquals(_spriteFront.Source, source)) return;
        if (blendMs <= 0 || !MotionAllowed || _spriteFront.Source is null)
        {
            _spriteFront.BeginAnimation(OpacityProperty, null);
            _spriteBack.BeginAnimation(OpacityProperty, null);
            _spriteFront.Source = source;
            _spriteFront.Opacity = 1;
            _spriteBack.Opacity = 0;
            return;
        }
        (_spriteFront, _spriteBack) = (_spriteBack, _spriteFront);
        _spriteFront.Source = source;
        // Keep the outfit layer on top of both sprite layers.
        _actor.Children.Remove(_spriteFront);
        _actor.Children.Insert(_actor.Children.IndexOf(_outfit), _spriteFront);
        var blend = TimeSpan.FromMilliseconds(blendMs);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        _spriteFront.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, blend) { EasingFunction = ease });
        _spriteBack.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, blend) { EasingFunction = ease, BeginTime = blend / 3 });
    }

    private ImageSource? LoadSprite(FruitThemeKind theme, DesktopMascotPose pose)
    {
        var path = DesktopMascotLogic.SpritePath(theme, pose);
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;
        var source = TryLoad(path)
                     ?? (pose != DesktopMascotPose.Idle ? LoadSprite(theme, DesktopMascotPose.Idle) : null)
                     ?? (theme != FruitThemeKind.Avocado ? LoadSprite(FruitThemeKind.Avocado, pose) : null)
                     ?? TryLoad(theme == FruitThemeKind.Cat ? "Assets/cat-scene.png"
                         : $"Assets/theme-scenes/{theme.ToString().ToLowerInvariant()}-scene.png");
        _spriteCache[path] = source;
        return source;
    }

    /// <summary>Loads an optional extra frame (blink, tail, half poses); null when it does not exist.</summary>
    private ImageSource? LoadFrame(FruitThemeKind theme, string frame)
    {
        var path = DesktopMascotLogic.FramePath(theme, frame);
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;
        var source = TryLoad(path);
        _spriteCache[path] = source;
        return source;
    }

    private static ImageSource? TryLoad(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Avocado;component/{path}");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 320;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (IOException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ---------- Idle behaviour ----------
    // Designed to feel hand-animated: the body is always gently alive (breathing, bob, sway),
    // poses blend into each other, and tricks use anticipation -> action -> settle easing.

    private bool MotionAllowed => SystemParameters.ClientAreaAnimation || Feature(DesktopMascotFeature.AlwaysAnimate);

    private void ScheduleIdle()
    {
        _idleTimer.Stop();
        _idleTimer.Interval = DesktopMascotLogic.NextIdleDelay(_random);
        _idleTimer.Start();
    }

    private void PerformIdleAction()
    {
        ScheduleIdle();
        if (_walking || _isDragging || _sleeping || _bubble.Visibility == Visibility.Visible || !IsVisible) return;
        if (TryFeatureIdleAction()) return;
        var action = DesktopMascotLogic.PickIdleAction(_random, _lastAction);
        _lastAction = action;
        if (!MotionAllowed && action is not DesktopMascotIdleAction.Wave and not DesktopMascotIdleAction.Cheer)
            action = DesktopMascotIdleAction.Wave;
        switch (action)
        {
            case DesktopMascotIdleAction.Bounce: Hop(1); break;
            case DesktopMascotIdleAction.Sway: Wiggle(); break;
            case DesktopMascotIdleAction.Wave: WaveHello(); break;
            case DesktopMascotIdleAction.Cheer: Dance(); break;
            case DesktopMascotIdleAction.Love: HugHeart(); break;
            case DesktopMascotIdleAction.LookAround: LookAround(); break;
            case DesktopMascotIdleAction.Stretch: StretchUp(); break;
        }
    }

    /// <summary>Blends to <paramref name="pose"/>, holds it, then blends back to rest.</summary>
    private void HoldPose(DesktopMascotPose pose, int milliseconds)
    {
        SetPose(pose);
        _poseTimer.Stop();
        _poseTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        _poseTimer.Start();
    }

    private void ReturnToRest()
    {
        if (_isDragging) return;
        if (_bubble.Visibility == Visibility.Visible && _bubbleIsReminder) { SetPose(DesktopMascotPose.Wave); return; }
        SetPose(_sleeping ? DesktopMascotPose.Sleep : DesktopMascotPose.Idle);
    }

    /// <summary>Continuous "alive" loop: breathing, a soft bob and a slow sway with offset periods.</summary>
    private void StartBreathing()
    {
        _breath.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _breath.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _bob.BeginAnimation(TranslateTransform.YProperty, null);
        _sway.BeginAnimation(RotateTransform.AngleProperty, null);
        if (!MotionAllowed) return;

        var breathe = TimeSpan.FromSeconds(_sleeping ? 2.7 : 1.7);
        _breath.BeginAnimation(ScaleTransform.ScaleYProperty, Loop((TimeSpan.Zero, 1), (breathe, _sleeping ? 1.025 : 1.018), (breathe * 2, 1)));
        // Keep volume: get a hair thinner while inhaling.
        _breath.BeginAnimation(ScaleTransform.ScaleXProperty, Loop((TimeSpan.Zero, 1), (breathe, 0.994), (breathe * 2, 1)));
        if (_sleeping)
        {
            _sway.BeginAnimation(RotateTransform.AngleProperty,
                Loop((TimeSpan.Zero, 0), (TimeSpan.FromSeconds(3), 1.2), (TimeSpan.FromSeconds(6), 0)));
            return;
        }
        var bob = TimeSpan.FromSeconds(1.7);
        _bob.BeginAnimation(TranslateTransform.YProperty, Loop((TimeSpan.Zero, 0), (bob, -2.2), (bob * 2, 0)));
        var sway = TimeSpan.FromSeconds(2.4);
        _sway.BeginAnimation(RotateTransform.AngleProperty,
            Loop((TimeSpan.Zero, 0), (sway, 1.4), (sway * 2, 0), (sway * 3, -1.4), (sway * 4, 0)));
    }

    private static DoubleAnimationUsingKeyFrames Loop(params (TimeSpan At, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        foreach (var (at, value) in frames)
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(at),
                new SineEase { EasingMode = EasingMode.EaseInOut }));
        return animation;
    }

    /// <summary>A soft happy bounce: crouch (anticipation), rise, land with a squash, settle.</summary>
    private void Hop(int hops)
    {
        if (!MotionAllowed) { HoldPose(DesktopMascotPose.Play, 900); return; }
        const int length = 900;
        var lift = new List<(int, double)>();
        var stretchY = new List<(int, double)>();
        var stretchX = new List<(int, double)>();
        for (var i = 0; i < hops; i++)
        {
            var t = i * length;
            lift.AddRange([(t, 0), (t + 200, 2), (t + 470, -16), (t + 720, 0), (t + length, 0)]);
            stretchY.AddRange([(t, 1), (t + 200, 0.93), (t + 380, 1.04), (t + 720, 0.95), (t + length, 1)]);
            stretchX.AddRange([(t, 1), (t + 200, 1.05), (t + 380, 0.97), (t + 720, 1.04), (t + length, 1)]);
        }
        _bounce.BeginAnimation(TranslateTransform.YProperty, Keyframes([.. lift]));
        _squash.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes([.. stretchY]));
        _squash.BeginAnimation(ScaleTransform.ScaleXProperty, Keyframes([.. stretchX]));
    }

    /// <summary>A playful lean to one side and back, like shifting weight.</summary>
    private void Wiggle()
    {
        if (!MotionAllowed) return;
        var side = _random.Next(2) == 0 ? -1 : 1;
        _tilt.BeginAnimation(RotateTransform.AngleProperty,
            Keyframes((0, 0), (450, 4 * side), (900, -1.5 * side), (1300, 0.6 * side), (1700, 0)));
    }

    private void WaveHello()
    {
        HoldPose(DesktopMascotPose.Wave, 2600);
        if (!MotionAllowed) return;
        _tilt.BeginAnimation(RotateTransform.AngleProperty,
            Keyframes((0, 0), (500, 2.5), (1000, -1.5), (1500, 2.5), (2000, -1), (2600, 0)));
    }

    /// <summary>Arms-up cheer with two small hops and floating notes.</summary>
    private void Dance()
    {
        HoldPose(DesktopMascotPose.Play, 2400);
        if (!MotionAllowed) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        timer.Tick += (_, _) => { timer.Stop(); Hop(2); };
        timer.Start();
        AddSparkles("♪", 3);
    }

    /// <summary>Holds a softly glowing heart in front of the chest, like the reference clip.</summary>
    private void HugHeart()
    {
        if (!MotionAllowed) { WaveHello(); return; }
        var heart = new Path
        {
            Data = Geometry.Parse("M 12,21 C 12,21 2,14.5 2,8 A 5,5 0 0 1 12,5.5 A 5,5 0 0 1 22,8 C 22,14.5 12,21 12,21 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0xF6, 0xB8, 0xF0)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xE6, 0xFB)),
            StrokeThickness = 1, Width = 34, Height = 30, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 4, 58), Opacity = 0, RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 22, ShadowDepth = 0, Color = Color.FromRgb(0xFF, 0x7C, 0xE8), Opacity = 0.95 }
        };
        var pulse = new ScaleTransform(0.4, 0.4);
        heart.RenderTransform = pulse;
        _effects.Children.Add(heart);
        var scale = Keyframes((0, 0.4), (450, 1.05), (650, 1), (1050, 1.1), (1450, 1), (1850, 1.1), (2250, 1), (2800, 0.9), (3200, 0.6));
        pulse.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        pulse.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        var fade = Keyframes((0, 0), (450, 1), (2700, 1), (3200, 0));
        fade.Completed += (_, _) => _effects.Children.Remove(heart);
        heart.BeginAnimation(OpacityProperty, fade);
        // Snuggle the heart with a slow happy rock.
        _tilt.BeginAnimation(RotateTransform.AngleProperty,
            Keyframes((0, 0), (800, -2.5), (1600, 2.5), (2400, -1.5), (3200, 0)));
        _squash.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes((0, 1), (450, 0.97), (900, 1.01), (3200, 1)));
    }

    /// <summary>Slowly leans to look one way, pauses, looks the other way, then settles.</summary>
    private void LookAround()
    {
        _tilt.BeginAnimation(RotateTransform.AngleProperty,
            Keyframes((0, 0), (700, -4), (1500, -4), (2300, 4), (3100, 4), (3800, 0)));
        _bounce.BeginAnimation(TranslateTransform.XProperty,
            Keyframes((0, 0), (700, -3), (1500, -3), (2300, 3), (3100, 3), (3800, 0)));
    }

    private void StretchUp()
    {
        HoldPose(DesktopMascotPose.Play, 1700);
        _squash.BeginAnimation(ScaleTransform.ScaleYProperty,
            Keyframes((0, 1), (250, 0.96), (850, 1.07), (1300, 1.07), (1600, 0.98), (1900, 1)));
        _squash.BeginAnimation(ScaleTransform.ScaleXProperty,
            Keyframes((0, 1), (250, 1.03), (850, 0.96), (1300, 0.96), (1600, 1.02), (1900, 1)));
    }

    private void AddSparkles(string glyph, int count)
    {
        if (!MotionAllowed) return;
        var ink = BrushFrom(_host.MascotTheme.Accent);
        for (var i = 0; i < count; i++)
        {
            var note = new TextBlock
            {
                Text = glyph, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = ink,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(i % 2 == 0 ? -130 + i * 24 : 130 - i * 24, 34, 0, 0), Opacity = 0
            };
            var drift = new TranslateTransform();
            note.RenderTransform = drift;
            _effects.Children.Add(note);
            var begin = TimeSpan.FromMilliseconds(i * 380);
            drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, -26, TimeSpan.FromMilliseconds(1600))
            { BeginTime = begin, EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
            var fade = Keyframes((0, 0), (400, 1), (1100, 1), (1600, 0));
            fade.BeginTime = begin;
            fade.Completed += (_, _) => _effects.Children.Remove(note);
            note.BeginAnimation(OpacityProperty, fade);
        }
    }

    private static DoubleAnimationUsingKeyFrames Keyframes(params (int Milliseconds, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        foreach (var (ms, value) in frames)
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)),
                new SineEase { EasingMode = EasingMode.EaseInOut }));
        return animation;
    }

    // ---------- Reminders & nags ----------

    /// <summary>Periodic check: sleep during quiet hours, nag about old tasks.</summary>
    private void Tick()
    {
        var quiet = _host.IsMascotQuiet;
        if (quiet != _sleeping)
        {
            _sleeping = quiet;
            if (!(_bubble.Visibility == Visibility.Visible && _bubbleIsReminder)) SetPose(quiet ? DesktopMascotPose.Sleep : DesktopMascotPose.Idle);
            StartBreathing();
            UpdateSleepBadge();
        }
        var now = DateTime.Now;
        if (quiet || now < _nextNagAt || _bubble.Visibility == Visibility.Visible) return;
        _nextNagAt = now + DesktopMascotLogic.NagInterval;
        var nag = DesktopMascotLogic.FindNag(_host.OpenTasks, now, _lastNagged);
        if (nag is null) return;
        _lastNagged[nag.Task.Id] = now;
        ShowBubble(nag.Title, nag.Summary, [nag.Task], reminder: false);
    }

    public void ShowReminder(IReadOnlyList<TodoItem> tasks)
    {
        if (tasks.Count == 0) return;
        foreach (var task in tasks) _lastNagged[task.Id] = DateTime.Now;
        ShowBubble(DesktopMascotLogic.ReminderTitle(tasks.Count), DesktopMascotLogic.ReminderSummary(tasks), tasks, reminder: true);
        Hop(1);
        Sound(DesktopMascotSound.Hello);
        BeginEscalation(tasks);
    }

    /// <summary>Called by the host when an alert is resolved elsewhere (e.g. in the main window).</summary>
    public void DismissReminder()
    {
        if (_bubbleIsReminder) HideBubble();
    }

    private void Greet()
    {
        if (_sleeping) return;
        var count = _host.OpenTasks.Count(task => !task.IsCompleted);
        ShowBubble("Hi there!", count == 0 ? "All clear — no open tasks. Let's play!" :
            count == 1 ? "You have 1 open task. I'll keep an eye on it!" :
            $"You have {count} open tasks. I'll keep an eye on them!", [], reminder: false, seconds: 5);
        HoldPose(DesktopMascotPose.Wave, 2400);
    }

    private void Poke()
    {
        if (_sleeping)
        {
            _sleeping = false;
            SetPose(DesktopMascotPose.Idle);
            StartBreathing();
            UpdateSleepBadge();
            ShowBubble("*yawn*", "Oh! I was napping… What's up?", [], reminder: false, seconds: 4);
            Hop(1);
            return;
        }
        if (_bubble.Visibility == Visibility.Visible) return;
        Sound(DesktopMascotSound.Poke);
        ShowBubble(string.Empty, Feature(DesktopMascotFeature.Mood) && _random.Next(3) == 0
            ? DesktopMascotFeatures.MoodLine(_mood) : DesktopMascotLogic.PickPokeLine(_random), [], reminder: false, seconds: 3);
        Hop(1);
    }

    private void ShowBubble(string title, string body, IReadOnlyList<TodoItem> tasks, bool reminder, int seconds = 0)
    {
        _bubbleIsReminder = reminder;
        var theme = _host.MascotTheme;
        var ink = BrushFrom(theme.Ink);
        var panel = new StackPanel { MaxWidth = WindowWidth - 44 };

        var header = new DockPanel { LastChildFill = true };
        var close = new Button
        {
            Content = "✕", Width = 22, Height = 22, Padding = new Thickness(0), FontSize = 11,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = ink, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Dismiss"
        };
        close.Click += (_, _) => HideBubble();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var bodyText = new TextBlock
        {
            Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = ink,
            FontFamily = new FontFamily("Segoe UI"), MaxHeight = 120, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (title.Length > 0)
        {
            header.Children.Add(new TextBlock
            {
                Text = title, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = ink, TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 0, 6, 4)
            });
            panel.Children.Add(header);
            panel.Children.Add(bodyText);
        }
        else
        {
            // No title: the message shares the first row with the close button instead of sliding under it.
            bodyText.Margin = new Thickness(0, 0, 6, 0);
            header.Children.Add(bodyText);
            panel.Children.Add(header);
        }

        if (tasks.Count > 0)
        {
            var actions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(BubbleButton(tasks.Count == 1 ? "Done ✓" : "All done ✓", theme, primary: true, () =>
            {
                foreach (var task in tasks.ToList()) _host.CompleteMascotTask(task);
                HideBubble();
                Celebrate();
            }));
            var snooze = reminder ? SnoozeMinutes() : 60;
            actions.Children.Add(BubbleButton(reminder ? $"Snooze {snooze}m" : "Later", theme, primary: false, () =>
            {
                if (reminder) RememberSnooze(snooze);
                foreach (var task in tasks.ToList()) _host.SnoozeMascotTask(task, snooze);
                HideBubble();
            }));
            if (reminder && Feature(DesktopMascotFeature.SmartReminders))
                foreach (var alternative in new[] { 5, 10, 30, 60 }.Where(minutes => minutes != snooze).Take(2))
                    actions.Children.Add(BubbleButton($"{alternative}m", theme, primary: false, () =>
                    {
                        RememberSnooze(alternative);
                        foreach (var task in tasks.ToList()) _host.SnoozeMascotTask(task, alternative);
                        HideBubble();
                    }));
            actions.Children.Add(BubbleButton("Open", theme, primary: false, () => { HideBubble(); _host.ShowMainFromMascot(); }));
            panel.Children.Add(actions);
        }

        PresentBubble(panel, sticky: reminder, seconds, large: false);
        if (reminder || tasks.Count > 0) SetPose(DesktopMascotPose.Wave);
    }

    /// <summary>Shows any content in the speech bubble; large bubbles grow the window upward.</summary>
    private void PresentBubble(UIElement content, bool sticky, int seconds, bool large)
    {
        StopWalk(returnHome: true);
        _digestClock.Stop();
        SetLarge(large);
        _bubble.Child = content;
        _bubble.Visibility = Visibility.Visible;
        _bubbleTail.Visibility = Visibility.Visible;
        if (MotionAllowed)
        {
            var pop = new ScaleTransform(0.9, 0.9);
            var rise = new TranslateTransform(0, 8);
            var group = new TransformGroup();
            group.Children.Add(pop);
            group.Children.Add(rise);
            _bubble.RenderTransform = group;
            var duration = TimeSpan.FromMilliseconds(320);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var grow = new DoubleAnimation(0.9, 1, duration) { EasingFunction = ease };
            pop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            pop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
            var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            _bubble.BeginAnimation(OpacityProperty, fadeIn);
            _bubbleTail.BeginAnimation(OpacityProperty, fadeIn);
        }
        _bubbleTimer.Stop();
        // Reminders and the digest stay until the user reacts; everything else fades away.
        if (!sticky)
        {
            _bubbleTimer.Interval = seconds > 0 ? TimeSpan.FromSeconds(seconds) : DesktopMascotLogic.BubbleDuration;
            _bubbleTimer.Start();
        }
    }

    // ---------- Big bubble sizing ----------

    /// <summary>Resizes the window around the mascot's feet so the mascot never jumps.</summary>
    private void SetLarge(bool large)
    {
        if (large == _isLarge) return;
        BeginAnimation(LeftProperty, null);
        var footX = Left + Width / 2;
        var footY = Top + Height;
        _isLarge = large;
        Width = large ? LargeWidth : WindowWidth;
        Height = large ? LargeHeight : WindowHeight;
        _bubble.MaxWidth = Width - 12;
        var (x, y) = DesktopMascotLogic.ClampToArea(footX - Width / 2, footY - Height, Width, Height,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = x;
        Top = y;
    }

    /// <summary>Position of the compact window, which is what gets persisted.</summary>
    private (double Left, double Top) CompactPosition() =>
        (Left + (Width - WindowWidth) / 2, Top + Height - WindowHeight);

    // ---------- "What's up!" digest ----------

    public void ShowDigest()
    {
        if (_sleeping)
        {
            _sleeping = false;
            StartBreathing();
            UpdateSleepBadge();
        }
        var digest = DesktopMascotLogic.BuildDigest(_host.DigestTasks, DateTime.Now);
        var content = BuildDigestContent(digest);
        if (_isLarge && _bubble.Visibility == Visibility.Visible && _digestClock.IsEnabled)
        {
            // Refresh in place (e.g. after ticking a task off) without replaying the pop-in.
            _bubble.Child = content;
            return;
        }
        PresentBubble(content, sticky: true, seconds: 0, large: true);
        SetPose(DesktopMascotPose.Wave);
        Hop(1);
        StartDigestClock();
    }

    private UIElement BuildDigestContent(DesktopMascotDigest digest)
    {
        var theme = _host.MascotTheme;
        var ink = BrushFrom(theme.Ink);
        var muted = BrushFrom(theme.MutedInk);
        var root = new DockPanel { Width = LargeWidth - 44, LastChildFill = true };

        // Header: greeting, live clock and date.
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(header, Dock.Top);
        var close = new Button
        {
            Content = "✕", Width = 24, Height = 24, Padding = new Thickness(0), FontSize = 12,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = ink, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Close"
        };
        close.Click += (_, _) => HideBubble();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = DesktopMascotLogic.DigestGreeting(DateTime.Now), FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = muted, FontFamily = new FontFamily("Segoe UI")
        });
        _digestClockText = new TextBlock
        {
            Text = digest.Time, FontSize = 34, FontWeight = FontWeights.Bold, Foreground = ink,
            FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, -2, 0, -4)
        };
        titleStack.Children.Add(_digestClockText);
        _digestDateText = new TextBlock
        {
            Text = digest.Date, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = ink,
            FontFamily = new FontFamily("Segoe UI")
        };
        titleStack.Children.Add(_digestDateText);
        if (CurrentWeather is DesktopMascotWeatherReading weather)
            titleStack.Children.Add(new TextBlock
            {
                Text = DesktopMascotWeather.Summary(weather), FontSize = 13, Foreground = muted,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"), Margin = new Thickness(0, 2, 0, 0)
            });
        header.Children.Add(titleStack);
        root.Children.Add(header);

        // Footer actions.
        var footer = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(BubbleButton("Open Avocado", theme, primary: true, () => { HideBubble(); _host.ShowMainFromMascot(); }));
        footer.Children.Add(BubbleButton("Got it!", theme, primary: false, HideBubble));
        if (Feature(DesktopMascotFeature.QuickAdd))
            footer.Children.Add(BubbleButton("+ Add task", theme, primary: false, ShowQuickAdd));
        root.Children.Add(footer);

        var sections = new StackPanel();
        AddDigestSection(sections, "☀  Today", digest.Today, "Nothing scheduled — enjoy! 🎉");
        AddDigestSection(sections, "⏳  Old tasks", digest.Old, "No old tasks. Nice and tidy ✨");
        AddDigestSection(sections, "🌙  Tomorrow", digest.Tomorrow, "Tomorrow is wide open.");
        root.Children.Add(new ScrollViewer
        {
            Content = sections, MaxHeight = LargeHeight - _spriteSize - 230,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        return root;
    }

    private void AddDigestSection(System.Windows.Controls.Panel parent, string title, IReadOnlyList<DesktopMascotDigestEntry> entries, string emptyText)
    {
        var theme = _host.MascotTheme;
        var ink = BrushFrom(theme.Ink);
        var muted = BrushFrom(theme.MutedInk);
        var heading = new Border
        {
            Background = BrushFrom(theme.Task), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 8, 0, 4),
            Child = new TextBlock
            {
                Text = $"{title}  ({entries.Count})", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = ink, FontFamily = new FontFamily("Segoe UI")
            }
        };
        parent.Children.Add(heading);
        if (entries.Count == 0)
        {
            parent.Children.Add(new TextBlock
            {
                Text = emptyText, FontSize = 12.5, FontStyle = FontStyles.Italic, Foreground = muted,
                Margin = new Thickness(8, 0, 0, 2), FontFamily = new FontFamily("Segoe UI")
            });
            return;
        }
        foreach (var entry in entries)
        {
            var row = new DockPanel { Margin = new Thickness(2, 1, 0, 1) };
            var done = new Button
            {
                Content = "✓", Width = 22, Height = 22, Padding = new Thickness(0), FontSize = 11, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 1, 6, 0), VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand,
                Background = BrushFrom(theme.Cream), Foreground = ink, BorderBrush = ink, BorderThickness = new Thickness(1.5),
                ToolTip = "Mark done", Tag = false, Template = RoundedButtonTemplate()
            };
            var task = entry.Task;
            done.Click += (_, _) =>
            {
                _host.CompleteMascotTask(task);
                AddSparkles("★", 2);
                ShowDigest();
            };
            DockPanel.SetDock(done, Dock.Left);
            row.Children.Add(done);
            var when = new TextBlock
            {
                Text = entry.When, FontSize = 11.5, Margin = new Thickness(6, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top,
                Foreground = entry.IsLate ? BrushFrom(theme.Seed) : muted, FontWeight = entry.IsLate ? FontWeights.SemiBold : FontWeights.Normal,
                FontFamily = new FontFamily("Segoe UI")
            };
            DockPanel.SetDock(when, Dock.Right);
            row.Children.Add(when);
            var priority = TaskReminderLogic.PriorityPrefix(task.Priority);
            row.Children.Add(new TextBlock
            {
                Text = (priority.Length > 0 ? priority + " " : string.Empty) + task.Text,
                FontSize = 13.5, Foreground = ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
                FontFamily = new FontFamily("Segoe UI")
            });
            parent.Children.Add(row);
        }
    }

    private void StartDigestClock()
    {
        _digestClock.Stop();
        _digestClock.Start();
    }

    private void StopDigestClock()
    {
        _digestClock.Stop();
        _digestClockText = null;
        _digestDateText = null;
    }

    private void UpdateDigestClock()
    {
        if (_digestClockText is null) { _digestClock.Stop(); return; }
        var now = DateTime.Now;
        _digestClockText.Text = now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (_digestDateText is not null)
            _digestDateText.Text = now.ToString("dddd, MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private Button BubbleButton(string text, FruitThemePalette theme, bool primary, Action onClick)
    {
        var button = new Button
        {
            Content = text, Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(10, 3, 10, 4),
            FontSize = 12, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
            Background = BrushFrom(primary ? theme.Button : theme.Cream),
            Foreground = BrushFrom(primary ? theme.Cream : theme.Ink),
            BorderBrush = BrushFrom(primary ? theme.ButtonBorder : theme.Ink), BorderThickness = new Thickness(1.5),
            Tag = primary
        };
        button.Template = RoundedButtonTemplate();
        button.Click += (_, _) => onClick();
        return button;
    }

    private void RestyleBubble(FrameworkElement content)
    {
        var theme = _host.MascotTheme;
        foreach (var text in FindChildren<TextBlock>(content)) text.Foreground = BrushFrom(theme.Ink);
        foreach (var button in FindChildren<Button>(content))
        {
            if (button.Tag is not bool primary) { button.Foreground = BrushFrom(theme.Ink); continue; }
            button.Background = BrushFrom(primary ? theme.Button : theme.Cream);
            button.Foreground = BrushFrom(primary ? theme.Cream : theme.Ink);
            button.BorderBrush = BrushFrom(primary ? theme.ButtonBorder : theme.Ink);
        }
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var nested in FindChildren<T>(child)) yield return nested;
        }
    }

    private static ControlTemplate RoundedButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private void HideBubble()
    {
        _bubbleTimer.Stop();
        _bubbleIsReminder = false;
        EndEscalation();
        if (_quickAddOpen) EndQuickAdd();
        var child = _bubble.Child;
        void Collapse()
        {
            // A newer bubble may have replaced this one while it was fading out.
            if (!ReferenceEquals(_bubble.Child, child)) return;
            _bubble.Visibility = Visibility.Collapsed;
            _bubbleTail.Visibility = Visibility.Collapsed;
            _bubble.Child = null;
            StopDigestClock();
            SetLarge(false);
            UpdateSleepBadge();
        }
        ReturnToRest();
        if (!MotionAllowed || _bubble.Visibility != Visibility.Visible) { Collapse(); return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new SineEase() };
        fade.Completed += (_, _) => Collapse();
        _bubble.BeginAnimation(OpacityProperty, fade);
        _bubbleTail.BeginAnimation(OpacityProperty, fade);
    }

    private void Celebrate()
    {
        HoldPose(DesktopMascotPose.Play, 2200);
        Sound(DesktopMascotSound.Cheer);
        Hop(2);
        AddSparkles("★", 3);
        ShowBubble("Yay!", "Task done — great job! 🎉", [], reminder: false, seconds: 3);
    }

    private void UpdateSleepBadge()
    {
        foreach (var z in _effects.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "zzz")).ToList())
        {
            z.BeginAnimation(OpacityProperty, null);
            _effects.Children.Remove(z);
        }
        if (!_sleeping || _bubble.Visibility == Visibility.Visible) return;
        var badge = new TextBlock
        {
            Text = "z Z z", Tag = "zzz", FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = BrushFrom(_host.MascotTheme.Ink), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(110, 0, 0, 0)
        };
        _effects.Children.Add(badge);
        if (MotionAllowed)
            badge.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromSeconds(1.6))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    // ---------- Input ----------

    private void Sprite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { _host.ShowMainFromMascot(); e.Handled = true; return; }
        StopWalk(returnHome: false);
        var startLeft = Left;
        var startTop = Top;
        BeginAnimation(LeftProperty, null);
        _isDragging = true;
        var pose = _pose;
        try { DragMove(); }
        catch (InvalidOperationException) { }
        _isDragging = false;
        var moved = Math.Abs(Left - startLeft) + Math.Abs(Top - startTop) > 3;
        if (moved)
        {
            var (x, y) = DesktopMascotLogic.ClampToArea(Left, Top, Width, Height,
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            Left = x;
            Top = y;
            var (compactLeft, compactTop) = CompactPosition();
            _host.SaveMascotPosition(compactLeft, compactTop);
            // Soft landing after being carried.
            _squash.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes((0, 1), (140, 0.94), (380, 1.02), (600, 1)));
            _squash.BeginAnimation(ScaleTransform.ScaleXProperty, Keyframes((0, 1), (140, 1.04), (380, 0.99), (600, 1)));
        }
        else
        {
            SetPose(pose);
            Poke();
        }
        e.Handled = true;
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuEntry("What's up!", ShowDigest));
        if (Feature(DesktopMascotFeature.QuickAdd)) menu.Items.Add(MenuEntry("Quick add…", ShowQuickAdd));
        if (Feature(DesktopMascotFeature.DayWrapUp)) menu.Items.Add(MenuEntry("Day wrap-up", ShowWrapUp));
        menu.Items.Add(MenuEntry("Open Avocado", () => _host.ShowMainFromMascot()));
        menu.Items.Add(MenuEntry("Play!", Dance));
        if (Feature(DesktopMascotFeature.WeatherOutfits)) menu.Items.Add(MenuEntry("Weather", ShowWeather));
        if (Feature(DesktopMascotFeature.TaskbarWalks) && !_sleeping)
            menu.Items.Add(_walking ? MenuEntry("Stop walking", () => StopWalk(returnHome: true)) : MenuEntry("Go for a little walk", () => StartWalk(manual: true)));
        menu.Items.Add(MenuEntry(_sleeping ? "Wake up" : "Take a nap", () =>
        {
            StopWalk(returnHome: true);
            _sleeping = !_sleeping;
            SetPose(_sleeping ? DesktopMascotPose.Sleep : DesktopMascotPose.Idle);
            StartBreathing();
            UpdateSleepBadge();
        }));
        menu.Items.Add(MenuEntry("Remind me about old tasks", () =>
        {
            _lastNagged.Clear();
            var nag = DesktopMascotLogic.FindNag(_host.OpenTasks, DateTime.Now);
            if (nag is null) ShowBubble("Nothing old!", "No overdue or stale tasks. Nice work!", [], reminder: false, seconds: 4);
            else { _lastNagged[nag.Task.Id] = DateTime.Now; ShowBubble(nag.Title, nag.Summary, [nag.Task], reminder: false); }
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuEntry("Hide mascot", () => _host.HideMascotFromMascot()));
        menu.PlacementTarget = _spriteHost;
        menu.IsOpen = true;
    }

    private static MenuItem MenuEntry(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Dispatcher.Invoke(StartBreathing);
    }

    // ---------- Lifetime ----------

    public void CloseMascot()
    {
        _closing = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 on the mascot should just hide it through the host, keeping settings in sync.
        if (!_closing) { e.Cancel = true; _host.HideMascotFromMascot(); return; }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _idleTimer.Stop();
        _poseTimer.Stop();
        _bubbleTimer.Stop();
        _nagTimer.Stop();
        _digestClock.Stop();
        StopFeatures();
        base.OnClosed(e);
    }

    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, (style | WsExToolWindow | WsExNoActivate) & ~WsExAppWindow);
    }

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
