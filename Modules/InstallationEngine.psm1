<#
.SYNOPSIS
    Win11Forge - Installation Engine Module v3.1.5 (Modular Architecture)

.DESCRIPTION
    Core installation engine orchestration with multi-source support and parallel execution.

    This module has been split into submodules for maintainability:
    - ApplicationDetection.psm1: Detection and verification functions
    - InstallationMethods.psm1: Individual installation method implementations

    Features:
    - Winget (primary) with retry logic (sequential AND parallel)
    - Chocolatey (fallback) with retry logic (sequential AND parallel)
    - Microsoft Store (UWP apps)
    - Direct download + silent install with SHA256 validation (sequential AND parallel)
    - Application detection with special cases (PowerToys, Quick Assist)
    - Windows Features/Capabilities
    - Parallel installation (up to 5 apps simultaneously)
    - Rollback and deployment state management
    - Environment restriction checking

.NOTES
    Author: Julien Bombled
    Version: 3.1.5

    Changelog v3.1.5 (Modular Architecture):
    - ARCHITECTURE: Split into 3 modules for maintainability (InstallationEngine, ApplicationDetection, InstallationMethods)
    - ARCHITECTURE: ApplicationDetection.psm1 contains all detection and verification functions
    - ARCHITECTURE: InstallationMethods.psm1 contains individual install method implementations
    - ARCHITECTURE: InstallationEngine.psm1 retains orchestration logic and state management

    Changelog v3.1.4 (Critical Security Fixes):
    - SECURITY: CRITICAL - Replaced cmd /c with Start-Process argument arrays in Invoke-Rollback
    - SECURITY: Added Test-ValidStateData for deployment state file validation
    - SECURITY: Added path traversal protection to parallel Test-AppInstalledParallel
    - SECURITY: Added executable whitelist for Command detection method
    - SECURITY: Added ValidateAppId in C# PowerShellBridge
    - CONFIG: Standardized parallel timeout to use $script:ParallelInstallTimeoutMs

    Changelog v3.1.3 (Security Hardening Update):
    - SECURITY: Added path traversal protection in Expand-DetectionPath
    - SECURITY: URL validation now blocks non-whitelisted domains by default
    - SECURITY: Trusted domains loaded from Config/download-sources.json
    - SECURITY: Full GUID used for temp directories (32 chars vs 8 chars)
    - SECURITY: Added -AllowUntrusted parameter for explicit override

    Changelog v3.0.0 (Parallel Reliability Update):
    - RELIABILITY: Added retry logic to parallel Winget (3 attempts with exponential backoff)
    - RELIABILITY: Added retry logic to parallel Chocolatey (3 attempts with exponential backoff)
    - SECURITY: Added SHA256 checksum validation for parallel DirectUrl downloads
    - REFACTORING: Created Invoke-InstallationMethodSequence helper for sequential installs
    - REFACTORING: Created Invoke-CustomInstallMethod helper for WindowsFeature/Capability
    - REFACTORING: Reduced Install-Application from 189 to ~70 lines
    - QUALITY: 458 Pester tests passing (100% pass rate)

    Changelog v3.0.0 (Reliability & Quality Update):
    - RELIABILITY: Added retry logic to Winget (3 attempts with exponential backoff)
    - RELIABILITY: Added retry logic to Chocolatey (3 attempts with exponential backoff)
    - SECURITY: Added SHA256 checksum validation for DirectUrl downloads
    - SECURITY: Invalid checksums trigger file deletion and download failure
    - QUALITY: Added comprehensive Pester test suite (145+ tests, ~50% coverage)
    - QUALITY: Added PSScriptAnalyzer integration with custom ruleset
    - MAINTAINABILITY: Identified long functions for v3.0.0 refactoring
    - USER AGENT: Updated to Win11Forge/3.0.0

    Changelog v2.4.0 (Security & Performance Update):
    - SECURITY: Replaced Invoke-Expression with secure Start-Process (eliminates command injection vulnerability)
    - SECURITY: Added URL validation for DirectUrl downloads with domain whitelisting
    - PERFORMANCE: Implemented streaming downloads in sequential mode (memory-efficient, no longer loads files in RAM)
    - PERFORMANCE: Harmonized streaming downloads in parallel mode (prevents RAM saturation on large files)
    - STABILITY: Added timeout protection to all sequential installation methods (default: 10 minutes)
    - STABILITY: Added timeout protection to all parallel installation methods (Winget/Chocolatey/Store)
    - STABILITY: Fixed race condition in parallel logs directory creation with retry logic
    - MAINTENANCE: Added automatic log retention policy (7 days, configurable)
    - CODE QUALITY: Added helper functions (Test-ValidDownloadUrl, Start-ProcessWithTimeout, Invoke-FileDownloadWithProgress)
    - CONSISTENCY: Sequential and parallel modes now have identical security and performance protections

    Previous fixes (v2.2.0):
    - PowerShell 5.1 StrictMode compatibility (InstallationOptions null-safe checks)
    - Nested conditions instead of chained -and operators
    - Automatic fallback on installation failure
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
$script:EnvironmentDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'
$script:FeatureFlagsPath = Join-Path $script:RepositoryRoot 'Core\FeatureFlags.psm1'
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
$script:ExceptionsPath = Join-Path $script:RepositoryRoot 'Core\Win11ForgeExceptions.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Feature Flags module
if (-not (Get-Command -Name Test-FeatureEnabled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:FeatureFlagsPath) {
        Import-Module -Name $script:FeatureFlagsPath -Force
    }
}

