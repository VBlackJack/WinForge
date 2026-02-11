<#
.SYNOPSIS
    Win11Forge - Installation Orchestrator v3.7.1

.DESCRIPTION
    High-level orchestration logic for application installation.

    This module coordinates installation across multiple sources:
    - Sequential installation with fallback (Winget -> Chocolatey -> Store -> DirectDownload)
    - Parallel installation for PowerShell 7+
    - Rollback execution (state tracking delegated to StateManager.psm1)
    - Environment restriction checking

    Works in conjunction with:
    - ApplicationDetection.psm1: Detection and verification functions
    - InstallationMethods.psm1: Individual installation method implementations

.NOTES
    Author: Julien Bombled
    v3.7.1

    Changelog v3.2.2:
    - ARCHITECTURE: Extracted from InstallationEngine.psm1 for improved maintainability
    - ARCHITECTURE: Contains only orchestration logic (Install-Application, Install-ApplicationsParallel)
    - ARCHITECTURE: State management (rollback, deployment resume) centralized here
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

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent

# Import required modules
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
$script:EnvironmentDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

if (-not (Get-Command -Name Test-IsWindowsSandbox -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:EnvironmentDetectionPath) {
        Import-Module -Name $script:EnvironmentDetectionPath -Force
    }
}

# Import sibling modules
$script:ApplicationDetectionPath = Join-Path $script:ModuleRoot 'ApplicationDetection.psm1'
if (-not (Get-Command -Name Test-ApplicationInstalled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:ApplicationDetectionPath) {
        Import-Module -Name $script:ApplicationDetectionPath -Force
    }
}

$script:InstallationMethodsPath = Join-Path $script:ModuleRoot 'InstallationMethods.psm1'
if (-not (Get-Command -Name Install-ViaWinget -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:InstallationMethodsPath) {
        Import-Module -Name $script:InstallationMethodsPath -Force
    }
}

$script:WingetCachePath = Join-Path $script:ModuleRoot 'WingetCache.psm1'
if (-not (Get-Command -Name Get-CachedWingetList -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:WingetCachePath) {
        Import-Module -Name $script:WingetCachePath -Force
    }
}

# === TIMEOUT CONFIGURATION ===
# Import TimeoutSettings and StateManager modules for centralized configuration
$script:TimeoutSettingsPath = Join-Path $script:RepositoryRoot 'Core\TimeoutSettings.psm1'
$script:StateManagerPath = Join-Path $script:ModuleRoot 'StateManager.psm1'

if (Test-Path -Path $script:TimeoutSettingsPath) {
    Import-Module -Name $script:TimeoutSettingsPath -Force -ErrorAction SilentlyContinue
}

if (Test-Path -Path $script:StateManagerPath) {
    Import-Module -Name $script:StateManagerPath -Force -ErrorAction SilentlyContinue
}

# Helper functions to get configured timeouts (with fallbacks)
function script:Get-ConfiguredMaxParallelJobs {
    if (Get-Command -Name Get-MaxParallelJobs -ErrorAction SilentlyContinue) {
        return Get-MaxParallelJobs
    }
    return 5  # Fallback default
}

function script:Get-ConfiguredParallelTimeout {
    if (Get-Command -Name Get-ParallelTimeout -ErrorAction SilentlyContinue) {
        return Get-ParallelTimeout
    }
    return 600000  # 10 minutes fallback
}

function script:Get-ConfiguredJobCheckInterval {
    $config = $null
    if (Get-Command -Name Get-TimeoutSettings -ErrorAction SilentlyContinue) {
        $config = Get-TimeoutSettings
    }
    if ($config -and $config.Parallel.JobCheckIntervalSeconds) {
        return $config.Parallel.JobCheckIntervalSeconds
    }
    return 2  # Fallback default
}

# === STATE MANAGEMENT ===
# Rollback and deployment state functions are provided by StateManager.psm1
# (imported above at line 104-106). Only orchestration-specific functions
# that add UI/coordination logic beyond pure state management are defined here.

function Invoke-Rollback {
    <#
    .SYNOPSIS
        Rolls back installed applications from the current session.
    .DESCRIPTION
        Uninstalls applications that were installed during the current deployment session.
        Supports Winget and Chocolatey uninstallation methods.
        Delegates state tracking to StateManager.psm1.
    .PARAMETER Force
        Skip confirmation prompts.
    .OUTPUTS
        [hashtable] with Success, RolledBack, and Failed arrays.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [switch]$Force
    )

    $result = @{
        Success = $true
        RolledBack = @()
        Failed = @()
    }

    $rollbackState = Get-RollbackState
    if ($rollbackState.InstalledApps.Count -eq 0) {
        Write-Status -Message (Get-LocalizedString -Key 'rollback.no_apps') -Level 'Info'
        return $result
    }

    Write-Status -Message (Get-LocalizedString -Key 'rollback.rolling_back' -Parameters @{ Count = $rollbackState.InstalledApps.Count }) -Level 'Info'

    foreach ($app in $rollbackState.InstalledApps) {
        $uninstalled = $false

        try {
            switch ($app.Method) {
                'Winget' {
                    if ($app.Identifier -and (Get-Command winget -ErrorAction SilentlyContinue)) {
                        $wingetArgs = @('uninstall', '--id', $app.Identifier, '--silent', '--accept-source-agreements')
                        $process = Start-Process -FilePath 'winget' -ArgumentList $wingetArgs -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue
                        $uninstalled = ($null -ne $process -and $process.ExitCode -eq 0)
                    }
                }
                'Chocolatey' {
                    if ($app.Identifier -and (Get-Command choco -ErrorAction SilentlyContinue)) {
                        $chocoArgs = @('uninstall', $app.Identifier, '-y')
                        $process = Start-Process -FilePath 'choco' -ArgumentList $chocoArgs -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue
                        $uninstalled = ($null -ne $process -and $process.ExitCode -eq 0)
                    }
                }
                default {
                    Write-Status -Message (Get-LocalizedString -Key 'rollback.cannot_auto_rollback' -Parameters @{ AppName = $app.AppName; Method = $app.Method }) -Level 'Warning'
                }
            }

            if ($uninstalled) {
                Write-Status -Message (Get-LocalizedString -Key 'rollback.rolled_back' -Parameters @{ AppName = $app.AppName }) -Level 'Success'
                $result.RolledBack += $app.AppName
            } else {
                Write-Status -Message (Get-LocalizedString -Key 'rollback.rollback_failed' -Parameters @{ AppName = $app.AppName }) -Level 'Warning'
                $result.Failed += $app.AppName
                $result.Success = $false
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'rollback.rollback_error' -Parameters @{ AppName = $app.AppName; Error = $_.Exception.Message }) -Level 'Error'
            $result.Failed += $app.AppName
            $result.Success = $false
        }
    }

    Clear-RollbackState
    return $result
}

function Test-IncompleteDeployment {
    <#
    .SYNOPSIS
        Checks if there is an incomplete deployment that can be resumed.
    .DESCRIPTION
        Wrapper around StateManager's Test-DeploymentInProgress for backwards compatibility.
    .OUTPUTS
        Boolean indicating if an incomplete deployment exists.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    return Test-DeploymentInProgress
}

