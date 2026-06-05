# SQL Deploy — WinUI 3 Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the WinForms SQL migration tool as a modern WinUI 3 (MVVM) app — left nav rail with Deploy / History / Settings pages — reusing the existing deployment backend unchanged, shipped as a portable self-contained `.exe`.

**Architecture:** Three projects. `SqlDeployer.Core` (plain `net10.0` class library) holds all UI-agnostic, unit-testable code: the existing `SqlServerDeployer`, an extracted `ISqlDeployer` interface, models, `ConnectionStringFactory`, `SettingsService`, `DeploymentRunner`, the `IDialogService` abstraction, and the three ViewModels (CommunityToolkit.Mvvm). `SqlDeployerGui` (WinUI 3 app) holds App/MainWindow/Views and the concrete dialog + folder-picker services, and composes everything. `SqlDeployer.Core.Tests` (xUnit) references only Core, so tests run without the Windows App SDK.

**Tech Stack:** WinUI 3 (Windows App SDK, unpackaged + self-contained), .NET 10, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient, xUnit.

---

## File Structure

**`SqlDeployer.Core/` (class library, `net10.0`)**
- `SqlServerDeployer.cs` — moved as-is from root; one-line change to implement `ISqlDeployer`.
- `ISqlDeployer.cs` — interface extracted from `SqlServerDeployer` (pending scripts, execute, history).
- `Models/ConnectionProfile.cs` — server, login, database, scriptPath (no password).
- `Models/AppSettings.cs` — `LastConnection` + `Theme`.
- `Models/LogEntry.cs` — message + `LogKind` (Success / Error / Info).
- `Models/DeploymentProgress.cs` — per-script progress record.
- `Models/DeploymentResult.cs` — final tally record.
- `Services/ConnectionStringFactory.cs` — pure builder for the SQL connection string.
- `Services/SettingsService.cs` — JSON load/save under `%LocalAppData%`.
- `Services/DeploymentRunner.cs` — orchestrates a deploy run over `ISqlDeployer`.
- `Services/IDialogService.cs` — `ShowMessageAsync`, `ConfirmAsync`, `PickFolderAsync`.
- `ViewModels/DeployViewModel.cs`
- `ViewModels/HistoryViewModel.cs`
- `ViewModels/SettingsViewModel.cs`

**`SqlDeployerGui/` (WinUI 3 app)**
- `SqlDeployerGui.csproj`, `app.manifest`
- `App.xaml` / `App.xaml.cs` — startup, service composition, theme, window.
- `MainWindow.xaml` / `.cs` — NavigationView shell, Mica, custom title bar.
- `Views/DeployPage.xaml` / `.cs`
- `Views/HistoryPage.xaml` / `.cs`
- `Views/SettingsPage.xaml` / `.cs`
- `Services/DialogService.cs` — `IDialogService` via `ContentDialog` + `FolderPicker`.
- `Converters/` — `LogKindToBrushConverter`, `BoolToVisibilityConverter`, `InverseBoolConverter`.

**`SqlDeployer.Core.Tests/` (xUnit, `net10.0`)**
- `ConnectionStringFactoryTests.cs`, `SettingsServiceTests.cs`, `DeploymentRunnerTests.cs`,
  `DeployViewModelTests.cs`, `HistoryViewModelTests.cs`, `Fakes/FakeSqlDeployer.cs`, `Fakes/FakeDialogService.cs`.

**Removed from root:** `Form1.cs`, `Form1.Designer.cs`, `ModernUI.cs`, `Program.cs`, old `SqlDeployerGui.csproj`, `SqlServerDeployer.cs` (moved into Core).

---

## Task 0: Initialize git + solution

**Files:**
- Create: `.gitignore`, `SqlDeploy.sln`

- [ ] **Step 1: Initialize the repository**

Run:
```bash
cd /c/Users/Jagan/Downloads/Gui
git init
```

- [ ] **Step 2: Add a .NET .gitignore**

Create `.gitignore`:
```gitignore
bin/
obj/
.vs/
*.user
publish/
```

- [ ] **Step 3: Commit the existing code as a baseline**

```bash
git add .
git commit -m "chore: baseline WinForms app before WinUI 3 rewrite"
```

- [ ] **Step 4: Create an empty solution**

Run:
```bash
dotnet new sln -n SqlDeploy
```
Expected: `SqlDeploy.sln` created.

- [ ] **Step 5: Commit**

```bash
git add SqlDeploy.sln .gitignore
git commit -m "chore: add solution file and gitignore"
```

---

## Task 1: Create the Core class library and move the backend

**Files:**
- Create: `SqlDeployer.Core/SqlDeployer.Core.csproj`
- Move: `SqlServerDeployer.cs` → `SqlDeployer.Core/SqlServerDeployer.cs`

- [ ] **Step 1: Create the library project**

Run:
```bash
dotnet new classlib -n SqlDeployer.Core -f net10.0 -o SqlDeployer.Core
dotnet sln add SqlDeployer.Core/SqlDeployer.Core.csproj
dotnet add SqlDeployer.Core package Microsoft.Data.SqlClient --version 7.0.1
dotnet add SqlDeployer.Core package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Delete the auto-generated placeholder**

Delete `SqlDeployer.Core/Class1.cs`.

- [ ] **Step 3: Move the backend file**

Move `SqlServerDeployer.cs` from the repo root into `SqlDeployer.Core/`. Keep its `namespace SqlDeployer;` declaration and contents exactly as they are.

- [ ] **Step 4: Set the csproj contents**

Overwrite `SqlDeployer.Core/SqlDeployer.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>SqlDeployer</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.1" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Build to verify the backend compiles in the library**

Run: `dotnet build SqlDeployer.Core/SqlDeployer.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add SqlDeployer.Core SqlServerDeployer.cs
git commit -m "refactor: move SqlServerDeployer into SqlDeployer.Core library"
```

---

## Task 2: Extract the ISqlDeployer interface

**Files:**
- Create: `SqlDeployer.Core/ISqlDeployer.cs`
- Modify: `SqlDeployer.Core/SqlServerDeployer.cs` (class declaration line only)

- [ ] **Step 1: Create the interface**

Create `SqlDeployer.Core/ISqlDeployer.cs`:
```csharp
namespace SqlDeployer;

public interface ISqlDeployer
{
    Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default);

    Task ExecuteScript(
        string connectionString, string scriptPath, string version, string environment,
        CancellationToken cancellationToken = default);

    Task<List<DeploymentHistory>> GetDeploymentHistory(string connectionString);
}
```

- [ ] **Step 2: Implement the interface on the existing class**

