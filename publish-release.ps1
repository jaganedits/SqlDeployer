#!/usr/bin/env pwsh
# Build AND publish a release to GitHub in one step, so installed apps auto-update.
#
#   $env:GITHUB_TOKEN = "ghp_xxx"          # a token with 'repo' scope (set once per shell)
#   .\publish-release.ps1 -Version 2.1.0   # bump, build the installer, push the GitHub release
#
# What it does:
#   1. Writes <Version> into the .csproj (so the app reports the right version).
#   2. Builds the installer (publish + vpk pack) into .\Releases.
#   3. Creates a published GitHub Release 'v<Version>' and uploads the update feed
#      (Setup.exe + *-full.nupkg + RELEASES). Velopack merges with prior releases
#      so already-installed users get a delta update.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Token   = $env:GITHUB_TOKEN,
    [string]$RepoUrl = "https://github.com/jaganedits/SqlDeployer",
    [string]$OutDir  = "Releases",
    [string]$Channel = "win"
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

if (-not $Token) {
    throw "No GitHub token. Set `$env:GITHUB_TOKEN to a token with 'repo' scope, or pass -Token."
}

$proj = "SqlDeployerGui/SqlDeployerGui.csproj"

# 1. Stamp the version into the project file.
Write-Host "Setting version $Version in $proj ..." -ForegroundColor Cyan
$xml = [xml](Get-Content $proj)
$xml.Project.PropertyGroup[0].Version = $Version
$xml.Save((Resolve-Path $proj))

# 2. Build the installer + update package.
& "$PSScriptRoot/make-installer.ps1" -Version $Version -OutDir $OutDir -Channel $Channel

# 3. Publish the GitHub release and upload the feed.
Write-Host "Publishing GitHub release v$Version ..." -ForegroundColor Cyan
vpk upload github `
    --repoUrl     $RepoUrl `
    --token       $Token `
    --channel     $Channel `
    --outputDir   $OutDir `
    --releaseName "SqlDeployer $Version" `
    --tag         "v$Version" `
    --merge `
    --publish true

Write-Host "`nPublished. Installed apps will offer v$Version on next launch." -ForegroundColor Green
