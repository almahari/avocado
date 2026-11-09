using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace Avocado;

/// <summary>Optional mascot behaviours. Every feature checks its own tray toggle.</summary>
public sealed partial class DesktopMascotWindow
{
    private readonly DispatcherTimer _featureTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _screenTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _frameTimer = new();
    private readonly DispatcherTimer _frameLoopTimer = new();
    private readonly DispatcherTimer _friendTimer = new();
    private readonly DispatcherTimer _escalationTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _moodTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _weatherTimer = new() { Interval = DesktopMascotWeather.RefreshEvery };
    private DesktopMascotWeatherReading? _weather;
    private string? _lastWeatherMood;
    private bool _weatherFetching;
    private bool? _lastMotionAllowed;
    private readonly Queue<(ImageSource? Source, int BlendMs, int HoldMs)> _frameQueue = new();
    private readonly HashSet<Guid> _headsUpWarned = [];
    private readonly List<(DateTime At, double X)> _petSamples = [];
    private DesktopMascotMood _mood = DesktopMascotMood.Content;
    private DesktopMascotNudge _nextNudge = DesktopMascotNudge.Stretch;
    private TimeSpan _activeTime;
    private DateTime _lastLateNudge = DateTime.MinValue;
    private DateTime _lastYawn = DateTime.MinValue;
    private DateTime _lastPet = DateTime.MinValue;
    private DateTime _lastDodge = DateTime.MinValue;
    private DateTime _escalationStart;
    private int _escalationLevel;
    private DateTime _lastTap = DateTime.MinValue;
    private bool _quickAddOpen;
    private bool _dropHover;
    private IntPtr _lastForeground;
    private RECT _lastForegroundRect;

    private bool Feature(DesktopMascotFeature feature) => _host.MascotSettings.IsEnabled(feature);

    private void Sound(DesktopMascotSound sound)
    {
        if (Feature(DesktopMascotFeature.SoundEffects)) DesktopMascotSounds.Play(sound, _host.MascotSoundMode);
    }

    private bool HasStickyBubble => _bubble.Visibility == Visibility.Visible && (_bubbleIsReminder || _isLarge || _quickAddOpen || !_bubbleTimer.IsEnabled);

    // ---------- Lifetime ----------

    private void StartFeatures()
    {
        _featureTimer.Tick += (_, _) => FeatureTick();
        _screenTimer.Tick += (_, _) => ScreenTick();
        _frameTimer.Tick += (_, _) => NextQueuedFrame();
        _frameLoopTimer.Tick += (_, _) => FrameLoopTick();
        _friendTimer.Tick += (_, _) => FriendTick();
        _escalationTimer.Tick += (_, _) => EscalationTick();
        _moodTimer.Tick += (_, _) => { if (_mood == DesktopMascotMood.Worried && !_sleeping) SweatDrop(); };
        _weatherTimer.Tick += (_, _) => RefreshWeather(announce: false);
        _walkTimer.Tick += (_, _) => WalkTimerTick();
        _featureTimer.Start();
        ScheduleFriend(first: true);
        ApplySettings();
        // First check shortly after start so the morning briefing doesn't collide with the greeting.
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        first.Tick += (_, _) => { first.Stop(); FeatureTick(); };
        first.Start();
    }

    private void StopFeatures()
    {
        StopWalk(returnHome: false);
        foreach (var timer in new[] { _featureTimer, _screenTimer, _frameTimer, _frameLoopTimer, _friendTimer, _escalationTimer, _moodTimer, _weatherTimer, _walkTimer })
            timer.Stop();
    }

    /// <summary>Re-reads the settings after a tray toggle.</summary>
    public void ApplySettings()
    {
        var size = DesktopMascotFeatures.SpriteSize(_host.MascotSettings.Size);
        if (Math.Abs(size - _spriteSize) > 0.5) ApplySize(size);
        if (_lastMotionAllowed != MotionAllowed)
        {
            _lastMotionAllowed = MotionAllowed;
            StartBreathing();
        }
        UpdateOutfit();

        if (Feature(DesktopMascotFeature.ScreenAwareness)) _screenTimer.Start();
        else
        {
            _screenTimer.Stop();
            _look.Angle = 0;
            _lookShift.X = 0;
        }

        if (Feature(DesktopMascotFeature.FrameAnimation)) ScheduleFrameLoop();
        else
        {
            _frameLoopTimer.Stop();
            if (_pose == DesktopMascotPose.Idle && _frameQueue.Count == 0) ShowSource(LoadSprite(_host.MascotTheme.Kind, _pose), 200);
        }

        if (!Feature(DesktopMascotFeature.Mood)) { _mood = DesktopMascotMood.Content; _moodTimer.Stop(); }
        if (!Feature(DesktopMascotFeature.SmartReminders)) EndEscalation();
        if (!Feature(DesktopMascotFeature.FriendVisits)) _friendTimer.Stop();
        else if (!_friendTimer.IsEnabled) ScheduleFriend(first: false);
        ApplySeeThrough(hovered: _spriteHost.IsMouseOver);
        UpdateMood();
        ApplyWeatherSetting();
        ApplyWalkSetting();
    }

