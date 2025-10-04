<#
.SYNOPSIS
    Launch Startup Manager HTML Tool
.DESCRIPTION
    Opens the Startup Manager HTML interface in default browser
.NOTES
    Author: Julien Bombled
#>

$htmlPath = Join-Path $PSScriptRoot "StartupManager.html"

if (-not (Test-Path $htmlPath)) {
    Write-Host "Error: StartupManager.html not found at $htmlPath" -ForegroundColor Red
    exit 1
}

Write-Host "Opening Startup Manager in browser..." -ForegroundColor Cyan
Start-Process $htmlPath

Write-Host ""
Write-Host "Startup Manager is now open in your browser." -ForegroundColor Green
Write-Host "Use the interface to select applications to disable from startup." -ForegroundColor Yellow
Write-Host "When done, download the config and copy to:" -ForegroundColor Yellow
Write-Host "  C:\sys\Win11Forge\Config\startup-blacklist.json" -ForegroundColor White
