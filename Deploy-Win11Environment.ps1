<#
.SYNOPSIS
    Win11Forge - Main Deployment Script v2.1 (FIXED - Parallel Support)

.DESCRIPTION
    Orchestrates complete Windows 11 environment deployment with:
    - Environment detection (Sandbox/VM/Physical)
    - Prerequisites installation
    - Profile-based application deployment (Sequential OR Parallel)
    - System configuration
    - Comprehensive logging and reporting

.PARAMETER ProfileName
    Name of the deployment profile (Base, Office, Gaming, Personnel) or path to custom JSON

.PARAMETER TestMode
    Run in test mode without actual installation

.PARAMETER SkipPrerequisites
    Skip prerequisite installation phase

.PARAMETER SkipSystemConfig
    Skip system configuration phase

.PARAMETER Force
    Force reinstallation of already installed applications

.PARAMETER Parallel
    Enable parallel installation (faster, 5 apps at a time)

.PARAMETER MaxParallelJobs
    Maximum number of parallel installations (default: 5)

.EXAMPLE
    .\Deploy-Win11Environment.ps1 -ProfileName "Gaming"

.EXAMPLE
    .\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel -Force

.EXAMPLE
    .\Deploy-Win11Environment.ps1 -ProfileName "Base" -Parallel -MaxParallelJobs 3

.NOTES
    Version: 2.1.0 FIXED
    NEW: Parallel installation support for faster deployment
    FIXED: All bugs from deployment test (empty Write-Log, InheritanceChain.Count)
    Requires: Administrator privileges, PowerShell 5.1+
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProfileName = 'Base',

    [Parameter()]
    [switch]$TestMode,

    [Parameter()]
    [switch]$SkipPrerequisites,

    [Parameter()]
    [switch]$SkipSystemConfig,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [switch]$Parallel,

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$MaxParallelJobs = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# === SCRIPT INITIALIZATION ===

$script:ScriptRoot = $PSScriptRoot
$script:StartTime = Get-Date
$script:DeploymentStats = @{
    TotalApplications = 0
    InstalledSuccessfully = 0
    AlreadyInstalled = 0
    Failed = 0
    Skipped = 0
}

# === LOGGING SETUP ===

$LogDirectory = Join-Path -Path $script:ScriptRoot -ChildPath 'Logs'
if (-not (Test-Path -Path $LogDirectory)) {
    New-Item -Path $LogDirectory -ItemType Directory -Force | Out-Null
}

$LogTimestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogFile = Join-Path -Path $LogDirectory -ChildPath "deployment_$LogTimestamp.log"

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logMessage = "[$timestamp] [$Level] $Message"
    
    $logMessage | Out-File -FilePath $LogFile -Append -Encoding UTF8
    
    switch ($Level) {
        'Info'    { Write-Host $Message -ForegroundColor Cyan }
        'Success' { Write-Host $Message -ForegroundColor Green }
        'Warning' { Write-Host $Message -ForegroundColor Yellow }
        'Error'   { Write-Host $Message -ForegroundColor Red }
    }
}

# === ADMINISTRATOR CHECK ===

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Log -Message "ERROR: Administrator privileges required" -Level 'Error'
    Write-Log -Message "Please run this script as Administrator" -Level 'Error'
    return 1
}

# === MODULE LOADING ===

Write-Log -Message "=== Win11Forge Framework v2.1 ===" -Level 'Info'
Write-Log -Message "PowerShell Version: $($PSVersionTable.PSVersion)" -Level 'Info'
Write-Log -Message "Starting deployment process..." -Level 'Info'

# Vérification de la version pour mode parallèle
if ($Parallel -and $PSVersionTable.PSVersion.Major -lt 7) {
    Write-Log -Message "WARNING: Parallel mode requires PowerShell 7+" -Level 'Warning'
    Write-Log -Message "Current version: $($PSVersionTable.PSVersion)" -Level 'Warning'
    Write-Log -Message "Disabling parallel mode - falling back to sequential installation" -Level 'Warning'
    $Parallel = $false
}

Write-Log -Message "Installation Mode: $(if ($Parallel) { 'PARALLEL (Max ' + $MaxParallelJobs + ' threads)' } else { 'SEQUENTIAL' })" -Level 'Info'
Write-Log -Message "Log file: $LogFile" -Level 'Info'
Write-Host ""