In `SqlDeployer.Core/SqlServerDeployer.cs`, change the class declaration line:
```csharp
public class SqlServerDeployer
```
to:
```csharp
public class SqlServerDeployer : ISqlDeployer
```
(The method signatures already match the interface — no other changes.)

- [ ] **Step 3: Build to verify**

Run: `dotnet build SqlDeployer.Core/SqlDeployer.Core.csproj`
Expected: Build succeeded — confirms the existing methods satisfy `ISqlDeployer`.

- [ ] **Step 4: Commit**

```bash
git add SqlDeployer.Core/ISqlDeployer.cs SqlDeployer.Core/SqlServerDeployer.cs
git commit -m "refactor: extract ISqlDeployer interface for testability"
```

---

## Task 3: Create the test project

**Files:**
- Create: `SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj`
- Create: `SqlDeployer.Core.Tests/SmokeTest.cs`

- [ ] **Step 1: Create the xUnit project and wire references**

Run:
```bash
dotnet new xunit -n SqlDeployer.Core.Tests -f net10.0 -o SqlDeployer.Core.Tests
dotnet sln add SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj
dotnet add SqlDeployer.Core.Tests reference SqlDeployer.Core/SqlDeployer.Core.csproj
```

- [ ] **Step 2: Delete the generated placeholder test**

Delete `SqlDeployer.Core.Tests/UnitTest1.cs`.

- [ ] **Step 3: Write a smoke test that proves the harness runs**

Create `SqlDeployer.Core.Tests/SmokeTest.cs`:
```csharp
using SqlDeployer;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void DeploymentScript_record_holds_its_values()
    {
        var script = new DeploymentScript("001_init.sql", "001", IsRollback: false);
        Assert.Equal("001", script.Version);
        Assert.False(script.IsRollback);
    }
}
```

- [ ] **Step 4: Run the test to verify the harness works**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj`
Expected: PASS — 1 test passed.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core.Tests
git commit -m "test: add Core test project and smoke test"
```

---

## Task 4: ConnectionStringFactory (TDD)

**Files:**
- Create: `SqlDeployer.Core/Services/ConnectionStringFactory.cs`
- Test: `SqlDeployer.Core.Tests/ConnectionStringFactoryTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/ConnectionStringFactoryTests.cs`:
```csharp
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class ConnectionStringFactoryTests
{
    [Fact]
    public void Uses_sql_auth_when_login_provided()
    {
        var cs = ConnectionStringFactory.Build("myserver", "sa", "secret", "mydb");
        Assert.Contains("Data Source=myserver", cs);
        Assert.Contains("Initial Catalog=mydb", cs);
        Assert.Contains("User ID=sa", cs);
        Assert.Contains("Password=secret", cs);
        Assert.DoesNotContain("Integrated Security=True", cs);
    }

    [Fact]
    public void Uses_windows_auth_when_login_blank()
    {
        var cs = ConnectionStringFactory.Build("myserver", "   ", "ignored", "mydb");
        Assert.Contains("Integrated Security=True", cs);
        Assert.DoesNotContain("User ID=", cs);
    }

    [Fact]
    public void Trims_server_and_database_and_login()
    {
        var cs = ConnectionStringFactory.Build("  myserver ", " sa ", "secret", " mydb ");
        Assert.Contains("Data Source=myserver", cs);
        Assert.Contains("Initial Catalog=mydb", cs);
        Assert.Contains("User ID=sa", cs);
    }

    [Fact]
    public void Disables_encryption_to_match_existing_behavior()
    {
        var cs = ConnectionStringFactory.Build("s", "", "", "d");
        Assert.Contains("Encrypt=False", cs);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter ConnectionStringFactoryTests`
Expected: FAIL — `ConnectionStringFactory` does not exist.

- [ ] **Step 3: Implement ConnectionStringFactory**

Create `SqlDeployer.Core/Services/ConnectionStringFactory.cs`:
```csharp
using Microsoft.Data.SqlClient;

namespace SqlDeployer.Services;

// Pure builder mirroring the original Form1.GetConnectionString behavior:
// SQL auth when a login is supplied, otherwise Windows integrated auth.
public static class ConnectionStringFactory
{
    public static string Build(string server, string login, string password, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server.Trim(),
            InitialCatalog = database.Trim(),
            Encrypt = false
        };

        var trimmedLogin = login.Trim();
        if (!string.IsNullOrEmpty(trimmedLogin))
        {
            builder.UserID = trimmedLogin;
            builder.Password = password;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter ConnectionStringFactoryTests`
Expected: PASS — 4 tests passed.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/ConnectionStringFactory.cs SqlDeployer.Core.Tests/ConnectionStringFactoryTests.cs
git commit -m "feat: add ConnectionStringFactory with tests"
```

---

## Task 5: Core models

**Files:**
- Create: `SqlDeployer.Core/Models/ConnectionProfile.cs`
- Create: `SqlDeployer.Core/Models/AppSettings.cs`
- Create: `SqlDeployer.Core/Models/LogEntry.cs`
- Create: `SqlDeployer.Core/Models/DeploymentProgress.cs`
- Create: `SqlDeployer.Core/Models/DeploymentResult.cs`

- [ ] **Step 1: Create ConnectionProfile (note: no password field by design)**

Create `SqlDeployer.Core/Models/ConnectionProfile.cs`:
```csharp
namespace SqlDeployer.Models;

