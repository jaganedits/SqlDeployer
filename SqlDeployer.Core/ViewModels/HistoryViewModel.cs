using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlDeployer;

namespace SqlDeployer.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISqlDeployer _deployer;

    [ObservableProperty] private string _connectionString = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;

    public ObservableCollection<DeploymentHistory> Items { get; } = new();

    public HistoryViewModel(ISqlDeployer deployer) => _deployer = deployer;

    [RelayCommand]
    private async Task Refresh()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Status = "Set a connection on the Deploy page first.";
            return;
        }

        IsBusy = true;
        try
        {
            var rows = await _deployer.GetDeploymentHistory(ConnectionString);
            Items.Clear();
            foreach (var row in rows) Items.Add(row);
            Status = Items.Count == 0 ? "No deployment history." : $"{Items.Count} record(s).";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Clear()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Status = "Set a connection on the Deploy page first.";
            return;
        }

        IsBusy = true;
        try
        {
            await _deployer.ClearHistory(ConnectionString);
            Items.Clear();
            Status = "Deployment history cleared.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
