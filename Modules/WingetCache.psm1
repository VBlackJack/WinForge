<#
.SYNOPSIS
    WinForge - Winget Cache v3.7.2

.DESCRIPTION
    Provides intelligent caching for Winget operations to reduce
    redundant API calls and improve performance:
    - Caches winget list output with configurable TTL (default: 30 min)
    - Caches winget search results with configurable TTL (default: 60 min)
    - Persists cache to disk for session continuity
    - Pre-warms cache at deployment start
    - Provides cache statistics and management functions

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
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# Import DirectoryConstants for path management
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-WinForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# === CACHE CONFIGURATION ===
$script:CacheConfig = @{
    ListTTLMinutes = 30
    SearchTTLMinutes = 60
    ChocoListTTLMinutes = 60
    MaxEntries = 500
    CacheDirectory = Get-WinForgeDirectory -DirectoryType 'Cache'
    CacheFileName = 'WingetCache.json'
}

# === CACHE STATE ===
$script:WingetCache = @{
    ListCache = $null
    ListCacheTime = $null
    SearchCache = @{}
    SearchCacheTimes = @{}
    Statistics = @{
        ListHits = 0
        ListMisses = 0
        SearchHits = 0
        SearchMisses = 0
        LastWarmup = $null
    }
}

# === CHOCOLATEY CACHE STATE ===
$script:ChocoCache = @{
    ListCache = $null
    ListCacheTime = $null
    Statistics = @{
        ListHits = 0
        ListMisses = 0
    }
}

# === INITIALIZATION FUNCTIONS ===

function Initialize-WingetCache {
    <#
    .SYNOPSIS
        Initializes the Winget cache system.

    .DESCRIPTION
        Sets up the cache directory, loads any existing cache from disk,
        and optionally pre-warms the cache with winget list data.

    .PARAMETER PreWarm
        If specified, pre-warms the cache by running winget list.

    .PARAMETER ListTTLMinutes
        TTL in minutes for winget list cache (default: 30)

    .PARAMETER SearchTTLMinutes
        TTL in minutes for winget search cache (default: 60)

    .EXAMPLE
        Initialize-WingetCache -PreWarm
    #>
    [CmdletBinding()]
    param(
        [switch]$PreWarm,
        [int]$ListTTLMinutes = 30,
        [int]$SearchTTLMinutes = 60
    )

    # Update configuration
    $script:CacheConfig.ListTTLMinutes = $ListTTLMinutes
    $script:CacheConfig.SearchTTLMinutes = $SearchTTLMinutes

    # Ensure cache directory exists
    $cacheDir = $script:CacheConfig.CacheDirectory
    if (-not (Test-Path -Path $cacheDir)) {
        try {
            New-Item -Path $cacheDir -ItemType Directory -Force | Out-Null
            Write-Verbose "Created cache directory: $cacheDir"
        } catch {
            Write-Warning "Failed to create cache directory: $_"
        }
    }

    # Load existing cache from disk
    $cachePath = Join-Path $cacheDir $script:CacheConfig.CacheFileName
    if (Test-Path -Path $cachePath) {
        try {
            $loadedCache = Get-Content -Path $cachePath -Raw | ConvertFrom-Json

            # Validate and restore list cache if still valid
            if ($loadedCache.ListCache -and $loadedCache.ListCacheTime) {
                $cacheTime = [DateTime]::Parse($loadedCache.ListCacheTime)
                $age = (Get-Date) - $cacheTime
                if ($age.TotalMinutes -lt $script:CacheConfig.ListTTLMinutes) {
                    $script:WingetCache.ListCache = $loadedCache.ListCache
                    $script:WingetCache.ListCacheTime = $cacheTime
                    Write-Verbose "Restored list cache from disk (age: $([math]::Round($age.TotalMinutes, 1)) min)"
                }
            }

            # Restore search cache entries that are still valid
            if ($loadedCache.SearchCache) {
                foreach ($key in $loadedCache.SearchCache.PSObject.Properties.Name) {
                    $entry = $loadedCache.SearchCache.$key
                    if ($entry.CacheTime) {
                        $cacheTime = [DateTime]::Parse($entry.CacheTime)
                        $age = (Get-Date) - $cacheTime
                        if ($age.TotalMinutes -lt $script:CacheConfig.SearchTTLMinutes) {
                            $script:WingetCache.SearchCache[$key] = $entry.Data
                            $script:WingetCache.SearchCacheTimes[$key] = $cacheTime
                        }
                    }
                }
                $restoredCount = $script:WingetCache.SearchCache.Count
                if ($restoredCount -gt 0) {
                    Write-Verbose "Restored $restoredCount search cache entries from disk"
                }
            }

            # Restore statistics
            if ($loadedCache.Statistics) {
                foreach ($stat in $loadedCache.Statistics.PSObject.Properties) {
                    if ($script:WingetCache.Statistics.ContainsKey($stat.Name)) {
                        $script:WingetCache.Statistics[$stat.Name] = $stat.Value
                    }
                }
            }
        } catch {
            Write-Verbose "Failed to load cache from disk: $_"
        }
    }

    # Pre-warm cache if requested
    if ($PreWarm) {
        Write-Verbose "Pre-warming winget list cache..."
        $null = Get-CachedWingetList -Force
        $script:WingetCache.Statistics.LastWarmup = Get-Date
    }
}

