<#
.SYNOPSIS
    Win11Forge - Directory and Path Constants v3.7.2

.DESCRIPTION
    Centralized constants for all directory paths, registry paths, and
    file locations used across the framework. Eliminates hardcoding
    and ensures consistency.

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

# === WIN11FORGE DATA DIRECTORIES ===
$script:Win11ForgeDataDir = Join-Path $env:LOCALAPPDATA 'Win11Forge'
$script:Win11ForgeLogsDir = Join-Path $script:Win11ForgeDataDir 'Logs'
$script:Win11ForgeJsonLogsDir = Join-Path $script:Win11ForgeLogsDir 'json'
$script:Win11ForgeCacheDir = Join-Path $script:Win11ForgeDataDir 'Cache'
$script:Win11ForgeBackupsDir = Join-Path $script:Win11ForgeDataDir 'Backups'
$script:Win11ForgeProfilesDir = Join-Path $script:Win11ForgeDataDir 'UserProfiles'
$script:Win11ForgeTelemetryDir = Join-Path $script:Win11ForgeDataDir 'Telemetry'
$script:Win11ForgePluginsDir = Join-Path $script:RepositoryRoot 'Plugins'

# === WINDOWS SYSTEM DIRECTORIES ===
$script:WindowsDir = $env:SystemRoot
$script:System32Dir = Join-Path $script:WindowsDir 'System32'
$script:ProgramFilesDir = $env:ProgramFiles
$script:ProgramFilesX86Dir = ${env:ProgramFiles(x86)}
$script:CommonProgramFilesDir = $env:CommonProgramFiles
$script:UserProfileDir = $env:USERPROFILE
$script:AppDataDir = $env:APPDATA
$script:LocalAppDataDir = $env:LOCALAPPDATA
$script:TempDir = $env:TEMP
$script:PublicDir = $env:PUBLIC

# === USER SHELL FOLDERS ===
$script:DesktopDir = [Environment]::GetFolderPath('Desktop')
$script:DocumentsDir = [Environment]::GetFolderPath('MyDocuments')
$script:DownloadsDir = Join-Path $script:UserProfileDir 'Downloads'
$script:StartMenuDir = [Environment]::GetFolderPath('StartMenu')
$script:StartMenuProgramsDir = [Environment]::GetFolderPath('Programs')
$script:StartupDir = [Environment]::GetFolderPath('Startup')
$script:CommonStartMenuDir = [Environment]::GetFolderPath('CommonStartMenu')
$script:CommonProgramsDir = [Environment]::GetFolderPath('CommonPrograms')
$script:CommonStartupDir = [Environment]::GetFolderPath('CommonStartup')
$script:CommonDesktopDir = [Environment]::GetFolderPath('CommonDesktopDirectory')
$script:DefaultUserProfileDir = Join-Path $env:SystemDrive 'Users\Default'
$script:StartMenuBinaryDir = Join-Path $script:System32Dir 'StartMenuExperienceHost'

# === REGISTRY PATHS ===
$script:RegistryPaths = @{
    # Startup locations
    CurrentUserRun              = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    CurrentUserRunOnce          = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
    LocalMachineRun             = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
    LocalMachineRunOnce         = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce'

    # Uninstall locations
    UninstallX64                = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
    UninstallX86                = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    UninstallCurrentUser        = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'

    # Explorer settings
    ExplorerAdvanced            = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    ExplorerCabinetState        = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState'
    ExplorerRibbon              = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Ribbon'

    # Taskbar settings
    TaskbarSearch               = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'
    TaskbarFeeds                = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Feeds'
    TaskbarPolicies             = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer'

    # Privacy settings
    Privacy                     = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy'
    ContentDeliveryManager      = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'

    # System policies
    WindowsUpdate               = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
    WindowsUpdateAU             = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'

    # Network settings
    NetworkProfiles             = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Profiles'
    Tcpip                       = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'
    TcpipInterfaces             = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'

    # Power settings
    PowerSettings               = 'HKLM:\SYSTEM\CurrentControlSet\Control\Power'
    PowerPolicies               = 'HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerSettings'

    # Version detection
    WindowsNTVersion            = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'

    # Container / virtualization
    ContainerManager            = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages'

    # Office detection
    OfficeClickToRun            = 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
    OfficeInstallRoot           = 'HKLM:\SOFTWARE\Microsoft\Office'

    # Frameworks
    DotNetFramework             = 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'
    VCRedistX64                 = 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64'
    VCRedistX86                 = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X86'
}

