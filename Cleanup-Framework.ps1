<#
.SYNOPSIS
    Win11Forge Framework Cleanup Script

.DESCRIPTION
    Cleanup and maintenance script for Win11Forge:
    - Remove old log files
    - Clean temporary files
    - Reset framework to clean state
    - Uninstall framework (optional)

.PARAMETER CleanLogs
    Remove log files older than specified days

.PARAMETER DaysToKeep
    Number of days of logs to keep (default: 30)

.PARAMETER CleanTemp
    Clean temporary download files

.PARAMETER ResetConfig
    Reset to default configuration (backup current config)

.PARAMETER Uninstall
    Completely remove the framework

.EXAMPLE
    .\Cleanup-Framework.ps1 -CleanLogs -DaysToKeep 7

.EXAMPLE
    .\Cleanup-Framework.ps1 -CleanTemp

.EXAMPLE
    .\Cleanup-Framework.ps1 -Uninstall

.NOTES
    Author: Julien Bombled
    Version: Dynamic (loaded from Config/version.json)
    Use with caution - some operations are irreversible
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$CleanLogs,

    [Parameter()]
    [int]$DaysToKeep = 30,

    [Parameter()]
    [switch]$CleanTemp,

    [Parameter()]
    [switch]$ResetConfig,

    [Parameter()]
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

# === LOCALIZATION ===

$script:FrameworkRoot = $PSScriptRoot

# Import Localization module
$localizationModule = Join-Path $script:FrameworkRoot 'Core\Localization.psm1'
if (Test-Path $localizationModule) {
    Import-Module $localizationModule -Force
    Initialize-Localization
}

# Helper function for localization (fallback if module not loaded)
function Get-Text {
    param([string]$Key, [hashtable]$Parameters = @{}, [string]$Default = $Key)
    if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
        return Get-LocalizedString -Key $Key -Parameters $Parameters -DefaultValue $Default
    }
    return $Default
}

# === HELPER FUNCTIONS ===

function Write-CleanupStatus {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
    }

    $timestamp = Get-Date -Format 'HH:mm:ss'
    Write-Host "[$timestamp] $Message" -ForegroundColor $color
}

function Format-FileSize {
    param([long]$Bytes)

    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    } elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    } elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    } else {
        return "$Bytes bytes"
    }
}

# === INITIALIZATION ===

$script:Stats = @{
    FilesDeleted = 0
    SpaceFreed = 0
}

# Load version dynamically
$versionInfo = & "$PSScriptRoot\Tools\Get-Win11ForgeVersion.ps1"
$version = $versionInfo.Version

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'cleanup.banner_title' -Parameters @{ Version = $version } -Default "Win11Forge Framework Cleanup & Maintenance v$version")" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

Write-CleanupStatus (Get-Text -Key 'cleanup.framework_path' -Parameters @{ Path = $script:FrameworkRoot } -Default "Framework path: $script:FrameworkRoot") -Level Info
Write-Host ""

# === LOG CLEANUP ===

