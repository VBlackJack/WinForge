<#
.SYNOPSIS
    Win11Forge - Main Deployment Script v3.7.1

.DESCRIPTION
    Orchestrates complete Windows 11 environment deployment with:
    - Environment detection (Sandbox/VM/Physical)
    - Prerequisites installation (auto-restart in PowerShell 7 if needed)
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
    Author: Julien Bombled
    Version: 3.6.8
    Fixed: Auto-restart in PowerShell 7 after prerequisites installation
    Fixed: PowerShell 5.1 StrictMode compatibility
    Requires: Administrator privileges, PowerShell 5.1+
#>

#
# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

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

# Import Localization module early for i18n support
$script:LocalizationModulePath = Join-Path $script:ScriptRoot 'Core\Localization.psm1'
if (Test-Path -Path $script:LocalizationModulePath) {
    Import-Module -Name $script:LocalizationModulePath -Force -ErrorAction SilentlyContinue
}
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
    Write-Log -Message (Get-LocalizedString -Key 'core.admin_required') -Level 'Error'
    Write-Log -Message (Get-LocalizedString -Key 'core.admin_run_as') -Level 'Error'
    return 1
}

# === MODULE LOADING ===

# Load framework version dynamically
$frameworkVersion = try {
    $versionPath = Join-Path $PSScriptRoot 'Config\version.json'
    if (Test-Path $versionPath) {
        $versionData = Get-Content -Path $versionPath -Raw | ConvertFrom-Json
        $versionData.Version
    } else { '3.6.8' }
} catch { '3.6.8' }

Write-Log -Message "=== $(Get-LocalizedString -Key 'setup.banner_title' -Params @{ Version = $frameworkVersion }) ===" -Level 'Info'
Write-Log -Message (Get-LocalizedString -Key 'setup.ps_version' -Params @{ Version = $PSVersionTable.PSVersion }) -Level 'Info'
Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.starting') -Level 'Info'

# Vérification de la version pour mode parallèle
if ($Parallel -and $PSVersionTable.PSVersion.Major -lt 7) {
    Write-Log -Message (Get-LocalizedString -Key 'parallel.requires_ps7') -Level 'Warning'
    Write-Log -Message (Get-LocalizedString -Key 'parallel.current_version' -Params @{ Version = $PSVersionTable.PSVersion }) -Level 'Warning'
    Write-Log -Message (Get-LocalizedString -Key 'parallel.fallback_sequential') -Level 'Warning'
    $Parallel = $false
}

$modeText = if ($Parallel) {
    "$(Get-LocalizedString -Key 'gui.deploy.mode_name_parallel') (Max $MaxParallelJobs threads)"
} else {
    Get-LocalizedString -Key 'gui.deploy.mode_name_sequential'
}
Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.mode_label' -Params @{ Mode = $modeText }) -Level 'Info'
Write-Log -Message (Get-LocalizedString -Key 'launcher.log_file_path' -Params @{ Path = $LogFile }) -Level 'Info'
Write-Host ""

try {
    # Load Core module
    $coreModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Core\Core.psm1'
    if (Test-Path -Path $coreModule) {
        Import-Module -Name $coreModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'Core' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $coreModule })
    }

    # Load Environment Detection module
    $envModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\EnvironmentDetection.psm1'
    if (Test-Path -Path $envModule) {
        Import-Module -Name $envModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'EnvironmentDetection' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $envModule })
    }

    # Load Prerequisites module
    $prereqModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\Prerequisites.psm1'
    if (Test-Path -Path $prereqModule) {
        Import-Module -Name $prereqModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'Prerequisites' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $prereqModule })
    }

    # Load Profile Manager module
    $profileModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\ProfileManager.psm1'
    if (Test-Path -Path $profileModule) {
        Import-Module -Name $profileModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'ProfileManager' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $profileModule })
    }

    # Load Installation Engine module
    $installModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\InstallationEngine.psm1'
    if (Test-Path -Path $installModule) {
        Import-Module -Name $installModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'InstallationEngine' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $installModule })
    }

    # Load System Configuration module
    $sysconfigModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\SystemConfig.psm1'
    if (Test-Path -Path $sysconfigModule) {
        Import-Module -Name $sysconfigModule -Force -Global
        Write-Log -Message (Get-LocalizedString -Key 'launcher.module_loaded_specific' -Params @{ Name = 'SystemConfig' }) -Level 'Success'
    } else {
        throw (Get-LocalizedString -Key 'launcher.module_missing' -Params @{ Path = $sysconfigModule })
    }

} catch {
    Write-Log -Message (Get-LocalizedString -Key 'launcher.load_failed' -Params @{ Error = $_.Exception.Message }) -Level 'Error'
    return 1
}

