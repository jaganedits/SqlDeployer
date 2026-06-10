namespace SqlDeployer.Models;

// Reported once per script as a run proceeds.
// Success == null means "starting this script"; non-null means it finished.
// The very first report of a run instead carries Plan: the full ordered list of
// script ids about to be executed (Current = 0, FileName empty).
public record DeploymentProgress(
    int Current,
    int Total,
    string FileName,
    bool? Success = null,
    string? Error = null,
    IReadOnlyList<string>? Plan = null);