# === CONFIGURATION FILE PATHS ===
$script:ConfigPaths = @{
    Version                     = Join-Path $script:RepositoryRoot 'Config\version.json'
    ApiSettings                 = Join-Path $script:RepositoryRoot 'Config\api-settings.json'
    PluginsSettings             = Join-Path $script:RepositoryRoot 'Config\plugins-settings.json'
    LoggingSettings             = Join-Path $script:RepositoryRoot 'Config\logging-settings.json'
    RollbackSettings            = Join-Path $script:RepositoryRoot 'Config\rollback-settings.json'
    DownloadSources             = Join-Path $script:RepositoryRoot 'Config\download-sources.json'
    GlobalOptimizations         = Join-Path $script:RepositoryRoot 'Config\global-optimizations.json'
    SystemSettings              = Join-Path $script:RepositoryRoot 'Config\SystemSettings.json'
    StartupBlacklist            = Join-Path $script:RepositoryRoot 'Config\startup-blacklist.json'
    FeatureFlags                = Join-Path $script:RepositoryRoot 'Config\feature-flags.json'
    LocaleEn                    = Join-Path $script:RepositoryRoot 'Config\Locales\en.json'
    LocaleFr                    = Join-Path $script:RepositoryRoot 'Config\Locales\fr.json'
}

# === STATE FILE PATHS ===
$script:StatePaths = @{
    RollbackState               = Join-Path $script:Win11ForgeDataDir 'RollbackState.json'
    DeploymentState             = Join-Path $script:Win11ForgeDataDir 'DeploymentState.json'
    WingetCache                 = Join-Path $script:Win11ForgeDataDir 'WingetCache.json'
    TelemetryData               = Join-Path $script:Win11ForgeTelemetryDir 'telemetry.json'
    UserSettings                = Join-Path $script:Win11ForgeDataDir 'settings.json'
    DeploymentHistory           = Join-Path $script:Win11ForgeDataDir 'deployment-history.json'
    SecureStorage               = Join-Path $script:Win11ForgeDataDir 'secure-storage.dpapi'
    ApiKeys                     = Join-Path $script:Win11ForgeDataDir 'api-keys.secure'
    Entropy                     = Join-Path $script:Win11ForgeDataDir 'entropy.bin'
}

# === DATABASE PATHS ===
$script:DatabasePaths = @{
    Applications                = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
    Profiles                    = Join-Path $script:RepositoryRoot 'Profiles'
}

# === TIMEOUTS (in seconds) ===
$script:Timeouts = @{
    DefaultInstall              = 1800   # 30 minutes
    OfficeInstall               = 2700   # 45 minutes
    ParallelInstall             = 600    # 10 minutes
    Download                    = 300    # 5 minutes
    ProcessStart                = 60     # 1 minute
    ApiRequest                  = 30     # 30 seconds
    OfficeDetection             = 120    # 2 minutes
}

# === PARALLEL EXECUTION LIMITS ===
$script:ParallelLimits = @{
    MaxInstallJobs              = 5
    MaxScanJobs                 = 8
    JobCheckIntervalSeconds     = 2
    MaxRetryAttempts            = 3
}

# === NETWORK DEFAULTS ===
$script:NetworkDefaults = @{
    ConnectivityTestHost        = '8.8.8.8'
    ConnectivityTestCount       = 1
}

# === EXIT CODES ===
$script:ExitCodes = @{
    Winget = @{
        Success                     = 0
        PackageAlreadyInstalled     = -1978334974   # 0x8A150102
        InstallerHashMismatch       = -1978335215   # 0x8A150011
        UpdateNotApplicable         = -1978335189   # 0x8A15002B
        NoApplicableInstaller       = -1978335183   # 0x8A150031
        PackageInUse                = -1978335178   # 0x8A150036
    }
    Chocolatey = @{
        Success                     = 0
        Reboot                      = 3010
        RebootInitiated             = 1641
    }
    General = @{
        Success                     = 0
        GeneralError                = 1
        AccessDenied                = 5
        RebootRequired              = 3010
    }
}

