<#
.SYNOPSIS
    WinForge - Rollback Manager v3.7.2

.DESCRIPTION
    Provides enhanced rollback management capabilities for WinForge:
    - Auto-rollback on critical failures with configurable thresholds
    - User confirmation prompts before rollback
    - Rollback summary and reporting
    - Critical failure classification and handling
    - Rollback capability testing

.NOTES
    Author: Julien Bombled
    v3.7.2
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
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\rollback-settings.json'
$script:FeatureFlagsPath = Join-Path $script:RepositoryRoot 'Core\FeatureFlags.psm1'
$script:ExceptionsPath = Join-Path $script:RepositoryRoot 'Core\WinForgeExceptions.psm1'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module
if (-not (Get-Command -Name Get-LogString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# Import Feature Flags module
if (-not (Get-Command -Name Test-FeatureEnabled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:FeatureFlagsPath) {
        Import-Module -Name $script:FeatureFlagsPath -Force
    }
}

# === CONFIGURATION ===
$script:DefaultConfig = @{
    AutoRollbackEnabled = $true
    AutoRollbackThreshold = 3
    RequireConfirmation = $true
    ConfirmationTimeoutSeconds = 30
    CriticalFailurePatterns = @(
        'system32',
        'registry corruption',
        'boot',
        'driver',
        'kernel'
    )
}

$script:RollbackConfig = $null
$script:FailureCount = 0
$script:CriticalFailureHandlers = @()

# === CONFIGURATION FUNCTIONS ===

function Get-RollbackConfig {
    <#
    .SYNOPSIS
        Loads and returns the rollback configuration.

    .DESCRIPTION
        Reads rollback settings from rollback-settings.json or returns defaults.

    .OUTPUTS
        Hashtable containing rollback configuration.

    .EXAMPLE
        $config = Get-RollbackConfig
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($null -eq $script:RollbackConfig) {
        if (Test-Path -Path $script:ConfigPath) {
            try {
                $json = Get-Content -Path $script:ConfigPath -Raw | ConvertFrom-Json
                $script:RollbackConfig = @{
                    AutoRollbackEnabled = if ($null -ne $json.autoRollbackEnabled) { $json.autoRollbackEnabled } else { $script:DefaultConfig.AutoRollbackEnabled }
                    AutoRollbackThreshold = if ($null -ne $json.autoRollbackThreshold) { $json.autoRollbackThreshold } else { $script:DefaultConfig.AutoRollbackThreshold }
                    RequireConfirmation = if ($null -ne $json.requireConfirmation) { $json.requireConfirmation } else { $script:DefaultConfig.RequireConfirmation }
                    ConfirmationTimeoutSeconds = if ($null -ne $json.confirmationTimeoutSeconds) { $json.confirmationTimeoutSeconds } else { $script:DefaultConfig.ConfirmationTimeoutSeconds }
                    CriticalFailurePatterns = if ($null -ne $json.criticalFailurePatterns) { @($json.criticalFailurePatterns) } else { $script:DefaultConfig.CriticalFailurePatterns }
                }
                Write-Verbose (Get-LogString -Key 'rollbackManager.config.loaded' -Parameters @{ Path = $script:ConfigPath })
            } catch {
                Write-Warning (Get-LogString -Key 'rollbackManager.config.loadFailed' -Parameters @{ Error = $_.Exception.Message })
                $script:RollbackConfig = $script:DefaultConfig.Clone()
            }
        } else {
            $script:RollbackConfig = $script:DefaultConfig.Clone()
        }
    }

    return $script:RollbackConfig
}

# === AUTO-ROLLBACK FUNCTIONS ===

function Enable-AutoRollbackOnFailure {
    <#
    .SYNOPSIS
        Enables automatic rollback when failure threshold is reached.

    .DESCRIPTION
        When enabled, the system will automatically trigger a rollback
        if the number of consecutive failures reaches the configured threshold.

    .PARAMETER Threshold
        Optional custom threshold (overrides config setting).

    .EXAMPLE
        Enable-AutoRollbackOnFailure
        Enable-AutoRollbackOnFailure -Threshold 5
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateRange(1, 100)]
        [int]$Threshold
    )

    $config = Get-RollbackConfig
    $config.AutoRollbackEnabled = $true

    if ($PSBoundParameters.ContainsKey('Threshold')) {
        $config.AutoRollbackThreshold = $Threshold
    }

    $script:FailureCount = 0
    Write-Status -Message (Get-LogString -Key 'rollbackManager.enabled' -Parameters @{ Threshold = $config.AutoRollbackThreshold }) -Level 'Info'
}

