using System.IO;
using SqlDeployer.Core.Tests.Fakes;
using SqlDeployer.Models;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class DeployViewModelTests
{
    private static DeployViewModel NewVm(
        FakeSqlDeployer? deployer = null,
        FakeDialogService? dialogs = null,
        SettingsService? settings = null)
    {
        deployer ??= new FakeSqlDeployer();
        dialogs ??= new FakeDialogService();
        settings ??= new SettingsService(Path.Combine(
            Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json"));
        return new DeployViewModel(new DeploymentRunner(deployer), deployer, dialogs, settings);
    }

    private static DeploymentScript Script(string v) => new($@"C:\s\{v}_x.sql", v, false);

    [Fact]
    public async Task Deploy_with_missing_fields_shows_validation_dialog_and_does_not_run()
    {
        var dialogs = new FakeDialogService();
        var vm = NewVm(dialogs: dialogs);
        vm.Server = "";

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Messages);
        Assert.Contains("required", dialogs.Messages[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deploy_with_nonexistent_folder_shows_validation_dialog()
    {
        var dialogs = new FakeDialogService();
        var vm = NewVm(dialogs: dialogs);
        vm.Server = "s";
        vm.Database = "d";
        vm.ScriptPath = @"C:\definitely\does\not\exist_" + Guid.NewGuid().ToString("N");

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Messages);
        Assert.Contains("exist", dialogs.Messages[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Successful_deploy_populates_success_log_and_persists_profile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var settingsPath = Path.Combine(Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json");
        var settings = new SettingsService(settingsPath);
        var deployer = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var vm = NewVm(deployer: deployer, settings: settings);
        vm.Server = "localhost";
        vm.Login = "sa";
        vm.Password = "secret";
        vm.Database = "AppDb";
        vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        // 1 "Plan: 0 script(s)..." preview line + 2 script success lines = 3.
        Assert.Equal(3, vm.SuccessLog.Count);
        Assert.Empty(vm.ErrorLog);
        Assert.False(vm.IsBusy);

        var reloaded = new SettingsService(settingsPath).Load();
        Assert.Equal("localhost", reloaded.LastConnection!.Server);
        Assert.DoesNotContain("secret", File.ReadAllText(settingsPath));

        Directory.Delete(tempDir, true);
        File.Delete(settingsPath);
    }

    [Fact]
    public async Task Successful_deploy_saves_encrypted_password_and_a_new_vm_restores_it()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var settingsPath = Path.Combine(Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json");
        var deployer = new FakeSqlDeployer { Pending = { Script("001") } };
        var vm = NewVm(deployer: deployer, settings: new SettingsService(settingsPath));
        vm.Server = "localhost"; vm.Login = "sa"; vm.Password = "secret"; vm.Database = "AppDb"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        // The plaintext password must not be written to disk.
        Assert.DoesNotContain("secret", File.ReadAllText(settingsPath));

        // A fresh VM loads the saved (encrypted) credential and restores the password and server list.
        var reloaded = NewVm(settings: new SettingsService(settingsPath));
        Assert.Equal("secret", reloaded.Password);
        Assert.Contains("localhost", reloaded.SavedServers);

        Directory.Delete(tempDir, true);
        File.Delete(settingsPath);
    }

    [Fact]
    public async Task Failing_script_goes_to_error_log()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002") },
            FailingVersions = { "002" }
        };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        // 1 "Plan: 0 script(s)..." preview line + 1 success line = 2.
        Assert.Equal(2, vm.SuccessLog.Count);
        Assert.Single(vm.ErrorLog);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Deploy_with_no_pending_logs_each_script_and_why_it_was_skipped()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer
        {
            Pending = { }, // nothing pending -> NoPendingScripts
            ScriptStatuses =
            {
                new ScriptStatus("001_init.sql", "001", IsRollback: false, AlreadyDeployed: true),
                new ScriptStatus("002_rollback.sql", "002", IsRollback: true, AlreadyDeployed: false)
            }
        };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        // 1 "Plan: 0 script(s)..." preview line + 2 status lines = 3.
        Assert.Equal(3, vm.SuccessLog.Count);
        Assert.Contains(vm.SuccessLog, e => e.Message.Contains("already deployed"));
        Assert.Contains(vm.SuccessLog, e => e.Message.Contains("rollback"));
        Assert.True(vm.IsResultOpen);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Deploy_without_force_does_not_include_already_deployed()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.False(deployer.LastIncludeDeployed);
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Deploy_with_force_rerun_includes_already_deployed_scripts()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;
        vm.ForceRerun = true;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.True(deployer.LastIncludeDeployed);
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Deploy_with_no_sql_files_reports_empty_folder()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { }, ScriptStatuses = { } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        // 1 "Plan: 0 script(s)..." preview line + 1 "No .sql files" line = 2.
        Assert.Equal(2, vm.SuccessLog.Count);
        Assert.Contains(vm.SuccessLog, e => e.Message.Contains("No .sql files"));
        Assert.True(vm.IsResultOpen);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task TestConnection_with_missing_fields_shows_dialog()
    {
        var dialogs = new FakeDialogService();
        var vm = NewVm(dialogs: dialogs);
        vm.Server = "";

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Messages);
    }

    [Fact]
    public async Task Browse_sets_ScriptPath_from_picker()
    {
        var dialogs = new FakeDialogService { FolderToReturn = @"C:\picked\scripts" };
        var vm = NewVm(dialogs: dialogs);

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\picked\scripts", vm.ScriptPath);
    }

    [Fact]
    public void Loads_last_connection_from_settings_on_construction()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json");
        var settings = new SettingsService(settingsPath);
        settings.Save(new AppSettings
        {
            LastConnection = new ConnectionProfile { Server = "saved-srv", Database = "saved-db", Login = "sa", ScriptPath = @"C:\x" }
        });

        var vm = NewVm(settings: settings);

        Assert.Equal("saved-srv", vm.Server);
        Assert.Equal("saved-db", vm.Database);
        Assert.Equal(string.Empty, vm.Password);
        File.Delete(settingsPath);
    }

    [Fact]
    public async Task LoadDatabases_with_no_server_shows_dialog_and_loads_nothing()
    {
        var dialogs = new FakeDialogService();
        var vm = NewVm(dialogs: dialogs);
        vm.Server = "";

        await vm.LoadDatabasesCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Messages);
        Assert.Empty(vm.Databases);
    }

    [Fact]
    public async Task LoadDatabases_populates_collection_from_deployer()
    {
        var deployer = new FakeSqlDeployer { Databases = { "AppDb", "Sales", "Hr" } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "localhost";

        await vm.LoadDatabasesCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Databases.Count);
        Assert.Contains("Sales", vm.Databases);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoadDatabases_on_error_shows_dialog_and_clears_busy()
    {
        var deployer = new FakeSqlDeployer { GetDatabasesError = new InvalidOperationException("boom") };
        var dialogs = new FakeDialogService();
        var vm = NewVm(deployer: deployer, dialogs: dialogs);
        vm.Server = "localhost";

        await vm.LoadDatabasesCommand.ExecuteAsync(null);

        Assert.Contains(dialogs.Messages, m => m.Message.Contains("boom"));
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Deploy_drives_progress_percent_to_100()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Equal(100, vm.ProgressPercent);
        Assert.Contains("100%", vm.ProgressText);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void AutoOrder_toggle_persists_to_settings()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json");
        var vm = NewVm(settings: new SettingsService(settingsPath));

        vm.AutoOrderByDependencies = false;

        Assert.False(new SettingsService(settingsPath).Load().AutoOrderByDependencies);
        File.Delete(settingsPath);
    }

    [Fact]
    public async Task Deploy_passes_autoOrder_flag_to_deployer()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;
        vm.AutoOrderByDependencies = false;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.False(deployer.LastAutoOrder);
        Directory.Delete(tempDir, true);
    }
}
