<#
.SYNOPSIS
    Win11Forge - Directory and Path Constants Module v3.1.4

.DESCRIPTION
    Centralized constants for all directory paths, registry paths, and
    file locations used across the framework. Eliminates hardcoding
    and ensures consistency.

.NOTES
    Author: Julien Bombled
    Version: 3.1.4
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
}

# === PARALLEL EXECUTION LIMITS ===
$script:ParallelLimits = @{
    MaxInstallJobs              = 5
    MaxScanJobs                 = 8
    JobCheckIntervalSeconds     = 2
    MaxRetryAttempts            = 3
}

# === ALLOWED EXECUTABLES FOR DETECTION ===
$script:AllowedDetectionExecutables = @(
    'java', 'java.exe',
    'javac', 'javac.exe',
    'dotnet', 'dotnet.exe',
    'python', 'python.exe',
    'python3', 'python3.exe',
    'node', 'node.exe',
    'npm', 'npm.cmd',
    'git', 'git.exe',
    'docker', 'docker.exe',
    'rustc', 'rustc.exe',
    'cargo', 'cargo.exe',
    'go', 'go.exe',
    'ruby', 'ruby.exe',
    'php', 'php.exe',
    'perl', 'perl.exe'
)

# === PUBLIC FUNCTIONS ===

function Get-Win11ForgeDirectory {
    <#
    .SYNOPSIS
        Returns Win11Forge data directory paths.
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

function Get-AllowedDetectionExecutables {
    <#
    .SYNOPSIS
        Returns the list of allowed executables for detection.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param()

    return $script:AllowedDetectionExecutables
}

function Get-ShellFolder {
    <#
    .SYNOPSIS
        Returns a Windows shell folder path.
    .PARAMETER FolderType
        Type of shell folder to return.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Desktop', 'Documents', 'Downloads', 'StartMenu', 'StartMenuPrograms',
                     'Startup', 'CommonStartMenu', 'CommonPrograms', 'CommonStartup', 'CommonDesktop')]
        [string]$FolderType
    )

    $path = switch ($FolderType) {
        'Desktop'           { $script:DesktopDir }
        'Documents'         { $script:DocumentsDir }
        'Downloads'         { $script:DownloadsDir }
        'StartMenu'         { $script:StartMenuDir }
        'StartMenuPrograms' { $script:StartMenuProgramsDir }
        'Startup'           { $script:StartupDir }
        'CommonStartMenu'   { $script:CommonStartMenuDir }
        'CommonPrograms'    { $script:CommonProgramsDir }
        'CommonStartup'     { $script:CommonStartupDir }
        'CommonDesktop'     { $script:CommonDesktopDir }
    }

    return $path
}

function Get-RepositoryRoot {
    <#
    .SYNOPSIS
        Returns the Win11Forge repository root path.
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
    'Get-AllowedDetectionExecutables',
    'Get-ShellFolder',
    'Get-RepositoryRoot'
)
