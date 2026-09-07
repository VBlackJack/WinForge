<#
.SYNOPSIS
    WinForge - Installation Orchestrator v3.7.2

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
    v3.7.2

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

# Resolve feature flags in this module's scope, including direct imports.
Import-Module (Join-Path $script:RepositoryRoot 'Core\FeatureFlags.psm1') -ErrorAction Stop

# Import required modules
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
$script:EnvironmentDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue) -or
    -not (Get-Command -Name Invoke-NativeCommandUtf8 -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

if (-not (Get-Command -Name Get-LogString -ErrorAction SilentlyContinue)) {
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
if (-not (Get-Command -Name Install-ViaWinget -ErrorAction SilentlyContinue) -or
    -not (Get-Command -Name Test-ValidDownloadUrl -ErrorAction SilentlyContinue) -or
    -not (Get-Command -Name Test-InstallerSignature -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:InstallationMethodsPath) {
        Import-Module -Name $script:InstallationMethodsPath -Force
    }
}

# DownloadValidation provides the shared direct-download validation leaves used by
# the sequential and parallel paths here (nested imports do not re-export).
$script:DownloadValidationPath = Join-Path $script:ModuleRoot 'DownloadValidation.psm1'
if (-not (Get-Command -Name Test-DirectDownloadChecksumGate -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DownloadValidationPath) {
        Import-Module -Name $script:DownloadValidationPath -Force
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
$script:DefaultMaxParallelJobs = 5
$script:DefaultParallelTimeoutMs = 600000
$script:DefaultJobCheckIntervalSeconds = 2
$script:WingetNoApplicableUpdateExitCode = -1978335189
$script:WingetAlreadyInstalledExitCode = -1978334974
$script:WingetHashMismatchExitCode = -1978335215
$script:WingetNetworkErrorExitCodes = @(-1978335212)
$script:ChocolateyRebootExitCodes = @(1641, 3010)
$script:DirectDownloadTimeoutSeconds = 600

# Parallel installation defaults
$script:ParallelInstallMaxRetries = 3
$script:ParallelLogRetentionDays = 7
$script:ParallelLogSubPath = 'Logs\Parallel'

if (Test-Path -Path $script:TimeoutSettingsPath) {
    Import-Module -Name $script:TimeoutSettingsPath -Force -ErrorAction SilentlyContinue
}

$script:DefaultInstallTimeoutSeconds = 1800
if (Get-Command -Name Get-TimeoutSetting -ErrorAction SilentlyContinue) {
    $configuredInstallTimeout = Get-TimeoutSetting -Name 'DefaultInstallTimeoutSeconds'
    if ($null -ne $configuredInstallTimeout) { $script:DefaultInstallTimeoutSeconds = [int]$configuredInstallTimeout }
}

if (Test-Path -Path $script:StateManagerPath) {
    Import-Module -Name $script:StateManagerPath -Force -ErrorAction SilentlyContinue
}

# Helper functions to get configured timeouts (with fallbacks)
function script:Get-ConfiguredMaxParallelJobs {
    if (Get-Command -Name Get-MaxParallelJobs -ErrorAction SilentlyContinue) {
        return Get-MaxParallelJobs
    }
    return $script:DefaultMaxParallelJobs
}

function script:Get-ConfiguredParallelTimeout {
    if (Get-Command -Name Get-ParallelTimeout -ErrorAction SilentlyContinue) {
        return Get-ParallelTimeout
    }
    return $script:DefaultParallelTimeoutMs
}

function script:Get-ConfiguredJobCheckInterval {
    $config = $null
    if (Get-Command -Name Get-TimeoutSettings -ErrorAction SilentlyContinue) {
        $config = Get-TimeoutSettings
    }
    if ($config -and $config.Parallel.JobCheckIntervalSeconds) {
        return $config.Parallel.JobCheckIntervalSeconds
    }
    return $script:DefaultJobCheckIntervalSeconds
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
        Write-Status -Message (Get-LogString -Key 'rollback.no_apps') -Level 'Info'
        return $result
    }

    Write-Status -Message (Get-LogString -Key 'rollback.rolling_back' -Parameters @{ Count = $rollbackState.InstalledApps.Count }) -Level 'Info'

    foreach ($app in $rollbackState.InstalledApps) {
        $uninstalled = $false

        try {
            switch ($app.Method) {
                'Winget' {
                    if ($app.Identifier -and (Get-Command winget -ErrorAction SilentlyContinue)) {
                        $wingetArgs = @('uninstall', '--id', $app.Identifier, '--silent', '--accept-source-agreements')
                        $process = Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList $wingetArgs -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
                        $uninstalled = ($null -ne $process -and $process.ExitCode -eq 0)
                    }
                }
                'Chocolatey' {
                    if ($app.Identifier -and (Get-Command choco -ErrorAction SilentlyContinue)) {
                        $chocoArgs = @('uninstall', $app.Identifier, '-y')
                        $process = Invoke-NativeCommandUtf8 -FilePath 'choco' -ArgumentList $chocoArgs -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
                        $uninstalled = ($null -ne $process -and $process.ExitCode -eq 0)
                    }
                }
                default {
                    Write-Status -Message (Get-LogString -Key 'rollback.cannot_auto_rollback' -Parameters @{ AppName = $app.AppName; Method = $app.Method }) -Level 'Warning'
                }
            }

            if ($uninstalled) {
                Remove-RollbackEntry -Entry $app
                Write-Status -Message (Get-LogString -Key 'rollback.rolled_back' -Parameters @{ AppName = $app.AppName }) -Level 'Success'
                $result.RolledBack += $app.AppName
            } else {
                Write-Status -Message (Get-LogString -Key 'rollback.rollback_failed' -Parameters @{ AppName = $app.AppName }) -Level 'Warning'
                $result.Failed += $app.AppName
                $result.Success = $false
            }
        } catch {
            Write-Status -Message (Get-LogString -Key 'rollback.rollback_error' -Parameters @{ AppName = $app.AppName; Error = $_.Exception.Message }) -Level 'Error'
            $result.Failed += $app.AppName
            $result.Success = $false
        }
    }

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
        Write-Status -Message (Get-LogString -Key 'deployment.no_incomplete') -Level 'Info'
        return $null
    }

    Write-Status -Message (Get-LogString -Key 'deployment.resuming' -Parameters @{ ProfileName = $state.ProfileName }) -Level 'Info'
    Write-Status -Message "  $(Get-LogString -Key 'deployment.completed_count' -Parameters @{ Count = $state.CompletedApps.Count })" -Level 'Info'
    Write-Status -Message "  $(Get-LogString -Key 'deployment.pending_count' -Parameters @{ Count = $state.PendingApps.Count })" -Level 'Info'
    Write-Status -Message "  $(Get-LogString -Key 'deployment.failed_count' -Parameters @{ Count = $state.FailedApps.Count })" -Level 'Info'

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
            $result.Message = (Get-LogString -Key 'engine.env_restricted' -Parameters @{ AppName = $Application.Name; Environment = $currentEnv })
            Write-Status -Message $result.Message -Level 'Warning'
        }
    } catch {
        Write-Status -Message (Get-LogString -Key 'engine.env_check_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
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
        $result.Message = (Get-LogString -Key 'orchestrator.no_sources')
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

    $completeSuccessfulInstall = {
        param(
            [string]$MethodName,
            [string]$MessageKey
        )

        $result.Success = $true
        $result.Method = $MethodName
        $result.Message = (Get-LogString -Key $MessageKey)
        return $result
    }

    $waitForOfficeIfNeeded = {
        param([bool]$OfficeAware)

        if (-not $OfficeAware -or -not $isOfficeApp) {
            return
        }

        & $writeLog (Get-LogString -Key 'install.orchestrator.office_c2r_waiting') 'Info'
        $officeTimeout = Get-InstallationTimeout -AppName 'Office'
        $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $officeTimeout
        if (-not $officeInstalled) {
            $result.FailureReasons += (Get-LogString -Key 'orchestrator.failure.office_c2r_timeout')
        }
    }

    $installMethods = @(
        @{
            Name = 'Winget'
            Source = $sources.Winget
            AttemptKey = 'install.orchestrator.attempting_winget'
            AttemptParameters = { param($source) @{ PackageId = $source } }
            Invoke = {
                param($source)
                if ($Application.PSObject.Properties['SourceLock'] -and $Application.SourceLock) {
                    @(Install-ViaWinget -PackageId $source -Version $Application.SourceLock.Version)
                } else { @(Install-ViaWinget -PackageId $source) }
            }
            ResultKey = 'orchestrator.result.winget'
            VerifiedResultKey = 'orchestrator.result.winget_verified'
            FailureKey = 'orchestrator.failure.winget'
            FailureParameters = { param($source) @{ PackageId = $source } }
            OfficeAware = $true
        },
        @{
            Name = 'Chocolatey'
            Source = $sources.Chocolatey
            AttemptKey = 'install.orchestrator.attempting_choco'
            AttemptParameters = { param($source) @{ PackageId = $source } }
            Invoke = {
                param($source)
                if ($Application.PSObject.Properties['SourceLock'] -and $Application.SourceLock) {
                    @(Install-ViaChocolatey -PackageName $source -Version $Application.SourceLock.Version)
                } else { @(Install-ViaChocolatey -PackageName $source) }
            }
            ResultKey = 'orchestrator.result.chocolatey'
            VerifiedResultKey = 'orchestrator.result.chocolatey_verified'
            FailureKey = 'orchestrator.failure.chocolatey'
            FailureParameters = { param($source) @{ PackageId = $source } }
            OfficeAware = $true
        },
        @{
            Name = 'Store'
            Source = $sources.Store
            AttemptKey = 'install.orchestrator.attempting_store'
            AttemptParameters = { param($source) @{ ProductId = $source } }
            Invoke = { param($source) @(Install-ViaStore -ProductId $source) }
            ResultKey = 'orchestrator.result.store'
            VerifiedResultKey = $null
            FailureKey = 'orchestrator.failure.store'
            FailureParameters = { param($source) @{ ProductId = $source } }
            OfficeAware = $false
        },
        @{
            Name = 'DirectDownload'
            Source = $sources.DirectUrl
            AttemptKey = 'install.orchestrator.attempting_direct'
            AttemptParameters = { param($source) @{ Url = $source } }
            Invoke = {
                param($source)

                $installParams = @{ Url = $source }

                $installArgs = if ($Application.PSObject.Properties['InstallArguments']) { $Application.InstallArguments } else { $null }
                if ($installArgs) {
                    $installParams['CustomArguments'] = $installArgs
                    & $writeLog (Get-LogString -Key 'install.orchestrator.custom_args_detected' -Parameters @{ Arguments = $installArgs }) 'Verbose'
                }

                if ($Application.Detection -and $Application.Detection.Path) {
                    $installParams['DetectionPath'] = $Application.Detection.Path
                }

                # Canonical checksum accessor (shared: DownloadValidation\Get-ExpectedChecksum).
                # SKIP_VALIDATION flows through so the fail-closed gate in
                # Install-ViaDirectDownload can honor the opt-out.
                $expectedChecksum = Get-ExpectedChecksum -Sources $sources
                if ($expectedChecksum) {
                    $installParams['ExpectedSHA256'] = $expectedChecksum
                }

                if ($sources.PSObject.Properties['ExpectedPublisher'] -and $sources.ExpectedPublisher) {
                    $installParams['ExpectedPublisher'] = $sources.ExpectedPublisher
                }

                Write-Verbose "Calling Install-ViaDirectDownload"
                Write-Verbose "installParams: $($installParams | ConvertTo-Json -Compress)"
                $downloadOutput = @(Install-ViaDirectDownload @installParams)
                Write-Verbose "Install-ViaDirectDownload returned"
                Write-Verbose "downloadOutput count: $($downloadOutput.Count)"
                return $downloadOutput
            }
            ResultKey = 'orchestrator.result.direct_download'
            VerifiedResultKey = $null
            FailureKey = 'orchestrator.failure.direct_download'
            FailureParameters = { @{} }
            OfficeAware = $false
        }
    )

    if ($Application.PSObject.Properties['SourceLock'] -and $Application.SourceLock) {
        $sourceLock = $Application.SourceLock
        if ($sourceLock.Method -notin @('Winget','Chocolatey') -or -not $sourceLock.Version -or
            $sourceLock.Identifier -cne $sources.($sourceLock.Method)) {
            throw 'Invalid source lock: method, identifier and version must match a supported catalog source.'
        }
        $installMethods = @($installMethods | Where-Object { $_.Name -eq $sourceLock.Method })
    }
    foreach ($method in $installMethods) {
        if (-not $method.Source) {
            continue
        }

        $result.AttemptedMethods += $method.Name
        $attemptParameterFactory = $method.AttemptParameters
        & $writeLog (Get-LogString -Key $method.AttemptKey -Parameters (& $attemptParameterFactory $method.Source)) 'Verbose'

        $installer = $method.Invoke
        $installOutput = & $installer $method.Source
        if (& $getInstallResult $installOutput) {
            & $waitForOfficeIfNeeded ([bool]$method.OfficeAware)
            return (& $completeSuccessfulInstall $method.Name $method.ResultKey)
        }

        if ($method.VerifiedResultKey -and (& $testIgnoreExitCode)) {
            & $writeLog (Get-LogString -Key 'install.orchestrator.success_despite_exit_code') 'Success'
            return (& $completeSuccessfulInstall $method.Name $method.VerifiedResultKey)
        }

        $failureParameterFactory = $method.FailureParameters
        $failureParameters = & $failureParameterFactory $method.Source
        if ($failureParameters.Count -gt 0) {
            $result.FailureReasons += (Get-LogString -Key $method.FailureKey -Parameters $failureParameters)
        } else {
            $result.FailureReasons += (Get-LogString -Key $method.FailureKey)
        }
    }

    $result.Message = if ($result.AttemptedMethods.Count -gt 0) {
        Get-LogString -Key 'orchestrator.all_methods_failed' -Parameters @{ Reasons = ($result.FailureReasons -join '; ') }
    } else {
        Get-LogString -Key 'orchestrator.no_valid_sources'
    }

    & $writeLog (Get-LogString -Key 'orchestrator.install_failed' -Parameters @{ Message = $result.Message }) 'Warning'
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
            Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.attempting_winget' -Parameters @{ PackageId = $sources.Winget }) -Level 'Info'

            $arguments = @(
                'upgrade',
                '--id', $sources.Winget,
                '--exact',
                '--source', 'winget',
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            $process = Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList $arguments -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                if (Get-Command Clear-WingetCache -ErrorAction SilentlyContinue) { Clear-WingetCache }
                if (Get-Command Clear-RegistryAppsCache -ErrorAction SilentlyContinue) { Clear-RegistryAppsCache }
                Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.success_winget' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = (Get-LogString -Key 'orchestrator.result.upgraded')
                return $result
            } elseif ($process.ExitCode -eq $script:WingetNoApplicableUpdateExitCode) {
                Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.no_update_winget' -Parameters @{ AppName = $Application.Name }) -Level 'Verbose'
            } else {
                Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.exit_code_winget' -Parameters @{ ExitCode = $process.ExitCode }) -Level 'Verbose'
            }
        } catch {
            Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.error_winget' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
        }
    }

    if ($sources.Chocolatey -and (Test-CommandExists -Name 'choco')) {
        try {
            Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.attempting_choco' -Parameters @{ PackageId = $sources.Chocolatey }) -Level 'Info'

            $arguments = @(
                'upgrade',
                $sources.Chocolatey,
                '-y',
                '--no-progress'
            )

            $process = Invoke-NativeCommandUtf8 -FilePath 'choco' -ArgumentList $arguments -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.success_choco' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
                $result.Success = $true
                $result.Method = 'Chocolatey'
                $result.Message = (Get-LogString -Key 'orchestrator.result.upgraded')
                return $result
            } else {
                Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.exit_code_choco' -Parameters @{ ExitCode = $process.ExitCode }) -Level 'Verbose'
            }
        } catch {
            Write-Status -Message (Get-LogString -Key 'orchestrator.upgrade.error_choco' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
        }
    }

    $result.Message = (Get-LogString -Key 'orchestrator.result.no_upgrade')
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

    .PARAMETER SkipRollbackJournal
        The caller owns a separate durable deployment receipt.

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
        [switch]$ForceUpdate,
        [switch]$SkipRollbackJournal
    )

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $false
        Method = $null
        Message = ''
    }

    if ($Application.PSObject.Properties['SourceLock'] -and $Application.SourceLock) {
        $lock = $Application.SourceLock
        if ($lock.Method -notin @('Winget','Chocolatey') -or
            $lock.Version -notmatch '^[A-Za-z0-9][A-Za-z0-9._+-]*$' -or
            ($Application.PSObject.Properties['InstallMethod'] -and $Application.InstallMethod) -or
            -not $Application.Sources -or $lock.Identifier -cne $Application.Sources.($lock.Method)) {
            $result.Message = 'Invalid source lock: use an exact supported package source and version without a custom installer.'
            return $result
        }
    }

    # 1. Check environment restrictions
    $envCheck = Test-EnvironmentRestriction -Application $Application
    if ($envCheck.Restricted) {
        $result.Message = (Get-LogString -Key 'engine.env_not_compatible' -Parameters @{ Environment = $envCheck.Environment })
        return $result
    }

    # 2. Check if already installed
    $isInstalled = Test-ApplicationInstalled -Application $Application
    $result.WasInstalled = [bool]$isInstalled
    if ($isInstalled -and $Application.PSObject.Properties['SourceLock'] -and $Application.SourceLock) {
        # A pinned profile must not silently upgrade, downgrade or accept an unknown version.
        $observed = Get-ApplicationsInstallationStatus -Applications @($Application)
        $actualVersion = $observed[$Application.AppId].Version
        if (-not $actualVersion -or $actualVersion -cne $Application.SourceLock.Version) {
            $result.Message = 'Installed version does not match the source lock; reconcile it before retrying.'
            return $result
        }
        $result.Success = $true
        $result.AlreadyInstalled = $true
        $result.Message = Get-LogString -Key 'orchestrator.already_installed_status'
        return $result
    }
    if (-not $Force) {
        if ($isInstalled) {
            if ($ForceUpdate) {
                Write-Status -Message (Get-LogString -Key 'orchestrator.checking_updates' -Parameters @{ AppName = $Application.Name }) -Level 'Info'
                $upgradeResult = Invoke-ApplicationUpgrade -Application $Application
                if ($upgradeResult.Success) {
                    $upgradeResult.WasInstalled = $true
                    return $upgradeResult
                }
                Write-Status -Message (Get-LogString -Key 'orchestrator.no_update_available' -Parameters @{ AppName = $Application.Name }) -Level 'Info'
                $result.AlreadyInstalled = $true
                $result.Success = $true
                $result.Message = (Get-LogString -Key 'orchestrator.already_installed_no_update')
                return $result
            }

            Write-Status -Message (Get-LogString -Key 'orchestrator.already_installed' -Parameters @{ AppName = $Application.Name }) -Level 'Success'
            $result.AlreadyInstalled = $true
            $result.Success = $true
            $result.Message = (Get-LogString -Key 'orchestrator.already_installed_status')
            return $result
        }
    }

    Write-Status -Message (Get-LogString -Key 'orchestrator.installing' -Parameters @{ AppName = $Application.Name }) -Level 'Info'

    # 3. Handle custom install methods (WindowsFeature, WindowsCapability)
    $installMethod = if ($Application.PSObject.Properties['InstallMethod']) { $Application.InstallMethod } else { $null }
    if ($installMethod) {
        $result = Invoke-CustomInstallMethod -Application $Application
    } else {
        $result = Invoke-InstallationMethodSequence -Application $Application
    }
    $result.WasInstalled = [bool]$isInstalled
    if (-not $SkipRollbackJournal) {
        Add-InstallationRollbackEntry -Application $Application -Result $result -WasInstalled ([bool]$isInstalled)
    }
    return $result
}