try {
    # Load Core module
    $coreModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Core\Core.psm1'
    if (Test-Path -Path $coreModule) {
        Import-Module -Name $coreModule -Force -Global
        Write-Log -Message "Core module loaded" -Level 'Success'
    } else {
        throw "Core module not found: $coreModule"
    }

    # Load Environment Detection module
    $envModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\EnvironmentDetection.psm1'
    if (Test-Path -Path $envModule) {
        Import-Module -Name $envModule -Force -Global
        Write-Log -Message "Environment Detection module loaded" -Level 'Success'
    } else {
        throw "Environment Detection module not found: $envModule"
    }

    # Load Prerequisites module
    $prereqModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\Prerequisites.psm1'
    if (Test-Path -Path $prereqModule) {
        Import-Module -Name $prereqModule -Force -Global
        Write-Log -Message "Prerequisites module loaded" -Level 'Success'
    } else {
        throw "Prerequisites module not found: $prereqModule"
    }

    # Load Profile Manager module
    $profileModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\ProfileManager.psm1'
    if (Test-Path -Path $profileModule) {
        Import-Module -Name $profileModule -Force -Global
        Write-Log -Message "Profile Manager module loaded" -Level 'Success'
    } else {
        throw "Profile Manager module not found: $profileModule"
    }

    # Load Installation Engine module
    $installModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\InstallationEngine.psm1'
    if (Test-Path -Path $installModule) {
        Import-Module -Name $installModule -Force -Global
        Write-Log -Message "Installation Engine module loaded (v2.1 - Parallel Support)" -Level 'Success'
    } else {
        throw "Installation Engine module not found: $installModule"
    }

    # Load System Configuration module
    $sysconfigModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\SystemConfig.psm1'
    if (Test-Path -Path $sysconfigModule) {
        Import-Module -Name $sysconfigModule -Force -Global
        Write-Log -Message "System Configuration module loaded" -Level 'Success'
    } else {
        throw "System Configuration module not found: $sysconfigModule"
    }

} catch {
    Write-Log -Message "Failed to load required modules: $($_.Exception.Message)" -Level 'Error'
    return 1
}

# FIXED: Use Write-Host for empty lines
Write-Host ""

# === ENVIRONMENT DETECTION ===

Write-Log -Message "=== Environment Detection ===" -Level 'Info'

try {
    $environmentReport = Get-EnvironmentReport
    
    Write-Log -Message "Environment Type: $($environmentReport.EnvironmentType)" -Level 'Info'
    Write-Log -Message "Computer Name: $($environmentReport.ComputerName)" -Level 'Info'
    Write-Log -Message "OS: $($environmentReport.OSVersion) (Build $($environmentReport.OSBuild))" -Level 'Info'
    Write-Log -Message "Manufacturer: $($environmentReport.Manufacturer)" -Level 'Info'
    Write-Log -Message "Model: $($environmentReport.Model)" -Level 'Info'
    Write-Log -Message "Total Memory: $($environmentReport.TotalMemoryGB) GB" -Level 'Info'
    Write-Log -Message "Is Virtual: $($environmentReport.IsVirtual)" -Level 'Info'
    
    $capabilities = $environmentReport.Capabilities
    
    Write-Host ""
    Write-Log -Message "Environment Capabilities:" -Level 'Info'
    Write-Log -Message "  - Can Install Drivers: $($capabilities.CanInstallDrivers)" -Level 'Info'
    Write-Log -Message "  - Can Install Virtualization: $($capabilities.CanInstallVirtualization)" -Level 'Info'
    Write-Log -Message "  - Is Persistent: $($capabilities.IsPersistent)" -Level 'Info'
    Write-Log -Message "  - Recommended Source: $($capabilities.RecommendedPackageSource)" -Level 'Info'
    
} catch {
    Write-Log -Message "Environment detection failed: $($_.Exception.Message)" -Level 'Warning'
    Write-Log -Message "Continuing with default settings..." -Level 'Warning'
}

Write-Host ""

# === PREREQUISITES INSTALLATION ===

