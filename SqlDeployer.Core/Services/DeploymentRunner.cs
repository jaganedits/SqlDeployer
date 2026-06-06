using System.IO;
using SqlDeployer.Models;

namespace SqlDeployer.Services;

// Orchestrates a deployment run over ISqlDeployer: fetch pending scripts,
// execute each inside its own transaction (handled by the backend), report
// progress, count outcomes, and honor cancellation. Mirrors the original
// Form1.btnStartDeployment_Click loop, minus all UI concerns.
public class DeploymentRunner
{
    private readonly ISqlDeployer _deployer;

    public DeploymentRunner(ISqlDeployer deployer) => _deployer = deployer;

    public async Task<DeploymentResult> RunAsync(
        string connectionString,
        string scriptsPath,
        string environment,
        IProgress<DeploymentProgress> progress,
        CancellationToken cancellationToken,
        bool force = false)
    {
        var pending = await _deployer.GetPendingScripts(
            scriptsPath, environment, connectionString, cancellationToken, includeDeployed: force);

        if (pending.Count == 0)
            return new DeploymentResult(0, 0, Cancelled: false, NoPendingScripts: true);

        int success = 0, failed = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);

            var script = pending[i];
            var fileName = Path.GetFileName(script.FileName);
            progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                success++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);
            }
            catch (Exception ex)
            {
                failed++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName, Success: false, Error: ex.Message));
            }
        }

        return new DeploymentResult(success, failed, Cancelled: false, NoPendingScripts: false);
    }
}