function Resume-Deployment {
    <#
    .SYNOPSIS
        Resumes an incomplete deployment from where it left off.
    .DESCRIPTION
        Returns the list of pending applications to be installed,
        displaying progress information to the user.
    .OUTPUTS
        Array of pending application names, or null if no deployment to resume.
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $state = Get-DeploymentState
    if (-not $state -or $state.PendingApps.Count -eq 0) {
        Write-Status -Message (Get-LocalizedString -Key 'deployment.no_incomplete') -Level 'Info'
        return $null
    }

    Write-Status -Message (Get-LocalizedString -Key 'deployment.resuming' -Parameters @{ ProfileName = $state.ProfileName }) -Level 'Info'
    Write-Status -Message "  $(Get-LocalizedString -Key 'deployment.completed_count' -Parameters @{ Count = $state.CompletedApps.Count })" -Level 'Info'
    Write-Status -Message "  $(Get-LocalizedString -Key 'deployment.pending_count' -Parameters @{ Count = $state.PendingApps.Count })" -Level 'Info'
    Write-Status -Message "  $(Get-LocalizedString -Key 'deployment.failed_count' -Parameters @{ Count = $state.FailedApps.Count })" -Level 'Info'

    return $state.PendingApps
}

# === ENVIRONMENT RESTRICTION HELPER ===

function Test-EnvironmentRestriction {
    <#
    .SYNOPSIS
        Checks if an application is restricted in the current environment.

    .DESCRIPTION
        Validates whether the application can be installed in the current
        execution environment (Physical, Sandbox, VMware, etc.).

    .PARAMETER Application
        The application object to check.

    .OUTPUTS
        [hashtable] Contains Restricted (bool), Environment (string), Message (string)
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $result = @{
        Restricted = $false
        Environment = 'Unknown'
        Message = ''
    }

    if (-not $Application.EnvironmentRestrictions -or $Application.EnvironmentRestrictions.Count -eq 0) {
        return $result
    }

    if (-not (Get-Command -Name 'Get-SystemEnvironmentType' -ErrorAction SilentlyContinue)) {
        $envModule = Join-Path $script:RepositoryRoot 'Modules\EnvironmentDetection.psm1'
        if (Test-Path $envModule) {
            Import-Module $envModule -Force -WarningAction SilentlyContinue
        }
    }

    try {
        $currentEnv = Get-SystemEnvironmentType
        $result.Environment = $currentEnv.ToString()

        if ($Application.EnvironmentRestrictions -contains $currentEnv) {
            $result.Restricted = $true
            $result.Message = (Get-LocalizedString -Key 'engine.env_restricted' -Parameters @{ AppName = $Application.Name; Environment = $currentEnv })
            Write-Status -Message $result.Message -Level 'Warning'
        }
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'engine.env_check_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
    }

    return $result
}

# === INSTALLATION ORCHESTRATION ===

function Invoke-InstallationMethodSequence {
    <#
    .SYNOPSIS
        Tries installation methods in sequence: Winget -> Chocolatey -> Store -> DirectDownload.

    .DESCRIPTION
        Orchestrates installation attempts across multiple package managers,
        handling fallbacks and special cases like IgnoreExitCodeIfFileExists.

    .PARAMETER Application
        The application object to install.

    .PARAMETER LogCallback
        Optional scriptblock for parallel logging.

    .OUTPUTS
        [hashtable] Installation result
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter()]
        [scriptblock]$LogCallback = $null
    )

    $writeLog = {
        param([string]$Message, [string]$Level = 'Info')
        if ($LogCallback) {
            & $LogCallback -Message $Message -Level $Level
        } else {
            Write-Status -Message $Message -Level $Level
        }
    }

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $false
        Method = $null
        Message = ''
        AttemptedMethods = @()
        FailureReasons = @()
    }

    $sources = $Application.Sources

    if (-not $sources) {
        $result.Message = (Get-LocalizedString -Key 'orchestrator.no_sources')
        return $result
    }

    $testIgnoreExitCode = {
        if ($Application.PSObject.Properties['InstallationOptions']) {
            if ($Application.InstallationOptions.IgnoreExitCodeIfFileExists) {
                if (Test-ApplicationInstalled -Application $Application) {
                    return $true
                }
            }
        }
        return $false
    }

    $isOfficeApp = $sources.Winget -eq 'Microsoft.Office' -or
                   $sources.Chocolatey -eq 'microsoft-office-deployment' -or
                   $Application.Name -match 'Office\s*(365|2019|2021|2024)'

    $getInstallResult = {
        param([object[]]$Output)
        if ($null -eq $Output -or $Output.Count -eq 0) { return $false }
        return $Output[-1] -eq $true
    }

    # 1. Try Winget
    if ($sources.Winget) {
        $result.AttemptedMethods += 'Winget'
        & $writeLog (Get-LocalizedString -Key 'install.orchestrator.attempting_winget' -Parameters @{ PackageId = $sources.Winget }) 'Verbose'

        $wingetOutput = @(Install-ViaWinget -PackageId $sources.Winget)
        if (& $getInstallResult $wingetOutput) {
            if ($isOfficeApp) {
                & $writeLog (Get-LocalizedString -Key 'install.orchestrator.office_c2r_waiting') 'Info'
                $officeTimeout = Get-InstallationTimeout -AppName 'Office'
                $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $officeTimeout
                if (-not $officeInstalled) {
                    $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.office_c2r_timeout')
                }
            }
            $result.Success = $true
            $result.Method = 'Winget'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.winget')
            return $result
        } else {
            if (& $testIgnoreExitCode) {
                & $writeLog (Get-LocalizedString -Key 'install.orchestrator.success_despite_exit_code') 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = (Get-LocalizedString -Key 'orchestrator.result.winget_verified')
                return $result
            }
            $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.winget' -Parameters @{ PackageId = $sources.Winget })
        }
    }

    # 2. Try Chocolatey
    if ($sources.Chocolatey) {
        $result.AttemptedMethods += 'Chocolatey'
        & $writeLog (Get-LocalizedString -Key 'install.orchestrator.attempting_choco' -Parameters @{ PackageId = $sources.Chocolatey }) 'Verbose'

        $chocoOutput = @(Install-ViaChocolatey -PackageName $sources.Chocolatey)
        if (& $getInstallResult $chocoOutput) {
            if ($isOfficeApp) {
                & $writeLog (Get-LocalizedString -Key 'install.orchestrator.office_c2r_waiting') 'Info'
                $officeTimeout = Get-InstallationTimeout -AppName 'Office'
                $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $officeTimeout
                if (-not $officeInstalled) {
                    $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.office_c2r_timeout')
                }
            }
            $result.Success = $true
            $result.Method = 'Chocolatey'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.chocolatey')
            return $result
        } else {
            if (& $testIgnoreExitCode) {
                & $writeLog (Get-LocalizedString -Key 'install.orchestrator.success_despite_exit_code') 'Success'
                $result.Success = $true
                $result.Method = 'Chocolatey'
                $result.Message = (Get-LocalizedString -Key 'orchestrator.result.chocolatey_verified')
                return $result
            }
            $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.chocolatey' -Parameters @{ PackageId = $sources.Chocolatey })
        }
    }

    # 3. Try Microsoft Store
    if ($sources.Store) {
        $result.AttemptedMethods += 'Store'
        & $writeLog (Get-LocalizedString -Key 'install.orchestrator.attempting_store' -Parameters @{ ProductId = $sources.Store }) 'Verbose'

        $storeOutput = @(Install-ViaStore -ProductId $sources.Store)
        if (& $getInstallResult $storeOutput) {
            $result.Success = $true
            $result.Method = 'Store'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.store')
            return $result
        } else {
            $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.store' -Parameters @{ ProductId = $sources.Store })
        }
    }

    # 4. Try Direct Download
    if ($sources.DirectUrl) {
        $result.AttemptedMethods += 'DirectDownload'
        & $writeLog (Get-LocalizedString -Key 'install.orchestrator.attempting_direct' -Parameters @{ Url = $sources.DirectUrl }) 'Verbose'

        $installParams = @{ Url = $sources.DirectUrl }

        $installArgs = if ($Application.PSObject.Properties['InstallArguments']) { $Application.InstallArguments } else { $null }
        if ($installArgs) {
            $installParams['CustomArguments'] = $installArgs
            & $writeLog (Get-LocalizedString -Key 'install.orchestrator.custom_args_detected' -Parameters @{ Arguments = $installArgs }) 'Verbose'
        }

        if ($Application.Detection -and $Application.Detection.Path) {
            $installParams['DetectionPath'] = $Application.Detection.Path
        }

        if ($sources.PSObject.Properties['SHA256'] -and $sources.SHA256) {
            $installParams['ExpectedSHA256'] = $sources.SHA256
        }

        Write-Verbose "Calling Install-ViaDirectDownload"
        Write-Verbose "installParams: $($installParams | ConvertTo-Json -Compress)"
        $downloadOutput = @(Install-ViaDirectDownload @installParams)
        Write-Verbose "Install-ViaDirectDownload returned"
        Write-Verbose "downloadOutput count: $($downloadOutput.Count)"
        if (& $getInstallResult $downloadOutput) {
            $result.Success = $true
            $result.Method = 'DirectDownload'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.direct_download')
            return $result
        } else {
            $result.FailureReasons += (Get-LocalizedString -Key 'orchestrator.failure.direct_download')
        }
    }

    $result.Message = if ($result.AttemptedMethods.Count -gt 0) {
        Get-LocalizedString -Key 'orchestrator.all_methods_failed' -Parameters @{ Reasons = ($result.FailureReasons -join '; ') }
    } else {
        Get-LocalizedString -Key 'orchestrator.no_valid_sources'
    }

    & $writeLog (Get-LocalizedString -Key 'orchestrator.install_failed' -Parameters @{ Message = $result.Message }) 'Warning'
    return $result
}