// Persisted connection info. Intentionally has NO password field —
// passwords are never written to disk (security decision).
public class ConnectionProfile
{
    public string Server { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create AppSettings**

Create `SqlDeployer.Core/Models/AppSettings.cs`:
```csharp
namespace SqlDeployer.Models;

public class AppSettings
{
    public ConnectionProfile? LastConnection { get; set; }

    // "Light", "Dark", or "Default" (follow system).
    public string Theme { get; set; } = "Default";
}
```

- [ ] **Step 3: Create LogEntry**

Create `SqlDeployer.Core/Models/LogEntry.cs`:
```csharp
namespace SqlDeployer.Models;

public enum LogKind { Success, Error, Info }

public record LogEntry(string Message, LogKind Kind);
```

- [ ] **Step 4: Create DeploymentProgress**

Create `SqlDeployer.Core/Models/DeploymentProgress.cs`:
```csharp
namespace SqlDeployer.Models;

// Reported once per script as a run proceeds.
// Success == null means "starting this script"; non-null means it finished.
public record DeploymentProgress(
    int Current,
    int Total,
    string FileName,
    bool? Success = null,
    string? Error = null);
```

- [ ] **Step 5: Create DeploymentResult**

Create `SqlDeployer.Core/Models/DeploymentResult.cs`:
```csharp
namespace SqlDeployer.Models;

public record DeploymentResult(
    int SucceededCount,
    int FailedCount,
    bool Cancelled,
    bool NoPendingScripts);
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build SqlDeployer.Core/SqlDeployer.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add SqlDeployer.Core/Models
git commit -m "feat: add core models (profile, settings, log, progress, result)"
```

---

## Task 6: SettingsService (TDD)

**Files:**
- Create: `SqlDeployer.Core/Services/SettingsService.cs`
- Test: `SqlDeployer.Core.Tests/SettingsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/SettingsServiceTests.cs`:
```csharp
using System.IO;
using SqlDeployer.Models;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SettingsServiceTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "sqldeploy_test_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var path = TempFile();
        var svc = new SettingsService(path);

        var settings = svc.Load();

        Assert.NotNull(settings);
        Assert.Null(settings.LastConnection);
        Assert.Equal("Default", settings.Theme);
    }

    [Fact]
    public void Save_then_Load_round_trips_values()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        var settings = new AppSettings
        {
            Theme = "Dark",
            LastConnection = new ConnectionProfile
            {
                Server = "localhost",
                Login = "sa",
                Database = "AppDb",
                ScriptPath = @"C:\scripts"
            }
        };

        svc.Save(settings);
        var loaded = new SettingsService(path).Load();

        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal("localhost", loaded.LastConnection!.Server);
        Assert.Equal("AppDb", loaded.LastConnection.Database);

        File.Delete(path);
    }

    [Fact]
    public void Saved_json_never_contains_a_password()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        svc.Save(new AppSettings
        {
            LastConnection = new ConnectionProfile { Server = "s", Login = "sa", Database = "d", ScriptPath = "p" }
        });

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

        File.Delete(path);
    }

    [Fact]
    public void Load_returns_defaults_when_file_is_corrupt()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ not valid json ");

        var settings = new SettingsService(path).Load();

        Assert.Equal("Default", settings.Theme);
        File.Delete(path);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter SettingsServiceTests`
Expected: FAIL — `SettingsService` does not exist.

- [ ] **Step 3: Implement SettingsService**

Create `SqlDeployer.Core/Services/SettingsService.cs`:
```csharp
using System.Text.Json;
using SqlDeployer.Models;

namespace SqlDeployer.Services;

// Persists AppSettings as JSON. Because ConnectionProfile has no password
// property, secrets can never be written by construction.
public class SettingsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SettingsService(string filePath) => _filePath = filePath;

    // Default location: %LocalAppData%\SqlDeploy\settings.json
    public static SettingsService Default()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SqlDeploy");
        Directory.CreateDirectory(dir);
        return new SettingsService(Path.Combine(dir, "settings.json"));
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            // Missing/corrupt settings should never crash the app.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter SettingsServiceTests`
Expected: PASS — 4 tests passed.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/SettingsService.cs SqlDeployer.Core.Tests/SettingsServiceTests.cs
git commit -m "feat: add SettingsService with tests (password never persisted)"
```

---

## Task 7: DeploymentRunner (TDD)

**Files:**
- Create: `SqlDeployer.Core/Services/DeploymentRunner.cs`
- Create: `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs`
- Test: `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`

- [ ] **Step 1: Create the fake deployer**

Create `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs`:
```csharp
using SqlDeployer;

namespace SqlDeployer.Core.Tests.Fakes;

// In-memory ISqlDeployer for testing DeploymentRunner orchestration.
public class FakeSqlDeployer : ISqlDeployer
{
    public List<DeploymentScript> Pending { get; set; } = new();
    public HashSet<string> FailingVersions { get; set; } = new();
    public List<string> Executed { get; } = new();
    public Func<string, Task>? OnExecute { get; set; }
    public List<DeploymentHistory> History { get; set; } = new();

    public Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<DeploymentScript>(Pending));

    public async Task ExecuteScript(
        string connectionString, string scriptPath, string version, string environment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OnExecute is not null) await OnExecute(version);
        Executed.Add(version);
        if (FailingVersions.Contains(version))
            throw new InvalidOperationException($"script {version} failed");
    }

    public Task<List<DeploymentHistory>> GetDeploymentHistory(string connectionString)
        => Task.FromResult(new List<DeploymentHistory>(History));
}
```

- [ ] **Step 2: Write the failing tests**

Create `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`:
```csharp
using SqlDeployer;
using SqlDeployer.Core.Tests.Fakes;
using SqlDeployer.Models;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class DeploymentRunnerTests
{
    private static DeploymentScript Script(string v) =>
        new($@"C:\scripts\{v}_x.sql", v, IsRollback: false);

    [Fact]
    public async Task Reports_no_pending_scripts_when_none()
    {
        var fake = new FakeSqlDeployer { Pending = new() };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.True(result.NoPendingScripts);
        Assert.Equal(0, result.SucceededCount);
    }

    [Fact]
    public async Task Runs_all_scripts_and_counts_successes()
    {
        var fake = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(new[] { "001", "002" }, fake.Executed.ToArray());
    }

    [Fact]
    public async Task A_failing_script_is_counted_and_run_continues()
    {
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            FailingVersions = { "002" }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(3, fake.Executed.Count); // did not stop at the failure
    }

    [Fact]
    public async Task Reports_progress_per_script()
    {
        var fake = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var runner = new DeploymentRunner(fake);
        var updates = new List<DeploymentProgress>();
        var progress = new Progress<DeploymentProgress>(updates.Add);

        await runner.RunAsync("cs", "path", "GUI", progress, CancellationToken.None);

        // Progress is delivered on the captured sync context; give it a beat.
        await Task.Delay(50);
        Assert.Contains(updates, u => u.FileName.Contains("001") && u.Success == true);
        Assert.Contains(updates, u => u.FileName.Contains("002") && u.Success == true);
    }

    [Fact]
    public async Task Cancellation_stops_the_run_and_sets_flag()
    {
        var cts = new CancellationTokenSource();
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002") },
            OnExecute = _ => { cts.Cancel(); return Task.CompletedTask; }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(1, fake.Executed.Count); // stopped after the first
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter DeploymentRunnerTests`
Expected: FAIL — `DeploymentRunner` does not exist.

- [ ] **Step 4: Implement DeploymentRunner**

Create `SqlDeployer.Core/Services/DeploymentRunner.cs`:
```csharp
using System.IO;
using SqlDeployer.Models;

namespace SqlDeployer.Services;

// Orchestrates a deployment run over ISqlDeployer: fetch pending scripts,
// execute each inside its own transaction (handled by the backend), report
// progress, count outcomes, and honor cancellation. Mirrors the original
// Form1.btnStartDeployment_Click loop, minus all UI concerns.
public class DeploymentRunner
{
    private readonly ISqlDeployer _deployer;

    public DeploymentRunner(ISqlDeployer deployer) => _deployer = deployer;

    public async Task<DeploymentResult> RunAsync(
        string connectionString,
        string scriptsPath,
        string environment,
        IProgress<DeploymentProgress> progress,
        CancellationToken cancellationToken)
    {
        var pending = await _deployer.GetPendingScripts(
            scriptsPath, environment, connectionString, cancellationToken);

        if (pending.Count == 0)
            return new DeploymentResult(0, 0, Cancelled: false, NoPendingScripts: true);

        int success = 0, failed = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);

            var script = pending[i];
            var fileName = Path.GetFileName(script.FileName);
            progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                success++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);
            }
            catch (Exception ex)
            {
                failed++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, fileName, Success: false, Error: ex.Message));
            }
        }

        return new DeploymentResult(success, failed, Cancelled: false, NoPendingScripts: false);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter DeploymentRunnerTests`
Expected: PASS — 5 tests passed.

- [ ] **Step 6: Commit**

```bash
git add SqlDeployer.Core/Services/DeploymentRunner.cs SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs SqlDeployer.Core.Tests/DeploymentRunnerTests.cs
git commit -m "feat: add DeploymentRunner orchestration with tests"
```

---

## Task 8: IDialogService abstraction + fake

**Files:**
- Create: `SqlDeployer.Core/Services/IDialogService.cs`
- Create: `SqlDeployer.Core.Tests/Fakes/FakeDialogService.cs`

- [ ] **Step 1: Create the interface**

Create `SqlDeployer.Core/Services/IDialogService.cs`:
```csharp
namespace SqlDeployer.Services;

