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
    Version: 2.4.0
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

$script:FrameworkRoot = $PSScriptRoot
$script:Stats = @{
    FilesDeleted = 0
    SpaceFreed = 0
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║      Win11Forge Framework Cleanup & Maintenance v2.5.0     ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

Write-CleanupStatus "Framework path: $script:FrameworkRoot" -Level Info
Write-Host ""

# === LOG CLEANUP ===

if ($CleanLogs) {
    Write-CleanupStatus "Cleaning log files older than $DaysToKeep days..." -Level Info
    
    $logsPath = Join-Path $script:FrameworkRoot 'Logs'
    
    if (Test-Path -Path $logsPath) {
        $cutoffDate = (Get-Date).AddDays(-$DaysToKeep)
        $oldLogs = Get-ChildItem -Path $logsPath -Filter '*.log' |
                   Where-Object { $_.LastWriteTime -lt $cutoffDate }
        
        if ($oldLogs.Count -gt 0) {
            $totalSize = ($oldLogs | Measure-Object -Property Length -Sum).Sum
            
            Write-CleanupStatus "Found $($oldLogs.Count) old log files ($(Format-FileSize $totalSize))" -Level Info
            
            $confirm = Read-Host "Delete these files? (Y/N)"
            if ($confirm -match '^[Yy]') {
                foreach ($log in $oldLogs) {
                    try {
                        Remove-Item -Path $log.FullName -Force
                        $script:Stats.FilesDeleted++
                        $script:Stats.SpaceFreed += $log.Length
                        Write-CleanupStatus "Deleted: $($log.Name)" -Level Success
                    } catch {
                        Write-CleanupStatus "Failed to delete: $($log.Name)" -Level Warning
                    }
                }
            } else {
                Write-CleanupStatus "Log cleanup cancelled" -Level Info
            }
        } else {
            Write-CleanupStatus "No old log files found" -Level Info
        }
    } else {
        Write-CleanupStatus "Logs directory not found" -Level Warning
    }
    
    Write-Host ""
}

# === TEMP FILE CLEANUP ===

if ($CleanTemp) {
    Write-CleanupStatus "Cleaning temporary files..." -Level Info
    
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
            Write-CleanupStatus "Found $($frameworkTemp.Count) Win11Forge temp items in $tempPath" -Level Info
            
            foreach ($item in $frameworkTemp) {
                try {
                    if ($item.PSIsContainer) {
                        Remove-Item -Path $item.FullName -Recurse -Force
                    } else {
                        Remove-Item -Path $item.FullName -Force
                    }
                    $script:Stats.FilesDeleted++
                    Write-CleanupStatus "Deleted: $($item.Name)" -Level Success
                } catch {
                    Write-CleanupStatus "Failed to delete: $($item.Name)" -Level Warning
                }
            }
        }
    }
    
    # Clean Chocolatey cache
    if (Test-Path 'C:\ProgramData\chocolatey\cache') {
        $chocoCache = Get-ChildItem -Path 'C:\ProgramData\chocolatey\cache' -ErrorAction SilentlyContinue
        if ($chocoCache.Count -gt 0) {
            $cacheSize = ($chocoCache | Measure-Object -Property Length -Sum).Sum
            Write-CleanupStatus "Chocolatey cache: $($chocoCache.Count) files ($(Format-FileSize $cacheSize))" -Level Info
            
            $confirm = Read-Host "Clear Chocolatey cache? (Y/N)"
            if ($confirm -match '^[Yy]') {
                try {
                    Remove-Item -Path 'C:\ProgramData\chocolatey\cache\*' -Force -Recurse
                    $script:Stats.SpaceFreed += $cacheSize
                    Write-CleanupStatus "Chocolatey cache cleared" -Level Success
                } catch {
                    Write-CleanupStatus "Failed to clear Chocolatey cache" -Level Warning
                }
            }
        }
    }
    
    Write-Host ""
}

# === CONFIGURATION RESET ===

if ($ResetConfig) {
    Write-CleanupStatus "Resetting framework configuration..." -Level Warning
    Write-Host ""
    Write-Host "This will:" -ForegroundColor Yellow
    Write-Host "  • Backup current profiles to Profiles/Backup_TIMESTAMP"
    Write-Host "  • Reset to default Base profile"
    Write-Host "  • Keep all installed applications"
    Write-Host ""
    
    $confirm = Read-Host "Continue with configuration reset? (Y/N)"
    
    if ($confirm -match '^[Yy]') {
        $profilesPath = Join-Path $script:FrameworkRoot 'Profiles'
        
        if (Test-Path -Path $profilesPath) {
            # Create backup
            $backupPath = Join-Path $profilesPath "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            
            try {
                New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
                Copy-Item -Path "$profilesPath\*.json" -Destination $backupPath -Force
                Write-CleanupStatus "Configuration backed up to: $backupPath" -Level Success
            } catch {
                Write-CleanupStatus "Failed to backup configuration" -Level Error
                return
            }
        }
        
        Write-CleanupStatus "Configuration reset complete" -Level Success
    } else {
        Write-CleanupStatus "Configuration reset cancelled" -Level Info
    }
    
    Write-Host ""
}

