<#
.SYNOPSIS
    Test applying Start Menu layout to current user

.DESCRIPTION
    Quick test of the -ApplyToCurrentUser flag
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Check admin
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Administrator privileges required" -ForegroundColor Red
    exit 1
}

# Load modules
$ScriptRoot = $PSScriptRoot
Import-Module (Join-Path $ScriptRoot 'Core\Core.psm1') -Force
Import-Module (Join-Path $ScriptRoot 'Modules\StartMenuPinning.psm1') -Force

Write-Host "=== Test: Apply Layout to Current User ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Backup your current Start Menu layout" -ForegroundColor Gray
Write-Host "  2. Deploy to Default profile" -ForegroundColor Gray
Write-Host "  3. Apply the layout back to your current user" -ForegroundColor Gray
Write-Host "  4. Restart Start Menu" -ForegroundColor Gray
Write-Host ""

$response = Read-Host "Continue? (Y/N)"
if ($response -ne 'Y') {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Invoke-StartMenuPinning -BackupName "Test_ApplyToCurrentUser_$(Get-Date -Format 'yyyyMMdd_HHmmss')" -ApplyToCurrentUser

Write-Host ""
Write-Host "Test completed!" -ForegroundColor Green
Write-Host "Check your Start Menu to verify pinned items" -ForegroundColor Cyan