function Get-ApplicationSources {
    <#
    .SYNOPSIS
        Gets the installation sources for an application.

    .DESCRIPTION
        Returns the Sources property of an application object, which contains
        Winget ID, Chocolatey package name, Store ID, DirectUrl, etc.

    .PARAMETER Application
        The application object from the database.

    .OUTPUTS
        PSCustomObject containing Winget, Chocolatey, Store, DirectUrl properties.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    if ($null -eq $Application) {
        return $null
    }

    return $Application.Sources
}

function Invoke-ApplicationUpgrade {
    <#
    .SYNOPSIS
        Attempts to upgrade an already installed application.

    .DESCRIPTION
        Tries to upgrade using Winget or Chocolatey upgrade commands.
        Handles exit codes gracefully - "no update available" is not an error.

    .PARAMETER Application
        The application object containing installation sources.

    .OUTPUTS
        [hashtable] Upgrade result with Success, Method, Message properties.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $true
        Method = $null
        Message = ''
    }

    $sources = Get-ApplicationSources -Application $Application

    if ($sources.Winget -and (Test-CommandExists -Name 'winget')) {
        try {
            Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.attempting_winget' -Parameters @{ PackageId = $sources.Winget }) -Level 'Info'

            $arguments = @(
                'upgrade',
                '--id', $sources.Winget,
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            $process = Start-ProcessWithTimeout -FilePath 'winget' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.success_winget' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = (Get-LocalizedString -Key 'orchestrator.result.upgraded')
                return $result
            } elseif ($process.ExitCode -eq -1978335189) {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.no_update_winget' -Parameters @{ AppName = $Application.Name }) -Level 'Verbose'
            } else {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.exit_code_winget' -Parameters @{ ExitCode = $process.ExitCode }) -Level 'Verbose'
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.error_winget' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
        }
    }

    if ($sources.Chocolatey -and (Test-CommandExists -Name 'choco')) {
        try {
            Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.attempting_choco' -Parameters @{ PackageId = $sources.Chocolatey }) -Level 'Info'

            $arguments = @(
                'upgrade',
                $sources.Chocolatey,
                '-y',
                '--no-progress'
            )

            $process = Start-ProcessWithTimeout -FilePath 'choco' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.success_choco' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
                $result.Success = $true
                $result.Method = 'Chocolatey'
                $result.Message = (Get-LocalizedString -Key 'orchestrator.result.upgraded')
                return $result
            } else {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.exit_code_choco' -Parameters @{ ExitCode = $process.ExitCode }) -Level 'Verbose'
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'orchestrator.upgrade.error_choco' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
        }
    }

    $result.Message = (Get-LocalizedString -Key 'orchestrator.result.no_upgrade')
    return $result
}

function Install-Application {
    <#
    .SYNOPSIS
        Installs a single application using available methods.

    .DESCRIPTION
        Orchestrates application installation by:
        1. Checking environment restrictions
        2. Verifying if already installed (skip or upgrade based on ForceUpdate)
        3. Using custom install methods (WindowsFeature/Capability) if specified
        4. Trying standard methods in sequence (Winget -> Chocolatey -> Store -> DirectDownload)

    .PARAMETER Application
        The application object containing installation sources and configuration.

    .PARAMETER Force
        Force installation even if already detected.

    .PARAMETER ForceUpdate
        If the app is already installed, attempt to upgrade it instead of skipping.

    .OUTPUTS
        [hashtable] Installation result with Success, Method, Message properties.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter()]
        [switch]$Force,

        [Parameter()]
        [switch]$ForceUpdate
    )

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $false
        Method = $null
        Message = ''
    }

    # 1. Check environment restrictions
    $envCheck = Test-EnvironmentRestriction -Application $Application
    if ($envCheck.Restricted) {
        $result.Message = (Get-LocalizedString -Key 'engine.env_not_compatible' -Parameters @{ Environment = $envCheck.Environment })
        return $result
    }

    # 2. Check if already installed
    if (-not $Force) {
        $isInstalled = Test-ApplicationInstalled -Application $Application
        if ($isInstalled) {
            if ($ForceUpdate) {
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.checking_updates' -Parameters @{ AppName = $Application.Name }) -Level 'Info'
                $upgradeResult = Invoke-ApplicationUpgrade -Application $Application
                if ($upgradeResult.Success) {
                    return $upgradeResult
                }
                Write-Status -Message (Get-LocalizedString -Key 'orchestrator.no_update_available' -Parameters @{ AppName = $Application.Name }) -Level 'Info'
                $result.AlreadyInstalled = $true
                $result.Success = $true
                $result.Message = (Get-LocalizedString -Key 'orchestrator.already_installed_no_update')
                return $result
            }

            Write-Status -Message (Get-LocalizedString -Key 'orchestrator.already_installed' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
            $result.AlreadyInstalled = $true
            $result.Success = $true
            $result.Message = (Get-LocalizedString -Key 'orchestrator.already_installed_status')
            return $result
        }
    }

    Write-Status -Message (Get-LocalizedString -Key 'orchestrator.installing' -Parameters @{ AppName = $Application.Name }) -Level 'Info'

    # 3. Handle custom install methods (WindowsFeature, WindowsCapability)
    $installMethod = if ($Application.PSObject.Properties['InstallMethod']) { $Application.InstallMethod } else { $null }
    if ($installMethod) {
        return Invoke-CustomInstallMethod -Application $Application
    }

    # 4. Try standard installation methods in sequence
    return Invoke-InstallationMethodSequence -Application $Application
}