# === UNINSTALL ===

if ($Uninstall) {
    Write-Host ""
    Write-CleanupStatus "UNINSTALL FRAMEWORK" -Level Error
    Write-Host ""
    Write-Host "⚠️  WARNING: This will completely remove Win11Forge!" -ForegroundColor Red
    Write-Host ""
    Write-Host "This will:" -ForegroundColor Yellow
    Write-Host "  • Delete all framework files and directories"
    Write-Host "  • Remove all logs and temporary files"
    Write-Host "  • Delete desktop shortcuts (if any)"
    Write-Host ""
    Write-Host "This will NOT:" -ForegroundColor Green
    Write-Host "  • Uninstall applications that were installed"
    Write-Host "  • Revert system configuration changes"
    Write-Host "  • Remove Chocolatey or Winget"
    Write-Host ""
    
    $confirm1 = Read-Host "Are you sure you want to uninstall? (yes/no)"
    
    if ($confirm1 -eq 'yes') {
        $confirm2 = Read-Host "Type 'DELETE' to confirm uninstallation"
        
        if ($confirm2 -eq 'DELETE') {
            Write-CleanupStatus "Starting uninstallation..." -Level Warning
            
            # Remove desktop shortcut
            $desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Win11Forge.lnk'
            if (Test-Path -Path $desktopShortcut) {
                Remove-Item -Path $desktopShortcut -Force
                Write-CleanupStatus "Removed desktop shortcut" -Level Success
            }
            
            # Calculate total size
            $totalSize = (Get-ChildItem -Path $script:FrameworkRoot -Recurse -File | 
                         Measure-Object -Property Length -Sum).Sum
            
            Write-CleanupStatus "Framework size: $(Format-FileSize $totalSize)" -Level Info
            Write-Host ""
            
            # Final confirmation
            Write-Host "Last chance to cancel! Press Ctrl+C to abort." -ForegroundColor Red
            Start-Sleep -Seconds 5
            
            Write-CleanupStatus "Removing framework directory..." -Level Warning
            
            try {
                # Move to parent directory
                $parentPath = Split-Path $script:FrameworkRoot -Parent
                Set-Location -Path $parentPath
                
                # Remove framework
                Remove-Item -Path $script:FrameworkRoot -Recurse -Force
                
                Write-Host ""
                Write-CleanupStatus "Framework successfully uninstalled!" -Level Success
                Write-CleanupStatus "Freed $(Format-FileSize $totalSize) of disk space" -Level Success
                Write-Host ""
                Write-Host "Thank you for using Win11Forge!" -ForegroundColor Cyan
                
            } catch {
                Write-CleanupStatus "Failed to uninstall: $($_.Exception.Message)" -Level Error
                Write-CleanupStatus "You may need to manually delete: $script:FrameworkRoot" -Level Warning
            }
            
        } else {
            Write-CleanupStatus "Uninstallation cancelled (confirmation mismatch)" -Level Info
        }
    } else {
        Write-CleanupStatus "Uninstallation cancelled" -Level Info
    }
    
    return
}

# === SUMMARY ===

if ($CleanLogs -or $CleanTemp -or $ResetConfig) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║              Cleanup Summary                                 ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Files deleted:  $($script:Stats.FilesDeleted)"
    Write-Host "  Space freed:    $(Format-FileSize $script:Stats.SpaceFreed)"
    Write-Host ""
    Write-CleanupStatus "Cleanup completed successfully!" -Level Success
} else {
    Write-Host ""
    Write-CleanupStatus "No cleanup operations specified" -Level Info
    Write-Host ""
    Write-Host "Available options:" -ForegroundColor Cyan
    Write-Host "  -CleanLogs       Remove old log files"
    Write-Host "  -CleanTemp       Clean temporary files"
    Write-Host "  -ResetConfig     Reset to default configuration"
    Write-Host "  -Uninstall       Completely remove framework"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\Cleanup-Framework.ps1 -CleanLogs -DaysToKeep 7"
    Write-Host "  .\Cleanup-Framework.ps1 -CleanTemp"
    Write-Host "  .\Cleanup-Framework.ps1 -Uninstall"
}

Write-Host ""
