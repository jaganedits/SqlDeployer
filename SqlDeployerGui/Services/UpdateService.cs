using System.Reflection;
using SqlDeployer.Services;
using Velopack;
using Velopack.Sources;

namespace SqlDeployerGui.Services;

public enum UpdateStatus
{
    UpToDate,
    UpdateReady,     // newer release downloaded and staged to apply (installed app)
    UpdateAvailable, // newer release exists but can't auto-apply (portable/dev) — Url points at it
    Failed           // network / feed error (swallowed)
}

public sealed record UpdateResult(UpdateStatus Status, string? Version, string? Url = null);

// Checks GitHub Releases for a newer version. Installed via Setup.exe: Velopack
// downloads and stages the update for apply-on-restart (UpdateReady). Portable
// zip or dev build (UpdateManager.IsInstalled == false): falls back to the GitHub
// API and reports UpdateAvailable with the release page URL — notify-only, the
// user downloads it themselves. All failures are swallowed: a missing network or
// release feed must never block the app from starting.
public sealed class UpdateService
{
    // Velopack reads the "latest" release assets (RELEASES file + *-full.nupkg)
    // that `vpk upload github` publishes there.
    private const string RepoUrl = "https://github.com/jaganedits/SqlDeployer";
    private const string ApiLatestUrl =
        "https://api.github.com/repos/jaganedits/SqlDeployer/releases/latest";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
        _manager = new UpdateManager(source);
    }

    public bool IsInstalled => _manager.IsInstalled;

    // Checks the feed and, if a newer release exists, downloads it (installed) or
    // reports where to get it (portable/dev). Callers show UI per status.
    public async Task<UpdateResult> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
            return await CheckLatestReleaseAsync();

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

    // Portable/dev fallback: compare the latest GitHub release tag against the
    // running assembly version. Notify-only — no download, no install.
    private static async Task<UpdateResult> CheckLatestReleaseAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SqlDeployer");
            var json = await http.GetStringAsync(ApiLatestUrl);

            var release = UpdateCheck.ParseLatestRelease(json);
            var latest = release is null ? null : UpdateCheck.ParseTag(release.Value.Tag);
            if (latest is null)
                return new UpdateResult(UpdateStatus.Failed, null);

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            return UpdateCheck.IsNewer(latest, current)
                ? new UpdateResult(UpdateStatus.UpdateAvailable, latest.ToString(3),
                    release!.Value.Url ?? RepoUrl + "/releases/latest")
                : new UpdateResult(UpdateStatus.UpToDate, null);
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
