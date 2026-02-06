<#
.SYNOPSIS
    Win11Forge - Timeout Settings v3.6.8

.DESCRIPTION
    Provides centralized timeout configuration loading for Win11Forge:
    - Loads timeout settings from Config/timeouts-settings.json
    - Provides fallback defaults if config is unavailable
    - Caches configuration for performance

.NOTES
    Author: Julien Bombled
    v3.6.8
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
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\timeouts-settings.json'

# === CACHED CONFIGURATION ===
$script:TimeoutConfig = $null
$script:ConfigLoadedAt = $null
$script:CacheTtlMinutes = 5

# === DEFAULT CONFIGURATION ===
$script:DefaultTimeouts = @{
    Installation = @{
        DefaultTimeoutSeconds = 1800      # 30 minutes
        OfficeTimeoutSeconds = 2700       # 45 minutes
    }
    Parallel = @{
        TimeoutMilliseconds = 600000      # 10 minutes
        MaxParallelJobs = 5
        JobCheckIntervalSeconds = 2
    }
    Download = @{
        TimeoutSeconds = 300              # 5 minutes
        RetryDelaySeconds = 5
        MaxRetries = 3
    }
    Detection = @{
        RegistryCacheTtlMinutes = 5
        WingetCacheTtlMinutes = 30
        SearchCacheTtlMinutes = 60
    }
    Api = @{
        RequestTimeoutMs = 30000          # 30 seconds
        ShutdownTimeoutMs = 5000          # 5 seconds
    }
    Plugin = @{
        ExecutionTimeoutSeconds = 30
        LoadTimeoutSeconds = 10
    }
}

# === CONFIGURATION LOADING ===

