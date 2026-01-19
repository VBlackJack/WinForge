<#
.SYNOPSIS
    Win11Forge - State Manager Module v3.3.0

.DESCRIPTION
    Centralized state management for Win11Forge deployments:
    - Rollback state tracking and persistence
    - Deployment state tracking and persistence
    - State validation and security
    - State recovery and cleanup

    Extracted from InstallationOrchestrator.psm1 for better separation of concerns.

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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

# === DATA DIRECTORY ===
$script:Win11ForgeDataDir = Join-Path $env:LOCALAPPDATA 'Win11Forge'
if (-not (Test-Path $script:Win11ForgeDataDir)) {
    New-Item -Path $script:Win11ForgeDataDir -ItemType Directory -Force | Out-Null
}

# === STATE FILE PATHS ===
$script:RollbackStateFile = Join-Path $script:Win11ForgeDataDir 'RollbackState.json'
$script:DeploymentStateFile = Join-Path $script:Win11ForgeDataDir 'DeploymentState.json'

# === STATE OBJECTS ===
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

# === VALIDATION FUNCTIONS ===

function Test-ValidStateData {
    <#
    .SYNOPSIS
        Validates deployment state data for security.
    .DESCRIPTION
        Validates SessionId is GUID format, ProfileName has no path traversal,
        and app names are safe strings.
    .PARAMETER StateData
        The state data hashtable to validate.
    .OUTPUTS
        Boolean indicating if state data is valid.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        $StateData
    )

    # Validate SessionId format
    if ($StateData.SessionId) {
        try {
            [guid]::Parse($StateData.SessionId) | Out-Null
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_session_id') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate ProfileName (no path traversal, no special chars)
    if ($StateData.ProfileName) {
        if ($StateData.ProfileName -match '\.\.|\|[/\\]|[<>:"|?*]') {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_profile_name') -Level 'Warning' -Category 'State'
            return $false
        }
        if ($StateData.ProfileName.Length -gt 100) {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.profile_name_too_long') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate TotalApps range
    if ($null -ne $StateData.TotalApps) {
        if ($StateData.TotalApps -lt 0 -or $StateData.TotalApps -gt 1000) {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_total_apps') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate app names (no shell metacharacters)
    $dangerousPattern = '[;&|`$<>]'
    foreach ($appList in @($StateData.CompletedApps, $StateData.FailedApps, $StateData.PendingApps)) {
        if ($appList) {
            foreach ($appName in $appList) {
                if ($appName -match $dangerousPattern) {
                    Write-Status -Message (Get-LocalizedString -Key 'state.validation.dangerous_app_name') -Level 'Warning' -Category 'State'
                    return $false
                }
            }
        }
    }

    return $true
}

function Test-ValidRollbackEntry {
    <#
    .SYNOPSIS
        Validates a rollback entry for security.
    .PARAMETER Entry
        The rollback entry to validate.
    .OUTPUTS
        Boolean indicating if entry is valid.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        $Entry
    )

    # Validate AppName
    if (-not $Entry.AppName -or $Entry.AppName.Length -gt 200) {
        return $false
    }

    # Validate Method
    $validMethods = @('Winget', 'Chocolatey', 'Store', 'DirectDownload', 'WindowsFeature', 'WindowsCapability')
    if ($Entry.Method -and $Entry.Method -notin $validMethods) {
        return $false
    }

    # Validate Identifier (no shell metacharacters)
    if ($Entry.Identifier -and $Entry.Identifier -match '[;&|`$<>]') {
        return $false
    }

    return $true
}

# === ROLLBACK STATE FUNCTIONS ===

function Initialize-RollbackSession {
    <#
    .SYNOPSIS
        Initializes a new rollback session to track installed applications.
    .OUTPUTS
        The session ID of the new rollback session.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $script:RollbackState = @{
        SessionId = [guid]::NewGuid().ToString()
        InstalledApps = @()
        StartTime = Get-Date -Format 'o'
    }

    Save-RollbackState
    Write-Status -Message (Get-LocalizedString -Key 'state.rollback.session_initialized' -Parameters @{ SessionId = $script:RollbackState.SessionId }) -Level 'Verbose' -Category 'State'

    return $script:RollbackState.SessionId
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
        Write-Status -Message (Get-LocalizedString -Key 'state.rollback.save_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning' -Category 'State'
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
        [ValidateNotNullOrEmpty()]
        [ValidateLength(1, 200)]
        [string]$AppName,

        [Parameter(Mandatory)]
        [ValidateSet('Winget', 'Chocolatey', 'Store', 'DirectDownload', 'WindowsFeature', 'WindowsCapability')]
        [string]$Method,

        [Parameter()]
        [ValidatePattern('^[^;&|`$<>]*$')]
        [string]$Identifier = $null
    )

    $entry = @{
        AppName = $AppName
        Method = $Method
        Identifier = $Identifier
        InstalledAt = Get-Date -Format 'o'
    }

    # Validate entry
    if (-not (Test-ValidRollbackEntry -Entry $entry)) {
        Write-Status -Message (Get-LocalizedString -Key 'state.rollback.invalid_entry') -Level 'Warning' -Category 'State'
        return
    }

    $script:RollbackState.InstalledApps += $entry
    Save-RollbackState
    Write-Status -Message (Get-LocalizedString -Key 'state.rollback.entry_added' -Parameters @{ AppName = $AppName; Method = $Method }) -Level 'Verbose' -Category 'State'
}

function Get-RollbackState {
    <#
    .SYNOPSIS
        Returns the current rollback state.
    .OUTPUTS
        Hashtable containing the current rollback state.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    return $script:RollbackState.Clone()
}

function Get-RollbackEntries {
    <#
    .SYNOPSIS
        Returns all rollback entries for the current session.
    .OUTPUTS
        Array of rollback entries.
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    return @($script:RollbackState.InstalledApps)
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

    Write-Status -Message (Get-LocalizedString -Key 'state.rollback.cleared') -Level 'Verbose' -Category 'State'
}

function Restore-RollbackState {
    <#
    .SYNOPSIS
        Restores rollback state from disk if available.
    .OUTPUTS
        Boolean indicating if state was restored.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if (Test-Path $script:RollbackStateFile) {
        try {
            $json = Get-Content $script:RollbackStateFile -Raw | ConvertFrom-Json
            $stateData = @{
                SessionId = $json.SessionId
                InstalledApps = @($json.InstalledApps)
                StartTime = $json.StartTime
            }

            if (Test-ValidStateData -StateData $stateData) {
                $script:RollbackState = $stateData
                Write-Status -Message (Get-LocalizedString -Key 'state.rollback.restored') -Level 'Verbose' -Category 'State'
                return $true
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'state.rollback.restore_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning' -Category 'State'
        }
    }

    return $false
}

# === DEPLOYMENT STATE FUNCTIONS ===

function Initialize-DeploymentSession {
    <#
    .SYNOPSIS
        Initializes a deployment session for tracking progress and enabling resume.
    .PARAMETER ProfileName
        Name of the profile being deployed.
    .PARAMETER Applications
        List of applications to be installed.
    .OUTPUTS
        The session ID of the new deployment session.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [ValidateLength(1, 100)]
        [ValidatePattern('^[^\.\.\\/<>:"|?*]+$')]
        [string]$ProfileName,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
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
    Write-Status -Message (Get-LocalizedString -Key 'state.deployment.session_initialized' -Parameters @{ ProfileName = $ProfileName; Count = $Applications.Count }) -Level 'Info' -Category 'State'

    return $script:DeploymentState.SessionId
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
        Write-Status -Message (Get-LocalizedString -Key 'state.deployment.save_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning' -Category 'State'
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
        [ValidateNotNullOrEmpty()]
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

function Get-DeploymentState {
    <#
    .SYNOPSIS
        Returns current deployment state or loads from disk if available.
    .OUTPUTS
        Hashtable containing the current deployment state.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($script:DeploymentState.SessionId) {
        return $script:DeploymentState.Clone()
    }

    # Try to load from disk
    if (Test-Path $script:DeploymentStateFile) {
        try {
            $json = Get-Content $script:DeploymentStateFile -Raw | ConvertFrom-Json
            $stateData = @{
                SessionId = $json.SessionId
                ProfileName = $json.ProfileName
                TotalApps = $json.TotalApps
                CompletedApps = @($json.CompletedApps)
                FailedApps = @($json.FailedApps)
                PendingApps = @($json.PendingApps)
                StartTime = $json.StartTime
                LastUpdated = $json.LastUpdated
            }

            if (Test-ValidStateData -StateData $stateData) {
                return $stateData
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'state.deployment.load_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning' -Category 'State'
        }
    }

    return $null
}

function Get-DeploymentProgress {
    <#
    .SYNOPSIS
        Returns deployment progress as a percentage.
    .OUTPUTS
        Progress percentage (0-100).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    if ($script:DeploymentState.TotalApps -eq 0) {
        return 0
    }

    $completed = $script:DeploymentState.CompletedApps.Count + $script:DeploymentState.FailedApps.Count
    return [math]::Round(($completed / $script:DeploymentState.TotalApps) * 100)
}

function Clear-DeploymentState {
    <#
    .SYNOPSIS
        Clears the deployment state (call after successful deployment).
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

    Write-Status -Message (Get-LocalizedString -Key 'state.deployment.cleared') -Level 'Verbose' -Category 'State'
}

function Test-DeploymentInProgress {
    <#
    .SYNOPSIS
        Checks if there is an incomplete deployment that can be resumed.
    .OUTPUTS
        Boolean indicating if there is a resumable deployment.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $state = Get-DeploymentState
    return ($null -ne $state -and $state.PendingApps.Count -gt 0)
}

function Get-ResumableDeployment {
    <#
    .SYNOPSIS
        Returns information about a resumable deployment.
    .OUTPUTS
        Hashtable with deployment info or null if none.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $state = Get-DeploymentState

    if ($null -eq $state -or $state.PendingApps.Count -eq 0) {
        return $null
    }

    return @{
        ProfileName = $state.ProfileName
        CompletedCount = $state.CompletedApps.Count
        FailedCount = $state.FailedApps.Count
        PendingCount = $state.PendingApps.Count
        TotalCount = $state.TotalApps
        StartTime = $state.StartTime
        LastUpdated = $state.LastUpdated
        PendingApps = $state.PendingApps
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    # Validation
    'Test-ValidStateData',
    'Test-ValidRollbackEntry',
    # Rollback State
    'Initialize-RollbackSession',
    'Save-RollbackState',
    'Add-RollbackEntry',
    'Get-RollbackState',
    'Get-RollbackEntries',
    'Clear-RollbackState',
    'Restore-RollbackState',
    # Deployment State
    'Initialize-DeploymentSession',
    'Save-DeploymentState',
    'Update-DeploymentProgress',
    'Get-DeploymentState',
    'Get-DeploymentProgress',
    'Clear-DeploymentState',
    'Test-DeploymentInProgress',
    'Get-ResumableDeployment'
)
