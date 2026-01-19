<#
.SYNOPSIS
    Win11Forge - Plugin Manager Module v3.1.4

.DESCRIPTION
    Provides plugin system functionality for Win11Forge:
    - Plugin discovery and loading
    - Hook system (pre-install, post-install, etc.)
    - Custom installation method registration
    - Plugin lifecycle management
    - Plugin validation and sandboxing

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
$script:CoreModulePath = Join-Path $script:ModuleRoot 'Core.psm1'
$script:PluginsDir = Join-Path $script:RepositoryRoot 'Plugins'
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\plugins-settings.json'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import PluginSandbox module for sandboxed execution
$script:PluginSandboxPath = Join-Path $script:ModuleRoot 'PluginSandbox.psm1'
$script:SandboxingEnabled = $false
if (Test-Path -Path $script:PluginSandboxPath) {
    try {
        Import-Module -Name $script:PluginSandboxPath -Force -ErrorAction Stop
        $script:SandboxingEnabled = $true
    } catch {
        Write-Verbose "Plugin sandboxing not available: $($_.Exception.Message)"
    }
}

# Import FeatureFlags module
$script:FeatureFlagsPath = Join-Path $script:ModuleRoot 'FeatureFlags.psm1'
if (Test-Path -Path $script:FeatureFlagsPath) {
    Import-Module -Name $script:FeatureFlagsPath -Force -ErrorAction SilentlyContinue
}

# === PLUGIN STATE ===
$script:PluginState = @{
    LoadedPlugins = @{}
    RegisteredHooks = @{
        'pre-install' = @()
        'post-install' = @()
        'pre-uninstall' = @()
        'post-uninstall' = @()
        'pre-deployment' = @()
        'post-deployment' = @()
        'on-error' = @()
    }
    CustomMethods = @{}
    Initialized = $false
}

# === CONFIGURATION ===
$script:DefaultConfig = @{
    Enabled = $true
    AutoLoad = $true
    AllowedHooks = @(
        'pre-install',
        'post-install',
        'pre-uninstall',
        'post-uninstall',
        'pre-deployment',
        'post-deployment',
        'on-error'
    )
    DisabledPlugins = @()
    PluginTimeout = 30
}

# === INITIALIZATION ===

function Initialize-PluginManager {
    <#
    .SYNOPSIS
        Initializes the plugin manager.

    .DESCRIPTION
        Creates the plugins directory structure and loads enabled plugins.

    .PARAMETER AutoLoad
        Automatically load all discovered plugins.

    .EXAMPLE
        Initialize-PluginManager
        Initialize-PluginManager -AutoLoad:$false
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$AutoLoad = $true
    )

    # Create plugins directory if it doesn't exist
    if (-not (Test-Path $script:PluginsDir)) {
        try {
            New-Item -Path $script:PluginsDir -ItemType Directory -Force | Out-Null
            Write-Verbose "Created plugins directory: $script:PluginsDir"
        } catch {
            Write-Warning "Failed to create plugins directory: $($_.Exception.Message)"
        }
    }

    # Load configuration
    $config = Get-PluginConfig

    $script:PluginState.Initialized = $true

    # Auto-load plugins if enabled
    if ($AutoLoad -and $config.AutoLoad) {
        $plugins = Get-AvailablePlugins
        foreach ($plugin in $plugins) {
            if ($plugin.Name -notin $config.DisabledPlugins) {
                try {
                    Import-Plugin -Name $plugin.Name
                } catch {
                    Write-Warning "Failed to load plugin '$($plugin.Name)': $($_.Exception.Message)"
                }
            }
        }
    }

    Write-Verbose "Plugin manager initialized"
}

function Get-PluginConfig {
    <#
    .SYNOPSIS
        Loads and returns the plugin configuration.

    .OUTPUTS
        Hashtable containing plugin configuration.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if (Test-Path $script:ConfigPath) {
        try {
            $json = Get-Content $script:ConfigPath -Raw | ConvertFrom-Json
            return @{
                Enabled = if ($null -ne $json.enabled) { $json.enabled } else { $script:DefaultConfig.Enabled }
                AutoLoad = if ($null -ne $json.autoLoad) { $json.autoLoad } else { $script:DefaultConfig.AutoLoad }
                AllowedHooks = if ($null -ne $json.allowedHooks) { @($json.allowedHooks) } else { $script:DefaultConfig.AllowedHooks }
                DisabledPlugins = if ($null -ne $json.disabledPlugins) { @($json.disabledPlugins) } else { $script:DefaultConfig.DisabledPlugins }
                PluginTimeout = if ($null -ne $json.pluginTimeout) { $json.pluginTimeout } else { $script:DefaultConfig.PluginTimeout }
            }
        } catch {
            return $script:DefaultConfig
        }
    }

    return $script:DefaultConfig
}

