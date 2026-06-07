#!/usr/bin/env pwsh
# Run the unit tests. Usage: .\test.ps1 [-Filter "FullyQualifiedName~SettingsService"] [-Coverage]
param(
    [string]$Filter = "",
    [switch]$Coverage
)
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$cmdArgs = @("test", "SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj", "--nologo")
if ($Filter) { $cmdArgs += @("--filter", $Filter) }
if ($Coverage) { $cmdArgs += @("--collect:XPlat Code Coverage") }

Write-Host "Running tests..." -ForegroundColor Cyan
dotnet @cmdArgs
