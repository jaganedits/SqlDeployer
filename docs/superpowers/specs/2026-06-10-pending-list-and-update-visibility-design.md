# Pending-Script Visibility & Update-Notification Visibility — Design

**Date:** 2026-06-10
**Status:** Approved

## Problem

1. When a deployment finishes with errors, the user cannot see at a glance
   which files failed and which files remain undeployed (still runnable).
   The Success/Errors lists exist, but there is no "what remains" view —
   especially when the deploy aborts up front (FK cycle, missing folder) or
   is cancelled mid-run.
2. Update notifications appear to do nothing. Diagnosis: the Velopack
   pipeline and GitHub release assets are healthy, but when the app runs as
   a Portable zip or dev build, `UpdateManager.IsInstalled` is false and
   every check is a silent no-op — the user never learns a new version
   exists.

## Decisions (from brainstorming)

- Error policy stays **continue past failures** (run everything, report
  fully). No stop-on-error mode.
- Report surface: a third **Pending (N)** list next to the existing
  Success (N) / Errors (N) lists on the Deploy page.
- Updates: keep Velopack for installed copies; add a **GitHub Releases API
  fallback** for non-installed runs that notifies with a download link.
  No auto-download in the fallback path.

## Feature 1 — Pending list

### Core (SqlDeployer.Core)

- **`DeploymentProgress`**: the runner's first report carries the full plan
  (ordered list of pending script ids) before any script executes.
  Per-script reports are unchanged.
- **`DeploymentResult`**: add `IReadOnlyList<string> Succeeded`, `Failed`,
  `NotRun` (script ids). `NotRun` is populated only when the run stops
  early (cancellation); with run-all policy every planned script is
  otherwise attempted.
- **`DeploymentRunner.RunAsync`**: emits the plan report first, then
  accumulates the three lists while looping. Execution order and the
  continue-past-failure policy are unchanged.

### ViewModel (DeployViewModel)

- New `ObservableCollection<LogEntry> PendingLog` and
  `PendingHeader => $"Pending ({PendingLog.Count})"` (same
  CollectionChanged → OnPropertyChanged wiring as the existing headers).
- Plan report → fill `PendingLog` with every planned file.
- Success report → remove that file from `PendingLog`.
- Failure report → file **stays** in `PendingLog` (it is still
  re-runnable) and is added to `ErrorLog` as today.
- Deploy aborts before the loop (FK cycle, missing folder, connection
  failure) → fill `PendingLog` from the discovered plan
  (DiscoverScripts + Resolve, rollback and already-deployed files
  excluded where determinable) so the user still sees what would have run.
- Clean full success → `PendingLog` ends empty.
- `PendingLog` is cleared at the start of each deploy alongside
  Success/Error logs.

### UI (SqlDeployerGui, DeployPage.xaml)

- Third list "Pending (N)" alongside Success/Errors, same `LogEntry`
  item template, Info styling.

### Tests (SqlDeployer.Core.Tests)

- Runner: result lists for all-success, mixed success/failure,
  cancellation mid-run (NotRun populated), no-pending.
- ViewModel: pending fills from plan, drains on success, retains failed
  files, abort path fills pending, cleared between runs.
- All against `FakeSqlDeployer` / existing fakes.

## Feature 2 — Update visibility for portable/dev runs

### UpdateService (SqlDeployerGui)

- New status `UpdateAvailable` (newer release exists; cannot be
  auto-applied) carried in `UpdateResult` together with the release page
  URL.
- `CheckAndDownloadAsync` when `IsInstalled == false`: call the GitHub API
  `repos/jaganedits/SqlDeployer/releases/latest` (HttpClient, ~5 s
  timeout), parse the tag (`v2.3.2` → semver), compare against the running
  assembly version. Newer → `UpdateAvailable`; same/older → `UpToDate`;
  any failure → `Failed` (swallowed; never blocks startup).
- Installed path (Velopack check/download/`UpdateReady`) unchanged.

### UI

- `MainWindow.ShowUpdateBanner` handles both outcomes:
  - `UpdateReady` → "downloaded — Restart to apply" (existing behavior).
  - `UpdateAvailable` → "Version X.Y.Z is available" with a **Download**
    button that opens the GitHub release page in the default browser.
- Settings update card: always shows the current app version; the check
  button reports "Update available — Download" for the fallback case
  instead of "Updates apply only to the installed app."

### Error handling

- Startup check stays fire-and-forget and silent on failure.
- Settings check keeps the explicit "Couldn't check — try again later."

## Out of scope

- Stop-on-first-error mode or per-script dependency-aware skipping.
- Auto-download/auto-install for portable builds.
- Changes to the release pipeline (`vpk upload github` already publishes
  correct assets).