if ($CleanLogs) {
    Write-CleanupStatus (Get-Text -Key 'cleanup.cleaning_logs' -Parameters @{ Days = $DaysToKeep } -Default "Cleaning log files older than $DaysToKeep days...") -Level Info

    $logsPath = Join-Path $script:FrameworkRoot 'Logs'

    if (Test-Path -Path $logsPath) {
        $cutoffDate = (Get-Date).AddDays(-$DaysToKeep)
        $oldLogs = Get-ChildItem -Path $logsPath -Filter '*.log' |
                   Where-Object { $_.LastWriteTime -lt $cutoffDate }

        if ($oldLogs.Count -gt 0) {
            $totalSize = ($oldLogs | Measure-Object -Property Length -Sum).Sum

            Write-CleanupStatus (Get-Text -Key 'cleanup.found_old_logs' -Parameters @{ Count = $oldLogs.Count; Size = (Format-FileSize $totalSize) } -Default "Found $($oldLogs.Count) old log files ($(Format-FileSize $totalSize))") -Level Info

            $confirm = Read-Host (Get-Text -Key 'cleanup.delete_prompt' -Default 'Delete these files? (Y/N)')
            if ($confirm -match '^[Yy]') {
                foreach ($log in $oldLogs) {
                    try {
                        Remove-Item -Path $log.FullName -Force
                        $script:Stats.FilesDeleted++
                        $script:Stats.SpaceFreed += $log.Length
                        Write-CleanupStatus (Get-Text -Key 'cleanup.deleted_file' -Parameters @{ Name = $log.Name } -Default "Deleted: $($log.Name)") -Level Success
                    } catch {
                        Write-CleanupStatus (Get-Text -Key 'cleanup.delete_failed' -Parameters @{ Name = $log.Name } -Default "Failed to delete: $($log.Name)") -Level Warning
                    }
                }
            } else {
                Write-CleanupStatus (Get-Text -Key 'cleanup.cleanup_cancelled' -Default 'Log cleanup cancelled') -Level Info
            }
        } else {
            Write-CleanupStatus (Get-Text -Key 'cleanup.no_old_logs' -Default 'No old log files found') -Level Info
        }
    } else {
        Write-CleanupStatus (Get-Text -Key 'cleanup.logs_dir_not_found' -Default 'Logs directory not found') -Level Warning
    }

    Write-Host ""
}

# === TEMP FILE CLEANUP ===

if ($CleanTemp) {
    Write-CleanupStatus (Get-Text -Key 'cleanup.cleaning_temp' -Default 'Cleaning temporary files...') -Level Info

    $tempPaths = @(
        $env:TEMP,
        "$env:LOCALAPPDATA\Temp",
        "C:\Windows\Temp"
    )

    foreach ($tempPath in $tempPaths) {
        if (-not (Test-Path -Path $tempPath)) { continue }

        # Clean Win11Forge temp files
        $frameworkTemp = Get-ChildItem -Path $tempPath -Filter 'Win11Forge_*' -ErrorAction SilentlyContinue

        if ($frameworkTemp.Count -gt 0) {
            Write-CleanupStatus (Get-Text -Key 'cleanup.found_temp_items' -Parameters @{ Count = $frameworkTemp.Count; Path = $tempPath } -Default "Found $($frameworkTemp.Count) Win11Forge temp items in $tempPath") -Level Info

            foreach ($item in $frameworkTemp) {
                try {
                    if ($item.PSIsContainer) {
                        Remove-Item -Path $item.FullName -Recurse -Force
                    } else {
                        Remove-Item -Path $item.FullName -Force
                    }
                    $script:Stats.FilesDeleted++
                    Write-CleanupStatus (Get-Text -Key 'cleanup.deleted_file' -Parameters @{ Name = $item.Name } -Default "Deleted: $($item.Name)") -Level Success
                } catch {
                    Write-CleanupStatus (Get-Text -Key 'cleanup.delete_failed' -Parameters @{ Name = $item.Name } -Default "Failed to delete: $($item.Name)") -Level Warning
                }
            }
        }
    }

    # Clean Chocolatey cache
    if (Test-Path 'C:\ProgramData\chocolatey\cache') {
        $chocoCache = Get-ChildItem -Path 'C:\ProgramData\chocolatey\cache' -ErrorAction SilentlyContinue
        if ($chocoCache.Count -gt 0) {
            $cacheSize = ($chocoCache | Measure-Object -Property Length -Sum).Sum
            Write-CleanupStatus (Get-Text -Key 'cleanup.choco_cache_info' -Parameters @{ Count = $chocoCache.Count; Size = (Format-FileSize $cacheSize) } -Default "Chocolatey cache: $($chocoCache.Count) files ($(Format-FileSize $cacheSize))") -Level Info

            $confirm = Read-Host (Get-Text -Key 'cleanup.clear_choco_prompt' -Default 'Clear Chocolatey cache? (Y/N)')
            if ($confirm -match '^[Yy]') {
                try {
                    Remove-Item -Path 'C:\ProgramData\chocolatey\cache\*' -Force -Recurse
                    $script:Stats.SpaceFreed += $cacheSize
                    Write-CleanupStatus (Get-Text -Key 'cleanup.choco_cleared' -Default 'Chocolatey cache cleared') -Level Success
                } catch {
                    Write-CleanupStatus (Get-Text -Key 'cleanup.choco_clear_failed' -Default 'Failed to clear Chocolatey cache') -Level Warning
                }
            }
        }
    }

    Write-Host ""
}

