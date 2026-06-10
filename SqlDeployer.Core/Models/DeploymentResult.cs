namespace SqlDeployer.Models;

public record DeploymentResult(
    int SucceededCount,
    int FailedCount,
    bool Cancelled,
    bool NoPendingScripts)
{
    // Script ids (relative-path identities) by outcome. NotRun is non-empty only
    // when the run stopped early (cancellation) — with the run-all policy every
    // planned script is otherwise attempted.
    public IReadOnlyList<string> Succeeded { get; init; } = [];
    public IReadOnlyList<string> Failed { get; init; } = [];
    public IReadOnlyList<string> NotRun { get; init; } = [];
}
