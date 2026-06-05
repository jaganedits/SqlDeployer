namespace SqlDeployer.Models;

// Reported once per script as a run proceeds.
// Success == null means "starting this script"; non-null means it finished.
public record DeploymentProgress(
    int Current,
    int Total,
    string FileName,
    bool? Success = null,
    string? Error = null);