function Disable-AutoRollbackOnFailure {
    <#
    .SYNOPSIS
        Disables automatic rollback on failure.

    .DESCRIPTION
        Prevents automatic rollback triggers. Manual rollback is still available.

    .EXAMPLE
        Disable-AutoRollbackOnFailure
    #>
    [CmdletBinding()]
    param()

    $config = Get-RollbackConfig
    $config.AutoRollbackEnabled = $false
    $script:FailureCount = 0
    Write-Status -Message (Get-LogString 'rollbackManager.disabled') -Level 'Info'
}

function Register-CriticalFailureHandler {
    <#
    .SYNOPSIS
        Registers a callback handler for critical failures.

    .DESCRIPTION
        Registers a script block that will be invoked when a critical
        failure is detected. Multiple handlers can be registered.

    .PARAMETER Handler
        Script block to invoke on critical failure. Receives failure details as parameter.

    .PARAMETER Name
        Optional name for the handler (for identification/removal).

    .OUTPUTS
        String containing the handler ID.

    .EXAMPLE
        Register-CriticalFailureHandler -Handler { param($details) Send-Alert $details } -Name "AlertHandler"
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Handler,

        [Parameter()]
        [string]$Name = "Handler_$(New-Guid)"
    )

    $handlerEntry = @{
        Id = $Name
        Handler = $Handler
        RegisteredAt = Get-Date
    }

    $script:CriticalFailureHandlers += $handlerEntry
    Write-Verbose (Get-LogString -Key 'rollbackManager.handler_registered' -Parameters @{ Name = $Name })
    return $Name
}

function Unregister-CriticalFailureHandler {
    <#
    .SYNOPSIS
        Removes a registered critical failure handler.
    .DESCRIPTION
        Removes a previously registered critical failure handler by its name/ID from the handlers
        list, so it will no longer be invoked when the failure threshold is reached.

    .PARAMETER Name
        Name/ID of the handler to remove.

    .EXAMPLE
        Unregister-CriticalFailureHandler -Name "AlertHandler"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $script:CriticalFailureHandlers = @($script:CriticalFailureHandlers | Where-Object { $_.Id -ne $Name })
    Write-Verbose (Get-LogString -Key 'rollbackManager.handler.unregistered' -Parameters @{ Name = $Name })
}

# === FAILURE TRACKING ===

function Register-InstallationFailure {
    <#
    .SYNOPSIS
        Records an installation failure and checks if rollback is needed.

    .DESCRIPTION
        Increments the failure counter and evaluates whether the threshold
        for automatic rollback has been reached. If a critical failure is
        detected, registered handlers are invoked.

    .PARAMETER AppName
        Name of the application that failed to install.

    .PARAMETER ErrorMessage
        Error message describing the failure.

    .PARAMETER IsCritical
        If specified, treats this as a critical failure regardless of pattern matching.

    .OUTPUTS
        PSCustomObject with failure details and rollback recommendation.

    .EXAMPLE
        Register-InstallationFailure -AppName "VSCode" -ErrorMessage "Download failed"
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter()]
        [string]$ErrorMessage,

        [Parameter()]
        [switch]$IsCritical
    )

    if (-not $PSBoundParameters.ContainsKey('ErrorMessage')) {
        $ErrorMessage = Get-LogString 'rollbackManager.error.unknown'
    }

    $config = Get-RollbackConfig
    $script:FailureCount++

    # Check if this is a critical failure
    $isCriticalFailure = $IsCritical.IsPresent
    if (-not $isCriticalFailure -and $config.CriticalFailurePatterns) {
        foreach ($pattern in $config.CriticalFailurePatterns) {
            if ($ErrorMessage -match $pattern) {
                $isCriticalFailure = $true
                break
            }
        }
    }

    $failureDetails = [PSCustomObject]@{
        AppName = $AppName
        ErrorMessage = $ErrorMessage
        Timestamp = Get-Date
        FailureNumber = $script:FailureCount
        IsCritical = $isCriticalFailure
        ShouldRollback = $false
        RollbackReason = $null
    }

    # Invoke critical failure handlers
    if ($isCriticalFailure) {
        Write-Status -Message (Get-LogString -Key 'rollbackManager.critical_detected' -Parameters @{ AppName = $AppName }) -Level 'Error'
        foreach ($handlerEntry in $script:CriticalFailureHandlers) {
            try {
                & $handlerEntry.Handler $failureDetails
            } catch {
                Write-Verbose (Get-LogString -Key 'rollbackManager.handler.failed' -Parameters @{ Name = $handlerEntry.Id; Error = $_.Exception.Message })
            }
        }
        $failureDetails.ShouldRollback = $true
        $failureDetails.RollbackReason = Get-LogString 'rollbackManager.reason.criticalFailure'
    }

    # Check threshold
    if ($config.AutoRollbackEnabled -and $script:FailureCount -ge $config.AutoRollbackThreshold) {
        $failureDetails.ShouldRollback = $true
        $failureDetails.RollbackReason = Get-LogString -Key 'rollbackManager.reason.thresholdReached' -Parameters @{ Count = $script:FailureCount }
        Write-Status -Message (Get-LogString -Key 'rollbackManager.threshold_reached' -Parameters @{ Count = $script:FailureCount }) -Level 'Warning'
    }

    return $failureDetails
}