// UI-agnostic dialog/picker abstraction so ViewModels stay testable.
// The WinUI app provides the concrete implementation (ContentDialog + FolderPicker).
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task<string?> PickFolderAsync();
}
```

- [ ] **Step 2: Create the fake for tests**

Create `SqlDeployer.Core.Tests/Fakes/FakeDialogService.cs`:
```csharp
using SqlDeployer.Services;

namespace SqlDeployer.Core.Tests.Fakes;

public class FakeDialogService : IDialogService
{
    public List<(string Title, string Message)> Messages { get; } = new();
    public string? FolderToReturn { get; set; }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<string?> PickFolderAsync() => Task.FromResult(FolderToReturn);
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build SqlDeployer.Core/SqlDeployer.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add SqlDeployer.Core/Services/IDialogService.cs SqlDeployer.Core.Tests/Fakes/FakeDialogService.cs
git commit -m "feat: add IDialogService abstraction and test fake"
```

---

## Task 9: DeployViewModel (TDD)

**Files:**
- Create: `SqlDeployer.Core/ViewModels/DeployViewModel.cs`
- Test: `SqlDeployer.Core.Tests/DeployViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/DeployViewModelTests.cs`:
```csharp
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
        return new DeployViewModel(new DeploymentRunner(deployer), dialogs, settings);
    }

    private static DeploymentScript Script(string v) => new($@"C:\s\{v}_x.sql", v, false);

    [Fact]
    public async Task Deploy_with_missing_fields_shows_validation_dialog_and_does_not_run()
    {
        var dialogs = new FakeDialogService();
        var vm = NewVm(dialogs: dialogs);
        vm.Server = "";  // missing

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

        Assert.Equal(2, vm.SuccessLog.Count);
        Assert.Empty(vm.ErrorLog);
        Assert.False(vm.IsBusy);

        // Profile persisted, password NOT written.
        var reloaded = new SettingsService(settingsPath).Load();
        Assert.Equal("localhost", reloaded.LastConnection!.Server);
        Assert.DoesNotContain("secret", File.ReadAllText(settingsPath));

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

        Assert.Single(vm.SuccessLog);
        Assert.Single(vm.ErrorLog);

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
        Assert.Equal(string.Empty, vm.Password); // never restored
        File.Delete(settingsPath);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter DeployViewModelTests`
Expected: FAIL — `DeployViewModel` does not exist.

- [ ] **Step 3: Implement DeployViewModel**

Create `SqlDeployer.Core/ViewModels/DeployViewModel.cs`:
```csharp
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

        var progress = new Progress<DeploymentProgress>(OnProgress);

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
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter DeployViewModelTests`
Expected: PASS — 7 tests passed.

> Note: the `TestConnection` "happy path" is not unit-tested because it opens a real SQL connection; it is covered by manual smoke testing in Task 17. The validation path IS tested above.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/ViewModels/DeployViewModel.cs SqlDeployer.Core.Tests/DeployViewModelTests.cs
git commit -m "feat: add DeployViewModel with tests"
```

---

## Task 10: HistoryViewModel (TDD)

**Files:**
- Create: `SqlDeployer.Core/ViewModels/HistoryViewModel.cs`
- Test: `SqlDeployer.Core.Tests/HistoryViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/HistoryViewModelTests.cs`:
```csharp
using SqlDeployer;
using SqlDeployer.Core.Tests.Fakes;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class HistoryViewModelTests
{
    [Fact]
    public async Task Refresh_loads_history_rows_from_deployer()
    {
        var fake = new FakeSqlDeployer
        {
            History =
            {
                new DeploymentHistory("001_init.sql", "001", DateTime.UtcNow, true, null),
                new DeploymentHistory("002_bad.sql", "002", DateTime.UtcNow, false, "boom")
            }
        };
        var vm = new HistoryViewModel(fake);
        vm.ConnectionString = "cs";

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, h => h.Version == "002" && !h.Success);
    }

    [Fact]
    public async Task Refresh_without_connection_string_does_nothing()
    {
        var fake = new FakeSqlDeployer { History = { new DeploymentHistory("x", "1", DateTime.UtcNow, true, null) } };
        var vm = new HistoryViewModel(fake);
        vm.ConnectionString = "";

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter HistoryViewModelTests`
Expected: FAIL — `HistoryViewModel` does not exist.

- [ ] **Step 3: Implement HistoryViewModel**

Create `SqlDeployer.Core/ViewModels/HistoryViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlDeployer;

namespace SqlDeployer.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISqlDeployer _deployer;

    [ObservableProperty] private string _connectionString = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;

    public ObservableCollection<DeploymentHistory> Items { get; } = new();

    public HistoryViewModel(ISqlDeployer deployer) => _deployer = deployer;

    [RelayCommand]
    private async Task Refresh()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Status = "Set a connection on the Deploy page first.";
            return;
        }

        IsBusy = true;
        try
        {
            var rows = await _deployer.GetDeploymentHistory(ConnectionString);
            Items.Clear();
            foreach (var row in rows) Items.Add(row);
            Status = Items.Count == 0 ? "No deployment history." : $"{Items.Count} record(s).";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter HistoryViewModelTests`
Expected: PASS — 2 tests passed.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/ViewModels/HistoryViewModel.cs SqlDeployer.Core.Tests/HistoryViewModelTests.cs
git commit -m "feat: add HistoryViewModel with tests"
```

---

## Task 11: SettingsViewModel (TDD)

**Files:**
- Create: `SqlDeployer.Core/ViewModels/SettingsViewModel.cs`
- Test: `SqlDeployer.Core.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/SettingsViewModelTests.cs`:
```csharp
using System.IO;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SettingsViewModelTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "sqldeploy_sv_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Loads_saved_theme_on_construction()
    {
        var path = TempPath();
        var settings = new SettingsService(path);
        settings.Save(new SqlDeployer.Models.AppSettings { Theme = "Dark" });

        var vm = new SettingsViewModel(settings);

        Assert.Equal("Dark", vm.SelectedTheme);
        File.Delete(path);
    }

    [Fact]
    public void Changing_theme_persists_and_raises_event()
    {
        var path = TempPath();
        var settings = new SettingsService(path);
        var vm = new SettingsViewModel(settings);
        string? raised = null;
        vm.ThemeChanged += (_, theme) => raised = theme;

        vm.SelectedTheme = "Light";

        Assert.Equal("Light", new SettingsService(path).Load().Theme);
        Assert.Equal("Light", raised);
        File.Delete(path);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter SettingsViewModelTests`
Expected: FAIL — `SettingsViewModel` does not exist.

- [ ] **Step 3: Implement SettingsViewModel**

Create `SqlDeployer.Core/ViewModels/SettingsViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using SqlDeployer.Services;

namespace SqlDeployer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    // Raised when the theme changes so the app shell can re-apply RequestedTheme.
    public event EventHandler<string>? ThemeChanged;

    public string[] Themes { get; } = { "Default", "Light", "Dark" };

    [ObservableProperty] private string _selectedTheme = "Default";

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _selectedTheme = _settings.Load().Theme;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var s = _settings.Load();
        s.Theme = value;
        _settings.Save(s);
        ThemeChanged?.Invoke(this, value);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter SettingsViewModelTests`
Expected: PASS — 2 tests passed.

- [ ] **Step 5: Run the FULL test suite**

Run: `dotnet test SqlDeployer.Core.Tests`
Expected: PASS — all tests green (smoke + ConnectionStringFactory + Settings + DeploymentRunner + DeployViewModel + History + SettingsViewModel).

- [ ] **Step 6: Commit**

```bash
git add SqlDeployer.Core/ViewModels/SettingsViewModel.cs SqlDeployer.Core.Tests/SettingsViewModelTests.cs
git commit -m "feat: add SettingsViewModel with tests"
```

---

## Task 12: Scaffold the WinUI 3 app project (blank, builds & runs)

**Files:**
- Create: `SqlDeployerGui/SqlDeployerGui.csproj`
- Create: `SqlDeployerGui/app.manifest`
- Create: `SqlDeployerGui/App.xaml` / `App.xaml.cs`
- Create: `SqlDeployerGui/MainWindow.xaml` / `MainWindow.xaml.cs`
- Delete: root `SqlDeployerGui.csproj`, `Form1.cs`, `Form1.Designer.cs`, `ModernUI.cs`, `Program.cs`

- [ ] **Step 1: Remove the old WinForms project files**

Delete from the repo root: `SqlDeployerGui.csproj`, `Form1.cs`, `Form1.Designer.cs`, `ModernUI.cs`, `Program.cs`. Also delete the root `bin/` and `obj/` folders.

- [ ] **Step 2: Create the WinUI csproj**

Create `SqlDeployerGui/SqlDeployerGui.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>SqlDeployerGui</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMsixTooling>true</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.7.250606001" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.4654" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SqlDeployer.Core\SqlDeployer.Core.csproj" />
  </ItemGroup>

</Project>
```
> If NuGet restore reports those exact versions are unavailable, run
> `dotnet add SqlDeployerGui package Microsoft.WindowsAppSDK` and
> `dotnet add SqlDeployerGui package Microsoft.Windows.SDK.BuildTools`
> to pull the current stable versions, then continue.

- [ ] **Step 3: Create the app manifest (DPI + supported OS)**

Create `SqlDeployerGui/app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="SqlDeployerGui.app"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 4: Create App.xaml**

Create `SqlDeployerGui/App.xaml`:
```xml
<Application
    x:Class="SqlDeployerGui.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 5: Create App.xaml.cs (minimal, just opens the window for now)**

Create `SqlDeployerGui/App.xaml.cs`:
```csharp
using Microsoft.UI.Xaml;

namespace SqlDeployerGui;

public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
```

- [ ] **Step 6: Create a placeholder MainWindow**

Create `SqlDeployerGui/MainWindow.xaml`:
```xml
<Window
    x:Class="SqlDeployerGui.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <TextBlock Text="SQL Deploy — WinUI 3"
                   HorizontalAlignment="Center" VerticalAlignment="Center"
                   FontSize="24" />
    </Grid>
</Window>
```

Create `SqlDeployerGui/MainWindow.xaml.cs`:
```csharp
using Microsoft.UI.Xaml;

namespace SqlDeployerGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "SQL Deploy — Migration Console";
    }
}
```

- [ ] **Step 7: Add the app to the solution and build**

Run:
```bash
dotnet sln add SqlDeployerGui/SqlDeployerGui.csproj
dotnet build SqlDeployerGui/SqlDeployerGui.csproj -r win-x64
```
Expected: Build succeeded. (First restore may take a while pulling the Windows App SDK.)

- [ ] **Step 8: Run the blank app to confirm WinUI works end-to-end**

Run: `dotnet run --project SqlDeployerGui/SqlDeployerGui.csproj -r win-x64`
Expected: A window opens showing "SQL Deploy — WinUI 3". Close it.

> This is the critical environment checkpoint. Do not proceed until the blank WinUI window runs. If it fails, resolve the Windows App SDK / self-contained setup before continuing.

- [ ] **Step 9: Commit**

```bash
git add SqlDeployerGui SqlDeploy.sln
git rm SqlDeployerGui.csproj Form1.cs Form1.Designer.cs ModernUI.cs Program.cs
git commit -m "feat: scaffold WinUI 3 app project; remove WinForms files"
```

---

## Task 13: Converters + DialogService (WinUI services)

**Files:**
- Create: `SqlDeployerGui/Converters/LogKindToBrushConverter.cs`
- Create: `SqlDeployerGui/Converters/BoolNegationConverter.cs`
- Create: `SqlDeployerGui/Services/DialogService.cs`

- [ ] **Step 1: Create LogKindToBrushConverter**

Create `SqlDeployerGui/Converters/LogKindToBrushConverter.cs`:
```csharp
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SqlDeployer.Models;
using Windows.UI;

