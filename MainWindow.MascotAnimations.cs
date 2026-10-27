using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brush = System.Windows.Media.Brush;

namespace Avocado;

public partial class MainWindow
{
    private readonly Dictionary<FruitThemeKind, ImageSource> _sleepSceneCache = [];
    private FrameworkElement? _mascotAnimationTarget;
    private FruitThemeKind _mascotAnimationTheme;
    private System.Windows.Controls.Image? _mascotGroundImage;
    private MascotAnimationState _mascotState;
    private bool _mascotMotionAllowed;
    private bool _mascotClosed;

    public bool IsMascotAnimationsEnabled => _state.MascotAnimationsEnabled;
    public MascotAnimationState CurrentMascotAnimationState => _mascotState;

    public void SetMascotAnimations(bool enabled, bool persist = true)
    {
        _state.MascotAnimationsEnabled = enabled;
        UpdateMascotAnimation(force: true);
        if (persist) SaveState();
    }

    private ImageSource GetSleepingMascotScene()
    {
        if (_sleepSceneCache.TryGetValue(_currentTheme.Kind, out var scene)) return scene;
        scene = LoadMascotBitmap($"Assets/mascot-sleep/{_currentTheme.Kind.ToString().ToLowerInvariant()}-sleep.png");
        _sleepSceneCache[_currentTheme.Kind] = scene;
        return scene;
    }

    private FrameworkElement GetMascotAnimationTarget() => IsSideDockLayout
        ? (_currentTheme.Kind == FruitThemeKind.Cat ? DockCatScene : DockThemeScene)
        : (FrameworkElement)FindName(FruitThemes.IsAnimal(_currentTheme.Kind)
            ? $"{_currentTheme.Kind}MascotImage" : $"{_currentTheme.Kind}Shape");

    private void UpdateMascotAnimation(bool force = false)
    {
        if (!IsLoaded || _mascotClosed) return;
        var now = DateTime.Now;
        var state = MascotAnimationLogic.GetState(
            IsMascotAnimationsEnabled && IsVisible && WindowState != WindowState.Minimized && !IsSideDockCollapsed && !_isSleeping,
            DoNotDisturbSettings.IsActive(_state.DoNotDisturb, now), now < _happyUntil);
        var motion = SystemParameters.ClientAreaAnimation;
        var target = GetMascotAnimationTarget();
        if (!force && state == _mascotState && motion == _mascotMotionAllowed && ReferenceEquals(target, _mascotAnimationTarget)) return;
        ApplyMascotAnimation(state, motion, target);
    }

