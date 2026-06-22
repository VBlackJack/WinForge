<#
.SYNOPSIS
    WinForge - Environment Detection Module v3.7.2

.DESCRIPTION
    Detects execution environment (Windows Sandbox, VMware, Hyper-V, Physical)
    and provides filtering capabilities for environment-specific installations.

.NOTES
    Author: Julien Bombled
    v3.7.2
    Supports: Windows Sandbox, VMware, Hyper-V, Physical machines
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

# Import DirectoryConstants for centralized registry paths
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-RegistryPath -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# === CIM CACHE ===
# Caches expensive CIM queries to avoid repeated WMI calls
$script:CimCache = @{}
$script:CimCacheMetadata = @{}
$script:CimCacheDefaultTTLMinutes = 30

function Get-CachedCimInstance {
    <#
    .SYNOPSIS
        Gets a CIM instance from cache or queries it if not cached.
    .DESCRIPTION
        Provides a caching layer around Get-CimInstance to avoid repeated WMI
        queries for the same class. Returns the cached result when available
        and not expired; otherwise performs a fresh query and stores the result
        with a configurable time-to-live.
    .PARAMETER ClassName
        The WMI class name to query.
    .PARAMETER Force
        Bypass cache and force a fresh query.
    .PARAMETER TTLMinutes
        Optional time-to-live for this cache entry in minutes.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ClassName,
        [switch]$Force,
        [int]$TTLMinutes = $script:CimCacheDefaultTTLMinutes
    )

    $now = Get-Date

    # Check if we have a valid cached entry
    if (-not $Force -and $script:CimCache.ContainsKey($ClassName)) {
        $metadata = $script:CimCacheMetadata[$ClassName]
        if ($metadata) {
            $age = ($now - $metadata.CachedAt).TotalMinutes
            if ($age -lt $metadata.TTLMinutes) {
                Write-Verbose "CIM cache hit: $ClassName (age: $([math]::Round($age, 1)) min)"
                return $script:CimCache[$ClassName]
            }
            Write-Verbose "CIM cache expired: $ClassName (age: $([math]::Round($age, 1)) min, TTL: $($metadata.TTLMinutes) min)"
        }
    }

    Write-Verbose "CIM cache miss: $ClassName - querying WMI"
    $instance = Get-CimInstance -ClassName $ClassName -ErrorAction SilentlyContinue
    $script:CimCache[$ClassName] = $instance
    $script:CimCacheMetadata[$ClassName] = @{
        CachedAt = $now
        TTLMinutes = $TTLMinutes
        QueryCount = 1
    }
    return $instance
}

function Clear-CimCache {
    <#
    .SYNOPSIS
        Clears the CIM instance cache.
    .DESCRIPTION
        Removes entries from the CIM instance cache. When a specific ClassName
        is provided, only that entry is removed; otherwise the entire cache is
        cleared, forcing fresh WMI queries on subsequent access.
    .PARAMETER ClassName
        Optional specific class name to clear. If not specified, clears all.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ClassName
    )

    if ($ClassName) {
        if ($script:CimCache.ContainsKey($ClassName)) {
            $script:CimCache.Remove($ClassName)
            $script:CimCacheMetadata.Remove($ClassName)
            Write-Verbose "CIM cache cleared: $ClassName"
        }
    }
    else {
        $script:CimCache.Clear()
        $script:CimCacheMetadata.Clear()
        Write-Verbose "CIM cache cleared completely"
    }
}

function Clear-ExpiredCimCache {
    <#
    .SYNOPSIS
        Clears only expired entries from the CIM cache.
    .DESCRIPTION
        Iterates through all cached CIM entries and removes those whose age
        exceeds their configured TTL. This is useful for periodic cache
        maintenance without discarding still-valid entries.
    .OUTPUTS
        Number of entries cleared.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    $now = Get-Date
    $keysToRemove = @()

    foreach ($className in $script:CimCacheMetadata.Keys) {
        $metadata = $script:CimCacheMetadata[$className]
        $age = ($now - $metadata.CachedAt).TotalMinutes
        if ($age -ge $metadata.TTLMinutes) {
            $keysToRemove += $className
        }
    }

    foreach ($key in $keysToRemove) {
        $script:CimCache.Remove($key)
        $script:CimCacheMetadata.Remove($key)
        Write-Verbose "Removed expired CIM cache entry: $key"
    }

    if ($keysToRemove.Count -gt 0) {
        Write-Verbose "Cleared $($keysToRemove.Count) expired CIM cache entries"
    }

    return $keysToRemove.Count
}

