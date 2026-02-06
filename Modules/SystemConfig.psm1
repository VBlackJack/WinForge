<#
.SYNOPSIS
    Win11Forge - System Configuration v3.6.8 (Zero Hardcoding)

.DESCRIPTION
    Applies system configuration from profile JSON:
    - Windows Explorer settings
    - Taskbar configuration
    - Network settings (DNS)
    - Privacy settings
    - Performance optimizations
    - Security settings

    All registry paths, names, types, and values are loaded from
    Config/SystemSettings.json - TRUE Zero Hardcoding implementation.

.NOTES
    Author: Julien Bombled
    v3.6.8
    Requires administrator privileges
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
$script:SystemSettingsPath = Join-Path $script:RepositoryRoot 'Config\SystemSettings.json'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module for i18n support
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === CONFIGURATION LOADING ===

# Initialize cached settings variable to avoid StrictMode errors
$script:CachedSystemSettings = $null
$script:SettingsLoaded = $false

function Get-SystemSettings {
    <#
    .SYNOPSIS
        Loads system settings from configuration file.

    .DESCRIPTION
        Reads and caches the SystemSettings.json configuration file.
        Falls back to defaults if the file is missing.
    #>
    [CmdletBinding()]
    param()

    if (-not $script:SettingsLoaded) {
        $script:SettingsLoaded = $true
        if (Test-Path -Path $script:SystemSettingsPath) {
            try {
                $script:CachedSystemSettings = Get-Content -Path $script:SystemSettingsPath -Raw | ConvertFrom-Json
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.loaded_settings') -Level 'Verbose'
            }
            catch {
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.load_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning'
                $script:CachedSystemSettings = $null
            }
        }
        else {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.settings_not_found') -Level 'Warning'
            $script:CachedSystemSettings = $null
        }
    }

    return $script:CachedSystemSettings
}

function Get-RegistryPath {
    <#
    .SYNOPSIS
        Gets a registry path from configuration.

    .PARAMETER PathKey
        The key name in RegistryPaths configuration.

    .PARAMETER FallbackPath
        The fallback path if configuration is not available.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PathKey,

        [Parameter(Mandatory)]
        [string]$FallbackPath
    )

    $settings = Get-SystemSettings
    if ($null -ne $settings -and $null -ne $settings.RegistryPaths) {
        $path = $settings.RegistryPaths.$PathKey
        if ($path) {
            return $path
        }
    }
    return $FallbackPath
}

function Get-RegistryValueDefinition {
    <#
    .SYNOPSIS
        Gets a registry value definition from configuration.

    .PARAMETER Group
        The group name (Explorer, Taskbar, Privacy, etc.).

    .PARAMETER Setting
        The setting key name.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Group,

        [Parameter(Mandatory)]
        [string]$Setting
    )

    $settings = Get-SystemSettings
    if ($null -ne $settings -and $null -ne $settings.RegistryValues) {
        $groupValues = $settings.RegistryValues.$Group
        if ($null -ne $groupValues) {
            return $groupValues.$Setting
        }
    }
    return $null
}

function Get-PowerPlanGuid {
    <#
    .SYNOPSIS
        Gets a power plan GUID from configuration.

    .PARAMETER PlanName
        The power plan name (HighPerformance, Balanced, PowerSaver).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('HighPerformance', 'Balanced', 'PowerSaver')]
        [string]$PlanName
    )

    $settings = Get-SystemSettings
    $fallbackGuids = @{
        'HighPerformance' = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
        'Balanced'        = '381b4222-f694-41f0-9685-ff5bb260df2e'
        'PowerSaver'      = 'a1841308-3541-4fab-bc81-f71556f20b4a'
    }

    if ($null -ne $settings -and $null -ne $settings.PowerPlans) {
        $guid = $settings.PowerPlans.$PlanName
        if ($guid) {
            return $guid
        }
    }
    return $fallbackGuids[$PlanName]
}

function Get-ServicesList {
    <#
    .SYNOPSIS
        Gets services list from configuration.

    .PARAMETER ListType
        The type of services list (ToDisable, ToManual).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('ToDisable', 'ToManual')]
        [string]$ListType
    )

    $settings = Get-SystemSettings

    $fallbackServices = @{
        'ToDisable' = @(
            'MapsBroker', 'lfsvc', 'RetailDemo', 'PhoneSvc', 'Fax',
            'XblAuthManager', 'XblGameSave', 'XboxNetApiSvc', 'XboxGipSvc',
            'WalletService', 'WMPNetworkSvc'
        )
        'ToManual' = @(
            'WSearch', 'SysMain', 'TrkWks', 'WbioSrvc'
        )
    }

    if ($null -ne $settings -and $null -ne $settings.Services) {
        $services = $settings.Services.$ListType
        if ($services) {
            return @($services)
        }
    }
    return $fallbackServices[$ListType]
}