# Import Directory Constants module
if (-not (Get-Command -Name Get-Win11ForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# Import Localization module for i18n support
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# Import EnvironmentDetection for sandbox detection
if (-not (Get-Command -Name Test-IsWindowsSandbox -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:EnvironmentDetectionPath) {
        Import-Module -Name $script:EnvironmentDetectionPath -Force
    }
}

# Import WingetCache for optimized winget list caching
$script:WingetCachePath = Join-Path $script:ModuleRoot 'WingetCache.psm1'
if (-not (Get-Command -Name Get-CachedWingetList -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:WingetCachePath) {
        Import-Module -Name $script:WingetCachePath -Force
    }
}

# Import ApplicationDetection module (extracted from InstallationEngine v3.1.4)
$script:ApplicationDetectionPath = Join-Path $script:ModuleRoot 'ApplicationDetection.psm1'
if (-not (Get-Command -Name Test-ApplicationInstalled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:ApplicationDetectionPath) {
        Import-Module -Name $script:ApplicationDetectionPath -Force
    }
}

# Import InstallationMethods module (extracted from InstallationEngine v3.1.4)
$script:InstallationMethodsPath = Join-Path $script:ModuleRoot 'InstallationMethods.psm1'
if (-not (Get-Command -Name Install-ViaWinget -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:InstallationMethodsPath) {
        Import-Module -Name $script:InstallationMethodsPath -Force
    }
}

# === CONFIGURATION ===
$script:MaxParallelJobs = 5
$script:JobCheckInterval = 2
$script:DefaultInstallTimeoutSeconds = 1800  # 30 minutes (increased for slower VMs/networks)
$script:OfficeInstallTimeoutSeconds = 2700   # 45 minutes for Office (Click-to-Run is slow)
$script:ParallelInstallTimeoutMs = 600000    # 10 minutes for parallel installs (consistent with sequential)

# Note: AllowedDetectionExecutables moved to ApplicationDetection.psm1

# === ROLLBACK & RESUME SYSTEM ===
# Use LOCALAPPDATA for state files (more secure than TEMP, user-specific)
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
                        # Security: Use Start-Process with argument array to prevent command injection
                        $wingetArgs = @('uninstall', '--id', $app.Identifier, '--silent', '--accept-source-agreements')
                        $process = Start-Process -FilePath 'winget' -ArgumentList $wingetArgs -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue
                        $uninstalled = ($null -ne $process -and $process.ExitCode -eq 0)
                    }
                }
                'Chocolatey' {
                    if ($app.Identifier -and (Get-Command choco -ErrorAction SilentlyContinue)) {
                        # Security: Use Start-Process with argument array to prevent command injection
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

    # Clear rollback state after execution
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

    # Remove from pending
    $script:DeploymentState.PendingApps = @($script:DeploymentState.PendingApps | Where-Object { $_ -ne $AppName })

    # Add to appropriate list
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

    # Validate SessionId is a valid GUID
    if ($StateData.SessionId) {
        try {
            [guid]::Parse($StateData.SessionId) | Out-Null
        } catch {
            Write-Status -Message "Invalid SessionId format in state file" -Level 'Warning'
            return $false
        }
    }

    # Validate ProfileName has no path traversal characters
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

    # Validate TotalApps is a reasonable number
    if ($null -ne $StateData.TotalApps) {
        if ($StateData.TotalApps -lt 0 -or $StateData.TotalApps -gt 1000) {
            Write-Status -Message "Invalid TotalApps value in state file" -Level 'Warning'
            return $false
        }
    }

    # Validate app arrays contain only safe strings (no shell metacharacters)
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

    # Try to load from disk
    if (Test-Path $script:DeploymentStateFile) {
        try {
            $loaded = Get-Content $script:DeploymentStateFile -Raw | ConvertFrom-Json

            # Security: Validate loaded state data
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

# === HELPER FUNCTIONS ===

function Test-ValidDownloadUrl {
    <#
    .SYNOPSIS
        Validates download URL for security and format
    .DESCRIPTION
        Validates URL structure and checks against trusted domain whitelist.
        Loads trusted domains from Config/download-sources.json if available.
    .PARAMETER Url
        The URL to validate.
    .PARAMETER AllowUntrusted
        If specified, allows non-whitelisted domains (logs warning but returns true).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [switch]$AllowUntrusted
    )

    # Check URL format (must be HTTP/HTTPS)
    if (-not ($Url -match '^https?://')) {
        Write-Status -Message "Invalid URL protocol (must be HTTP/HTTPS): $Url" -Level 'Verbose'
        return $false
    }

    # Validate URI structure
    try {
        $uri = [System.Uri]$Url
        if ($uri.Scheme -notin @('http', 'https')) {
            Write-Status -Message "Invalid URL scheme: $($uri.Scheme)" -Level 'Verbose'
            return $false
        }
    } catch {
        Write-Status -Message "Malformed URL: $Url" -Level 'Verbose'
        return $false
    }

    # Try to load trusted domains from config file
    $configPath = Join-Path $script:RepositoryRoot 'Config\download-sources.json'
    $configTrustedDomains = @()

    if (Test-Path -Path $configPath -ErrorAction SilentlyContinue) {
        try {
            $config = Get-Content -Path $configPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            if ($config.trustedDomains -and $config.trustedDomains.domains) {
                $configTrustedDomains = @($config.trustedDomains.domains)
            }
        } catch {
            Write-Status -Message "Could not load trusted domains config: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Fallback whitelist if config not available (common trusted CDNs and vendors)
    $fallbackDomains = @(
        # Microsoft ecosystem
        'microsoft.com', 'windows.com', 'windowsupdate.com', 'azure.com', 'azureedge.net',
        'office.com', 'visualstudio.com', 'visualstudio.microsoft.com',
        # Code hosting
        'github.com', 'githubusercontent.com', 'githubassets.com', 'gitlab.com',
        # CDNs
        'akamai.net', 'akamaized.net', 'cloudflare.com', 'cloudfront.net',
        'fastly.net', 'jsdelivr.net', 'amazonaws.com', 'steamstatic.com',
        # Common vendors
        'chocolatey.org', 'community.chocolatey.org'
    )

    # Merge config and fallback domains
    $trustedDomains = @()
    if ($configTrustedDomains.Count -gt 0) {
        $trustedDomains = $configTrustedDomains
    }
    $trustedDomains += $fallbackDomains
    $trustedDomains = $trustedDomains | Select-Object -Unique

    # Check if domain matches any trusted domain
    $hostLower = $uri.Host.ToLower()
    $domainMatched = $false

    foreach ($trusted in $trustedDomains) {
        $trustedLower = $trusted.ToLower()
        # Exact match or subdomain match
        if ($hostLower -eq $trustedLower -or $hostLower.EndsWith(".$trustedLower")) {
            $domainMatched = $true
            break
        }
    }

    if (-not $domainMatched) {
        if ($AllowUntrusted) {
            Write-Status -Message "Downloading from non-whitelisted domain (allowed by caller): $($uri.Host)" -Level 'Warning'
            return $true
        } else {
            Write-Status -Message "Download blocked - untrusted domain: $($uri.Host). Add to Config\download-sources.json trustedDomains to allow." -Level 'Warning'
            return $false
        }
    }

    return $true
}

function Start-ProcessWithTimeout {
    <#
    .SYNOPSIS
        Starts a process with timeout protection
    #>
    [CmdletBinding()]
    [OutputType([System.Diagnostics.Process])]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$ArgumentList,

        [Parameter()]
        [int]$TimeoutSeconds = 600,

        [Parameter()]
        [switch]$NoNewWindow,

        [Parameter()]
        [switch]$PassThru
    )

    try {
        $processParams = @{
            FilePath = $FilePath
            NoNewWindow = $NoNewWindow.IsPresent
            PassThru = $true
        }

        if ($ArgumentList) {
            $processParams['ArgumentList'] = $ArgumentList
        }

        $process = Start-Process @processParams

        # Wait for process with timeout
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Write-Status -Message "Process timed out after $TimeoutSeconds seconds - terminating" -Level 'Warning'
            $process.Kill()
            throw "Process execution timed out after $TimeoutSeconds seconds"
        }

        # Refresh process object to ensure ExitCode is available
        # This is required because Start-Process -PassThru may not update ExitCode after WaitForExit
        $process.Refresh()

        # Ensure we have a valid exit code (fallback to -1 if null)
        $exitCode = if ($null -ne $process.ExitCode) { $process.ExitCode } else { -1 }

        if ($PassThru) {
            # Create a wrapper object with guaranteed ExitCode
            return [PSCustomObject]@{
                ExitCode = $exitCode
                Id = $process.Id
                HasExited = $process.HasExited
            }
        }

        return $exitCode
    } catch {
        Write-Status -Message "Process execution failed: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

function Invoke-FileDownloadWithProgress {
    <#
    .SYNOPSIS
        Downloads file with progress reporting, streaming, and optional checksum validation
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter()]
        [int]$TimeoutSeconds = 1800,  # 30 minutes for large files

        [Parameter()]
        [string]$ExpectedSHA256 = $null  # Optional SHA256 checksum for validation
    )

    # Ensure TLS 1.2 is enabled (required by many modern servers)
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $downloadSuccess = $false

    # Method 1: Try WebClient (fast, efficient for most URLs)
    try {
        Write-Output "[INFO] Attempting download via WebClient..."
        Write-Status -Message "Attempting download via WebClient..." -Level 'Verbose'
        $webClient = New-Object System.Net.WebClient
        # Use browser-like headers to avoid blocks
        $webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
        $webClient.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
        $webClient.Headers.Add("Accept-Language", "en-US,en;q=0.5")

        # Progress reporting (optional)
        $script:lastReportedPercent = -10
        $progressHandler = {
            param($eventSender, $e)
            if ($e.TotalBytesToReceive -gt 0) {
                $percent = [int](($e.BytesReceived / $e.TotalBytesToReceive) * 100)
                if ($percent -ge ($script:lastReportedPercent + 10)) {
                    $script:lastReportedPercent = $percent
                    Write-Output "[INFO] Download progress: $percent%"
                    Write-Status -Message "Download progress: $percent% ($($e.BytesReceived) / $($e.TotalBytesToReceive) bytes)" -Level 'Verbose'
                }
            }
        }

        Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -Action $progressHandler | Out-Null

        $downloadTask = $webClient.DownloadFileTaskAsync($Url, $OutputPath)
        $downloadTask.Wait()

        $webClient.Dispose()
        Get-EventSubscriber | Where-Object { $_.SourceObject -eq $webClient } | Unregister-Event -ErrorAction SilentlyContinue

        if ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
            $downloadSuccess = $true
            Write-Output "[SUCCESS] Download completed"
            Write-Status -Message "WebClient download succeeded" -Level 'Verbose'
        }
    } catch {
        Write-Output "[WARNING] WebClient download failed, trying fallback method..."
        Write-Status -Message "WebClient download failed: $($_.Exception.Message)" -Level 'Verbose'
        if ($webClient) { $webClient.Dispose() }
        Get-EventSubscriber | Where-Object { $_.SourceObject -eq $webClient } | Unregister-Event -ErrorAction SilentlyContinue
        # Clean up any partial file
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
    }

    # Method 2: Fallback to Invoke-WebRequest (better redirect handling)
    if (-not $downloadSuccess) {
        # Clean up any partial file from previous attempt
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            Write-Output "[INFO] Attempting download via Invoke-WebRequest..."
            Write-Status -Message "Attempting download via Invoke-WebRequest (handles redirects)..." -Level 'Verbose'
            $ProgressPreference = 'SilentlyContinue'  # Disable progress bar for speed
            $headers = @{
                "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                "Accept" = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"
            }
            Invoke-WebRequest -Uri $Url -OutFile $OutputPath -UseBasicParsing -TimeoutSec $TimeoutSeconds -Headers $headers -ErrorAction Stop
            if ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                $downloadSuccess = $true
                Write-Output "[SUCCESS] Download completed"
            }
        } catch {
            Write-Output "[WARNING] Invoke-WebRequest failed, trying BITS transfer..."
            Write-Status -Message "Invoke-WebRequest download failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Method 3: Try curl.exe (built into Windows 10/11, handles redirects well)
    if (-not $downloadSuccess) {
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            $curlPath = "$env:SystemRoot\System32\curl.exe"
            if (Test-Path $curlPath) {
                Write-Output "[INFO] Attempting download via curl.exe..."
                Write-Status -Message "Attempting download via curl.exe..." -Level 'Verbose'
                # Use & operator with proper argument handling
                $curlOutput = & $curlPath -L -o "$OutputPath" -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0" --connect-timeout 30 --max-time $TimeoutSeconds "$Url" 2>&1
                if ($LASTEXITCODE -eq 0 -and (Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                    $downloadSuccess = $true
                    Write-Output "[SUCCESS] Download completed via curl"
                } else {
                    Write-Output "[WARNING] curl exit code: $LASTEXITCODE"
                }
            }
        } catch {
            Write-Output "[WARNING] curl.exe download failed: $($_.Exception.Message)"
            Write-Status -Message "curl.exe download failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Method 4: Last resort - Start-BitsTransfer (BITS service)
    if (-not $downloadSuccess) {
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            Write-Output "[INFO] Attempting download via BITS transfer..."
            Write-Status -Message "Attempting download via BITS transfer..." -Level 'Verbose'
            Start-BitsTransfer -Source $Url -Destination $OutputPath -ErrorAction Stop
            if ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                $downloadSuccess = $true
                Write-Output "[SUCCESS] Download completed via BITS"
            }
        } catch {
            Write-Output "[ERROR] BITS transfer failed"
            Write-Status -Message "BITS transfer failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    if (-not $downloadSuccess) {
        Write-Output "[ERROR] Download failed: All methods exhausted"
        Write-Status -Message "Download failed: All methods exhausted" -Level 'Error'
        return $false
    }

    # Verify file exists and has content
    if (-not (Test-Path -Path $OutputPath)) {
        Write-Status -Message "Download failed: File not found after download" -Level 'Error'
        return $false
    }

    $fileSize = (Get-Item -Path $OutputPath).Length
    if ($fileSize -eq 0) {
        Write-Status -Message "Download failed: Downloaded file is empty" -Level 'Error'
        Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        return $false
    }

    Write-Status -Message "Download completed ($fileSize bytes)" -Level 'Verbose'

    # SHA256 checksum validation
    if ($ExpectedSHA256) {
        Write-Status -Message "Validating SHA256 checksum..." -Level 'Verbose'
        $fileHash = (Get-FileHash -Path $OutputPath -Algorithm SHA256).Hash

        if ($fileHash -ne $ExpectedSHA256) {
            Write-Status -Message "Checksum validation FAILED! Expected: $ExpectedSHA256, Got: $fileHash" -Level 'Error'
            Write-Status -Message "Removing potentially corrupted file..." -Level 'Warning'
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
            return $false
        }

        Write-Status -Message "Checksum validation passed (SHA256: $fileHash)" -Level 'Success'
    }

    return $true
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

    # No restrictions defined - allow installation
    if (-not $Application.EnvironmentRestrictions -or $Application.EnvironmentRestrictions.Count -eq 0) {
        return $result
    }

    # Ensure EnvironmentDetection module is loaded
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

# === DETECTION FUNCTIONS ===

function Wait-ForOfficeInstallation {
    <#
    .SYNOPSIS
        Waits for Office Click-to-Run installation to complete.

    .DESCRIPTION
        Office installations via Winget/Chocolatey often return before the actual
        installation is complete because Click-to-Run downloads and installs in the
        background. This function polls for Office executables until they appear
        or until timeout is reached.

    .PARAMETER TimeoutSeconds
        Maximum time to wait for installation completion. Default: 2700 (45 minutes).

    .PARAMETER PollIntervalSeconds
        Time between checks. Default: 30 seconds.

    .OUTPUTS
        [bool] True if Office was detected as installed, false if timeout reached.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [int]$TimeoutSeconds = 2700,

        [Parameter()]
        [int]$PollIntervalSeconds = 30
    )

    Write-Status -Message "Waiting for Office Click-to-Run installation to complete..." -Level 'Info'

    $officePaths = @(
        "${env:ProgramFiles}\Microsoft Office\root\Office16\WINWORD.EXE",
        "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\WINWORD.EXE",
        "${env:ProgramFiles}\Microsoft Office\root\Office16\EXCEL.EXE",
        "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\EXCEL.EXE"
    )

    $startTime = Get-Date
    $endTime = $startTime.AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $endTime) {
        # Check if any Office executable exists
        foreach ($path in $officePaths) {
            if (Test-Path -Path $path -PathType Leaf) {
                Write-Status -Message "Office installation detected: $path" -Level 'Success'
                return $true
            }
        }

        # Check if OfficeClickToRun process is still running (installation in progress)
        $clickToRunProcess = Get-Process -Name "OfficeClickToRun" -ErrorAction SilentlyContinue
        if ($clickToRunProcess) {
            $elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
            Write-Status -Message "Office Click-to-Run still installing... ($elapsed minutes elapsed)" -Level 'Info'
        }

        # Also check for OfficeC2RClient which handles updates/installs
        $c2rClient = Get-Process -Name "OfficeC2RClient" -ErrorAction SilentlyContinue
        if ($c2rClient) {
            $elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
            Write-Status -Message "Office C2R Client active... ($elapsed minutes elapsed)" -Level 'Info'
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    # Final check before returning failure
    foreach ($path in $officePaths) {
        if (Test-Path -Path $path -PathType Leaf) {
            Write-Status -Message "Office installation detected: $path" -Level 'Success'
            return $true
        }
    }

    Write-Status -Message "Office installation detection timed out after $([math]::Round($TimeoutSeconds / 60)) minutes" -Level 'Warning'
    return $false
}

function Test-ApplicationInstalled {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $appName = $Application.Name

    # Special case: PowerToys - Check multiple paths
    if ($appName -eq 'Microsoft PowerToys') {
        $powerToysPaths = @(
            "${env:ProgramFiles}\PowerToys\PowerToys.exe",
            "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
            "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
        )

        foreach ($path in $powerToysPaths) {
            if (Test-Path $path -ErrorAction SilentlyContinue) {
                return $true
            }
        }

        if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) {
            return $true
        }
    }

    # Special case: Quick Assist - Store App (use winget to avoid Appx module conflicts)
    if ($appName -eq 'Microsoft Quick Assist') {
        if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            $wingetList = Get-CachedWingetList
            if ($wingetList -match 'MicrosoftCorporationII') {
                return $true
            }
        }
    }

    if (-not $Application.Detection) {
        $detected = Test-ApplicationByName -Name $appName
    } else {
        $method = $Application.Detection.Method
        $detected = $false

        switch ($method) {
            'Registry' {
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $detected = Test-RegistryKey -Path $Application.Detection.Path
                }
            }
            'File' {
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $expandedPath = Expand-DetectionPath -Path $Application.Detection.Path
                    $detected = Test-Path -Path $expandedPath -PathType Leaf
                }
            }
            'Command' {
                try {
                    # Secure command execution - parse command into executable and arguments
                    $commandParts = $Application.Detection.Command -split '\s+', 2
                    $executable = $commandParts[0]
                    $arguments = if ($commandParts.Count -gt 1) { $commandParts[1] } else { $null }

                    # Security: Only allow whitelisted executables for command detection
                    $exeBaseName = [System.IO.Path]::GetFileName($executable).ToLower()
                    if ($exeBaseName -notin $script:AllowedDetectionExecutables) {
                        Write-Status -Message "Command detection blocked: '$executable' not in allowed executables list" -Level 'Verbose'
                        $detected = $false
                    }
                    # Validate executable exists
                    elseif (Get-Command -Name $executable -ErrorAction SilentlyContinue) {
                        # Check if we need to verify output content (Arguments field contains expected pattern)
                        $expectedPattern = if ($Application.Detection.PSObject.Properties['Arguments']) { $Application.Detection.Arguments } else { $null }

                        if ($expectedPattern) {
                            # Run command and capture output for pattern matching
                            $output = if ($arguments) {
                                & $executable $arguments 2>&1 | Out-String
                            } else {
                                & $executable 2>&1 | Out-String
                            }
                            $detected = $output -match [regex]::Escape($expectedPattern)
                        } else {
                            # Execute securely with Start-Process for simple exit code check
                            $process = if ($arguments) {
                                Start-Process -FilePath $executable -ArgumentList $arguments -Wait -NoNewWindow -PassThru -ErrorAction Stop
                            } else {
                                Start-Process -FilePath $executable -Wait -NoNewWindow -PassThru -ErrorAction Stop
                            }
                            $detected = $process.ExitCode -eq 0
                        }
                    }
                } catch {
                    $detected = $false
                }
            }
            'WindowsFeature' {
                $feature = Get-WindowsOptionalFeature -Online -FeatureName $Application.Detection.Feature -ErrorAction SilentlyContinue
                $detected = $feature -and $feature.State -eq 'Enabled'
            }
            'WindowsCapability' {
                $capability = Get-WindowsCapability -Online -Name "*$($Application.Detection.Capability)*" -ErrorAction SilentlyContinue
                $detected = $capability -and $capability.State -eq 'Installed'
            }
            'StoreApp' {
                # Use cached winget list instead of Get-AppxPackage to avoid Appx module conflicts in PowerShell 7
                if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                    try {
                        $wingetList = Get-CachedWingetList

                        # Try 1: Match by Store ID (most specific)
                        if ($Application.Sources.Store) {
                            if ($wingetList -match [regex]::Escape($Application.Sources.Store) -and $wingetList -notmatch "No installed package") {
                                $detected = $true
                            }
                        }

                        # Try 2: Match by full PackageName (specific, avoids false positives from vendor prefix)
                        if (-not $detected -and $Application.Detection.PackageName) {
                            if ($wingetList -match [regex]::Escape($Application.Detection.PackageName)) {
                                $detected = $true
                            }
                        }
                    } catch {
                        $detected = $false
                    }
                } else {
                    # Fallback to Get-AppxPackage if winget not available (PowerShell 5.1)
                    try {
                        $package = Get-AppxPackage -Name "*$($Application.Detection.PackageName)*" -ErrorAction SilentlyContinue
                        $detected = $null -ne $package
                    } catch {
                        $detected = $false
                    }
                }
            }
            default {
                $detected = Test-ApplicationByName -Name $appName
            }
        }
    }

    # Winget-based fallback detection if primary method failed
    if (-not $detected -and $Application.Sources -and $Application.Sources.Winget) {
        if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            try {
                $wingetId = $Application.Sources.Winget
                $wingetResult = & winget list --id $wingetId --accept-source-agreements 2>&1 | Out-String
                if ($wingetResult -match [regex]::Escape($wingetId) -and $wingetResult -notmatch "No installed package") {
                    $detected = $true
                }
            } catch {
                # Winget fallback failed silently - keep original detection result
                Write-Verbose "Winget fallback detection failed for $($Application.Name): $_"
            }
        }
    }

    return $detected
}

