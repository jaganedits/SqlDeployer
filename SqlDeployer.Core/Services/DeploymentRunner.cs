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

        var planIds = pending.Select(p => p.Version).ToList();
        progress.Report(new DeploymentProgress(0, pending.Count, string.Empty, Plan: planIds));
        var succeeded = new List<string>();
        var failed = new List<string>();

        // NotRun = planned but neither succeeded nor failed (only on early stop).
        DeploymentResult Finish(bool cancelled) =>
            new(succeeded.Count, failed.Count, cancelled, NoPendingScripts: false)
            {
                Succeeded = succeeded,
                Failed = failed,
                NotRun = planIds.Where(id => !succeeded.Contains(id) && !failed.Contains(id)).ToList()
            };

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Finish(cancelled: true);

            var script = pending[i];
            var displayName = script.Version; // relative-path identity, phase-qualified

            progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                succeeded.Add(script.Version);
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return Finish(cancelled: true);
            }
            catch (Exception ex)
            {
                failed.Add(script.Version);
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: false, Error: ex.Message));
                // Continue past failures: run everything that can run and collect every
                // error, so the user sees all failures at once, then fixes and re-runs.
                // Scripts are idempotent (IF NOT EXISTS) and FK-ordered, so this is safe.
            }
        }

        return Finish(cancelled: false);
    }
}
