<#
.SYNOPSIS
    Win11Forge - State Manager v3.7.2

.DESCRIPTION
    Centralized state management for Win11Forge deployments:
    - Rollback state tracking and persistence
    - Deployment state tracking and persistence
    - State validation and security
    - State recovery and cleanup

    Extracted from InstallationOrchestrator.psm1 for better separation of concerns.

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

# === TYPE DEFINITIONS ===
# PowerShell classes for type-safe state management

enum InstallationMethod {
    Winget
    Chocolatey
    Store
    DirectDownload
    WindowsFeature
    WindowsCapability
}

class RollbackEntry {
    [string]$AppName
    [InstallationMethod]$Method
    [string]$Identifier
    [datetime]$InstalledAt

    RollbackEntry([string]$appName, [InstallationMethod]$method, [string]$identifier) {
        if ([string]::IsNullOrWhiteSpace($appName)) {
            throw [System.ArgumentException]::new('AppName cannot be null or empty')
        }
        if ($identifier -match '[;&|`$<>]') {
            throw [System.ArgumentException]::new('Identifier contains invalid characters')
        }
        $this.AppName = $appName
        $this.Method = $method
        $this.Identifier = $identifier
        $this.InstalledAt = Get-Date
    }

    [hashtable] ToHashtable() {
        return @{
            AppName = $this.AppName
            Method = $this.Method.ToString()
            Identifier = $this.Identifier
            InstalledAt = $this.InstalledAt.ToString('o')
        }
    }

    static [RollbackEntry] FromHashtable([hashtable]$data) {
        $parsedMethod = [InstallationMethod]::Winget
        if ($data.Method) {
            $parsedMethod = [Enum]::Parse([InstallationMethod], $data.Method)
        }
        $entry = [RollbackEntry]::new($data.AppName, $parsedMethod, $data.Identifier)
        if ($data.InstalledAt) {
            $entry.InstalledAt = [datetime]::Parse($data.InstalledAt)
        }
        return $entry
    }
}

class RollbackStateData {
    [string]$SessionId
    [RollbackEntry[]]$InstalledApps
    [datetime]$StartTime

    RollbackStateData() {
        $this.InstalledApps = @()
    }

    RollbackStateData([string]$sessionId) {
        if (-not ($sessionId -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')) {
            throw [System.ArgumentException]::new('SessionId must be a valid GUID')
        }
        $this.SessionId = $sessionId
        $this.InstalledApps = @()
        $this.StartTime = Get-Date
    }

    [void] AddEntry([RollbackEntry]$entry) {
        $this.InstalledApps = @($this.InstalledApps) + $entry
    }

    [hashtable] ToHashtable() {
        return @{
            SessionId = $this.SessionId
            InstalledApps = @($this.InstalledApps | ForEach-Object { $_.ToHashtable() })
            StartTime = $this.StartTime.ToString('o')
        }
    }
}

class DeploymentStateData {
    [string]$SessionId
    [string]$ProfileName
    [int]$TotalApps
    [string[]]$CompletedApps
    [string[]]$FailedApps
    [string[]]$PendingApps
    [datetime]$StartTime
    [datetime]$LastUpdated

    DeploymentStateData() {
        $this.CompletedApps = @()
        $this.FailedApps = @()
        $this.PendingApps = @()
    }

