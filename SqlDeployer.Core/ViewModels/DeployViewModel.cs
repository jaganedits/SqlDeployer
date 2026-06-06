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
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _server = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _database = string.Empty;
    [ObservableProperty] private string _scriptPath = string.Empty;

    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // Inline result banner shown after Test/Deploy (green on success, red on failure)
    // instead of an interrupting popup dialog.
    [ObservableProperty] private bool _isResultOpen;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private LogKind _resultKind = LogKind.Info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    public ObservableCollection<LogEntry> SuccessLog { get; } = new();
    public ObservableCollection<LogEntry> ErrorLog { get; } = new();
    public ObservableCollection<string> Databases { get; } = new();

    // Tab header labels with live counts (e.g. "Success (3)"), mirroring the
    // original WinForms "Success Log(n)" / "Error Log(n)" segmented tabs.
    public string SuccessHeader => $"Success ({SuccessLog.Count})";
    public string ErrorHeader => $"Errors ({ErrorLog.Count})";

    public DeployViewModel(DeploymentRunner runner, ISqlDeployer deployer, IDialogService dialogs, SettingsService settings)
    {
        _runner = runner;
        _deployer = deployer;
        _dialogs = dialogs;
        _settings = settings;

        SuccessLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SuccessHeader));
        ErrorLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ErrorHeader));

        var saved = _settings.Load().LastConnection;
        if (saved is not null)
        {
            Server = saved.Server;
            Login = saved.Login;
            Database = saved.Database;
            ScriptPath = saved.ScriptPath;
            // Password is intentionally never restored.
        }
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
            ShowResult("Connection successful — the database is reachable.", LogKind.Success);
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
        ProgressValue = 0;
        IsResultOpen = false;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        Status = "Loading pending scripts...";

        // Synchronous IProgress so the UI/log updates happen inline on the calling
        // thread (WinUI marshals via bindings; tests need deterministic ordering).
        var progress = new SyncProgress<DeploymentProgress>(OnProgress);

        try
        {
            var cs = ConnectionStringFactory.Build(Server, Login, Password, Database);
            var result = await _runner.RunAsync(cs, ScriptPath, Environment, progress, _cts.Token);

            if (result.NoPendingScripts)
            {
                Status = "No pending scripts.";
                await LogScanSummary(cs);
            }
            else if (result.Cancelled)
            {
                Status = "Deployment cancelled.";
                ShowResult("Deployment cancelled.", LogKind.Info);
            }
            else
            {
                Status = $"Finished: {result.SucceededCount} succeeded, {result.FailedCount} failed.";
                ShowResult(
                    $"Deployment complete — {result.SucceededCount} succeeded, {result.FailedCount} failed.",
                    result.FailedCount > 0 ? LogKind.Error : LogKind.Success);
            }

            PersistConnection();
        }
        catch (Exception ex)
        {
            Status = "Deployment failed.";
            ShowResult($"Deployment failed: {ex.Message}", LogKind.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
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
        try
        {
            var cs = ConnectionStringFactory.BuildForServer(Server, Login, Password);
            var names = await _deployer.GetDatabases(cs);
            Databases.Clear();
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
        ProgressMax = p.Total;
        if (p.Success is null)
        {
            Status = $"Processing {p.Current}/{p.Total}: {p.FileName}...";
            return;
        }

        ProgressValue = p.Current;
        if (p.Success == true)
            SuccessLog.Add(new LogEntry($"{p.FileName} :: Processed successfully", LogKind.Success));
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
                "No .sql files were found in the selected folder. Check the Script path — only top-level *.sql files are scanned (subfolders are ignored).",
                LogKind.Info);
            return;
        }

        foreach (var s in statuses)
        {
            var reason = s.IsRollback ? "rollback script — skipped"
                : s.AlreadyDeployed ? $"already deployed (version {s.Version}) — skipped"
                : "pending";
            SuccessLog.Add(new LogEntry($"{s.FileName} :: {reason}", LogKind.Info));
        }

        var deployed = statuses.Count(s => s.AlreadyDeployed && !s.IsRollback);
        ShowResult(
            $"No pending scripts. Found {statuses.Count} file(s); {deployed} already deployed. " +
            "Each version is applied only once — add a new versioned script (or remove its DeploymentHistory row) to deploy again.",
            LogKind.Info);
    }

    private void PersistConnection()
    {
        var settings = _settings.Load();
        settings.LastConnection = new ConnectionProfile
        {
            Server = Server,
            Login = Login,
            Database = Database,
            ScriptPath = ScriptPath
        };
        _settings.Save(settings);
    }

    // Synchronous IProgress so the UI/log updates happen inline on the calling
    // thread (WinUI marshals via bindings; tests need deterministic ordering).
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