function Install-ApplicationsParallel {
    <#
    .SYNOPSIS
        Installs multiple applications in parallel using PowerShell 7+ ForEach-Object -Parallel.

    .DESCRIPTION
        Orchestrates parallel installation with:
        - Environment restriction filtering
        - Automatic fallback to sequential mode on PS 5.1
        - Per-application logging to files
        - Retry logic for transient errors

    .PARAMETER Applications
        Array of application objects to install.

    .PARAMETER Force
        Force installation even if already detected.

    .PARAMETER MaxParallel
        Maximum number of parallel installations (1-10, default 5).

    .OUTPUTS
        Array of installation result hashtables.
    #>
    [CmdletBinding()]
    [OutputType([hashtable[]])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications,

        [Parameter()]
        [switch]$Force,

        [Parameter()]
        [ValidateRange(1, 10)]
        [int]$MaxParallel = 5
    )

    # Validate PowerShell 7+ with ForEach-Object -Parallel support
    $hasParallelSupport = $false
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        try {
            $foreachCommand = Get-Command ForEach-Object -ErrorAction Stop
            $hasParallelSupport = $foreachCommand.Parameters.ContainsKey('Parallel')
        } catch {
            $hasParallelSupport = $false
        }
    }

    if (-not $hasParallelSupport) {
        Write-Host (Get-LocalizedString -Key 'parallel.requires_ps7') -ForegroundColor Yellow
        Write-Host (Get-LocalizedString -Key 'parallel.current_version' -Parameters @{ Version = $PSVersionTable.PSVersion }) -ForegroundColor Yellow
        Write-Host (Get-LocalizedString -Key 'parallel.fallback_sequential') -ForegroundColor Yellow

        $results = @()
        foreach ($app in $Applications) {
            $results += Install-Application -Application $app -Force:$Force
        }
        return $results
    }

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'parallel.title') -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'parallel.max_threads' -Parameters @{ Count = $MaxParallel }) -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'parallel.total_apps' -Parameters @{ Count = $Applications.Count }) -ForegroundColor Cyan
    Write-Host ""

    $startTime = Get-Date
    $sortedApps = $Applications | Sort-Object -Property Priority

    $moduleRoot = $script:ModuleRoot
    $repoRoot = $script:RepositoryRoot
    $forceInstall = $Force.IsPresent

    # Export helper functions for parallel scope
    $validateUrlFunction = ${function:Test-ValidDownloadUrl}.ToString()

    # Self-contained detection function for parallel scope
    $detectAppFunction = @'
