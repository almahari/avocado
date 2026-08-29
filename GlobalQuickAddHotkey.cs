using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Avocado;

public sealed class GlobalQuickAddHotkey : IDisposable
{
    private const int QuickAddHotkeyId = 0xA70C;
    private const int ClipboardTaskHotkeyId = 0xA70D;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyN = 0x4E;
    private const uint VirtualKeyV = 0x56;
    private readonly IntPtr _handle;
    private readonly HwndSource? _source;
    private readonly Action _quickAddCallback;
    private readonly Action _clipboardTaskCallback;
    private bool _quickAddRegistered;
    private bool _clipboardTaskRegistered;

    public GlobalQuickAddHotkey(Window window, Action quickAddCallback, Action clipboardTaskCallback)
    {
        _quickAddCallback = quickAddCallback;
        _clipboardTaskCallback = clipboardTaskCallback;
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowMessageHook);
        var modifiers = ModAlt | ModControl | ModNoRepeat;
        _quickAddRegistered = RegisterHotKey(_handle, QuickAddHotkeyId, modifiers, VirtualKeyN);
        _clipboardTaskRegistered = RegisterHotKey(_handle, ClipboardTaskHotkeyId, modifiers, VirtualKeyV);
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
        _quickAddRegistered = false;
        _clipboardTaskRegistered = false;
        _source?.RemoveHook(WindowMessageHook);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
