using SqlDeployer;

namespace SqlDeployer.Core.Tests.Fakes;

// In-memory ISqlDeployer for testing DeploymentRunner orchestration.
public class FakeSqlDeployer : ISqlDeployer
{
    public List<DeploymentScript> Pending { get; set; } = new();
    public HashSet<string> FailingVersions { get; set; } = new();
    public List<string> Executed { get; } = new();
    public Func<string, Task>? OnExecute { get; set; }
    public List<DeploymentHistory> History { get; set; } = new();

    public Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<DeploymentScript>(Pending));

    public async Task ExecuteScript(
        string connectionString, string scriptPath, string version, string environment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OnExecute is not null) await OnExecute(version);
        Executed.Add(version);
        if (FailingVersions.Contains(version))
            throw new InvalidOperationException($"script {version} failed");
    }

    public Task<List<DeploymentHistory>> GetDeploymentHistory(string connectionString)
        => Task.FromResult(new List<DeploymentHistory>(History));
}
