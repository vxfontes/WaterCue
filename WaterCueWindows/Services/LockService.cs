using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WaterCueWindows.Services;

/// <summary>
/// Opens a LockOverlayWindow (Views.LockOverlayWindow) per monitor,
/// installs a low-level keyboard hook to block Alt+F4, Win+D, Alt+Tab during lock.
/// </summary>
public class LockService
{
    public static readonly LockService Shared = new();

    public bool IsLocked { get; private set; }

    private readonly List<Window> _overlayWindows = [];
    private IntPtr _hookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc; // keep reference to prevent GC

    private LockService() { }

    public void Engage()
    {
        if (IsLocked) return;
        IsLocked = true;

        Application.Current.Dispatcher.Invoke(() =>
        {
            bool isPrimary = true;
            foreach (var rect in GetAllMonitorRects())
            {
                var win = CreateOverlayWindow(rect, isPrimary);
                _overlayWindows.Add(win);
                win.Show();
                win.Activate();
                isPrimary = false;
            }
        });

        InstallKeyboardHook();
    }

    public void Release()
    {
        if (!IsLocked) return;
        IsLocked = false;

        UninstallKeyboardHook();

        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var win in _overlayWindows)
                win.Close();
            _overlayWindows.Clear();
        });
    }

    // ----- Window construction -----

    private static Window CreateOverlayWindow(Rect dipRect, bool isPrimary)
    {
        var winType = Type.GetType("WaterCueWindows.Views.LockOverlayWindow, WaterCueWindows");
        var win = winType != null
            ? (Window)Activator.CreateInstance(winType)!
            : new Window { Background = System.Windows.Media.Brushes.Black };

        // Set IsPrimary before Show() so Loaded handler sees the right value
        winType?.GetProperty("IsPrimary")?.SetValue(win, isPrimary);

        win.WindowStyle = WindowStyle.None;
        win.ResizeMode = ResizeMode.NoResize;
        win.Topmost = true;
        win.ShowInTaskbar = false;
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = dipRect.Left;
        win.Top = dipRect.Top;
        win.Width = dipRect.Width;
        win.Height = dipRect.Height;

        win.Loaded += (_, _) => win.WindowState = WindowState.Maximized;
        return win;
    }

    // ----- Monitor enumeration (Win32, no WinForms dependency) -----

    private static IEnumerable<Rect> GetAllMonitorRects()
    {
        var rects = new List<Rect>();

        MonitorEnumDelegate callback = (IntPtr hMon, IntPtr _hdc, ref RECT _rc, IntPtr _data) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMon, ref info))
            {
                var r = info.rcMonitor;
                // Convert physical pixels → WPF DIPs using per-monitor DPI
                GetDpiForMonitor(hMon, MonitorDpiType.Default, out uint dpiX, out uint dpiY);
                double scaleX = dpiX / 96.0;
                double scaleY = dpiY / 96.0;
                rects.Add(new Rect(
                    r.left / scaleX, r.top / scaleY,
                    (r.right - r.left) / scaleX, (r.bottom - r.top) / scaleY));
            }
            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

        // Fallback: single-screen via WPF SystemParameters
        if (rects.Count == 0)
            rects.Add(new Rect(0, 0,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight));

        return rects;
    }

    // ----- Keyboard hook -----

    private void InstallKeyboardHook()
    {
        _hookProc = LowLevelKeyboardCallback;
        using var module = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
            GetModuleHandle(module.ModuleName!), 0);
    }

    private void UninstallKeyboardHook()
    {
        if (_hookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
    }

    private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsLocked)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            if (isDown)
            {
                bool alt = (info.flags & LLKHF_ALTDOWN) != 0;
                bool win = IsWinDown();

                if (alt && info.vkCode == VK_F4)  return Block; // Alt+F4
                if (alt && info.vkCode == VK_TAB) return Block; // Alt+Tab
                if (win && info.vkCode == VK_D)   return Block; // Win+D (show desktop)
                if (info.vkCode is VK_LWIN or VK_RWIN) return Block; // Win key
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsWinDown()
        => (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0
        || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

    private static readonly IntPtr Block = new(1);

    // ----- Win32 constants -----

    private const int WH_KEYBOARD_LL = 13;
    private const nint WM_KEYDOWN    = 0x0100;
    private const nint WM_SYSKEYDOWN = 0x0104;
    private const uint LLKHF_ALTDOWN = 0x20;
    private const uint VK_F4   = 0x73;
    private const uint VK_TAB  = 0x09;
    private const uint VK_D    = 0x44;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;

    private enum MonitorDpiType { Effective = 0, Angular = 1, Raw = 2, Default = 0 }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hMonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(uint vkCode);
}