    private void ApplyMascotAnimation(MascotAnimationState state, bool motion, FrameworkElement target)
    {
        StopMascotMotion();
        _mascotState = state;
        _mascotMotionAllowed = motion;
        _mascotAnimationTarget = target;
        _mascotAnimationTheme = _currentTheme.Kind;
        OnPropertyChanged(nameof(CurrentMascotAnimationState));
        RefreshAdaptivePersonality();

        var source = state == MascotAnimationState.Sleeping ? GetSleepingMascotScene() : GetMascotScene(_currentTheme.Kind);
        if (target is System.Windows.Controls.Image image) image.Source = source;
        if (state == MascotAnimationState.Still) return;

        target.RenderTransformOrigin = new System.Windows.Point(0.68, 0.92);
        var scale = new ScaleTransform(1, 1);
        var rotation = new RotateTransform();
        var translation = new TranslateTransform();
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(rotation);
        transforms.Children.Add(translation);
        target.RenderTransform = transforms;

        var effects = IsSideDockLayout ? DockMascotEffects : FloatingMascotEffects;
        effects.Visibility = Visibility.Visible;
        AddMascotEffects(effects, state, motion);
        if (!motion) return;

        // Keep the landscape and feet anchored while the upper mascot moves.
        if (target is System.Windows.Controls.Image movingImage)
        {
            _mascotGroundImage = IsSideDockLayout ? DockMascotGround :
                (System.Windows.Controls.Image)FindName($"{_currentTheme.Kind}MascotGround");
            _mascotGroundImage.Source = source;
            _mascotGroundImage.OpacityMask = MascotRegionMask(upper: false);
            _mascotGroundImage.Visibility = Visibility.Visible;
            movingImage.OpacityMask = MascotRegionMask(upper: true);
        }

        if (state == MascotAnimationState.Sleeping)
        {
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.008, TimeSpan.FromSeconds(2.8))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        else
        {
            translation.BeginAnimation(TranslateTransform.YProperty, MascotKeyframes((0, 0), (180, -10), (420, 0), (620, -5), (900, 0)));
            rotation.BeginAnimation(RotateTransform.AngleProperty, MascotKeyframes((0, 0), (180, -3), (420, 2), (620, -1), (900, 0)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, MascotKeyframes((0, 1), (180, 1.025), (420, 1), (620, 1.015), (900, 1)));
        }
    }

    private static DoubleAnimationUsingKeyFrames MascotKeyframes(params (int Milliseconds, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        foreach (var frame in frames)
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(frame.Value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frame.Milliseconds)),
                new SineEase { EasingMode = EasingMode.EaseInOut }));
        return animation;
    }

    private static LinearGradientBrush MascotRegionMask(bool upper)
    {
        var start = upper ? Colors.White : Colors.Transparent;
        var end = upper ? Colors.Transparent : Colors.White;
        var mask = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1),
            GradientStops = [new(start, 0), new(start, 0.80), new(end, 0.86), new(end, 1)]
        };
        mask.Freeze();
        return mask;
    }

    private void AddMascotEffects(Grid layer, MascotAnimationState state, bool motion)
    {
        var sleeping = state == MascotAnimationState.Sleeping;
        var label = new TextBlock
        {
            Text = sleeping ? "z Z z" : "Task complete!", FontSize = sleeping ? 23 : 14,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Resources["InkBrush"]
        };
        var badge = new Border
        {
            Child = label, CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 5, 12, 5),
            Background = (Brush)Resources["CreamBrush"], HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 30, 44, 0)
        };
        layer.Children.Add(badge);
        if (sleeping)
        {
            if (motion)
            {
                var drift = new TranslateTransform();
                badge.RenderTransform = drift;
                drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6, TimeSpan.FromSeconds(2.8))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            }
            return;
        }
        for (var index = 0; index < 3; index++)
        {
            var star = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 7,0 L 9,5 L 14,7 L 9,9 L 7,14 L 5,9 L 0,7 L 5,5 Z"),
                Fill = (Brush)Resources["AccentBrush"], Width = 14, Height = 14, Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 82 + index % 2 * 22, 28 + index * 50, 0), Opacity = motion ? 0 : 1
            };
            layer.Children.Add(star);
            if (motion) star.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            { BeginTime = TimeSpan.FromMilliseconds(index * 120), AutoReverse = true, RepeatBehavior = new RepeatBehavior(2), FillBehavior = FillBehavior.Stop });
        }
    }

    private void StopMascotMotion()
    {
        if (_mascotAnimationTarget is not null)
        {
            if (_mascotAnimationTarget.RenderTransform is TransformGroup group)
                foreach (var transform in group.Children)
                {
                    if (transform is ScaleTransform scale) { scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); }
                    if (transform is RotateTransform rotation) rotation.BeginAnimation(RotateTransform.AngleProperty, null);
                    if (transform is TranslateTransform translation) translation.BeginAnimation(TranslateTransform.YProperty, null);
                }
            _mascotAnimationTarget.RenderTransform = Transform.Identity;
            _mascotAnimationTarget.OpacityMask = null;
            // Restore the old image before changing theme or layout.
            if (_mascotAnimationTarget is System.Windows.Controls.Image image)
                image.Source = GetMascotScene(_mascotAnimationTheme);
        }
        if (_mascotGroundImage is not null)
        {
            _mascotGroundImage.Source = null;
            _mascotGroundImage.OpacityMask = null;
            _mascotGroundImage.Visibility = Visibility.Collapsed;
            _mascotGroundImage = null;
        }
        foreach (var layer in new[] { DockMascotEffects, FloatingMascotEffects })
        {
            foreach (UIElement child in layer.Children)
            {
                child.BeginAnimation(OpacityProperty, null);
                if (child.RenderTransform is TranslateTransform drift) drift.BeginAnimation(TranslateTransform.YProperty, null);
            }
            layer.Children.Clear();
            layer.Visibility = Visibility.Collapsed;
        }
    }
}
