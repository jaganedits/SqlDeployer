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

## Clean

```powershell
dotnet clean SqlDeploy.slnx
```

To remove all build output entirely, delete the `bin/`, `obj/`, and `publish/` folders
(these are git-ignored).