function Test-ApplicationByName {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    try {
        if (Test-CommandExists -Name 'winget') {
            # Use cached winget list for performance
            $wingetList = Get-CachedWingetList
            if ($wingetList -match [regex]::Escape($Name)) {
                return $true
            }
        }
    } catch {
        Write-Status -Message "Winget detection failed: $($_.Exception.Message)" -Level 'Verbose'
    }

    try {
        if (Test-CommandExists -Name 'choco') {
            $chocoList = & choco list --local-only --exact $Name 2>&1 | Out-String
            if ($chocoList -match $Name -and $chocoList -notmatch '0 packages installed') {
                return $true
            }
        }
    } catch {
        Write-Status -Message "Chocolatey detection failed: $($_.Exception.Message)" -Level 'Verbose'
    }

    $programFiles = @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        "$env:LOCALAPPDATA\Programs"
    )

    foreach ($baseDir in $programFiles) {
        if (Test-Path -Path "$baseDir\$Name") {
            return $true
        }
    }

    return $false
}

function Test-RegistryKey {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return Test-Path -Path $Path -ErrorAction SilentlyContinue
}

function Expand-DetectionPath {
    <#
    .SYNOPSIS
        Expands environment variables in detection paths with security validation.
    .DESCRIPTION
        Supports both %VAR% and $env:VAR syntax for environment variable expansion.
        Also handles common path aliases like %PROGRAMFILES%, %LOCALAPPDATA%, etc.
        Validates against path traversal attacks.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    # Security: Block path traversal attempts in input
    if ($Path -match '\.\.' -or $Path -match '[\\/]\.\.[\\/]?' -or $Path -match '^\.\.') {
        Write-Status -Message "Path traversal attempt blocked in detection path: $Path" -Level 'Warning'
        return $null
    }

    # Expand %VAR% style environment variables
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)

    # Also handle $env:VAR style (PowerShell native)
    if ($expanded -match '\$env:') {
        $expanded = $ExecutionContext.InvokeCommand.ExpandString($expanded)
    }

    # Security: Block path traversal attempts after expansion
    if ($expanded -match '\.\.' -or $expanded -match '[\\/]\.\.[\\/]?' -or $expanded -match '^\.\.') {
        Write-Status -Message "Path traversal attempt blocked after expansion: $expanded" -Level 'Warning'
        return $null
    }

    # Security: Ensure path is absolute (not relative)
    if (-not [System.IO.Path]::IsPathRooted($expanded)) {
        # Allow wildcards in detection paths (e.g., "C:\Program Files\*\app.exe")
        if ($expanded -notmatch '^[A-Za-z]:[\\/]') {
            Write-Status -Message "Relative path not allowed in detection: $expanded" -Level 'Warning'
            return $null
        }
    }

    return $expanded
}