# === PLUGIN DISCOVERY ===

function Get-AvailablePlugins {
    <#
    .SYNOPSIS
        Lists all available plugins.

    .DESCRIPTION
        Discovers plugins in the Plugins directory by looking for manifest.json files.

    .OUTPUTS
        Array of plugin information objects.

    .EXAMPLE
        Get-AvailablePlugins | Format-Table
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $plugins = @()

    if (-not (Test-Path $script:PluginsDir)) {
        return $plugins
    }

    $pluginDirs = Get-ChildItem -Path $script:PluginsDir -Directory -ErrorAction SilentlyContinue

    foreach ($dir in $pluginDirs) {
        $manifestPath = Join-Path $dir.FullName 'manifest.json'

        if (Test-Path $manifestPath) {
            try {
                $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

                $plugins += [PSCustomObject]@{
                    Name = $manifest.name
                    Version = $manifest.version
                    Description = $manifest.description
                    Author = $manifest.author
                    EntryPoint = $manifest.entryPoint
                    Hooks = if ($manifest.hooks) { @($manifest.hooks) } else { @() }
                    InstallationMethods = if ($manifest.installationMethods) { @($manifest.installationMethods) } else { @() }
                    Dependencies = if ($manifest.dependencies) { @($manifest.dependencies) } else { @() }
                    Path = $dir.FullName
                    ManifestPath = $manifestPath
                    Loaded = $script:PluginState.LoadedPlugins.ContainsKey($manifest.name)
                }
            } catch {
                Write-Verbose "Failed to parse manifest for plugin: $($dir.Name)"
            }
        }
    }

    return $plugins
}

function Get-LoadedPlugins {
    <#
    .SYNOPSIS
        Lists currently loaded plugins.

    .OUTPUTS
        Array of loaded plugin information.

    .EXAMPLE
        Get-LoadedPlugins
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    return $script:PluginState.LoadedPlugins.Values | ForEach-Object {
        [PSCustomObject]$_
    }
}

# === PLUGIN LOADING ===

