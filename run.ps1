#!/usr/bin/env pwsh
# Launch the WinUI 3 desktop app. Usage: .\run.ps1 [-Configuration Debug]
param(
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot
Write-Host "Running SqlDeployerGui ($Configuration)..." -ForegroundColor Cyan
dotnet run --project SqlDeployerGui/SqlDeployerGui.csproj -c $Configuration
