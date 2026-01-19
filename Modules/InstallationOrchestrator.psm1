<#
.SYNOPSIS
    Win11Forge - Installation Orchestrator Module v3.2.2

.DESCRIPTION
    High-level orchestration logic for application installation.

    This module coordinates installation across multiple sources:
    - Sequential installation with fallback (Winget -> Chocolatey -> Store -> DirectDownload)
    - Parallel installation for PowerShell 7+
    - Rollback and deployment state management
    - Environment restriction checking

    Works in conjunction with:
    - ApplicationDetection.psm1: Detection and verification functions
    - InstallationMethods.psm1: Individual installation method implementations

.NOTES
    Author: Julien Bombled
    Version: 3.2.2

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

# === CONFIGURATION ===
$script:MaxParallelJobs = 5
$script:JobCheckInterval = 2
$script:DefaultInstallTimeoutSeconds = 1800
$script:OfficeInstallTimeoutSeconds = 2700
$script:ParallelInstallTimeoutMs = 600000

# === ROLLBACK & RESUME SYSTEM ===
$script:Win11ForgeDataDir = Join-Path $env:LOCALAPPDATA 'Win11Forge'
if (-not (Test-Path $script:Win11ForgeDataDir)) {
    New-Item -Path $script:Win11ForgeDataDir -ItemType Directory -Force | Out-Null
}
$script:RollbackStateFile = Join-Path $script:Win11ForgeDataDir 'RollbackState.json'
$script:DeploymentStateFile = Join-Path $script:Win11ForgeDataDir 'DeploymentState.json'

$script:RollbackState = @{
    SessionId = $null
    InstalledApps = @()
    StartTime = $null
}

$script:DeploymentState = @{
    SessionId = $null
    ProfileName = $null
    TotalApps = 0
    CompletedApps = @()
    FailedApps = @()
    PendingApps = @()
    StartTime = $null
    LastUpdated = $null
}

# === ROLLBACK FUNCTIONS ===

function Initialize-RollbackSession {
    <#
    .SYNOPSIS
        Initializes a new rollback session to track installed applications.
    #>
    [CmdletBinding()]
    param()

    $script:RollbackState = @{
        SessionId = [guid]::NewGuid().ToString()
        InstalledApps = @()
        StartTime = Get-Date -Format 'o'
    }

    Save-RollbackState
    Write-Status -Message "Rollback session initialized: $($script:RollbackState.SessionId)" -Level 'Verbose'
}

function Save-RollbackState {
    <#
    .SYNOPSIS
        Persists the rollback state to disk.
    #>
    [CmdletBinding()]
    param()

    try {
        $script:RollbackState | ConvertTo-Json -Depth 5 | Set-Content -Path $script:RollbackStateFile -Encoding UTF8
    } catch {
        Write-Status -Message "Could not save rollback state: $($_.Exception.Message)" -Level 'Warning'
    }
}

function Add-RollbackEntry {
    <#
    .SYNOPSIS
        Adds an installed application to the rollback registry.
    .PARAMETER AppName
        Name of the installed application.
    .PARAMETER Method
        Installation method used (Winget, Chocolatey, Store, DirectDownload).
    .PARAMETER Identifier
        Package identifier (e.g., Winget ID, Chocolatey package name).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter()]
        [string]$Identifier = $null
    )

    $entry = @{
        AppName = $AppName
        Method = $Method
        Identifier = $Identifier
        InstalledAt = Get-Date -Format 'o'
    }

    $script:RollbackState.InstalledApps += $entry
    Save-RollbackState
    Write-Status -Message "Rollback entry added: $AppName ($Method)" -Level 'Verbose'
}