if (-not $SkipPrerequisites) {
    Write-Log -Message "=== Prerequisites Installation ===" -Level 'Info'
    
    if ($TestMode) {
        Write-Log -Message "TEST MODE: Skipping actual prerequisite installation" -Level 'Warning'
        $prereqResults = Test-Prerequisites
    } else {
        try {
            $prereqResults = Start-PrerequisitesInstallation -Force:$Force
            
            Write-Host ""
            Write-Log -Message "Prerequisites installation completed" -Level 'Success'
            
            # Check if PowerShell 7 was just installed
            if ($prereqResults.PowerShell7.Installed -and $PSVersionTable.PSVersion.Major -lt 7) {
                Write-Host ""
                Write-Log -Message "IMPORTANT: PowerShell 7 has been installed" -Level 'Warning'
                Write-Log -Message "For optimal performance, please restart this script in PowerShell 7:" -Level 'Warning'
                Write-Log -Message "  pwsh.exe -File `"$PSCommandPath`" -ProfileName `"$ProfileName`" $(if ($Parallel) { '-Parallel' })" -Level 'Warning'
                Write-Host ""
                
                $response = Read-Host "Continue with current PowerShell version? (Y/N)"
                if ($response -ne 'Y') {
                    Write-Log -Message "Deployment cancelled by user" -Level 'Info'
                    return 0
                }
            }
            
        } catch {
            Write-Log -Message "Prerequisites installation failed: $($_.Exception.Message)" -Level 'Error'
            Write-Log -Message "Some applications may fail to install without prerequisites" -Level 'Warning'
        }
    }
} else {
    Write-Log -Message "=== Prerequisites Check ===" -Level 'Info'
    Write-Log -Message "Prerequisites installation skipped (--SkipPrerequisites)" -Level 'Warning'
    $prereqResults = Test-Prerequisites
}

Write-Host ""

# === PROFILE LOADING ===

Write-Log -Message "=== Profile Configuration ===" -Level 'Info'
Write-Log -Message "Loading profile: $ProfileName" -Level 'Info'

try {
    $profilesDirectory = Join-Path -Path $script:ScriptRoot -ChildPath 'Profiles'
    $deploymentProfile = Get-DeploymentProfile -ProfileName $ProfileName -ProfilesDirectory $profilesDirectory
    
    Write-Log -Message "Profile: $($deploymentProfile.Name) v$($deploymentProfile.Version)" -Level 'Success'
    Write-Log -Message "Description: $($deploymentProfile.Description)" -Level 'Info'
    
    # FIXED: Robust handling of InheritanceChain
    if ($deploymentProfile.InheritanceChain) {
        $chainCount = 0
        
        # Check if it's an array
        if ($deploymentProfile.InheritanceChain -is [array]) {
            $chainCount = $deploymentProfile.InheritanceChain.Count
        } elseif ($deploymentProfile.InheritanceChain -is [string]) {
            # Single string value
            $chainCount = 1
        } else {
            # Try to get Count property safely
            try {
                $chainCount = $deploymentProfile.InheritanceChain.Count
            } catch {
                $chainCount = 1
            }
        }
        
        if ($chainCount -gt 1) {
            $chainDisplay = if ($deploymentProfile.InheritanceChain -is [array]) {
                $deploymentProfile.InheritanceChain -join ' -> '
            } else {
                $deploymentProfile.InheritanceChain.ToString()
            }
            Write-Log -Message "Inheritance chain: $chainDisplay" -Level 'Info'
        }
    }
    
    $applications = $deploymentProfile.Applications
    $systemConfig = $deploymentProfile.SystemConfig
    
    $script:DeploymentStats.TotalApplications = $applications.Count
    Write-Log -Message "Total applications to process: $($applications.Count)" -Level 'Info'
    
} catch {
    Write-Log -Message "Failed to load profile: $($_.Exception.Message)" -Level 'Error'
    return 1
}

Write-Host ""

# === APPLICATION INSTALLATION ===

Write-Log -Message "=== Application Installation ===" -Level 'Info'

if ($TestMode) {
    Write-Log -Message "TEST MODE: No applications will be installed" -Level 'Warning'
}

Write-Host ""

