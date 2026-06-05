namespace SqlDeployer.Models;

public record DeploymentResult(
    int SucceededCount,
    int FailedCount,
    bool Cancelled,
    bool NoPendingScripts);
