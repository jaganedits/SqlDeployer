using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using SqlDeployer;
using SqlDeployer.Models;
using SqlDeployer.Services;

namespace SqlDeployer.ViewModels;

public partial class DeployViewModel : ObservableObject
{
    private const string Environment = "GUI";

    private readonly DeploymentRunner _runner;
    private readonly ISqlDeployer _deployer;
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settings;
    private readonly IDeployNotifier _notifier;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _server = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _database = string.Empty;
    [ObservableProperty] private string _scriptPath = string.Empty;

    // When true, the next deploy ignores deployment history and re-runs every script.
    [ObservableProperty] private bool _forceRerun;

    [ObservableProperty] private string _status = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progressValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progressMax = 1;

    public int ProgressPercent => ProgressMax > 0
        ? (int)(ProgressValue * 100.0 / ProgressMax)
        : 0;

    public string ProgressText => $"{ProgressPercent}% — {ProgressValue} / {ProgressMax}";

    // Auto-order deploys by detected FK dependencies (persisted). Default on.
    [ObservableProperty] private bool _autoOrderByDependencies = true;

    // Inline result banner shown after Test/Deploy (green on success, red on failure)
    // instead of an interrupting popup dialog.
    [ObservableProperty] private bool _isResultOpen;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private LogKind _resultKind = LogKind.Info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    // True only while a deployment is actually running. Drives the progress bar's
    // visibility so the "0% — 0 / 1" bar is hidden at rest and shown only after the
    // user clicks Deploy. (IsBusy also covers Test Connection / Load Databases, which
    // have no meaningful progress, so it isn't used for this.)
    [ObservableProperty] private bool _isDeploying;

    public ObservableCollection<LogEntry> SuccessLog { get; } = new();
    public ObservableCollection<LogEntry> ErrorLog { get; } = new();

    // Scripts planned for the current/last run that are not deployed yet: fills
    // from the runner's plan report, drains as scripts succeed. After a run, what
    // remains is exactly the failed + never-attempted files (still re-runnable).
    public ObservableCollection<LogEntry> PendingLog { get; } = new();

    public ObservableCollection<string> Databases { get; } = new();

    // Servers from previously-saved connections, for the Server autocomplete.
    public ObservableCollection<string> SavedServers { get; } = new();

    // Tab header labels with live counts (e.g. "Success (3)"), mirroring the
    // original WinForms "Success Log(n)" / "Error Log(n)" segmented tabs.
    public string SuccessHeader => $"Success ({SuccessLog.Count})";
    public string ErrorHeader => $"Errors ({ErrorLog.Count})";
    public string PendingHeader => $"Pending ({PendingLog.Count})";