function Invoke-Rollback {
    <#
    .SYNOPSIS
        Rolls back installed applications from the current session.
    .DESCRIPTION
        Uninstalls applications that were installed during the current deployment session.
        Supports Winget and Chocolatey uninstallation methods.
    .PARAMETER Force
        Skip confirmation prompts.
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

    if ($script:RollbackState.InstalledApps.Count -eq 0) {
        Write-Status -Message "No applications to roll back" -Level 'Info'
        return $result
    }

    Write-Status -Message "Rolling back $($script:RollbackState.InstalledApps.Count) application(s)..." -Level 'Info'

    foreach ($app in $script:RollbackState.InstalledApps) {
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
                    Write-Status -Message "Cannot auto-rollback $($app.AppName) (method: $($app.Method))" -Level 'Warning'
                }
            }

            if ($uninstalled) {
                Write-Status -Message "Rolled back: $($app.AppName)" -Level 'Success'
                $result.RolledBack += $app.AppName
            } else {
                Write-Status -Message "Could not roll back: $($app.AppName)" -Level 'Warning'
                $result.Failed += $app.AppName
                $result.Success = $false
            }
        } catch {
            Write-Status -Message "Rollback error for $($app.AppName): $($_.Exception.Message)" -Level 'Error'
            $result.Failed += $app.AppName
            $result.Success = $false
        }
    }

    Clear-RollbackState
    return $result
}

function Clear-RollbackState {
    <#
    .SYNOPSIS
        Clears the rollback state (call after successful deployment or rollback).
    #>
    [CmdletBinding()]
    param()

    $script:RollbackState = @{
        SessionId = $null
        InstalledApps = @()
        StartTime = $null
    }

    if (Test-Path $script:RollbackStateFile) {
        Remove-Item $script:RollbackStateFile -Force -ErrorAction SilentlyContinue
    }

    Write-Status -Message "Rollback state cleared" -Level 'Verbose'
}

function Get-RollbackState {
    <#
    .SYNOPSIS
        Returns the current rollback state.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    return $script:RollbackState
}

# === DEPLOYMENT RESUME FUNCTIONS ===

function Initialize-DeploymentSession {
    <#
    .SYNOPSIS
        Initializes a deployment session for tracking progress and enabling resume.
    .PARAMETER ProfileName
        Name of the profile being deployed.
    .PARAMETER Applications
        List of applications to be installed.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ProfileName,

        [Parameter(Mandatory)]
        [array]$Applications
    )

    $script:DeploymentState = @{
        SessionId = [guid]::NewGuid().ToString()
        ProfileName = $ProfileName
        TotalApps = $Applications.Count
        CompletedApps = @()
        FailedApps = @()
        PendingApps = @($Applications | ForEach-Object { $_.Name })
        StartTime = Get-Date -Format 'o'
        LastUpdated = Get-Date -Format 'o'
    }

    Save-DeploymentState
    Write-Status -Message "Deployment session initialized: $ProfileName ($($Applications.Count) apps)" -Level 'Info'
}

function Save-DeploymentState {
    <#
    .SYNOPSIS
        Persists deployment state to disk for crash recovery.
    #>
    [CmdletBinding()]
    param()

    try {
        $script:DeploymentState.LastUpdated = Get-Date -Format 'o'
        $script:DeploymentState | ConvertTo-Json -Depth 5 | Set-Content -Path $script:DeploymentStateFile -Encoding UTF8
    } catch {
        Write-Status -Message "Could not save deployment state: $($_.Exception.Message)" -Level 'Warning'
    }
}

function Update-DeploymentProgress {
    <#
    .SYNOPSIS
        Updates deployment progress after an application installation attempt.
    .PARAMETER AppName
        Name of the application.
    .PARAMETER Success
        Whether installation succeeded.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter(Mandatory)]
        [bool]$Success
    )

    $script:DeploymentState.PendingApps = @($script:DeploymentState.PendingApps | Where-Object { $_ -ne $AppName })

    if ($Success) {
        $script:DeploymentState.CompletedApps += $AppName
    } else {
        $script:DeploymentState.FailedApps += $AppName
    }

    Save-DeploymentState
}

