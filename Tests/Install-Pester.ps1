<#
.SYNOPSIS
    Install Pester v5+ for Win11Forge tests

.DESCRIPTION
    Installs or updates Pester to v5+ required for Win11Forge v2.5.0 tests

.NOTES
    Author: Win11Forge Team
    Version: 2.5.0
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Pester Installation for Win11Forge v2.5.0" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check current version
$currentPester = Get-Module -Name Pester -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1

if ($currentPester) {
    Write-Host "Current Pester version: v$($currentPester.Version)" -ForegroundColor Yellow

    if ($currentPester.Version.Major -ge 5) {
        Write-Host "✅ Pester v5+ already installed!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Ready to run tests with:" -ForegroundColor White
        Write-Host "  .\Invoke-Tests.ps1" -ForegroundColor Cyan
        exit 0
    }
} else {
    Write-Host "Pester not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Installing Pester v5+..." -ForegroundColor Yellow
Write-Host ""

try {
    # Install Pester v5+
    Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck -Scope CurrentUser

    Write-Host ""
    Write-Host "✅ Pester v5+ installed successfully!" -ForegroundColor Green

    # Verify installation
    $newPester = Get-Module -Name Pester -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1
    Write-Host "Installed version: v$($newPester.Version)" -ForegroundColor Green

    Write-Host ""
    Write-Host "Ready to run tests with:" -ForegroundColor White
    Write-Host "  .\Invoke-Tests.ps1" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "❌ Failed to install Pester" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try manual installation:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Cyan
    exit 1
}