# FIXED: Use Write-Host for empty lines
Write-Host ""

# === ENVIRONMENT DETECTION ===

Write-Log -Message "=== Environment Detection ===" -Level 'Info'

try {
    $environmentReport = Get-EnvironmentReport

    if ($environmentReport) {
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
    } else {
        throw "Get-EnvironmentReport returned null"
    }

} catch {
    Write-Log -Message (Get-LocalizedString -Key 'launcher.env_detection_failed' -Params @{ Error = $_.Exception.Message }) -Level 'Warning'
    Write-Log -Message (Get-LocalizedString -Key 'launcher.continuing_defaults') -Level 'Warning'

    # Create fallback environment report to prevent null reference errors
    $environmentReport = [PSCustomObject]@{
        EnvironmentType = 'Unknown'
        ComputerName = $env:COMPUTERNAME
        OSVersion = 'Unknown'
        OSBuild = 'Unknown'
        Manufacturer = 'Unknown'
        Model = 'Unknown'
        TotalMemoryGB = 0
        IsVirtual = $false
        Capabilities = @{
            CanInstallDrivers = $true
            CanInstallVirtualization = $true
            IsPersistent = $true
            RecommendedPackageSource = 'Winget'
        }
    }
}

Write-Host ""

# === PREREQUISITES INSTALLATION ===

if (-not $SkipPrerequisites) {
    Write-Log -Message "=== $(Get-LocalizedString -Key 'prerequisites.title') ===" -Level 'Info'

    if ($TestMode) {
        Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.test_mode') -Level 'Warning'
        $prereqResults = Test-Prerequisites
    } else {
        try {
            $prereqResults = Start-PrerequisitesInstallation -Force:$Force

            Write-Host ""
            Write-Log -Message (Get-LocalizedString -Key 'prerequisites.workflow.completed') -Level 'Success'

            # Check if PowerShell 7 was just installed
            if ($prereqResults.PowerShell7.Installed -and $PSVersionTable.PSVersion.Major -lt 7) {
                Write-Host ""
                Write-Log -Message (Get-LocalizedString -Key 'prerequisites.powershell.completed') -Level 'Success'
                Write-Log -Message (Get-LocalizedString -Key 'prerequisites.powershell.restart_required') -Level 'Info'
                Write-Host ""

                # Build restart command
                $pwshPath = 'pwsh.exe'
                $restartArgs = @(
                    '-ExecutionPolicy', 'Bypass',
                    '-File', $PSCommandPath,
                    '-ProfileName', $ProfileName
                )
                if ($Parallel) { $restartArgs += '-Parallel' }
                if ($Force) { $restartArgs += '-Force' }
                if ($TestMode) { $restartArgs += '-TestMode' }
                if ($SkipPrerequisites) { $restartArgs += '-SkipPrerequisites' }

                Write-Log -Message "Restarting with: pwsh.exe $($restartArgs -join ' ')" -Level 'Verbose'

                # Restart in PowerShell 7
                Start-Process -FilePath $pwshPath -ArgumentList $restartArgs -NoNewWindow -Wait

                Write-Log -Message "PowerShell 7 deployment completed" -Level 'Success'
                return 0
            }

        } catch {
            Write-Log -Message (Get-LocalizedString -Key 'prerequisites.workflow.failed' -Params @{ Error = $_.Exception.Message }) -Level 'Error'
            Write-Log -Message (Get-LocalizedString -Key 'prerequisites.some_missing') -Level 'Warning'
        }
    }
} else {
    Write-Log -Message "=== $(Get-LocalizedString -Key 'prerequisites.checking') ===" -Level 'Info'
    Write-Log -Message (Get-LocalizedString -Key 'prerequisites.all_passed') -Level 'Warning'
    $prereqResults = Test-Prerequisites
}

