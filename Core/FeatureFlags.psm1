<#
.SYNOPSIS
    Win11Forge - Feature Flags Module v3.1.4

.DESCRIPTION
    Manages feature flags for conditional feature enablement.
    Allows runtime feature toggling without code changes.

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
$script:FeatureFlagsPath = Join-Path $script:RepositoryRoot 'Config\feature-flags.json'

# === STATE ===
$script:FeatureFlags = $null
$script:FlagsLoaded = $false
$script:RuntimeOverrides = @{}

# === DEFAULT FLAGS ===
$script:DefaultFlags = @{
    parallelInstallation     = $true
    structuredLogging        = $true
    restApi                  = $false
    apiAuthentication        = $true
    pluginSystem             = $false
    pluginSandboxing         = $false
    wingetCache              = $true
    autoRollback             = $true
    telemetryCollection      = $false
    telemetryDashboard       = $false
    userProfiles             = $true
    autoUpdate               = $false
    dryRunMode               = $true
    checksumValidation       = $true
    urlWhitelisting          = $true
    environmentRestrictions  = $true
    retryLogic               = $true
    officeSpecialHandling    = $true
}

# === PRIVATE FUNCTIONS ===

function Load-FeatureFlags {
    <#
    .SYNOPSIS
        Loads feature flags from configuration file.
    #>
    [CmdletBinding()]
    param()

    if ($script:FlagsLoaded) {
        return
    }

    try {
        if (Test-Path $script:FeatureFlagsPath) {
            $content = Get-Content -Path $script:FeatureFlagsPath -Raw -Encoding UTF8
            $config = $content | ConvertFrom-Json

            $script:FeatureFlags = @{}

            # Load main features
            if ($config.features) {
                foreach ($prop in $config.features.PSObject.Properties) {
                    $featureName = $prop.Name
                    $featureConfig = $prop.Value
                    $script:FeatureFlags[$featureName] = [bool]$featureConfig.enabled
                }
            }

            # Load experimental features
            if ($config.experimental) {
                foreach ($prop in $config.experimental.PSObject.Properties) {
                    if ($prop.Name -ne 'description') {
                        $featureName = $prop.Name
                        $featureConfig = $prop.Value
                        if ($featureConfig.PSObject.Properties.Name -contains 'enabled') {
                            $script:FeatureFlags[$featureName] = [bool]$featureConfig.enabled
                        }
                    }
                }
            }

            $script:FlagsLoaded = $true
        }
        else {
            # Use defaults if file doesn't exist
            $script:FeatureFlags = $script:DefaultFlags.Clone()
            $script:FlagsLoaded = $true
        }
    }
    catch {
        # Use defaults on error
        $script:FeatureFlags = $script:DefaultFlags.Clone()
        $script:FlagsLoaded = $true
        Write-Warning "Failed to load feature flags: $($_.Exception.Message). Using defaults."
    }
}

# === PUBLIC FUNCTIONS ===

function Initialize-FeatureFlags {
    <#
    .SYNOPSIS
        Initializes the feature flags system.
    .DESCRIPTION
        Loads feature flags from configuration and prepares for runtime use.
    .PARAMETER Force
        Force reload of feature flags.
    #>
    [CmdletBinding()]
    param(
        [switch]$Force
    )

    if ($Force) {
        $script:FlagsLoaded = $false
        $script:RuntimeOverrides = @{}
    }

    Load-FeatureFlags
}

function Test-FeatureEnabled {
    <#
    .SYNOPSIS
        Tests if a feature is enabled.
    .PARAMETER FeatureName
        Name of the feature to test.
    .OUTPUTS
        Boolean indicating if feature is enabled.
    .EXAMPLE
        if (Test-FeatureEnabled -FeatureName 'parallelInstallation') { ... }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName
    )

    # Ensure flags are loaded
    if (-not $script:FlagsLoaded) {
        Load-FeatureFlags
    }

    # Check runtime override first
    if ($script:RuntimeOverrides.ContainsKey($FeatureName)) {
        return $script:RuntimeOverrides[$FeatureName]
    }

    # Check loaded flags
    if ($script:FeatureFlags.ContainsKey($FeatureName)) {
        return $script:FeatureFlags[$FeatureName]
    }

    # Check defaults
    if ($script:DefaultFlags.ContainsKey($FeatureName)) {
        return $script:DefaultFlags[$FeatureName]
    }

    # Unknown feature = disabled
    return $false
}

function Set-FeatureOverride {
    <#
    .SYNOPSIS
        Sets a runtime override for a feature flag.
    .DESCRIPTION
        Allows temporary override of feature flags without modifying configuration.
        Overrides are not persisted and reset on module reload.
    .PARAMETER FeatureName
        Name of the feature to override.
    .PARAMETER Enabled
        Whether to enable or disable the feature.
    .EXAMPLE
        Set-FeatureOverride -FeatureName 'pluginSystem' -Enabled $true
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName,

        [Parameter(Mandatory)]
        [bool]$Enabled
    )

    $script:RuntimeOverrides[$FeatureName] = $Enabled
    Write-Verbose "Feature '$FeatureName' overridden to: $Enabled"
}