function Reset-FailureCount {
    <#
    .SYNOPSIS
        Resets the failure counter to zero.

    .DESCRIPTION
        Call this after a successful installation to reset the consecutive failure count.

    .EXAMPLE
        Reset-FailureCount
    #>
    [CmdletBinding()]
    param()

    $script:FailureCount = 0
    Write-Verbose (Get-LogString 'rollbackManager.failureCount.reset')
}

# === ROLLBACK EXECUTION ===

function Invoke-RollbackWithConfirmation {
    <#
    .SYNOPSIS
        Initiates rollback with optional user confirmation.

    .DESCRIPTION
        Prompts the user for confirmation (if configured) before executing
        the rollback. Includes a configurable timeout with auto-proceed option.

    .PARAMETER Force
        Skips confirmation prompt and executes rollback immediately.

    .PARAMETER TimeoutSeconds
        Custom timeout for confirmation prompt.

    .PARAMETER AutoProceedOnTimeout
        If true, automatically proceeds with rollback when timeout expires.

    .OUTPUTS
        PSCustomObject with rollback execution results.

    .EXAMPLE
        Invoke-RollbackWithConfirmation
        Invoke-RollbackWithConfirmation -Force
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$Force,

        [Parameter()]
        [int]$TimeoutSeconds,

        [Parameter()]
        [switch]$AutoProceedOnTimeout
    )

    $config = Get-RollbackConfig
    $timeout = if ($PSBoundParameters.ContainsKey('TimeoutSeconds')) { $TimeoutSeconds } else { $config.ConfirmationTimeoutSeconds }

    # Get rollback summary first
    $summary = Get-RollbackSummary
    if ($summary.TotalApps -eq 0) {
        return [PSCustomObject]@{
            Success = $false
            Message = Get-LogString 'rollbackManager.noApps.toRollback'
            AppsRolledBack = 0
            Errors = @()
        }
    }

    # Show summary
    Write-Host ""
    Write-Host (Get-LogString -Key 'rollback.summary_title') -ForegroundColor Cyan
    Write-Host (Get-LogString -Key 'rollback.summary_apps_count' -Parameters @{ Count = $summary.TotalApps }) -ForegroundColor Yellow
    foreach ($app in $summary.Applications) {
        Write-Host (Get-LogString -Key 'rollback.summary_app_item' -Parameters @{ AppName = $app.AppName; Method = $app.Method }) -ForegroundColor Gray
    }
    Write-Host ""

    # Check if confirmation is required
    $proceed = $Force.IsPresent -or (-not $config.RequireConfirmation)

    if (-not $proceed) {
        Write-Host (Get-LogString -Key 'rollback.confirm_prompt' -Parameters @{ Timeout = $timeout }) -ForegroundColor Yellow

        $startTime = Get-Date
        $response = $null

        while ($true) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -eq 'Y') {
                    $proceed = $true
                    break
                } elseif ($key.Key -eq 'N') {
                    $proceed = $false
                    break
                }
            }

            $elapsed = (Get-Date) - $startTime
            if ($elapsed.TotalSeconds -ge $timeout) {
                if ($AutoProceedOnTimeout) {
                    Write-Host (Get-LogString -Key 'rollback.confirm_timeout_proceed') -ForegroundColor Yellow
                    $proceed = $true
                } else {
                    Write-Host (Get-LogString -Key 'rollback.confirm_timeout_cancel') -ForegroundColor Yellow
                    $proceed = $false
                }
                break
            }

            Start-Sleep -Milliseconds 100
        }
    }

    if (-not $proceed) {
        return [PSCustomObject]@{
            Success = $false
            Message = Get-LogString 'rollbackManager.cancelled'
            AppsRolledBack = 0
            Errors = @()
        }
    }

    # Execute rollback via InstallationEngine
    Write-Status -Message (Get-LogString 'rollbackManager.executing') -Level 'Info'
    $result = @{
        Success = $true
        Message = Get-LogString 'rollbackManager.completed'
        AppsRolledBack = 0
        Errors = @()
    }

    try {
        # Import InstallationEngine for rollback functions
        $installEngineModule = Join-Path $script:ModuleRoot 'InstallationEngine.psm1'
        if (Test-Path $installEngineModule) {
            Import-Module $installEngineModule -Force
        }

        if (Get-Command -Name 'Invoke-Rollback' -ErrorAction SilentlyContinue) {
            Invoke-Rollback
            $result.AppsRolledBack = $summary.TotalApps
        } else {
            $result.Success = $false
            $result.Message = Get-LogString 'rollbackManager.functionNotAvailable'
        }
    } catch {
        $result.Success = $false
        $result.Errors += $_.Exception.Message
        $result.Message = Get-LogString -Key 'rollbackManager.failed' -Parameters @{ Error = $_.Exception.Message }
    }

    # Reset failure count after rollback
    Reset-FailureCount

    return [PSCustomObject]$result
}

