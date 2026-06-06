using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SqlDeployerGui.Views;
using Windows.Graphics;

namespace SqlDeployerGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "SQL Deploy — Migration Console";
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Open at a size that gives the Deploy two-pane layout (form + live output) room to breathe.
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(1320, 840));

        ContentFrame.Navigate(typeof(DeployPage));
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
