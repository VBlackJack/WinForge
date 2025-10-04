# ==============================================================================
# Launch Windows Program with TrustedInstaller Privileges and GUI Display
# PowerShell Script for Windows 11 24H2
# Uses built-in Windows tools only
# Author: Julien Bombled (based on Perplexity Pro research)
# Version: 1.0.0
# ==============================================================================

param(
    [Parameter(Position=0)]
    [string]$Program = "cmd.exe",

    [Parameter()]
    [string]$Arguments = ""
)

# Require Administrator privileges
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run with Administrator privileges."
    exit 1
}

Write-Host "TrustedInstaller GUI Launcher for Windows 11 24H2" -ForegroundColor Green
Write-Host "Target Program: $Program $Arguments" -ForegroundColor Yellow
Write-Host "Uses NtObjectManager PowerShell module" -ForegroundColor Gray

function Invoke-TrustedInstallerProcess {
    param(
        [string]$ExecutablePath,
        [string]$ExecutableArgs
    )

    try {
        # Check if NtObjectManager module is installed
        Write-Host "Checking for NtObjectManager module..."
        if (-not (Get-Module -ListAvailable -Name NtObjectManager)) {
            Write-Host "Installing NtObjectManager module..." -ForegroundColor Yellow
            Install-Module -Name NtObjectManager -Force -Scope CurrentUser -AllowClobber -SkipPublisherCheck -Confirm:$false -ErrorAction Stop
            Write-Host "Module installed successfully" -ForegroundColor Green
        }

        # Import the module
        Import-Module NtObjectManager -ErrorAction Stop
        Write-Host "Module loaded successfully"

        # Get TrustedInstaller process
        Write-Host "Getting TrustedInstaller parent process..."
        $parent = Get-NtProcess -ServiceName TrustedInstaller -ErrorAction Stop

        # Handle .msc files (need to be launched via mmc.exe)
        if ($ExecutablePath -match '\.msc$') {
            $mscFile = $ExecutablePath
            $ExecutablePath = "mmc.exe"
            $ExecutableArgs = "`"$mscFile`" $ExecutableArgs".Trim()
            Write-Host "Detected .msc file, launching via mmc.exe" -ForegroundColor Yellow
        }

        # Build command line
        $commandLine = if ($ExecutableArgs) {
            "$ExecutablePath $ExecutableArgs"
        } else {
            $ExecutablePath
        }

        Write-Host "Launching: $commandLine" -ForegroundColor Cyan

        # Create new process with TrustedInstaller as parent
        $proc = New-Win32Process -CommandLine $commandLine -CreationFlags NewConsole -ParentProcess $parent -ErrorAction Stop

        Write-Host "Process launched successfully!" -ForegroundColor Green
        Write-Host "Process ID: $($proc.Pid)" -ForegroundColor Cyan
        Write-Host "Running as: NT AUTHORITY\SYSTEM (with TrustedInstaller privileges)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Failed to launch process: $($_.Exception.Message)"
        return $false
    }
    finally {
        # Cleanup parent process handle if it exists
        if ($parent) {
            $parent.Dispose()
        }
    }
}

function Start-TrustedInstallerProcess {
    param(
        [string]$ExecutablePath,
        [string]$ExecutableArgs
    )

    Write-Host "`nStarting TrustedInstaller process launcher..." -ForegroundColor Cyan

    # Start TrustedInstaller service if not running
    $tiService = Get-Service -Name "TrustedInstaller" -ErrorAction SilentlyContinue
    if ($tiService.Status -ne "Running") {
        Write-Host "Starting TrustedInstaller service..."
        Start-Service -Name "TrustedInstaller" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    # Launch using NtObjectManager method
    $success = Invoke-TrustedInstallerProcess -ExecutablePath $ExecutablePath -ExecutableArgs $ExecutableArgs

    if ($success) {
        Write-Host "`nSUCCESS: Program launched with TrustedInstaller privileges!" -ForegroundColor Green
        Write-Host "The program window should appear in the current user session."
        Write-Host "Note: 'whoami' will show 'NT AUTHORITY\SYSTEM' (TrustedInstaller runs as SYSTEM)"
    } else {
        Write-Error "Failed to launch program with TrustedInstaller privileges."
        return 1
    }

    return 0
}

# Main execution
try {
    $exitCode = Start-TrustedInstallerProcess -ExecutablePath $Program -ExecutableArgs $Arguments
    exit $exitCode
}
catch {
    Write-Error "Script execution failed: $($_.Exception.Message)"
    exit 1
}