Write-Host ""

# === PROFILE LOADING ===

Write-Log -Message "=== $(Get-LocalizedString -Key 'profile.loading' -Params @{ Name = $ProfileName }) ===" -Level 'Info'

try {
    $profilesDirectory = Join-Path -Path $script:ScriptRoot -ChildPath 'Profiles'
    $deploymentProfile = Get-DeploymentProfile -ProfileName $ProfileName -ProfilesDirectory $profilesDirectory

    Write-Log -Message (Get-LocalizedString -Key 'profile.loaded' -Params @{ Name = $deploymentProfile.Name; AppCount = $deploymentProfile.Applications.Count }) -Level 'Success'
    Write-Log -Message (Get-LocalizedString -Key 'gui.profiles.desc_label' -Params @{ Description = $deploymentProfile.Description }) -Level 'Info'

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
            Write-Log -Message (Get-LocalizedString -Key 'profile.inheritance.inherits_from' -Params @{ Parent = $chainDisplay }) -Level 'Info'
        }
    }

    $applications = $deploymentProfile.Applications
    $systemConfig = $deploymentProfile.SystemConfig

    $script:DeploymentStats.TotalApplications = $applications.Count
    Write-Log -Message (Get-LocalizedString -Key 'profile.applications.total' -Params @{ Count = $applications.Count }) -Level 'Info'

} catch {
    Write-Log -Message (Get-LocalizedString -Key 'profile.not_found' -Params @{ Name = $ProfileName }) -Level 'Error'
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
        Write-Log -Message (Get-LocalizedString -Key 'parallel.title') -Level 'Info'
        Write-Log -Message (Get-LocalizedString -Key 'parallel.max_threads' -Params @{ Count = $MaxParallelJobs }) -Level 'Success'
        Write-Host ""

        # Installation parallèle
        $installResults = Install-ApplicationsParallel -Applications $applications -Force:$Force -MaxParallel $MaxParallelJobs

        # Traiter les résultats
        foreach ($result in $installResults) {
            # Check if Skipped property exists and is true
            if ($result.PSObject.Properties['Skipped'] -and $result.Skipped) {
                # App skipped due to environment restrictions
                $script:DeploymentStats.Skipped++
            } elseif ($result.AlreadyInstalled) {
                $script:DeploymentStats.AlreadyInstalled++
            } elseif ($result.Success) {
                $script:DeploymentStats.InstalledSuccessfully++
            } else {
                $script:DeploymentStats.Failed++
            }
        }

    } else {
        Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.mode_sequential') -Level 'Info'
        Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.mode_parallel') -Level 'Warning'
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
                    Write-Log -Message "  [SKIP] $(Get-LocalizedString -Key 'install.skipping_environment' -Params @{ AppName = $appName; Environment = $currentEnv })" -Level 'Warning'
                    $script:DeploymentStats.Skipped++
                    Write-Host ""
                    continue
                }
            }

            # Attempt installation
            $installResult = Install-Application -Application $app -Force:$Force

            if ($installResult.AlreadyInstalled) {
                Write-Log -Message "  [OK] $(Get-LocalizedString -Key 'install.already_installed' -Params @{ AppName = $appName })" -Level 'Success'
                $script:DeploymentStats.AlreadyInstalled++
            }
            elseif ($installResult.Success) {
                Write-Log -Message "  [OK] $(Get-LocalizedString -Key 'install.completed' -Params @{ AppName = $appName })" -Level 'Success'
                $script:DeploymentStats.InstalledSuccessfully++
            }
            else {
                Write-Log -Message "  [FAIL] $(Get-LocalizedString -Key 'install.failed' -Params @{ AppName = $appName; Message = $installResult.Message })" -Level 'Error'
                $script:DeploymentStats.Failed++

                if ($appRequired) {
                    Write-Log -Message "  [WARN] $(Get-LocalizedString -Key 'profile.applications.required' -Params @{ Count = 1 })" -Level 'Warning'
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
            # ApplyToCurrentUser: Also applies the layout to the deployment user account
            $pinningResult = Invoke-StartMenuPinning -BackupName "Deployment_$(Get-Date -Format 'yyyyMMdd_HHmmss')" -ApplyToCurrentUser

            if ($pinningResult) {
                Write-Log -Message "Start Menu pinning deployed to Default profile" -Level 'Success'
                Write-Log -Message "Layout also applied to current user account" -Level 'Success'
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

Write-Log -Message "=== $(Get-LocalizedString -Key 'parallel.summary.title') ===" -Level 'Success'
Write-Log -Message "Profile: $ProfileName" -Level 'Info'
Write-Log -Message (Get-LocalizedString -Key 'system.info.environment' -Params @{ Type = $environmentReport.EnvironmentType }) -Level 'Info'
$summaryMode = if ($Parallel) { Get-LocalizedString -Key 'gui.deploy.mode_name_parallel' } else { Get-LocalizedString -Key 'gui.deploy.mode_name_sequential' }
Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.mode_label' -Params @{ Mode = $summaryMode }) -Level 'Info'
Write-Log -Message (Get-LocalizedString -Key 'parallel.summary.total_time' -Params @{ Time = $duration.ToString('hh\:mm\:ss') }) -Level 'Info'
Write-Host ""

if (-not $TestMode) {
    Write-Log -Message (Get-LocalizedString -Key 'parallel.summary.apps_processed' -Params @{ Count = $script:DeploymentStats.TotalApplications }) -Level 'Info'
    Write-Log -Message "  $(Get-LocalizedString -Key 'common.success'): $($script:DeploymentStats.InstalledSuccessfully)" -Level 'Success'
    Write-Log -Message "  $(Get-LocalizedString -Key 'common.skipped'): $($script:DeploymentStats.AlreadyInstalled + $script:DeploymentStats.Skipped)" -Level 'Info'
    Write-Log -Message "  $(Get-LocalizedString -Key 'common.failed'): $($script:DeploymentStats.Failed)" -Level 'Error'
}

Write-Host ""
Write-Log -Message (Get-LocalizedString -Key 'launcher.log_file_path' -Params @{ Path = $LogFile }) -Level 'Info'

# Determine overall deployment status and set exit code
if ($script:DeploymentStats.Failed -gt 0) {
    Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.completed_with_failures' -Params @{ Code = $script:DeploymentStats.Failed }) -Level 'Error'
    $exitCode = 1
} elseif ($script:DeploymentStats.Skipped -gt 0) {
    Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.completed') -Level 'Warning'
    $exitCode = 0
} else {
    Write-Log -Message (Get-LocalizedString -Key 'gui.deploy.completed') -Level 'Success'
    $exitCode = 0
}

# Set exit code for callers
$global:LASTEXITCODE = $exitCode

# Detect if script was invoked with & operator (GUI/scripted call) or direct execution
# When called via &, we should return to allow caller to continue
# When called directly (powershell -File), we should exit to set process exit code
$invokedViaCallOperator = $MyInvocation.InvocationName -ne $MyInvocation.MyCommand.Path

if ($invokedViaCallOperator) {
    # Called via & operator from GUI or another script - return to caller
    return $exitCode
} else {
    # Direct execution (batch file, command line) - exit to set process exit code
    exit $exitCode
}
