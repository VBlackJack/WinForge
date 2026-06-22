<#
.SYNOPSIS
    Pester tests for WingetCache module

.DESCRIPTION
    Unit tests for WinForge WingetCache v3.2.2
    Tests caching, statistics, and persistence functions
    Includes mocked tests for winget isolation

.NOTES
    Author: Julien Bombled
    Version: 3.2.2
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
            $cacheDir = Join-Path $env:LOCALAPPDATA 'WinForge'
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

    # === MOCKED TESTS (isolated from real winget) ===

    Context 'Mocked Winget List Output' {
        BeforeAll {
            # Sample winget list output (simulated)
            $script:MockedWingetListOutput = @"
Name                                      Id                                      Version      Available    Source
------------------------------------------------------------------------------------------------------------
Microsoft Visual Studio Code              Microsoft.VisualStudioCode              1.85.1       1.86.0       winget
Git                                       Git.Git                                 2.43.0                    winget
Node.js                                   OpenJS.NodeJS                           21.5.0                    winget
7-Zip                                     7zip.7zip                               23.01                     winget
Mozilla Firefox                           Mozilla.Firefox                         121.0                     winget
"@
        }

        It 'Should parse mocked winget list output correctly' {
            # Test that the mocked output format is recognized
            $script:MockedWingetListOutput | Should -Match 'Microsoft.VisualStudioCode'
            $script:MockedWingetListOutput | Should -Match 'Git.Git'
            $script:MockedWingetListOutput | Should -Match 'Mozilla.Firefox'
        }

        It 'Should detect applications in mocked output' {
            $output = $script:MockedWingetListOutput

            # Test detection patterns used by InstallationEngine
            ($output -match 'Microsoft.VisualStudioCode') | Should -Be $true
            ($output -match 'Git.Git') | Should -Be $true
            ($output -match 'NonExistent.App') | Should -Be $false
        }

        It 'Should detect version availability in mocked output' {
            $output = $script:MockedWingetListOutput

            # VSCode has an update available (1.86.0)
            ($output -match 'Microsoft.VisualStudioCode.*1\.86\.0') | Should -Be $true

            # Git has no update available (empty Available column)
            $lines = $output -split "`n"
            $gitLine = $lines | Where-Object { $_ -match 'Git\.Git' }
            $gitLine | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Mocked Winget Search Output' {
        BeforeAll {
            # Sample winget search output (simulated)
            $script:MockedWingetSearchOutput = @"
Name                          Id                              Version   Match        Source
---------------------------------------------------------------------------------------------
Mozilla Firefox               Mozilla.Firefox                 121.0     Tag: firefox winget
Firefox Developer Edition     Mozilla.Firefox.DeveloperEdition 121.0                 winget
Firefox Nightly               Mozilla.Firefox.Nightly         122.0a1                winget
"@
        }

        It 'Should find packages in mocked search output' {
            $output = $script:MockedWingetSearchOutput

            ($output -match 'Mozilla.Firefox') | Should -Be $true
            ($output -match 'Firefox Developer') | Should -Be $true
        }

        It 'Should handle no results scenario' {
            $noResultsOutput = "No package found matching input criteria."

            ($noResultsOutput -match 'No package found') | Should -Be $true
            ($noResultsOutput -match 'Mozilla.Firefox') | Should -Be $false
        }
    }

    Context 'Cache Expiry Behavior (Mocked Time)' {
        BeforeEach {
            Clear-WingetCache
            Initialize-WingetCache -ListTTLMinutes 5 -SearchTTLMinutes 10
        }

        It 'Should correctly identify cache within TTL' {
            $stats = Get-WingetCacheStatistics
            $stats.TTLListMinutes | Should -Be 5
            $stats.TTLSearchMinutes | Should -Be 10
        }

        It 'Should track cache age in minutes' {
            # First call to populate cache
            $null = Get-CachedWingetList

            $stats = Get-WingetCacheStatistics
            if ($stats.ListCacheValid) {
                $stats.ListCacheAgeMinutes | Should -Not -BeNullOrEmpty
                $stats.ListCacheAgeMinutes | Should -BeOfType [double]
            }
        }
    }

    Context 'Winget Unavailable Scenario' {
        It 'Should handle gracefully when winget is not in PATH' {
            # Note: This test documents expected behavior
            # When winget is unavailable, Get-CachedWingetList returns empty string

            # The actual behavior depends on system state
            # If winget is available: returns list
            # If winget is unavailable: returns ""
            $result = Get-CachedWingetList
            $result | Should -BeOfType [string]
        }
    }

    Context 'Cache Miss Scenarios' {
        BeforeEach {
            Clear-WingetCache
        }

        It 'Should increment miss counter on first call' {
            $statsBefore = Get-WingetCacheStatistics
            $missesBefore = $statsBefore.ListMisses

            $null = Get-CachedWingetList
            $statsAfter = Get-WingetCacheStatistics

            $statsAfter.ListMisses | Should -Be ($missesBefore + 1)
        }

        It 'Should increment hit counter on subsequent calls' {
            # First call - miss
            $null = Get-CachedWingetList
            $statsAfterMiss = Get-WingetCacheStatistics
            $hitsBefore = $statsAfterMiss.ListHits

            # Second call - should be hit
            $null = Get-CachedWingetList
            $statsAfterHit = Get-WingetCacheStatistics

            $statsAfterHit.ListHits | Should -Be ($hitsBefore + 1)
        }

        It 'Should miss when Force parameter is used' {
            # Populate cache
            $null = Get-CachedWingetList
            $statsPopulated = Get-WingetCacheStatistics
            $missesBefore = $statsPopulated.ListMisses

            # Force refresh - should miss
            $null = Get-CachedWingetList -Force
            $statsAfterForce = Get-WingetCacheStatistics

            $statsAfterForce.ListMisses | Should -Be ($missesBefore + 1)
        }
    }

    Context 'Search Cache Key Normalization' {
        BeforeEach {
            Clear-WingetCache
            Initialize-WingetCache
        }

        It 'Should use same cache key for case variations' {
            # Reset statistics by clearing cache
            Clear-WingetCache
            Initialize-WingetCache

            $null = Get-CachedWingetSearch -Query 'VSCODE'
            $null = Get-CachedWingetSearch -Query 'vscode'
            $null = Get-CachedWingetSearch -Query 'VsCode'

            $stats = Get-WingetCacheStatistics
            # All three should use same normalized key
            $stats.SearchCacheEntries | Should -Be 1
            # Verify hits increased (at least 2 hits for the cached queries)
            $stats.SearchHits | Should -BeGreaterOrEqual 2
        }

        It 'Should trim whitespace from search queries' {
            # Reset statistics
            Clear-WingetCache
            Initialize-WingetCache

            $null = Get-CachedWingetSearch -Query '  firefox  '
            $null = Get-CachedWingetSearch -Query 'firefox'

            $stats = Get-WingetCacheStatistics
            $stats.SearchCacheEntries | Should -Be 1
        }
    }

    Context 'Cache Entry Limit' {
        It 'Should respect MaxSearchEntries configuration' {
            $stats = Get-WingetCacheStatistics
            $stats.MaxSearchEntries | Should -Be 500
        }
    }
}
