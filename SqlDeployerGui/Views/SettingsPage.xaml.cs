using Microsoft.UI.Xaml.Controls;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm => App.SettingsVm;

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }
}
