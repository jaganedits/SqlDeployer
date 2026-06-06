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

    public List<ScriptStatus> ScriptStatuses { get; set; } = new();

    // Records the last includeDeployed value so tests can assert the re-run flag flows through.
    public bool? LastIncludeDeployed { get; private set; }

    public Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default,
        bool includeDeployed = false)
    {
        LastIncludeDeployed = includeDeployed;
        return Task.FromResult(new List<DeploymentScript>(Pending));
    }

    public Task<List<ScriptStatus>> GetScriptStatuses(
        string scriptsPath, string connectionString,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ScriptStatus>(ScriptStatuses));

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

    public Task ClearHistory(string connectionString)
    {
        History.Clear();
        return Task.CompletedTask;
    }

    public List<string> Databases { get; set; } = new();
    public Exception? GetDatabasesError { get; set; }

    public Task<List<string>> GetDatabases(string connectionString)
    {
        if (GetDatabasesError is not null) throw GetDatabasesError;
        return Task.FromResult(new List<string>(Databases));
    }
}