function Import-Plugin {
    <#
    .SYNOPSIS
        Loads a plugin.

    .DESCRIPTION
        Loads a plugin by name, executing its entry point and registering hooks.

    .PARAMETER Name
        Name of the plugin to load.

    .PARAMETER Force
        Force reload if already loaded.

    .EXAMPLE
        Import-Plugin -Name 'my-custom-plugin'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter()]
        [switch]$Force
    )

    $config = Get-PluginConfig
    if (-not $config.Enabled) {
        Write-Warning "Plugin system is disabled"
        return
    }

    # Check if already loaded
    if ($script:PluginState.LoadedPlugins.ContainsKey($Name) -and -not $Force) {
        Write-Verbose "Plugin '$Name' is already loaded"
        return
    }

    # Find the plugin
    $plugin = Get-AvailablePlugins | Where-Object { $_.Name -eq $Name } | Select-Object -First 1

    if (-not $plugin) {
        throw "Plugin not found: $Name"
    }

    # Validate manifest
    if (-not (Test-PluginManifest -ManifestPath $plugin.ManifestPath)) {
        throw "Invalid plugin manifest: $Name"
    }

    # Load entry point module with path traversal protection
    $entryPointPath = Join-Path $plugin.Path $plugin.EntryPoint

    # Security: Validate resolved path stays within plugin directory
    $canonicalPluginPath = [System.IO.Path]::GetFullPath($plugin.Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $canonicalEntryPath = [System.IO.Path]::GetFullPath($entryPointPath)

    if (-not $canonicalEntryPath.StartsWith($canonicalPluginPath + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Security: Plugin entry point escapes plugin directory: $($plugin.EntryPoint)"
    }

    if (-not (Test-Path $entryPointPath)) {
        throw "Plugin entry point not found: $entryPointPath"
    }

    Write-Status -Message "Loading plugin: $Name" -Level 'Info' -Category 'Plugin'

    try {
        # Import the plugin module
        Import-Module $entryPointPath -Force -ErrorAction Stop

        # Register hooks
        foreach ($hook in $plugin.Hooks) {
            if ($hook -in $config.AllowedHooks) {
                # Look for hook function in plugin
                $hookFunctionName = "Invoke-$Name-$hook" -replace '-', ''
                if (Get-Command -Name $hookFunctionName -ErrorAction SilentlyContinue) {
                    Register-PluginHook -HookName $hook -PluginName $Name -Handler (Get-Command $hookFunctionName)
                }
            }
        }

        # Register custom installation methods
        foreach ($method in $plugin.InstallationMethods) {
            $methodFunctionName = "Install-$Name-$method" -replace '-', ''
            if (Get-Command -Name $methodFunctionName -ErrorAction SilentlyContinue) {
                Register-CustomInstallMethod -MethodName $method -PluginName $Name -Handler (Get-Command $methodFunctionName)
            }
        }

        # Record as loaded
        $script:PluginState.LoadedPlugins[$Name] = @{
            Name = $Name
            Version = $plugin.Version
            Path = $plugin.Path
            LoadedAt = Get-Date
            Hooks = $plugin.Hooks
            Methods = $plugin.InstallationMethods
        }

        Write-Status -Message "Plugin loaded: $Name v$($plugin.Version)" -Level 'Success' -Category 'Plugin'

    } catch {
        Write-Status -Message "Failed to load plugin '$Name': $($_.Exception.Message)" -Level 'Error' -Category 'Plugin'
        throw
    }
}

function Unload-Plugin {
    <#
    .SYNOPSIS
        Unloads a plugin.

    .PARAMETER Name
        Name of the plugin to unload.

    .EXAMPLE
        Unload-Plugin -Name 'my-custom-plugin'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    if (-not $script:PluginState.LoadedPlugins.ContainsKey($Name)) {
        Write-Warning "Plugin '$Name' is not loaded"
        return
    }

    $plugin = $script:PluginState.LoadedPlugins[$Name]

    # Unregister hooks
    foreach ($hookName in $script:PluginState.RegisteredHooks.Keys) {
        $script:PluginState.RegisteredHooks[$hookName] = @(
            $script:PluginState.RegisteredHooks[$hookName] | Where-Object { $_.PluginName -ne $Name }
        )
    }

    # Unregister custom methods
    $methodsToRemove = @($script:PluginState.CustomMethods.Keys | Where-Object { $script:PluginState.CustomMethods[$_].PluginName -eq $Name })
    foreach ($method in $methodsToRemove) {
        $script:PluginState.CustomMethods.Remove($method)
    }

    # Remove from loaded list
    $script:PluginState.LoadedPlugins.Remove($Name)

    Write-Status -Message "Plugin unloaded: $Name" -Level 'Info' -Category 'Plugin'
}

# === HOOK SYSTEM ===

function Register-PluginHook {
    <#
    .SYNOPSIS
        Registers a hook handler.

    .PARAMETER HookName
        Name of the hook (e.g., 'pre-install').

    .PARAMETER PluginName
        Name of the plugin registering the hook.

    .PARAMETER Handler
        Command or script block to execute.

    .EXAMPLE
        Register-PluginHook -HookName 'post-install' -PluginName 'MyPlugin' -Handler $handlerCommand
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$HookName,

        [Parameter(Mandatory)]
        [string]$PluginName,

        [Parameter(Mandatory)]
        $Handler
    )

    if (-not $script:PluginState.RegisteredHooks.ContainsKey($HookName)) {
        Write-Warning "Unknown hook: $HookName"
        return
    }

    $script:PluginState.RegisteredHooks[$HookName] += @{
        PluginName = $PluginName
        Handler = $Handler
        RegisteredAt = Get-Date
    }

    Write-Verbose "Registered hook '$HookName' for plugin '$PluginName'"
}

function Invoke-PluginHook {
    <#
    .SYNOPSIS
        Invokes all handlers for a specific hook.

    .DESCRIPTION
        Executes all registered handlers for the specified hook in order.
        When sandboxing is enabled (via feature flag), handlers run in
        isolated jobs with timeout enforcement.

    .PARAMETER HookName
        Name of the hook to invoke.

    .PARAMETER Context
        Context data to pass to handlers.

    .PARAMETER ForceSandbox
        Force sandboxed execution even if feature flag is disabled.

    .OUTPUTS
        Array of handler results.

    .EXAMPLE
        Invoke-PluginHook -HookName 'pre-install' -Context @{ AppName = 'VSCode' }
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$HookName,

        [Parameter()]
        [hashtable]$Context = @{},

        [Parameter()]
        [switch]$ForceSandbox
    )

    $config = Get-PluginConfig
    $results = @()

    if (-not $script:PluginState.RegisteredHooks.ContainsKey($HookName)) {
        return $results
    }

    $handlers = $script:PluginState.RegisteredHooks[$HookName]

    if ($handlers.Count -eq 0) {
        return $results
    }

    # Check if sandboxing should be used
    $useSandbox = $ForceSandbox.IsPresent
    if (-not $useSandbox -and $script:SandboxingEnabled) {
        # Check feature flag
        if (Get-Command -Name Test-FeatureEnabled -ErrorAction SilentlyContinue) {
            $useSandbox = Test-FeatureEnabled -FeatureName 'pluginSandboxing'
        }
    }

    if ($useSandbox -and (Get-Command -Name Invoke-PluginHookSandboxed -ErrorAction SilentlyContinue)) {
        # Use sandboxed execution
        Write-Status -Message "Invoking hook '$HookName' with sandboxing ($($handlers.Count) handler(s))" -Level 'Verbose' -Category 'Plugin'
        return Invoke-PluginHookSandboxed -HookName $HookName -Context $Context -Handlers $handlers
    }

    # Standard (non-sandboxed) execution
    foreach ($handler in $handlers) {
        try {
            Write-Verbose "Invoking hook '$HookName' from plugin '$($handler.PluginName)'"

            $result = & $handler.Handler $Context

            $results += @{
                Plugin = $handler.PluginName
                Success = $true
                Result = $result
            }
        } catch {
            Write-Warning "Hook '$HookName' failed for plugin '$($handler.PluginName)': $($_.Exception.Message)"

            $results += @{
                Plugin = $handler.PluginName
                Success = $false
                Error = $_.Exception.Message
            }
        }
    }

    return $results
}

