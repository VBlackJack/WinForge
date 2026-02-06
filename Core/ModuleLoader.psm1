<#
.SYNOPSIS
    Win11Forge - Centralized Module Loader v1.0.0

.DESCRIPTION
    Provides centralized module dependency management for Win11Forge:
    - Resolves module paths relative to repository root
    - Loads core dependencies with idempotent import checks
    - Reduces boilerplate code in feature modules

.NOTES
    Author: Julien Bombled
    v3.6.8
    This module should be imported first by other modules
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

# === MODULE PATHS ===
$script:ModuleLoaderRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleLoaderRoot -Parent

# Module path mapping: command name -> module file path (relative to repository root)
$script:CommandToModuleMap = @{
    # Core.psm1 exports
    'Write-Status'           = 'Core\Core.psm1'
    'Write-LogMessage'       = 'Core\Core.psm1'
    'Get-Win11ForgeDirectory' = 'Core\Core.psm1'
    'Test-IsWindowsSandbox'  = 'Core\Core.psm1'
    'Test-CommandExists'     = 'Core\Core.psm1'

    # Localization.psm1 exports
    'Get-LocalizedString'    = 'Core\Localization.psm1'

    # DirectoryConstants.psm1 exports
    'Get-Win11ForgePath'     = 'Core\DirectoryConstants.psm1'

    # FeatureFlags.psm1 exports
    'Test-FeatureEnabled'    = 'Core\FeatureFlags.psm1'
    'Get-FeatureFlags'       = 'Core\FeatureFlags.psm1'

    # TimeoutSettings.psm1 exports
    'Get-TimeoutSettings'    = 'Core\TimeoutSettings.psm1'
    'Get-InstallationTimeout' = 'Core\TimeoutSettings.psm1'
    'Get-MaxParallelJobs'    = 'Core\TimeoutSettings.psm1'
    'Get-ParallelTimeout'    = 'Core\TimeoutSettings.psm1'
    'Get-PluginTimeout'      = 'Core\TimeoutSettings.psm1'

    # SecureStorage.psm1 exports
    'Protect-Data'           = 'Core\SecureStorage.psm1'
    'Unprotect-Data'         = 'Core\SecureStorage.psm1'
    'Test-SecureStorageAvailable' = 'Core\SecureStorage.psm1'

    # StructuredLogging.psm1 exports
    'Write-StructuredLog'    = 'Core\StructuredLogging.psm1'

    # RestApiServer.psm1 exports
    'Register-ApiEndpoint'   = 'Core\RestApiServer.psm1'

    # JsonSchemaValidation.psm1 exports
    'Test-JsonAgainstSchema' = 'Modules\JsonSchemaValidation.psm1'

    # ApplicationDetection.psm1 exports
    'Test-ApplicationInstalled' = 'Modules\ApplicationDetection.psm1'
    'Get-InstalledAppVersion'   = 'Modules\ApplicationDetection.psm1'

    # WingetCache.psm1 exports
    'Get-CachedWingetList'   = 'Modules\WingetCache.psm1'
    'Get-WingetCacheStatistics' = 'Modules\WingetCache.psm1'

    # InstallationMethods.psm1 exports
    'Install-ViaWinget'      = 'Modules\InstallationMethods.psm1'
    'Install-Application'    = 'Modules\InstallationMethods.psm1'

    # ApplicationDatabase.psm1 exports
    'Get-ApplicationDatabase' = 'Modules\ApplicationDatabase.psm1'

    # RollbackManager.psm1 exports
    'Invoke-Rollback'        = 'Modules\RollbackManager.psm1'
    'Invoke-RollbackWithConfirmation' = 'Modules\RollbackManager.psm1'
    'Get-RollbackState'      = 'Modules\RollbackManager.psm1'
    'Get-RollbackSummary'    = 'Modules\RollbackManager.psm1'

    # EnvironmentDetection.psm1 exports
    'Get-SystemEnvironmentType' = 'Modules\EnvironmentDetection.psm1'

    # PluginSandbox.psm1 exports
    'Invoke-PluginHookSandboxed' = 'Core\PluginSandbox.psm1'
}

# Cache for already loaded modules
$script:LoadedModules = @{}

function Get-Win11ForgeRepositoryRoot {
    <#
    .SYNOPSIS
        Gets the Win11Forge repository root path.
    .DESCRIPTION
        Returns the absolute path to the Win11Forge repository root.
        This is useful for modules that need to locate other resources.
    .OUTPUTS
        [string] The repository root path.
    .EXAMPLE
        $root = Get-Win11ForgeRepositoryRoot
        $configPath = Join-Path $root 'Config\settings.json'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:RepositoryRoot
}