function Get-InstalledAppVersion {
    <#
    .SYNOPSIS
        Gets the installed version of an application via Winget or Chocolatey.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [string]$WingetId,

        [Parameter()]
        [string]$ChocolateyId
    )

    # Try Winget first
    if ($WingetId -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        try {
            $output = & winget list --id $WingetId --accept-source-agreements 2>&1 | Out-String
            # Parse version from winget list output (format: Name  Id  Version  Available  Source)
            # Version is typically after the ID column
            $lines = $output -split "`n" | Where-Object { $_ -match [regex]::Escape($WingetId) }
            if ($lines) {
                # Extract version using regex - version is usually a pattern like 1.2.3 or 1.2.3.4
                if ($lines[0] -match '\s(\d+[\.\d]+)\s') {
                    return $Matches[1]
                }
            }
        } catch { }
    }

    # Try Chocolatey
    if ($ChocolateyId -and (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)) {
        try {
            $output = & choco list --local-only --exact $ChocolateyId 2>&1 | Out-String
            # Parse version from choco list output (format: packagename version)
            if ($output -match "$([regex]::Escape($ChocolateyId))\s+([\d\.]+)") {
                return $Matches[1]
            }
        } catch { }
    }

    return $null
}

# === INSTALLATION METHODS ===

function Install-ViaWinget {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter()]
        [switch]$Silent,

        [Parameter()]
        [int]$MaxRetries = 3,

        [Parameter()]
        [int]$RetryDelaySeconds = 2
    )

    if (-not (Test-CommandExists -Name 'winget')) {
        Write-Status -Message "Winget not available" -Level 'Verbose'
        return $false
    }

    # Silent installation by default (unless explicitly set to $false)
    $isSilent = -not $PSBoundParameters.ContainsKey('Silent') -or $Silent.IsPresent

    $arguments = @(
        'install',
        '--id', $PackageId,
        '--accept-package-agreements',
        '--accept-source-agreements'
    )

    if ($isSilent) {
        $arguments += '--silent'
    }

    # Retry logic with exponential backoff
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            if ($attempt -eq 1) {
                Write-Output "[INFO] Installing via Winget: $PackageId"
                Write-Status -Message "Installing via Winget: $PackageId" -Level 'Info'
            } else {
                Write-Output "[INFO] Retry $attempt/$MaxRetries for Winget: $PackageId"
                Write-Status -Message "Retry $attempt/$MaxRetries for Winget: $PackageId" -Level 'Info'
            }

            # Execute winget and capture output to detect "already installed" patterns
            $wingetOutput = & winget @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" patterns in output - treat as success
            # Winget may say "already installed", "No available upgrade", "No newer package versions"
            if ($wingetOutput -match 'already installed' -or
                $wingetOutput -match 'No available upgrade' -or
                $wingetOutput -match 'No newer package versions' -or
                $wingetOutput -match 'Successfully installed') {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Exit code 0 = success
            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Exit code -1978334974 = APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_ALREADY_INSTALLED
            if ($exitCode -eq -1978334974) {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Already installed (Winget)$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Already installed (Winget)$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Check for transient network errors (exit codes that might benefit from retry)
            $transientErrors = @(-1978335189, -1978335212)  # Common Winget network errors
            if ($transientErrors -contains $exitCode) {
                if ($attempt -lt $MaxRetries) {
                    $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                    Write-Output "[WARNING] Transient error detected (exit code: $exitCode), retrying in $delay seconds..."
                    Write-Status -Message "Transient error detected (exit code: $exitCode), retrying in $delay seconds..." -Level 'Warning'
                    Start-Sleep -Seconds $delay
                    continue
                }
                # Last attempt with transient error - don't verify (could detect old install), just fail
                Write-Output "[WARNING] Winget installation failed after $MaxRetries attempts (exit code: $exitCode)"
                Write-Status -Message "Winget installation failed after $MaxRetries attempts (exit code: $exitCode)" -Level 'Warning'
                return $false
            }

            # Post-install verification: Check if package is actually installed despite non-zero exit code
            # This handles cases where winget returns unexpected exit codes but installation succeeded
            Write-Output "[INFO] Verifying installation..."
            $verifyResult = & winget list --id $PackageId --accept-source-agreements 2>&1 | Out-String
            if ($verifyResult -match [regex]::Escape($PackageId) -and $verifyResult -notmatch "No installed package") {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            Write-Output "[WARNING] Winget installation failed (exit code: $exitCode)"
            Write-Status -Message "Winget installation failed (exit code: $exitCode)" -Level 'Warning'
            return $false

        } catch {
            if ($attempt -lt $MaxRetries) {
                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Winget error: $($_.Exception.Message), retrying in $delay seconds..."
                Write-Status -Message "Winget error: $($_.Exception.Message), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            } else {
                Write-Output "[ERROR] Winget installation error after $MaxRetries attempts: $($_.Exception.Message)"
                Write-Status -Message "Winget installation error after $MaxRetries attempts: $($_.Exception.Message)" -Level 'Verbose'
                return $false
            }
        }
    }

    return $false
}

function Install-ViaChocolatey {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageName,

        [Parameter()]
        [int]$MaxRetries = 3,

        [Parameter()]
        [int]$RetryDelaySeconds = 2
    )

    if (-not (Test-CommandExists -Name 'choco')) {
        Write-Status -Message "Chocolatey not available" -Level 'Verbose'
        return $false
    }

    $arguments = @(
        'install', $PackageName,
        '-y',
        '--no-progress',
        '--ignore-checksums'
    )

    # Retry logic with exponential backoff
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            if ($attempt -eq 1) {
                Write-Output "[INFO] Installing via Chocolatey: $PackageName"
                Write-Status -Message "Installing via Chocolatey: $PackageName" -Level 'Info'
            } else {
                Write-Output "[INFO] Retry $attempt/$MaxRetries for Chocolatey: $PackageName"
                Write-Status -Message "Retry $attempt/$MaxRetries for Chocolatey: $PackageName" -Level 'Info'
            }

            # Execute choco and capture output to detect "already installed"
            $chocoOutput = & choco @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" pattern in output - treat as success
            if ($chocoOutput -match 'already installed' -or $chocoOutput -match 'has been installed') {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Check for transient network errors (but NOT if already installed)
            $transientErrors = @(1641, 3010, -1)  # Common Chocolatey transient errors (reboot required, network timeout)
            if ($transientErrors -contains $exitCode -and $attempt -lt $MaxRetries) {
                # Before retrying, check if package is already installed
                Write-Output "[INFO] Verifying installation..."
                $chocoList = & choco list --local-only --exact $PackageName 2>&1 | Out-String
                if ($chocoList -match $PackageName -and $chocoList -notmatch "0 packages installed") {
                    $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                    $versionInfo = if ($version) { " v$version" } else { "" }
                    Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo (verified)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                    Write-Status -Message "Installed successfully via Chocolatey$versionInfo (verified)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                    return $true
                }

                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Transient error detected (exit code: $exitCode), retrying in $delay seconds..."
                Write-Status -Message "Transient error detected (exit code: $exitCode), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            }

            # Post-install verification: Check if package is actually installed despite non-zero exit code
            # Chocolatey may return non-zero codes even when installation succeeded
            Write-Output "[INFO] Verifying installation..."
            $chocoList = & choco list --local-only --exact $PackageName 2>&1 | Out-String
            if ($chocoList -match $PackageName -and $chocoList -notmatch "0 packages installed") {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            Write-Output "[WARNING] Chocolatey installation failed (exit code: $exitCode)"
            Write-Status -Message "Chocolatey installation failed (exit code: $exitCode)" -Level 'Verbose'
            return $false

        } catch {
            if ($attempt -lt $MaxRetries) {
                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Chocolatey error: $($_.Exception.Message), retrying in $delay seconds..."
                Write-Status -Message "Chocolatey error: $($_.Exception.Message), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            } else {
                Write-Output "[ERROR] Chocolatey installation error after $MaxRetries attempts: $($_.Exception.Message)"
                Write-Status -Message "Chocolatey installation error after $MaxRetries attempts: $($_.Exception.Message)" -Level 'Verbose'
                return $false
            }
        }
    }

    return $false
}