# === CUSTOM INSTALLATION METHODS ===

function Register-CustomInstallMethod {
    <#
    .SYNOPSIS
        Registers a custom installation method.

    .PARAMETER MethodName
        Name of the installation method.

    .PARAMETER PluginName
        Name of the plugin providing the method.

    .PARAMETER Handler
        Installation handler command.

    .EXAMPLE
        Register-CustomInstallMethod -MethodName 'CustomSource' -PluginName 'MyPlugin' -Handler $installCommand
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$MethodName,

        [Parameter(Mandatory)]
        [string]$PluginName,

        [Parameter(Mandatory)]
        $Handler
    )

    $script:PluginState.CustomMethods[$MethodName] = @{
        PluginName = $PluginName
        Handler = $Handler
        RegisteredAt = Get-Date
    }

    Write-Verbose "Registered custom install method '$MethodName' from plugin '$PluginName'"
}

function Get-CustomInstallMethod {
    <#
    .SYNOPSIS
        Gets a custom installation method handler.

    .PARAMETER MethodName
        Name of the method.

    .OUTPUTS
        Handler object or null.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$MethodName
    )

    if ($script:PluginState.CustomMethods.ContainsKey($MethodName)) {
        return $script:PluginState.CustomMethods[$MethodName]
    }

    return $null
}

function Get-RegisteredInstallMethods {
    <#
    .SYNOPSIS
        Lists all registered custom installation methods.

    .OUTPUTS
        Array of method information.
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    return $script:PluginState.CustomMethods.GetEnumerator() | ForEach-Object {
        [PSCustomObject]@{
            Method = $_.Key
            PluginName = $_.Value.PluginName
            RegisteredAt = $_.Value.RegisteredAt
        }
    }
}

# === VALIDATION ===

function Test-PluginManifest {
    <#
    .SYNOPSIS
        Validates a plugin manifest.

    .PARAMETER ManifestPath
        Path to the manifest.json file.

    .OUTPUTS
        Boolean indicating if manifest is valid.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$ManifestPath
    )

    if (-not (Test-Path $ManifestPath)) {
        return $false
    }

    try {
        $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json

        # Required fields
        $requiredFields = @('name', 'version', 'entryPoint')

        foreach ($field in $requiredFields) {
            if (-not $manifest.$field) {
                Write-Verbose "Missing required field: $field"
                return $false
            }
        }

        # Validate name format
        if ($manifest.name -notmatch '^[a-zA-Z0-9_-]+$') {
            Write-Verbose "Invalid plugin name format"
            return $false
        }

        # Validate version format
        if ($manifest.version -notmatch '^\d+\.\d+\.\d+') {
            Write-Verbose "Invalid version format"
            return $false
        }

        # Security: Validate entryPoint for path traversal
        if ($manifest.entryPoint -match '\.\.') {
            Write-Verbose "Security: Path traversal detected in entryPoint"
            return $false
        }

        # Security: entryPoint must be a simple filename or relative path within plugin
        if ($manifest.entryPoint -match '^[/\\]' -or $manifest.entryPoint -match '^[a-zA-Z]:') {
            Write-Verbose "Security: Absolute paths not allowed in entryPoint"
            return $false
        }

        # Security: Validate entryPoint characters (only allow safe characters)
        if ($manifest.entryPoint -notmatch '^[a-zA-Z0-9_\-./\\]+\.psm1$') {
            Write-Verbose "Security: Invalid entryPoint format (must be .psm1 file)"
            return $false
        }

        return $true
    } catch {
        Write-Verbose "Failed to validate manifest: $($_.Exception.Message)"
        return $false
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-PluginManager',
    'Get-PluginConfig',
    'Get-AvailablePlugins',
    'Get-LoadedPlugins',
    'Import-Plugin',
    'Unload-Plugin',
    'Register-PluginHook',
    'Invoke-PluginHook',
    'Register-CustomInstallMethod',
    'Get-CustomInstallMethod',
    'Get-RegisteredInstallMethods',
    'Test-PluginManifest'
)