namespace SqlDeployerGui.Converters;

public class LogKindToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = new(Color.FromArgb(255, 78, 201, 130));
    private static readonly SolidColorBrush Error = new(Color.FromArgb(255, 232, 106, 102));
    private static readonly SolidColorBrush Info = new(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is LogKind k
            ? k switch { LogKind.Success => Success, LogKind.Error => Error, _ => Info }
            : Info;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Create BoolNegationConverter (for enabling Cancel = IsBusy, disabling form = IsBusy)**

Create `SqlDeployerGui/Converters/BoolNegationConverter.cs`:
```csharp
using Microsoft.UI.Xaml.Data;

namespace SqlDeployerGui.Converters;

public class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;
}
```

- [ ] **Step 3: Implement DialogService (ContentDialog + FolderPicker, wired to the window HWND)**

Create `SqlDeployerGui/Services/DialogService.cs`:
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlDeployer.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SqlDeployerGui.Services;

// Concrete IDialogService for WinUI. Folder picking in an UNPACKAGED app must
// be associated with the window HWND via InitializeWithWindow.
public class DialogService : IDialogService
{
    private readonly Window _window;

    public DialogService(Window window) => _window = window;

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = _window.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
```

- [ ] **Step 4: Build to verify the WinUI services compile**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -r win-x64`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployerGui/Converters SqlDeployerGui/Services
git commit -m "feat: add WinUI converters and DialogService"
```

---

## Task 14: Compose services + app theme in App.xaml.cs

**Files:**
- Modify: `SqlDeployerGui/App.xaml.cs`

- [ ] **Step 1: Wire up a simple service container and shared ViewModels**

Overwrite `SqlDeployerGui/App.xaml.cs`:
```csharp
using Microsoft.UI.Xaml;
using SqlDeployer;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using SqlDeployerGui.Services;

namespace SqlDeployerGui;

public partial class App : Application
{
    public static MainWindow Window { get; private set; } = null!;

    // Shared singletons reused by every page.
    public static SettingsService Settings { get; private set; } = null!;
    public static DeployViewModel Deploy { get; private set; } = null!;
    public static HistoryViewModel History { get; private set; } = null!;
    public static SettingsViewModel SettingsVm { get; private set; } = null!;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();

        var deployer = new SqlServerDeployer();
        var dialogs = new DialogService(Window);
        Settings = SettingsService.Default();

        Deploy = new DeployViewModel(new DeploymentRunner(deployer), dialogs, Settings);
        History = new HistoryViewModel(deployer);
        SettingsVm = new SettingsViewModel(Settings);

        // Re-apply theme whenever the user changes it on the Settings page.
        SettingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

        Window.Activate();
        ApplyTheme(SettingsVm.SelectedTheme);
    }

    public static void ApplyTheme(string theme)
    {
        if (Window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -r win-x64`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add SqlDeployerGui/App.xaml.cs
git commit -m "feat: compose services and ViewModels at app startup with theme application"
```

---

## Task 15: MainWindow shell — NavigationView + Mica + custom title bar

**Files:**
- Modify: `SqlDeployerGui/MainWindow.xaml` / `MainWindow.xaml.cs`

- [ ] **Step 1: Replace MainWindow.xaml with the nav shell**

Overwrite `SqlDeployerGui/MainWindow.xaml`:
```xml
<Window
    x:Class="SqlDeployerGui.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:muxc="using:Microsoft.UI.Xaml.Controls">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Custom draggable title bar -->
        <Grid x:Name="AppTitleBar" Grid.Row="0" Height="40">
            <TextBlock Text="SQL Deploy — Migration Console"
                       VerticalAlignment="Center" Margin="16,0,0,0"
                       Style="{StaticResource CaptionTextBlockStyle}" />
        </Grid>

        <muxc:NavigationView x:Name="Nav" Grid.Row="1"
                             IsBackButtonVisible="Collapsed"
                             IsSettingsVisible="False"
                             SelectionChanged="Nav_SelectionChanged"
                             PaneDisplayMode="Left">
            <muxc:NavigationView.MenuItems>
                <muxc:NavigationViewItem Content="Deploy" Tag="Deploy" IsSelected="True">
                    <muxc:NavigationViewItem.Icon>
                        <SymbolIcon Symbol="Upload" />
                    </muxc:NavigationViewItem.Icon>
                </muxc:NavigationViewItem>
                <muxc:NavigationViewItem Content="History" Tag="History">
                    <muxc:NavigationViewItem.Icon>
                        <SymbolIcon Symbol="Clock" />
                    </muxc:NavigationViewItem.Icon>
                </muxc:NavigationViewItem>
                <muxc:NavigationViewItem Content="Settings" Tag="Settings">
                    <muxc:NavigationViewItem.Icon>
                        <SymbolIcon Symbol="Setting" />
                    </muxc:NavigationViewItem.Icon>
                </muxc:NavigationViewItem>
            </muxc:NavigationView.MenuItems>

            <Frame x:Name="ContentFrame" />
        </muxc:NavigationView>
    </Grid>
</Window>
```

- [ ] **Step 2: Replace MainWindow.xaml.cs with Mica, title bar, and navigation**

Overwrite `SqlDeployerGui/MainWindow.xaml.cs`:
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SqlDeployerGui.Views;

namespace SqlDeployerGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "SQL Deploy — Migration Console";
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ContentFrame.Navigate(typeof(DeployPage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag as string)
            {
                case "Deploy": ContentFrame.Navigate(typeof(DeployPage)); break;
                case "History": ContentFrame.Navigate(typeof(HistoryPage)); break;
                case "Settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
            }
        }
    }
}
```

> This references `DeployPage`, `HistoryPage`, `SettingsPage`, created in Tasks 16–18. The project will not build until those exist; that is expected. Build verification happens at the end of Task 18.

- [ ] **Step 3: Commit (work-in-progress shell)**

```bash
git add SqlDeployerGui/MainWindow.xaml SqlDeployerGui/MainWindow.xaml.cs
git commit -m "feat: NavigationView shell with Mica and custom title bar"
```

---

## Task 16: DeployPage

**Files:**
- Create: `SqlDeployerGui/Views/DeployPage.xaml` / `DeployPage.xaml.cs`

- [ ] **Step 1: Create DeployPage.xaml**

Create `SqlDeployerGui/Views/DeployPage.xaml`:
```xml
<Page
    x:Class="SqlDeployerGui.Views.DeployPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
    xmlns:conv="using:SqlDeployerGui.Converters"
    xmlns:models="using:SqlDeployer.Models">

    <Page.Resources>
        <conv:BoolNegationConverter x:Key="Not" />
        <conv:LogKindToBrushConverter x:Key="LogBrush" />
    </Page.Resources>

    <ScrollViewer Padding="24">
        <StackPanel Spacing="16" MaxWidth="720" HorizontalAlignment="Left">

            <TextBlock Text="Deploy" Style="{StaticResource TitleTextBlockStyle}" />
            <TextBlock Text="Database migration console"
                       Style="{StaticResource BodyTextBlockStyle}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

            <!-- Connection card -->
            <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    BorderThickness="1" CornerRadius="8" Padding="20">
                <StackPanel Spacing="12" IsEnabled="{Binding IsBusy, Converter={StaticResource Not}}">
                    <TextBlock Text="CONNECTION" Style="{StaticResource CaptionTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

                    <TextBox Header="Server" PlaceholderText="localhost\SQLEXPRESS"
                             Text="{Binding Server, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Login" PlaceholderText="Leave blank for Windows auth"
                             Text="{Binding Login, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <PasswordBox Header="Password" PlaceholderText="Enter password"
                                 Password="{Binding Password, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Database" PlaceholderText="Target database name"
                             Text="{Binding Database, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

                    <Grid ColumnSpacing="8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBox Grid.Column="0" Header="Script path"
                                 PlaceholderText="Folder with .sql migration scripts"
                                 Text="{Binding ScriptPath, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <Button Grid.Column="1" Content="Browse" VerticalAlignment="Bottom"
                                Command="{Binding BrowseCommand}" />
                    </Grid>
                </StackPanel>
            </Border>

            <!-- Action buttons -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="Test Connection" Command="{Binding TestConnectionCommand}"
                        IsEnabled="{Binding IsBusy, Converter={StaticResource Not}}" />
                <Button Content="Deploy" Style="{StaticResource AccentButtonStyle}"
                        Command="{Binding DeployCommand}"
                        IsEnabled="{Binding IsBusy, Converter={StaticResource Not}}" />
                <Button Content="Cancel" Command="{Binding CancelCommand}"
                        IsEnabled="{Binding IsBusy}" />
            </StackPanel>

            <!-- Progress + status -->
            <muxc:ProgressBar Value="{Binding ProgressValue}" Maximum="{Binding ProgressMax}" />
            <TextBlock Text="{Binding Status}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

            <!-- Output logs -->
            <TextBlock Text="OUTPUT" Style="{StaticResource CaptionTextBlockStyle}"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
            <muxc:Pivot>
                <muxc:PivotItem>
                    <muxc:PivotItem.Header>
                        <TextBlock>
                            <Run Text="Success (" /><Run Text="{Binding SuccessLog.Count}" /><Run Text=")" />
                        </TextBlock>
                    </muxc:PivotItem.Header>
                    <ListView ItemsSource="{Binding SuccessLog}" Height="160"
                              SelectionMode="None">
                        <ListView.ItemTemplate>
                            <DataTemplate x:DataType="models:LogEntry">
                                <TextBlock Text="{x:Bind Message}" FontFamily="Consolas"
                                           Foreground="{Binding Kind, Converter={StaticResource LogBrush}}"
                                           TextWrapping="Wrap" />
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                </muxc:PivotItem>
                <muxc:PivotItem>
                    <muxc:PivotItem.Header>
                        <TextBlock>
                            <Run Text="Errors (" /><Run Text="{Binding ErrorLog.Count}" /><Run Text=")" />
                        </TextBlock>
                    </muxc:PivotItem.Header>
                    <ListView ItemsSource="{Binding ErrorLog}" Height="160"
                              SelectionMode="None">
                        <ListView.ItemTemplate>
                            <DataTemplate x:DataType="models:LogEntry">
                                <TextBlock Text="{x:Bind Message}" FontFamily="Consolas"
                                           Foreground="{Binding Kind, Converter={StaticResource LogBrush}}"
                                           TextWrapping="Wrap" />
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                </muxc:PivotItem>
            </muxc:Pivot>

        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 2: Create DeployPage.xaml.cs (binds to the shared DeployViewModel)**

Create `SqlDeployerGui/Views/DeployPage.xaml.cs`:
```csharp
using Microsoft.UI.Xaml.Controls;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class DeployPage : Page
{
    public DeployViewModel Vm => App.Deploy;

    public DeployPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }
}
```

> Note: `x:Bind` for `LogEntry` uses `{x:Bind Message}` (compiled), while collection
> and VM bindings use classic `{Binding}` against `DataContext`. The `LogBrush`
> converter uses `{Binding Kind}` because `Kind` is an enum on the data item.

- [ ] **Step 3: Commit**

```bash
git add SqlDeployerGui/Views/DeployPage.xaml SqlDeployerGui/Views/DeployPage.xaml.cs
git commit -m "feat: add DeployPage view"
```

---

## Task 17: HistoryPage and SettingsPage

**Files:**
- Create: `SqlDeployerGui/Views/HistoryPage.xaml` / `.cs`
- Create: `SqlDeployerGui/Views/SettingsPage.xaml` / `.cs`

- [ ] **Step 1: Create HistoryPage.xaml**

Create `SqlDeployerGui/Views/HistoryPage.xaml`:
```xml
<Page
    x:Class="SqlDeployerGui.Views.HistoryPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
    xmlns:sql="using:SqlDeployer">

    <Grid Padding="24" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="12">
            <TextBlock Text="History" Style="{StaticResource TitleTextBlockStyle}" />
            <Button Content="Refresh" Command="{Binding RefreshCommand}" VerticalAlignment="Center" />
        </StackPanel>

        <TextBlock Grid.Row="1" Text="{Binding Status}"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

        <ListView Grid.Row="2" ItemsSource="{Binding Items}" SelectionMode="None">
            <ListView.HeaderTemplate>
                <DataTemplate>
                    <Grid Padding="8" ColumnSpacing="12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="2*" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="2*" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="Script" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="Version" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="Deployed (UTC)" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="3" Text="Status" FontWeight="SemiBold" />
                    </Grid>
                </DataTemplate>
            </ListView.HeaderTemplate>
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="sql:DeploymentHistory">
                    <Grid Padding="8" ColumnSpacing="12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="2*" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="2*" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind ScriptName}" TextWrapping="Wrap" />
                        <TextBlock Grid.Column="1" Text="{x:Bind Version}" />
                        <TextBlock Grid.Column="2" Text="{x:Bind DeployedAt}" />
                        <TextBlock Grid.Column="3" Text="{x:Bind Success}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 2: Create HistoryPage.xaml.cs (seed connection string from Deploy VM, then refresh)**

Create `SqlDeployerGui/Views/HistoryPage.xaml.cs`:
```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel Vm => App.History;

    public HistoryPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Use whatever connection the Deploy page currently has.
        var d = App.Deploy;
        if (!string.IsNullOrWhiteSpace(d.Server) && !string.IsNullOrWhiteSpace(d.Database))
        {
            Vm.ConnectionString = ConnectionStringFactory.Build(d.Server, d.Login, d.Password, d.Database);
            await Vm.RefreshCommand.ExecuteAsync(null);
        }
    }
}
```

- [ ] **Step 3: Create SettingsPage.xaml**

Create `SqlDeployerGui/Views/SettingsPage.xaml`:
```xml
<Page
    x:Class="SqlDeployerGui.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <StackPanel Padding="24" Spacing="16" MaxWidth="520" HorizontalAlignment="Left">
        <TextBlock Text="Settings" Style="{StaticResource TitleTextBlockStyle}" />

        <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                BorderThickness="1" CornerRadius="8" Padding="20">
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" Style="{StaticResource SubtitleTextBlockStyle}" />
                <ComboBox Header="Theme" Width="240"
                          ItemsSource="{Binding Themes}"
                          SelectedItem="{Binding SelectedTheme, Mode=TwoWay}" />
                <TextBlock Text="Connection details (except password) are remembered between sessions. Passwords are never saved."
                           Style="{StaticResource CaptionTextBlockStyle}"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                           TextWrapping="Wrap" />
            </StackPanel>
        </Border>
    </StackPanel>
