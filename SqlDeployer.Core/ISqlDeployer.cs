namespace SqlDeployer;

public interface ISqlDeployer
{
    Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default);

    Task ExecuteScript(
        string connectionString, string scriptPath, string version, string environment,
        CancellationToken cancellationToken = default);

    Task<List<DeploymentHistory>> GetDeploymentHistory(string connectionString);

    Task<List<string>> GetDatabases(string connectionString);
}