if (-not $TestMode) {
    # Mode parallèle ou séquentiel
    if ($Parallel) {
        Write-Log -Message "Using PARALLEL installation mode (Max $MaxParallelJobs concurrent jobs)" -Level 'Info'
        Write-Log -Message "This will significantly reduce deployment time" -Level 'Success'
        Write-Host ""
        
        # Installation parallèle
        $installResults = Install-ApplicationsParallel -Applications $applications -Force:$Force -MaxParallel $MaxParallelJobs
        
        # Traiter les résultats
        foreach ($result in $installResults) {
            if ($result.AlreadyInstalled) {
                $script:DeploymentStats.AlreadyInstalled++
            } elseif ($result.Success) {
                $script:DeploymentStats.InstalledSuccessfully++
            } else {
                $script:DeploymentStats.Failed++
            }
        }
        
    } else {
        Write-Log -Message "Using SEQUENTIAL installation mode (one app at a time)" -Level 'Info'
        Write-Log -Message "TIP: Use -Parallel parameter for faster deployment" -Level 'Warning'
        Write-Host ""
        
        # Installation séquentielle (mode original)
        $sortedApplications = $applications | Sort-Object -Property Priority

        foreach ($app in $sortedApplications) {
            $appName = $app.Name
            $appCategory = $app.Category
            $appRequired = $app.Required
            
            Write-Log -Message "[$($app.Priority)] Processing: $appName ($appCategory)" -Level 'Info'
            
            # Check environment compatibility
            if ($app.EnvironmentRestrictions -and $app.EnvironmentRestrictions.Count -gt 0) {
                $currentEnv = Get-SystemEnvironmentType
                if ($app.EnvironmentRestrictions -contains $currentEnv) {
                    Write-Log -Message "  [SKIP] Skipped: Not compatible with $currentEnv environment" -Level 'Warning'
                    $script:DeploymentStats.Skipped++
                    Write-Host ""
                    continue
                }
            }
            
            # Attempt installation
            $installResult = Install-Application -Application $app -Force:$Force
            
            if ($installResult.AlreadyInstalled) {
                Write-Log -Message "  [OK] Already installed" -Level 'Success'
                $script:DeploymentStats.AlreadyInstalled++
            }
            elseif ($installResult.Success) {
                Write-Log -Message "  [OK] Installed via $($installResult.Method)" -Level 'Success'
                $script:DeploymentStats.InstalledSuccessfully++
            }
            else {
                Write-Log -Message "  [FAIL] Installation failed: $($installResult.Message)" -Level 'Error'
                $script:DeploymentStats.Failed++

                if ($appRequired) {
                    Write-Log -Message "  [WARN] This is a required application!" -Level 'Warning'
                }
            }
            
            Write-Host ""
        }
    }
} else {
    # Test mode - juste afficher ce qui serait installé
    $sortedApplications = $applications | Sort-Object -Property Priority
    foreach ($app in $sortedApplications) {
        $sources = @()
        if ($app.Sources.Winget) { $sources += "Winget:$($app.Sources.Winget)" }
        if ($app.Sources.Chocolatey) { $sources += "Choco:$($app.Sources.Chocolatey)" }
        if ($app.Sources.Store) { $sources += "Store:$($app.Sources.Store)" }
        
        Write-Log -Message "[$($app.Priority)] $($app.Name) - Would install from: $($sources -join ' | ')" -Level 'Info'
    }
}

# === SYSTEM CONFIGURATION ===

if (-not $SkipSystemConfig -and -not $TestMode) {
    Write-Log -Message "=== System Configuration ===" -Level 'Info'
    
    if ($systemConfig -and $systemConfig.Count -gt 0) {
        try {
            # CRITICAL FIX: Convert all PSCustomObject configs to Hashtables
            $convertedConfig = @{}
            foreach ($key in $systemConfig.Keys) {
                if ($systemConfig[$key] -is [PSCustomObject]) {
                    # Use ProfileManager's ConvertTo-Hashtable function
                    $convertedConfig[$key] = ConvertTo-Hashtable -InputObject $systemConfig[$key]
                } elseif ($systemConfig[$key] -is [hashtable]) {
                    $convertedConfig[$key] = $systemConfig[$key]
                } else {
                    $convertedConfig[$key] = $systemConfig[$key]
                }
            }
            
            Set-SystemConfiguration -Config $convertedConfig
        } catch {
            Write-Log -Message "System configuration failed: $($_.Exception.Message)" -Level 'Error'
        }
    } else {
        Write-Log -Message "No system configuration found in profile" -Level 'Info'
    }
    
    Write-Host ""
}

# === START MENU ORGANIZATION ===

if (-not $TestMode) {
    Write-Log -Message "=== Start Menu Organization ===" -Level 'Info'

    try {
        # Import StartMenuLayout module (organizes shortcuts by category)
        $startMenuModule = Join-Path $script:ScriptRoot 'Modules\StartMenuLayout.psm1'
        if (Test-Path $startMenuModule) {
            Import-Module $startMenuModule -Force -WarningAction SilentlyContinue

            Write-Log -Message "Organizing desktop shortcuts in Start Menu by category..." -Level 'Info'
            # Creates category folders in Start Menu Programs
            Invoke-StartMenuOrganization

            Write-Log -Message "Start Menu organization completed" -Level 'Success'
        } else {
            Write-Log -Message "StartMenuLayout module not found, skipping organization" -Level 'Warning'
        }
    }
    catch {
        Write-Log -Message "Error during Start Menu organization: $($_.Exception.Message)" -Level 'Warning'
    }

    Write-Host ""
}