    public DeployViewModel(DeploymentRunner runner, ISqlDeployer deployer, IDialogService dialogs, SettingsService settings, IDeployNotifier notifier)
    {
        _runner = runner;
        _deployer = deployer;
        _dialogs = dialogs;
        _settings = settings;
        _notifier = notifier;

        SuccessLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SuccessHeader));
        ErrorLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ErrorHeader));
        PendingLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PendingHeader));

        var loaded = _settings.Load();
        _autoOrderByDependencies = loaded.AutoOrderByDependencies;
        foreach (var c in loaded.SavedConnections)
            if (!SavedServers.Contains(c.Server)) SavedServers.Add(c.Server);

        var saved = loaded.LastConnection;
        if (saved is not null)
        {
            Server = saved.Server;
            Login = saved.Login;
            Database = saved.Database;
            ScriptPath = saved.ScriptPath;
            Password = CredentialProtector.Unprotect(saved.Secret);
        }
    }

    // Loads a full saved profile onto the form (used by the Settings list). Server
    // is set first because OnServerChanged clears the database, so Database is set
    // last to survive that reset.
    public void LoadProfile(ConnectionProfile profile)
    {
        Server = profile.Server;
        Login = profile.Login;
        ScriptPath = profile.ScriptPath;
        Password = CredentialProtector.Unprotect(profile.Secret);
        Database = profile.Database;
    }

    // Refill the credential fields from a previously-saved server, if known.
    public void ApplyServerProfile(string server)
    {
        if (string.IsNullOrWhiteSpace(server)) return;

        var profile = _settings.Load().SavedConnections
            .FirstOrDefault(c => string.Equals(c.Server, server, StringComparison.OrdinalIgnoreCase));
        if (profile is null) return;

        Login = profile.Login;
        Database = profile.Database;
        ScriptPath = profile.ScriptPath;
        Password = CredentialProtector.Unprotect(profile.Secret);
    }

    [RelayCommand]
    private async Task Browse()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (!string.IsNullOrEmpty(folder)) ScriptPath = folder;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database))
        {
            await _dialogs.ShowMessageAsync("Validation", "Server and Database are required.");
            return;
        }

        IsResultOpen = false;
        IsBusy = true;
        Status = "Testing connection...";
        try
        {
            var cs = ConnectionStringFactory.Build(Server, Login, Password, Database);
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            Status = "Connection succeeded";
            SaveCredential();
            ShowResult("Connection successful — credentials saved for this server.", LogKind.Success);
        }
        catch (Exception ex)
        {
            Status = "Connection failed";
            ShowResult($"Connection failed: {ex.Message}", LogKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Deploy()
    {
        if (string.IsNullOrWhiteSpace(Server) ||
            string.IsNullOrWhiteSpace(Database) ||
            string.IsNullOrWhiteSpace(ScriptPath))
        {
            await _dialogs.ShowMessageAsync("Validation", "Server, Database, and Script path are required.");
            return;
        }

        if (!Directory.Exists(ScriptPath))
        {
            await _dialogs.ShowMessageAsync("Validation", "The selected script folder does not exist.");
            return;
        }

        SuccessLog.Clear();
        ErrorLog.Clear();
        PendingLog.Clear();
        ProgressValue = 0;
        IsResultOpen = false;
        IsBusy = true;
        IsDeploying = true;
        _cts = new CancellationTokenSource();
        Status = "Loading pending scripts...";
        LogPlanPreview();

        // Synchronous IProgress so the UI/log updates happen inline on the calling
        // thread (WinUI marshals via bindings; tests need deterministic ordering).
        var progress = new SyncProgress<DeploymentProgress>(OnProgress);

        try
        {
            var cs = ConnectionStringFactory.Build(Server, Login, Password, Database);
            var result = await _runner.RunAsync(cs, ScriptPath, Environment, progress, _cts.Token, ForceRerun, AutoOrderByDependencies);

            if (result.NoPendingScripts)
            {
                // LogPlanPreview prefilled from disk without DB knowledge; nothing
                // is actually pending, so drop the prefill.
                PendingLog.Clear();
                Status = "No pending scripts.";
                await LogScanSummary(cs);
            }
            else if (result.Cancelled)
            {
                Status = $"Deployment cancelled — {PendingLog.Count} script(s) not run.";
                ShowResult("Deployment cancelled.", LogKind.Info);
                _notifier.Notify(DeployNotificationKind.Stopped,
                    "Deployment stopped",
                    $"Deployment to {Database} was cancelled.");
            }
            else
            {
                Status = $"Finished: {result.SucceededCount} succeeded, {result.FailedCount} failed.";
                ShowResult(
                    $"Deployment complete — {result.SucceededCount} succeeded, {result.FailedCount} failed.",
                    result.FailedCount > 0 ? LogKind.Error : LogKind.Success);
                _notifier.Notify(
                    result.FailedCount > 0 ? DeployNotificationKind.Failed : DeployNotificationKind.Finished,
                    result.FailedCount > 0 ? "Deployment finished with errors" : "Deployment finished",
                    $"{Database}: {result.SucceededCount} succeeded, {result.FailedCount} failed.");
            }

            SaveCredential();
        }
        catch (Exception ex)
        {
            // PendingLog is left as-is on a hard failure: the remaining entries are
            // exactly the scripts that are still re-runnable.
            Status = "Deployment failed.";
            ShowResult($"Deployment failed: {ex.Message}", LogKind.Error);
            _notifier.Notify(DeployNotificationKind.Failed,
                "Deployment failed",
                $"Deployment to {Database} failed: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
            IsDeploying = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_cts is { IsCancellationRequested: false })
        {
            Status = "Cancelling...";
            _cts.Cancel();
        }
    }

    [RelayCommand]
    private async Task LoadDatabases()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            await _dialogs.ShowMessageAsync("Validation", "Enter a Server first to load its databases.");
            return;
        }

        IsBusy = true;
        Status = "Loading databases...";
        // Clear up front so a failed/cancelled load never leaves the previous
        // server's databases on screen.
        Databases.Clear();
        try
        {
            var cs = ConnectionStringFactory.BuildForServer(Server, Login, Password);
            var names = await _deployer.GetDatabases(cs);
            foreach (var name in names) Databases.Add(name);
            Status = Databases.Count == 0 ? "No user databases found." : $"{Databases.Count} database(s) loaded.";
        }
        catch (Exception ex)
        {
            Status = "Failed to load databases.";
            await _dialogs.ShowMessageAsync("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnProgress(DeploymentProgress p)
    {
        if (p.Plan is not null)
        {
            PendingLog.Clear();
            foreach (var id in p.Plan)
                PendingLog.Add(new LogEntry(id, LogKind.Info));
            ProgressMax = p.Total;
            return;
        }

        ProgressMax = p.Total;
        if (p.Success is null)
        {
            Status = $"Processing {p.Current}/{p.Total}: {p.FileName}...";
            return;
        }

        ProgressValue = p.Current;
        if (p.Success == true)
        {
            var pendingEntry = PendingLog.FirstOrDefault(x => x.Message == p.FileName);
            if (pendingEntry is not null) PendingLog.Remove(pendingEntry);
            SuccessLog.Add(new LogEntry($"{p.FileName} :: Processed successfully", LogKind.Success));
        }
        else
            ErrorLog.Add(new LogEntry($"{p.FileName} :: {p.Error}", LogKind.Error));
    }

    private void ShowResult(string message, LogKind kind)
    {
        ResultMessage = message;
        ResultKind = kind;
        IsResultOpen = true;
    }

    // When a deploy finds nothing to run, list every .sql file in the folder and
    // why it was skipped — so "no pending scripts" is explained rather than mysterious.
    private async Task LogScanSummary(string connectionString)
    {
        List<ScriptStatus> statuses;
        try
        {
            statuses = await _deployer.GetScriptStatuses(ScriptPath, connectionString);
        }
        catch (Exception ex)
        {
            ShowResult($"Could not scan the scripts folder: {ex.Message}", LogKind.Error);
            return;
        }

        if (statuses.Count == 0)
        {
            SuccessLog.Add(new LogEntry($"No .sql files found in: {ScriptPath}", LogKind.Info));
            ShowResult(
                "No .sql files were found in the selected folder or its subfolders. Check the Script path.",
                LogKind.Info);
            return;
        }

        foreach (var s in statuses)
        {
            var reason = s.IsRollback ? "rollback script — skipped"
                : s.AlreadyDeployed ? "already deployed — skipped"
                : "pending";
            SuccessLog.Add(new LogEntry($"{s.FileName} :: {reason}", LogKind.Info));
        }

        var deployed = statuses.Count(s => s.AlreadyDeployed && !s.IsRollback);
        ShowResult(
            $"No pending scripts. Found {statuses.Count} file(s); {deployed} already deployed. " +
            "Each script is applied only once — add a new versioned script (or remove its DeploymentHistory row) to deploy again.",
            LogKind.Info);
    }

    // Saves the current connection (with the password DPAPI-encrypted) as the
    // last-used profile and upserts it into the per-server saved list.
    private void SaveCredential()
    {
        if (string.IsNullOrWhiteSpace(Server)) return;

        var profile = new ConnectionProfile
        {
            Server = Server,
            Login = Login,
            Database = Database,
            ScriptPath = ScriptPath,
            Secret = CredentialProtector.Protect(Password)
        };

        var settings = _settings.Load();
        settings.LastConnection = profile;
        settings.SavedConnections.RemoveAll(c =>
            string.Equals(c.Server, Server, StringComparison.OrdinalIgnoreCase));
        settings.SavedConnections.Add(profile);
        _settings.Save(settings);

        if (!SavedServers.Contains(Server)) SavedServers.Add(Server);
    }

    // Editing the server invalidates the loaded database list — it belongs to the
    // old server. Clearing it also re-arms the lazy reload in the Database box
    // (which only fetches when the list is empty), so the new server's databases
    // are pulled on next use instead of showing the previous server's.
    partial void OnServerChanged(string value)
    {
        Databases.Clear();
        Database = string.Empty;
    }

    partial void OnAutoOrderByDependenciesChanged(bool value)
    {
        var s = _settings.Load();
        s.AutoOrderByDependencies = value;
        _settings.Save(s);
    }

    // Logs the computed run order (and detected parent->child links when auto-ordering)
    // before running, so the order is visible rather than a black box. Best-effort.
    private void LogPlanPreview()
    {
        try
        {
            var nodes = SqlServerDeployer.DiscoverScripts(ScriptPath);
            var plan = ScriptDependencyResolver.Resolve(nodes, AutoOrderByDependencies);
            if (plan.Order.Count == 0) return; // nothing to deploy; skip the preview noise

            // Best-effort prefill: replaced by the runner's accurate plan report
            // (which also excludes already-deployed scripts) moments later, but it
            // survives when the deploy aborts before running (cycle, bad folder).
            PendingLog.Clear();
            foreach (var n in plan.Order)
                if (!SqlServerDeployer.IsRollbackScript(n.Id))
                    PendingLog.Add(new LogEntry(n.Id, LogKind.Info));

            var ordering = AutoOrderByDependencies ? "dependency-ordered" : "folder + name order";
            SuccessLog.Add(new LogEntry($"Plan: {plan.Order.Count} script(s), {ordering}.", LogKind.Info));
            if (AutoOrderByDependencies)
                foreach (var e in plan.Edges)
                    SuccessLog.Add(new LogEntry($"  {e.ChildId} depends on {e.ParentId} (table {e.Table})", LogKind.Info));
        }
        catch
        {
            // Preview is best-effort; the real run still reports errors.
        }
    }

}