function Test-AppInstalledParallel {
    param([PSCustomObject]$App)

    $appName = $App.Name

    # Special case: PowerToys
    if ($appName -eq 'Microsoft PowerToys') {
        $paths = @("${env:ProgramFiles}\PowerToys\PowerToys.exe", "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe", "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe")
        foreach ($p in $paths) { if (Test-Path $p -ErrorAction SilentlyContinue) { return $true } }
        if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) { return $true }
    }

    # Special case: Quick Assist
    if ($appName -eq 'Microsoft Quick Assist') {
        try {
            $pkg = Get-AppxPackage -Name "MicrosoftCorporationII.QuickAssist" -ErrorAction SilentlyContinue
            if ($pkg) { return $true }
        } catch {
            Write-Verbose "Quick Assist detection failed: $($_.Exception.Message)"
        }
    }

    if (-not $App.Detection) {
        if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            $list = Get-CachedWingetList
            if ($list -match [regex]::Escape($appName)) { return $true }
        }
        return $false
    }

    switch ($App.Detection.Method) {
        'Registry' {
            if ($App.Detection.PSObject.Properties['Path'] -and $App.Detection.Path) {
                $regPath = $App.Detection.Path
                if ($regPath -match '\.\.') { return $false }
                return Test-Path -Path $regPath -ErrorAction SilentlyContinue
            }
            return $false
        }
        'File' {
            if (-not ($App.Detection.PSObject.Properties['Path'] -and $App.Detection.Path)) { return $false }
            $rawPath = $App.Detection.Path
            if ($rawPath -match '\.\.' -or $rawPath -match '[\\/]\.\.[\\/]?' -or $rawPath -match '^\.\.') { return $false }
            $expandedPath = [Environment]::ExpandEnvironmentVariables($rawPath)
            if ($expandedPath -match '\.\.' -or $expandedPath -match '[\\/]\.\.[\\/]?' -or $expandedPath -match '^\.\.') { return $false }
            if ($expandedPath -notmatch '^[A-Za-z]:[\\/]') { return $false }

            # Security: Normalize path and validate against allowed root directories
            try {
                $normalizedPath = [System.IO.Path]::GetFullPath($expandedPath)
                # Ensure no traversal remains after normalization
                if ($normalizedPath -match '\.\.') { return $false }

                # Validate against allowed roots (Program Files, AppData, User profile, System drive)
                $allowedRoots = @(
                    $env:ProgramFiles,
                    ${env:ProgramFiles(x86)},
                    $env:LOCALAPPDATA,
                    $env:APPDATA,
                    $env:USERPROFILE,
                    $env:SystemDrive + '\'
                ) | Where-Object { $_ }

                $isAllowed = $false
                foreach ($root in $allowedRoots) {
                    if ($normalizedPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                        $isAllowed = $true
                        break
                    }
                }
                if (-not $isAllowed) {
                    Write-Verbose "Security: Path outside allowed roots blocked in parallel detection"
                    return $false
                }
            } catch {
                return $false
            }

            if ($expandedPath -match '\*') {
                return (Get-ChildItem -Path $expandedPath -ErrorAction SilentlyContinue).Count -gt 0
            }
            return Test-Path -Path $expandedPath -PathType Leaf -ErrorAction SilentlyContinue
        }
        'Command' {
            try {
                $parts = $App.Detection.Command -split '\s+', 2
                $exe = $parts[0]; $cmdArgs = if ($parts.Count -gt 1) { $parts[1] } else { $null }
                $allowedExes = @('java','java.exe','javac','javac.exe','dotnet','dotnet.exe','python','python.exe','python3','python3.exe','node','node.exe','npm','npm.cmd','git','git.exe','docker','docker.exe','rustc','rustc.exe','cargo','cargo.exe','go','go.exe','ruby','ruby.exe','php','php.exe','perl','perl.exe')
                $exeBaseName = [System.IO.Path]::GetFileName($exe).ToLower()
                if ($exeBaseName -notin $allowedExes) { return $false }
                if (-not (Get-Command -Name $exe -ErrorAction SilentlyContinue)) { return $false }
                $expectedPattern = if ($App.Detection.PSObject.Properties['Arguments']) { $App.Detection.Arguments } else { $null }
                if ($expectedPattern) {
                    $output = if ($cmdArgs) { & $exe $cmdArgs 2>&1 | Out-String } else { & $exe 2>&1 | Out-String }
                    return $output -match [regex]::Escape($expectedPattern)
                } else {
                    $proc = if ($cmdArgs) { Start-Process -FilePath $exe -ArgumentList $cmdArgs -Wait -NoNewWindow -PassThru -ErrorAction Stop }
                            else { Start-Process -FilePath $exe -Wait -NoNewWindow -PassThru -ErrorAction Stop }
                    return $proc.ExitCode -eq 0
                }
            } catch { return $false }
        }
        'WindowsFeature' {
            $f = Get-WindowsOptionalFeature -Online -FeatureName $App.Detection.Feature -ErrorAction SilentlyContinue
            return $f -and $f.State -eq 'Enabled'
        }
        'WindowsCapability' {
            $c = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$($App.Detection.Capability)*" } -ErrorAction SilentlyContinue
            return $c -and $c.State -eq 'Installed'
        }
        'StoreApp' {
            if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                try {
                    $list = Get-CachedWingetList
                    if ($App.Sources.Store -and $list -match [regex]::Escape($App.Sources.Store) -and $list -notmatch "No installed package") { return $true }
                    if ($App.Detection.PackageName -and $list -match [regex]::Escape($App.Detection.PackageName)) { return $true }
                } catch {
                    Write-Verbose "StoreApp detection failed for $appName : $($_.Exception.Message)"
                }
            }
            return $false
        }
        default {
            if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                $list = Get-CachedWingetList
                if ($list -match [regex]::Escape($appName)) { return $true }
            }
            return $false
        }
    }
}
'@

    $currentEnvironment = Get-SystemEnvironmentType

    $appsToInstall = @()
    $skippedApps = @()

    foreach ($app in $sortedApps) {
        if ($app.EnvironmentRestrictions -and $app.EnvironmentRestrictions.Count -gt 0) {
            if ($app.EnvironmentRestrictions -contains $currentEnvironment) {
                Write-Host (Get-LocalizedString -Key 'install.skipping_environment' -Parameters @{ AppName = $app.Name; Environment = $currentEnvironment }) -ForegroundColor Yellow
                $skippedApps += [PSCustomObject]@{
                    ApplicationName = $app.Name
                    Success = $false
                    Skipped = $true
                    AlreadyInstalled = $false
                    Method = $null
                    Message = (Get-LocalizedString -Key 'install.skipping_environment' -Parameters @{ AppName = $app.Name; Environment = $currentEnvironment })
                }
                continue
            }
        }
        $appsToInstall += $app
    }

    Write-Host (Get-LocalizedString -Key 'parallel.apps_to_install' -Parameters @{ Count = $appsToInstall.Count }) -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'parallel.skipped_environment' -Parameters @{ Count = $skippedApps.Count }) -ForegroundColor Yellow
    Write-Host ""

    # Create parallel logs directory
    $parallelLogsDir = Join-Path $repoRoot 'Logs\Parallel'
    $maxRetries = 3
    $retryCount = 0

    while ($retryCount -lt $maxRetries) {
        try {
            if (-not (Test-Path $parallelLogsDir)) {
                New-Item -Path $parallelLogsDir -ItemType Directory -Force -ErrorAction Stop | Out-Null
            }
            break
        } catch {
            $retryCount++
            if ($retryCount -ge $maxRetries) {
                Write-Host (Get-LocalizedString -Key 'parallel.logs_create_failed' -Parameters @{ Retries = $maxRetries; Error = $_ }) -ForegroundColor Red
                throw
            }
            Start-Sleep -Milliseconds (100 * $retryCount)
        }
    }

    # Cleanup old logs (retention: 7 days)
    try {
        $cutoffDate = (Get-Date).AddDays(-7)
        Get-ChildItem -Path $parallelLogsDir -Filter "parallel_*.log" -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt $cutoffDate } |
            Remove-Item -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Host (Get-LocalizedString -Key 'parallel.logs_cleanup_failed' -Parameters @{ Error = $_ }) -ForegroundColor Yellow
    }

    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $parallelTimeoutMs = $script:ParallelInstallTimeoutMs
    $frameworkVersion = (Get-Content (Join-Path $script:RepositoryRoot 'Config\version.json') -Raw | ConvertFrom-Json).Version

    $installResults = $appsToInstall | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $app = $_
        $force = $using:forceInstall
        $repRoot = $using:repoRoot
        $parallelLogDir = $using:parallelLogsDir
        $ts = $using:timestamp
        $validateUrl = $using:validateUrlFunction
        $detectAppFunc = $using:detectAppFunction
        $installTimeoutMs = $using:parallelTimeoutMs
        $fwVersion = $using:frameworkVersion

        ${function:Test-ValidDownloadUrl} = [ScriptBlock]::Create($validateUrl)
        . ([ScriptBlock]::Create($detectAppFunc))

        $appLogFile = Join-Path $parallelLogDir "parallel_${ts}_$($app.Name -replace '[^\w\-]', '_').log"

        function Write-ParallelLog {
            param([string]$Message, [string]$Level = 'Info')
            $logTimestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
            $logMessage = "[$logTimestamp] [$Level] $Message"
            $logMessage | Out-File -FilePath $appLogFile -Append -Encoding UTF8
        }

        function Write-ParallelException {
            param(
                [System.Management.Automation.ErrorRecord]$ErrorRecord,
                [string]$Context = 'Unknown'
            )
            Write-ParallelLog "EXCEPTION in $Context" 'Error'
            Write-ParallelLog "  Type: $($ErrorRecord.Exception.GetType().FullName)" 'Error'
            Write-ParallelLog "  Message: $($ErrorRecord.Exception.Message)" 'Error'
            if ($ErrorRecord.ScriptStackTrace) {
                Write-ParallelLog "  Stack: $($ErrorRecord.ScriptStackTrace -replace "`n", ' -> ')" 'Error'
            }
            if ($ErrorRecord.Exception.InnerException) {
                Write-ParallelLog "  Inner: $($ErrorRecord.Exception.InnerException.Message)" 'Error'
            }
            if ($ErrorRecord.InvocationInfo) {
                $line = $ErrorRecord.InvocationInfo.ScriptLineNumber
                $cmd = $ErrorRecord.InvocationInfo.Line.Trim()
                if ($cmd.Length -gt 100) { $cmd = $cmd.Substring(0, 100) + '...' }
                Write-ParallelLog "  At line $line`: $cmd" 'Error'
            }
        }

        Write-ParallelLog "Starting installation of $($app.Name)" 'Info'

        $coreModulePath = Join-Path $repRoot 'Core\Core.psm1'
        if (Test-Path $coreModulePath) {
            Import-Module $coreModulePath -Force -WarningAction SilentlyContinue
        }

        $localizationModulePath = Join-Path $repRoot 'Core\Localization.psm1'
        if (Test-Path $localizationModulePath) {
            Import-Module $localizationModulePath -Force -WarningAction SilentlyContinue
        }

        $result = @{
            ApplicationName = $app.Name
            Success = $false
            AlreadyInstalled = $false
            Method = $null
            Message = ''
        }

        try {
            if (-not $force) {
                $installed = Test-AppInstalledParallel -App $app
                if ($installed) {
                    Write-ParallelLog "Already installed - skipping" 'Success'
                    $result.AlreadyInstalled = $true
                    $result.Success = $true
                    $result.Message = (Get-LocalizedString -Key 'orchestrator.already_installed_status')
                    return $result
                }
            }

            Write-ParallelLog "Not installed - proceeding with installation" 'Info'

            $appInstallMethod = if ($app.PSObject.Properties['InstallMethod']) { $app.InstallMethod } else { $null }
            if ($appInstallMethod) {
                Write-ParallelLog "Using custom install method: $appInstallMethod" 'Info'
                switch ($appInstallMethod) {
                    'WindowsFeature' {
                        Write-ParallelLog "Installing as Windows Feature: $($app.Detection.Feature)" 'Info'
                        $feature = Get-WindowsOptionalFeature -Online -FeatureName $app.Detection.Feature -ErrorAction Stop
                        if ($feature.State -ne 'Enabled') {
                            Enable-WindowsOptionalFeature -Online -FeatureName $app.Detection.Feature -NoRestart -ErrorAction Stop | Out-Null
                        }
                        Write-ParallelLog "Windows Feature installed successfully" 'Success'
                        $result.Success = $true
                        $result.Method = 'WindowsFeature'
                        $result.Message = (Get-LocalizedString -Key 'orchestrator.result.windows_feature')
                        return $result
                    }
                    'WindowsCapability' {
                        Write-ParallelLog "Installing as Windows Capability: $($app.Detection.Capability)" 'Info'
                        $capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$($app.Detection.Capability)*" }
                        if ($capabilities) {
                            $capability = if ($capabilities -is [array]) { $capabilities[0] } else { $capabilities }
                            if ($capability.State -ne 'Installed') {
                                Add-WindowsCapability -Online -Name $capability.Name -ErrorAction Stop | Out-Null
                            }
                            Write-ParallelLog "Windows Capability installed successfully" 'Success'
                            $result.Success = $true
                            $result.Method = 'WindowsCapability'
                            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.windows_capability')
                            return $result
                        }
                    }
                }
            }

            $sources = $app.Sources

            # 1. Winget (with retry logic)
            if ($sources.Winget -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
                Write-ParallelLog "Attempting installation via Winget: $($sources.Winget)" 'Info'
                $arguments = @(
                    'install',
                    '--id', $sources.Winget,
                    '--accept-package-agreements',
                    '--accept-source-agreements',
                    '--silent'
                )

                $maxRetries = 3
                $retryDelaySeconds = 2
                $transientErrors = @(-1978335189, -1978335212)

                for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
                    if ($attempt -gt 1) {
                        Write-ParallelLog "Retry $attempt/$maxRetries for Winget: $($sources.Winget)" 'Info'
                    }

                    $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -NoNewWindow -PassThru
                    $timeoutMs = $installTimeoutMs

                    if (-not $process.WaitForExit($timeoutMs)) {
                        Write-ParallelLog "Process timed out after $([math]::Round($timeoutMs / 1000)) seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Winget installation failed (timeout)" 'Warning'
                        break
                    } elseif ($process.ExitCode -eq 0) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Installed successfully via Winget$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Winget'
                        $result.Message = if ($attempt -gt 1) { Get-LocalizedString -Key 'orchestrator.result.winget_retry' -Parameters @{ Attempt = $attempt } } else { Get-LocalizedString -Key 'orchestrator.result.winget' }
                        return $result
                    } elseif ($process.ExitCode -eq -1978334974) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Already installed (Winget)$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Winget'
                        $result.AlreadyInstalled = $true
                        $result.Message = if ($attempt -gt 1) { Get-LocalizedString -Key 'orchestrator.result.already_installed_winget_retry' -Parameters @{ Attempt = $attempt } } else { Get-LocalizedString -Key 'orchestrator.result.already_installed_winget' }
                        return $result
                    } elseif ($transientErrors -contains $process.ExitCode -and $attempt -lt $maxRetries) {
                        $delay = $retryDelaySeconds * [Math]::Pow(2, $attempt - 1)
                        Write-ParallelLog "Transient error (exit code: $($process.ExitCode)), retrying in $delay seconds..." 'Warning'
                        Start-Sleep -Seconds $delay
                        continue
                    } else {
                        Write-ParallelLog "Winget installation failed (exit code: $($process.ExitCode))" 'Warning'
                        break
                    }
                }
            }

            # 2. Chocolatey (with retry logic)
            if ($sources.Chocolatey -and (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)) {
                Write-ParallelLog "Attempting installation via Chocolatey: $($sources.Chocolatey)" 'Info'
                $arguments = @(
                    'install', $sources.Chocolatey,
                    '-y',
                    '--no-progress',
                    '--ignore-checksums'
                )

                $maxRetries = 3
                $retryDelaySeconds = 2
                $transientErrors = @(1641, 3010, -1)

                for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
                    if ($attempt -gt 1) {
                        Write-ParallelLog "Retry $attempt/$maxRetries for Chocolatey: $($sources.Chocolatey)" 'Info'
                    }

                    $process = Start-Process -FilePath 'choco' -ArgumentList $arguments -NoNewWindow -PassThru
                    $timeoutMs = $installTimeoutMs

                    if (-not $process.WaitForExit($timeoutMs)) {
                        Write-ParallelLog "Process timed out after $([math]::Round($timeoutMs / 1000)) seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Chocolatey installation failed (timeout)" 'Warning'
                        break
                    } elseif ($process.ExitCode -eq 0) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Installed successfully via Chocolatey$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Chocolatey'
                        $result.Message = if ($attempt -gt 1) { Get-LocalizedString -Key 'orchestrator.result.chocolatey_retry' -Parameters @{ Attempt = $attempt } } else { Get-LocalizedString -Key 'orchestrator.result.chocolatey' }
                        return $result
                    } elseif ($transientErrors -contains $process.ExitCode -and $attempt -lt $maxRetries) {
                        $delay = $retryDelaySeconds * [Math]::Pow(2, $attempt - 1)
                        Write-ParallelLog "Transient error (exit code: $($process.ExitCode)), retrying in $delay seconds..." 'Warning'
                        Start-Sleep -Seconds $delay
                        continue
                    } else {
                        Write-ParallelLog "Chocolatey installation failed (exit code: $($process.ExitCode))" 'Warning'
                        break
                    }
                }
            }

            # 3. Microsoft Store
            if ($sources.Store -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
                $isSandbox = ($env:USERNAME -eq 'WDAGUtilityAccount') -or
                             ($env:COMPUTERNAME -match '^SANDBOX-') -or
                             (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Control\ContainerManager' -ErrorAction SilentlyContinue)

                if ($isSandbox) {
                    Write-ParallelLog "Skipping Store install - Windows Store unavailable in Sandbox" 'Warning'
                } else {
                    Write-ParallelLog "Attempting installation via Microsoft Store: $($sources.Store)" 'Info'
                    $arguments = @(
                        'install',
                        '--id', $sources.Store,
                        '--source', 'msstore',
                        '--accept-package-agreements',
                        '--accept-source-agreements',
                        '--silent'
                    )

                    $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -NoNewWindow -PassThru
                    $timeoutMs = $installTimeoutMs

                    if (-not $process.WaitForExit($timeoutMs)) {
                        Write-ParallelLog "Process timed out after $([math]::Round($timeoutMs / 1000)) seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Microsoft Store installation failed (timeout)" 'Warning'
                    } elseif ($process.ExitCode -eq 0) {
                        Write-ParallelLog "Installed successfully via Microsoft Store" 'Success'
                        $result.Success = $true
                        $result.Method = 'Store'
                        $result.Message = (Get-LocalizedString -Key 'orchestrator.result.store')
                        return $result
                    } else {
                        Write-ParallelLog "Microsoft Store installation failed (exit code: $($process.ExitCode))" 'Warning'
                    }
                }
            }

            # 4. Direct Download
            if ($sources.DirectUrl) {
                if (-not (Test-ValidDownloadUrl -Url $sources.DirectUrl)) {
                    Write-ParallelLog "Invalid or insecure URL: $($sources.DirectUrl)" 'Error'
                    $result.Message = (Get-LocalizedString -Key 'orchestrator.result.invalid_direct_url')
                    return $result
                }

                Write-ParallelLog "Attempting direct download installation: $($sources.DirectUrl)" 'Info'

                try {
                    # Extract filename from URL, handling query parameters properly
                    $filename = $null
                    $dlUrl = $sources.DirectUrl
                    try {
                        $uri = [System.Uri]::new($dlUrl)
                        # First try: parse query string for filename parameters
                        if ($uri.Query) {
                            $queryString = $uri.Query.TrimStart('?')
                            $queryPairs = $queryString -split '&'
                            foreach ($pair in $queryPairs) {
                                $parts = $pair -split '=', 2
                                if ($parts.Count -eq 2) {
                                    $paramName = [System.Uri]::UnescapeDataString($parts[0]).ToLower()
                                    $paramValue = [System.Uri]::UnescapeDataString($parts[1])
                                    if ($paramName -in @('installer', 'file', 'filename', 'name', 'download')) {
                                        if ($paramValue -match '\.(exe|msi|zip)$') {
                                            $filename = [System.IO.Path]::GetFileName($paramValue)
                                            break
                                        }
                                    }
                                }
                            }
                        }
                        # Second try: use the last path segment
                        if (-not $filename) {
                            $pathSegment = $uri.Segments[-1]
                            if ($pathSegment -and $pathSegment -match '\.(exe|msi|zip)$') {
                                $filename = $pathSegment
                            }
                        }
                    } catch {
                        $filename = ($dlUrl -split '\?')[0]
                        $filename = $filename.Substring($filename.LastIndexOf('/') + 1)
                    }
                    # Final fallback
                    if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.(exe|msi|zip)$' -or $filename -match '[?&=<>:"|*]') {
                        $filename = "installer_$([guid]::NewGuid().ToString('N')).exe"
                    }
                    Write-ParallelLog "Installer filename: $filename" 'Info'

                    # Parallel runspace - intentional direct env usage
                    $tempDir = Join-Path $env:TEMP "Win11Forge_$([guid]::NewGuid().ToString('N'))"
                    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
                    $tempFile = Join-Path $tempDir $filename

                    Write-ParallelLog "Downloading to: $tempFile" 'Verbose'

                    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

                    $downloadSuccess = $false

                    # Method 1: Invoke-WebRequest (modern, secure, replaces deprecated WebClient)
                    try {
                        $ProgressPreference = 'SilentlyContinue'
                        $headers = @{
                            'User-Agent' = "Win11Forge/$fwVersion (Windows NT; PowerShell)"
                        }
                        Invoke-WebRequest -Uri $sources.DirectUrl -OutFile $tempFile -Headers $headers -UseBasicParsing -TimeoutSec 600 -ErrorAction Stop
                        if ((Test-Path -Path $tempFile) -and (Get-Item -Path $tempFile).Length -gt 0) { $downloadSuccess = $true }
                    } catch {
                        Write-ParallelLog "Invoke-WebRequest failed: $($_.Exception.Message)" 'Verbose'
                        if (Test-Path -Path $tempFile) { Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue }
                    }

                    # Method 3: BITS transfer
                    if (-not $downloadSuccess) {
                        if (Test-Path -Path $tempFile) { Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue }
                        try {
                            Start-BitsTransfer -Source $sources.DirectUrl -Destination $tempFile -ErrorAction Stop
                            if ((Test-Path -Path $tempFile) -and (Get-Item -Path $tempFile).Length -gt 0) { $downloadSuccess = $true }
                        } catch {
                            Write-ParallelLog "BITS failed: $($_.Exception.Message)" 'Verbose'
                        }
                    }

                    if (-not $downloadSuccess -or -not (Test-Path -Path $tempFile)) {
                        throw (Get-LocalizedString -Key 'orchestrator.result.download_exhausted')
                    }
                    Write-ParallelLog "Download completed" 'Info'

                    # SHA256 checksum validation
                    if ($sources.SHA256) {
                        Write-ParallelLog "Validating SHA256 checksum..." 'Info'
                        $fileHash = (Get-FileHash -Path $tempFile -Algorithm SHA256).Hash
                        if ($fileHash -ne $sources.SHA256) {
                            Write-ParallelLog "Checksum FAILED! Expected: $($sources.SHA256), Got: $fileHash" 'Error'
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.checksum_failed')
                            return $result
                        }
                        Write-ParallelLog "Checksum validation passed" 'Success'
                    }

                    $installerType = switch -Regex ($filename) {
                        '\.msi$' { 'msi' }
                        '\.zip$' { 'zip' }
                        default  { 'exe' }
                    }

                    Write-ParallelLog "Detected installer type: $installerType" 'Info'

                    $processExitCode = -1

                    switch ($installerType) {
                        'msi' {
                            $msiArgs = @('/i', "`"$tempFile`"", '/qn', '/norestart')
                            if ($app.PSObject.Properties['InstallArguments'] -and $app.InstallArguments) {
                                # Security: Validate InstallArguments against whitelist pattern
                                # Allow only safe MSI property patterns: PROPERTY=value, /flag, -flag
                                $safeArgsPattern = '^[A-Za-z0-9_]+=[A-Za-z0-9_.:\\/"-]*$|^[/-][A-Za-z0-9_]+$'
                                $argParts = $app.InstallArguments -split '\s+' | Where-Object { $_ -ne '' }
                                $validatedArgs = @()
                                foreach ($argPart in $argParts) {
                                    if ($argPart -match $safeArgsPattern) {
                                        $validatedArgs += $argPart
                                    } else {
                                        Write-ParallelLog "Security: Skipping unsafe MSI argument: $argPart" 'Warning'
                                    }
                                }
                                if ($validatedArgs.Count -gt 0) {
                                    $msiArgs += $validatedArgs
                                }
                            }
                            $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $msiArgs -Wait -NoNewWindow -PassThru
                            $processExitCode = $process.ExitCode
                        }
                        'zip' {
                            Write-ParallelLog "Extracting ZIP archive" 'Info'
                            $extractPath = Join-Path $tempDir "extracted"
                            # Security: Use safe archive extraction with validation
                            # AllowDangerousExtensions is required because installers may contain .exe files
                            if (Get-Command -Name Expand-ArchiveSafe -ErrorAction SilentlyContinue) {
                                $expandResult = Expand-ArchiveSafe -Path $tempFile -DestinationPath $extractPath -AllowDangerousExtensions
                                if (-not $expandResult) {
                                    Write-ParallelLog "Archive extraction blocked by security validation" 'Error'
                                    throw (Get-LocalizedString -Key 'orchestrator.result.archive_security_failed')
                                }
                            } else {
                                Expand-Archive -Path $tempFile -DestinationPath $extractPath -Force
                            }

                            $setupExe = Get-ChildItem -Path $extractPath -Filter *.exe -Recurse |
                                Where-Object { $_.Name -match 'setup|install' } |
                                Select-Object -First 1

                            if ($setupExe) {
                                $zipArgs = '/S'
                                if ($app.PSObject.Properties['InstallArguments'] -and $app.InstallArguments) {
                                    # Security: Validate InstallArguments - allow common silent switches
                                    $safeExeArgsPattern = '^[/-][A-Za-z0-9_=]+$|^--[A-Za-z0-9_-]+(=[A-Za-z0-9_.:\\/"-]*)?$'
                                    if ($app.InstallArguments -match $safeExeArgsPattern -or
                                        $app.InstallArguments -match '^[/-][Ss](ilent)?$') {
                                        $zipArgs = $app.InstallArguments
                                    } else {
                                        Write-ParallelLog "Security: Using default /S - unsafe arguments blocked" 'Warning'
                                    }
                                }
                                $process = Start-Process -FilePath $setupExe.FullName -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru
                                $processExitCode = $process.ExitCode
                            } else {
                                Write-ParallelLog "No installer found - deploying portable tools" 'Info'

                                $destinationPath = $null
                                if ($app.Detection -and $app.Detection.Path) {
                                    $destinationPath = Split-Path $app.Detection.Path -Parent
                                }

                                if (-not $destinationPath) {
                                    $destinationPath = Join-Path ${env:ProgramFiles} $app.Name
                                }

                                if (-not (Test-Path $destinationPath)) {
                                    New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
                                }

                                Copy-Item -Path "$extractPath\*" -Destination $destinationPath -Recurse -Force
                                $processExitCode = 0
                            }
                        }
                        'exe' {
                            $exeArgs = '/S'
                            if ($app.PSObject.Properties['InstallArguments'] -and $app.InstallArguments) {
                                # Security: Validate InstallArguments - allow common silent switches
                                $safeExeArgsPattern = '^[/-][A-Za-z0-9_=]+$|^--[A-Za-z0-9_-]+(=[A-Za-z0-9_.:\\/"-]*)?$'
                                if ($app.InstallArguments -match $safeExeArgsPattern -or
                                    $app.InstallArguments -match '^[/-][Ss](ilent)?$') {
                                    $exeArgs = $app.InstallArguments
                                } else {
                                    Write-ParallelLog "Security: Using default /S - unsafe arguments blocked" 'Warning'
                                }
                            }
                            $process = Start-Process -FilePath $tempFile -ArgumentList $exeArgs -Wait -NoNewWindow -PassThru
                            $processExitCode = $process.ExitCode
                        }
                    }

                    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

                    if ($processExitCode -eq 0) {
                        Write-ParallelLog "Installed successfully via direct download" 'Success'
                        $result.Success = $true
                        $result.Method = 'DirectDownload'
                        $result.Message = (Get-LocalizedString -Key 'orchestrator.result.direct_download')
                        return $result
                    } else {
                        Write-ParallelLog "Direct download installation failed (exit code: $processExitCode)" 'Warning'
                    }
                } catch {
                    Write-ParallelException -ErrorRecord $_ -Context 'DirectDownload'
                }
            }

            Write-ParallelLog "All installation methods failed" 'Error'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.all_failed')

        } catch {
            Write-ParallelException -ErrorRecord $_ -Context 'MainInstallLoop'
            $result.Message = (Get-LocalizedString -Key 'orchestrator.result.error' -Parameters @{ Error = $_.Exception.Message })
        }

        if ($result.Success -or $result.AlreadyInstalled) {
            $status = if ($result.AlreadyInstalled) { "Already Installed" } else { "Success" }
            Write-ParallelLog "RESULT: $status - $($result.Message)" 'Success'
        } else {
            Write-ParallelLog "RESULT: Failed - $($result.Message)" 'Error'
        }

        return $result
    }

    $allResults = @($installResults) + @($skippedApps)

    $endTime = Get-Date
    $totalTime = $endTime - $startTime

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'parallel.summary.title') -ForegroundColor Green
    Write-Host (Get-LocalizedString -Key 'parallel.summary.total_time' -Parameters @{ Time = $totalTime.ToString('mm\:ss') }) -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'parallel.summary.apps_processed' -Parameters @{ Count = $Applications.Count }) -ForegroundColor Cyan
    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'parallel.logs_directory' -Parameters @{ Path = $parallelLogsDir }) -ForegroundColor Yellow
    Write-Host (Get-LocalizedString -Key 'parallel.logs_pattern' -Parameters @{ Timestamp = $timestamp }) -ForegroundColor Gray
    Write-Host ""

    Write-Host (Get-LocalizedString -Key 'parallel.summary.results_title') -ForegroundColor Cyan
    foreach ($result in $allResults) {
        if ($result.PSObject.Properties['Skipped'] -and $result.Skipped) {
            Write-Host (Get-LocalizedString -Key 'parallel.summary.result_skip' -Parameters @{ AppName = $result.ApplicationName }) -ForegroundColor Yellow
            Write-Host "    $(Get-LocalizedString -Key 'parallel.summary.reason' -Parameters @{ Message = $result.Message })" -ForegroundColor Gray
        } elseif ($result.Success -or $result.AlreadyInstalled) {
            $status = if ($result.AlreadyInstalled) { (Get-LocalizedString -Key 'install.already_installed' -Parameters @{ AppName = '' }) } else { (Get-LocalizedString -Key 'common.success') }
            Write-Host (Get-LocalizedString -Key 'parallel.summary.result_ok' -Parameters @{ AppName = $result.ApplicationName; Status = $status }) -ForegroundColor Green
            if ($result.Method) {
                Write-Host "    $(Get-LocalizedString -Key 'parallel.summary.method_used' -Parameters @{ Method = $result.Method })" -ForegroundColor Gray
            }
        } else {
            Write-Host (Get-LocalizedString -Key 'parallel.summary.result_failed' -Parameters @{ AppName = $result.ApplicationName }) -ForegroundColor Red
            Write-Host "    $(Get-LocalizedString -Key 'parallel.summary.reason' -Parameters @{ Message = $result.Message })" -ForegroundColor Gray
        }
    }

    Write-Host ""

    return $allResults
}

# === EXPORTS ===
# State management functions (Initialize-RollbackSession, Save-RollbackState,
# Add-RollbackEntry, Get-RollbackState, Clear-RollbackState, Initialize-DeploymentSession,
# Save-DeploymentState, Update-DeploymentProgress, Test-ValidStateData, Get-DeploymentState,
# Clear-DeploymentState) are exported by StateManager.psm1.
Export-ModuleMember -Function @(
    # Rollback orchestration (state functions from StateManager)
    'Invoke-Rollback',

    # Deployment wrappers (delegate to StateManager)
    'Test-IncompleteDeployment',
    'Resume-Deployment',

    # Environment
    'Test-EnvironmentRestriction',

    # Orchestration
    'Invoke-InstallationMethodSequence',
    'Get-ApplicationSources',
    'Invoke-ApplicationUpgrade',
    'Install-Application',
    'Install-ApplicationsParallel'
)

