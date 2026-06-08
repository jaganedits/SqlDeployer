using System;
using System.IO;
using System.Reflection;
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
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int GWL_STYLE = -16;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_BORDER = 0x00800000L;
    private const long WS_DLGFRAME = 0x00400000L;
    private const long WS_SYSMENU = 0x00080000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_FRAMECHANGED = 0x0020, SWP_NOACTIVATE = 0x0010;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    private readonly IntPtr _hwnd;

    public SplashWindow()
    {
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        // Strip the window frame entirely (no caption, no border, no resize edge)
        // so there is no Windows 11 white border around the splash.
        MakeFrameless();

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
        LogoImage.Source = new BitmapImage(
            new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png")));

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = v is null ? "" : $"Version {v.Major}.{v.Minor}.{v.Build}";

        Root.Loaded += OnRootLoaded;
    }

    private void MakeFrameless()
    {
        long style = GetWindowLongPtr(_hwnd, GWL_STYLE).ToInt64();
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER | WS_DLGFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        style |= WS_POPUP;
        SetWindowLongPtr(_hwnd, GWL_STYLE, new IntPtr(style));
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Belt and suspenders: also tell DWM to draw no border, and round corners.
        MakeFrameless();
        int none = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref none, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        if (Root.Resources.TryGetValue("FadeIn", out var resource) && resource is Storyboard fadeIn)
            fadeIn.Begin();
    }
}
