using Microsoft.UI.Xaml;
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

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear deployment history?",
            Content = "This permanently deletes all deployment history records. "
                + "Already-deployed scripts will be treated as pending again on the next deploy.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await Vm.ClearCommand.ExecuteAsync(null);
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