function Test-ValidStateData {
    <#
    .SYNOPSIS
        Validates deployment state data for security.
    .DESCRIPTION
        Validates SessionId is GUID format, ProfileName has no path traversal,
        and app names are safe strings.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        $StateData
    )

    if ($StateData.SessionId) {
        try {
            [guid]::Parse($StateData.SessionId) | Out-Null
        } catch {
            Write-Status -Message "Invalid SessionId format in state file" -Level 'Warning'
            return $false
        }
    }

    if ($StateData.ProfileName) {
        if ($StateData.ProfileName -match '\.\.|\|[/\\]|[<>:"|?*]') {
            Write-Status -Message "Invalid ProfileName in state file (contains forbidden characters)" -Level 'Warning'
            return $false
        }
        if ($StateData.ProfileName.Length -gt 100) {
            Write-Status -Message "ProfileName too long in state file" -Level 'Warning'
            return $false
        }
    }

    if ($null -ne $StateData.TotalApps) {
        if ($StateData.TotalApps -lt 0 -or $StateData.TotalApps -gt 1000) {
            Write-Status -Message "Invalid TotalApps value in state file" -Level 'Warning'
            return $false
        }
    }

    $dangerousPattern = '[;&|`$<>]'
    foreach ($appList in @($StateData.CompletedApps, $StateData.FailedApps, $StateData.PendingApps)) {
        if ($appList) {
            foreach ($appName in $appList) {
                if ($appName -match $dangerousPattern) {
                    Write-Status -Message "Invalid app name in state file: contains shell metacharacters" -Level 'Warning'
                    return $false
                }
            }
        }
    }

    return $true
}

function Get-DeploymentState {
    <#
    .SYNOPSIS
        Returns current deployment state or loads from disk if available.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($script:DeploymentState.SessionId) {
        return $script:DeploymentState
    }

    if (Test-Path $script:DeploymentStateFile) {
        try {
            $loaded = Get-Content $script:DeploymentStateFile -Raw | ConvertFrom-Json

            if (-not (Test-ValidStateData -StateData $loaded)) {
                Write-Status -Message "State file validation failed - ignoring corrupted state" -Level 'Warning'
                Remove-Item $script:DeploymentStateFile -Force -ErrorAction SilentlyContinue
                return $null
            }

            $script:DeploymentState = @{
                SessionId = $loaded.SessionId
                ProfileName = $loaded.ProfileName
                TotalApps = [int]$loaded.TotalApps
                CompletedApps = @($loaded.CompletedApps)
                FailedApps = @($loaded.FailedApps)
                PendingApps = @($loaded.PendingApps)
                StartTime = $loaded.StartTime
                LastUpdated = $loaded.LastUpdated
            }
            return $script:DeploymentState
        } catch {
            Write-Status -Message "Could not load deployment state: $($_.Exception.Message)" -Level 'Warning'
        }
    }

    return $null
}

function Test-IncompleteDeployment {
    <#
    .SYNOPSIS
        Checks if there is an incomplete deployment that can be resumed.
    .OUTPUTS
        Boolean indicating if an incomplete deployment exists.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $state = Get-DeploymentState
    if (-not $state) { return $false }

    return ($state.PendingApps.Count -gt 0)
}

function Resume-Deployment {
    <#
    .SYNOPSIS
        Resumes an incomplete deployment from where it left off.
    .DESCRIPTION
        Returns the list of pending applications to be installed.
    .OUTPUTS
        Array of pending application names, or null if no deployment to resume.
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $state = Get-DeploymentState
    if (-not $state -or $state.PendingApps.Count -eq 0) {
        Write-Status -Message "No incomplete deployment to resume" -Level 'Info'
        return $null
    }

    Write-Status -Message "Resuming deployment: $($state.ProfileName)" -Level 'Info'
    Write-Status -Message "  Completed: $($state.CompletedApps.Count)" -Level 'Info'
    Write-Status -Message "  Pending: $($state.PendingApps.Count)" -Level 'Info'
    Write-Status -Message "  Failed: $($state.FailedApps.Count)" -Level 'Info'

    return $state.PendingApps
}