# === CONFIGURATION RESET ===

if ($ResetConfig) {
    Write-CleanupStatus (Get-Text -Key 'cleanup.resetting_config' -Default 'Resetting framework configuration...') -Level Warning
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.reset_will' -Default 'This will:') -ForegroundColor Yellow
    Write-Host "  $(Get-Text -Key 'cleanup.reset_backup' -Default 'Backup current profiles to Profiles/Backup_TIMESTAMP')"
    Write-Host "  $(Get-Text -Key 'cleanup.reset_default' -Default 'Reset to default Base profile')"
    Write-Host "  $(Get-Text -Key 'cleanup.reset_keep_apps' -Default 'Keep all installed applications')"
    Write-Host ""

    $confirm = Read-Host (Get-Text -Key 'cleanup.reset_confirm' -Default 'Continue with configuration reset? (Y/N)')

    if ($confirm -match '^[Yy]') {
        $profilesPath = Join-Path $script:FrameworkRoot 'Profiles'

        if (Test-Path -Path $profilesPath) {
            # Create backup
            $backupPath = Join-Path $profilesPath "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

            try {
                New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
                Copy-Item -Path "$profilesPath\*.json" -Destination $backupPath -Force
                Write-CleanupStatus (Get-Text -Key 'cleanup.config_backed_up' -Parameters @{ Path = $backupPath } -Default "Configuration backed up to: $backupPath") -Level Success
            } catch {
                Write-CleanupStatus (Get-Text -Key 'cleanup.backup_failed' -Default 'Failed to backup configuration') -Level Error
                return
            }
        }

        Write-CleanupStatus (Get-Text -Key 'cleanup.reset_complete' -Default 'Configuration reset complete') -Level Success
    } else {
        Write-CleanupStatus (Get-Text -Key 'cleanup.reset_cancelled' -Default 'Configuration reset cancelled') -Level Info
    }

    Write-Host ""
}

# === UNINSTALL ===