function Install-ViaStore {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$ProductId
    )

    # Check for Windows Sandbox - Store is unavailable
    if (Test-IsWindowsSandbox) {
        Write-Output "[WARNING] Skipping Store install for $ProductId - Windows Store is unavailable in Sandbox"
        Write-Status -Message "Skipping Store install for $ProductId - Windows Store is unavailable in Sandbox" -Level 'Warning'
        return $false
    }

    try {
        Write-Output "[INFO] Installing via Microsoft Store: $ProductId"
        Write-Status -Message "Installing via Microsoft Store: $ProductId" -Level 'Info'

        if (Test-CommandExists -Name 'winget') {
            $arguments = @(
                'install',
                '--id', $ProductId,
                '--source', 'msstore',
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            # Execute winget and capture output to detect "already installed" patterns
            $storeOutput = & winget @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" patterns in output - treat as success
            if ($storeOutput -match 'already installed' -or
                $storeOutput -match 'No available upgrade' -or
                $storeOutput -match 'No newer package versions' -or
                $storeOutput -match 'Successfully installed') {
                $version = Get-InstalledAppVersion -WingetId $ProductId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Microsoft Store$versionInfo"
                Write-Status -Message "Installed successfully via Microsoft Store$versionInfo" -Level 'Success'
                return $true
            }

            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -WingetId $ProductId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Microsoft Store$versionInfo"
                Write-Status -Message "Installed successfully via Microsoft Store$versionInfo" -Level 'Success'
                return $true
            }
        }

        Start-Process "ms-windows-store://pdp/?ProductId=$ProductId"
        Write-Output "[WARNING] Store opened - please complete installation manually"
        Write-Status -Message "Store opened - please complete installation manually" -Level 'Warning'
        return $false

    } catch {
        Write-Output "[ERROR] Store installation error: $($_.Exception.Message)"
        Write-Status -Message "Store installation error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

# === DIRECT DOWNLOAD HELPER FUNCTIONS ===

function Install-MsiPackage {
    <#
    .SYNOPSIS
        Installs an MSI package silently.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath
    )

    Write-Output "[INFO] Installing MSI package..."
    $arguments = @('/i', "`"$InstallerPath`"", '/qn', '/norestart')
    $process = Start-ProcessWithTimeout -FilePath 'msiexec.exe' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
    if ($process.ExitCode -eq 0) {
        Write-Output "[SUCCESS] MSI package installed successfully"
    } else {
        Write-Output "[WARNING] MSI installer returned exit code: $($process.ExitCode)"
    }
    return ($process.ExitCode -eq 0)
}

function Install-ExePackage {
    <#
    .SYNOPSIS
        Installs an EXE package with custom or auto-detected silent switches.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath,

        [Parameter()]
        [string]$CustomArguments = $null
    )

    if ($CustomArguments) {
        Write-Output "[INFO] Running installer with custom arguments: $CustomArguments"
        Write-Status -Message "Using custom install arguments: $CustomArguments" -Level 'Verbose'
        try {
            $process = Start-ProcessWithTimeout -FilePath $InstallerPath -ArgumentList $CustomArguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            if ($process.ExitCode -eq 0) {
                Write-Output "[SUCCESS] Installation completed"
            } else {
                Write-Output "[WARNING] Installer returned exit code: $($process.ExitCode)"
            }
            return ($process.ExitCode -eq 0)
        } catch {
            Write-Output "[ERROR] EXE installation failed: $($_.Exception.Message)"
            Write-Status -Message "EXE installation with custom args failed: $($_.Exception.Message)" -Level 'Verbose'
            return $false
        }
    }

    Write-Output "[INFO] Trying silent installation switches..."
    # Try common silent switches
    $silentSwitches = @('/S', '/SILENT', '/VERYSILENT', '/quiet', '/qn')
    foreach ($switch in $silentSwitches) {
        try {
            $process = Start-ProcessWithTimeout -FilePath $InstallerPath -ArgumentList $switch -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            if ($process.ExitCode -eq 0) {
                Write-Output "[SUCCESS] Installation completed with switch: $switch"
                return $true
            }
        } catch {
            continue
        }
    }

    Write-Output "[WARNING] No silent switch worked for this installer"
    return $false
}

function Install-ZipPackage {
    <#
    .SYNOPSIS
        Extracts and installs a ZIP package (installer or portable).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath,

        [Parameter()]
        [string]$TempDir,

        [Parameter()]
        [string]$CustomArguments = $null,

        [Parameter()]
        [string]$DetectionPath = $null
    )

    Write-Status -Message "Extracting ZIP archive" -Level 'Info'
    $extractPath = Join-Path $TempDir "extracted"
    Expand-Archive -Path $InstallerPath -DestinationPath $extractPath -Force

    # Check if ZIP contains an installer (setup.exe/install.exe)
    $setupExe = Get-ChildItem -Path $extractPath -Filter *.exe -Recurse |
        Where-Object { $_.Name -match 'setup|install' } |
        Select-Object -First 1

    if ($setupExe) {
        Write-Status -Message "Executing installer from archive: $($setupExe.Name)" -Level 'Info'
        try {
            $args = if ($CustomArguments) { $CustomArguments } else { '/S' }
            $process = Start-ProcessWithTimeout -FilePath $setupExe.FullName -ArgumentList $args -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            return ($process.ExitCode -eq 0)
        } catch {
            Write-Status -Message "ZIP installer execution failed: $($_.Exception.Message)" -Level 'Verbose'
            return $false
        }
    }

    # ZIP contains portable tools - deploy to destination
    Write-Status -Message "No installer found - deploying portable tools" -Level 'Info'

    $destinationPath = $null
    if ($DetectionPath) {
        $destinationPath = Split-Path $DetectionPath -Parent
        Write-Status -Message "Using detection path: $DetectionPath" -Level 'Verbose'
    }

    if (-not $destinationPath) {
        $destinationPath = Join-Path ${env:ProgramFiles} ([System.IO.Path]::GetFileNameWithoutExtension($InstallerPath))
    }

    Write-Status -Message "Deploying to: $destinationPath" -Level 'Info'

    if (-not (Test-Path $destinationPath)) {
        New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
    }

    Copy-Item -Path "$extractPath\*" -Destination $destinationPath -Recurse -Force
    Write-Status -Message "Deployment completed successfully" -Level 'Success'
    return $true
}

function Install-ViaDirectDownload {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [ValidateSet('exe', 'msi', 'zip', 'auto')]
        [string]$InstallerType = 'auto',

        [Parameter()]
        [string]$CustomArguments = $null,

        [Parameter()]
        [string]$DetectionPath = $null,

        [Parameter()]
        [string]$ExpectedSHA256 = $null  # Optional SHA256 checksum
    )

    try {
        # Validate URL before download
        if (-not (Test-ValidDownloadUrl -Url $Url)) {
            Write-Output "[ERROR] Invalid or insecure URL: $Url"
            Write-Status -Message "Invalid or insecure URL: $Url" -Level 'Error'
            return $false
        }

        Write-Output "[INFO] Downloading from: $Url"
        Write-Status -Message "Downloading from: $Url" -Level 'Info'

        $tempDir = Join-Path -Path $env:TEMP -ChildPath "Win11Forge_$([guid]::NewGuid().ToString('N'))"
        New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

        $filename = [System.IO.Path]::GetFileName($Url)
        if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.[a-z]{3,4}$') {
            $filename = "installer_$([guid]::NewGuid().ToString('N')).exe"
        }

        $installerPath = Join-Path -Path $tempDir -ChildPath $filename

        # Use streaming download with optional checksum validation
        $downloadParams = @{
            Url = $Url
            OutputPath = $installerPath
        }

        if ($ExpectedSHA256) {
            $downloadParams['ExpectedSHA256'] = $ExpectedSHA256
            Write-Output "[INFO] Checksum validation enabled (SHA256)"
            Write-Status -Message "Checksum validation enabled (SHA256)" -Level 'Info'
        }

        $downloadSuccess = Invoke-FileDownloadWithProgress @downloadParams

        if (-not $downloadSuccess -or -not (Test-Path -Path $installerPath)) {
            Write-Output "[ERROR] Download failed: File not found or checksum mismatch"
            Write-Status -Message "Download failed: File not found or checksum mismatch" -Level 'Error'
            return $false
        }

        $fileSize = Format-FileSize -Bytes (Get-Item $installerPath).Length
        Write-Output "[INFO] Downloaded: $fileSize"
        Write-Status -Message "Downloaded: $fileSize" -Level 'Info'

        if ($InstallerType -eq 'auto') {
            $InstallerType = switch -Regex ($filename) {
                '\.msi$' { 'msi' }
                '\.zip$' { 'zip' }
                default  { 'exe' }
            }
        }

        Write-Output "[INFO] Running $InstallerType installer..."

        # Install using appropriate method (delegated to helper functions)
        $installed = switch ($InstallerType) {
            'msi' { Install-MsiPackage -InstallerPath $installerPath }
            'exe' { Install-ExePackage -InstallerPath $installerPath -CustomArguments $CustomArguments }
            'zip' { Install-ZipPackage -InstallerPath $installerPath -TempDir $tempDir -CustomArguments $CustomArguments -DetectionPath $DetectionPath }
            default { $false }
        }

        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

        if ($installed) {
            Write-Output "[SUCCESS] Installed successfully via direct download"
            Write-Status -Message "Installed successfully via direct download" -Level 'Success'
        } else {
            Write-Output "[WARNING] Direct installation failed"
            Write-Status -Message "Direct installation failed" -Level 'Verbose'
        }

        return $installed

    } catch {
        Write-Output "[ERROR] Direct download error: $($_.Exception.Message)"
        Write-Status -Message "Direct download error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

function Install-WindowsFeature {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName
    )

    try {
        Write-Status -Message "Enabling Windows feature: $FeatureName" -Level 'Info'

        $feature = Get-WindowsOptionalFeature -Online -FeatureName $FeatureName -ErrorAction Stop

        if ($feature.State -eq 'Enabled') {
            Write-Status -Message "Feature already enabled" -Level 'Success'
            return $true
        }

        Enable-WindowsOptionalFeature -Online -FeatureName $FeatureName -NoRestart -ErrorAction Stop | Out-Null

        Write-Status -Message "Feature enabled successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to enable feature: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

function Install-WindowsCapability {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName
    )

    try {
        Write-Status -Message "Installing Windows capability: $CapabilityName" -Level 'Info'

        $capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$CapabilityName*" }

        if ($null -eq $capabilities -or $capabilities.Count -eq 0) {
            Write-Status -Message "Capability not found: $CapabilityName" -Level 'Error'
            return $false
        }

        $capability = if ($capabilities -is [array]) { $capabilities[0] } else { $capabilities }

        if ($capability.State -eq 'Installed') {
            Write-Status -Message "Capability already installed" -Level 'Success'
            return $true
        }

        Write-Status -Message "Installing capability: $($capability.Name)" -Level 'Verbose'
        Add-WindowsCapability -Online -Name $capability.Name -ErrorAction Stop | Out-Null

        Write-Status -Message "Capability installed successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to install capability: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

# === INSTALLATION ORCHESTRATION HELPERS ===

function Invoke-CustomInstallMethod {
    <#
    .SYNOPSIS
        Handles custom installation methods (WindowsFeature, WindowsCapability).

    .PARAMETER Application
        The application object with InstallMethod property.

    .OUTPUTS
        [hashtable] Installation result
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
        AlreadyInstalled = $false
        Method = $null
        Message = ''
    }

    $installMethod = $Application.InstallMethod

    switch ($installMethod) {
        'WindowsFeature' {
            $installed = Install-WindowsFeature -FeatureName $Application.Detection.Feature
            $result.Method = 'WindowsFeature'
            if ($installed) {
                $result.Success = $true
                $result.Message = "Installed via WindowsFeature"
            } else {
                $result.Message = "Failed to install via WindowsFeature"
            }
        }
        'WindowsCapability' {
            $installed = Install-WindowsCapability -CapabilityName $Application.Detection.Capability
            $result.Method = 'WindowsCapability'
            if ($installed) {
                $result.Success = $true
                $result.Message = "Installed via WindowsCapability"
            } else {
                $result.Message = "Failed to install via WindowsCapability"
            }
        }
        default {
            $result.Message = "Unknown install method: $installMethod"
        }
    }

    return $result
}

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

    # Helper for logging (supports both sequential and parallel modes)
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

    # Helper to check if files exist despite exit code failure
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

    # Helper to check if this is an Office installation requiring special handling
    $isOfficeApp = $sources.Winget -eq 'Microsoft.Office' -or
                   $sources.Chocolatey -eq 'microsoft-office-deployment' -or
                   $Application.Name -match 'Office\s*(365|2019|2021|2024)'

    # Helper to extract boolean result from Install-Via* functions
    # These functions use Write-Output for logging, which pollutes the return value
    # We extract just the last element (the boolean) from the output array
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
            # Special handling for Office - wait for Click-to-Run to complete
            if ($isOfficeApp) {
                & $writeLog "Office installation initiated, waiting for Click-to-Run to complete..." 'Info'
                $officeInstalled = Wait-ForOfficeInstallation -TimeoutSeconds $script:OfficeInstallTimeoutSeconds
                if (-not $officeInstalled) {
                    $result.FailureReasons += "Office Click-to-Run did not complete in time"
                    # Don't return failure yet - continue to check if actually installed
                }
            }
            $result.Success = $true
            $result.Method = 'Winget'
            $result.Message = 'Installed via Winget'
            return $result
        } else {
            # Check if files exist despite failure
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
            # Special handling for Office - wait for Click-to-Run to complete
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
            # Check if files exist despite failure
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

        # Build install parameters
        $installParams = @{ Url = $sources.DirectUrl }

        # Custom install arguments
        $installArgs = if ($Application.PSObject.Properties['InstallArguments']) { $Application.InstallArguments } else { $null }
        if ($installArgs) {
            $installParams['CustomArguments'] = $installArgs
            & $writeLog "Custom arguments detected: $installArgs" 'Verbose'
        }

        # Detection path for ZIP deployment
        if ($Application.Detection -and $Application.Detection.Path) {
            $installParams['DetectionPath'] = $Application.Detection.Path
        }

        # SHA256 checksum if available (StrictMode-safe property access)
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

    # All methods failed
    $result.Message = if ($result.AttemptedMethods.Count -gt 0) {
        "All methods failed: $($result.FailureReasons -join '; ')"
    } else {
        'No valid installation sources configured'
    }

    & $writeLog "Installation failed: $($result.Message)" 'Warning'
    return $result
}

# === APPLICATION SOURCES ===

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

# === APPLICATION UPGRADE ===

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

    # Get application sources
    $sources = Get-ApplicationSources -Application $Application

    # Try Winget upgrade first
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

            # Winget exit codes:
            # 0 = Success
            # -1978335189 = No applicable update found (APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE)
            # Other non-zero = Error
            if ($process.ExitCode -eq 0) {
                Write-Status -Message "Successfully upgraded: $($Application.Name)" -Level 'Success'
                $result.Success = $true
                $result.Method = 'Winget'
                $result.Message = 'Upgraded successfully'
                return $result
            } elseif ($process.ExitCode -eq -1978335189) {
                Write-Status -Message "No update available via Winget for: $($Application.Name)" -Level 'Verbose'
                # Not an error, just no update available
            } else {
                Write-Status -Message "Winget upgrade returned exit code: $($process.ExitCode)" -Level 'Verbose'
            }
        } catch {
            Write-Status -Message "Winget upgrade error: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Try Chocolatey upgrade
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

    # No upgrade method succeeded or available
    $result.Message = 'No update available or upgrade not supported'
    return $result
}

# === SEQUENTIAL INSTALLATION ===

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
            # If ForceUpdate is specified, try to upgrade instead of skipping
            if ($ForceUpdate) {
                Write-Output "[INFO] Checking for updates: $($Application.Name)"
                Write-Status -Message "Checking for updates: $($Application.Name)" -Level 'Info'
                $upgradeResult = Invoke-ApplicationUpgrade -Application $Application
                if ($upgradeResult.Success) {
                    return $upgradeResult
                }
                # If upgrade failed (no update available or error), still return success since app is installed
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

# === PARALLEL INSTALLATION (PowerShell 7+) ===
# Note: This function contains inline logic that duplicates sequential helpers.
# Planned for v3.0.0: Further refactoring to use shared helpers via function export.
# Current implementation works correctly but has higher maintenance overhead.

function Install-ApplicationsParallel {
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
        # Fallback: check winget list (using cache for performance)
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
                # Security: Block path traversal in registry paths
                if ($regPath -match '\.\.') {
                    return $false
                }
                return Test-Path -Path $regPath -ErrorAction SilentlyContinue
            }
            return $false
        }
        'File' {
            if (-not ($App.Detection.PSObject.Properties['Path'] -and $App.Detection.Path)) { return $false }
            $rawPath = $App.Detection.Path
            # Security: Block path traversal attempts in input
            if ($rawPath -match '\.\.' -or $rawPath -match '[\\/]\.\.[\\/]?' -or $rawPath -match '^\.\.') {
                return $false
            }
            # Expand environment variables in path
            $expandedPath = [Environment]::ExpandEnvironmentVariables($rawPath)
            # Security: Block path traversal after expansion
            if ($expandedPath -match '\.\.' -or $expandedPath -match '[\\/]\.\.[\\/]?' -or $expandedPath -match '^\.\.') {
                return $false
            }
            # Security: Ensure path is absolute
            if ($expandedPath -notmatch '^[A-Za-z]:[\\/]') {
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
                # Security: Only allow whitelisted executables for command detection
                $allowedExes = @('java','java.exe','javac','javac.exe','dotnet','dotnet.exe','python','python.exe','python3','python3.exe','node','node.exe','npm','npm.cmd','git','git.exe','docker','docker.exe','rustc','rustc.exe','cargo','cargo.exe','go','go.exe','ruby','ruby.exe','php','php.exe','perl','perl.exe')
                $exeBaseName = [System.IO.Path]::GetFileName($exe).ToLower()
                if ($exeBaseName -notin $allowedExes) { return $false }
                if (-not (Get-Command -Name $exe -ErrorAction SilentlyContinue)) { return $false }
                # Check if we need to verify output content (Arguments field contains expected pattern)
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
                    # Use cached winget list for performance
                    $list = Get-CachedWingetList
                    if ($App.Sources.Store -and $list -match [regex]::Escape($App.Sources.Store) -and $list -notmatch "No installed package") { return $true }
                    if ($App.Detection.PackageName -and $list -match [regex]::Escape($App.Detection.PackageName)) { return $true }
                } catch { }
            }
            return $false
        }
        default {
            if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                # Use cached winget list for performance
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

    # Create parallel logs directory with thread-safe creation and retention policy
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
            Start-Sleep -Milliseconds (100 * $retryCount)  # Exponential backoff
        }
    }

    # Cleanup old logs (retention: 7 days)
    try {
        $cutoffDate = (Get-Date).AddDays(-7)
        Get-ChildItem -Path $parallelLogsDir -Filter "parallel_*.log" -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt $cutoffDate } |
            Remove-Item -Force -ErrorAction SilentlyContinue
    } catch {
        # Non-critical error, continue execution
        Write-Host (Get-LocalizedString -Key 'parallel.logs_cleanup_failed' -Parameters @{ Error = $_ }) -ForegroundColor Yellow
    }

    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $parallelTimeoutMs = $script:ParallelInstallTimeoutMs  # Pass timeout to parallel scope

    $installResults = $appsToInstall | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $app = $_
        $force = $using:forceInstall
        $repRoot = $using:repoRoot
        $parallelLogDir = $using:parallelLogsDir
        $ts = $using:timestamp
        $validateUrl = $using:validateUrlFunction
        $detectAppFunc = $using:detectAppFunction
        $installTimeoutMs = $using:parallelTimeoutMs

        # Recreate helper functions in parallel scope using safe ScriptBlock creation
        ${function:Test-ValidDownloadUrl} = [ScriptBlock]::Create($validateUrl)
        # Use dot-sourcing with ScriptBlock instead of Invoke-Expression for security
        . ([ScriptBlock]::Create($detectAppFunc))

        # Create app-specific log file
        $appLogFile = Join-Path $parallelLogDir "parallel_${ts}_$($app.Name -replace '[^\w\-]', '_').log"

        # Helper function to log to file
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
            # Use exported detection function (replaces ~150 lines of duplicated code)
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

            # === INSTALLATION ===
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
                $transientErrors = @(-1978335189, -1978335212)  # Common Winget network errors

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
                        # APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_ALREADY_INSTALLED
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
                $transientErrors = @(1641, 3010, -1)  # Reboot required, network timeout

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
                # Check for Windows Sandbox - Store is unavailable
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
                # Validate URL first
                if (-not (Test-ValidDownloadUrl -Url $sources.DirectUrl)) {
                    Write-ParallelLog "Invalid or insecure URL: $($sources.DirectUrl)" 'Error'
                    $result.Message = 'Invalid DirectUrl'
                    return $result
                }

                Write-ParallelLog "Attempting direct download installation: $($sources.DirectUrl)" 'Info'

                try {
                    # Detect file type from URL
                    $filename = [System.IO.Path]::GetFileName($sources.DirectUrl)
                    if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.[a-z]{3,4}$') {
                        $filename = "installer_$([guid]::NewGuid().ToString('N')).exe"
                    }

                    $tempDir = Join-Path $env:TEMP "Win11Forge_$([guid]::NewGuid().ToString('N'))"
                    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
                    $tempFile = Join-Path $tempDir $filename

                    Write-ParallelLog "Downloading to: $tempFile" 'Verbose'

                    # Ensure TLS 1.2 is enabled
                    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

                    $downloadSuccess = $false

                    # Method 1: WebClient (fast)
                    try {
                        $webClient = New-Object System.Net.WebClient
                        $webClient.Headers.Add("User-Agent", "Win11Forge/3.0.0 (Windows NT; PowerShell)")
                        $downloadTask = $webClient.DownloadFileTaskAsync($sources.DirectUrl, $tempFile)
                        $downloadTask.Wait()
                        $webClient.Dispose()
                        if ((Test-Path -Path $tempFile) -and (Get-Item -Path $tempFile).Length -gt 0) { $downloadSuccess = $true }
                    } catch {
                        Write-ParallelLog "WebClient failed: $($_.Exception.Message)" 'Verbose'
                        if ($webClient) { $webClient.Dispose() }
                        if (Test-Path -Path $tempFile) { Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue }
                    }

                    # Method 2: Invoke-WebRequest (handles redirects)
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

                    # SHA256 checksum validation (if provided)
                    if ($sources.SHA256) {
                        Write-ParallelLog "Validating SHA256 checksum..." 'Info'
                        $fileHash = (Get-FileHash -Path $tempFile -Algorithm SHA256).Hash
                        if ($fileHash -ne $sources.SHA256) {
                            Write-ParallelLog "Checksum FAILED! Expected: $($sources.SHA256), Got: $fileHash" 'Error'
                            Write-ParallelLog "Removing potentially corrupted file..." 'Warning'
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                            $result.Message = 'SHA256 checksum validation failed'
                            return $result
                        }
                        Write-ParallelLog "Checksum validation passed (SHA256: $fileHash)" 'Success'
                    }

                    # Auto-detect installer type
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
                            Write-ParallelLog "MSI arguments: $($msiArgs -join ' ')" 'Verbose'
                            $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $msiArgs -Wait -NoNewWindow -PassThru
                            $processExitCode = $process.ExitCode
                        }
                        'zip' {
                            Write-ParallelLog "Extracting ZIP archive" 'Info'
                            $extractPath = Join-Path $tempDir "extracted"
                            Expand-Archive -Path $tempFile -DestinationPath $extractPath -Force

                            # Check if ZIP contains an installer (setup.exe/install.exe)
                            $setupExe = Get-ChildItem -Path $extractPath -Filter *.exe -Recurse |
                                Where-Object { $_.Name -match 'setup|install' } |
                                Select-Object -First 1

                            if ($setupExe) {
                                # ZIP contains installer - execute it
                                $zipArgs = if ($app.PSObject.Properties['InstallArguments']) { $app.InstallArguments } else { '/S' }
                                Write-ParallelLog "Executing installer: $($setupExe.FullName) $zipArgs" 'Info'
                                $process = Start-Process -FilePath $setupExe.FullName -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru
                                $processExitCode = $process.ExitCode
                            } else {
                                # ZIP contains portable tools - deploy to destination
                                Write-ParallelLog "No installer found - deploying portable tools" 'Info'

                                # Determine destination from Detection.Path
                                $destinationPath = $null
                                if ($app.Detection -and $app.Detection.Path) {
                                    $destinationPath = Split-Path $app.Detection.Path -Parent
                                }

                                if (-not $destinationPath) {
                                    # Default to Program Files\AppName
                                    $destinationPath = Join-Path ${env:ProgramFiles} $app.Name
                                }

                                Write-ParallelLog "Deploying to: $destinationPath" 'Info'

                                # Create destination directory
                                if (-not (Test-Path $destinationPath)) {
                                    New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
                                }

                                # Copy all files from extracted archive to destination
                                Copy-Item -Path "$extractPath\*" -Destination $destinationPath -Recurse -Force
                                Write-ParallelLog "Deployment completed successfully" 'Success'
                                $processExitCode = 0
                            }
                        }
                        'exe' {
                            $exeArgs = if ($app.PSObject.Properties['InstallArguments']) { $app.InstallArguments } else { '/S' }
                            Write-ParallelLog "EXE arguments: $exeArgs" 'Verbose'
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

        # Final result logging
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
        # Check if Skipped property exists and is true
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

# === BATCH DETECTION (OPTIMIZED) ===

<#
.SYNOPSIS
    Gets all installed applications in a single batch operation for fast detection.

.DESCRIPTION
    Retrieves all installed applications from multiple sources in ONE pass:
    - Registry (Win32 apps from Uninstall keys)
    - Winget list (cached)
    - AppX packages (Store apps)

    This is 10-50x faster than checking apps individually.

.OUTPUTS
    PSCustomObject with:
    - RegistryApps: Hashtable of DisplayName -> PSObject (Version, Publisher, InstallLocation)
    - WingetOutput: Raw winget list output string
    - AppxPackages: Hashtable of PackageName prefix -> PSObject (Version, PackageFullName)

.EXAMPLE
    $cache = Get-InstalledApplicationsCache
    # Then use Test-ApplicationInstalledFast -Application $app -Cache $cache
#>
function Get-InstalledApplicationsCache {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    Write-Verbose "Building installed applications cache..."
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    # 1. Registry: Get all Win32 uninstall entries (one query)
    $registryApps = @{}
    $uninstallPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    foreach ($path in $uninstallPaths) {
        try {
            $entries = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
            foreach ($entry in $entries) {
                if ($entry.DisplayName) {
                    $key = $entry.DisplayName.ToLowerInvariant()
                    if (-not $registryApps.ContainsKey($key)) {
                        $registryApps[$key] = [PSCustomObject]@{
                            DisplayName     = $entry.DisplayName
                            Version         = $entry.DisplayVersion
                            Publisher       = $entry.Publisher
                            InstallLocation = $entry.InstallLocation
                            UninstallString = $entry.UninstallString
                        }
                    }
                }
            }
        } catch {
            Write-Verbose "Error reading registry path $path : $_"
        }
    }

    Write-Verbose "Registry: Found $($registryApps.Count) apps"

    # 2. Winget: Get list output once (using WingetCache module)
    $wingetOutput = ""
    if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
        try {
            $wingetOutput = Get-CachedWingetList
        } catch {
            Write-Verbose "Error running winget list: $_"
        }
    }

    Write-Verbose "Winget: Output from cache ($($wingetOutput.Length) chars)"

    # 3. AppX: Get all Store/MSIX packages (one query)
    $appxPackages = @{}
    try {
        $packages = Get-AppxPackage -ErrorAction SilentlyContinue
        foreach ($pkg in $packages) {
            # Use package name prefix for matching (e.g., "Microsoft.PowerToys" matches "Microsoft.PowerToys.SparseApp")
            $prefix = $pkg.Name -replace '\..*$', ''
            if (-not $appxPackages.ContainsKey($pkg.Name)) {
                $appxPackages[$pkg.Name] = [PSCustomObject]@{
                    Name            = $pkg.Name
                    Version         = $pkg.Version
                    PackageFullName = $pkg.PackageFullName
                    Publisher       = $pkg.Publisher
                }
            }
        }
    } catch {
        Write-Verbose "Error getting AppX packages: $_"
    }

    Write-Verbose "AppX: Found $($appxPackages.Count) packages"

    # 4. Pre-cache common command outputs (for Command detection method)
    $commandOutputs = @{}

    # Cache dotnet runtimes (used by .NET Runtime detections)
    if (Get-Command -Name 'dotnet' -ErrorAction SilentlyContinue) {
        try {
            $commandOutputs['dotnet --list-runtimes'] = & dotnet --list-runtimes 2>&1 | Out-String
            $commandOutputs['dotnet --version'] = & dotnet --version 2>&1 | Out-String
        } catch { }
    }

    # Cache java version (used by Java/JRE/JDK detections)
    if (Get-Command -Name 'java' -ErrorAction SilentlyContinue) {
        try {
            $commandOutputs['java -version'] = & java -version 2>&1 | Out-String
        } catch { }
    }

    # Cache python version
    if (Get-Command -Name 'python' -ErrorAction SilentlyContinue) {
        try {
            $commandOutputs['python --version'] = & python --version 2>&1 | Out-String
        } catch { }
    }

    # Cache node version
    if (Get-Command -Name 'node' -ErrorAction SilentlyContinue) {
        try {
            $commandOutputs['node --version'] = & node --version 2>&1 | Out-String
        } catch { }
    }

    # Cache git version
    if (Get-Command -Name 'git' -ErrorAction SilentlyContinue) {
        try {
            $commandOutputs['git --version'] = & git --version 2>&1 | Out-String
        } catch { }
    }

    Write-Verbose "Commands: Cached $($commandOutputs.Count) command outputs"

    $stopwatch.Stop()
    Write-Verbose "Cache built in $($stopwatch.ElapsedMilliseconds)ms"

    return [PSCustomObject]@{
        RegistryApps    = $registryApps
        WingetOutput    = $wingetOutput
        AppxPackages    = $appxPackages
        CommandOutputs  = $commandOutputs
        CacheBuiltAt    = [DateTime]::Now
    }
}

<#
.SYNOPSIS
    Fast application detection using pre-built cache.

.DESCRIPTION
    Checks if an application is installed using the cached data from Get-InstalledApplicationsCache.
    This is much faster than Test-ApplicationInstalled for batch operations.

.PARAMETER Application
    The application object from the database.

.PARAMETER Cache
    The cache object from Get-InstalledApplicationsCache.

.OUTPUTS
    Boolean indicating if the application is installed.
#>
function Test-ApplicationInstalledFast {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter(Mandatory)]
        [PSCustomObject]$Cache
    )

    $appName = $Application.Name
    $detected = $false
    $version = $null

    # Method 1: Check by detection configuration
    if ($Application.Detection) {
        $method = $Application.Detection.Method

        switch ($method) {
            'Registry' {
                # Check if registry path exists (with null-safe property check)
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $detected = Test-Path -Path $Application.Detection.Path -ErrorAction SilentlyContinue
                    # Try to get version from registry
                    if ($detected) {
                        try {
                            $regEntry = Get-ItemProperty -Path $Application.Detection.Path -ErrorAction SilentlyContinue
                            if ($regEntry.DisplayVersion) { $version = $regEntry.DisplayVersion }
                            elseif ($regEntry.Version) { $version = $regEntry.Version }
                        } catch {}
                    }
                }
            }
            'File' {
                # Check if file exists (with null-safe property check)
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $expandedPath = Expand-DetectionPath -Path $Application.Detection.Path
                    $detected = Test-Path -Path $expandedPath -PathType Leaf -ErrorAction SilentlyContinue
                    # Try to get version from file
                    if ($detected) {
                        try {
                            $fileInfo = Get-Item -Path $expandedPath -ErrorAction SilentlyContinue
                            if ($fileInfo.VersionInfo.FileVersion) {
                                $version = $fileInfo.VersionInfo.FileVersion
                            }
                        } catch {}
                    }
                }
            }
            'StoreApp' {
                # Check in AppX cache by package name prefix
                $packageName = $Application.Detection.PackageName
                if ($packageName) {
                    foreach ($key in $Cache.AppxPackages.Keys) {
                        if ($key -like "$packageName*" -or $key -eq $packageName) {
                            $detected = $true
                            $version = $Cache.AppxPackages[$key].Version
                            break
                        }
                    }
                }
                # Also check winget output for Store ID
                if (-not $detected -and $Application.Sources.Store -and $Cache.WingetOutput) {
                    $detected = $Cache.WingetOutput -match [regex]::Escape($Application.Sources.Store)
                }
            }
            'Command' {
                # Try to use cached command output first (much faster)
                try {
                    $fullCommand = $Application.Detection.Command
                    $commandParts = $fullCommand -split '\s+', 2
                    $executable = $commandParts[0]
                    $expectedPattern = if ($Application.Detection.PSObject.Properties['Arguments']) { $Application.Detection.Arguments } else { $null }

                    # Security: Only allow whitelisted executables for command detection
                    $exeBaseName = [System.IO.Path]::GetFileName($executable).ToLower()
                    if ($exeBaseName -notin $script:AllowedDetectionExecutables) {
                        $detected = $false
                    } else {
                        # Check if this command output is cached
                        $output = $null
                        if ($Cache.CommandOutputs -and $Cache.CommandOutputs.ContainsKey($fullCommand)) {
                            $output = $Cache.CommandOutputs[$fullCommand]
                        }

                        if ($output) {
                            # Use cached output
                            if ($expectedPattern) {
                                $detected = $output -match [regex]::Escape($expectedPattern)
                                if ($detected -and $output -match '(\d+\.\d+[\.\d]*)') {
                                    $version = $matches[1]
                                }
                            } else {
                                $detected = $true
                            }
                        } elseif (Get-Command -Name $executable -ErrorAction SilentlyContinue) {
                            # Fallback: execute command if not cached
                            $arguments = if ($commandParts.Count -gt 1) { $commandParts[1] } else { $null }
                            if ($expectedPattern) {
                                $output = if ($arguments) {
                                    & $executable $arguments 2>&1 | Out-String
                                } else {
                                    & $executable 2>&1 | Out-String
                                }
                                $detected = $output -match [regex]::Escape($expectedPattern)
                                if ($detected -and $output -match '(\d+\.\d+[\.\d]*)') {
                                    $version = $matches[1]
                                }
                            } else {
                                $detected = $true
                            }
                        }
                    }
                } catch {
                    $detected = $false
                }
            }
            'WindowsFeature' {
                $feature = Get-WindowsOptionalFeature -Online -FeatureName $Application.Detection.Feature -ErrorAction SilentlyContinue
                $detected = $feature -and $feature.State -eq 'Enabled'
            }
            'WindowsCapability' {
                $capability = Get-WindowsCapability -Online -Name "*$($Application.Detection.Capability)*" -ErrorAction SilentlyContinue
                $detected = $capability -and $capability.State -eq 'Installed'
            }
        }
    }

    # Method 2: Check by Winget ID in cached output (also extract version)
    if (-not $detected -and $Application.Sources.Winget -and $Cache.WingetOutput) {
        $wingetId = $Application.Sources.Winget
        if ($Cache.WingetOutput -match [regex]::Escape($wingetId)) {
            $detected = $true
            # Try to extract version from winget output line
            $lines = $Cache.WingetOutput -split "`n"
            foreach ($line in $lines) {
                if ($line -match [regex]::Escape($wingetId)) {
                    # Parse version from the line (typically format: Name  Id  Version  Available  Source)
                    if ($line -match '(\d+\.[\d.]+)') {
                        $version = $matches[1]
                    }
                    break
                }
            }
        }
    }

    # Method 3: Check by app name in Registry cache
    if (-not $detected -and $Cache.RegistryApps) {
        $nameLower = $appName.ToLowerInvariant()
        # Exact match
        if ($Cache.RegistryApps.ContainsKey($nameLower)) {
            $detected = $true
            $version = $Cache.RegistryApps[$nameLower].Version
        }
        # Partial match
        if (-not $detected) {
            foreach ($key in $Cache.RegistryApps.Keys) {
                if ($key -like "*$nameLower*" -or $nameLower -like "*$key*") {
                    $detected = $true
                    $version = $Cache.RegistryApps[$key].Version
                    break
                }
            }
        }
    }

    return [PSCustomObject]@{
        IsInstalled = $detected
        Version     = $version
    }
}