function Get-CimCacheStatistics {
    <#
    .SYNOPSIS
        Returns statistics about the CIM cache.
    .DESCRIPTION
        Produces a summary of the current CIM cache state, including total and
        expired entry counts, the default TTL, and per-entry details such as
        class name, age, and expiration status.
    .OUTPUTS
        Hashtable with cache statistics.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $now = Get-Date
    $entries = @()

    foreach ($className in $script:CimCache.Keys) {
        $metadata = $script:CimCacheMetadata[$className]
        $age = if ($metadata) { ($now - $metadata.CachedAt).TotalMinutes } else { 0 }
        $ttl = if ($metadata) { $metadata.TTLMinutes } else { $script:CimCacheDefaultTTLMinutes }
        $expired = $age -ge $ttl

        $entries += [PSCustomObject]@{
            ClassName = $className
            AgeMinutes = [math]::Round($age, 2)
            TTLMinutes = $ttl
            Expired = $expired
            CachedAt = if ($metadata) { $metadata.CachedAt } else { $null }
        }
    }

    return @{
        TotalEntries = $script:CimCache.Count
        ExpiredEntries = ($entries | Where-Object { $_.Expired }).Count
        DefaultTTLMinutes = $script:CimCacheDefaultTTLMinutes
        Entries = $entries
    }
}

function Set-CimCacheDefaultTTL {
    <#
    .SYNOPSIS
        Sets the default TTL for CIM cache entries.
    .DESCRIPTION
        Updates the default time-to-live applied to new CIM cache entries.
        Existing entries retain their original TTL; only entries cached after
        this change will use the new default value.
    .PARAMETER Minutes
        Default TTL in minutes.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateRange(1, 1440)]
        [int]$Minutes
    )

    $script:CimCacheDefaultTTLMinutes = $Minutes
    Write-Verbose "CIM cache default TTL set to $Minutes minutes"
}

# === ENVIRONMENT TYPES ===
enum EnvironmentType {
    Physical
    WindowsSandbox
    VMware
    HyperV
    VirtualBox
    Unknown
}

# === DETECTION FUNCTIONS ===

function Get-SystemEnvironmentType {
    <#
    .SYNOPSIS
        Detects the current system environment type.

    .DESCRIPTION
        Uses multiple detection methods to identify if running on:
        - Windows Sandbox
        - VMware virtual machine
        - Hyper-V virtual machine
        - VirtualBox
        - Physical hardware

    .OUTPUTS
        [EnvironmentType] The detected environment type
    #>
    [CmdletBinding()]
    [OutputType([EnvironmentType])]
    param()

    Write-Verbose "Detecting system environment..."

    # Check Windows Sandbox first (most specific)
    if (Test-WindowsSandbox) {
        return [EnvironmentType]::WindowsSandbox
    }

    # Check for virtualization (using cached query)
    $computerSystem = Get-CachedCimInstance -ClassName 'Win32_ComputerSystem'

    if ($computerSystem) {
        $manufacturer = $computerSystem.Manufacturer
        $model = $computerSystem.Model

        # VMware detection
        if ($manufacturer -match 'VMware' -or $model -match 'VMware') {
            return [EnvironmentType]::VMware
        }

        # Hyper-V detection
        if ($manufacturer -match 'Microsoft Corporation' -and $model -match 'Virtual Machine') {
            return [EnvironmentType]::HyperV
        }

        # VirtualBox detection
        if ($manufacturer -match 'innotek' -or $model -match 'VirtualBox') {
            return [EnvironmentType]::VirtualBox
        }
    }

    # Additional virtualization checks via BIOS (using cached query)
    $bios = Get-CachedCimInstance -ClassName 'Win32_BIOS'
    if ($bios) {
        $biosVersion = $bios.Version
        $serialNumber = $bios.SerialNumber

        if ($biosVersion -match 'VBOX' -or $serialNumber -match 'VirtualBox') {
            return [EnvironmentType]::VirtualBox
        }

        if ($biosVersion -match 'VMware' -or $serialNumber -match 'VMware') {
            return [EnvironmentType]::VMware
        }

        if ($biosVersion -match 'Hyper-V' -or $biosVersion -match 'VRTUAL') {
            return [EnvironmentType]::HyperV
        }
    }

    # Check for virtualization via processor features (using cached query)
    try {
        $processor = Get-CachedCimInstance -ClassName 'Win32_Processor' | Select-Object -First 1
        if ($processor -and $processor.Name -match 'Virtual') {
            return [EnvironmentType]::Unknown
        }
    } catch {
        Write-Verbose "Could not query processor information"
    }

    # Default to physical machine
    return [EnvironmentType]::Physical
}