function Install-ApplicationsParallel {
    <#
    .SYNOPSIS
        Runs the common installation engine with bounded concurrency.
    .DESCRIPTION
        Workers import the same orchestrator used by sequential and GUI installs.
        Source fallback, retries, timeout enforcement and rollback recording remain
        in Install-Application and its leaf methods. PowerShell 5.1 runs sequentially.
    #>
    [CmdletBinding()]
    [OutputType([hashtable[]])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications,
        [switch]$Force,
        [switch]$ForceUpdate,
        [ValidateRange(1, 10)]
        [int]$MaxParallel = 5
    )

    $orderedApps = @($Applications | Sort-Object -Property Priority)
    if ($PSVersionTable.PSVersion.Major -lt 7 -or $MaxParallel -eq 1) {
        foreach ($application in $orderedApps) {
            try {
                Install-Application -Application $application -Force:$Force -ForceUpdate:$ForceUpdate
            } catch {
                @{ ApplicationName=$application.Name; Success=$false; AlreadyInstalled=$false; Method=$null; Message=$_.Exception.Message }
            }
        }
        return
    }

    $orchestratorPath = Join-Path $script:RepositoryRoot 'Modules/InstallationOrchestrator.psm1'
    $forceInstall = $Force.IsPresent
    $forceUpgrade = $ForceUpdate.IsPresent
    $orderedApps | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $application = $_
        try {
            Import-Module -Name $using:orchestratorPath -ErrorAction Stop
            Install-Application -Application $application -Force:$using:forceInstall -ForceUpdate:$using:forceUpgrade
        } catch {
            @{ ApplicationName=$application.Name; Success=$false; AlreadyInstalled=$false; Method=$null; Message=$_.Exception.Message }
        }
    }
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