function Remove-FeatureOverride {
    <#
    .SYNOPSIS
        Removes a runtime override for a feature flag.
    .PARAMETER FeatureName
        Name of the feature override to remove.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName
    )

    if ($script:RuntimeOverrides.ContainsKey($FeatureName)) {
        $script:RuntimeOverrides.Remove($FeatureName)
        Write-Verbose "Feature override removed: $FeatureName"
    }
}

function Clear-FeatureOverrides {
    <#
    .SYNOPSIS
        Clears all runtime feature overrides.
    #>
    [CmdletBinding()]
    param()

    $script:RuntimeOverrides = @{}
    Write-Verbose "All feature overrides cleared"
}

function Get-AllFeatureFlags {
    <#
    .SYNOPSIS
        Returns all feature flags and their current status.
    .OUTPUTS
        Array of feature flag objects.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param()

    # Ensure flags are loaded
    if (-not $script:FlagsLoaded) {
        Load-FeatureFlags
    }

    $result = @()

    # Combine all known features
    $allFeatures = @{}
    foreach ($key in $script:DefaultFlags.Keys) {
        $allFeatures[$key] = $script:DefaultFlags[$key]
    }
    foreach ($key in $script:FeatureFlags.Keys) {
        $allFeatures[$key] = $script:FeatureFlags[$key]
    }

    foreach ($feature in $allFeatures.Keys | Sort-Object) {
        $configValue = $script:FeatureFlags[$feature]
        $defaultValue = $script:DefaultFlags[$feature]
        $overrideValue = $script:RuntimeOverrides[$feature]
        $effectiveValue = Test-FeatureEnabled -FeatureName $feature

        $result += [PSCustomObject]@{
            FeatureName    = $feature
            Enabled        = $effectiveValue
            ConfigValue    = $configValue
            DefaultValue   = $defaultValue
            HasOverride    = $script:RuntimeOverrides.ContainsKey($feature)
            OverrideValue  = $overrideValue
        }
    }

    return $result
}

function Get-EnabledFeatures {
    <#
    .SYNOPSIS
        Returns list of all enabled features.
    .OUTPUTS
        Array of enabled feature names.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param()

    $allFlags = Get-AllFeatureFlags
    return @($allFlags | Where-Object { $_.Enabled } | Select-Object -ExpandProperty FeatureName)
}

function Get-DisabledFeatures {
    <#
    .SYNOPSIS
        Returns list of all disabled features.
    .OUTPUTS
        Array of disabled feature names.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param()

    $allFlags = Get-AllFeatureFlags
    return @($allFlags | Where-Object { -not $_.Enabled } | Select-Object -ExpandProperty FeatureName)
}

function Save-FeatureFlags {
    <#
    .SYNOPSIS
        Saves current feature flag state to configuration file.
    .DESCRIPTION
        Persists any runtime overrides to the configuration file.
    .PARAMETER IncludeOverrides
        If set, includes runtime overrides in the saved configuration.
    #>
    [CmdletBinding()]
    param(
        [switch]$IncludeOverrides
    )

    try {
        $content = Get-Content -Path $script:FeatureFlagsPath -Raw -Encoding UTF8
        $config = $content | ConvertFrom-Json

        # Update features with current state
        if ($IncludeOverrides) {
            foreach ($feature in $script:RuntimeOverrides.Keys) {
                if ($config.features.PSObject.Properties.Name -contains $feature) {
                    $config.features.$feature.enabled = $script:RuntimeOverrides[$feature]
                }
                elseif ($config.experimental.PSObject.Properties.Name -contains $feature) {
                    $config.experimental.$feature.enabled = $script:RuntimeOverrides[$feature]
                }
            }
        }

        # Update lastUpdated
        $config.lastUpdated = (Get-Date).ToString('yyyy-MM-dd')

        # Save to file
        $config | ConvertTo-Json -Depth 10 | Set-Content -Path $script:FeatureFlagsPath -Encoding UTF8

        Write-Verbose "Feature flags saved to $script:FeatureFlagsPath"
    }
    catch {
        Write-Error "Failed to save feature flags: $($_.Exception.Message)"
    }
}

function Invoke-WithFeature {
    <#
    .SYNOPSIS
        Executes a script block only if a feature is enabled.
    .PARAMETER FeatureName
        Name of the feature to check.
    .PARAMETER ScriptBlock
        Script block to execute if feature is enabled.
    .PARAMETER Fallback
        Optional fallback script block if feature is disabled.
    .EXAMPLE
        Invoke-WithFeature -FeatureName 'parallelInstallation' -ScriptBlock {
            Install-ApplicationsParallel $apps
        } -Fallback {
            Install-Applications $apps
        }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName,

        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock,

        [scriptblock]$Fallback
    )

    if (Test-FeatureEnabled -FeatureName $FeatureName) {
        & $ScriptBlock
    }
    elseif ($Fallback) {
        & $Fallback
    }
}

# Alias for convenience
Set-Alias -Name 'ff' -Value 'Test-FeatureEnabled'

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-FeatureFlags',
    'Test-FeatureEnabled',
    'Set-FeatureOverride',
    'Remove-FeatureOverride',
    'Clear-FeatureOverrides',
    'Get-AllFeatureFlags',
    'Get-EnabledFeatures',
    'Get-DisabledFeatures',
    'Save-FeatureFlags',
    'Invoke-WithFeature'
) -Alias @('ff')
