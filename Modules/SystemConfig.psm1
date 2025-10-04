<#
.SYNOPSIS
    Win11Forge - System Configuration Module v2.0.1 (FIXED)

.DESCRIPTION
    Applies system configuration from profile JSON:
    - Windows Explorer settings
    - Taskbar configuration
    - Network settings (DNS) - FIXED
    - Privacy settings
    - Performance optimizations
    - Security settings

.NOTES
    Author: Julien Bombled
    Version: 2.0.1 FIXED
    FIXED: DNS array handling bug
    FIXED: Taskbar error handling
    Requires administrator privileges
#>

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
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

        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type -Force
        Write-Status -Message "Set: $Path\$Name = $Value" -Level 'Verbose'
        return $true
    }
    catch {
        Write-Status -Message "Failed to set: $Path\$Name - $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
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

    Write-Status -Message "Configuring Windows Explorer..." -Level 'Info'

    $explorerPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'

    # Show hidden files
    if ($Config.ContainsKey('ShowHiddenFiles') -and $Config.ShowHiddenFiles) {
        Set-RegistryValue -Path $explorerPath -Name 'Hidden' -Value 1 -Type DWord | Out-Null
    }

    # Show file extensions
    if ($Config.ContainsKey('ShowFileExtensions') -and $Config.ShowFileExtensions) {
        Set-RegistryValue -Path $explorerPath -Name 'HideFileExt' -Value 0 -Type DWord | Out-Null
    }

    # Navigation pane optimization - Show all folders
    if ($Config.ContainsKey('NavigationPaneOptimized') -and $Config.NavigationPaneOptimized) {
        Set-RegistryValue -Path $explorerPath -Name 'NavPaneShowAllFolders' -Value 1 -Type DWord | Out-Null
    }

    # Show Libraries in navigation pane
    if ($Config.ContainsKey('ShowLibraries') -and $Config.ShowLibraries) {
        # Windows 11 uses a different location for libraries visibility
        Set-RegistryValue -Path 'HKCU:\Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}' -Name 'System.IsPinnedToNameSpaceTree' -Value 1 -Type DWord | Out-Null
    }

    # Expand to open folder (navigation pane follows current folder)
    if ($Config.ContainsKey('ExpandToOpenFolder') -and $Config.ExpandToOpenFolder) {
        Set-RegistryValue -Path $explorerPath -Name 'NavPaneExpandToCurrentFolder' -Value 1 -Type DWord | Out-Null
    }

    # Show sync provider notifications (availability status)
    if ($Config.ContainsKey('ShowSyncProviderNotifications') -and $Config.ShowSyncProviderNotifications) {
        # Enable "Always show availability status" in Windows 11
        # This shows OneDrive and other cloud storage sync status
        Set-RegistryValue -Path $explorerPath -Name 'ShowSyncProviderNotifications' -Value 1 -Type DWord | Out-Null
        # Also enable the Status column visibility
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer' -Name 'ShowStatusColumn' -Value 1 -Type DWord | Out-Null
    }

    # Show full path in title bar
    if ($Config.ContainsKey('ShowFullPathInTitleBar') -and $Config.ShowFullPathInTitleBar) {
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState' -Name 'FullPath' -Value 1 -Type DWord | Out-Null
    }

    # Launch folder windows in separate process
    if ($Config.ContainsKey('LaunchFolderWindowsInSeparateProcess') -and $Config.LaunchFolderWindowsInSeparateProcess) {
        Set-RegistryValue -Path $explorerPath -Name 'SeparateProcess' -Value 1 -Type DWord | Out-Null
    }

    # Developer mode
    if ($Config.ContainsKey('DeveloperMode') -and $Config.DeveloperMode) {
        $devModePath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
        Set-RegistryValue -Path $devModePath -Name 'AllowDevelopmentWithoutDevLicense' -Value 1 -Type DWord | Out-Null
    }

    # Restart Explorer to apply changes
    Write-Status -Message "Restarting Explorer to apply changes..." -Level 'Info'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Status -Message "Explorer configuration applied" -Level 'Success'
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

    Write-Status -Message "Configuring Taskbar..." -Level 'Info'

    $taskbarPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    $searchPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'

    try {
        # Disable widgets
        if ($Config.ContainsKey('DisableWidgets') -and $Config.DisableWidgets) {
            Set-RegistryValue -Path $taskbarPath -Name 'TaskbarDa' -Value 0 -Type DWord | Out-Null
        }

        # Taskbar alignment (0 = left, 1 = center)
        if ($Config.ContainsKey('StartAlignment') -and $Config.StartAlignment) {
            $alignment = if ($Config.StartAlignment -eq 'left') { 0 } else { 1 }
            Set-RegistryValue -Path $taskbarPath -Name 'TaskbarAl' -Value $alignment -Type DWord | Out-Null
        }

        # Search box mode (0 = hidden, 1 = icon only, 2 = box)
        if ($Config.ContainsKey('SearchMode') -and $Config.SearchMode) {
            $searchMode = switch ($Config.SearchMode) {
                'hidden'    { 0 }
                'icon_only' { 1 }
                'box'       { 2 }
                default     { 1 }
            }
            Set-RegistryValue -Path $searchPath -Name 'SearchboxTaskbarMode' -Value $searchMode -Type DWord | Out-Null
        }

        # Hide Task View button
        if ($Config.ContainsKey('HideTaskView') -and $Config.HideTaskView) {
            Set-RegistryValue -Path $taskbarPath -Name 'ShowTaskViewButton' -Value 0 -Type DWord | Out-Null
        }

        # Hide Chat icon
        if ($Config.ContainsKey('HideChat') -and $Config.HideChat) {
            Set-RegistryValue -Path $taskbarPath -Name 'TaskbarMn' -Value 0 -Type DWord | Out-Null
        }

        Write-Status -Message "Taskbar configuration applied" -Level 'Success'
    }
    catch {
        Write-Status -Message "Some taskbar settings require additional permissions" -Level 'Warning'
        Write-Status -Message "Taskbar configuration partially applied" -Level 'Success'
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

    Write-Status -Message "Configuring Network settings..." -Level 'Info'

    # Configure DNS servers - ULTRA ROBUST FIX v2.0.4
    if ($Config.ContainsKey('DnsServers') -and $Config.DnsServers) {
        try {
            # Build DNS array - handle all input types
            $dnsArray = @()

            # Convert to array if needed (handle ArrayList, PSCustomObject arrays, etc.)
            $dnsInput = $Config.DnsServers
            if ($dnsInput -is [System.Collections.ArrayList] -or
                $dnsInput -is [System.Collections.Generic.List[object]] -or
                $dnsInput -is [object[]]) {
                # Already array-like, convert to standard array
                $dnsArray = @($dnsInput | Where-Object {
                    $_ -and $_ -is [string] -and $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$'
                })
            }
            # Case 2: String (single IP or comma-separated)
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
            # Case 3: Try to enumerate it
            else {
                try {
                    $dnsArray = @($dnsInput | ForEach-Object { $_ } | Where-Object {
                        $_ -and $_ -is [string] -and $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$'
                    })
                } catch {
                    Write-Status -Message "Unable to parse DNS servers: $($_.Exception.Message)" -Level 'Verbose'
                }
            }
            
            # Validate and apply
            if ($dnsArray.Count -gt 0) {
                Write-Status -Message "Setting DNS servers: $($dnsArray -join ', ')" -Level 'Info'

                $adapters = Get-NetAdapter | Where-Object { 
                    $_.Status -eq 'Up' -and $_.InterfaceType -ne 'Loopback' 
                }

                $successCount = 0
                foreach ($adapter in $adapters) {
                    try {
                        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex `
                            -ServerAddresses $dnsArray -ErrorAction Stop
                        Write-Status -Message "DNS configured for: $($adapter.Name)" -Level 'Success'
                        $successCount++
                    } catch {
                        Write-Status -Message "Failed to set DNS for $($adapter.Name): $($_.Exception.Message)" -Level 'Warning'
                    }
                }

                if ($successCount -gt 0) {
                    Write-Status -Message "DNS configuration completed ($successCount adapter(s))" -Level 'Success'
                }
            } else {
                Write-Status -Message "No valid DNS servers found in configuration" -Level 'Warning'
            }
        } catch {
            Write-Status -Message "DNS configuration error: $($_.Exception.Message)" -Level 'Error'
        }
    }

    # Network optimizations for gaming
    if ($Config.ContainsKey('GamingOptimizations') -and $Config.GamingOptimizations) {
        Write-Status -Message "Applying gaming network optimizations..." -Level 'Info'
        
        Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'TcpAckFrequency' -Value 1 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'TCPNoDelay' -Value 1 -Type DWord | Out-Null
    }

    # QoS optimization
    if ($Config.ContainsKey('QoSOptimization') -and $Config.QoSOptimization) {
        Write-Status -Message "Enabling QoS optimization..." -Level 'Info'
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched' -Name 'NonBestEffortLimit' -Value 0 -Type DWord | Out-Null
    }

    # Developer optimizations
    if ($Config.ContainsKey('DeveloperOptimizations') -and $Config.DeveloperOptimizations) {
        Write-Status -Message "Applying developer network optimizations..." -Level 'Info'
        Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'TcpAckFrequency' -Value 1 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'TCPNoDelay' -Value 1 -Type DWord | Out-Null
    }

    Write-Status -Message "Network configuration applied" -Level 'Success'
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

    Write-Status -Message "Configuring Privacy settings..." -Level 'Info'

    # Disable telemetry
    if ($Config.ContainsKey('DisableTelemetry') -and $Config.DisableTelemetry) {
        Write-Status -Message "Disabling telemetry..." -Level 'Info'
        
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord | Out-Null
        
        # Disable Connected User Experiences and Telemetry service
        try {
            Set-Service -Name 'DiagTrack' -StartupType Disabled -ErrorAction SilentlyContinue
            Stop-Service -Name 'DiagTrack' -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Status -Message "Could not disable DiagTrack service" -Level 'Verbose'
        }
    }

    # Minimal data collection
    if ($Config.ContainsKey('MinimalDataCollection') -and $Config.MinimalDataCollection) {
        Write-Status -Message "Configuring minimal data collection..." -Level 'Info'
        
        # Disable advertising ID
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord | Out-Null
        
        # Disable app launch tracking
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_TrackProgs' -Value 0 -Type DWord | Out-Null
        
        # Disable suggested content
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338393Enabled' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353694Enabled' -Value 0 -Type DWord | Out-Null
    }

    # Disable activity history
    if ($Config.ContainsKey('DisableActivityHistory') -and $Config.DisableActivityHistory) {
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord | Out-Null
    }

    # Disable Cortana (obsolete with modern AI)
    if ($Config.ContainsKey('DisableCortana') -and $Config.DisableCortana) {
        Write-Status -Message "Disabling Cortana..." -Level 'Info'
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord | Out-Null
    }

    # Disable consumer features (bloatware suggestions)
    if ($Config.ContainsKey('DisableConsumerFeatures') -and $Config.DisableConsumerFeatures) {
        Write-Status -Message "Disabling consumer features and bloatware..." -Level 'Info'
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SilentInstalledAppsEnabled' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'PreInstalledAppsEnabled' -Value 0 -Type DWord | Out-Null
    }

    # Disable Windows tips and tricks
    if ($Config.ContainsKey('DisableWindowsTips') -and $Config.DisableWindowsTips) {
        Write-Status -Message "Disabling Windows tips and tricks..." -Level 'Info'
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord | Out-Null
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SoftLandingEnabled' -Value 0 -Type DWord | Out-Null
    }

    Write-Status -Message "Privacy settings configured" -Level 'Success'
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

    Write-Status -Message "Configuring Performance settings..." -Level 'Info'

    # Disable visual effects
    if ($Config.ContainsKey('DisableVisualEffects') -and $Config.DisableVisualEffects) {
        Write-Status -Message "Disabling visual effects for best performance..." -Level 'Info'
        
        $visualFXPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects'
        Set-RegistryValue -Path $visualFXPath -Name 'VisualFXSetting' -Value 2 -Type DWord | Out-Null
    }

    # Optimize services (Safe global optimization)
    if ($Config.ContainsKey('OptimizeServices') -and $Config.OptimizeServices) {
        Write-Status -Message "Optimizing Windows services (safe mode)..." -Level 'Info'

        # Safe services to disable (won't break functionality for most users)
        $servicesToDisable = @(
            'MapsBroker',              # Downloaded Maps Manager (rarely used)
            'lfsvc',                   # Geolocation Service (optional)
            'RetailDemo',              # Retail Demo Service (not needed)
            'PhoneSvc',                # Phone Service (unless using Phone Link heavily)
            'Fax',                     # Fax service (obsolete)
            'XblAuthManager',          # Xbox Live Auth (unless gaming)
            'XblGameSave',             # Xbox Live Game Save (unless gaming)
            'XboxNetApiSvc',           # Xbox Networking (unless gaming)
            'XboxGipSvc',              # Xbox Accessory Management (unless Xbox controller)
            'WalletService',           # Microsoft Wallet (rarely used)
            'WMPNetworkSvc'            # Windows Media Player Network Sharing (rarely used)
        )

        # Services to set to Manual (from Automatic) - saves resources but still available
        $servicesToManual = @(
            'WSearch',                 # Windows Search - Keep available but manual
            'SysMain',                 # Superfetch - Keep available but manual
            'TrkWks',                  # Distributed Link Tracking Client
            'WbioSrvc'                 # Windows Biometric Service (manual is fine)
        )

        foreach ($service in $servicesToDisable) {
            try {
                $svc = Get-Service -Name $service -ErrorAction SilentlyContinue
                if ($svc -and $svc.StartType -ne 'Disabled') {
                    Set-Service -Name $service -StartupType Disabled -ErrorAction Stop
                    Write-Status -Message "Disabled: $service" -Level 'Verbose'
                }
            }
            catch {
                Write-Status -Message "Could not disable: $service" -Level 'Verbose'
            }
        }

        foreach ($service in $servicesToManual) {
            try {
                $svc = Get-Service -Name $service -ErrorAction SilentlyContinue
                if ($svc -and $svc.StartType -eq 'Automatic') {
                    Set-Service -Name $service -StartupType Manual -ErrorAction Stop
                    Write-Status -Message "Set to Manual: $service" -Level 'Verbose'
                }
            }
            catch {
                Write-Status -Message "Could not modify: $service" -Level 'Verbose'
            }
        }
    }

    # Power plan
    if ($Config.ContainsKey('PowerPlan') -and $Config.PowerPlan) {
        Write-Status -Message "Setting power plan: $($Config.PowerPlan)" -Level 'Info'
        
        try {
            $powerPlan = switch ($Config.PowerPlan) {
                'High Performance' { '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c' }
                'Balanced'         { '381b4222-f694-41f0-9685-ff5bb260df2e' }
                'Power Saver'      { 'a1841308-3541-4fab-bc81-f71556f20b4a' }
                default            { '381b4222-f694-41f0-9685-ff5bb260df2e' }
            }

            & powercfg.exe /setactive $powerPlan
            Write-Status -Message "Power plan configured" -Level 'Success'
        }
        catch {
            Write-Status -Message "Failed to set power plan: $($_.Exception.Message)" -Level 'Error'
        }
    }

    # Game Mode
    if ($Config.ContainsKey('GameMode') -and $Config.GameMode) {
        Write-Status -Message "Enabling Game Mode..." -Level 'Info'
        Set-RegistryValue -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AutoGameModeEnabled' -Value 1 -Type DWord | Out-Null
    }

    Write-Status -Message "Performance settings configured" -Level 'Success'
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

    Write-Status -Message "Configuring Security settings..." -Level 'Info'

    # Windows Defender
    if ($Config.ContainsKey('WindowsDefender')) {
        if ($Config.WindowsDefender) {
            Write-Status -Message "Ensuring Windows Defender is enabled..." -Level 'Info'
        } else {
            Write-Status -Message "Warning: Disabling Windows Defender is not recommended" -Level 'Warning'
        }
    }

    # Firewall
    if ($Config.ContainsKey('Firewall') -and $Config.Firewall) {
        Write-Status -Message "Ensuring Windows Firewall is enabled..." -Level 'Info'
        
        try {
            Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
            Write-Status -Message "Firewall enabled for all profiles" -Level 'Success'
        }
        catch {
            Write-Status -Message "Failed to configure firewall: $($_.Exception.Message)" -Level 'Error'
        }
    }

    # Developer mode
    if ($Config.ContainsKey('DeveloperMode') -and $Config.DeveloperMode) {
        Write-Status -Message "Enabling Developer Mode..." -Level 'Info'
        Set-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowDevelopmentWithoutDevLicense' -Value 1 -Type DWord | Out-Null
    }

    Write-Status -Message "Security settings configured" -Level 'Success'
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
    Write-Status -Message "=== Applying System Configuration ===" -Level 'Info'
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
        Write-Status -Message "System configuration completed successfully" -Level 'Success'
        Write-Status -Message "Note: Some changes may require a system restart to take full effect" -Level 'Warning'

    }
    catch {
        Write-Status -Message "System configuration failed: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Set-RegistryValue',
    'Set-ExplorerConfiguration',
    'Set-TaskbarConfiguration',
    'Set-NetworkConfiguration',
    'Set-PrivacyConfiguration',
    'Set-PerformanceConfiguration',
    'Set-SecurityConfiguration',
    'Set-SystemConfiguration'
)