function Get-TimeoutSettings {
    <#
    .SYNOPSIS
        Loads and returns the timeout configuration.
    .DESCRIPTION
        Loads timeout settings from Config/timeouts-settings.json with caching.
        Falls back to default values if the config file is unavailable.
    .PARAMETER Force
        Force reload from disk, ignoring cache.
    .OUTPUTS
        Hashtable containing timeout configuration.
    .EXAMPLE
        $timeouts = Get-TimeoutSettings
        $installTimeout = $timeouts.Installation.DefaultTimeoutSeconds
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$Force
    )

    # Check cache validity
    $cacheValid = $script:TimeoutConfig -and $script:ConfigLoadedAt -and `
        ((Get-Date) - $script:ConfigLoadedAt).TotalMinutes -lt $script:CacheTtlMinutes

    if ($cacheValid -and -not $Force) {
        return $script:TimeoutConfig
    }

    # Load from file
    if (Test-Path $script:ConfigPath) {
        try {
            $json = Get-Content $script:ConfigPath -Raw -ErrorAction Stop | ConvertFrom-Json

            $script:TimeoutConfig = @{
                Installation = @{
                    DefaultTimeoutSeconds = if ($null -ne $json.installation.defaultTimeoutSeconds) {
                        $json.installation.defaultTimeoutSeconds
                    } else {
                        $script:DefaultTimeouts.Installation.DefaultTimeoutSeconds
                    }
                    OfficeTimeoutSeconds = if ($null -ne $json.installation.officeTimeoutSeconds) {
                        $json.installation.officeTimeoutSeconds
                    } else {
                        $script:DefaultTimeouts.Installation.OfficeTimeoutSeconds
                    }
                }
                Parallel = @{
                    TimeoutMilliseconds = if ($null -ne $json.parallel.timeoutMilliseconds) {
                        $json.parallel.timeoutMilliseconds
                    } else {
                        $script:DefaultTimeouts.Parallel.TimeoutMilliseconds
                    }
                    MaxParallelJobs = if ($null -ne $json.parallel.maxParallelJobs) {
                        $json.parallel.maxParallelJobs
                    } else {
                        $script:DefaultTimeouts.Parallel.MaxParallelJobs
                    }
                    JobCheckIntervalSeconds = if ($null -ne $json.parallel.jobCheckIntervalSeconds) {
                        $json.parallel.jobCheckIntervalSeconds
                    } else {
                        $script:DefaultTimeouts.Parallel.JobCheckIntervalSeconds
                    }
                }
                Download = @{
                    TimeoutSeconds = if ($null -ne $json.download.timeoutSeconds) {
                        $json.download.timeoutSeconds
                    } else {
                        $script:DefaultTimeouts.Download.TimeoutSeconds
                    }
                    RetryDelaySeconds = if ($null -ne $json.download.retryDelaySeconds) {
                        $json.download.retryDelaySeconds
                    } else {
                        $script:DefaultTimeouts.Download.RetryDelaySeconds
                    }
                    MaxRetries = if ($null -ne $json.download.maxRetries) {
                        $json.download.maxRetries
                    } else {
                        $script:DefaultTimeouts.Download.MaxRetries
                    }
                }
                Detection = @{
                    RegistryCacheTtlMinutes = if ($null -ne $json.detection.registryCacheTtlMinutes) {
                        $json.detection.registryCacheTtlMinutes
                    } else {
                        $script:DefaultTimeouts.Detection.RegistryCacheTtlMinutes
                    }
                    WingetCacheTtlMinutes = if ($null -ne $json.detection.wingetCacheTtlMinutes) {
                        $json.detection.wingetCacheTtlMinutes
                    } else {
                        $script:DefaultTimeouts.Detection.WingetCacheTtlMinutes
                    }
                    SearchCacheTtlMinutes = if ($null -ne $json.detection.searchCacheTtlMinutes) {
                        $json.detection.searchCacheTtlMinutes
                    } else {
                        $script:DefaultTimeouts.Detection.SearchCacheTtlMinutes
                    }
                }
                Api = @{
                    RequestTimeoutMs = if ($null -ne $json.api.requestTimeoutMs) {
                        $json.api.requestTimeoutMs
                    } else {
                        $script:DefaultTimeouts.Api.RequestTimeoutMs
                    }
                    ShutdownTimeoutMs = if ($null -ne $json.api.shutdownTimeoutMs) {
                        $json.api.shutdownTimeoutMs
                    } else {
                        $script:DefaultTimeouts.Api.ShutdownTimeoutMs
                    }
                }
                Plugin = @{
                    ExecutionTimeoutSeconds = if ($null -ne $json.plugin.executionTimeoutSeconds) {
                        $json.plugin.executionTimeoutSeconds
                    } else {
                        $script:DefaultTimeouts.Plugin.ExecutionTimeoutSeconds
                    }
                    LoadTimeoutSeconds = if ($null -ne $json.plugin.loadTimeoutSeconds) {
                        $json.plugin.loadTimeoutSeconds
                    } else {
                        $script:DefaultTimeouts.Plugin.LoadTimeoutSeconds
                    }
                }
            }

            $script:ConfigLoadedAt = Get-Date
            return $script:TimeoutConfig
        } catch {
            Write-Warning "Failed to load timeout settings: $($_.Exception.Message). Using defaults."
        }
    }

    # Return defaults
    $script:TimeoutConfig = $script:DefaultTimeouts
    $script:ConfigLoadedAt = Get-Date
    return $script:TimeoutConfig
}

function Get-InstallationTimeout {
    <#
    .SYNOPSIS
        Gets the installation timeout for a specific application.
    .PARAMETER AppName
        Name of the application (used to detect Office apps).
    .OUTPUTS
        Timeout in seconds.
    .EXAMPLE
        $timeout = Get-InstallationTimeout -AppName "Microsoft Office"
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter()]
        [string]$AppName = ''
    )

    $config = Get-TimeoutSettings

    # Check if this is an Office application
    if ($AppName -match 'Office|Microsoft 365|Word|Excel|PowerPoint|Outlook|OneNote|Access|Publisher') {
        return $config.Installation.OfficeTimeoutSeconds
    }

    return $config.Installation.DefaultTimeoutSeconds
}

function Get-ParallelTimeout {
    <#
    .SYNOPSIS
        Gets the parallel installation timeout in milliseconds.
    .OUTPUTS
        Timeout in milliseconds.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    $config = Get-TimeoutSettings
    return $config.Parallel.TimeoutMilliseconds
}

function Get-MaxParallelJobs {
    <#
    .SYNOPSIS
        Gets the maximum number of parallel installation jobs.
    .OUTPUTS
        Maximum parallel jobs.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    $config = Get-TimeoutSettings
    return $config.Parallel.MaxParallelJobs
}

function Get-DownloadTimeout {
    <#
    .SYNOPSIS
        Gets the download timeout in seconds.
    .OUTPUTS
        Timeout in seconds.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    $config = Get-TimeoutSettings
    return $config.Download.TimeoutSeconds
}

function Get-CacheTtl {
    <#
    .SYNOPSIS
        Gets cache TTL settings.
    .PARAMETER CacheType
        Type of cache (Registry, Winget, Search).
    .OUTPUTS
        TTL in minutes.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Registry', 'Winget', 'Search')]
        [string]$CacheType
    )

    $config = Get-TimeoutSettings

    switch ($CacheType) {
        'Registry' { return $config.Detection.RegistryCacheTtlMinutes }
        'Winget' { return $config.Detection.WingetCacheTtlMinutes }
        'Search' { return $config.Detection.SearchCacheTtlMinutes }
    }
}

function Get-PluginTimeout {
    <#
    .SYNOPSIS
        Gets plugin timeout settings.
    .PARAMETER Operation
        Type of operation (Execution, Load).
    .OUTPUTS
        Timeout in seconds.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Execution', 'Load')]
        [string]$Operation
    )

    $config = Get-TimeoutSettings

    switch ($Operation) {
        'Execution' { return $config.Plugin.ExecutionTimeoutSeconds }
        'Load' { return $config.Plugin.LoadTimeoutSeconds }
    }
}

function Get-ApiTimeout {
    <#
    .SYNOPSIS
        Gets API timeout settings.
    .PARAMETER Operation
        Type of operation (Request, Shutdown).
    .OUTPUTS
        Timeout in milliseconds.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Request', 'Shutdown')]
        [string]$Operation
    )

    $config = Get-TimeoutSettings

    switch ($Operation) {
        'Request' { return $config.Api.RequestTimeoutMs }
        'Shutdown' { return $config.Api.ShutdownTimeoutMs }
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Get-TimeoutSettings',
    'Get-InstallationTimeout',
    'Get-ParallelTimeout',
    'Get-MaxParallelJobs',
    'Get-DownloadTimeout',
    'Get-CacheTtl',
    'Get-PluginTimeout',
    'Get-ApiTimeout'
)