</Page>
```

- [ ] **Step 4: Create SettingsPage.xaml.cs**

Create `SqlDeployerGui/Views/SettingsPage.xaml.cs`:
```csharp
using Microsoft.UI.Xaml.Controls;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm => App.SettingsVm;

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }
}
```

- [ ] **Step 5: Build the whole app (all pages now exist)**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -r win-x64`
Expected: Build succeeded — `MainWindow` now resolves `DeployPage`, `HistoryPage`, `SettingsPage`.

- [ ] **Step 6: Commit**

```bash
git add SqlDeployerGui/Views/HistoryPage.xaml SqlDeployerGui/Views/HistoryPage.xaml.cs SqlDeployerGui/Views/SettingsPage.xaml SqlDeployerGui/Views/SettingsPage.xaml.cs
git commit -m "feat: add HistoryPage and SettingsPage views"
```

---

## Task 18: Full run, manual smoke test, and self-contained publish

**Files:** none (verification + publish)

- [ ] **Step 1: Run the full automated test suite one more time**

Run: `dotnet test SqlDeployer.Core.Tests`
Expected: PASS — all Core tests green.

- [ ] **Step 2: Run the app**

Run: `dotnet run --project SqlDeployerGui/SqlDeployerGui.csproj -r win-x64`
Expected: The window opens with the nav rail (Deploy / History / Settings), Mica backdrop, and a custom title bar.

