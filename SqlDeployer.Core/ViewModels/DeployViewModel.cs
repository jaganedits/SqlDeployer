using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using SqlDeployer.Models;
using SqlDeployer.Services;

namespace SqlDeployer.ViewModels;

public partial class DeployViewModel : ObservableObject
{
    private const string Environment = "GUI";

    private readonly DeploymentRunner _runner;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    public ObservableCollection<LogEntry> SuccessLog { get; } = new();
    public ObservableCollection<LogEntry> ErrorLog { get; } = new();

    public DeployViewModel(DeploymentRunner runner, IDialogService dialogs, SettingsService settings)
    {
        _runner = runner;
        _dialogs = dialogs;
        _settings = settings;

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

        IsBusy = true;
        Status = "Testing connection...";
        try
        {
            var cs = ConnectionStringFactory.Build(Server, Login, Password, Database);
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            Status = "Connection succeeded";
            await _dialogs.ShowMessageAsync("Connection", "Database connection successful.");
        }
        catch (Exception ex)
        {
            Status = "Connection failed";
            await _dialogs.ShowMessageAsync("Connection error", ex.Message);
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
                await _dialogs.ShowMessageAsync("Deployment", "No new pending scripts found to deploy.");
            }
            else if (result.Cancelled)
            {
                Status = "Deployment cancelled.";
            }
            else
            {
                Status = $"Finished: {result.SucceededCount} succeeded, {result.FailedCount} failed.";
                await _dialogs.ShowMessageAsync(
                    result.FailedCount > 0 ? "Completed with errors" : "Deployment complete",
                    $"Succeeded: {result.SucceededCount}\nFailed: {result.FailedCount}");
            }

            PersistConnection();
        }
        catch (Exception ex)
        {
            Status = "Deployment failed.";
            await _dialogs.ShowMessageAsync("Error", ex.Message);
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