function Test-WindowsSandbox {
    <#
    .SYNOPSIS
        Checks if running inside Windows Sandbox.

    .DESCRIPTION
        Uses multiple methods to detect Windows Sandbox environment:
        - Computer name pattern
        - Specific registry keys
        - Container detection

    .OUTPUTS
        [bool] True if running in Windows Sandbox
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    # Method 1: Check computer name (Sandbox uses specific pattern)
    $computerName = $env:COMPUTERNAME
    if ($computerName -match '^SANDBOX-') {
        Write-Verbose "Windows Sandbox detected via computer name: $computerName"
        return $true
    }

    # Method 2: Check for Sandbox-specific registry key
    try {
        $sandboxKey = Get-ItemProperty -Path (Get-RegistryPath -PathKey 'WindowsNTVersion') -Name 'InstallationType' -ErrorAction SilentlyContinue
        if ($sandboxKey.InstallationType -eq 'WindowsSandbox') {
            Write-Verbose "Windows Sandbox detected via registry"
            return $true
        }
    } catch {
        Write-Verbose "Could not check Sandbox registry key"
    }

    # Method 3: Check for container environment
    # No centralized key for SYSTEM\CurrentControlSet\Control\ContainerManager - keeping as-is
    try {
        $containerKey = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\ContainerManager' -ErrorAction SilentlyContinue
        if ($containerKey) {
            Write-Verbose "Windows Sandbox detected via container manager"
            return $true
        }
    } catch {
        Write-Verbose "Could not check container manager"
    }

    return $false
}

function Test-IsVirtualMachine {
    <#
    .SYNOPSIS
        Quick check if running on any virtual machine.
    .DESCRIPTION
        Calls Get-SystemEnvironmentType and returns $true if the detected
        environment is anything other than Physical, covering Hyper-V, VMware,
        VirtualBox, Docker, WSL, and Windows Sandbox.
    .OUTPUTS
        [bool] True if running on a VM (any type)
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $envType = Get-SystemEnvironmentType
    return $envType -ne [EnvironmentType]::Physical
}

function Test-IsWindowsSandbox {
    <#
    .SYNOPSIS
        Checks if running inside Windows Sandbox environment.

    .DESCRIPTION
        Uses multiple detection methods to identify Windows Sandbox:
        - Check for WDAGUtilityAccount user (Sandbox uses this account)
        - Check for specific Sandbox registry keys
        - Check computer name pattern

        This is critical for Store/Winget msstore installations which crash in Sandbox.

    .OUTPUTS
        [bool] True if running in Windows Sandbox
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    # Method 1: Check for WDAGUtilityAccount (Sandbox-specific user)
    if ($env:USERNAME -eq 'WDAGUtilityAccount') {
        Write-Verbose "Windows Sandbox detected via WDAGUtilityAccount user"
        return $true
    }

    # Method 2: Check computer name pattern (Sandbox uses SANDBOX- prefix)
    if ($env:COMPUTERNAME -match '^SANDBOX-') {
        Write-Verbose "Windows Sandbox detected via computer name: $($env:COMPUTERNAME)"
        return $true
    }

    # Method 3: Check for Sandbox-specific registry key
    try {
        $sandboxKey = Get-ItemProperty -Path (Get-RegistryPath -PathKey 'WindowsNTVersion') -Name 'InstallationType' -ErrorAction SilentlyContinue
        if ($sandboxKey.InstallationType -eq 'WindowsSandbox') {
            Write-Verbose "Windows Sandbox detected via registry InstallationType"
            return $true
        }
    } catch {
        Write-Verbose "Could not check Sandbox registry key"
    }

    # Method 4: Check for container environment
    # No centralized key for SYSTEM\CurrentControlSet\Control\ContainerManager - keeping as-is
    try {
        $containerKey = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\ContainerManager' -ErrorAction SilentlyContinue
        if ($containerKey) {
            Write-Verbose "Windows Sandbox detected via container manager"
            return $true
        }
    } catch {
        Write-Verbose "Could not check container manager"
    }

    return $false
}

function Get-EnvironmentCapabilities {
    <#
    .SYNOPSIS
        Returns environment-specific capabilities and limitations.

    .DESCRIPTION
        Provides information about what can and cannot be installed
        in the current environment.

    .OUTPUTS
        [hashtable] Environment capabilities
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $envType = Get-SystemEnvironmentType

    $capabilities = @{
        EnvironmentType = $envType
        CanInstallDrivers = $true
        CanInstallServices = $true
        CanModifyRegistry = $true
        CanInstallHyperV = $true
        CanInstallWSL = $true
        CanInstallVirtualization = $true
        CanInstallHardwareSpecific = $true
        IsPersistent = $true
        RecommendedPackageSource = 'Winget'
    }

    switch ($envType) {
        'WindowsSandbox' {
            $capabilities.CanInstallDrivers = $false
            $capabilities.CanInstallHyperV = $false
            $capabilities.CanInstallWSL = $false
            $capabilities.CanInstallVirtualization = $false
            $capabilities.IsPersistent = $false
            $capabilities.RecommendedPackageSource = 'Portable'
        }

        'VMware' {
            $capabilities.CanInstallHyperV = $false
            $capabilities.CanInstallVirtualization = $false
            $capabilities.CanInstallHardwareSpecific = $false
        }

        'HyperV' {
            $capabilities.CanInstallVirtualization = $false
            $capabilities.CanInstallHardwareSpecific = $false
        }

        'VirtualBox' {
            $capabilities.CanInstallHyperV = $false
            $capabilities.CanInstallVirtualization = $false
            $capabilities.CanInstallHardwareSpecific = $false
        }
    }

    return $capabilities
}

