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

    private void DatabaseCombo_DropDownOpened(object sender, object e)
    {
        // Auto-load the list the first time the dropdown opens, if a server is set.
        if (Vm.Databases.Count == 0 && !string.IsNullOrWhiteSpace(Vm.Server) && !Vm.IsBusy)
        {
            _ = Vm.LoadDatabasesCommand.ExecuteAsync(null);
        }
    }
}