# === START MENU PINNING ===

if (-not $TestMode) {
    Write-Log -Message "=== Start Menu Pinning (start2.bin method) ===" -Level 'Info'

    try {
        # Import StartMenuPinning module (uses start2.bin - reliable Windows 11 method)
        $pinningModule = Join-Path $script:ScriptRoot 'Modules\StartMenuPinning.psm1'
        if (Test-Path $pinningModule) {
            Import-Module $pinningModule -Force -WarningAction SilentlyContinue

            Write-Log -Message "Capturing current Start Menu pinned items..." -Level 'Info'
            # Uses start2.bin/start.bin binary file method (works on Windows 11 22H2+)
            # This is the most reliable method as LayoutModification.json is deprecated
            $pinningResult = Invoke-StartMenuPinning -BackupName "Deployment_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

            if ($pinningResult) {
                Write-Log -Message "Start Menu pinning deployed to Default profile" -Level 'Success'
                Write-Log -Message "New user accounts will inherit current pinned items" -Level 'Info'
            } else {
                Write-Log -Message "Start Menu pinning deployment failed or was skipped" -Level 'Warning'
            }
        } else {
            Write-Log -Message "StartMenuPinning module not found, skipping pinning" -Level 'Warning'
        }
    }
    catch {
        Write-Log -Message "Error during Start Menu pinning: $($_.Exception.Message)" -Level 'Warning'
    }

    Write-Host ""
}

# === STARTUP APPLICATIONS MANAGEMENT ===

if (-not $TestMode) {
    Write-Log -Message "=== Startup Applications Management ===" -Level 'Info'

    try {
        # Import StartupManager module
        $startupModule = Join-Path $script:ScriptRoot 'Modules\StartupManager.psm1'
        if (Test-Path $startupModule) {
            Import-Module $startupModule -Force -WarningAction SilentlyContinue

            Write-Log -Message "Applying startup blacklist configuration..." -Level 'Info'
            # Reads Config\startup-blacklist.json and disables configured applications
            Invoke-StartupBlacklist

            Write-Log -Message "Startup management completed" -Level 'Success'
        } else {
            Write-Log -Message "StartupManager module not found, skipping startup management" -Level 'Warning'
        }
    }
    catch {
        Write-Log -Message "Error during startup management: $($_.Exception.Message)" -Level 'Warning'
    }

    Write-Host ""
}

# === DEPLOYMENT SUMMARY ===

$script:EndTime = Get-Date
$duration = $script:EndTime - $script:StartTime

Write-Log -Message "=== Deployment Summary ===" -Level 'Success'
Write-Log -Message "Profile: $ProfileName" -Level 'Info'
Write-Log -Message "Environment: $($environmentReport.EnvironmentType)" -Level 'Info'
Write-Log -Message "Installation Mode: $(if ($Parallel) { 'Parallel' } else { 'Sequential' })" -Level 'Info'
Write-Log -Message "Start Time: $($script:StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -Level 'Info'
Write-Log -Message "End Time: $($script:EndTime.ToString('yyyy-MM-dd HH:mm:ss'))" -Level 'Info'
Write-Log -Message "Duration: $($duration.ToString('hh\:mm\:ss'))" -Level 'Info'
Write-Host ""

if (-not $TestMode) {
    Write-Log -Message "Applications Statistics:" -Level 'Info'
    Write-Log -Message "  Total: $($script:DeploymentStats.TotalApplications)" -Level 'Info'
    Write-Log -Message "  Installed: $($script:DeploymentStats.InstalledSuccessfully)" -Level 'Success'
    Write-Log -Message "  Already Installed: $($script:DeploymentStats.AlreadyInstalled)" -Level 'Info'
    Write-Log -Message "  Skipped: $($script:DeploymentStats.Skipped)" -Level 'Warning'
    Write-Log -Message "  Failed: $($script:DeploymentStats.Failed)" -Level 'Error'
}

Write-Host ""
Write-Log -Message "Log file: $LogFile" -Level 'Info'
Write-Log -Message "Deployment completed successfully!" -Level 'Success'

# Return to caller instead of exiting (for GUI compatibility)
return 0