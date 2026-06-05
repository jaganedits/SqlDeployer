# SQL Deploy — WinUI 3 Rewrite Design

**Date:** 2026-06-06
**Status:** Approved (design), pending implementation plan
**Author:** designer@leitenindia.com (with Claude Code)

## Summary

Rewrite the existing **WinForms** SQL migration deployment tool (`SqlDeployerGui`)
as a modern **WinUI 3** (Windows App SDK) application using the **MVVM** pattern.
The pure backend (`SqlServerDeployer.cs`) is reused unchanged. The UI layer is
fully rebuilt with a left navigation rail and multiple pages, gaining a
deployment-history view and persisted connection settings.

## Goals

- Native modern Windows 11 look (Fluent controls, Mica backdrop, system theming).
- Preserve **all** existing deployment behavior exactly.
- Keep the current distribution model: a portable, **self-contained single `.exe`**,
  no installer, no admin rights (unpackaged Windows App SDK).
- Add high-value features the backend already supports: deployment history view,
  persisted last-used connection.

## Non-Goals

- No change to deployment logic, SQL, or the `DeploymentHistory` schema.
- No MSIX / Store packaging.
- No storing of SQL passwords on disk.

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Distribution | Unpackaged + self-contained single `.exe` (closest to today) |
| Scope | Modernize freely — nav pages, history view, persisted connection |
| Layout | Left navigation rail (`NavigationView`) + pages |
| Saved credentials | Persist all fields **except password**; user re-enters password each session |
| Architecture | WinUI 3 + MVVM (`CommunityToolkit.Mvvm`) |

## Tech & Project Setup

- **Framework:** WinUI 3 via Windows App SDK.
- **Target:** `net10.0-windows10.0.19041.0`, `WindowsAppSDKSelfContained=true`,
  `WindowsPackageType=None` (unpackaged), `SelfContained=true`, `RuntimeIdentifier=win-x64`.
- **NuGet:** `Microsoft.WindowsAppSDK` (current stable), `CommunityToolkit.Mvvm`,
  `Microsoft.Data.SqlClient` (already used).
- **Removed:** `Form1.cs`, `Form1.Designer.cs`, `ModernUI.cs`, old WinForms `.csproj`,
  `Program.cs` (replaced by WinUI `App`).
- **Reused unchanged:** `SqlServerDeployer.cs` (and its records/config types).

## Project Structure

```
App.xaml(.cs)                 – app startup, theme init, window creation
MainWindow.xaml(.cs)          – NavigationView shell, Mica backdrop, custom title bar
Views/
  DeployPage.xaml(.cs)        – connection card, Test/Deploy/Cancel, progress, live logs
  HistoryPage.xaml(.cs)       – list/grid of DeploymentHistory rows
  SettingsPage.xaml(.cs)      – theme selection, saved-connection management
ViewModels/
  DeployViewModel.cs
  HistoryViewModel.cs
  SettingsViewModel.cs
Services/
  SettingsService.cs          – JSON persistence in %LocalAppData% (no password)
  DeploymentRunner.cs         – async wrapper over SqlServerDeployer (progress + cancel)
Models/
  ConnectionProfile.cs        – server, login, database, scriptPath (no password)
  LogEntry.cs                 – message + kind (success/error) for log lists
SqlDeployer/
  SqlServerDeployer.cs        – moved as-is, unchanged
```

## UI Mapping (today → WinUI 3)

| Today (WinForms) | WinUI 3 |
|---|---|
| Custom `RoundedTextBox` fields | `TextBox` in a Fluent `Card` |
| Password field w/ custom eye toggle | `PasswordBox` (native reveal button) |
| `RoundedButton` Test/Deploy/Cancel | `Button` (Deploy = `AccentButtonStyle`); Cancel enabled only during run |
| `ProgressBar pb1` + `lblmessage` | `ProgressBar` + status `TextBlock` with live counts |
| Segmented Success/Error log tabs | `SelectorBar`/`Pivot`, each an auto-scrolling colored log `ListView` |
| Theme toggle button | Moves to **Settings**; applied app-wide via `RequestedTheme`; title bar follows |
| `FolderBrowserDialog` | `FolderPicker` (WinRT) wired to the window handle |
| `MessageBox` (validation/success/error) | `ContentDialog` |
| — (unused backend method) | **New** History page surfacing `GetDeploymentHistory()` |
| — | **New** last-used connection (minus password) restored on launch |