# === REPORTING FUNCTIONS ===

function Get-RollbackSummary {
    <#
    .SYNOPSIS
        Returns a summary of applications pending rollback.

    .DESCRIPTION
        Retrieves information about all applications that would be
        rolled back if a rollback operation is initiated.

    .OUTPUTS
        PSCustomObject containing rollback summary.

    .EXAMPLE
        Get-RollbackSummary | Format-List
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    # Import InstallationEngine to access rollback state
    $installEngineModule = Join-Path $script:ModuleRoot 'InstallationEngine.psm1'
    if (Test-Path $installEngineModule) {
        Import-Module $installEngineModule -Force
    }

    $rollbackState = $null
    if (Get-Command -Name 'Get-RollbackState' -ErrorAction SilentlyContinue) {
        $rollbackState = Get-RollbackState
    }

    $apps = @()
    if ($rollbackState -and $rollbackState.InstalledApps) {
        foreach ($app in $rollbackState.InstalledApps) {
            $apps += [PSCustomObject]@{
                AppName = $app.AppName
                Method = $app.Method
                PackageId = $app.PackageId
                InstalledAt = $app.InstalledAt
                CanRollback = Test-RollbackCapability -AppName $app.AppName -Method $app.Method
            }
        }
    }

    $rollbackableApps = @($apps | Where-Object { $_.CanRollback })

    return [PSCustomObject]@{
        SessionId = if ($rollbackState) { $rollbackState.SessionId } else { $null }
        StartTime = if ($rollbackState) { $rollbackState.StartTime } else { $null }
        TotalApps = $apps.Count
        Applications = $apps
        RollbackableCount = $rollbackableApps.Count
        CurrentFailureCount = $script:FailureCount
        AutoRollbackEnabled = (Get-RollbackConfig).AutoRollbackEnabled
    }
}

