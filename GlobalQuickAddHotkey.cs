using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Avocado;

public sealed class GlobalQuickAddHotkey : IDisposable
{
    private const int QuickAddHotkeyId = 0xA70C;
    private const int ClipboardTaskHotkeyId = 0xA70D;
    private const int SleepNowHotkeyId = 0xA70E;
    private const int WakeHotkeyId = 0xA70F;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private readonly IntPtr _handle;
    private readonly HwndSource? _source;
    private readonly Action _quickAddCallback;
    private readonly Action _clipboardTaskCallback;
    private readonly Action _sleepNowCallback;
    private readonly Action _wakeCallback;
    private bool _quickAddRegistered;
    private bool _clipboardTaskRegistered;
    private bool _sleepNowRegistered;
    private bool _wakeRegistered;

    public bool QuickAddAvailable => _quickAddGesture.IsDisabled || _quickAddRegistered;
    public bool ClipboardTaskAvailable => _clipboardTaskGesture.IsDisabled || _clipboardTaskRegistered;
    public bool SleepNowAvailable => _sleepNowGesture.IsDisabled || _sleepNowRegistered;
    public bool WakeAvailable => _wakeGesture.IsDisabled || _wakeRegistered;
    public bool AllAvailable => QuickAddAvailable && ClipboardTaskAvailable && SleepNowAvailable && WakeAvailable;
    private readonly GlobalShortcutGesture _quickAddGesture;
    private readonly GlobalShortcutGesture _clipboardTaskGesture;
    private readonly GlobalShortcutGesture _sleepNowGesture;
    private readonly GlobalShortcutGesture _wakeGesture;

    public GlobalQuickAddHotkey(
        Window window,
        Action quickAddCallback,
        Action clipboardTaskCallback,
        Action sleepNowCallback,
        Action wakeCallback,
        GlobalShortcutGesture quickAddGesture,
        GlobalShortcutGesture clipboardTaskGesture,
        GlobalShortcutGesture sleepNowGesture,
        GlobalShortcutGesture wakeGesture)
    {
        _quickAddCallback = quickAddCallback;
        _clipboardTaskCallback = clipboardTaskCallback;
        _sleepNowCallback = sleepNowCallback;
        _wakeCallback = wakeCallback;
        _quickAddGesture = quickAddGesture;
        _clipboardTaskGesture = clipboardTaskGesture;
        _sleepNowGesture = sleepNowGesture;
        _wakeGesture = wakeGesture;
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowMessageHook);
        if (!quickAddGesture.IsDisabled)
            _quickAddRegistered = RegisterHotKey(_handle, QuickAddHotkeyId,
                ToNativeModifiers(quickAddGesture.Modifiers), (uint)quickAddGesture.VirtualKey);
        if (!clipboardTaskGesture.IsDisabled)
            _clipboardTaskRegistered = RegisterHotKey(_handle, ClipboardTaskHotkeyId,
                ToNativeModifiers(clipboardTaskGesture.Modifiers), (uint)clipboardTaskGesture.VirtualKey);
        if (!sleepNowGesture.IsDisabled)
            _sleepNowRegistered = RegisterHotKey(_handle, SleepNowHotkeyId,
                ToNativeModifiers(sleepNowGesture.Modifiers), (uint)sleepNowGesture.VirtualKey);
        if (!wakeGesture.IsDisabled)
            _wakeRegistered = RegisterHotKey(_handle, WakeHotkeyId,
                ToNativeModifiers(wakeGesture.Modifiers), (uint)wakeGesture.VirtualKey);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey) return IntPtr.Zero;
        switch (wParam.ToInt32())
        {
            case QuickAddHotkeyId:
                _quickAddCallback();
                break;
            case ClipboardTaskHotkeyId:
                _clipboardTaskCallback();
                break;
            case SleepNowHotkeyId:
                _sleepNowCallback();
                break;
            case WakeHotkeyId:
                _wakeCallback();
                break;
            default:
                return IntPtr.Zero;
        }
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_quickAddRegistered) UnregisterHotKey(_handle, QuickAddHotkeyId);
        if (_clipboardTaskRegistered) UnregisterHotKey(_handle, ClipboardTaskHotkeyId);
        if (_sleepNowRegistered) UnregisterHotKey(_handle, SleepNowHotkeyId);
        if (_wakeRegistered) UnregisterHotKey(_handle, WakeHotkeyId);
        _quickAddRegistered = false;
        _clipboardTaskRegistered = false;
        _sleepNowRegistered = false;
        _wakeRegistered = false;
        _source?.RemoveHook(WindowMessageHook);
    }

    private static uint ToNativeModifiers(GlobalShortcutModifiers modifiers)
    {
        var native = ModNoRepeat;
        if (modifiers.HasFlag(GlobalShortcutModifiers.Alt)) native |= ModAlt;
        if (modifiers.HasFlag(GlobalShortcutModifiers.Control)) native |= ModControl;
        if (modifiers.HasFlag(GlobalShortcutModifiers.Shift)) native |= 0x0004;
        return native;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
