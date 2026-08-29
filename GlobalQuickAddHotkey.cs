using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Avocado;

public sealed class GlobalQuickAddHotkey : IDisposable
{
    private const int HotkeyId = 0xA70C;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VirtualKeyN = 0x4E;
    private readonly IntPtr _handle;
    private readonly HwndSource? _source;
    private readonly Action _callback;
    private bool _registered;

    public GlobalQuickAddHotkey(Window window, Action callback)
    {
        _callback = callback;
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowMessageHook);
        _registered = RegisterHotKey(_handle, HotkeyId, ModAlt | ModControl, VirtualKeyN);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey || wParam.ToInt32() != HotkeyId) return IntPtr.Zero;
        _callback();
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_handle, HotkeyId);
        _registered = false;
        _source?.RemoveHook(WindowMessageHook);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
