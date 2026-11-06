using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace Avocado;

/// <summary>Little walks: now and then the mascot takes a few steps to one side, pauses, and walks back to its spot.</summary>
public sealed partial class DesktopMascotWindow
{
    private enum WalkLeg { Out, Pause, Back }

    private readonly DispatcherTimer _walkTimer = new();
    private TimeSpan? _walkLastFrame;
    private double _walkHomeLeft;
    private double _walkTurnLeft;
    private double _walkPhase;
    private TimeSpan _walkPauseLeft;
    private WalkLeg _walkLeg;
    private bool _walking;

    private void ApplyWalkSetting()
    {
        if (Feature(DesktopMascotFeature.TaskbarWalks))
        {
            if (!_walkTimer.IsEnabled) ScheduleWalk();
        }
        else
        {
            _walkTimer.Stop();
            StopWalk(returnHome: true);
        }
    }

    private void ScheduleWalk()
    {
        _walkTimer.Stop();
        if (!Feature(DesktopMascotFeature.TaskbarWalks)) return;
        _walkTimer.Interval = DesktopMascotWalk.NextWalkDelay(_random);
        _walkTimer.Start();
    }

    private void WalkTimerTick()
    {
        ScheduleWalk();
        if (!MotionAllowed || _host.IsMascotQuiet || _bubble.Visibility == Visibility.Visible || _quickAddOpen) return;
        StartWalk(manual: false);
    }

    private void StartWalk(bool manual)
    {
        if (_walking || _isDragging || _sleeping || _isLarge || !IsVisible) return;

        // Freeze any running slide (e.g. a window dodge) at its current spot.
        var left = Left;
        BeginAnimation(LeftProperty, null);
        Left = left;

        var homeX = Left + Width / 2;
        var turnX = DesktopMascotWalk.PlanStroll(WalkAreas(), homeX, _spriteSize / 2, _random);
        if (turnX is null)
        {
            if (manual) ShowBubble(string.Empty, "Hmm, there's no room to stroll here.", [], reminder: false, seconds: 3);
            return;
        }

        _bounce.BeginAnimation(TranslateTransform.YProperty, null);
        _tilt.BeginAnimation(RotateTransform.AngleProperty, null);
        _walkHomeLeft = Left;
        _walkTurnLeft = turnX.Value - Width / 2;
        _walkLeg = WalkLeg.Out;
        _walkLastFrame = null;
        _walkPhase = 0;
        _walking = true;
        if (_pose != DesktopMascotPose.Idle) SetPose(DesktopMascotPose.Idle);
        CompositionTarget.Rendering += WalkFrame;
    }

    private void WalkFrame(object? sender, EventArgs e)
    {
        if (!_walking) return;
        var time = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.FromTicks(DateTime.Now.Ticks);
        if (_walkLastFrame is not TimeSpan last || time <= last) { _walkLastFrame = time; return; }
        var dt = Math.Min(0.05, (time - last).TotalSeconds);
        _walkLastFrame = time;

        if (_walkLeg == WalkLeg.Pause)
        {
            // Stand still for a moment at the turnaround point, settling the gait.
            _bounce.Y *= 0.8;
            _tilt.Angle *= 0.8;
            _walkPauseLeft -= TimeSpan.FromSeconds(dt);
            if (_walkPauseLeft <= TimeSpan.Zero) { _walkLeg = WalkLeg.Back; _walkPhase = 0; }
            return;
        }

        var target = _walkLeg == WalkLeg.Out ? _walkTurnLeft : _walkHomeLeft;
        var remaining = target - Left;
        var step = DesktopMascotWalk.Speed * dt;
        if (Math.Abs(remaining) <= step)
        {
            Left = target;
            if (_walkLeg == WalkLeg.Out)
            {
                _walkLeg = WalkLeg.Pause;
                _walkPauseLeft = DesktopMascotWalk.PauseAtTurn(_random);
            }
            else
            {
                StopWalk(returnHome: false);
            }
            return;
        }
        var direction = Math.Sign(remaining);
        Left += direction * step;
        _walkPhase += dt * 2 * Math.PI * DesktopMascotWalk.StepsPerSecond;
        var (bob, rock) = DesktopMascotWalk.Gait(_walkPhase);
        _bounce.Y = bob;
        _tilt.Angle = rock + direction * 2;
    }

    /// <summary>Ends a walk. With <paramref name="returnHome"/> the mascot glides back to where the walk started.</summary>
    private void StopWalk(bool returnHome)
    {
        if (!_walking) return;
        _walking = false;
        CompositionTarget.Rendering -= WalkFrame;
        var ease = new SineEase { EasingMode = EasingMode.EaseOut };
        _bounce.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        _tilt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        if (!returnHome || Math.Abs(Left - _walkHomeLeft) < 0.5) return;
        // The real position snaps home at once (so bubbles and saving use it); the glide is only visual.
        var from = Left;
        Left = _walkHomeLeft;
        BeginAnimation(LeftProperty, new DoubleAnimation(from, _walkHomeLeft, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    /// <summary>Every screen's work area converted to WPF units.</summary>
    private List<DesktopMascotWalkArea> WalkAreas()
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice;
        var dpi = VisualTreeHelper.GetDpi(this);
        Point Convert(double x, double y) => transform is Matrix matrix
            ? matrix.Transform(new Point(x, y))
            : new Point(x / dpi.DpiScaleX, y / dpi.DpiScaleY);

        return Forms.Screen.AllScreens.Select(screen =>
        {
            var area = screen.WorkingArea;
            var topLeft = Convert(area.Left, area.Top);
            var bottomRight = Convert(area.Right, area.Bottom);
            return new DesktopMascotWalkArea(topLeft.X, bottomRight.X, topLeft.Y, bottomRight.Y);
        }).OrderBy(area => area.Left).ToList();
    }
}
