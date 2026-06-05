using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel Vm => App.History;

    public HistoryPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var d = App.Deploy;
        if (!string.IsNullOrWhiteSpace(d.Server) && !string.IsNullOrWhiteSpace(d.Database))
        {
            Vm.ConnectionString = ConnectionStringFactory.Build(d.Server, d.Login, d.Password, d.Database);
            await Vm.RefreshCommand.ExecuteAsync(null);
        }
    }
}
