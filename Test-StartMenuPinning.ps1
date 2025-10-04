<#
.SYNOPSIS
    Test script for StartMenuPinning module (start2.bin method)

.DESCRIPTION
    Tests the new Start Menu pinning functionality using start2.bin binary file method.
    This is the reliable method for Windows 11 22H2+ as LayoutModification.json is deprecated.

.EXAMPLE
    .\Test-StartMenuPinning.ps1

.NOTES
    Requires Administrator privileges
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
    Write-Host "Please run this script as Administrator" -ForegroundColor Red
    exit 1
}

# === LOAD MODULES ===
$script:ScriptRoot = $PSScriptRoot

# Load Core module
$coreModule = Join-Path $script:ScriptRoot 'Core\Core.psm1'
if (Test-Path $coreModule) {
    Import-Module $coreModule -Force
    Write-Host "✓ Core module loaded" -ForegroundColor Green
}

# Load StartMenuPinning module
$pinningModule = Join-Path $script:ScriptRoot 'Modules\StartMenuPinning.psm1'
if (-not (Test-Path $pinningModule)) {
    Write-Host "ERROR: StartMenuPinning module not found at: $pinningModule" -ForegroundColor Red
    exit 1
}

Import-Module $pinningModule -Force
Write-Host "✓ StartMenuPinning module loaded" -ForegroundColor Green
Write-Host ""

# === TEST FUNCTIONS ===

Write-Host "=== Start Menu Pinning Test ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Detect binary type
Write-Host "Test 1: Detecting Start Menu binary type..." -ForegroundColor Yellow
$binaryType = Get-StartMenuBinaryType

if ($binaryType) {
    Write-Host "[OK] Detected: $binaryType" -ForegroundColor Green

    $currentBinary = Get-CurrentUserStartMenuBinary
    if (Test-Path $currentBinary) {
        $size = (Get-Item $currentBinary).Length
        Write-Host "  File: $currentBinary" -ForegroundColor Gray
        Write-Host "  Size: $size bytes" -ForegroundColor Gray
    }
} else {
    Write-Host "[FAIL] No Start Menu binary found (start2.bin or start.bin)" -ForegroundColor Red
    Write-Host "  This may indicate an incompatible Windows version" -ForegroundColor Yellow
}

Write-Host ""

# Test 2: List existing backups
Write-Host "Test 2: Checking for existing backups..." -ForegroundColor Yellow
$backups = Get-BackedUpLayouts

Write-Host ""

# Test 3: Show what would be done
Write-Host "Test 3: Deployment plan..." -ForegroundColor Yellow
$defaultBinary = Get-DefaultProfileStartMenuBinary
Write-Host "  Source: $currentBinary" -ForegroundColor Gray
Write-Host "  Target: $defaultBinary" -ForegroundColor Gray

if (Test-Path $defaultBinary) {
    $existingSize = (Get-Item $defaultBinary).Length
    Write-Host "  Existing Default profile layout: $existingSize bytes (will be backed up)" -ForegroundColor Yellow
} else {
    Write-Host "  No existing Default profile layout (will be created)" -ForegroundColor Gray
}

Write-Host ""

# Test 4: Ask user if they want to proceed
Write-Host "=== Ready to Deploy ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This will:" -ForegroundColor White
Write-Host "  1. Capture your current Start Menu pinned items" -ForegroundColor Gray
Write-Host "  2. Back up the layout to Backups\StartMenuLayouts\" -ForegroundColor Gray
Write-Host "  3. Deploy it to C:\Users\Default\" -ForegroundColor Gray
Write-Host "  4. New user accounts will inherit your pinned items" -ForegroundColor Gray
Write-Host ""

$response = Read-Host "Proceed with deployment? (Y/N)"

if ($response -ne 'Y') {
    Write-Host "Deployment cancelled by user" -ForegroundColor Yellow
    exit 0
}

Write-Host ""

# Test 5: Execute deployment
Write-Host "=== Executing Deployment ===" -ForegroundColor Cyan
Write-Host ""

try {
    $result = Invoke-StartMenuPinning -BackupName "Test_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

    if ($result) {
        Write-Host ""
        Write-Host "=== Deployment Successful ===" -ForegroundColor Green
        Write-Host ""
        Write-Host "[OK] Start Menu layout captured and deployed" -ForegroundColor Green
        Write-Host "[OK] New users will inherit current pinned items" -ForegroundColor Green
        Write-Host ""
        Write-Host "To verify:" -ForegroundColor Cyan
        Write-Host "  1. Create a new local user account" -ForegroundColor Gray
        Write-Host "  2. Log in with the new account" -ForegroundColor Gray
        Write-Host "  3. Open Start Menu and check pinned items" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Backups are stored in: $script:ScriptRoot\Backups\StartMenuLayouts\" -ForegroundColor Gray
    } else {
        Write-Host ""
        Write-Host "=== Deployment Failed ===" -ForegroundColor Red
        Write-Host "Check the error messages above for details" -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "=== Deployment Error ===" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
}

Write-Host ""
Write-Host "Test completed" -ForegroundColor Cyan
