using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SqlDeployerGui.Views;
using Windows.Graphics;
using Windows.UI;

namespace SqlDeployerGui;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        Title = "SQL Deploy — Migration Console";
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Open at a size that gives the Deploy two-pane layout (form + live output) room to breathe.
        _appWindow.Resize(new SizeInt32(1320, 840));

        // Keep the system caption buttons (minimize/maximize/close) legible against
        // whichever theme is active — otherwise they render white-on-white in Light.
        if (Content is FrameworkElement root)
        {
            UpdateCaptionButtonColors(root.ActualTheme);
            root.ActualThemeChanged += (s, _) => UpdateCaptionButtonColors(s.ActualTheme);
        }

        ContentFrame.Navigate(typeof(DeployPage));
    }

    private void UpdateCaptionButtonColors(ElementTheme theme)
    {
        var titleBar = _appWindow.TitleBar;
        bool light = theme == ElementTheme.Light;

        var foreground = light ? Colors.Black : Colors.White;
        var inactive = light ? Color.FromArgb(255, 150, 150, 150) : Color.FromArgb(255, 120, 120, 120);
        var hoverBg = light ? Color.FromArgb(25, 0, 0, 0) : Color.FromArgb(30, 255, 255, 255);
        var pressedBg = light ? Color.FromArgb(50, 0, 0, 0) : Color.FromArgb(55, 255, 255, 255);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactive;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag as string)
            {
                case "Deploy": ContentFrame.Navigate(typeof(DeployPage)); break;
                case "History": ContentFrame.Navigate(typeof(HistoryPage)); break;
                case "Settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
            }
        }
    }
}
