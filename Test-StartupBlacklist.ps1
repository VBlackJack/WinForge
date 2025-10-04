<#
.SYNOPSIS
    Test startup blacklist configuration
.DESCRIPTION
    Tests the startup-blacklist.json configuration
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\StartupManager.psm1" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Startup Blacklist Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test the blacklist
Invoke-StartupBlacklist

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Test Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration file: Config\startup-blacklist.json" -ForegroundColor Yellow
Write-Host ""
Write-Host "To modify which applications are disabled:" -ForegroundColor Yellow
Write-Host "  1. Edit Config\startup-blacklist.json" -ForegroundColor White
Write-Host "  2. Set 'Enabled: true' to disable an app from startup" -ForegroundColor White
Write-Host "  3. Set 'Enabled: false' to keep an app in startup" -ForegroundColor White
Write-Host "  4. Add new entries to DisabledApplications array" -ForegroundColor White
Write-Host ""