<#
.SYNOPSIS
    Batch detection of multiple applications using optimized cache.

.DESCRIPTION
    Detects installation status and version for multiple applications in a single optimized operation.
    Returns a hashtable mapping AppId to status object with IsInstalled and Version.

.PARAMETER Applications
    Array of application objects from the database.

.OUTPUTS
    Hashtable mapping AppId -> PSCustomObject with IsInstalled (bool) and Version (string)
#>
function Get-ApplicationsInstallationStatus {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications
    )

    Write-Host "Scanning $($Applications.Count) applications..." -ForegroundColor Cyan

    # Build cache once
    $cache = Get-InstalledApplicationsCache

    # Check all apps using cache
    $results = @{}
    $installedCount = 0

    foreach ($app in $Applications) {
        $status = Test-ApplicationInstalledFast -Application $app -Cache $cache
        $results[$app.AppId] = @{
            IsInstalled = $status.IsInstalled
            Version     = $status.Version
        }
        if ($status.IsInstalled) { $installedCount++ }
    }

    Write-Host "Scan complete: $installedCount/$($Applications.Count) installed" -ForegroundColor Green

    return $results
}

# === MODULE EXPORTS ===
# Note: Many functions are imported from submodules (ApplicationDetection.psm1, InstallationMethods.psm1)
# but are also defined locally for backward compatibility with parallel installation runspaces.
# The module re-exports all functions to maintain the original public API.

