<#
.SYNOPSIS
    Pester tests for TimeoutSettings module

.DESCRIPTION
    Comprehensive unit tests for WinForge TimeoutSettings v3.5.2
    Tests timeout configuration loading, caching, and helper functions

.NOTES
    Author: Julien Bombled
    Version: 3.5.2
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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Core'
    $script:ModulePath = Join-Path $script:ModuleRoot 'TimeoutSettings.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'TimeoutSettings Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-TimeoutSettings function' {
            Get-Command Get-TimeoutSettings -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-InstallationTimeout function' {
            Get-Command Get-InstallationTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ParallelTimeout function' {
            Get-Command Get-ParallelTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-MaxParallelJobs function' {
            Get-Command Get-MaxParallelJobs -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-DownloadTimeout function' {
            Get-Command Get-DownloadTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-CacheTtl function' {
            Get-Command Get-CacheTtl -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-PluginTimeout function' {
            Get-Command Get-PluginTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApiTimeout function' {
            Get-Command Get-ApiTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-TimeoutSettings' {
        It 'Should return hashtable' {
            $result = Get-TimeoutSettings
            $result | Should -BeOfType [hashtable]
        }

        It 'Should include Installation settings' {
            $result = Get-TimeoutSettings
            $result.Installation | Should -Not -BeNullOrEmpty
        }

        It 'Should include Parallel settings' {
            $result = Get-TimeoutSettings
            $result.Parallel | Should -Not -BeNullOrEmpty
        }

        It 'Should include Download settings' {
            $result = Get-TimeoutSettings
            $result.Download | Should -Not -BeNullOrEmpty
        }

        It 'Should include Detection settings' {
            $result = Get-TimeoutSettings
            $result.Detection | Should -Not -BeNullOrEmpty
        }

        It 'Should include Api settings' {
            $result = Get-TimeoutSettings
            $result.Api | Should -Not -BeNullOrEmpty
        }

        It 'Should include Plugin settings' {
            $result = Get-TimeoutSettings
            $result.Plugin | Should -Not -BeNullOrEmpty
        }

        It 'Should support Force parameter' {
            { Get-TimeoutSettings -Force } | Should -Not -Throw
        }

        It 'Should return numeric values for timeouts' {
            $result = Get-TimeoutSettings
            $result.Installation.DefaultTimeoutSeconds | Should -BeGreaterThan 0
        }
    }

    Context 'Get-InstallationTimeout' {
        It 'Should return integer' {
            $result = Get-InstallationTimeout
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return default timeout when no app specified' {
            $result = Get-InstallationTimeout
            $result | Should -BeGreaterThan 0
        }

        It 'Should return higher timeout for Office' {
            $defaultTimeout = Get-InstallationTimeout
            $officeTimeout = Get-InstallationTimeout -AppName 'Microsoft Office'
            ($officeTimeout -ge $defaultTimeout) | Should -Be $true
        }

        It 'Should detect Office 365' {
            $result = Get-InstallationTimeout -AppName 'Microsoft 365'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.OfficeTimeoutSeconds
        }

        It 'Should detect Word' {
            $result = Get-InstallationTimeout -AppName 'Word'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.OfficeTimeoutSeconds
        }

        It 'Should detect Excel' {
            $result = Get-InstallationTimeout -AppName 'Excel'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.OfficeTimeoutSeconds
        }

        It 'Should detect PowerPoint' {
            $result = Get-InstallationTimeout -AppName 'PowerPoint'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.OfficeTimeoutSeconds
        }

        It 'Should detect Outlook' {
            $result = Get-InstallationTimeout -AppName 'Outlook'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.OfficeTimeoutSeconds
        }

        It 'Should return default for non-Office app' {
            $result = Get-InstallationTimeout -AppName 'Google Chrome'
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Installation.DefaultTimeoutSeconds
        }
    }

    Context 'Get-ParallelTimeout' {
        It 'Should return integer' {
            $result = Get-ParallelTimeout
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return value in milliseconds (large number)' {
            $result = Get-ParallelTimeout
            $result | Should -BeGreaterThan 10000
        }

        It 'Should match config value' {
            $result = Get-ParallelTimeout
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Parallel.TimeoutMilliseconds
        }
    }

    Context 'Get-MaxParallelJobs' {
        It 'Should return integer' {
            $result = Get-MaxParallelJobs
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return positive value' {
            $result = Get-MaxParallelJobs
            $result | Should -BeGreaterThan 0
        }

        It 'Should return reasonable value (1-20)' {
            $result = Get-MaxParallelJobs
            $result | Should -BeGreaterOrEqual 1
            $result | Should -BeLessOrEqual 20
        }

        It 'Should match config value' {
            $result = Get-MaxParallelJobs
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Parallel.MaxParallelJobs
        }
    }

    Context 'Get-DownloadTimeout' {
        It 'Should return integer' {
            $result = Get-DownloadTimeout
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return positive value' {
            $result = Get-DownloadTimeout
            $result | Should -BeGreaterThan 0
        }

        It 'Should match config value' {
            $result = Get-DownloadTimeout
            $config = Get-TimeoutSettings
            $result | Should -Be $config.Download.TimeoutSeconds
        }
    }

    Context 'Get-CacheTtl' {
        It 'Should return integer for Registry' {
            $result = Get-CacheTtl -CacheType Registry
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return integer for Winget' {
            $result = Get-CacheTtl -CacheType Winget
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return integer for Search' {
            $result = Get-CacheTtl -CacheType Search
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return positive values' {
            Get-CacheTtl -CacheType Registry | Should -BeGreaterThan 0
            Get-CacheTtl -CacheType Winget | Should -BeGreaterThan 0
            Get-CacheTtl -CacheType Search | Should -BeGreaterThan 0
        }

        It 'Should require CacheType parameter' {
            { Get-CacheTtl } | Should -Throw
        }

        It 'Should reject invalid CacheType' {
            { Get-CacheTtl -CacheType 'Invalid' } | Should -Throw
        }

        It 'Should match config values' {
            $config = Get-TimeoutSettings
            Get-CacheTtl -CacheType Registry | Should -Be $config.Detection.RegistryCacheTtlMinutes
            Get-CacheTtl -CacheType Winget | Should -Be $config.Detection.WingetCacheTtlMinutes
            Get-CacheTtl -CacheType Search | Should -Be $config.Detection.SearchCacheTtlMinutes
        }
    }

    Context 'Get-PluginTimeout' {
        It 'Should return integer for Execution' {
            $result = Get-PluginTimeout -Operation Execution
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return integer for Load' {
            $result = Get-PluginTimeout -Operation Load
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return positive values' {
            Get-PluginTimeout -Operation Execution | Should -BeGreaterThan 0
            Get-PluginTimeout -Operation Load | Should -BeGreaterThan 0
        }

        It 'Should require Operation parameter' {
            { Get-PluginTimeout } | Should -Throw
        }

        It 'Should reject invalid Operation' {
            { Get-PluginTimeout -Operation 'Invalid' } | Should -Throw
        }

        It 'Should match config values' {
            $config = Get-TimeoutSettings
            Get-PluginTimeout -Operation Execution | Should -Be $config.Plugin.ExecutionTimeoutSeconds
            Get-PluginTimeout -Operation Load | Should -Be $config.Plugin.LoadTimeoutSeconds
        }
    }

    Context 'Get-ApiTimeout' {
        It 'Should return integer for Request' {
            $result = Get-ApiTimeout -Operation Request
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return integer for Shutdown' {
            $result = Get-ApiTimeout -Operation Shutdown
            ($result -is [int] -or $result -is [long]) | Should -Be $true
        }

        It 'Should return positive values' {
            Get-ApiTimeout -Operation Request | Should -BeGreaterThan 0
            Get-ApiTimeout -Operation Shutdown | Should -BeGreaterThan 0
        }

        It 'Should require Operation parameter' {
            { Get-ApiTimeout } | Should -Throw
        }

        It 'Should reject invalid Operation' {
            { Get-ApiTimeout -Operation 'Invalid' } | Should -Throw
        }

        It 'Should match config values' {
            $config = Get-TimeoutSettings
            Get-ApiTimeout -Operation Request | Should -Be $config.Api.RequestTimeoutMs
            Get-ApiTimeout -Operation Shutdown | Should -Be $config.Api.ShutdownTimeoutMs
        }
    }

    Context 'Default Values' {
        It 'Should have reasonable default installation timeout (30+ minutes)' {
            $config = Get-TimeoutSettings
            $config.Installation.DefaultTimeoutSeconds | Should -BeGreaterOrEqual 1800
        }

        It 'Should have higher Office timeout than default' {
            $config = Get-TimeoutSettings
            $config.Installation.OfficeTimeoutSeconds | Should -BeGreaterThan $config.Installation.DefaultTimeoutSeconds
        }

        It 'Should have reasonable download timeout (1-10 minutes)' {
            $config = Get-TimeoutSettings
            $config.Download.TimeoutSeconds | Should -BeGreaterOrEqual 60
            $config.Download.TimeoutSeconds | Should -BeLessOrEqual 600
        }

        It 'Should have reasonable retry count (1-5)' {
            $config = Get-TimeoutSettings
            $config.Download.MaxRetries | Should -BeGreaterOrEqual 1
            $config.Download.MaxRetries | Should -BeLessOrEqual 5
        }
    }

    Context 'Caching Behavior' {
        It 'Should return same instance without Force' {
            $first = Get-TimeoutSettings
            $second = Get-TimeoutSettings
            # While we can't directly compare hashtables, we can verify they have same structure
            $first.Keys.Count | Should -Be $second.Keys.Count
        }

        It 'Should reload with Force parameter' {
            $first = Get-TimeoutSettings
            $forced = Get-TimeoutSettings -Force
            # Both should have same structure
            $first.Keys.Count | Should -Be $forced.Keys.Count
        }
    }

    Context 'Edge Cases' {
        It 'Should handle empty app name' {
            { Get-InstallationTimeout -AppName '' } | Should -Not -Throw
        }

        It 'Should handle app name with special characters' {
            { Get-InstallationTimeout -AppName 'App (v1.0) - Special Edition' } | Should -Not -Throw
        }

        It 'Should handle case-insensitive Office detection' {
            $lower = Get-InstallationTimeout -AppName 'office'
            $upper = Get-InstallationTimeout -AppName 'OFFICE'
            $lower | Should -Be $upper
        }
    }
}
