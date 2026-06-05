using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SqlDeployerGui.Views;

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