    DeploymentStateData([string]$sessionId, [string]$profileName, [string[]]$applications) {
        if (-not ($sessionId -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')) {
            throw [System.ArgumentException]::new('SessionId must be a valid GUID')
        }
        if ([string]::IsNullOrWhiteSpace($profileName) -or $profileName.Length -gt 100) {
            throw [System.ArgumentException]::new('ProfileName must be 1-100 characters')
        }
        if ($applications.Count -lt 0 -or $applications.Count -gt 1000) {
            throw [System.ArgumentException]::new('Applications count must be 0-1000')
        }
        $this.SessionId = $sessionId
        $this.ProfileName = $profileName
        $this.TotalApps = $applications.Count
        $this.CompletedApps = @()
        $this.FailedApps = @()
        $this.PendingApps = @($applications)
        $this.StartTime = Get-Date
        $this.LastUpdated = Get-Date
    }

    [void] MarkCompleted([string]$appName) {
        $this.PendingApps = @($this.PendingApps | Where-Object { $_ -ne $appName })
        $this.CompletedApps = @($this.CompletedApps) + $appName
        $this.LastUpdated = Get-Date
    }

    [void] MarkFailed([string]$appName) {
        $this.PendingApps = @($this.PendingApps | Where-Object { $_ -ne $appName })
        $this.FailedApps = @($this.FailedApps) + $appName
        $this.LastUpdated = Get-Date
    }

    [int] GetProgressPercent() {
        if ($this.TotalApps -eq 0) { return 0 }
        $completed = $this.CompletedApps.Count + $this.FailedApps.Count
        return [math]::Round(($completed / $this.TotalApps) * 100)
    }

    [hashtable] ToHashtable() {
        return @{
            SessionId = $this.SessionId
            ProfileName = $this.ProfileName
            TotalApps = $this.TotalApps
            CompletedApps = @($this.CompletedApps)
            FailedApps = @($this.FailedApps)
            PendingApps = @($this.PendingApps)
            StartTime = $this.StartTime.ToString('o')
            LastUpdated = $this.LastUpdated.ToString('o')
        }
    }
}

class ResumableDeploymentInfo {
    [string]$ProfileName
    [int]$CompletedCount
    [int]$FailedCount
    [int]$PendingCount
    [int]$TotalCount
    [datetime]$StartTime
    [datetime]$LastUpdated
    [string[]]$PendingApps

    ResumableDeploymentInfo([DeploymentStateData]$state) {
        $this.ProfileName = $state.ProfileName
        $this.CompletedCount = $state.CompletedApps.Count
        $this.FailedCount = $state.FailedApps.Count
        $this.PendingCount = $state.PendingApps.Count
        $this.TotalCount = $state.TotalApps
        $this.StartTime = $state.StartTime
        $this.LastUpdated = $state.LastUpdated
        $this.PendingApps = @($state.PendingApps)
    }

    [hashtable] ToHashtable() {
        return @{
            ProfileName = $this.ProfileName
            CompletedCount = $this.CompletedCount
            FailedCount = $this.FailedCount
            PendingCount = $this.PendingCount
            TotalCount = $this.TotalCount
            StartTime = $this.StartTime.ToString('o')
            LastUpdated = $this.LastUpdated.ToString('o')
            PendingApps = $this.PendingApps
        }
    }
}

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

# Import DirectoryConstants for path management
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-Win11ForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# === DATA DIRECTORY ===
$script:Win11ForgeDataDir = Get-Win11ForgeDirectory -DirectoryType 'Data'
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

    # Convert hashtable to PSCustomObject for consistent property access
    if ($StateData -is [hashtable]) {
        $StateData = [PSCustomObject]$StateData
    }