- [ ] **Step 3: Manual smoke checklist (against a real SQL Server)**

Verify each:
- Deploy page shows; saved connection (if any) is pre-filled, password blank.
- **Test Connection** with a bad server → error `ContentDialog`. With a good server → success dialog.
- **Browse** opens a folder picker; chosen path lands in Script path.
- **Deploy** with empty Server/Database/Script path → validation dialog, nothing runs.
- **Deploy** against a folder of `.sql` scripts → progress bar advances, status updates per script, Success tab fills, count in the tab header increments.
- Introduce a deliberately broken `.sql` → it lands in the **Errors** tab; the run continues to the next script.
- **Cancel** mid-run → status shows "cancelling"/"cancelled"; run stops.
- Switch to **History** → past deployments listed (script, version, date, success).
- **Settings** → change Theme to Light/Dark → whole app + title bar re-themes immediately; restart the app → theme persists.
- Restart the app → last connection (minus password) is restored.

- [ ] **Step 4: Publish the self-contained, unpackaged single .exe**

Run:
```bash
dotnet publish SqlDeployerGui/SqlDeployerGui.csproj -c Release -r win-x64 -o publish
```
Expected: `publish/` contains `SqlDeployerGui.exe` plus the Windows App SDK self-contained runtime files. Confirm `publish/SqlDeployerGui.exe` launches by double-clicking it (no install, no admin).

