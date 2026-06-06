using System;
using System.IO;
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
    private const int Width = 480;
    private const int Height = 340;

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

        appWindow.Resize(new SizeInt32(Width, Height));

        // Center on the primary work area.
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        appWindow.Move(new PointInt32(
            area.WorkArea.X + (area.WorkArea.Width - Width) / 2,
            area.WorkArea.Y + (area.WorkArea.Height - Height) / 2));

        LogoImage.Source = new BitmapImage(
            new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png")));

        Root.Loaded += (_, _) => ((Storyboard)Root.Resources["FadeIn"]).Begin();
    }
}