    private void ApplySize(double size)
    {
        var footX = Left + Width / 2;
        var footY = Top + Height;
        _spriteSize = size;
        foreach (var layer in new[] { _spriteFront, _spriteBack }) layer.Width = layer.Height = size;
        _effects.Height = size + 30;
        _bubble.Margin = new Thickness(6, 0, 6, size + 18);
        _bubbleTail.Margin = new Thickness(0, 0, 30, size + 4);
        Width = _isLarge ? LargeWidth : WindowWidth;
        Height = _isLarge ? LargeHeight : WindowHeight;
        _bubble.MaxWidth = Width - 12;
        var (x, y) = DesktopMascotLogic.ClampToArea(footX - Width / 2, footY - Height, Width, Height,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = x;
        Top = y;
        var (compactLeft, compactTop) = CompactPosition();
        _host.SaveMascotPosition(compactLeft, compactTop);
    }

    /// <summary>Runs every 20 s: briefing, wrap-up, heads-up, mood, health and late-night nudges.</summary>
    private void FeatureTick()
    {
        if (!IsVisible) return;
        var now = DateTime.Now;
        var settings = _host.MascotSettings;
        var today = DateOnly.FromDateTime(now);
        var idle = UserIdleTime();
        var active = idle < TimeSpan.FromMinutes(2);
        UpdateMood();

        // #15 break & health nudges: count active minutes, reset after a real break.
        if (idle > TimeSpan.FromMinutes(5)) _activeTime = TimeSpan.Zero;
        else if (active) _activeTime += _featureTimer.Interval;

        if (_host.IsMascotQuiet || _isDragging) return;

        // #14 heads-up a few minutes before something is due.
        if (Feature(DesktopMascotFeature.MeetingHeadsUp) && !HasStickyBubble)
        {
            var soon = DesktopMascotFeatures.UpcomingSoon(_host.OpenTasks, now, _headsUpWarned);
            if (soon.Count > 0)
            {
                foreach (var task in soon) _headsUpWarned.Add(task.Id);
                ShowHeadsUp(soon, now);
                return;
            }
        }

        if (!active || HasStickyBubble) return;

        // #2 morning briefing (first activity of the day) and evening wrap-up.
        if (Feature(DesktopMascotFeature.MorningBriefing) && settings.LastBriefingDate != today && now.Hour >= 5 && now.Hour < settings.WrapUpHour)
        {
            settings.LastBriefingDate = today;
            _host.SaveMascotSettings();
            ShowDigest();
            return;
        }
        if (Feature(DesktopMascotFeature.DayWrapUp) && settings.LastWrapUpDate != today && now.Hour >= settings.WrapUpHour)
        {
            settings.LastWrapUpDate = today;
            _host.SaveMascotSettings();
            ShowWrapUp();
            return;
        }

        if (Feature(DesktopMascotFeature.HealthNudges) && _activeTime >= DesktopMascotFeatures.NudgeEvery && _bubble.Visibility != Visibility.Visible)
        {
            _activeTime = TimeSpan.Zero;
            ShowNudge(_nextNudge);
            _nextNudge = (DesktopMascotNudge)(((int)_nextNudge + 1) % Enum.GetValues<DesktopMascotNudge>().Length);
            return;
        }

        // #9 late-night nudge.
        if (Feature(DesktopMascotFeature.TimeOfDay) && DesktopMascotFeatures.IsTooLate(now) &&
            now - _lastLateNudge >= TimeSpan.FromHours(1) && _bubble.Visibility != Visibility.Visible)
        {
            _lastLateNudge = now;
            Yawn();
            ShowBubble("It's late 🌙", $"It's {now:HH:mm}. Your tasks will still be here tomorrow — time to get some sleep 💤",
                [], reminder: false, seconds: 10);
        }
    }

    /// <summary>Gives time-of-day and mood their own idle tricks. Returns true when it handled the idle slot.</summary>
    private bool TryFeatureIdleAction()
    {
        if (Feature(DesktopMascotFeature.TimeOfDay) && _random.Next(100) < 30)
        {
            switch (DesktopMascotFeatures.TimeOfDayProp(DateTime.Now))
            {
                case "yawn" when DateTime.Now - _lastYawn >= TimeSpan.FromHours(1):
                    _lastYawn = DateTime.Now;
                    Yawn();
                    return true;
            }
        }
        if (Feature(DesktopMascotFeature.Mood) && _random.Next(100) < 25)
        {
            if (_mood == DesktopMascotMood.Happy) { FloatGlyphs("♥", 3, PinkBrush); Wiggle(); return true; }
            if (_mood == DesktopMascotMood.Worried) { SweatDrop(); LookAround(); return true; }
        }
        return false;
    }

    // ---------- #2 Wrap-up ----------

    private void ShowWrapUp()
    {
        var now = DateTime.Now;
        var digest = DesktopMascotLogic.BuildDigest(_host.DigestTasks, now);
        var movable = digest.Today.Where(entry => entry.Task.Recurrence == TaskRecurrence.None && entry.When != "anytime")
            .Select(entry => entry.Task).ToList();
        var finished = _host.CompletedTodayCount;
        var theme = _host.MascotTheme;
        var ink = BrushFrom(theme.Ink);

        var root = new StackPanel { Width = LargeWidth - 44 };
        root.Children.Add(Text("Day wrap-up 🌇", 22, FontWeights.Bold, ink));
        root.Children.Add(Text(finished switch
        {
            0 => "No tasks finished today — tomorrow is a fresh start!",
            1 => "You finished 1 task today. Nice! 🎉",
            _ => $"You finished {finished} tasks today. Amazing! 🎉"
        }, 15, FontWeights.SemiBold, ink, new Thickness(0, 4, 0, 6)));
        if (movable.Count > 0)
        {
            root.Children.Add(Text("Still open from today:", 13, FontWeights.SemiBold, BrushFrom(theme.MutedInk)));
            foreach (var task in movable.Take(8))
                root.Children.Add(Text($"• {task.Text}  ({task.ReminderLabel})", 13.5, FontWeights.Normal, ink, new Thickness(6, 1, 0, 1)));
            if (movable.Count > 8)
                root.Children.Add(Text($"+{movable.Count - 8} more", 12, FontWeights.Normal, BrushFrom(theme.MutedInk), new Thickness(6, 0, 0, 0)));
        }
        else root.Children.Add(Text("Nothing left over from today ✨", 13.5, FontWeights.Normal, ink));

        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (movable.Count > 0)
            actions.Children.Add(BubbleButton($"Move {movable.Count} to tomorrow", theme, primary: true, () =>
            {
                foreach (var task in movable) _host.MoveMascotTaskToTomorrow(task);
                HideBubble();
                Sound(DesktopMascotSound.Pop);
                ShowBubble("Moved! 🌙", $"{movable.Count} task(s) moved to tomorrow. Rest well!", [], reminder: false, seconds: 5);
            }));
        actions.Children.Add(BubbleButton("What's up!", theme, primary: movable.Count == 0, ShowDigest));
        actions.Children.Add(BubbleButton("Close", theme, primary: false, HideBubble));
        root.Children.Add(actions);

        PresentBubble(root, sticky: true, seconds: 0, large: true);
        if (finished > 0) { HoldPose(DesktopMascotPose.Play, 2200); FloatGlyphs("★", 3, BrushFrom(theme.Accent)); Sound(DesktopMascotSound.Cheer); }
        else SetPose(DesktopMascotPose.Wave);
    }

    // ---------- #3 Drop to add & #4 quick add ----------

    private void AttachInteraction()
    {
        _spriteHost.AllowDrop = true;
        _spriteHost.DragEnter += OnDragEnter;
        _spriteHost.DragOver += (_, e) => { e.Effects = CanAcceptDrop(e) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _spriteHost.DragLeave += (_, _) => { _dropHover = false; ReturnToRest(); };
        _spriteHost.Drop += OnDrop;
        _spriteHost.MouseMove += OnPetMove;
        _spriteHost.MouseEnter += (_, _) => ApplySeeThrough(hovered: true);
        _spriteHost.MouseLeave += (_, _) => ApplySeeThrough(hovered: false);
    }

    private bool CanAcceptDrop(DragEventArgs e) =>
        Feature(DesktopMascotFeature.DropToAdd) &&
        (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.FileDrop));

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = CanAcceptDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (e.Effects == DragDropEffects.None || _dropHover) return;
        _dropHover = true;
        SetPose(DesktopMascotPose.Play);
        Hop(1);
        if (!HasStickyBubble) ShowBubble(string.Empty, "Ooh! Feed me a task 😋", [], reminder: false, seconds: 3);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _dropHover = false;
        e.Handled = true;
        if (!CanAcceptDrop(e)) return;
        var text = e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        var input = DesktopMascotFeatures.TaskFromDrop(text, files);
        if (input is null) { ReturnToRest(); return; }
        AddTaskWithFlair(DesktopMascotFeatures.NormalizeQuickAdd(input, DateTime.Now));
    }

