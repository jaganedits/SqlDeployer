#!/usr/bin/env pwsh
# Produce a self-contained build of the GUI into .\publish.
# Usage: .\publish.ps1 [-OutDir publish]
param(
    [string]$OutDir = "publish"
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot
Write-Host "Publishing SqlDeployerGui to '$OutDir'..." -ForegroundColor Cyan
dotnet publish SqlDeployerGui/SqlDeployerGui.csproj -c Release -o $OutDir
Write-Host "Done. Run: .\$OutDir\SqlDeployerGui.exe" -ForegroundColor Green
