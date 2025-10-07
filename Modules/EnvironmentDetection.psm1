<#
.SYNOPSIS
    Win11Forge - Environment Detection Module v2.0

.DESCRIPTION
    Detects execution environment (Windows Sandbox, VMware, Hyper-V, Physical)
    and provides filtering capabilities for environment-specific installations.

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
    Supports: Windows Sandbox, VMware, Hyper-V, Physical machines
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

    # Check for virtualization
    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue

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

    # Additional virtualization checks via BIOS
    $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction SilentlyContinue
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

    # Check for virtualization via processor features
    try {
        $processor = Get-CimInstance -ClassName Win32_Processor -ErrorAction SilentlyContinue | Select-Object -First 1
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
        $sandboxKey = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name 'InstallationType' -ErrorAction SilentlyContinue
        if ($sandboxKey.InstallationType -eq 'WindowsSandbox') {
            Write-Verbose "Windows Sandbox detected via registry"
            return $true
        }
    } catch {
        Write-Verbose "Could not check Sandbox registry key"
    }

    # Method 3: Check for container environment
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

    .OUTPUTS
        [bool] True if running on a VM (any type)
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $envType = Get-SystemEnvironmentType
    return $envType -ne [EnvironmentType]::Physical
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

    .OUTPUTS
        [hashtable] Complete environment information
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $envType = Get-SystemEnvironmentType
    $capabilities = Get-EnvironmentCapabilities

    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue
    $operatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction SilentlyContinue
    $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction SilentlyContinue

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
    'Get-SystemEnvironmentType',
    'Test-WindowsSandbox',
    'Test-IsVirtualMachine',
    'Get-EnvironmentCapabilities',
    'Test-ApplicationCompatibleWithEnvironment',
    'Get-EnvironmentReport'
)
