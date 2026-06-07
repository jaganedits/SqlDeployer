using SqlDeployer;
using SqlDeployer.Core.Tests.Fakes;
using SqlDeployer.Models;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class DeploymentRunnerTests
{
    private static DeploymentScript Script(string v) =>
        new($@"C:\scripts\{v}_x.sql", v, IsRollback: false);

    [Fact]
    public async Task Reports_no_pending_scripts_when_none()
    {
        var fake = new FakeSqlDeployer { Pending = new() };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.True(result.NoPendingScripts);
        Assert.Equal(0, result.SucceededCount);
    }

    [Fact]
    public async Task Runs_all_scripts_and_counts_successes()
    {
        var fake = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(new[] { "001", "002" }, fake.Executed.ToArray());
    }

    [Fact]
    public async Task Stops_at_the_first_failing_script()
    {
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            FailingVersions = { "002" }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, fake.Executed.Count); // stopped after 002 failed; 003 not run
        Assert.Equal(new[] { "001", "002" }, fake.Executed.ToArray());
    }

    [Fact]
    public async Task Reports_progress_per_script()
    {
        var fake = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var runner = new DeploymentRunner(fake);
        var updates = new List<DeploymentProgress>();
        var progress = new Progress<DeploymentProgress>(updates.Add);

        await runner.RunAsync("cs", "path", "GUI", progress, CancellationToken.None);

        await Task.Delay(200);
        Assert.Contains(updates, u => u.FileName.Contains("001") && u.Success == true);
        Assert.Contains(updates, u => u.FileName.Contains("002") && u.Success == true);
    }

    [Fact]
    public async Task Cancellation_stops_the_run_and_sets_flag()
    {
        var cts = new CancellationTokenSource();
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002") },
            OnExecute = _ => { cts.Cancel(); return Task.CompletedTask; }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(1, fake.Executed.Count); // stopped after the first
    }
}
