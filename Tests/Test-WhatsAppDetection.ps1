# Test WhatsApp Desktop Detection
# Validates that WhatsApp is correctly detected when installed

$ErrorActionPreference = 'Stop'

Write-Host "=== Testing WhatsApp Desktop Detection ===" -ForegroundColor Cyan
Write-Host ""

# Import required modules
$repoRoot = Split-Path $PSScriptRoot -Parent
Import-Module "$repoRoot\Modules\InstallationEngine.psm1" -Force
Import-Module "$repoRoot\Modules\ApplicationDatabase.psm1" -Force

# Get WhatsApp from database
$db = Get-ApplicationDatabase
$whatsapp = $db.Applications.WhatsAppDesktop

Write-Host "Application: $($whatsapp.Name)" -ForegroundColor Yellow
Write-Host "Detection Method: $($whatsapp.Detection.Method)" -ForegroundColor Yellow
Write-Host "Package Name: $($whatsapp.Detection.PackageName)" -ForegroundColor Yellow
Write-Host ""

# Check if package is installed
Write-Host "Checking for installed package..." -ForegroundColor Cyan
$package = Get-AppxPackage -Name "*$($whatsapp.Detection.PackageName)*" -ErrorAction SilentlyContinue

if ($package) {
    Write-Host "[OK] Package found: $($package.Name)" -ForegroundColor Green
    Write-Host "    Version: $($package.Version)" -ForegroundColor Gray
    Write-Host "    Full Name: $($package.PackageFullName)" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] Package not found" -ForegroundColor Red
}

Write-Host ""

# Test detection function
Write-Host "Testing Test-ApplicationInstalled function..." -ForegroundColor Cyan
$isInstalled = Test-ApplicationInstalled -Application $whatsapp

if ($isInstalled) {
    Write-Host "[OK] WhatsApp detected as INSTALLED" -ForegroundColor Green
} else {
    Write-Host "[FAIL] WhatsApp detected as NOT INSTALLED" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