function Test-ApplicationCompatibleWithEnvironment {
    <#
    .SYNOPSIS
        Checks if an application can be installed in current environment.
    .DESCRIPTION
        Evaluates the current environment capabilities against the requirements
        of a given application. Returns a compatibility result indicating whether
        the application can be installed, along with a reason and recommendation
        when it cannot (e.g., virtualization software on a VM, driver packages
        in a sandbox).
    .PARAMETER ApplicationName
        Name of the application to check

    .PARAMETER Category
        Category of the application

    .OUTPUTS
        [hashtable] Compatibility result with reason
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ApplicationName,

        [Parameter()]
        [string]$Category
    )

    $capabilities = Get-EnvironmentCapabilities
    $result = @{
        Compatible = $true
        Reason = ''
        Recommendation = ''
    }

    # Check for virtualization software
    if ($ApplicationName -match 'VMware|VirtualBox|Hyper-V|Docker|WSL') {
        if (-not $capabilities.CanInstallVirtualization) {
            $result.Compatible = $false
            $result.Reason = "Virtualization software cannot be installed in $($capabilities.EnvironmentType) environment"
            $result.Recommendation = "Install on physical machine"
            return $result
        }
    }

    # Check for hardware-specific drivers
    if ($Category -eq 'Drivers' -or $Category -eq '3DPrinting' -or $Category -eq 'Printing') {
        if (-not $capabilities.CanInstallHardwareSpecific) {
            $result.Compatible = $false
            $result.Reason = "Hardware-specific software not recommended in $($capabilities.EnvironmentType) environment"
            $result.Recommendation = "Skip in virtual environment"
            return $result
        }
    }

    # Windows Sandbox specific checks
    if ($capabilities.EnvironmentType -eq 'WindowsSandbox') {
        if (-not $capabilities.IsPersistent) {
            $result.Recommendation = "Installation will not persist after sandbox restart"
        }
    }

    return $result
}

function Get-EnvironmentReport {
    <#
    .SYNOPSIS
        Generates a detailed environment report.
    .DESCRIPTION
        Collects comprehensive information about the current runtime environment,
        including the environment type, capabilities, OS version, hardware specs
        (CPU, RAM, disk), and PowerShell version. Uses cached CIM queries for
        performance.
    .OUTPUTS
        [hashtable] Complete environment information
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $envType = Get-SystemEnvironmentType
    $capabilities = Get-EnvironmentCapabilities

    # Use cached CIM queries for performance
    $computerSystem = Get-CachedCimInstance -ClassName 'Win32_ComputerSystem'
    $operatingSystem = Get-CachedCimInstance -ClassName 'Win32_OperatingSystem'
    $bios = Get-CachedCimInstance -ClassName 'Win32_BIOS'

    $report = @{
        EnvironmentType = $envType
        ComputerName = $env:COMPUTERNAME
        UserName = $env:USERNAME
        OSVersion = if ($operatingSystem) { $operatingSystem.Caption } else { 'Unknown' }
        OSBuild = if ($operatingSystem) { $operatingSystem.BuildNumber } else { 'Unknown' }
        Manufacturer = if ($computerSystem) { $computerSystem.Manufacturer } else { 'Unknown' }
        Model = if ($computerSystem) { $computerSystem.Model } else { 'Unknown' }
        BIOSVersion = if ($bios) { $bios.Version } else { 'Unknown' }
        TotalMemoryGB = if ($computerSystem) { [math]::Round($computerSystem.TotalPhysicalMemory / 1GB, 2) } else { 0 }
        Capabilities = $capabilities
        IsVirtual = Test-IsVirtualMachine
        DetectionTimestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    }

    return $report
}

# === EXPORT FUNCTIONS ===
Export-ModuleMember -Function @(
    # Environment Detection
    'Get-SystemEnvironmentType',
    'Test-WindowsSandbox',
    'Test-IsVirtualMachine',
    'Test-IsWindowsSandbox',
    'Get-EnvironmentCapabilities',
    'Test-ApplicationCompatibleWithEnvironment',
    'Get-EnvironmentReport',
    # CIM Cache Management
    'Get-CachedCimInstance',
    'Clear-CimCache',
    'Clear-ExpiredCimCache',
    'Get-CimCacheStatistics',
    'Set-CimCacheDefaultTTL'
)
