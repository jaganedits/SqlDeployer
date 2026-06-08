# SqlDeployer

A Windows desktop app for deploying SQL scripts to SQL Server, with dependency-aware
ordering, deployment history, and theming. Built with **WinUI 3** (Windows App SDK) on
**.NET 10**, following an MVVM architecture.

## Projects

| Project | Type | Description |
|---------|------|-------------|
| `SqlDeployer.Core` | Class library (`net10.0`) | Models, services, and view models. No UI dependencies. |
| `SqlDeployerGui` | WinUI 3 app (`net10.0-windows`, x64) | The desktop UI. References `SqlDeployer.Core`. |
| `SqlDeployer.Core.Tests` | xUnit tests (`net10.0`) | Unit tests for the Core library. |

Solution file: `SqlDeploy.slnx`

## Prerequisites

- **Windows 10 (build 17763) or later**, x64 — the GUI uses the Windows App SDK and only targets `win-x64`.
- **.NET SDK 10.0** or later (pinned via `global.json`). Check with:

  ```powershell
  dotnet --version
  ```

The Core library and tests are plain `net10.0` and build on any platform, but the
`SqlDeployerGui` app builds and runs on Windows only.

## Build

Build the whole solution (Debug):

```powershell
dotnet build SqlDeploy.slnx
```

Build a single project:

```powershell
dotnet build SqlDeployer.Core/SqlDeployer.Core.csproj
dotnet build SqlDeployerGui/SqlDeployerGui.csproj
```

Release build:

```powershell
dotnet build SqlDeploy.slnx -c Release
```

## Run

Launch the GUI app:

```powershell
dotnet run --project SqlDeployerGui/SqlDeployerGui.csproj
```

Or run the built executable directly after a build:

```powershell
.\SqlDeployerGui\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe
```

## Test

Run all tests:

```powershell
dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj
```

Run tests via the solution:

```powershell
dotnet test SqlDeploy.slnx
```

Run a single test or filtered subset (by fully qualified name or trait):

```powershell
dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter "FullyQualifiedName~ScriptDependencyResolver"
```

Collect code coverage (the `coverlet.collector` package is already referenced):

```powershell
dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --collect:"XPlat Code Coverage"
```

## Publish

Produce a self-contained, single-folder build of the GUI (the `SqlDeployerGui.csproj`
is already configured as self-contained / `win-x64`):

```powershell
dotnet publish SqlDeployerGui/SqlDeployerGui.csproj -c Release -o publish
```

The runnable app is then `publish\SqlDeployerGui.exe`.

## Installer & releases (Velopack)

The app ships as a single **`Setup.exe`** that installs per-user, adds Start-menu /
desktop shortcuts, registers in Add/Remove Programs, and auto-updates from GitHub
Releases. Tooling is the [Velopack](https://velopack.io) `vpk` CLI — install it once:

```powershell
dotnet tool install -g vpk
```

**Build the installer locally** (no upload):

```powershell
.\make-installer.ps1                 # uses <Version> from the .csproj
.\make-installer.ps1 -Version 2.1.0  # override the version
```

Output lands in `.\Releases`:

| File | Purpose |
|------|---------|
| `SqlDeployer-win-Setup.exe` | The installer you hand to users (install + uninstall). |
| `SqlDeployer-<ver>-full.nupkg` + `RELEASES` | The update feed — upload so installed apps self-update. |
| `SqlDeployer-win-Portable.zip` | A no-install portable build. |

**Publish a release** (build + push to GitHub in one step, so installed apps update):

```powershell
$env:GITHUB_TOKEN = "ghp_xxx"            # token with 'repo' scope (once per shell)
.\publish-release.ps1 -Version 2.1.0
```

This stamps the version into the `.csproj`, builds the installer, then creates a
published GitHub release `v2.1.0` and uploads the feed. Already-installed apps detect
it on next launch (or via **Settings → Updates → Check for updates**) and offer a
one-click restart-to-update.

> **Versioning:** always bump `-Version` for each release. Velopack refuses to repack a
> version already present in `.\Releases`, and installed clients only update to a
> *higher* version.

> **Code signing:** unsigned installers trigger a SmartScreen warning on first run. For
> public distribution, pass `--signParams` to `vpk pack` (in `make-installer.ps1`) with a
> code-signing certificate.

> **Vercel hosting:** to serve the feed from Vercel instead of GitHub, host the `Releases`
> folder as static files and swap `GithubSource` for `SimpleWebSource` (pointed at that
> URL) in [`UpdateService.cs`](SqlDeployerGui/Services/UpdateService.cs).

## Clean

```powershell
dotnet clean SqlDeploy.slnx
```

To remove all build output entirely, delete the `bin/`, `obj/`, and `publish/` folders
(these are git-ignored).
