using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace SqlDeployerGui;

// Borderless, centered splash shown while the app starts up, then handed off
// to MainWindow by App. WinUI 3 (unpackaged) has no built-in splash screen.
public sealed partial class SplashWindow : Window
{
    // Size in DPI-independent (effective) pixels; scaled to physical below.
    private const int WidthDip = 480;
    private const int HeightDip = 400;

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    public SplashWindow()
    {
        InitializeComponent();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        // Frameless: no border, no title bar, not resizable.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        // Remove the Windows 11 white window border and round the corners.
        int none = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref none, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        // Size to physical pixels for the current monitor's scale so the content
        // isn't clipped on high-DPI displays (e.g. 125%/150%).
        double scale = GetDpiForWindow(hwnd) / 96.0;
        int w = (int)Math.Round(WidthDip * scale);
        int h = (int)Math.Round(HeightDip * scale);
        appWindow.Resize(new SizeInt32(w, h));

        // Center on the primary work area.
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        appWindow.Move(new PointInt32(
            area.WorkArea.X + (area.WorkArea.Width - w) / 2,
            area.WorkArea.Y + (area.WorkArea.Height - h) / 2));

        LogoImage.Source = new BitmapImage(
            new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png")));

        Root.Loaded += (_, _) =>
        {
            if (Root.Resources.TryGetValue("FadeIn", out var resource) && resource is Storyboard fadeIn)
                fadeIn.Begin();
        };
    }
}