function Import-CoreDependency {
    <#
    .SYNOPSIS
        Imports a Win11Forge module that provides the specified command.
    .DESCRIPTION
        Looks up the module that exports the given command and imports it
        if not already loaded. This provides idempotent module loading.
    .PARAMETER CommandName
        The name of the command that must be available.
    .PARAMETER Force
        If specified, forces reimport even if command is already available.
    .OUTPUTS
        [bool] True if command is now available, false otherwise.
    .EXAMPLE
        Import-CoreDependency -CommandName 'Write-Status'
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [ValidateNotNullOrEmpty()]
        [string]$CommandName,

        [Parameter()]
        [switch]$Force
    )

    process {
        # Check if command already exists
        if (-not $Force -and (Get-Command -Name $CommandName -ErrorAction SilentlyContinue)) {
            return $true
        }

        # Look up the module for this command
        if (-not $script:CommandToModuleMap.ContainsKey($CommandName)) {
            Write-Warning "Unknown command '$CommandName' - not in module map"
            return $false
        }

        $relativePath = $script:CommandToModuleMap[$CommandName]
        $modulePath = Join-Path $script:RepositoryRoot $relativePath

        # Check if already loaded this session
        if (-not $Force -and $script:LoadedModules.ContainsKey($modulePath)) {
            return (Get-Command -Name $CommandName -ErrorAction SilentlyContinue) -ne $null
        }

        # Import the module
        if (Test-Path -Path $modulePath) {
            try {
                Import-Module -Name $modulePath -Force -ErrorAction Stop
                $script:LoadedModules[$modulePath] = $true
                return $true
            } catch {
                Write-Warning "Failed to import module '$modulePath': $($_.Exception.Message)"
                return $false
            }
        } else {
            Write-Warning "Module not found: $modulePath"
            return $false
        }
    }
}

function Import-CoreDependencies {
    <#
    .SYNOPSIS
        Imports multiple Win11Forge core dependencies at once.
    .DESCRIPTION
        Convenience function to import multiple core dependencies.
        Commonly used at the start of feature modules to ensure
        all required commands are available.
    .PARAMETER CommandNames
        Array of command names that must be available.
    .PARAMETER ThrowOnFailure
        If specified, throws an exception if any command cannot be loaded.
    .OUTPUTS
        [bool] True if all commands are now available, false otherwise.
    .EXAMPLE
        Import-CoreDependencies -CommandNames @('Write-Status', 'Get-LocalizedString')
    .EXAMPLE
        Import-CoreDependencies -CommandNames @('Write-Status', 'Get-LocalizedString') -ThrowOnFailure
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string[]]$CommandNames,

        [Parameter()]
        [switch]$ThrowOnFailure
    )

    $allSuccess = $true
    $failedCommands = @()

    foreach ($commandName in $CommandNames) {
        $success = Import-CoreDependency -CommandName $commandName
        if (-not $success) {
            $allSuccess = $false
            $failedCommands += $commandName
        }
    }

    if (-not $allSuccess -and $ThrowOnFailure) {
        throw "Failed to import required dependencies: $($failedCommands -join ', ')"
    }

    return $allSuccess
}

function Initialize-Win11ForgeModule {
    <#
    .SYNOPSIS
        Initializes a Win11Forge feature module with standard dependencies.
    .DESCRIPTION
        Convenience function that imports the most commonly used core
        dependencies (Write-Status, Get-LocalizedString) and optionally
        additional specified commands.

        This function should be called at the top of feature modules
        to ensure core infrastructure is available.
    .PARAMETER AdditionalCommands
        Optional array of additional command names to import.
    .PARAMETER IncludeFeatureFlags
        If specified, also imports Test-FeatureEnabled from FeatureFlags.
    .PARAMETER IncludeTimeouts
        If specified, also imports timeout-related commands.
    .OUTPUTS
        [hashtable] Contains RepositoryRoot and success status.
    .EXAMPLE
        $init = Initialize-Win11ForgeModule
        $repoRoot = $init.RepositoryRoot
    .EXAMPLE
        Initialize-Win11ForgeModule -IncludeFeatureFlags -AdditionalCommands @('Test-ApplicationInstalled')
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [string[]]$AdditionalCommands = @(),

        [Parameter()]
        [switch]$IncludeFeatureFlags,

        [Parameter()]
        [switch]$IncludeTimeouts
    )

    # Build list of commands to import
    $commands = @('Write-Status', 'Get-LocalizedString')

    if ($IncludeFeatureFlags) {
        $commands += 'Test-FeatureEnabled'
    }

    if ($IncludeTimeouts) {
        $commands += 'Get-TimeoutSettings'
    }

    $commands += $AdditionalCommands

    # Import all dependencies
    $success = Import-CoreDependencies -CommandNames $commands

    return @{
        RepositoryRoot = $script:RepositoryRoot
        Success = $success
        LoadedCommands = $commands | Where-Object { Get-Command -Name $_ -ErrorAction SilentlyContinue }
    }
}

function Get-ModuleLoadStatus {
    <#
    .SYNOPSIS
        Gets the current module loading status.
    .DESCRIPTION
        Returns information about which modules have been loaded
        during this session.
    .OUTPUTS
        [hashtable] Information about loaded modules.
    .EXAMPLE
        Get-ModuleLoadStatus
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    return @{
        LoadedModuleCount = $script:LoadedModules.Count
        LoadedModules = $script:LoadedModules.Keys | Sort-Object
        AvailableCommands = $script:CommandToModuleMap.Keys | Sort-Object
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Get-Win11ForgeRepositoryRoot',
    'Import-CoreDependency',
    'Import-CoreDependencies',
    'Initialize-Win11ForgeModule',
    'Get-ModuleLoadStatus'
)