    private bool AddTaskWithFlair(string input)
    {
        var added = _host.AddMascotTasks(input);
        if (added == 0)
        {
            ShowBubble("Hmm 🤔", "I couldn't make a task out of that.", [], reminder: false, seconds: 3);
            return false;
        }
        EatAnimation();
        var parsed = TaskReminderLogic.Parse(input);
        ShowBubble("Yum! Task added ✓", added == 1 ? parsed.Text : $"{added} tasks added.", [], reminder: false, seconds: 4);
        return true;
    }

    private void ShowQuickAdd()
    {
        var theme = _host.MascotTheme;
        var ink = BrushFrom(theme.Ink);
        var root = new StackPanel { Width = WindowWidth - 44 };
        root.Children.Add(Text("Quick add ✏️", 15, FontWeights.Bold, ink));
        var box = new TextBox
        {
            FontSize = 14, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 6, 0, 4),
            Background = Brushes.White, Foreground = ink, BorderBrush = ink, BorderThickness = new Thickness(1.5)
        };
        root.Children.Add(box);
        root.Children.Add(Text("e.g. “remind me in 20 min to stretch”,\n“tomorrow 9am !! call Sam”", 11.5, FontWeights.Normal,
            BrushFrom(theme.MutedInk)));
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Submit()
        {
            if (string.IsNullOrWhiteSpace(box.Text)) return;
            var input = DesktopMascotFeatures.NormalizeQuickAdd(box.Text, DateTime.Now);
            EndQuickAdd();
            if (!AddTaskWithFlair(input)) ShowQuickAdd();
        }
        actions.Children.Add(BubbleButton("Add", theme, primary: true, Submit));
        actions.Children.Add(BubbleButton("Cancel", theme, primary: false, HideBubble));
        root.Children.Add(actions);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Submit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { HideBubble(); e.Handled = true; }
        };

        PresentBubble(root, sticky: true, seconds: 0, large: false);
        _quickAddOpen = true;
        SetActivatable(true);
        Activate();
        Dispatcher.BeginInvoke(() => { box.Focus(); Keyboard.Focus(box); }, DispatcherPriority.Input);
        SetPose(DesktopMascotPose.Wave);
    }

    private void EndQuickAdd()
    {
        _quickAddOpen = false;
        SetActivatable(false);
    }

    private void SetActivatable(bool activatable)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, activatable ? style & ~WsExNoActivate : style | WsExNoActivate);
    }

    // ---------- #6 Mood ----------

    private void UpdateMood()
    {
        if (!Feature(DesktopMascotFeature.Mood))
        {
            _spriteHost.ToolTip = "Drag me anywhere • double-click to open Avocado • right-click for options";
            return;
        }
        var digest = DesktopMascotLogic.BuildDigest(_host.DigestTasks, DateTime.Now);
        var overdue = digest.Old.Count(entry => entry.IsLate) + digest.Today.Count(entry => entry.IsLate);
        var stale = digest.Old.Count(entry => !entry.IsLate);
        var mood = DesktopMascotFeatures.GetMood(overdue, stale, _host.CompletedTodayCount);
        _spriteHost.ToolTip = $"{DesktopMascotFeatures.MoodLine(mood)}\nDrag me anywhere • double-click to open Avocado • right-click for options";
        if (mood == _mood) return;
        var improved = mood < _mood;
        _mood = mood;
        if (mood == DesktopMascotMood.Worried) { _moodTimer.Start(); SweatDrop(); }
        else _moodTimer.Stop();
        if (improved && mood == DesktopMascotMood.Happy && !_sleeping) FloatGlyphs("♥", 3, PinkBrush);
    }

    private void SweatDrop()
    {
        if (!MotionAllowed) return;
        var drop = new Path
        {
            Data = Geometry.Parse("M 5,0 C 5,0 0,7 0,10 A 5,5 0 0 0 10,10 C 10,7 5,0 5,0 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x8E, 0xD1, 0xFF)), Stroke = BrushFrom(_host.MascotTheme.Ink),
            StrokeThickness = 1.2, Width = 10, Height = 15, Stretch = Stretch.Fill, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(_spriteSize * 0.52, 0, 0, _spriteSize * 0.78)
        };
        var slide = new TranslateTransform();
        drop.RenderTransform = slide;
        _effects.Children.Add(drop);
        slide.BeginAnimation(TranslateTransform.YProperty, Keyframes((0, 0), (1400, 14)));
        var fade = Keyframes((0, 0), (250, 1), (1000, 1), (1400, 0));
        fade.Completed += (_, _) => _effects.Children.Remove(drop);
        drop.BeginAnimation(OpacityProperty, fade);
    }

    // ---------- #8 Feed & pet ----------

    /// <summary>Called by the host whenever any task is completed.</summary>
    public void OnTaskCompleted()
    {
        UpdateMood();
        if (!Feature(DesktopMascotFeature.FeedAndPet) || !IsVisible) return;
        FeedTreat();
        if (_bubble.Visibility != Visibility.Visible)
            ShowBubble(string.Empty, $"Nom nom! Thanks for finishing a task {DesktopMascotFeatures.TreatFor(_host.MascotTheme.Kind)}", [], reminder: false, seconds: 3);
    }

    private void FeedTreat()
    {
        var theme = _host.MascotTheme;
        if (!MotionAllowed) { EatAnimation(); return; }
        var treat = new Grid
        {
            Width = 22, Height = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, _spriteSize * 0.55), RenderTransformOrigin = new Point(0.5, 0.5)
        };
        treat.Children.Add(new Ellipse { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Bottom, Fill = BrushFrom(theme.Flesh), Stroke = BrushFrom(theme.Ink), StrokeThickness = 1.5 });
        treat.Children.Add(new Ellipse { Width = 8, Height = 5, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 3, 0), Fill = BrushFrom(theme.Accent), Stroke = BrushFrom(theme.Ink), StrokeThickness = 1 });
        var move = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(move);
        treat.RenderTransform = group;
        _effects.Children.Add(treat);
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-90, 0, TimeSpan.FromMilliseconds(650)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(650)) { EasingFunction = new SineEase() });
        var shrink = Keyframes((0, 1), (600, 1), (760, 0.2));
        shrink.Completed += (_, _) => { _effects.Children.Remove(treat); EatAnimation(); };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes((0, 1), (600, 1), (760, 0.2)));
    }

    private void EatAnimation()
    {
        Sound(DesktopMascotSound.Nom);
        HoldPose(DesktopMascotPose.Play, 1100);
        if (!MotionAllowed) return;
        // Two happy chomps.
        _squash.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes((0, 1), (120, 0.94), (240, 1.03), (380, 0.94), (500, 1.02), (700, 1)));
        _squash.BeginAnimation(ScaleTransform.ScaleXProperty, Keyframes((0, 1), (120, 1.04), (240, 0.98), (380, 1.04), (500, 0.99), (700, 1)));
        FloatGlyphs("♥", 1, PinkBrush);
    }

    private void OnPetMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!Feature(DesktopMascotFeature.FeedAndPet) || _isDragging || e.LeftButton == MouseButtonState.Pressed) return;
        var now = DateTime.Now;
        _petSamples.Add((now, e.GetPosition(this).X));
        if (_petSamples.Count > 40) _petSamples.RemoveRange(0, _petSamples.Count - 40);
        if (now - _lastPet < TimeSpan.FromSeconds(4) || !DesktopMascotFeatures.IsPetting(_petSamples, now)) return;
        _lastPet = now;
        _petSamples.Clear();
        if (_sleeping) return;
        FloatGlyphs("♥", 4, PinkBrush);
        Wiggle();
        HoldPose(DesktopMascotPose.Play, 1400);
        Sound(DesktopMascotSound.Hello);
        if (_bubble.Visibility != Visibility.Visible)
            ShowBubble(string.Empty, _random.Next(2) == 0 ? "Hehe, more pets please ♥" : "Purr… I mean, thank you ♥", [], reminder: false, seconds: 2);
    }

    // ---------- #9 Time of day ----------

    private void Yawn()
    {
        Sound(DesktopMascotSound.Yawn);
        HoldPose(DesktopMascotPose.Sleep, 1500);
        StretchUp();
        if (_bubble.Visibility != Visibility.Visible)
            ShowBubble(string.Empty, "*yaaawn* 🥱", [], reminder: false, seconds: 2);
    }

    // ---------- #10 Seasonal outfits ----------

    private void UpdateOutfit()
    {
        _outfit.Children.Clear();
        _outfit.Width = _outfit.Height = 100;
        _outfit.HorizontalAlignment = HorizontalAlignment.Left;
        _outfit.VerticalAlignment = VerticalAlignment.Top;
        _outfit.RenderTransform = new ScaleTransform(_spriteSize / 100, _spriteSize / 100);
        // Weather (when on and known) wins; otherwise fall back to the seasonal outfit.
        var outfit = CurrentWeather is DesktopMascotWeatherReading weather ? DesktopMascotWeather.OutfitFor(weather) : null;
        if (outfit is null && Feature(DesktopMascotFeature.SeasonalOutfits))
            outfit = DesktopMascotFeatures.OutfitFor(DateTime.Now, _host.MascotSeasonalSkin);
        if (outfit is null) return;
        var ink = BrushFrom(_host.MascotTheme.Ink);
        switch (outfit.Value)
        {
            case DesktopMascotOutfit.SantaHat:
                AddShape("M 31,15 C 36,4 50,-2 62,2 C 70,5 76,10 80,4 L 82,8 C 78,16 72,13 69,15 Z", "#D93A3A", ink);
                AddShape("M 28,12 C 40,9 60,9 72,12 L 72,18 C 60,15 40,15 28,18 Z", "#FFFFFF", ink);
                AddShape("M 77,1 A 4,4 0 1 1 85,1 A 4,4 0 1 1 77,1 Z", "#FFFFFF", ink);
                break;
            case DesktopMascotOutfit.WinterScarf:
                AddScarf(ink);
                break;
            case DesktopMascotOutfit.SnowGear:
                // Knitted beanie with a pom-pom, plus the scarf.
                AddShape("M 30,17 C 31,3 69,3 70,17 Z", "#4C7BD9", ink);
                AddShape("M 28,14 C 40,11 60,11 72,14 L 72,20 C 60,17 40,17 28,20 Z", "#FFFFFF", ink);
                AddShape("M 46,1 A 4,4 0 1 1 54,1 A 4,4 0 1 1 46,1 Z", "#FFFFFF", ink);
                AddShape("M 40,6 L 40,12 M 50,5 L 50,12 M 60,6 L 60,12", null, BrushFrom("#A9C1F2"), 1.2);
                AddScarf(ink);
                break;
            case DesktopMascotOutfit.Umbrella:
                AddUmbrella(ink);
                break;
            case DesktopMascotOutfit.FlowerCrown:
                string[] petals = ["#FF9EC4", "#FFE066", "#FFFFFF", "#C9A7FF", "#FF9EC4"];
                AddShape("M 30,14 C 40,8 60,8 70,14", null, BrushFrom("#5E9E4B"), 2);
                for (var i = 0; i < petals.Length; i++)
                {
                    var x = 33 + i * 8.5;
                    var y = 12 - Math.Sin(i / 4.0 * Math.PI) * 3.5;
                    AddShape($"M {x - 3.4},{y} A 3.4,3.4 0 1 1 {x + 3.4},{y} A 3.4,3.4 0 1 1 {x - 3.4},{y} Z", petals[i], ink, 0.8);
                    AddShape($"M {x - 1},{y} A 1,1 0 1 1 {x + 1},{y} A 1,1 0 1 1 {x - 1},{y} Z", "#F2A93B", null);
                }
                break;
            case DesktopMascotOutfit.Sunglasses:
                AddShape("M 33,26 L 47,26 C 47,33 45,36 40,36 C 35,36 33,33 33,26 Z", "#1E1E24", ink);
                AddShape("M 53,26 L 67,26 C 67,33 65,36 60,36 C 55,36 53,33 53,26 Z", "#1E1E24", ink);
                AddShape("M 47,27 C 49,25.5 51,25.5 53,27", null, ink, 1.6);
                AddShape("M 36,28 L 39,28 M 56,28 L 59,28", null, BrushFrom("#FFFFFF"), 1.2);
                break;
            case DesktopMascotOutfit.PumpkinHat:
                AddShape("M 38,10 C 38,2 62,2 62,10 C 62,16 38,16 38,10 Z", "#F28C28", ink);
                AddShape("M 50,3 C 50,6 50,13 50,15 M 44,4 C 43,8 43,12 44,15 M 56,4 C 57,8 57,12 56,15", null, BrushFrom("#C4661A"), 1);
                AddShape("M 49,3 L 50,-3 L 53,-2 L 51,3 Z", "#4F872D", ink, 0.8);
                break;
        }
    }

    private void AddScarf(Brush ink)
    {
        AddShape("M 31,47 C 42,51 58,51 69,47 L 70,54 C 58,58 42,58 30,54 Z", "#D9534F", ink);
        AddShape("M 56,53 L 64,53 L 66,68 L 57,68 Z", "#D9534F", ink);
        AddShape("M 36,49 L 38,55 M 44,51 L 45,57 M 52,51 L 52,57 M 60,50 L 59,56", null, BrushFrom("#FFFFFF"), 1.2);
    }

    /// <summary>A tilted umbrella held in the right paw, in the theme's colour, with a few raindrops.</summary>
    private void AddUmbrella(Brush ink)
    {
        var theme = _host.MascotTheme;
        var umbrella = new Canvas { IsHitTestVisible = false };
        // Drawn upright around the shaft (0,0 = canopy centre), then tilted over the head.
        umbrella.Children.Add(OutfitPath("M 0,-1 L 0,48 C 0,54 -7,54 -7,49", null, ink, 2.2));
        umbrella.Children.Add(OutfitPath("M -36,0 C -34,-24 34,-24 36,0 C 30,-5 24,-5 18,0 C 12,-5 6,-5 0,0 C -6,-5 -12,-5 -18,0 C -24,-5 -30,-5 -36,0 Z",
            theme.Outer, ink, 1.4));
        umbrella.Children.Add(OutfitPath("M -18,0 C -14,-12 -8,-18 0,-19 M 18,0 C 14,-12 8,-18 0,-19", null, BrushFrom(theme.Cream), 1));
        umbrella.Children.Add(OutfitPath("M -1.6,-19 L 0,-25 L 1.6,-19 Z", theme.Ink, null, 0));
        umbrella.RenderTransform = new TransformGroup
        {
            Children = { new RotateTransform(-22), new TranslateTransform(60, 0) }
        };
        _outfit.Children.Add(umbrella);
        foreach (var (x, y) in new[] { (12.0, 20.0), (5.0, 36.0), (9.0, 52.0), (98.0, -8.0), (104.0, 12.0) })
            AddShape($"M {x},{y - 4} C {x + 2.4},{y - 0.5} {x + 2.4},{y + 2} {x},{y + 2} C {x - 2.4},{y + 2} {x - 2.4},{y - 0.5} {x},{y - 4} Z",
                "#7FB8F0", null, 0);
    }

    // ---------- Weather outfits ----------

    private DesktopMascotWeatherReading? CurrentWeather =>
        Feature(DesktopMascotFeature.WeatherOutfits) && DesktopMascotWeather.IsFresh(_weather, DateTime.Now) ? _weather : null;

    private void ApplyWeatherSetting()
    {
        if (!Feature(DesktopMascotFeature.WeatherOutfits))
        {
            _weatherTimer.Stop();
            _lastWeatherMood = null;
            return;
        }
        if (!_weatherTimer.IsEnabled) _weatherTimer.Start();
        if (CurrentWeather is null) RefreshWeather(announce: false);
    }

    private void ShowWeather()
    {
        if (CurrentWeather is DesktopMascotWeatherReading weather)
        {
            ShowBubble(DesktopMascotWeather.Summary(weather), DesktopMascotWeather.Comment(weather), [], reminder: false, seconds: 6);
            Hop(1);
        }
        else
        {
            ShowBubble(string.Empty, "Let me check the sky…", [], reminder: false, seconds: 3);
            RefreshWeather(announce: true);
        }
    }

    private async void RefreshWeather(bool announce)
    {
        if (_weatherFetching || _closing) return;
        _weatherFetching = true;
        DesktopMascotWeatherReading? reading;
        try
        {
            reading = await DesktopMascotWeatherService.FetchAsync(_host.MascotSettings);
        }
        finally
        {
            _weatherFetching = false;
        }
        if (_closing || !Feature(DesktopMascotFeature.WeatherOutfits)) return;
        if (reading is null)
        {
            if (announce) ShowBubble(string.Empty, "I couldn't reach the weather service. I'll try again later.", [], reminder: false, seconds: 4);
            return;
        }
        _weather = reading;
        _host.SaveMascotSettings();
        UpdateOutfit();

        // Comment when the weather changes enough to swap outfits (or when asked).
        var mood = DesktopMascotWeather.Mood(reading);
        var changed = mood != _lastWeatherMood;
        _lastWeatherMood = mood;
        if (announce || changed && !_host.IsMascotQuiet && !_sleeping && _bubble.Visibility != Visibility.Visible)
        {
            ShowBubble(DesktopMascotWeather.Summary(reading), DesktopMascotWeather.Comment(reading), [], reminder: false, seconds: 6);
            Hop(1);
        }
    }

    private Path OutfitPath(string data, string? fill, Brush? stroke, double thickness) => new()
    {
        Data = Geometry.Parse(data), Fill = fill is null ? null : BrushFrom(fill), Stroke = stroke,
        StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
    };

    private void AddShape(string data, string? fill, Brush? stroke, double thickness = 1.4) =>
        _outfit.Children.Add(new Path
        {
            Data = Geometry.Parse(data), Fill = fill is null ? null : BrushFrom(fill), Stroke = stroke,
            StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });

    // ---------- #11 Cursor watching & window dodging ----------

    private void ScreenTick()
    {
        if (!IsVisible || _isDragging) return;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null || !GetCursorPos(out var cursorPixels)) return;
        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var cursor = fromDevice.Transform(new Point(cursorPixels.X, cursorPixels.Y));
        var centerX = Left + Width / 2;
        var centerY = Top + Height - _spriteSize / 2;
        var dx = cursor.X - centerX;
        var dy = cursor.Y - centerY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var over = Math.Abs(dx) < _spriteSize / 2 && Math.Abs(dy) < _spriteSize / 2;
        var interest = !_sleeping && !over && distance < 650 && MotionAllowed;
        var lean = interest ? Math.Clamp(dx / 220, -1, 1) : 0;
        // Ease toward the target every tick: smooth "head turn" without abrupt jumps.
        _look.Angle += (lean * 3.2 - _look.Angle) * 0.12;
        _lookShift.X += (lean * 3 - _lookShift.X) * 0.12;

        DodgeDraggedWindow(source);
    }

    private void DodgeDraggedWindow(PresentationSource source)
    {
        var foreground = GetForegroundWindow();
        var own = new WindowInteropHelper(this).Handle;
        if (foreground == IntPtr.Zero || foreground == own || !GetWindowRect(foreground, out var rect)) { _lastForeground = IntPtr.Zero; return; }
        var moved = foreground == _lastForeground &&
                    (rect.Left != _lastForegroundRect.Left || rect.Top != _lastForegroundRect.Top) &&
                    rect.Right - rect.Left == _lastForegroundRect.Right - _lastForegroundRect.Left &&
                    rect.Bottom - rect.Top == _lastForegroundRect.Bottom - _lastForegroundRect.Top;
        _lastForeground = foreground;
        _lastForegroundRect = rect;
        if (!moved || _walking || DateTime.Now - _lastDodge < TimeSpan.FromSeconds(2.5) || _isLarge || HasStickyBubble) return;

        var fromDevice = source.CompositionTarget!.TransformFromDevice;
        var topLeft = fromDevice.Transform(new Point(rect.Left, rect.Top));
        var bottomRight = fromDevice.Transform(new Point(rect.Right, rect.Bottom));
        var spriteLeft = Left + (Width - _spriteSize) / 2;
        var spriteTop = Top + Height - _spriteSize;
        var overlaps = topLeft.X < spriteLeft + _spriteSize && bottomRight.X > spriteLeft &&
                       topLeft.Y < spriteTop + _spriteSize && bottomRight.Y > spriteTop;
        if (!overlaps) return;
        _lastDodge = DateTime.Now;

        // Hop out of the way on the side with more room.
        var windowCenter = (topLeft.X + bottomRight.X) / 2;
        var goRight = windowCenter < spriteLeft + _spriteSize / 2;
        var distance = goRight ? bottomRight.X - spriteLeft + 30 : spriteLeft + _spriteSize - topLeft.X + 30;
        var target = Math.Clamp(Left + (goRight ? distance : -distance), SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width);
        if (Math.Abs(target - Left) < 10) return;
        Hop(1);
        Sound(DesktopMascotSound.Pop);
        var slide = new DoubleAnimation(Left, target, TimeSpan.FromMilliseconds(520))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop };
        slide.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = target;
            var (compactLeft, compactTop) = CompactPosition();
            _host.SaveMascotPosition(compactLeft, compactTop);
        };
        BeginAnimation(LeftProperty, slide);
    }

    // ---------- #12 Friend visits ----------

    private void ScheduleFriend(bool first)
    {
        _friendTimer.Stop();
        _friendTimer.Interval = TimeSpan.FromMinutes(first ? _random.Next(12, 26) : _random.Next(20, 41));
        if (Feature(DesktopMascotFeature.FriendVisits)) _friendTimer.Start();
    }

    private void FriendTick()
    {
        ScheduleFriend(first: false);
        if (!IsVisible || _sleeping || _isDragging || _isLarge || _host.IsMascotQuiet || _bubble.Visibility == Visibility.Visible) return;
        FriendVisit();
    }

    private void FriendVisit()
    {
        var others = FruitThemes.All.Where(theme => theme.Kind != _host.MascotTheme.Kind).ToList();
        var friendTheme = others[_random.Next(others.Count)];
        var friend = new Image
        {
            Width = _spriteSize * 0.8, Height = _spriteSize * 0.8, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 6), Source = LoadSprite(friendTheme.Kind, DesktopMascotPose.Idle),
            IsHitTestVisible = false, Opacity = 0
        };
        RenderOptions.SetBitmapScalingMode(friend, BitmapScalingMode.HighQuality);
        var offset = -(_spriteSize * 0.62);
        var slide = new TranslateTransform(offset - 70, 0);
        friend.RenderTransform = slide;
        _root.Children.Insert(_root.Children.IndexOf(_spriteHost), friend);

        var arrive = TimeSpan.FromMilliseconds(900);
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offset - 70, offset, arrive) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        friend.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, arrive));
        var bob = new TranslateTransform();
        var hello = new DispatcherTimer { Interval = arrive };
        hello.Tick += (_, _) =>
        {
            hello.Stop();
            friend.Source = LoadSprite(friendTheme.Kind, DesktopMascotPose.Wave);
            WaveHello();
            Sound(DesktopMascotSound.Hello);
            ShowBubble(string.Empty, $"Look, {friendTheme.DisplayName} came to say hi! 👋", [], reminder: false, seconds: 4);
        };
        hello.Start();
        var leave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.5) };
        leave.Tick += (_, _) =>
        {
            leave.Stop();
            friend.Source = LoadSprite(friendTheme.Kind, DesktopMascotPose.Idle);
            var exit = TimeSpan.FromMilliseconds(900);
            slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offset, offset - 80, exit) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
            var fade = new DoubleAnimation(1, 0, exit);
            fade.Completed += (_, _) => _root.Children.Remove(friend);
            friend.BeginAnimation(OpacityProperty, fade);
        };
        leave.Start();
    }

    // ---------- #13 Smart reminders ----------

    private int SnoozeMinutes() => Feature(DesktopMascotFeature.SmartReminders)
        ? DesktopMascotFeatures.PreferredSnooze(_host.MascotSettings.SnoozeHistory) : 10;

    private void RememberSnooze(int minutes)
    {
        if (!Feature(DesktopMascotFeature.SmartReminders)) return;
        DesktopMascotFeatures.RecordSnooze(_host.MascotSettings.SnoozeHistory, minutes);
        _host.SaveMascotSettings();
    }

    private void BeginEscalation(IReadOnlyList<TodoItem> tasks)
    {
        if (!Feature(DesktopMascotFeature.SmartReminders)) return;
        _escalationStart = DateTime.Now;
        _escalationLevel = 1;
        _escalationTimer.Start();
    }

    private void EndEscalation()
    {
        _escalationTimer.Stop();
        _escalationLevel = 0;
        if (_bubble.Child is FrameworkElement child) child.LayoutTransform = Transform.Identity;
    }

    private void EscalationTick()
    {
        if (!_bubbleIsReminder || _bubble.Visibility != Visibility.Visible) { EndEscalation(); return; }
        var level = DesktopMascotFeatures.EscalationLevel(DateTime.Now - _escalationStart);
        if (level <= _escalationLevel)
        {
            // Keep knocking gently, at most once a minute.
            if (level >= 2 && DateTime.Now - _lastTap >= TimeSpan.FromMinutes(1)) TapOnGlass();
            return;
        }
        _escalationLevel = level;
        if (level == 2) TapOnGlass();
        if (level == 3)
        {
            // Grow into a big, hard-to-miss bubble.
            SetLarge(true);
            if (_bubble.Child is FrameworkElement child) child.LayoutTransform = new ScaleTransform(1.35, 1.35);
            Hop(2);
            Sound(DesktopMascotSound.Hello);
        }
    }

    /// <summary>Leans toward the screen and knocks, as if tapping on the glass.</summary>
    private void TapOnGlass()
    {
        _lastTap = DateTime.Now;
        Sound(DesktopMascotSound.Knock);
        if (!MotionAllowed) return;
        _squash.BeginAnimation(ScaleTransform.ScaleXProperty, Keyframes((0, 1), (180, 1.12), (300, 1.08), (420, 1.12), (540, 1.08), (800, 1)));
        _squash.BeginAnimation(ScaleTransform.ScaleYProperty, Keyframes((0, 1), (180, 1.12), (300, 1.08), (420, 1.12), (540, 1.08), (800, 1)));
        var shake = new TranslateTransform();
        _bubble.RenderTransform = shake;
        shake.BeginAnimation(TranslateTransform.XProperty, Keyframes((0, 0), (60, -5), (120, 5), (180, -4), (240, 4), (300, -2), (360, 0)));
        FloatGlyphs("!", 2, BrushFrom(_host.MascotTheme.Seed));
    }

    // ---------- #14 Heads-up ----------

    private void ShowHeadsUp(IReadOnlyList<TodoItem> soon, DateTime now)
    {
        var lines = soon.Take(3).Select(task =>
        {
            var at = task.DueAt ?? now.Date + task.ReminderTime!.Value;
            var minutes = Math.Max(1, (int)Math.Ceiling((at - now).TotalMinutes));
            return $"• In {minutes} min: {task.Text}";
        });
        ShowBubble("Heads-up! ⏰", string.Join("\n", lines), [], reminder: false, seconds: 20);
        SetPose(DesktopMascotPose.Wave);
        Hop(1);
        Sound(DesktopMascotSound.Hello);
    }

    // ---------- #15 Health nudges ----------

    private void ShowNudge(DesktopMascotNudge nudge)
    {
        var (title, body) = DesktopMascotFeatures.NudgeText(nudge);
        var theme = _host.MascotTheme;
        var root = new StackPanel { Width = WindowWidth - 44 };
        root.Children.Add(Text(title, 15, FontWeights.Bold, BrushFrom(theme.Ink)));
        root.Children.Add(Text(body, 13, FontWeights.Normal, BrushFrom(theme.Ink), new Thickness(0, 2, 0, 0)));
        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(BubbleButton("Done ✓", theme, primary: true, () => { HideBubble(); Celebrate(); }));
        actions.Children.Add(BubbleButton("In 15 min", theme, primary: false, () =>
        {
            _activeTime = DesktopMascotFeatures.NudgeEvery - TimeSpan.FromMinutes(15);
            HideBubble();
        }));
        root.Children.Add(actions);
        PresentBubble(root, sticky: false, seconds: 60, large: false);
        Sound(DesktopMascotSound.Hello);
        switch (nudge)
        {
            case DesktopMascotNudge.Stretch:
                StretchUp();
                var again = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2100) };
                again.Tick += (_, _) => { again.Stop(); StretchUp(); };
                again.Start();
                break;
            case DesktopMascotNudge.EyeRest:
                HoldPose(DesktopMascotPose.Sleep, 3000);
                break;
            default:
                WaveHello();
                break;
        }
    }

    // ---------- #17 Frame animation ----------

    private ImageSource? InBetweenFrame(DesktopMascotPose from, DesktopMascotPose to)
    {
        var frame = (from, to) switch
        {
            (DesktopMascotPose.Idle, DesktopMascotPose.Wave) or (DesktopMascotPose.Wave, DesktopMascotPose.Idle) => "wavehalf",
            (DesktopMascotPose.Idle, DesktopMascotPose.Play) or (DesktopMascotPose.Play, DesktopMascotPose.Idle) => "playhalf",
            _ => null
        };
        return frame is null ? null : LoadFrame(_host.MascotTheme.Kind, frame);
    }

    private void StartFrameQueue(int delayMs)
    {
        _frameTimer.Stop();
        _frameTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, delayMs));
        _frameTimer.Start();
    }

    private void NextQueuedFrame()
    {
        _frameTimer.Stop();
        if (_frameQueue.Count == 0) return;
        var (source, blend, hold) = _frameQueue.Dequeue();
        ShowSource(source, blend);
        if (_frameQueue.Count > 0) StartFrameQueue(blend + hold);
    }

    private void ScheduleFrameLoop()
    {
        _frameLoopTimer.Stop();
        _frameLoopTimer.Interval = TimeSpan.FromMilliseconds(_random.Next(1800, 3600));
        _frameLoopTimer.Start();
    }

    /// <summary>Idle frame-by-frame life: random blinks and a lazy tail swish.</summary>
    private void FrameLoopTick()
    {
        ScheduleFrameLoop();
        if (!Feature(DesktopMascotFeature.FrameAnimation) || !MotionAllowed || _pose != DesktopMascotPose.Idle ||
            _sleeping || _isDragging || _frameQueue.Count > 0 || _frameTimer.IsEnabled) return;
        var theme = _host.MascotTheme.Kind;
        var idle = LoadSprite(theme, DesktopMascotPose.Idle);
        if (_random.Next(100) < 40)
        {
            if (LoadFrame(theme, "blink") is not ImageSource blink) return;
            _frameQueue.Enqueue((blink, 70, 110));
            _frameQueue.Enqueue((idle, 90, 0));
        }
        else
        {
            if (LoadFrame(theme, "tailleft") is not ImageSource left || LoadFrame(theme, "tailright") is not ImageSource right) return;
            _frameQueue.Enqueue((left, 420, 160));
            _frameQueue.Enqueue((idle, 420, 60));
            _frameQueue.Enqueue((right, 420, 160));
            _frameQueue.Enqueue((idle, 420, 0));
        }
        NextQueuedFrame();
    }

    // ---------- #19 See-through ----------

    private void ApplySeeThrough(bool hovered)
    {
        var target = Feature(DesktopMascotFeature.SeeThrough) && !hovered && _bubble.Visibility != Visibility.Visible ? 0.45 : 1.0;
        if (Math.Abs(_spriteHost.Opacity - target) < 0.01 && !_spriteHost.HasAnimatedProperties) return;
        _spriteHost.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)) { EasingFunction = new SineEase() });
    }

    // ---------- Helpers ----------

    private static readonly Brush PinkBrush = CreatePink();

    private static Brush CreatePink()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xF2, 0x6D, 0xB0));
        brush.Freeze();
        return brush;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, Brush brush, Thickness? margin = null) => new()
    {
        Text = text, FontSize = size, FontWeight = weight, Foreground = brush, TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Segoe UI"), Margin = margin ?? new Thickness(0)
    };

    private void FloatGlyphs(string glyph, int count, Brush brush)
    {
        if (!MotionAllowed) return;
        for (var i = 0; i < count; i++)
        {
            var note = new TextBlock
            {
                Text = glyph, FontSize = 18 + _random.Next(6), FontWeight = FontWeights.Bold, Foreground = brush,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(_random.Next(-110, 110), 40, 0, 0), Opacity = 0
            };
            var drift = new TranslateTransform();
            note.RenderTransform = drift;
            _effects.Children.Add(note);
            var begin = TimeSpan.FromMilliseconds(i * 260);
            drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, -34, TimeSpan.FromMilliseconds(1700))
            { BeginTime = begin, EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
            var fade = Keyframes((0, 0), (350, 1), (1200, 1), (1700, 0));
            fade.BeginTime = begin;
            fade.Completed += (_, _) => _effects.Children.Remove(note);
            note.BeginAnimation(OpacityProperty, fade);
        }
    }

    private static TimeSpan UserIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}
