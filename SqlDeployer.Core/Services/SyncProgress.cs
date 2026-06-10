namespace SqlDeployer.Services;

// Synchronous IProgress<T>: reports invoke the handler inline on the calling
// thread, unlike Progress<T> which posts asynchronously. Used where callers
// need deterministic ordering (UI log updates, tests asserting report order).
public sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;
    public SyncProgress(Action<T> handler) => _handler = handler;
    public void Report(T value) => _handler(value);
}