if ($Uninstall) {
    Write-Host ""
    Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_title' -Default 'UNINSTALL FRAMEWORK') -Level Error
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.uninstall_warning' -Default 'WARNING: This will completely remove Win11Forge!') -ForegroundColor Red
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.uninstall_will' -Default 'This will:') -ForegroundColor Yellow
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_delete_files' -Default 'Delete all framework files and directories')"
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_delete_logs' -Default 'Remove all logs and temporary files')"
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_delete_shortcuts' -Default 'Delete desktop shortcuts (if any)')"
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.uninstall_will_not' -Default 'This will NOT:') -ForegroundColor Green
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_keep_apps' -Default 'Uninstall applications that were installed')"
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_keep_config' -Default 'Revert system configuration changes')"
    Write-Host "  $(Get-Text -Key 'cleanup.uninstall_keep_choco' -Default 'Remove Chocolatey or Winget')"
    Write-Host ""

    $confirm1 = Read-Host (Get-Text -Key 'cleanup.uninstall_confirm1' -Default 'Are you sure you want to uninstall? (yes/no)')

    if ($confirm1 -eq 'yes') {
        $confirm2 = Read-Host (Get-Text -Key 'cleanup.uninstall_confirm2' -Default "Type 'DELETE' to confirm uninstallation")

        if ($confirm2 -eq 'DELETE') {
            Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_starting' -Default 'Starting uninstallation...') -Level Warning

            # Remove desktop shortcut
            $desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Win11Forge.lnk'
            if (Test-Path -Path $desktopShortcut) {
                Remove-Item -Path $desktopShortcut -Force
                Write-CleanupStatus (Get-Text -Key 'cleanup.shortcut_removed' -Default 'Removed desktop shortcut') -Level Success
            }

            # Calculate total size
            $totalSize = (Get-ChildItem -Path $script:FrameworkRoot -Recurse -File |
                         Measure-Object -Property Length -Sum).Sum

            Write-CleanupStatus (Get-Text -Key 'cleanup.framework_size' -Parameters @{ Size = (Format-FileSize $totalSize) } -Default "Framework size: $(Format-FileSize $totalSize)") -Level Info
            Write-Host ""

            # Final confirmation
            Write-Host (Get-Text -Key 'cleanup.last_chance' -Default 'Last chance to cancel! Press Ctrl+C to abort.') -ForegroundColor Red
            Start-Sleep -Seconds 5

            Write-CleanupStatus (Get-Text -Key 'cleanup.removing_framework' -Default 'Removing framework directory...') -Level Warning

            try {
                # Move to parent directory
                $parentPath = Split-Path $script:FrameworkRoot -Parent
                Set-Location -Path $parentPath

                # Remove framework
                Remove-Item -Path $script:FrameworkRoot -Recurse -Force

                Write-Host ""
                Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_success' -Default 'Framework successfully uninstalled!') -Level Success
                Write-CleanupStatus (Get-Text -Key 'cleanup.space_freed' -Parameters @{ Size = (Format-FileSize $totalSize) } -Default "Freed $(Format-FileSize $totalSize) of disk space") -Level Success
                Write-Host ""
                Write-Host (Get-Text -Key 'cleanup.thank_you' -Default 'Thank you for using Win11Forge!') -ForegroundColor Cyan

            } catch {
                Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_failed' -Parameters @{ Error = $_.Exception.Message } -Default "Failed to uninstall: $($_.Exception.Message)") -Level Error
                Write-CleanupStatus (Get-Text -Key 'cleanup.manual_delete_hint' -Parameters @{ Path = $script:FrameworkRoot } -Default "You may need to manually delete: $script:FrameworkRoot") -Level Warning
            }

        } else {
            Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_cancelled_mismatch' -Default 'Uninstallation cancelled (confirmation mismatch)') -Level Info
        }
    } else {
        Write-CleanupStatus (Get-Text -Key 'cleanup.uninstall_cancelled' -Default 'Uninstallation cancelled') -Level Info
    }

    return
}

# === SUMMARY ===

if ($CleanLogs -or $CleanTemp -or $ResetConfig) {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "  $(Get-Text -Key 'cleanup.summary_title' -Default 'Cleanup Summary')" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  $(Get-Text -Key 'cleanup.files_deleted' -Default 'Files deleted:')  $($script:Stats.FilesDeleted)"
    Write-Host "  $(Get-Text -Key 'cleanup.space_freed_label' -Default 'Space freed:')    $(Format-FileSize $script:Stats.SpaceFreed)"
    Write-Host ""
    Write-CleanupStatus (Get-Text -Key 'cleanup.cleanup_success' -Default 'Cleanup completed successfully!') -Level Success
} else {
    Write-Host ""
    Write-CleanupStatus (Get-Text -Key 'cleanup.no_operations' -Default 'No cleanup operations specified') -Level Info
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.available_options' -Default 'Available options:') -ForegroundColor Cyan
    Write-Host "  $(Get-Text -Key 'cleanup.option_logs' -Default '-CleanLogs       Remove old log files')"
    Write-Host "  $(Get-Text -Key 'cleanup.option_temp' -Default '-CleanTemp       Clean temporary files')"
    Write-Host "  $(Get-Text -Key 'cleanup.option_config' -Default '-ResetConfig     Reset to default configuration')"
    Write-Host "  $(Get-Text -Key 'cleanup.option_uninstall' -Default '-Uninstall       Completely remove framework')"
    Write-Host ""
    Write-Host (Get-Text -Key 'cleanup.examples' -Default 'Examples:') -ForegroundColor Cyan
    Write-Host "  .\Cleanup-Framework.ps1 -CleanLogs -DaysToKeep 7"
    Write-Host "  .\Cleanup-Framework.ps1 -CleanTemp"
    Write-Host "  .\Cleanup-Framework.ps1 -Uninstall"
}

Write-Host ""