Export-ModuleMember -Function @(
    # Rollback & Deployment State (local)
    'Initialize-RollbackSession',
    'Save-RollbackState',
    'Add-RollbackEntry',
    'Invoke-Rollback',
    'Clear-RollbackState',
    'Get-RollbackState',
    'Initialize-DeploymentSession',
    'Save-DeploymentState',
    'Update-DeploymentProgress',
    'Test-ValidStateData',
    'Get-DeploymentState',
    'Test-IncompleteDeployment',
    'Resume-Deployment',
    'Clear-DeploymentState',
    # Detection functions (from ApplicationDetection.psm1 + local)
    'Test-ApplicationInstalled',
    'Get-InstalledApplicationsCache',
    'Test-ApplicationInstalledFast',
    'Get-ApplicationsInstallationStatus',
    'Test-ApplicationByName',
    'Wait-ForOfficeInstallation',
    # Environment helper (local)
    'Test-EnvironmentRestriction',
    # Individual installation methods (from InstallationMethods.psm1 + local)
    'Install-ViaWinget',
    'Install-ViaChocolatey',
    'Install-ViaStore',
    'Install-ViaDirectDownload',
    'Install-WindowsFeature',
    'Install-WindowsCapability',
    # Orchestration helpers (local + from InstallationMethods.psm1)
    'Invoke-CustomInstallMethod',
    'Invoke-InstallationMethodSequence',
    'Get-ApplicationSources',
    'Invoke-ApplicationUpgrade',
    # Main installation functions (local)
    'Install-Application',
    'Install-ApplicationsParallel'
)
