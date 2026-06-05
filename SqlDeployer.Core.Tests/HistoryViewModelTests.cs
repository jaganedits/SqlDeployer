using SqlDeployer;
using SqlDeployer.Core.Tests.Fakes;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class HistoryViewModelTests
{
    [Fact]
    public async Task Refresh_loads_history_rows_from_deployer()
    {
        var fake = new FakeSqlDeployer
        {
            History =
            {
                new DeploymentHistory("001_init.sql", "001", DateTime.UtcNow, true, null),
                new DeploymentHistory("002_bad.sql", "002", DateTime.UtcNow, false, "boom")
            }
        };
        var vm = new HistoryViewModel(fake);
        vm.ConnectionString = "cs";

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, h => h.Version == "002" && !h.Success);
    }

    [Fact]
    public async Task Refresh_without_connection_string_does_nothing()
    {
        var fake = new FakeSqlDeployer { History = { new DeploymentHistory("x", "1", DateTime.UtcNow, true, null) } };
        var vm = new HistoryViewModel(fake);
        vm.ConnectionString = "";

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
    }
}
