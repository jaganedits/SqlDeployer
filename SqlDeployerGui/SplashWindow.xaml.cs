using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
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
    // COLORREF 0x00BBGGRR matching the gradient's top corner (#0B0F14) so the
    // Windows 11 window border blends into the splash instead of showing white.
    private const int BorderColor = 0x00140F0B;

    private readonly IntPtr _hwnd;

    // Raised (once) after the logo image has loaded, so the host can begin its
    // heavier UI-thread work without leaving the logo blank.
    public event Action? ContentReady;
    private bool _readyFired;

    public SplashWindow()
    {
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        // Frameless: no title bar, not resizable.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        // Size to physical pixels for the current monitor's scale so the content
        // isn't clipped on high-DPI displays (e.g. 125%/150%).
        double scale = GetDpiForWindow(_hwnd) / 96.0;
        int w = (int)Math.Round(WidthDip * scale);
        int h = (int)Math.Round(HeightDip * scale);
        appWindow.Resize(new SizeInt32(w, h));

        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        appWindow.Move(new PointInt32(
            area.WorkArea.X + (area.WorkArea.Width - w) / 2,
            area.WorkArea.Y + (area.WorkArea.Height - h) / 2));

        // Load the logo, and signal readiness once it's decoded so the host can
        // start building the main window only after the splash is fully drawn.
        var bitmap = new BitmapImage();
        bitmap.ImageOpened += (_, _) => FireReady();
        bitmap.ImageFailed += (_, _) => FireReady();
        bitmap.UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"));
        LogoImage.Source = bitmap;

        Root.Loaded += OnRootLoaded;
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Apply window-chrome tweaks after the window exists so they actually stick:
        // hide the white Win11 border and round the corners.
        int border = BorderColor;
        DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        if (Root.Resources.TryGetValue("FadeIn", out var resource) && resource is Storyboard fadeIn)
            fadeIn.Begin();
    }

    private void FireReady()
    {
        if (_readyFired) return;
        _readyFired = true;
        // Let the decoded image paint one frame before the host blocks the thread.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => ContentReady?.Invoke());
    }
}