function Export-RollbackReport {
    <#
    .SYNOPSIS
        Exports a detailed rollback report to file.

    .DESCRIPTION
        Generates a comprehensive report of the rollback state and
        exports it to JSON or text format.

    .PARAMETER Path
        Output file path.

    .PARAMETER Format
        Output format: Json or Text.

    .EXAMPLE
        Export-RollbackReport -Path "C:\Logs\rollback-report.json" -Format Json
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('Json', 'Text')]
        [string]$Format = 'Json'
    )

    $summary = Get-RollbackSummary
    $config = Get-RollbackConfig

    $report = [PSCustomObject]@{
        GeneratedAt = Get-Date
        ComputerName = $env:COMPUTERNAME
        UserName = $env:USERNAME
        Summary = $summary
        Configuration = $config
        FailureCount = $script:FailureCount
        RegisteredHandlers = $script:CriticalFailureHandlers.Count
    }

    $directory = Split-Path -Path $Path -Parent
    if (-not (Test-Path -Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    switch ($Format) {
        'Json' {
            $report | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8
        }
        'Text' {
            $separator = '=' * 80
            $subSeparator = '-' * 19
            $textLines = @(
                $separator
                (Get-LogString 'rollbackManager.report.title')
                (Get-LogString -Key 'rollbackManager.report.generated' -Parameters @{ Timestamp = $report.GeneratedAt })
                (Get-LogString -Key 'rollbackManager.report.computer' -Parameters @{ Name = $report.ComputerName })
                (Get-LogString -Key 'rollbackManager.report.user' -Parameters @{ Name = $report.UserName })
                $separator
                ''
                (Get-LogString 'rollbackManager.report.sessionInfo')
                $subSeparator
                (Get-LogString -Key 'rollbackManager.report.sessionId' -Parameters @{ SessionId = $summary.SessionId })
                (Get-LogString -Key 'rollbackManager.report.startTime' -Parameters @{ Time = $summary.StartTime })
                (Get-LogString -Key 'rollbackManager.report.appsInstalled' -Parameters @{ Count = $summary.TotalApps })
                (Get-LogString -Key 'rollbackManager.report.rollbackable' -Parameters @{ Count = $summary.RollbackableCount })
                ''
                (Get-LogString 'rollbackManager.report.configSection')
                ('-' * 13)
                (Get-LogString -Key 'rollbackManager.report.autoRollbackEnabled' -Parameters @{ Value = $config.AutoRollbackEnabled })
                (Get-LogString -Key 'rollbackManager.report.failureThreshold' -Parameters @{ Value = $config.AutoRollbackThreshold })
                (Get-LogString -Key 'rollbackManager.report.requireConfirmation' -Parameters @{ Value = $config.RequireConfirmation })
                (Get-LogString -Key 'rollbackManager.report.confirmationTimeout' -Parameters @{ Value = $config.ConfirmationTimeoutSeconds })
                ''
                (Get-LogString 'rollbackManager.report.statusSection')
                ('-' * 14)
                (Get-LogString -Key 'rollbackManager.report.consecutiveFailures' -Parameters @{ Count = $script:FailureCount })
                (Get-LogString -Key 'rollbackManager.report.registeredHandlers' -Parameters @{ Count = $script:CriticalFailureHandlers.Count })
                ''
                (Get-LogString 'rollbackManager.report.applicationsSection')
                ('-' * 12)
            )

            $textContent = $textLines -join "`n"

            foreach ($app in $summary.Applications) {
                $textContent += "`n- $($app.AppName)`n"
                $textContent += "    $(Get-LogString -Key 'rollbackManager.report.appMethod' -Parameters @{ Method = $app.Method })`n"
                $textContent += "    $(Get-LogString -Key 'rollbackManager.report.appPackageId' -Parameters @{ PackageId = $app.PackageId })`n"
                $textContent += "    $(Get-LogString -Key 'rollbackManager.report.appCanRollback' -Parameters @{ Value = $app.CanRollback })`n"
            }

            $textContent | Set-Content -Path $Path -Encoding UTF8
        }
    }

    Write-Status -Message (Get-LogString -Key 'rollbackManager.report_exported' -Parameters @{ Path = $Path }) -Level 'Info'
}

function Test-RollbackCapability {
    <#
    .SYNOPSIS
        Tests if an application can be rolled back.

    .DESCRIPTION
        Checks whether the specified application's installation method
        supports automatic rollback.

    .PARAMETER AppName
        Name of the application to check.

    .PARAMETER Method
        Installation method used.

    .OUTPUTS
        Boolean indicating if rollback is supported.

    .EXAMPLE
        Test-RollbackCapability -AppName "VSCode" -Method "Winget"
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter()]
        [string]$Method
    )

    # Methods that support automatic rollback
    $rollbackableMethods = @(
        'Winget',
        'Chocolatey',
        'Choco',
        'StoreApp',
        'MsStore'
    )

    # Methods that do NOT support automatic rollback
    $nonRollbackableMethods = @(
        'DirectDownload',
        'Custom',
        'WindowsFeature',
        'WindowsCapability',
        'Manual'
    )

    if ($Method -in $rollbackableMethods) {
        return $true
    }

    if ($Method -in $nonRollbackableMethods) {
        return $false
    }

    # Default to false for unknown methods
    return $false
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Get-RollbackConfig',
    'Enable-AutoRollbackOnFailure',
    'Disable-AutoRollbackOnFailure',
    'Register-CriticalFailureHandler',
    'Unregister-CriticalFailureHandler',
    'Register-InstallationFailure',
    'Reset-FailureCount',
    'Invoke-RollbackWithConfirmation',
    'Get-RollbackSummary',
    'Export-RollbackReport',
    'Test-RollbackCapability'
)
