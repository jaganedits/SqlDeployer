#!/usr/bin/env pwsh
# Build a single installer (Setup.exe) + update package for SqlDeployer using Velopack.
#
#   .\make-installer.ps1                 # uses <Version> from the .csproj
#   .\make-installer.ps1 -Version 2.1.0  # override the release version
#
# Output lands in .\Releases:
#   SqlDeployer-win-Setup.exe   <- the installer you hand to users (install + uninstall)
#   SqlDeployer-<ver>-full.nupkg + RELEASES  <- upload these so the app can auto-update
#
# First time only, install the CLI:  dotnet tool install -g vpk
param(
    [string]$Version,
    [string]$OutDir   = "Releases",
    [string]$PublishDir = "publish",
    [string]$Channel  = "win"
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$proj = "SqlDeployerGui/SqlDeployerGui.csproj"

if (-not $Version) {
    $Version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Select-Object -First 1
    Write-Host "Using version $Version from $proj" -ForegroundColor DarkGray
}

# 1. Clean self-contained publish of the GUI.
Write-Host "Publishing $proj ..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $proj -c Release -o $PublishDir

# 2. Pack into an installer + update feed.
Write-Host "Packing installer (v$Version) ..." -ForegroundColor Cyan
vpk pack `
    --packId      SqlDeployer `
    --packTitle   "SqlDeployer" `
    --packAuthors "Jagan" `
    --packVersion $Version `
    --packDir     $PublishDir `
    --mainExe     SqlDeployerGui.exe `
    --icon        SqlDeployerGui/Assets/app.ico `
    --channel     $Channel `
    --outputDir   $OutDir

Write-Host "`nDone. Installer: $OutDir\SqlDeployer-$Channel-Setup.exe" -ForegroundColor Green
Write-Host "Publish a GitHub release with:" -ForegroundColor DarkGray
Write-Host "  vpk upload github --repoUrl https://github.com/jaganedits/SqlDeployer --publish --releaseName `"SqlDeployer $Version`" --tag v$Version --token <YOUR_GH_TOKEN> --outputDir $OutDir" -ForegroundColor DarkGray