# === PUBLIC FUNCTIONS ===

function Get-Win11ForgeDirectory {
    <#
    .SYNOPSIS
        Returns Win11Forge data directory paths.
    .DESCRIPTION
        Resolves and returns the absolute path for a specified Win11Forge data directory type
        (e.g., Logs, Cache, Backups). Creates the directory automatically if it does not exist.
    .PARAMETER DirectoryType
        Type of directory to return.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Data', 'Logs', 'JsonLogs', 'Cache', 'Backups', 'Profiles', 'Telemetry', 'Plugins')]
        [string]$DirectoryType
    )

    $path = switch ($DirectoryType) {
        'Data'      { $script:Win11ForgeDataDir }
        'Logs'      { $script:Win11ForgeLogsDir }
        'JsonLogs'  { $script:Win11ForgeJsonLogsDir }
        'Cache'     { $script:Win11ForgeCacheDir }
        'Backups'   { $script:Win11ForgeBackupsDir }
        'Profiles'  { $script:Win11ForgeProfilesDir }
        'Telemetry' { $script:Win11ForgeTelemetryDir }
        'Plugins'   { $script:Win11ForgePluginsDir }
    }

    # Ensure directory exists
    if (-not (Test-Path $path)) {
        New-Item -Path $path -ItemType Directory -Force | Out-Null
    }

    return $path
}

function Get-RegistryPath {
    <#
    .SYNOPSIS
        Returns a registry path by key name.
    .DESCRIPTION
        Looks up a Windows registry path from the centralized registry paths dictionary using a
        logical key name. Throws an ArgumentException if the key is not recognized.
    .PARAMETER PathKey
        The registry path key name.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PathKey
    )

    if ($script:RegistryPaths.ContainsKey($PathKey)) {
        return $script:RegistryPaths[$PathKey]
    }

    throw [System.ArgumentException]::new("Unknown registry path key: $PathKey")
}

function Get-AllRegistryPaths {
    <#
    .SYNOPSIS
        Returns all registry paths as a hashtable.
    .DESCRIPTION
        Returns a cloned copy of the complete registry paths dictionary, allowing callers to
        enumerate all known registry locations without modifying the module's internal state.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    return $script:RegistryPaths.Clone()
}

function Get-ConfigPath {
    <#
    .SYNOPSIS
        Returns a configuration file path by key name.
    .DESCRIPTION
        Looks up a configuration file path from the centralized config paths dictionary using a
        logical key name. Throws an ArgumentException if the key is not recognized.
    .PARAMETER PathKey
        The configuration path key name.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PathKey
    )

    if ($script:ConfigPaths.ContainsKey($PathKey)) {
        return $script:ConfigPaths[$PathKey]
    }

    throw [System.ArgumentException]::new("Unknown config path key: $PathKey")
}

function Get-StatePath {
    <#
    .SYNOPSIS
        Returns a state file path by key name.
    .DESCRIPTION
        Looks up a runtime state file path from the centralized state paths dictionary using a
        logical key name. Throws an ArgumentException if the key is not recognized.
    .PARAMETER PathKey
        The state path key name.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PathKey
    )

    if ($script:StatePaths.ContainsKey($PathKey)) {
        return $script:StatePaths[$PathKey]
    }

    throw [System.ArgumentException]::new("Unknown state path key: $PathKey")
}

function Get-Timeout {
    <#
    .SYNOPSIS
        Returns a timeout value by key name.
    .DESCRIPTION
        Looks up a timeout value in seconds from the centralized timeouts dictionary using a
        logical key name. Throws an ArgumentException if the key is not recognized.
    .PARAMETER TimeoutKey
        The timeout key name.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [string]$TimeoutKey
    )

    if ($script:Timeouts.ContainsKey($TimeoutKey)) {
        return $script:Timeouts[$TimeoutKey]
    }

    throw [System.ArgumentException]::new("Unknown timeout key: $TimeoutKey")
}

