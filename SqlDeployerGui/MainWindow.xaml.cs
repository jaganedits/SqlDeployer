using Microsoft.UI.Xaml;

namespace SqlDeployerGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "SQL Deploy — Migration Console";
    }
}