function Get-TimeoutValue {
    <#
    .SYNOPSIS
        Gets a timeout value from configuration.

    .PARAMETER TimeoutKey
        The timeout key name.

    .PARAMETER FallbackValue
        The fallback value if not configured.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TimeoutKey,

        [Parameter(Mandatory)]
        [int]$FallbackValue
    )

    $settings = Get-SystemSettings
    if ($null -ne $settings -and $null -ne $settings.Timeouts) {
        $value = $settings.Timeouts.$TimeoutKey
        if ($null -ne $value) {
            return [int]$value
        }
    }
    return $FallbackValue
}

# === REGISTRY HELPERS ===

function Set-RegistryValue {
    <#
    .SYNOPSIS
        Sets a registry value with error handling.

    .PARAMETER Path
        Registry path

    .PARAMETER Name
        Value name

    .PARAMETER Value
        Value data

    .PARAMETER Type
        Value type
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        $Value,

        [Parameter()]
        [ValidateSet('String', 'DWord', 'QWord', 'Binary', 'MultiString', 'ExpandString')]
        [string]$Type = 'DWord'
    )

    try {
        # Ensure path exists
        if (-not (Test-Path -Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }

        # Use New-ItemProperty with -Force to create or overwrite the value
        New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType $Type -Force | Out-Null
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.set_value' -Parameters @{ Path = $Path; Name = $Name; Value = $Value }) -Level 'Verbose'
        return $true
    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.set_failed' -Parameters @{ Path = $Path; Name = $Name; Error = $_.Exception.Message }) -Level 'Verbose'
        return $false
    }
}

function Set-RegistrySettingFromConfig {
    <#
    .SYNOPSIS
        Sets a registry value using configuration from SystemSettings.json.

    .DESCRIPTION
        Looks up the setting definition in the configuration file and applies
        the appropriate registry value based on the Enable parameter.

    .PARAMETER Group
        The configuration group (Explorer, Taskbar, Privacy, etc.).

    .PARAMETER Setting
        The setting key name within the group.

    .PARAMETER Enable
        Whether to enable ($true) or disable ($false) the setting.

    .PARAMETER ValueKey
        Optional specific value key to use (e.g., 'ValueOptimized', 'ValueLeft').
        If not specified, uses ValueEnabled/ValueDisabled based on Enable parameter.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Group,

        [Parameter(Mandatory)]
        [string]$Setting,

        [Parameter()]
        [bool]$Enable = $true,

        [Parameter()]
        [string]$ValueKey
    )

    $definition = Get-RegistryValueDefinition -Group $Group -Setting $Setting
    if ($null -eq $definition) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.no_config_found' -Parameters @{ Group = $Group; Setting = $Setting }) -Level 'Warning'
        return $false
    }

    # Get the registry path from the definition's Path property
    $pathKey = $definition.Path
    if (-not $pathKey) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.no_path_defined' -Parameters @{ Group = $Group; Setting = $Setting }) -Level 'Warning'
        return $false
    }

    $registryPath = Get-RegistryPath -PathKey $pathKey -FallbackPath ''
    if (-not $registryPath) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.path_not_found' -Parameters @{ Path = $pathKey }) -Level 'Warning'
        return $false
    }

    # Determine the value to set
    $value = $null
    if ($ValueKey) {
        # Use specific value key (e.g., ValueOptimized, ValueLeft, ValueCenter)
        $value = $definition.$ValueKey
    }
    else {
        # Use ValueEnabled or ValueDisabled based on Enable parameter
        $value = if ($Enable) { $definition.ValueEnabled } else { $definition.ValueDisabled }
    }

    if ($null -eq $value) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.no_value_found' -Parameters @{ Group = $Group; Setting = $Setting; Enable = $Enable; ValueKey = $ValueKey }) -Level 'Warning'
        return $false
    }

    # Get name and type from definition
    $name = $definition.Name
    $type = $definition.Type
    if (-not $type) { $type = 'DWord' }

    return Set-RegistryValue -Path $registryPath -Name $name -Value $value -Type $type
}

