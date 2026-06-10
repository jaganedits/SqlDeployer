# Pending List & Update Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show which scripts failed / were never run after a deploy (live "Pending (N)" list), and notify portable/dev users when a newer GitHub release exists.

**Architecture:** Feature 1 extends `DeploymentRunner` to report its plan up front and per-file outcomes in `DeploymentResult`; `DeployViewModel` keeps a `PendingLog` that fills from the plan and drains on success; the Deploy page gets a third pivot tab. Feature 2 adds pure tag-compare logic in Core (`UpdateCheck`, testable) and a GitHub-API fallback in `UpdateService` when Velopack's `IsInstalled` is false; the MainWindow banner gets a dual mode (Restart vs Download).

**Tech Stack:** .NET 10, WinUI 3, CommunityToolkit.Mvvm, Velopack, xUnit. Tests live in `SqlDeployer.Core.Tests` (references Core only — that's why update-compare logic goes in Core).

**Spec:** `docs/superpowers/specs/2026-06-10-pending-list-and-update-visibility-design.md`

**Branch:** `feat/pending-list-and-update-visibility` (already created)

**Test command:** `dotnet test SqlDeployer.Core.Tests` (run from repo root `E:\Jagan\SqlDeployer-master`)

---

### Task 1: DeploymentResult outcome lists + runner population

**Files:**
- Modify: `SqlDeployer.Core/Models/DeploymentResult.cs`
- Modify: `SqlDeployer.Core/Services/DeploymentRunner.cs`
- Test: `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs` (inside the existing `DeploymentRunnerTests` class, before the closing brace):

```csharp
    [Fact]
    public async Task Result_lists_succeeded_and_failed_script_ids()
    {
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            FailingVersions = { "002" }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(new[] { "001", "003" }, result.Succeeded);
        Assert.Equal(new[] { "002" }, result.Failed);
        Assert.Empty(result.NotRun);
    }

    [Fact]
    public async Task Cancellation_lists_not_run_scripts()
    {
        var cts = new CancellationTokenSource();
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            // Cancels while 001 executes: 001 completes, 002/003 never run.
            OnExecute = _ => { cts.Cancel(); return Task.CompletedTask; }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(new[] { "001" }, result.Succeeded);
        Assert.Empty(result.Failed);
        Assert.Equal(new[] { "002", "003" }, result.NotRun);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeploymentRunnerTests" 2>&1 | Select-Object -Last 20`
Expected: compile error — `DeploymentResult` has no `Succeeded`/`Failed`/`NotRun`.

- [ ] **Step 3: Extend DeploymentResult**

Replace the whole content of `SqlDeployer.Core/Models/DeploymentResult.cs` with:

```csharp
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
```

- [ ] **Step 4: Populate the lists in DeploymentRunner**

In `SqlDeployer.Core/Services/DeploymentRunner.cs`, replace the body of `RunAsync` (keep the signature) with:

```csharp
        var pending = await _deployer.GetPendingScripts(
            scriptsPath, environment, connectionString, cancellationToken,
            includeDeployed: force, autoOrder: autoOrder);

        if (pending.Count == 0)
            return new DeploymentResult(0, 0, Cancelled: false, NoPendingScripts: true);

        var planIds = pending.Select(p => p.Version).ToList();
        var succeeded = new List<string>();
        var failed = new List<string>();

        // NotRun = planned but neither succeeded nor failed (only on early stop).
        DeploymentResult Finish(bool cancelled) =>
            new(succeeded.Count, failed.Count, cancelled, NoPendingScripts: false)
            {
                Succeeded = succeeded,
                Failed = failed,
                NotRun = planIds.Where(id => !succeeded.Contains(id) && !failed.Contains(id)).ToList()
            };

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Finish(cancelled: true);

            var script = pending[i];
            var displayName = script.Version; // relative-path identity, phase-qualified

            progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                succeeded.Add(script.Version);
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return Finish(cancelled: true);
            }
            catch (Exception ex)
            {
                failed.Add(script.Version);
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: false, Error: ex.Message));
                // Continue past failures: run everything that can run and collect every
                // error, so the user sees all failures at once, then fixes and re-runs.
                // Scripts are idempotent (IF NOT EXISTS) and FK-ordered, so this is safe.
            }
        }

        return Finish(cancelled: false);
```

Note: the old `int success = 0, failed = 0;` counters are replaced by the lists; counts come from `succeeded.Count` / `failed.Count`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeploymentRunnerTests" 2>&1 | Select-Object -Last 10`
Expected: all DeploymentRunnerTests PASS (including the 5 pre-existing ones).

- [ ] **Step 6: Commit**

```powershell
git add SqlDeployer.Core/Models/DeploymentResult.cs SqlDeployer.Core/Services/DeploymentRunner.cs SqlDeployer.Core.Tests/DeploymentRunnerTests.cs
git commit -m "feat(runner): report succeeded/failed/not-run script ids in DeploymentResult"
```

---

### Task 2: Plan report in DeploymentProgress

**Files:**
- Modify: `SqlDeployer.Core/Models/DeploymentProgress.cs`
- Modify: `SqlDeployer.Core/Services/DeploymentRunner.cs` (RunAsync, right after the `planIds` line from Task 1)
- Test: `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `DeploymentRunnerTests`:

```csharp
    [Fact]
    public async Task First_progress_report_carries_the_full_plan()
    {
        var fake = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var runner = new DeploymentRunner(fake);
        var updates = new List<DeploymentProgress>();
        // Synchronous capture (Progress<T> posts async; a plain lambda keeps order).
        var progress = new SyncCapture(updates);

        await runner.RunAsync("cs", "path", "GUI", progress, CancellationToken.None);

        Assert.NotNull(updates[0].Plan);
        Assert.Equal(new[] { "001", "002" }, updates[0].Plan!);
        Assert.All(updates.Skip(1), u => Assert.Null(u.Plan));
    }

    private sealed class SyncCapture : IProgress<DeploymentProgress>
    {
        private readonly List<DeploymentProgress> _sink;
        public SyncCapture(List<DeploymentProgress> sink) => _sink = sink;
        public void Report(DeploymentProgress value) => _sink.Add(value);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~First_progress_report" 2>&1 | Select-Object -Last 20`
Expected: compile error — `DeploymentProgress` has no `Plan`.

- [ ] **Step 3: Extend DeploymentProgress and emit the plan**

Replace the whole content of `SqlDeployer.Core/Models/DeploymentProgress.cs` with:

```csharp
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
```

In `DeploymentRunner.RunAsync`, directly after `var planIds = ...;` (before the `succeeded`/`failed` lists), add:

```csharp
        progress.Report(new DeploymentProgress(0, pending.Count, string.Empty, Plan: planIds));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests 2>&1 | Select-Object -Last 10`
Expected: full suite PASS (the plan report must not break existing progress tests or `DeployViewModelTests` — the VM's `OnProgress` currently treats a `Success == null` report as "starting", which only sets Status; harmless for the plan report until Task 3 handles it explicitly).

- [ ] **Step 5: Commit**

```powershell
git add SqlDeployer.Core/Models/DeploymentProgress.cs SqlDeployer.Core/Services/DeploymentRunner.cs SqlDeployer.Core.Tests/DeploymentRunnerTests.cs
git commit -m "feat(runner): emit the full script plan as the first progress report"
```

---

### Task 3: DeployViewModel PendingLog

**Files:**
- Modify: `SqlDeployer.Core/ViewModels/DeployViewModel.cs`
- Modify: `SqlDeployer.Core/SqlServerDeployer.cs:67` (make `IsRollbackScript` public)
- Modify: `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs` (add a throw hook for the abort test)
- Test: `SqlDeployer.Core.Tests/DeployViewModelTests.cs`

- [ ] **Step 1: Add the throw hook to FakeSqlDeployer**

In `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs`, add a property next to `GetDatabasesError`:

```csharp
    public Exception? GetPendingError { get; set; }
```

and at the top of `GetPendingScripts` (first line of the method body):

```csharp
        if (GetPendingError is not null) throw GetPendingError;
```

- [ ] **Step 2: Write the failing tests**

Append to `DeployViewModelTests` (the class already has the `NewVm` and `Script` helpers shown at the top of the file):

```csharp
    [Fact]
    public async Task Pending_log_drains_to_empty_on_full_success()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Empty(vm.PendingLog);
        Assert.Equal("Pending (0)", vm.PendingHeader);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Failed_scripts_remain_in_pending_log()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            FailingVersions = { "002" }
        };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        var entry = Assert.Single(vm.PendingLog);
        Assert.Equal("002", entry.Message);
        Assert.Equal("Pending (1)", vm.PendingHeader);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Aborted_deploy_fills_pending_log_from_the_discovered_plan()
    {
        // Real .sql files on disk so the plan preview can discover them; the deploy
        // itself aborts before running (GetPendingScripts throws).
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(tempDir, "001_a.sql"), "CREATE TABLE dbo.A (Id INT);");
        File.WriteAllText(Path.Combine(tempDir, "002_b.sql"), "CREATE TABLE dbo.B (Id INT);");
        var deployer = new FakeSqlDeployer
        {
            GetPendingError = new InvalidOperationException("Foreign-key cycle detected among: x")
        };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.PendingLog.Count);
        Assert.Contains(vm.PendingLog, e => e.Message == "001_a.sql");
        Assert.Contains(vm.PendingLog, e => e.Message == "002_b.sql");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Pending_log_is_cleared_between_runs()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer
        {
            Pending = { Script("001") },
            FailingVersions = { "001" }
        };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);
        Assert.Single(vm.PendingLog);

        // Second run succeeds: the stale pending entry must not linger.
        deployer.FailingVersions.Clear();
        await vm.DeployCommand.ExecuteAsync(null);
        Assert.Empty(vm.PendingLog);

        Directory.Delete(tempDir, true);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeployViewModelTests" 2>&1 | Select-Object -Last 20`
Expected: compile error — `DeployViewModel` has no `PendingLog`/`PendingHeader`.

- [ ] **Step 4: Implement PendingLog in DeployViewModel**

In `SqlDeployer.Core/SqlServerDeployer.cs`, change the access modifier of `IsRollbackScript` (line 67) from `private` to `public`:

```csharp
    public static bool IsRollbackScript(string id) =>
        Path.GetFileNameWithoutExtension(id).EndsWith("_rollback", StringComparison.OrdinalIgnoreCase);
```

In `SqlDeployer.Core/ViewModels/DeployViewModel.cs`:

(a) Next to the existing `SuccessLog`/`ErrorLog` declarations, add:

```csharp
    // Scripts planned for the current/last run that are not deployed yet: fills
    // from the runner's plan report, drains as scripts succeed. After a run, what
    // remains is exactly the failed + never-attempted files (still re-runnable).
    public ObservableCollection<LogEntry> PendingLog { get; } = new();
```

(b) Next to `SuccessHeader`/`ErrorHeader`, add:

```csharp
    public string PendingHeader => $"Pending ({PendingLog.Count})";
```

(c) In the constructor, next to the existing `SuccessLog.CollectionChanged` wiring, add:

```csharp
        PendingLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PendingHeader));
```

(d) In `Deploy()`, add `PendingLog.Clear();` directly after `ErrorLog.Clear();`.

(e) In `Deploy()`, change the `result.Cancelled` branch's status line to include the remaining count:

```csharp
                Status = $"Deployment cancelled — {PendingLog.Count} script(s) not run.";
```

(f) In `OnProgress`, handle the plan report first (insert at the top of the method):

```csharp
        if (p.Plan is not null)
        {
            PendingLog.Clear();
            foreach (var id in p.Plan)
                PendingLog.Add(new LogEntry(id, LogKind.Info));
            ProgressMax = p.Total;
            return;
        }
```

and in the `p.Success == true` branch, drain the matching pending entry (before the `SuccessLog.Add` line):

```csharp
            var pendingEntry = PendingLog.FirstOrDefault(x => x.Message == p.FileName);
            if (pendingEntry is not null) PendingLog.Remove(pendingEntry);
```

(g) In `LogPlanPreview()`, prefill `PendingLog` so an abort before the runner's plan report still shows what would have run. After the `if (plan.Order.Count == 0) return;` line, add:

```csharp
            // Best-effort prefill: replaced by the runner's accurate plan report
            // (which also excludes already-deployed scripts) moments later, but it
            // survives when the deploy aborts before running (cycle, bad folder).
            PendingLog.Clear();
            foreach (var n in plan.Order)
                if (!SqlServerDeployer.IsRollbackScript(n.Id))
                    PendingLog.Add(new LogEntry(n.Id, LogKind.Info));
```

Note: `System.Linq` is already imported transitively via implicit usings; `FirstOrDefault` works on `ObservableCollection`.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test SqlDeployer.Core.Tests 2>&1 | Select-Object -Last 10`
Expected: PASS. Watch for `Successful_deploy_populates_success_log_and_persists_profile` — it must still pass (PendingLog drains to empty there).

- [ ] **Step 6: Commit**

```powershell
git add SqlDeployer.Core/ViewModels/DeployViewModel.cs SqlDeployer.Core/SqlServerDeployer.cs SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs SqlDeployer.Core.Tests/DeployViewModelTests.cs
git commit -m "feat(vm): live PendingLog of not-yet-deployed scripts with abort prefill"
```

---

### Task 4: Pending tab on the Deploy page

**Files:**
- Modify: `SqlDeployerGui/Views/DeployPage.xaml` (add a third PivotItem after the Errors item, lines 253-279)
- Modify: `SqlDeployerGui/Views/DeployPage.xaml.cs` (CopyOutput_Click line 123-131, log scroll wiring lines 32-48)

- [ ] **Step 1: Add the Pending PivotItem**

In `SqlDeployerGui/Views/DeployPage.xaml`, after the closing `</muxc:PivotItem>` of the Errors item (line 279) and before `</muxc:Pivot>`, add:

```xml
                                <muxc:PivotItem>
                                    <muxc:PivotItem.Header>
                                        <TextBlock Text="{x:Bind Vm.PendingHeader, Mode=OneWay}" FontSize="14" />
                                    </muxc:PivotItem.Header>
                                    <ListView x:Name="PendingList" Style="{StaticResource TerminalList}"
                                              ItemsSource="{Binding PendingLog}">
                                        <ListView.ItemContainerTransitions>
                                            <TransitionCollection>
                                                <anim:AddDeleteThemeTransition />
                                                <anim:EntranceThemeTransition FromVerticalOffset="8" />
                                            </TransitionCollection>
                                        </ListView.ItemContainerTransitions>
                                        <ListView.ItemTemplate>
                                            <DataTemplate x:DataType="models:LogEntry">
                                                <StackPanel Orientation="Horizontal" Spacing="8">
                                                    <TextBlock Text="&#xE823;" FontFamily="Segoe Fluent Icons"
                                                               FontSize="11" VerticalAlignment="Top" Margin="0,3,0,0"
                                                               Foreground="{Binding Kind, Converter={StaticResource LogBrush}}" />
                                                    <TextBlock Text="{x:Bind Message}" FontFamily="Cascadia Mono, Consolas"
                                                               FontSize="13"
                                                               Foreground="{Binding Kind, Converter={StaticResource LogBrush}}"
                                                               TextWrapping="Wrap" IsTextSelectionEnabled="True" />
                                                </StackPanel>
                                            </DataTemplate>
                                        </ListView.ItemTemplate>
                                    </ListView>
                                </muxc:PivotItem>
```

(`&#xE823;` is the Segoe Fluent "Recent/clock" glyph — pending = waiting.)

- [ ] **Step 2: Include the Pending tab in Copy**

In `SqlDeployerGui/Views/DeployPage.xaml.cs`, replace the first line of `CopyOutput_Click`:

```csharp
        var entries = OutputPivot.SelectedIndex switch
        {
            1 => Vm.ErrorLog,
            2 => (IReadOnlyList<LogEntry>)Vm.PendingLog,
            _ => Vm.SuccessLog
        };
```

(`ObservableCollection<T>` implements `IReadOnlyList<T>`; the cast unifies the switch arms.)

- [ ] **Step 3: Build the GUI project**

Run: `dotnet build SqlDeployerGui -p:Platform=x64 2>&1 | Select-Object -Last 5`
Expected: Build succeeded. (If the repo's normal build needs a different platform flag, match whatever `dotnet build` at repo root does.)

- [ ] **Step 4: Commit**

```powershell
git add SqlDeployerGui/Views/DeployPage.xaml SqlDeployerGui/Views/DeployPage.xaml.cs
git commit -m "feat(ui): Pending (N) output tab listing not-yet-deployed scripts"
```

---

### Task 5: UpdateCheck — pure release-compare logic in Core

**Files:**
- Create: `SqlDeployer.Core/Services/UpdateCheck.cs`
- Test: `SqlDeployer.Core.Tests/UpdateCheckTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/UpdateCheckTests.cs`:

```csharp
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v2.3.4", "2.3.4.0")]
    [InlineData("2.3.4", "2.3.4.0")]
    [InlineData("V10.0.1", "10.0.1.0")]
    [InlineData("v2.4", "2.4.0.0")]
    public void ParseTag_accepts_common_tag_shapes(string tag, string expected)
        => Assert.Equal(Version.Parse(expected), UpdateCheck.ParseTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v2.3.4-beta")]
    public void ParseTag_rejects_non_versions(string? tag)
        => Assert.Null(UpdateCheck.ParseTag(tag));

    [Theory]
    [InlineData("2.3.3", "2.3.2", true)]
    [InlineData("2.3.2", "2.3.2", false)]
    [InlineData("2.3.1", "2.3.2", false)]
    [InlineData("2.4", "2.3.9", true)]
    public void IsNewer_compares_normalized_versions(string candidate, string current, bool expected)
        => Assert.Equal(expected,
            UpdateCheck.IsNewer(Version.Parse(candidate), Version.Parse(current)));

    [Fact]
    public void ParseLatestRelease_reads_tag_and_url()
    {
        const string json = """
            { "tag_name": "v2.3.2", "html_url": "https://github.com/jaganedits/SqlDeployer/releases/tag/v2.3.2", "name": "SqlDeployer 2.3.2" }
            """;

        var release = UpdateCheck.ParseLatestRelease(json);

        Assert.NotNull(release);
        Assert.Equal("v2.3.2", release!.Value.Tag);
        Assert.Equal("https://github.com/jaganedits/SqlDeployer/releases/tag/v2.3.2", release.Value.Url);
    }

    [Fact]
    public void ParseLatestRelease_returns_null_without_tag()
        => Assert.Null(UpdateCheck.ParseLatestRelease("""{ "message": "Not Found" }"""));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~UpdateCheckTests" 2>&1 | Select-Object -Last 20`
Expected: compile error — `UpdateCheck` does not exist.

- [ ] **Step 3: Implement UpdateCheck**

Create `SqlDeployer.Core/Services/UpdateCheck.cs`:

```csharp
using System.Text.Json;

namespace SqlDeployer.Services;

// Pure logic for the GitHub-release update fallback, kept UI- and network-free
// so it is unit-testable: parse a release tag ("v2.3.4" / "2.3.4") and decide
// whether it is newer than the running version.
public static class UpdateCheck
{
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var trimmed = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var v) ? Normalize(v) : null;
    }

    // GitHub "latest release" JSON -> (tag_name, html_url). Null when the payload
    // has no usable tag (e.g. a "Not Found" error body). Invalid JSON throws —
    // callers treat any exception as a failed check.
    public static (string Tag, string? Url)? ParseLatestRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement)) return null;
        var tag = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string? url = doc.RootElement.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString()
            : null;
        return (tag, url);
    }

    public static bool IsNewer(Version candidate, Version current)
        => Normalize(candidate) > Normalize(current);

    // Version treats missing parts as -1, which breaks comparisons (2.4 < 2.4.0);
    // normalize them to 0 so 2.4 == 2.4.0.0.
    private static Version Normalize(Version v) => new(
        v.Major, v.Minor, v.Build < 0 ? 0 : v.Build, v.Revision < 0 ? 0 : v.Revision);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~UpdateCheckTests" 2>&1 | Select-Object -Last 10`
Expected: all UpdateCheckTests PASS.

- [ ] **Step 5: Commit**

```powershell
git add SqlDeployer.Core/Services/UpdateCheck.cs SqlDeployer.Core.Tests/UpdateCheckTests.cs
git commit -m "feat(core): UpdateCheck tag-parse and version-compare helpers"
```

---

### Task 6: UpdateService GitHub-API fallback

**Files:**
- Modify: `SqlDeployerGui/Services/UpdateService.cs`

No unit tests (GUI project is not test-referenced; the logic worth testing lives in `UpdateCheck`, Task 5).

- [ ] **Step 1: Rewrite UpdateService**

Replace the whole content of `SqlDeployerGui/Services/UpdateService.cs` with:

```csharp
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
```

Note: `UpdateStatus.NotInstalled` is removed — the fallback now answers for that case. Task 8 removes its switch case in SettingsPage; the build stays red between Tasks 6 and 8 is NOT acceptable, so expect compile errors in `SettingsPage.xaml.cs` here and fix them in the SAME commit by doing Task 7 + 8 edits before committing — see Step 2.

- [ ] **Step 2: Do NOT commit yet**

`SettingsPage.xaml.cs` (switch on `UpdateStatus.NotInstalled`, call `ShowUpdateBanner(result.Version!)`) and `App.xaml.cs`/`MainWindow.xaml.cs` still reference the old shapes. Continue straight into Tasks 7 and 8; commit all three together at the end of Task 8.

---

### Task 7: Dual-mode update banner (Restart vs Download)

**Files:**
- Modify: `SqlDeployerGui/MainWindow.xaml:27-36` (banner action button)
- Modify: `SqlDeployerGui/MainWindow.xaml.cs:72-80` (`ShowUpdateBanner`, `UpdateRestart_Click`)
- Modify: `SqlDeployerGui/App.xaml.cs:129-141` (`CheckForUpdatesAsync`)

- [ ] **Step 1: Name the banner button in XAML**

In `SqlDeployerGui/MainWindow.xaml`, replace the `InfoBar.ActionButton` block (lines 33-35) with:

```xml
            <muxc:InfoBar.ActionButton>
                <Button x:Name="UpdateActionButton" Content="Restart to update" Click="UpdateRestart_Click" />
            </muxc:InfoBar.ActionButton>
```

- [ ] **Step 2: Dual-mode ShowUpdateBanner**

In `SqlDeployerGui/MainWindow.xaml.cs`, replace `ShowUpdateBanner` and `UpdateRestart_Click` (lines 72-80) with:

```csharp
    private string? _updateUrl;

    // Surfaces an update as a dismissible top banner. Two modes: a Velopack-staged
    // update offers "Restart to update"; a portable/dev detection (UpdateAvailable)
    // offers "Download", opening the release page in the browser.
    public void ShowUpdateBanner(SqlDeployerGui.Services.UpdateResult result)
    {
        if (result.Status == SqlDeployerGui.Services.UpdateStatus.UpdateAvailable)
        {
            _updateUrl = result.Url;
            UpdateBanner.Message = $"SqlDeployer {result.Version} is available. Download it from GitHub.";
            UpdateActionButton.Content = "Download";
        }
        else
        {
            _updateUrl = null;
            UpdateBanner.Message = $"SqlDeployer {result.Version} has been downloaded. Restart to apply it.";
            UpdateActionButton.Content = "Restart to update";
        }
        UpdateBanner.IsOpen = true;
    }

    private async void UpdateRestart_Click(object sender, RoutedEventArgs e)
    {
        if (_updateUrl is not null)
            await Windows.System.Launcher.LaunchUriAsync(new Uri(_updateUrl));
        else
            App.Updates.ApplyAndRestart();
    }
```

(If `SqlDeployerGui.Services` is already imported at the top of the file, drop the namespace qualifiers; check the existing `using` block.)

- [ ] **Step 3: Show the banner for both statuses at startup**

In `SqlDeployerGui/App.xaml.cs`, replace the body of `CheckForUpdatesAsync` (lines 129-141) with:

```csharp
        var result = await Updates.CheckAndDownloadAsync();
        if (result.Status is not (UpdateStatus.UpdateReady or UpdateStatus.UpdateAvailable)) return;

        // This runs from a DispatcherQueueTimer tick where SynchronizationContext can
        // be null, so the continuation after the await above may resume off the UI
        // thread. Touching the banner there throws (and is swallowed by the
        // fire-and-forget caller). Marshal the UI update back onto the window's
        // dispatcher explicitly.
        Window.DispatcherQueue.TryEnqueue(() => Window.ShowUpdateBanner(result));
```

- [ ] **Step 4: Do NOT commit yet** — `SettingsPage.xaml.cs` still calls `ShowUpdateBanner(result.Version!)`. Continue into Task 8.

---

### Task 8: Settings page — current version + UpdateAvailable status

**Files:**
- Modify: `SqlDeployerGui/Views/SettingsPage.xaml.cs:30-33` (constructor) and `:53-82` (`CheckUpdates_Click`)

- [ ] **Step 1: Show the current version at rest**

In the `SettingsPage` constructor, after the `AboutExpander.Description = ...` line, add:

```csharp
        UpdateStatusText.Text = $"Current version: {version}";
```

- [ ] **Step 2: Update the status switch**

Replace the `switch` block in `CheckUpdates_Click` with:

```csharp
        switch (result.Status)
        {
            case UpdateStatus.UpToDate:
                UpdateStatusText.Text = "You're up to date.";
                break;
            case UpdateStatus.Failed:
                UpdateStatusText.Text = "Couldn't check — try again later.";
                break;
            case UpdateStatus.UpdateAvailable:
                UpdateStatusText.Text = $"Version {result.Version} is available.";
                App.Window.ShowUpdateBanner(result);
                break;
            case UpdateStatus.UpdateReady:
                UpdateStatusText.Text = $"Version {result.Version} downloaded.";
                App.Window.ShowUpdateBanner(result);
                break;
        }
```

(The `NotInstalled` case is gone — the enum member no longer exists after Task 6.)

- [ ] **Step 3: Build everything and run all tests**

Run: `dotnet build 2>&1 | Select-Object -Last 5`
Expected: Build succeeded, 0 errors.

Run: `dotnet test SqlDeployer.Core.Tests 2>&1 | Select-Object -Last 10`
Expected: full suite PASS.

- [ ] **Step 4: Commit Tasks 6-8 together**

```powershell
git add SqlDeployerGui/Services/UpdateService.cs SqlDeployerGui/MainWindow.xaml SqlDeployerGui/MainWindow.xaml.cs SqlDeployerGui/App.xaml.cs SqlDeployerGui/Views/SettingsPage.xaml.cs
git commit -m "feat(updates): GitHub-API fallback so portable/dev builds get update notifications"
```

---

### Task 9: Final verification

- [ ] **Step 1: Full build + full test run**

Run: `dotnet build 2>&1 | Select-Object -Last 5` then `dotnet test SqlDeployer.Core.Tests 2>&1 | Select-Object -Last 10`
Expected: Build succeeded; all tests PASS.

- [ ] **Step 2: Manual smoke check (launch the app)**

Launch the GUI (F5 / `dotnet run --project SqlDeployerGui` — match how the project is normally run). Verify:
1. Deploy page shows three output tabs: Success (0) / Errors (0) / Pending (0).
2. Running a deploy against a folder with a deliberately broken script leaves the broken file visible in both Errors and Pending; good files drain out of Pending.
3. Since the dev build is not Velopack-installed and the local version equals the latest release (2.3.2), Settings → "Check for updates" should report "You're up to date." (To see the banner: temporarily lower `<Version>` in `SqlDeployerGui.csproj` to 2.3.1, run, observe "Version 2.3.2 is available" banner with a Download button, then revert.)

- [ ] **Step 3: Report results to the user** — including any deviation from expected output.

---

## Self-review notes

- Spec coverage: plan report (Task 2), result lists (Task 1), PendingLog fill/drain/retain/abort/clear (Task 3), UI tab (Task 4), fallback check (Tasks 5-6), dual banner (Task 7), Settings version/status (Task 8). Out-of-scope items untouched.
- Tasks 6-8 are intentionally one commit: removing `UpdateStatus.NotInstalled` and changing `ShowUpdateBanner`'s signature breaks GUI compilation until all three are edited.
- Type consistency: `UpdateResult(Status, Version, Url)` is used identically in Tasks 6, 7, 8; `PendingLog`/`PendingHeader` identical in Tasks 3, 4.
