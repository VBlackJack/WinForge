<#
.SYNOPSIS
    Test script for parallel installation mode

.DESCRIPTION
    Tests parallel installation with a small subset of applications

.EXAMPLE
    .\Test-ParallelInstall.ps1
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# === ADMIN CHECK ===
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Administrator privileges required" -ForegroundColor Red
    exit 1
}

# === POWERSHELL VERSION CHECK ===
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "ERROR: Parallel mode requires PowerShell 7+" -ForegroundColor Red
    Write-Host "Current version: $($PSVersionTable.PSVersion)" -ForegroundColor Yellow
    Write-Host "Please run: pwsh.exe -File `"$PSCommandPath`"" -ForegroundColor Cyan
    exit 1
}

# === LOAD MODULES ===
$script:ScriptRoot = $PSScriptRoot

Write-Host "=== Parallel Installation Test ===" -ForegroundColor Cyan
Write-Host "PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Green
Write-Host ""

# Load Core
$coreModule = Join-Path $script:ScriptRoot 'Core\Core.psm1'
if (Test-Path $coreModule) {
    Import-Module $coreModule -Force
    Write-Host "[OK] Core module loaded" -ForegroundColor Green
}

# Load Application Database
$dbModule = Join-Path $script:ScriptRoot 'Modules\ApplicationDatabase.psm1'
if (Test-Path $dbModule) {
    Import-Module $dbModule -Force
    Write-Host "[OK] ApplicationDatabase module loaded" -ForegroundColor Green
}

# Load Environment Detection
$envModule = Join-Path $script:ScriptRoot 'Modules\EnvironmentDetection.psm1'
if (Test-Path $envModule) {
    Import-Module $envModule -Force
    Write-Host "[OK] EnvironmentDetection module loaded" -ForegroundColor Green
}

# Load Installation Engine
$installModule = Join-Path $script:ScriptRoot 'Modules\InstallationEngine.psm1'
if (Test-Path $installModule) {
    Import-Module $installModule -Force
    Write-Host "[OK] InstallationEngine module loaded" -ForegroundColor Green
}

Write-Host ""

# === CREATE TEST APPLICATION SET ===
Write-Host "Creating test application set..." -ForegroundColor Cyan

# Get a few lightweight apps from database
$testApps = @(
    (Get-ApplicationById -AppId "NotepadPlusPlus"),
    (Get-ApplicationById -AppId "7Zip"),
    (Get-ApplicationById -AppId "VLC")
)

Write-Host "Test apps: $($testApps.Count)" -ForegroundColor Cyan
foreach ($app in $testApps) {
    Write-Host "  - $($app.Name)" -ForegroundColor Gray
}

Write-Host ""

# === TEST PARALLEL INSTALLATION ===
Write-Host "=== Starting Parallel Installation ===" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

try {
    $results = Install-ApplicationsParallel -Applications $testApps -Force -MaxParallel 3

    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host ""
    Write-Host "=== Test Results ===" -ForegroundColor Green
    Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Cyan
    Write-Host "Apps processed: $($results.Count)" -ForegroundColor Cyan
    Write-Host ""

    $successful = ($results | Where-Object { $_.Success -or $_.AlreadyInstalled }).Count
    $failed = ($results | Where-Object { -not ($_.Success -or $_.AlreadyInstalled) }).Count

    Write-Host "Successful: $successful" -ForegroundColor Green
    Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { 'Red' } else { 'Green' })

    if ($failed -gt 0) {
        Write-Host ""
        Write-Host "Failed applications:" -ForegroundColor Red
        foreach ($result in ($results | Where-Object { -not ($_.Success -or $_.AlreadyInstalled) })) {
            Write-Host "  - $($result.ApplicationName): $($result.Message)" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "[OK] Parallel installation test completed" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "=== Test Failed ===" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    exit 1
}