## Components & Responsibilities

- **MainWindow** — hosts `NavigationView` (Deploy / History / Settings), Mica backdrop,
  custom title bar; owns navigation. No business logic.
- **DeployViewModel** — holds connection fields (bound), validation, command handlers for
  Test/Deploy/Cancel, observable log collections (success/error), progress value, status
  text, busy state. Builds the connection string (same logic as today's `GetConnectionString`).
- **DeploymentRunner** — thin service: given connection string + scripts path, calls
  `SqlServerDeployer.GetPendingScripts`, loops `ExecuteScript`, reports each result through
  `IProgress<DeploymentProgress>`, honors `CancellationToken`. No UI types.
- **HistoryViewModel** — loads `GetDeploymentHistory(connectionString)` into an observable
  collection for the History page; refresh command.
- **SettingsService** — load/save `ConnectionProfile` + theme preference as JSON under
  `%LocalAppData%\SqlDeploy\settings.json`. Password is never written.
- **SqlServerDeployer** — unchanged backend (pending-script detection, version ordering,
  `GO` batch splitting, transaction per script, audit logging, tracking-table creation).

## Data Flow (Deploy)

1. User fills connection card on **DeployPage** (bound to `DeployViewModel`).
2. **Deploy** command validates inputs → on error shows `ContentDialog`.
3. ViewModel builds connection string → calls `DeploymentRunner.RunAsync(connStr, path, progress, token)`.
4. Runner: `GetPendingScripts` → if none, report "no pending scripts"; else loop
   `ExecuteScript` per file, reporting progress + per-file success/error.
5. ViewModel updates progress bar, status text, and the success/error log collections
   (bound to the log lists). Cancel triggers the `CancellationToken`.
6. On completion, a `ContentDialog` summarizes succeeded/failed counts.
7. Successful run persists the connection profile (minus password) via `SettingsService`.

## Error Handling

- **Validation** (missing server/database/path, nonexistent folder) → `ContentDialog`,
  no run started — mirrors today's checks.
- **Per-script failure** → captured to Error log; deployment continues to next script
  (unchanged behavior). Audit row written with the error message.
- **Cancellation** → `OperationCanceledException` handled cleanly; backend rolls back the
  in-flight transaction; status shows "cancelled"; not logged as an error.
- **Connection/test failure** → `ContentDialog` with the exception message.
- All long-running work is async off the UI thread; UI updates marshaled via bindings
  (no manual `Invoke` needed as in WinForms).

## Behavior Preserved Exactly

- Scripts processed in numeric-aware version order.
- Already-deployed versions skipped; `_rollback` scripts skipped.
- `GO` batch splitting; one transaction per script.
- `DeploymentHistory` audit table auto-created and written (script, version, environment
  "GUI", success, error, deployed-by).
- Windows authentication when login is blank; SQL auth otherwise; `Encrypt=false`.

## Testing

- **DeploymentRunner / SqlServerDeployer**: unit-testable independent of UI (pure async
  logic; connection string + temp script folder). Existing backend behavior covered by
  tests around pending-script selection, version ordering, and `GO` splitting.
- **ViewModels**: testable without XAML — validation, command enable/disable, log
  accumulation, progress updates.
- **SettingsService**: round-trip JSON load/save; assert password never persisted.
- **Manual smoke**: build self-contained `.exe`, run unpackaged, exercise Test/Deploy/
  Cancel/History/theme on a real SQL Server instance.

## Risks & Mitigations

- **Windows App SDK + .NET 10 / self-contained build friction** → pin a known-good
  WindowsAppSDK version; verify `dotnet publish` of the unpackaged self-contained `.exe`
  early in implementation.
- **FolderPicker in unpackaged apps** requires associating the picker with the window
  `HWND` (`InitializeWithWindow`) → handled in a small interop helper.
- **Single-file publish** with native WindowsAppSDK assets may differ from today's
  `PublishSingleFile`; validate output layout (a small folder of runtime files alongside
  the `.exe` is acceptable and expected for unpackaged self-contained).
