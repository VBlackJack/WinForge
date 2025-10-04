<#
.SYNOPSIS
    Test Battle.net installation
.DESCRIPTION
    Debug script to test Battle.net installation via DirectUrl
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\ApplicationDatabase.psm1" -Force
Import-Module "$PSScriptRoot\Modules\InstallationEngine.psm1" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Battle.net Installation Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get Battle.net app info
$app = Get-ApplicationById -AppId "BattleNet"

if ($null -eq $app) {
    Write-Host "ERROR: Battle.net not found in database" -ForegroundColor Red
    exit 1
}

Write-Host "Application: $($app.Name)" -ForegroundColor Yellow
Write-Host "Download URL: $($app.Sources.DirectUrl)" -ForegroundColor Yellow
Write-Host "Install Args: $($app.InstallArguments)" -ForegroundColor Yellow
Write-Host ""

# Check if already installed
Write-Host "Checking if Battle.net is already installed..." -ForegroundColor Cyan
$isInstalled = Test-ApplicationInstalled -Application $app

if ($isInstalled) {
    Write-Host "Battle.net is already installed!" -ForegroundColor Green
    exit 0
}

Write-Host "Battle.net is NOT installed" -ForegroundColor Yellow
Write-Host ""

# Ask for confirmation
$confirm = Read-Host "Do you want to install Battle.net? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Installation cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting installation..." -ForegroundColor Cyan
Write-Host ""

# Attempt installation
$result = Install-Application -Application $app -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Installation Result" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($result.Success) {
    Write-Host "SUCCESS: Battle.net installed via $($result.Method)" -ForegroundColor Green
} else {
    Write-Host "FAILED: $($result.Message)" -ForegroundColor Red
}

Write-Host ""

# Final check
Write-Host "Final verification..." -ForegroundColor Cyan
$isInstalledNow = Test-ApplicationInstalled -Application $app

if ($isInstalledNow) {
    Write-Host "Battle.net is now installed!" -ForegroundColor Green
} else {
    Write-Host "Battle.net installation could not be verified" -ForegroundColor Red
}

Write-Host ""
