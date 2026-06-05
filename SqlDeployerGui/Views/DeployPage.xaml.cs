using Microsoft.UI.Xaml.Controls;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class DeployPage : Page
{
    public DeployViewModel Vm => App.Deploy;

    public DeployPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }
}