# === CACHE FUNCTIONS ===

function Get-CachedWingetList {
    <#
    .SYNOPSIS
        Gets cached winget list output, refreshing if expired.

    .DESCRIPTION
        Returns cached winget list output if still valid (within TTL),
        otherwise runs winget list and caches the result.

    .PARAMETER Force
        Forces a cache refresh regardless of TTL.

    .OUTPUTS
        String containing winget list output.

    .EXAMPLE
        $installed = Get-CachedWingetList
    #>
    [CmdletBinding()]
    param(
        [switch]$Force
    )

    # Check if winget is available
    if (-not (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        Write-Verbose "Winget not available"
        return ""
    }

    # Check cache validity
    $cacheValid = $false
    if (-not $Force -and $script:WingetCache.ListCache -and $script:WingetCache.ListCacheTime) {
        $age = (Get-Date) - $script:WingetCache.ListCacheTime
        if ($age.TotalMinutes -lt $script:CacheConfig.ListTTLMinutes) {
            $cacheValid = $true
        }
    }

    if ($cacheValid) {
        $script:WingetCache.Statistics.ListHits++
        Write-Verbose "Cache hit for winget list (age: $([math]::Round($age.TotalMinutes, 1)) min)"
        return $script:WingetCache.ListCache
    }

    # Cache miss - refresh
    $script:WingetCache.Statistics.ListMisses++
    Write-Verbose "Cache miss for winget list - refreshing..."

    try {
        $output = & winget list --accept-source-agreements 2>&1 | Out-String
        $script:WingetCache.ListCache = $output
        $script:WingetCache.ListCacheTime = Get-Date
        Write-Verbose "Cached winget list output ($($output.Length) chars)"
        return $output
    } catch {
        Write-Verbose "Error running winget list: $_"
        return ""
    }
}

function Get-CachedWingetSearch {
    <#
    .SYNOPSIS
        Gets cached winget search output, refreshing if expired.

    .DESCRIPTION
        Returns cached winget search output for a specific query if still valid,
        otherwise runs winget search and caches the result.

    .PARAMETER Query
        The search query string.

    .PARAMETER Force
        Forces a cache refresh regardless of TTL.

    .OUTPUTS
        String containing winget search output.

    .EXAMPLE
        $results = Get-CachedWingetSearch -Query "Firefox"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Query,

        [switch]$Force
    )

    # Check if winget is available
    if (-not (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        Write-Verbose "Winget not available"
        return ""
    }

    $cacheKey = $Query.ToLower().Trim()

    # Check cache validity
    $cacheValid = $false
    if (-not $Force -and $script:WingetCache.SearchCache.ContainsKey($cacheKey)) {
        $cacheTime = $script:WingetCache.SearchCacheTimes[$cacheKey]
        if ($cacheTime) {
            $age = (Get-Date) - $cacheTime
            if ($age.TotalMinutes -lt $script:CacheConfig.SearchTTLMinutes) {
                $cacheValid = $true
            }
        }
    }

    if ($cacheValid) {
        $script:WingetCache.Statistics.SearchHits++
        Write-Verbose "Cache hit for search '$Query' (age: $([math]::Round($age.TotalMinutes, 1)) min)"
        return $script:WingetCache.SearchCache[$cacheKey]
    }

    # Cache miss - refresh
    $script:WingetCache.Statistics.SearchMisses++
    Write-Verbose "Cache miss for search '$Query' - refreshing..."

    try {
        $output = & winget search $Query --accept-source-agreements 2>&1 | Out-String

        # Enforce max entries limit
        if ($script:WingetCache.SearchCache.Count -ge $script:CacheConfig.MaxEntries) {
            # Remove oldest entry
            $oldest = $script:WingetCache.SearchCacheTimes.GetEnumerator() |
                Sort-Object Value |
                Select-Object -First 1
            if ($oldest) {
                $script:WingetCache.SearchCache.Remove($oldest.Key)
                $script:WingetCache.SearchCacheTimes.Remove($oldest.Key)
                Write-Verbose "Removed oldest search cache entry: $($oldest.Key)"
            }
        }

        $script:WingetCache.SearchCache[$cacheKey] = $output
        $script:WingetCache.SearchCacheTimes[$cacheKey] = Get-Date
        Write-Verbose "Cached search results for '$Query' ($($output.Length) chars)"
        return $output
    } catch {
        Write-Verbose "Error running winget search: $_"
        return ""
    }
}

function Update-WingetListCache {
    <#
    .SYNOPSIS
        Forces an update of the winget list cache.

    .DESCRIPTION
        Refreshes the winget list cache regardless of current TTL.

    .EXAMPLE
        Update-WingetListCache
    #>
    [CmdletBinding()]
    param()

    $null = Get-CachedWingetList -Force
    Write-Verbose "Winget list cache updated"
}

function Clear-WingetCache {
    <#
    .SYNOPSIS
        Clears all cached Winget data.

    .DESCRIPTION
        Removes all cached data from memory and optionally from disk.

    .PARAMETER IncludeDisk
        Also removes the cache file from disk.

    .EXAMPLE
        Clear-WingetCache -IncludeDisk
    #>
    [CmdletBinding()]
    param(
        [switch]$IncludeDisk
    )

    # Clear memory cache
    $script:WingetCache.ListCache = $null
    $script:WingetCache.ListCacheTime = $null
    $script:WingetCache.SearchCache = @{}
    $script:WingetCache.SearchCacheTimes = @{}

    Write-Verbose "Cleared in-memory Winget cache"

    if ($IncludeDisk) {
        $cachePath = Join-Path $script:CacheConfig.CacheDirectory $script:CacheConfig.CacheFileName
        if (Test-Path -Path $cachePath) {
            try {
                Remove-Item -Path $cachePath -Force
                Write-Verbose "Removed cache file from disk"
            } catch {
                Write-Warning "Failed to remove cache file: $_"
            }
        }
    }
}

function Save-WingetCache {
    <#
    .SYNOPSIS
        Saves the current cache to disk.

    .DESCRIPTION
        Persists the current cache state to disk for session continuity.

    .EXAMPLE
        Save-WingetCache
    #>
    [CmdletBinding()]
    param()

    $cacheDir = $script:CacheConfig.CacheDirectory
    if (-not (Test-Path -Path $cacheDir)) {
        New-Item -Path $cacheDir -ItemType Directory -Force | Out-Null
    }

    $cachePath = Join-Path $cacheDir $script:CacheConfig.CacheFileName

    # Build cache object for serialization
    $searchCacheForSave = @{}
    foreach ($key in $script:WingetCache.SearchCache.Keys) {
        $searchCacheForSave[$key] = @{
            Data = $script:WingetCache.SearchCache[$key]
            CacheTime = $script:WingetCache.SearchCacheTimes[$key].ToString('o')
        }
    }

    $cacheData = @{
        ListCache = $script:WingetCache.ListCache
        ListCacheTime = if ($script:WingetCache.ListCacheTime) { $script:WingetCache.ListCacheTime.ToString('o') } else { $null }
        SearchCache = $searchCacheForSave
        Statistics = $script:WingetCache.Statistics
        SavedAt = (Get-Date).ToString('o')
    }

    try {
        $cacheData | ConvertTo-Json -Depth 10 | Set-Content -Path $cachePath -Encoding UTF8
        Write-Verbose "Saved cache to disk: $cachePath"
    } catch {
        Write-Warning "Failed to save cache to disk: $_"
    }
}

function Get-WingetCacheStatistics {
    <#
    .SYNOPSIS
        Returns cache statistics.

    .DESCRIPTION
        Provides statistics about cache usage including hit/miss ratios.

    .OUTPUTS
        PSCustomObject with cache statistics.

    .EXAMPLE
        Get-WingetCacheStatistics | Format-List
    #>
    [CmdletBinding()]
    param()

    $stats = $script:WingetCache.Statistics

    $listTotal = $stats.ListHits + $stats.ListMisses
    $listHitRate = if ($listTotal -gt 0) { [math]::Round(($stats.ListHits / $listTotal) * 100, 1) } else { 0 }

    $searchTotal = $stats.SearchHits + $stats.SearchMisses
    $searchHitRate = if ($searchTotal -gt 0) { [math]::Round(($stats.SearchHits / $searchTotal) * 100, 1) } else { 0 }

    $listCacheAge = if ($script:WingetCache.ListCacheTime) {
        $age = (Get-Date) - $script:WingetCache.ListCacheTime
        [math]::Round($age.TotalMinutes, 1)
    } else { $null }

    [PSCustomObject]@{
        ListCacheValid = ($null -ne $script:WingetCache.ListCache)
        ListCacheAgeMinutes = $listCacheAge
        ListHits = $stats.ListHits
        ListMisses = $stats.ListMisses
        ListHitRate = "$listHitRate%"
        SearchCacheEntries = $script:WingetCache.SearchCache.Count
        SearchHits = $stats.SearchHits
        SearchMisses = $stats.SearchMisses
        SearchHitRate = "$searchHitRate%"
        LastWarmup = $stats.LastWarmup
        TTLListMinutes = $script:CacheConfig.ListTTLMinutes
        TTLSearchMinutes = $script:CacheConfig.SearchTTLMinutes
        MaxSearchEntries = $script:CacheConfig.MaxEntries
    }
}

# === CHOCOLATEY CACHE FUNCTIONS ===

function Get-CachedChocoList {
    <#
    .SYNOPSIS
        Gets cached Chocolatey list output with TTL-based refresh.

    .DESCRIPTION
        Returns cached `choco list --local-only` output if available and within TTL.
        Otherwise, executes the command and caches the result.
        Default TTL is 60 minutes (configurable).

    .PARAMETER Force
        If specified, refreshes the cache regardless of TTL.

    .OUTPUTS
        String containing the Chocolatey list output.

    .EXAMPLE
        $chocoList = Get-CachedChocoList
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [switch]$Force
    )

    # Check if choco is available
    if (-not (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)) {
        Write-Verbose "Chocolatey is not installed or not in PATH"
        return ""
    }

    # Check cache validity
    if (-not $Force -and $script:ChocoCache.ListCache -and $script:ChocoCache.ListCacheTime) {
        $age = (Get-Date) - $script:ChocoCache.ListCacheTime
        if ($age.TotalMinutes -lt $script:CacheConfig.ChocoListTTLMinutes) {
            $script:ChocoCache.Statistics.ListHits++
            Write-Verbose "Chocolatey cache hit (age: $([math]::Round($age.TotalMinutes, 1)) min)"
            return $script:ChocoCache.ListCache
        }
    }

    # Cache miss - refresh
    $script:ChocoCache.Statistics.ListMisses++
    Write-Verbose "Chocolatey cache miss - refreshing..."

    try {
        $output = & choco list --local-only 2>&1 | Out-String
        $script:ChocoCache.ListCache = $output
        $script:ChocoCache.ListCacheTime = Get-Date
        Write-Verbose "Cached Chocolatey list ($($output.Length) chars)"
        return $output
    } catch {
        Write-Verbose "Error running choco list: $_"
        return ""
    }
}

