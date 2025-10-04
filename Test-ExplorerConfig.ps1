<#
.SYNOPSIS
    Test Explorer Configuration
.DESCRIPTION
    Tests the enhanced Explorer configuration options
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\SystemConfig.psm1" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Explorer Configuration Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create test configuration
$explorerConfig = @{
    ShowHiddenFiles = $true
    ShowFileExtensions = $true
    NavigationPaneOptimized = $true
    ShowLibraries = $true
    ExpandToOpenFolder = $true
    ShowSyncProviderNotifications = $true
    ShowFullPathInTitleBar = $false
}

Write-Host "Applying Explorer Configuration:" -ForegroundColor Yellow
Write-Host "  - Show Hidden Files: $($explorerConfig.ShowHiddenFiles)" -ForegroundColor White
Write-Host "  - Show File Extensions: $($explorerConfig.ShowFileExtensions)" -ForegroundColor White
Write-Host "  - Show All Folders (Navigation Pane): $($explorerConfig.NavigationPaneOptimized)" -ForegroundColor White
Write-Host "  - Show Libraries: $($explorerConfig.ShowLibraries)" -ForegroundColor White
Write-Host "  - Expand to Open Folder: $($explorerConfig.ExpandToOpenFolder)" -ForegroundColor White
Write-Host "  - Show Availability Status: $($explorerConfig.ShowSyncProviderNotifications)" -ForegroundColor White
Write-Host ""

# Apply configuration
Set-ExplorerConfiguration -Config $explorerConfig

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Configuration Applied!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Explorer has been restarted to apply changes." -ForegroundColor Yellow
Write-Host ""
