<#
.SYNOPSIS
    Test script for Start Menu Organization (LayoutModification.json)
.DESCRIPTION
    Standalone script to test the Start Menu organization feature using official Microsoft method
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\ApplicationDatabase.psm1" -Force
Import-Module "$PSScriptRoot\Modules\StartMenuLayout.psm1" -Force -WarningAction SilentlyContinue

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Start Menu Organization Test" -ForegroundColor Cyan
Write-Host "  (LayoutModification.json method)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run the organization
Invoke-StartMenuOrganization -Verbose

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Test Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Layout file created at:" -ForegroundColor Yellow
Write-Host "$env:LOCALAPPDATA\Microsoft\Windows\Shell\LayoutModification.json" -ForegroundColor White
Write-Host ""
Write-Host "Start Menu has been restarted. Check your Start Menu!" -ForegroundColor Yellow
Write-Host "If changes don't appear, please log off and log back in." -ForegroundColor Yellow
Write-Host ""