function Clear-ChocoCache {
    <#
    .SYNOPSIS
        Clears the Chocolatey list cache.
    .DESCRIPTION
        Resets the in-memory Chocolatey package list cache and its timestamp, forcing
        the next lookup to re-query Chocolatey directly.
    #>
    [CmdletBinding()]
    param()

    $script:ChocoCache.ListCache = $null
    $script:ChocoCache.ListCacheTime = $null
    Write-Verbose "Cleared Chocolatey cache"
}

function Get-ChocoStatistics {
    <#
    .SYNOPSIS
        Returns Chocolatey cache statistics.
    .DESCRIPTION
        Computes and returns cache performance metrics for Chocolatey lookups, including
        hit count, miss count, hit rate percentage, and the age of the current cache entry.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $stats = $script:ChocoCache.Statistics
    $total = $stats.ListHits + $stats.ListMisses
    $hitRate = if ($total -gt 0) { [math]::Round(($stats.ListHits / $total) * 100, 1) } else { 0 }

    $cacheAge = if ($script:ChocoCache.ListCacheTime) {
        $age = (Get-Date) - $script:ChocoCache.ListCacheTime
        [math]::Round($age.TotalMinutes, 1)
    } else { $null }

    [PSCustomObject]@{
        CacheValid = ($null -ne $script:ChocoCache.ListCache)
        CacheAgeMinutes = $cacheAge
        Hits = $stats.ListHits
        Misses = $stats.ListMisses
        HitRate = "$hitRate%"
        TTLMinutes = $script:CacheConfig.ChocoListTTLMinutes
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-WingetCache',
    'Get-CachedWingetList',
    'Get-CachedWingetSearch',
    'Update-WingetListCache',
    'Clear-WingetCache',
    'Save-WingetCache',
    'Get-WingetCacheStatistics',
    'Get-CachedChocoList',
    'Clear-ChocoCache',
    'Get-ChocoStatistics'
)
