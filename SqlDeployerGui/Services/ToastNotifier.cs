using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SqlDeployer.Services;

namespace SqlDeployerGui.Services;

// Surfaces a deploy outcome outside the app: a Windows toast, a flashing taskbar
// icon, and a beep. Skips everything while the window is focused, since the
// in-app result banner already covers that case (the focus rule lives here
// because it needs the window handle). Every step is best-effort — a failed
// notification must never disrupt a deployment.
public sealed class ToastNotifier : IDeployNotifier
{
    private readonly Window _window;

    public ToastNotifier(Window window) => _window = window;

    public void Notify(DeployNotificationKind kind, string title, string message)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

            // If the user is already looking at the app, the InfoBar banner is enough.
            if (GetForegroundWindow() == hwnd) return;

            ShowToast(title, message);
            FlashTaskbar(hwnd);
            MessageBeep(kind == DeployNotificationKind.Failed ? MB_ICONHAND : MB_ICONASTERISK);
        }
        catch
        {
            // Notifications are a convenience; never let one break the deploy flow.
        }
    }

    private static void ShowToast(string title, string message)
    {
        var toast = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification();
        AppNotificationManager.Default.Show(toast);
    }

    private static void FlashTaskbar(IntPtr hwnd)
    {
        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            // Flash the taskbar button until the window is brought to the foreground.
            dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
            uCount = uint.MaxValue,
            dwTimeout = 0
        };
        FlashWindowEx(ref info);
    }

    // --- Win32 interop ---

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;

    private const uint MB_ICONHAND = 0x00000010;      // error tone
    private const uint MB_ICONASTERISK = 0x00000040;  // info tone

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);
}
