#!/usr/bin/env pwsh
# Build the full solution. Usage: .\build.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot
Write-Host "Building SqlDeploy.slnx ($Configuration)..." -ForegroundColor Cyan
dotnet build SqlDeploy.slnx -c $Configuration