    # Use PSObject.Properties for safe access (works with partial data under StrictMode)
    $sessionIdProp = $StateData.PSObject.Properties['SessionId']
    $sessionId = if ($sessionIdProp) { $sessionIdProp.Value } else { $null }
    if ($sessionId) {
        try {
            [guid]::Parse($sessionId) | Out-Null
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_session_id') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate ProfileName (no path traversal, no special chars)
    $profileNameProp = $StateData.PSObject.Properties['ProfileName']
    $profileName = if ($profileNameProp) { $profileNameProp.Value } else { $null }
    if ($profileName) {
        if ($profileName -match '\.\.|[/\\|<>:"|?*]') {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_profile_name') -Level 'Warning' -Category 'State'
            return $false
        }
        if ($profileName.Length -gt 100) {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.profile_name_too_long') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate TotalApps range
    $totalAppsProp = $StateData.PSObject.Properties['TotalApps']
    $totalApps = if ($totalAppsProp) { $totalAppsProp.Value } else { $null }
    if ($null -ne $totalApps) {
        if ($totalApps -lt 0 -or $totalApps -gt 1000) {
            Write-Status -Message (Get-LocalizedString -Key 'state.validation.invalid_total_apps') -Level 'Warning' -Category 'State'
            return $false
        }
    }

    # Validate app names (no shell metacharacters)
    $dangerousPattern = '[;&|`$<>]'
    $completedAppsProp = $StateData.PSObject.Properties['CompletedApps']
    $completedApps = if ($completedAppsProp) { $completedAppsProp.Value } else { $null }
    $failedAppsProp = $StateData.PSObject.Properties['FailedApps']
    $failedApps = if ($failedAppsProp) { $failedAppsProp.Value } else { $null }
    $pendingAppsProp = $StateData.PSObject.Properties['PendingApps']
    $pendingApps = if ($pendingAppsProp) { $pendingAppsProp.Value } else { $null }

    foreach ($appList in @($completedApps, $failedApps, $pendingApps)) {
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
    .DESCRIPTION
        Checks a rollback entry for safe values by verifying that the application name, installation
        method, and identifier do not contain shell metacharacters or exceed length limits, preventing
        injection attacks during rollback operations.
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
    .DESCRIPTION
        Creates a new rollback session with a unique GUID, an empty installed-apps list, and a
        start timestamp. The session state is immediately persisted to disk so that rollback
        entries can be recovered after an unexpected interruption.
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
    .DESCRIPTION
        Serializes the current rollback state hashtable to JSON and writes it to the rollback state
        file. This enables crash recovery by allowing the rollback session to be restored from disk.
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
    .DESCRIPTION
        Records an application's installation details (name, method, identifier, and timestamp)
        in the current rollback session after validating the entry for security. The updated state
        is immediately persisted to disk.
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
    .DESCRIPTION
        Returns a cloned copy of the current in-memory rollback state hashtable, including the
        session ID, start time, and the list of installed applications tracked for potential rollback.
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
    .DESCRIPTION
        Retrieves the array of installed-application entries from the current rollback session.
        Each entry contains the application name, installation method, identifier, and timestamp.
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
    .DESCRIPTION
        Resets the in-memory rollback state to its initial empty values and deletes the persisted
        rollback state file from disk. Call this after a successful deployment completes or after
        a rollback has been executed.
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
    .DESCRIPTION
        Attempts to load a previously persisted rollback state from the state file on disk. This
        enables recovery of an interrupted rollback session, allowing the framework to resume
        tracking or uninstalling applications from where it left off.
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
    .DESCRIPTION
        Creates a new deployment session with a unique GUID, populating it with the profile name,
        application list, and timestamps. The state is immediately persisted to disk so the
        deployment can be resumed if the process is interrupted.
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
    .DESCRIPTION
        Serializes the current deployment state (including progress, completed and failed apps) to
        JSON and writes it to the deployment state file. Updates the LastUpdated timestamp before
        each write to track when the state was last persisted.
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
    .DESCRIPTION
        Moves an application from the pending list to either the completed or failed list based on
        the installation outcome, then persists the updated deployment state to disk.
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
    .DESCRIPTION
        Returns the in-memory deployment state if a session is active. Otherwise, attempts to load
        a previously persisted state from disk for crash recovery. Returns null if no state exists.
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
    .DESCRIPTION
        Calculates the deployment completion percentage by dividing the number of completed and
        failed applications by the total application count, returning a value between 0 and 100.
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
    .DESCRIPTION
        Resets the in-memory deployment state to its initial empty values and deletes the persisted
        state file from disk. Call this after a deployment completes successfully to prevent stale
        state from interfering with future deployments.
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
    .DESCRIPTION
        Queries the current deployment state and returns true if a session exists with one or more
        applications still in the pending list, indicating the deployment was interrupted and can
        be resumed.
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
    .DESCRIPTION
        Retrieves the saved deployment state and returns a summary hashtable containing the profile
        name, remaining pending applications, and progress statistics. Returns null if there is no
        resumable deployment.
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