# === EXPLORER CONFIGURATION ===

function Set-ExplorerConfiguration {
    <#
    .SYNOPSIS
        Configures Windows Explorer settings.

    .PARAMETER Config
        Explorer configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.explorer.configuring') -Level 'Info'

    # Show hidden files
    if ($Config.ContainsKey('ShowHiddenFiles') -and $Config.ShowHiddenFiles) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'Hidden' -Enable $true | Out-Null
    }

    # Show file extensions
    if ($Config.ContainsKey('ShowFileExtensions') -and $Config.ShowFileExtensions) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'HideFileExt' -Enable $true | Out-Null
    }

    # Navigation pane optimization - Show all folders
    if ($Config.ContainsKey('NavigationPaneOptimized') -and $Config.NavigationPaneOptimized) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'NavPaneShowAllFolders' -Enable $true | Out-Null
    }

    # Show Libraries in navigation pane
    if ($Config.ContainsKey('ShowLibraries') -and $Config.ShowLibraries) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'IsPinnedToNameSpaceTree' -Enable $true | Out-Null
    }

    # Expand to open folder (navigation pane follows current folder)
    if ($Config.ContainsKey('ExpandToOpenFolder') -and $Config.ExpandToOpenFolder) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'NavPaneExpandToCurrentFolder' -Enable $true | Out-Null
    }

    # Show sync provider notifications (availability status)
    if ($Config.ContainsKey('ShowSyncProviderNotifications') -and $Config.ShowSyncProviderNotifications) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'ShowSyncProviderNotifications' -Enable $true | Out-Null
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'ShowStatusColumn' -Enable $true | Out-Null
    }

    # Show full path in title bar
    if ($Config.ContainsKey('ShowFullPathInTitleBar') -and $Config.ShowFullPathInTitleBar) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'FullPath' -Enable $true | Out-Null
    }

    # Launch folder windows in separate process
    if ($Config.ContainsKey('LaunchFolderWindowsInSeparateProcess') -and $Config.LaunchFolderWindowsInSeparateProcess) {
        Set-RegistrySettingFromConfig -Group 'Explorer' -Setting 'SeparateProcess' -Enable $true | Out-Null
    }

    # Developer mode (uses Security group)
    if ($Config.ContainsKey('DeveloperMode') -and $Config.DeveloperMode) {
        Set-RegistrySettingFromConfig -Group 'Security' -Setting 'AllowDevelopmentWithoutDevLicense' -Enable $true | Out-Null
    }

    # Restart Explorer to apply changes
    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.explorer.restarting') -Level 'Info'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    $restartDelay = Get-TimeoutValue -TimeoutKey 'ExplorerRestartDelaySeconds' -FallbackValue 2
    Start-Sleep -Seconds $restartDelay

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.explorer.applied') -Level 'Success'
}

# === TASKBAR CONFIGURATION ===

function Set-TaskbarConfiguration {
    <#
    .SYNOPSIS
        Configures Windows 11 Taskbar settings.

    .PARAMETER Config
        Taskbar configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.taskbar.configuring') -Level 'Info'

    try {
        # Disable widgets (DisableWidgets = true means we want widgets hidden = ValueDisabled)
        if ($Config.ContainsKey('DisableWidgets') -and $Config.DisableWidgets) {
            Set-RegistrySettingFromConfig -Group 'Taskbar' -Setting 'TaskbarDa' -Enable $false | Out-Null
        }

        # Taskbar alignment (uses special ValueLeft/ValueCenter)
        if ($Config.ContainsKey('StartAlignment') -and $Config.StartAlignment) {
            $valueKey = if ($Config.StartAlignment -eq 'left') { 'ValueLeft' } else { 'ValueCenter' }
            Set-RegistrySettingFromConfig -Group 'Taskbar' -Setting 'TaskbarAl' -ValueKey $valueKey | Out-Null
        }

        # Search box mode (uses special ValueHidden/ValueIconOnly/ValueBox)
        if ($Config.ContainsKey('SearchMode') -and $Config.SearchMode) {
            $valueKey = switch ($Config.SearchMode) {
                'hidden'    { 'ValueHidden' }
                'icon_only' { 'ValueIconOnly' }
                'box'       { 'ValueBox' }
                default     { 'ValueIconOnly' }
            }
            Set-RegistrySettingFromConfig -Group 'Search' -Setting 'SearchboxTaskbarMode' -ValueKey $valueKey | Out-Null
        }

        # Hide Task View button (HideTaskView = true means we want it hidden = ValueDisabled)
        if ($Config.ContainsKey('HideTaskView') -and $Config.HideTaskView) {
            Set-RegistrySettingFromConfig -Group 'Taskbar' -Setting 'ShowTaskViewButton' -Enable $false | Out-Null
        }

        # Hide Chat icon (HideChat = true means we want it hidden = ValueDisabled)
        if ($Config.ContainsKey('HideChat') -and $Config.HideChat) {
            Set-RegistrySettingFromConfig -Group 'Taskbar' -Setting 'TaskbarMn' -Enable $false | Out-Null
        }

        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.taskbar.applied') -Level 'Success'
    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.taskbar.permissions_warning') -Level 'Warning'
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.taskbar.partial_applied') -Level 'Success'
    }
}

