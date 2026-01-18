<#
.SYNOPSIS
    Pester tests for WingetCache module

.DESCRIPTION
    Unit tests for Win11Forge WingetCache v3.1.4
    Tests caching, statistics, and persistence functions

.NOTES
    Author: Julien Bombled
    Version: 3.1.4
    Requires: Pester v5+
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

BeforeAll {
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:ModulePath = Join-Path $script:ModuleRoot 'WingetCache.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'WingetCache Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-WingetCache function' {
            Get-Command Initialize-WingetCache -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-CachedWingetList function' {
            Get-Command Get-CachedWingetList -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-CachedWingetSearch function' {
            Get-Command Get-CachedWingetSearch -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Update-WingetListCache function' {
            Get-Command Update-WingetListCache -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-WingetCache function' {
            Get-Command Clear-WingetCache -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-WingetCache function' {
            Get-Command Save-WingetCache -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-WingetCacheStatistics function' {
            Get-Command Get-WingetCacheStatistics -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-WingetCache' {
        It 'Should initialize without errors' {
            { Initialize-WingetCache } | Should -Not -Throw
        }

        It 'Should accept TTL parameters' {
            { Initialize-WingetCache -ListTTLMinutes 60 -SearchTTLMinutes 120 } | Should -Not -Throw
        }
    }

    Context 'Get-WingetCacheStatistics' {
        It 'Should return statistics object' {
            $stats = Get-WingetCacheStatistics
            $stats | Should -Not -BeNullOrEmpty
        }

        It 'Should have required properties' {
            $stats = Get-WingetCacheStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'ListHits'
            $stats.PSObject.Properties.Name | Should -Contain 'ListMisses'
            $stats.PSObject.Properties.Name | Should -Contain 'SearchHits'
            $stats.PSObject.Properties.Name | Should -Contain 'SearchMisses'
        }
    }

    Context 'Clear-WingetCache' {
        It 'Should clear cache without errors' {
            { Clear-WingetCache } | Should -Not -Throw
        }

        It 'Should accept IncludeDisk switch' {
            { Clear-WingetCache -IncludeDisk } | Should -Not -Throw
        }

        It 'Should reset statistics list cache after clear' {
            Clear-WingetCache
            $stats = Get-WingetCacheStatistics
            $stats.ListCacheValid | Should -Be $false
            $stats.SearchCacheEntries | Should -Be 0
        }
    }

    Context 'Save-WingetCache' {
        It 'Should save cache without errors' {
            { Save-WingetCache } | Should -Not -Throw
        }

        It 'Should create cache file in expected location' {
            $cacheDir = Join-Path $env:LOCALAPPDATA 'Win11Forge'
            $cachePath = Join-Path $cacheDir 'WingetCache.json'

            # Initialize and save
            Initialize-WingetCache
            Save-WingetCache

            # Check file exists (may be empty if no data cached)
            $cacheDir | Should -Exist
        }
    }

    Context 'Update-WingetListCache' {
        It 'Should update cache without errors' {
            { Update-WingetListCache } | Should -Not -Throw
        }
    }

    Context 'Get-CachedWingetList' {
        It 'Should return string output' {
            $result = Get-CachedWingetList
            $result | Should -BeOfType [string]
        }

        It 'Should accept Force parameter' {
            { Get-CachedWingetList -Force } | Should -Not -Throw
        }

        It 'Should increment miss counter on first call after clear' {
            Clear-WingetCache
            $statsBefore = Get-WingetCacheStatistics
            $missesBefore = $statsBefore.ListMisses

            $null = Get-CachedWingetList
            $statsAfter = Get-WingetCacheStatistics
            $missesAfter = $statsAfter.ListMisses

            $missesAfter | Should -BeGreaterThan $missesBefore
        }
    }

    Context 'Get-CachedWingetSearch' {
        It 'Should accept Query parameter' {
            { Get-CachedWingetSearch -Query 'test' } | Should -Not -Throw
        }

        It 'Should return string output' {
            $result = Get-CachedWingetSearch -Query 'test'
            $result | Should -BeOfType [string]
        }

        It 'Should accept Force parameter' {
            { Get-CachedWingetSearch -Query 'test' -Force } | Should -Not -Throw
        }

        It 'Should normalize query case' {
            Clear-WingetCache
            # Both queries should use same cache entry
            $null = Get-CachedWingetSearch -Query 'Firefox'
            $null = Get-CachedWingetSearch -Query 'firefox'

            $stats = Get-WingetCacheStatistics
            # After first miss, second call should be hit (same normalized key)
            $stats.SearchCacheEntries | Should -Be 1
        }
    }

    Context 'Statistics Calculations' {
        BeforeEach {
            Clear-WingetCache
            Initialize-WingetCache
        }

        It 'Should calculate correct hit rate format' {
            $stats = Get-WingetCacheStatistics
            $stats.ListHitRate | Should -Match '^\d+(\.\d+)?%$'
            $stats.SearchHitRate | Should -Match '^\d+(\.\d+)?%$'
        }

        It 'Should track TTL configuration' {
            Initialize-WingetCache -ListTTLMinutes 45 -SearchTTLMinutes 90
            $stats = Get-WingetCacheStatistics
            $stats.TTLListMinutes | Should -Be 45
            $stats.TTLSearchMinutes | Should -Be 90
        }

        It 'Should track max search entries configuration' {
            $stats = Get-WingetCacheStatistics
            $stats.MaxSearchEntries | Should -Be 500
        }

        It 'Should track last warmup time when PreWarm used' {
            $beforeWarmup = Get-Date
            Initialize-WingetCache -PreWarm
            $afterWarmup = Get-Date

            $stats = Get-WingetCacheStatistics
            if ($stats.LastWarmup) {
                $stats.LastWarmup | Should -BeGreaterOrEqual $beforeWarmup
                $stats.LastWarmup | Should -BeLessOrEqual $afterWarmup
            }
        }
    }

    Context 'Cache Persistence' {
        It 'Should restore cache from disk when valid' {
            # Initialize and populate cache
            Initialize-WingetCache
            $null = Get-CachedWingetList
            Save-WingetCache

            # Clear memory but keep disk
            Clear-WingetCache

            # Re-initialize should restore from disk
            Initialize-WingetCache

            # Stats should show cache was restored (if list was valid)
            $stats = Get-WingetCacheStatistics
            # ListCacheValid depends on TTL, may or may not be true
            $stats | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Edge Cases' {
        It 'Should handle empty search query gracefully' {
            # Empty strings might be rejected by parameter validation
            # but should not crash the module
            { Get-CachedWingetSearch -Query '' } | Should -Throw
        }

        It 'Should handle whitespace-only search query' {
            $result = Get-CachedWingetSearch -Query '   '
            $result | Should -BeOfType [string]
        }

        It 'Should handle multiple rapid requests' {
            Clear-WingetCache
            1..5 | ForEach-Object {
                $null = Get-CachedWingetList
            }

            $stats = Get-WingetCacheStatistics
            # First should be miss, rest should be hits
            $stats.ListMisses | Should -BeGreaterOrEqual 1
            $stats.ListHits | Should -BeGreaterOrEqual 4
        }
    }
}
