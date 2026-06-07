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
        bool force = false,
        bool autoOrder = true)
    {
        var pending = await _deployer.GetPendingScripts(
            scriptsPath, environment, connectionString, cancellationToken,
            includeDeployed: force, autoOrder: autoOrder);

        if (pending.Count == 0)
            return new DeploymentResult(0, 0, Cancelled: false, NoPendingScripts: true);

        int success = 0, failed = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);

            var script = pending[i];
            var displayName = script.Version; // relative-path identity, phase-qualified

            progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                success++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);
            }
            catch (Exception ex)
            {
                failed++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: false, Error: ex.Message));
                // Continue past failures: run everything that can run and collect every
                // error, so the user sees all failures at once, then fixes and re-runs.
                // Scripts are idempotent (IF NOT EXISTS) and FK-ordered, so this is safe.
            }
        }

        return new DeploymentResult(success, failed, Cancelled: false, NoPendingScripts: false);
    }
}