# === NETWORK CONFIGURATION ===

function Set-NetworkConfiguration {
    <#
    .SYNOPSIS
        Configures network settings including DNS.

    .PARAMETER Config
        Network configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.configuring') -Level 'Info'

    # Configure DNS servers
    if ($Config.ContainsKey('DnsServers') -and $Config.DnsServers) {
        try {
            # Build DNS array - handle all input types
            $dnsArray = @()

            $dnsInput = $Config.DnsServers
            if ($dnsInput -is [System.Collections.ArrayList] -or
                $dnsInput -is [System.Collections.Generic.List[object]] -or
                $dnsInput -is [object[]]) {
                $dnsArray = @($dnsInput | Where-Object {
                    $_ -and $_ -is [string] -and $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$'
                })
            }
            elseif ($dnsInput -is [string]) {
                if ($dnsInput.Contains(',')) {
                    $dnsArray = @($dnsInput -split ',' | ForEach-Object {
                        $_.Trim()
                    } | Where-Object { $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$' })
                }
                elseif ($dnsInput -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$') {
                    $dnsArray = @($dnsInput)
                }
            }
            else {
                try {
                    $dnsArray = @($dnsInput | ForEach-Object { $_ } | Where-Object {
                        $_ -and $_ -is [string] -and $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$'
                    })
                } catch {
                    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.dns_parse_error' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
                }
            }

            if ($dnsArray.Count -gt 0) {
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.setting_dns' -Parameters @{ Servers = ($dnsArray -join ', ') }) -Level 'Info'

                $adapters = Get-NetAdapter | Where-Object {
                    $_.Status -eq 'Up' -and $_.InterfaceType -ne 'Loopback'
                }

                $successCount = 0
                foreach ($adapter in $adapters) {
                    try {
                        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex `
                            -ServerAddresses $dnsArray -ErrorAction Stop
                        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.dns_configured' -Parameters @{ Adapter = $adapter.Name }) -Level 'Success'
                        $successCount++
                    } catch {
                        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.dns_failed' -Parameters @{ Adapter = $adapter.Name; Error = $_.Exception.Message }) -Level 'Warning'
                    }
                }

                if ($successCount -gt 0) {
                    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.dns_completed' -Parameters @{ Count = $successCount }) -Level 'Success'
                }
            } else {
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.no_valid_dns') -Level 'Warning'
            }
        } catch {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.dns_error' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        }
    }

    # Network optimizations for gaming (uses ValueOptimized)
    if ($Config.ContainsKey('GamingOptimizations') -and $Config.GamingOptimizations) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.gaming_optimizations') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Network' -Setting 'TcpAckFrequency' -ValueKey 'ValueOptimized' | Out-Null
        Set-RegistrySettingFromConfig -Group 'Network' -Setting 'TCPNoDelay' -ValueKey 'ValueOptimized' | Out-Null
    }

    # QoS optimization
    if ($Config.ContainsKey('QoSOptimization') -and $Config.QoSOptimization) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.qos_optimization') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Network' -Setting 'NonBestEffortLimit' -ValueKey 'ValueOptimized' | Out-Null
    }

    # Developer optimizations (same as gaming)
    if ($Config.ContainsKey('DeveloperOptimizations') -and $Config.DeveloperOptimizations) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.developer_optimizations') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Network' -Setting 'TcpAckFrequency' -ValueKey 'ValueOptimized' | Out-Null
        Set-RegistrySettingFromConfig -Group 'Network' -Setting 'TCPNoDelay' -ValueKey 'ValueOptimized' | Out-Null
    }

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.network.applied') -Level 'Success'
}

# === PRIVACY CONFIGURATION ===

function Set-PrivacyConfiguration {
    <#
    .SYNOPSIS
        Configures Windows privacy settings.

    .PARAMETER Config
        Privacy configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.configuring') -Level 'Info'

    $settings = Get-SystemSettings
    $telemetryService = 'DiagTrack'
    if ($null -ne $settings -and $null -ne $settings.Services -and $settings.Services.Telemetry) {
        $telemetryService = $settings.Services.Telemetry
    }

    # Disable telemetry (DisableTelemetry = true means we want telemetry disabled = ValueDisabled)
    if ($Config.ContainsKey('DisableTelemetry') -and $Config.DisableTelemetry) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.disabling_telemetry') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'AllowTelemetry' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'AllowTelemetryUser' -Enable $false | Out-Null

        # Disable telemetry service
        try {
            Set-Service -Name $telemetryService -StartupType Disabled -ErrorAction SilentlyContinue
            Stop-Service -Name $telemetryService -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.telemetry_service_error' -Parameters @{ Service = $telemetryService }) -Level 'Verbose'
        }
    }

    # Minimal data collection
    if ($Config.ContainsKey('MinimalDataCollection') -and $Config.MinimalDataCollection) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.minimal_data') -Level 'Info'

        # Disable advertising ID
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'AdvertisingEnabled' -Enable $false | Out-Null

        # Disable app launch tracking
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'StartTrackProgs' -Enable $false | Out-Null

        # Disable suggested content
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SubscribedContent338393' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SubscribedContent353694' -Enable $false | Out-Null
    }

    # Disable activity history
    if ($Config.ContainsKey('DisableActivityHistory') -and $Config.DisableActivityHistory) {
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'EnableActivityFeed' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'PublishUserActivities' -Enable $false | Out-Null
    }

    # Disable Cortana
    if ($Config.ContainsKey('DisableCortana') -and $Config.DisableCortana) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.disabling_cortana') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'AllowCortana' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Search' -Setting 'CortanaConsent' -Enable $false | Out-Null
    }

    # Disable consumer features (bloatware suggestions)
    # Note: DisableWindowsConsumerFeatures=1 means consumer features ARE disabled
    if ($Config.ContainsKey('DisableConsumerFeatures') -and $Config.DisableConsumerFeatures) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.disabling_consumer') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'DisableWindowsConsumerFeatures' -Enable $true | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SilentInstalledAppsEnabled' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SystemPaneSuggestionsEnabled' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'PreInstalledAppsEnabled' -Enable $false | Out-Null
    }

    # Disable Windows tips and tricks
    if ($Config.ContainsKey('DisableWindowsTips') -and $Config.DisableWindowsTips) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.disabling_tips') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SubscribedContent338389' -Enable $false | Out-Null
        Set-RegistrySettingFromConfig -Group 'Privacy' -Setting 'SoftLandingEnabled' -Enable $false | Out-Null
    }

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.privacy.applied') -Level 'Success'
}

# === PERFORMANCE CONFIGURATION ===

function Set-PerformanceConfiguration {
    <#
    .SYNOPSIS
        Configures Windows performance settings.

    .PARAMETER Config
        Performance configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.configuring') -Level 'Info'

    # Disable visual effects (uses ValueBestPerformance)
    if ($Config.ContainsKey('DisableVisualEffects') -and $Config.DisableVisualEffects) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.disabling_visual_effects') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Performance' -Setting 'VisualFXSetting' -ValueKey 'ValueBestPerformance' | Out-Null
    }

    # Optimize services
    if ($Config.ContainsKey('OptimizeServices') -and $Config.OptimizeServices) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.optimizing_services') -Level 'Info'

        $servicesToDisable = Get-ServicesList -ListType 'ToDisable'
        $servicesToManual = Get-ServicesList -ListType 'ToManual'

        foreach ($service in $servicesToDisable) {
            try {
                $svc = Get-Service -Name $service -ErrorAction SilentlyContinue
                if ($svc -and $svc.StartType -ne 'Disabled') {
                    Set-Service -Name $service -StartupType Disabled -ErrorAction Stop
                    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.service_disabled' -Parameters @{ Service = $service }) -Level 'Verbose'
                }
            }
            catch {
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.service_disable_failed' -Parameters @{ Service = $service }) -Level 'Verbose'
            }
        }

        foreach ($service in $servicesToManual) {
            try {
                $svc = Get-Service -Name $service -ErrorAction SilentlyContinue
                if ($svc -and $svc.StartType -eq 'Automatic') {
                    Set-Service -Name $service -StartupType Manual -ErrorAction Stop
                    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.service_manual' -Parameters @{ Service = $service }) -Level 'Verbose'
                }
            }
            catch {
                Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.service_modify_failed' -Parameters @{ Service = $service }) -Level 'Verbose'
            }
        }
    }

    # Power plan
    if ($Config.ContainsKey('PowerPlan') -and $Config.PowerPlan) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.setting_power_plan' -Parameters @{ Plan = $Config.PowerPlan }) -Level 'Info'

        try {
            $powerPlan = switch ($Config.PowerPlan) {
                'High Performance' { Get-PowerPlanGuid -PlanName 'HighPerformance' }
                'Balanced'         { Get-PowerPlanGuid -PlanName 'Balanced' }
                'Power Saver'      { Get-PowerPlanGuid -PlanName 'PowerSaver' }
                default            { Get-PowerPlanGuid -PlanName 'Balanced' }
            }

            & powercfg.exe /setactive $powerPlan
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.power_plan_configured') -Level 'Success'
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.power_plan_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        }
    }

    # Game Mode
    if ($Config.ContainsKey('GameMode') -and $Config.GameMode) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.enabling_game_mode') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Performance' -Setting 'AutoGameModeEnabled' -Enable $true | Out-Null
    }

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.performance.applied') -Level 'Success'
}

# === SECURITY CONFIGURATION ===

function Set-SecurityConfiguration {
    <#
    .SYNOPSIS
        Configures Windows security settings.

    .PARAMETER Config
        Security configuration hashtable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.configuring') -Level 'Info'

    # Windows Defender
    if ($Config.ContainsKey('WindowsDefender')) {
        if ($Config.WindowsDefender) {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.defender_enabled') -Level 'Info'
        } else {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.defender_warning') -Level 'Warning'
        }
    }

    # Firewall
    if ($Config.ContainsKey('Firewall') -and $Config.Firewall) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.firewall_enabled') -Level 'Info'

        try {
            Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.firewall_success') -Level 'Success'
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.firewall_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        }
    }

    # Developer mode
    if ($Config.ContainsKey('DeveloperMode') -and $Config.DeveloperMode) {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.developer_mode') -Level 'Info'
        Set-RegistrySettingFromConfig -Group 'Security' -Setting 'AllowDevelopmentWithoutDevLicense' -Enable $true | Out-Null
    }

    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.security.applied') -Level 'Success'
}

# === MAIN CONFIGURATION FUNCTION ===

function Set-SystemConfiguration {
    <#
    .SYNOPSIS
        Applies complete system configuration from profile.

    .PARAMETER Config
        System configuration hashtable from profile JSON
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Config
    )

    Write-Host ""
    Write-Status -Message (Get-LocalizedString -Key 'sysconfig.title') -Level 'Info'
    Write-Host ""

    try {
        # Explorer settings
        if ($Config.Explorer) {
            Set-ExplorerConfiguration -Config $Config.Explorer
        }

        # Taskbar settings
        if ($Config.Taskbar) {
            Set-TaskbarConfiguration -Config $Config.Taskbar
        }

        # Network settings
        if ($Config.Network) {
            Set-NetworkConfiguration -Config $Config.Network
        }

        # Privacy settings
        if ($Config.Privacy) {
            Set-PrivacyConfiguration -Config $Config.Privacy
        }

        # Performance settings
        if ($Config.Performance) {
            Set-PerformanceConfiguration -Config $Config.Performance
        }

        # Security settings
        if ($Config.Security) {
            Set-SecurityConfiguration -Config $Config.Security
        }

        Write-Host ""
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.completed') -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.restart_note') -Level 'Warning'

    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'sysconfig.failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        throw
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-SystemSettings',
    'Get-RegistryPath',
    'Get-RegistryValueDefinition',
    'Get-PowerPlanGuid',
    'Get-ServicesList',
    'Get-TimeoutValue',
    'Set-RegistryValue',
    'Set-RegistrySettingFromConfig',
    'Set-ExplorerConfiguration',
    'Set-TaskbarConfiguration',
    'Set-NetworkConfiguration',
    'Set-PrivacyConfiguration',
    'Set-PerformanceConfiguration',
    'Set-SecurityConfiguration',
    'Set-SystemConfiguration'
)
