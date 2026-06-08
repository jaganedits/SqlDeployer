using Velopack;
using Velopack.Sources;

namespace SqlDeployerGui.Services;

public enum UpdateStatus
{
    NotInstalled, // running from a dev build / xcopy folder — updates don't apply
    UpToDate,
    UpdateReady,  // a newer release was downloaded and is staged to apply
    Failed        // network / feed error (swallowed)
}

public sealed record UpdateResult(UpdateStatus Status, string? Version);

// Checks GitHub Releases for a newer Velopack release, downloads it, and applies
// it on next launch. Only meaningful when the app was installed via Setup.exe —
// when running from a dev build (F5) or an xcopy folder, UpdateManager.IsInstalled
// is false and every call is a harmless no-op. All failures are swallowed: a
// missing network or release feed must never block the app from starting.
public sealed class UpdateService
{
    // Point this at your GitHub repo. Velopack reads the "latest" release assets
    // (RELEASES file + *-full.nupkg) that `vpk upload github` publishes there.
    // To host the feed on Vercel instead, swap this for a SimpleWebSource pointing
    // at your static URL.
    private const string RepoUrl = "https://github.com/jaganedits/SqlDeployer";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
        _manager = new UpdateManager(source);
    }

    public bool IsInstalled => _manager.IsInstalled;

    // Checks the feed and, if a newer release exists, downloads it and stages it
    // for apply. Reports the outcome so callers can show appropriate UI.
    public async Task<UpdateResult> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
            return new UpdateResult(UpdateStatus.NotInstalled, null);

        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null)
                return new UpdateResult(UpdateStatus.UpToDate, null);

            await _manager.DownloadUpdatesAsync(info);
            return new UpdateResult(UpdateStatus.UpdateReady, info.TargetFullRelease.Version.ToString());
        }
        catch
        {
            return new UpdateResult(UpdateStatus.Failed, null);
        }
    }

    // Relaunches into the freshly downloaded version. Does not return on success
    // (the process exits). Call only after a CheckAndDownloadAsync that returned
    // UpdateStatus.UpdateReady.
    public void ApplyAndRestart()
    {
        if (!_manager.IsInstalled) return;
        var info = _manager.CheckForUpdates();
        if (info is not null)
            _manager.ApplyUpdatesAndRestart(info);
    }
}