> A small folder of runtime files alongside the `.exe` is expected and acceptable for an unpackaged self-contained WinUI 3 app (this differs from the old `PublishSingleFile` single-binary output, which WinUI does not support the same way).

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "chore: verify full app, manual smoke pass, self-contained publish"
```

---

## Self-Review Notes (for the implementer)

- **Spec coverage:** Unpackaged self-contained (Task 12, 18) · modernize-freely nav pages (Tasks 15–17) · left nav rail (Task 15) · save-all-except-password (Tasks 5, 6, 9) · MVVM (Tasks 9–11) · history view surfacing `GetDeploymentHistory` (Tasks 10, 17) · persisted last connection (Tasks 6, 9, 16) · all original deploy behavior preserved via reused `SqlServerDeployer` + `DeploymentRunner` mirroring the old loop (Tasks 2, 7).
- **Behavior parity:** version ordering, skip-deployed, `GO` batching, per-script transaction, audit table, Windows-vs-SQL auth all live in the **unchanged** `SqlServerDeployer`.
- **Known manual-only areas:** XAML views and the `TestConnection`/publish steps are verified by the Task 18 smoke checklist, not unit tests (WinUI views aren't unit-tested; live SQL isn't mocked for the happy path).
- **Version pinning risk:** the `Microsoft.WindowsAppSDK` / `Microsoft.Windows.SDK.BuildTools` versions in Task 12 may need bumping to the current stable build; the step includes the `dotnet add package` fallback.