function Clear-DeploymentState {
    <#
    .SYNOPSIS
        Clears deployment state (call after successful completion).
    #>
    [CmdletBinding()]
    param()

    $script:DeploymentState = @{
        SessionId = $null
        ProfileName = $null
        TotalApps = 0
        CompletedApps = @()
        FailedApps = @()
        PendingApps = @()
        StartTime = $null
        LastUpdated = $null
    }

    if (Test-Path $script:DeploymentStateFile) {
        Remove-Item $script:DeploymentStateFile -Force -ErrorAction SilentlyContinue
    }

    Write-Status -Message "Deployment state cleared" -Level 'Verbose'
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
            $result.Message = "$($Application.Name) is restricted in $currentEnv environment"
            Write-Status -Message $result.Message -Level 'Warning'
        }
    } catch {
        Write-Status -Message "Could not verify environment restrictions: $($_.Exception.Message)" -Level 'Verbose'
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
        $result.Message = 'No installation sources available'
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
        & $writeLog "Attempting Winget: $($sources.Winget)" 'Verbose'

        $wingetOutput = @(Install-ViaWinget -PackageId $sources.Winget)
        if (& $getInstallResult $wingetOutput) {
            if ($isOfficeApp) {
                & $writeLog "Office installation initiated, waiting for Click-to-Run to complete..." 'Info'
                $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $script:OfficeInstallTimeoutSeconds
                if (-not $officeInstalled) {
                    $result.FailureReasons += "Office Click-to-Run did not complete in time"
                }
            }
            $result.Success = $true
            $result.Method = 'Winget'
            $result.Message = 'Installed via Winget'
            return $result
        } else {
            if (& $testIgnoreExitCode) {
                & $writeLog "Installation succeeded despite exit code (files verified)" 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = 'Installed via Winget (verified by file detection)'
                return $result
            }
            $result.FailureReasons += "Winget failed (ID: $($sources.Winget))"
        }
    }

    # 2. Try Chocolatey
    if ($sources.Chocolatey) {
        $result.AttemptedMethods += 'Chocolatey'
        & $writeLog "Attempting Chocolatey: $($sources.Chocolatey)" 'Verbose'

        $chocoOutput = @(Install-ViaChocolatey -PackageName $sources.Chocolatey)
        if (& $getInstallResult $chocoOutput) {
            if ($isOfficeApp) {
                & $writeLog "Office installation initiated, waiting for Click-to-Run to complete..." 'Info'
                $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $script:OfficeInstallTimeoutSeconds
                if (-not $officeInstalled) {
                    $result.FailureReasons += "Office Click-to-Run did not complete in time"
                }
            }
            $result.Success = $true
            $result.Method = 'Chocolatey'
            $result.Message = 'Installed via Chocolatey'
            return $result
        } else {
            if (& $testIgnoreExitCode) {
                & $writeLog "Installation succeeded despite exit code (files verified)" 'Success'
                $result.Success = $true
                $result.Method = 'Chocolatey'
                $result.Message = 'Installed via Chocolatey (verified by file detection)'
                return $result
            }
            $result.FailureReasons += "Chocolatey failed (Package: $($sources.Chocolatey))"
        }
    }

    # 3. Try Microsoft Store
    if ($sources.Store) {
        $result.AttemptedMethods += 'Store'
        & $writeLog "Attempting Microsoft Store: $($sources.Store)" 'Verbose'

        $storeOutput = @(Install-ViaStore -ProductId $sources.Store)
        if (& $getInstallResult $storeOutput) {
            $result.Success = $true
            $result.Method = 'Store'
            $result.Message = 'Installed via Microsoft Store'
            return $result
        } else {
            $result.FailureReasons += "Store failed (ID: $($sources.Store))"
        }
    }

    # 4. Try Direct Download
    if ($sources.DirectUrl) {
        $result.AttemptedMethods += 'DirectDownload'
        & $writeLog "Attempting direct download: $($sources.DirectUrl)" 'Verbose'

        $installParams = @{ Url = $sources.DirectUrl }

        $installArgs = if ($Application.PSObject.Properties['InstallArguments']) { $Application.InstallArguments } else { $null }
        if ($installArgs) {
            $installParams['CustomArguments'] = $installArgs
            & $writeLog "Custom arguments detected: $installArgs" 'Verbose'
        }

        if ($Application.Detection -and $Application.Detection.Path) {
            $installParams['DetectionPath'] = $Application.Detection.Path
        }

        if ($sources.PSObject.Properties['SHA256'] -and $sources.SHA256) {
            $installParams['ExpectedSHA256'] = $sources.SHA256
        }

        $downloadOutput = @(Install-ViaDirectDownload @installParams)
        if (& $getInstallResult $downloadOutput) {
            $result.Success = $true
            $result.Method = 'DirectDownload'
            $result.Message = 'Installed via direct download'
            return $result
        } else {
            $result.FailureReasons += "DirectDownload failed"
        }
    }

    $result.Message = if ($result.AttemptedMethods.Count -gt 0) {
        "All methods failed: $($result.FailureReasons -join '; ')"
    } else {
        'No valid installation sources configured'
    }

    & $writeLog "Installation failed: $($result.Message)" 'Warning'
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
            Write-Status -Message "Attempting Winget upgrade: $($sources.Winget)" -Level 'Info'

            $arguments = @(
                'upgrade',
                '--id', $sources.Winget,
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            $process = Start-ProcessWithTimeout -FilePath 'winget' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                Write-Status -Message "Successfully upgraded: $($Application.Name)" -Level 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = 'Upgraded successfully'
                return $result
            } elseif ($process.ExitCode -eq -1978335189) {
                Write-Status -Message "No update available via Winget for: $($Application.Name)" -Level 'Verbose'
            } else {
                Write-Status -Message "Winget upgrade returned exit code: $($process.ExitCode)" -Level 'Verbose'
            }
        } catch {
            Write-Status -Message "Winget upgrade error: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    if ($sources.Chocolatey -and (Test-CommandExists -Name 'choco')) {
        try {
            Write-Status -Message "Attempting Chocolatey upgrade: $($sources.Chocolatey)" -Level 'Info'

            $arguments = @(
                'upgrade',
                $sources.Chocolatey,
                '-y',
                '--no-progress'
            )

            $process = Start-ProcessWithTimeout -FilePath 'choco' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds

            if ($process.ExitCode -eq 0) {
                Write-Status -Message "Successfully upgraded via Chocolatey: $($Application.Name)" -Level 'Success'
                $result.Success = $true
                $result.Method = 'Chocolatey'
                $result.Message = 'Upgraded successfully'
                return $result
            } else {
                Write-Status -Message "Chocolatey upgrade returned exit code: $($process.ExitCode)" -Level 'Verbose'
            }
        } catch {
            Write-Status -Message "Chocolatey upgrade error: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    $result.Message = 'No update available or upgrade not supported'
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
        $result.Message = "Not compatible with $($envCheck.Environment) environment"
        return $result
    }

    # 2. Check if already installed
    if (-not $Force) {
        $isInstalled = Test-ApplicationInstalled -Application $Application
        if ($isInstalled) {
            if ($ForceUpdate) {
                Write-Output "[INFO] Checking for updates: $($Application.Name)"
                Write-Status -Message "Checking for updates: $($Application.Name)" -Level 'Info'
                $upgradeResult = Invoke-ApplicationUpgrade -Application $Application
                if ($upgradeResult.Success) {
                    return $upgradeResult
                }
                Write-Output "[INFO] No update available or upgrade not supported for: $($Application.Name)"
                Write-Status -Message "No update available or upgrade not supported for: $($Application.Name)" -Level 'Info'
                $result.AlreadyInstalled = $true
                $result.Success = $true
                $result.Message = 'Already installed (no update available)'
                return $result
            }

            Write-Output "[SUCCESS] Already installed: $($Application.Name)"
            Write-Status -Message "Already installed: $($Application.Name)" -Level 'Success'
            $result.AlreadyInstalled = $true
            $result.Success = $true
            $result.Message = 'Already installed'
            return $result
        }
    }

    Write-Output "[INFO] Installing: $($Application.Name)"
    Write-Status -Message "Installing: $($Application.Name)" -Level 'Info'

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
        } catch { }
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
                } catch { }
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

    $installResults = $appsToInstall | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $app = $_
        $force = $using:forceInstall
        $repRoot = $using:repoRoot
        $parallelLogDir = $using:parallelLogsDir
        $ts = $using:timestamp
        $validateUrl = $using:validateUrlFunction
        $detectAppFunc = $using:detectAppFunction
        $installTimeoutMs = $using:parallelTimeoutMs

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
                    $result.Message = 'Already installed'
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
                        $result.Message = 'Installed via WindowsFeature'
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
                            $result.Message = 'Installed via WindowsCapability'
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
                        Write-ParallelLog "Process timed out after 600 seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Winget installation failed (timeout)" 'Warning'
                        break
                    } elseif ($process.ExitCode -eq 0) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Installed successfully via Winget$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Winget'
                        $result.Message = "Installed via Winget$retryMsg"
                        return $result
                    } elseif ($process.ExitCode -eq -1978334974) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Already installed (Winget)$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Winget'
                        $result.AlreadyInstalled = $true
                        $result.Message = "Already installed (Winget)$retryMsg"
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
                        Write-ParallelLog "Process timed out after 600 seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Chocolatey installation failed (timeout)" 'Warning'
                        break
                    } elseif ($process.ExitCode -eq 0) {
                        $retryMsg = if ($attempt -gt 1) { " (attempt $attempt)" } else { "" }
                        Write-ParallelLog "Installed successfully via Chocolatey$retryMsg" 'Success'
                        $result.Success = $true
                        $result.Method = 'Chocolatey'
                        $result.Message = "Installed via Chocolatey$retryMsg"
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
                        Write-ParallelLog "Process timed out after 600 seconds - terminating" 'Warning'
                        $process.Kill()
                        Write-ParallelLog "Microsoft Store installation failed (timeout)" 'Warning'
                    } elseif ($process.ExitCode -eq 0) {
                        Write-ParallelLog "Installed successfully via Microsoft Store" 'Success'
                        $result.Success = $true
                        $result.Method = 'Store'
                        $result.Message = 'Installed via Microsoft Store'
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
                    $result.Message = 'Invalid DirectUrl'
                    return $result
                }

                Write-ParallelLog "Attempting direct download installation: $($sources.DirectUrl)" 'Info'

                try {
                    $filename = [System.IO.Path]::GetFileName($sources.DirectUrl)
                    if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.[a-z]{3,4}$') {
                        $filename = "installer_$([guid]::NewGuid().ToString('N')).exe"
                    }

                    $tempDir = Join-Path $env:TEMP "Win11Forge_$([guid]::NewGuid().ToString('N'))"
                    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
                    $tempFile = Join-Path $tempDir $filename

                    Write-ParallelLog "Downloading to: $tempFile" 'Verbose'

                    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

                    $downloadSuccess = $false

                    # Method 1: WebClient
                    try {
                        $webClient = New-Object System.Net.WebClient
                        $webClient.Headers.Add("User-Agent", "Win11Forge/3.2.0 (Windows NT; PowerShell)")
                        $downloadTask = $webClient.DownloadFileTaskAsync($sources.DirectUrl, $tempFile)
                        $downloadTask.Wait()
                        $webClient.Dispose()
                        if ((Test-Path -Path $tempFile) -and (Get-Item -Path $tempFile).Length -gt 0) { $downloadSuccess = $true }
                    } catch {
                        Write-ParallelLog "WebClient failed: $($_.Exception.Message)" 'Verbose'
                        if ($webClient) { $webClient.Dispose() }
                        if (Test-Path -Path $tempFile) { Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue }
                    }

                    # Method 2: Invoke-WebRequest
                    if (-not $downloadSuccess) {
                        if (Test-Path -Path $tempFile) { Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue }
                        try {
                            $ProgressPreference = 'SilentlyContinue'
                            Invoke-WebRequest -Uri $sources.DirectUrl -OutFile $tempFile -UseBasicParsing -TimeoutSec 600 -ErrorAction Stop
                            if ((Test-Path -Path $tempFile) -and (Get-Item -Path $tempFile).Length -gt 0) { $downloadSuccess = $true }
                        } catch {
                            Write-ParallelLog "Invoke-WebRequest failed: $($_.Exception.Message)" 'Verbose'
                        }
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
                        throw "Download failed - all methods exhausted"
                    }
                    Write-ParallelLog "Download completed" 'Info'

                    # SHA256 checksum validation
                    if ($sources.SHA256) {
                        Write-ParallelLog "Validating SHA256 checksum..." 'Info'
                        $fileHash = (Get-FileHash -Path $tempFile -Algorithm SHA256).Hash
                        if ($fileHash -ne $sources.SHA256) {
                            Write-ParallelLog "Checksum FAILED! Expected: $($sources.SHA256), Got: $fileHash" 'Error'
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                            $result.Message = 'SHA256 checksum validation failed'
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
                            if ($app.PSObject.Properties['InstallArguments']) {
                                $msiArgs += $app.InstallArguments -split ' '
                            }
                            $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $msiArgs -Wait -NoNewWindow -PassThru
                            $processExitCode = $process.ExitCode
                        }
                        'zip' {
                            Write-ParallelLog "Extracting ZIP archive" 'Info'
                            $extractPath = Join-Path $tempDir "extracted"
                            Expand-Archive -Path $tempFile -DestinationPath $extractPath -Force

                            $setupExe = Get-ChildItem -Path $extractPath -Filter *.exe -Recurse |
                                Where-Object { $_.Name -match 'setup|install' } |
                                Select-Object -First 1

                            if ($setupExe) {
                                $zipArgs = if ($app.PSObject.Properties['InstallArguments']) { $app.InstallArguments } else { '/S' }
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
                            $exeArgs = if ($app.PSObject.Properties['InstallArguments']) { $app.InstallArguments } else { '/S' }
                            $process = Start-Process -FilePath $tempFile -ArgumentList $exeArgs -Wait -NoNewWindow -PassThru
                            $processExitCode = $process.ExitCode
                        }
                    }

                    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

                    if ($processExitCode -eq 0) {
                        Write-ParallelLog "Installed successfully via direct download" 'Success'
                        $result.Success = $true
                        $result.Method = 'DirectDownload'
                        $result.Message = 'Installed via direct download'
                        return $result
                    } else {
                        Write-ParallelLog "Direct download installation failed (exit code: $processExitCode)" 'Warning'
                    }
                } catch {
                    Write-ParallelException -ErrorRecord $_ -Context 'DirectDownload'
                }
            }

            Write-ParallelLog "All installation methods failed" 'Error'
            $result.Message = 'All installation methods failed'

        } catch {
            Write-ParallelException -ErrorRecord $_ -Context 'MainInstallLoop'
            $result.Message = "Error: $($_.Exception.Message)"
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
Export-ModuleMember -Function @(
    # Rollback functions
    'Initialize-RollbackSession',
    'Save-RollbackState',
    'Add-RollbackEntry',
    'Invoke-Rollback',
    'Clear-RollbackState',
    'Get-RollbackState',

    # Deployment state functions
    'Initialize-DeploymentSession',
    'Save-DeploymentState',
    'Update-DeploymentProgress',
    'Test-ValidStateData',
    'Get-DeploymentState',
    'Test-IncompleteDeployment',
    'Resume-Deployment',
    'Clear-DeploymentState',

    # Environment
    'Test-EnvironmentRestriction',

    # Orchestration
    'Invoke-InstallationMethodSequence',
    'Get-ApplicationSources',
    'Invoke-ApplicationUpgrade',
    'Install-Application',
    'Install-ApplicationsParallel'
)
