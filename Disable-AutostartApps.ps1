<#
.SYNOPSIS
    Disable unwanted startup applications
.DESCRIPTION
    Disables Battle.net, Discord, and other specified applications from starting automatically
.NOTES
    Author: Julien Bombled
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\StartupManager.psm1" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Disable Autostart Applications" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Show current startup applications
Write-Host "Current startup applications:" -ForegroundColor Yellow
Show-StartupApplications

# Applications to disable
$appsToDisable = @(
    "Discord",
    "*Battle*",
    "BattleNet"
)

Write-Host ""
Write-Host "Applications to disable from startup:" -ForegroundColor Yellow
foreach ($app in $appsToDisable) {
    Write-Host "  - $app" -ForegroundColor White
}
Write-Host ""

$confirm = Read-Host "Continue? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""

# Disable the applications
Disable-StartupApplications -ApplicationNames $appsToDisable

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "The applications will no longer start automatically." -ForegroundColor Yellow
Write-Host "You can still launch them manually when needed." -ForegroundColor Yellow
Write-Host ""