function Get-ParallelLimit {
    <#
    .SYNOPSIS
        Returns a parallel execution limit by key name.
    .DESCRIPTION
        Looks up a parallel execution constraint (e.g., max jobs, retry attempts) from the centralized
        parallel limits dictionary. Throws an ArgumentException if the key is not recognized.
    .PARAMETER LimitKey
        The limit key name.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [string]$LimitKey
    )

    if ($script:ParallelLimits.ContainsKey($LimitKey)) {
        return $script:ParallelLimits[$LimitKey]
    }

    throw [System.ArgumentException]::new("Unknown parallel limit key: $LimitKey")
}

function Get-NetworkDefault {
    <#
    .SYNOPSIS
        Returns a network default setting by key name.
    .DESCRIPTION
        Looks up a network-related default value (e.g., connectivity test host, ping count) from the
        centralized network defaults dictionary. Throws an ArgumentException if the key is not recognized.
    .PARAMETER SettingKey
        The network setting key name.
    #>
    [CmdletBinding()]
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]
        [string]$SettingKey
    )

    if ($script:NetworkDefaults.ContainsKey($SettingKey)) {
        return $script:NetworkDefaults[$SettingKey]
    }

    throw [System.ArgumentException]::new("Unknown network default key: $SettingKey")
}

function Get-ShellFolder {
    <#
    .SYNOPSIS
        Returns a Windows shell folder path.
    .DESCRIPTION
        Resolves and returns the absolute path for a Windows shell folder such as Desktop, Documents,
        Downloads, Start Menu, or Startup directories for the current user or all users.
    .PARAMETER FolderType
        Type of shell folder to return.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Desktop', 'Documents', 'Downloads', 'StartMenu', 'StartMenuPrograms',
                     'Startup', 'CommonStartMenu', 'CommonPrograms', 'CommonStartup', 'CommonDesktop',
                     'Temp', 'DefaultUserProfile', 'StartMenuBinary')]
        [string]$FolderType
    )

    $path = switch ($FolderType) {
        'Desktop'            { $script:DesktopDir }
        'Documents'          { $script:DocumentsDir }
        'Downloads'          { $script:DownloadsDir }
        'StartMenu'          { $script:StartMenuDir }
        'StartMenuPrograms'  { $script:StartMenuProgramsDir }
        'Startup'            { $script:StartupDir }
        'CommonStartMenu'    { $script:CommonStartMenuDir }
        'CommonPrograms'     { $script:CommonProgramsDir }
        'CommonStartup'      { $script:CommonStartupDir }
        'CommonDesktop'      { $script:CommonDesktopDir }
        'Temp'               { $script:TempDir }
        'DefaultUserProfile' { $script:DefaultUserProfileDir }
        'StartMenuBinary'    { $script:StartMenuBinaryDir }
    }

    return $path
}

function Get-ExitCodes {
    <#
    .SYNOPSIS
        Returns exit code constants for package managers and general operations.
    .DESCRIPTION
        Returns a hashtable of known exit codes for Winget, Chocolatey, or general operations.
        When a Manager filter is specified, returns only that manager's exit codes; otherwise returns all.
    .PARAMETER Manager
        Optional package manager filter (Winget, Chocolatey, General).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [ValidateSet('Winget', 'Chocolatey', 'General')]
        [string]$Manager
    )

    if ($Manager) {
        return $script:ExitCodes[$Manager]
    }

    return $script:ExitCodes
}

function Get-RepositoryRoot {
    <#
    .SYNOPSIS
        Returns the Win11Forge repository root path.
    .DESCRIPTION
        Returns the absolute path to the Win11Forge repository root directory, computed at module
        load time from the location of this module file.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:RepositoryRoot
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Get-Win11ForgeDirectory',
    'Get-RegistryPath',
    'Get-AllRegistryPaths',
    'Get-ConfigPath',
    'Get-StatePath',
    'Get-Timeout',
    'Get-ParallelLimit',
    'Get-NetworkDefault',
    'Get-ShellFolder',
    'Get-ExitCodes',
    'Get-RepositoryRoot'
)
