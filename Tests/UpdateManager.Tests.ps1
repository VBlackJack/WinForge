<#
.SYNOPSIS
    Pester tests for UpdateManager module

.DESCRIPTION
    Unit tests for WinForge UpdateManager v3.1.4
    Tests version comparison, update checking, and backup functions

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'UpdateManager.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'UpdateManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-CurrentVersion function' {
            Get-Command Get-CurrentVersion -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Compare-SemanticVersions function' {
            Get-Command Compare-SemanticVersions -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-UpdateAvailable function' {
            Get-Command Test-UpdateAvailable -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-AvailableBackups function' {
            Get-Command Get-AvailableBackups -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UpdateConfiguration function' {
            Get-Command Get-UpdateConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-UpdateConfiguration function' {
            Get-Command Set-UpdateConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-WingetUpdatesCache function' {
            Get-Command Clear-WingetUpdatesCache -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-CurrentVersion' {
        It 'Should return version string' {
            $version = Get-CurrentVersion
            $version | Should -Not -BeNullOrEmpty
        }

        It 'Should return valid version format' {
            $version = Get-CurrentVersion
            $version | Should -Match '^\d{10}$'
        }
    }

    Context 'Compare-SemanticVersions' {
        It 'Should return 0 for equal versions' {
            Compare-SemanticVersions -Version1 '1.0.0' -Version2 '1.0.0' | Should -Be 0
        }

        It 'Should return 1 when Version1 is greater (major)' {
            Compare-SemanticVersions -Version1 '2.0.0' -Version2 '1.0.0' | Should -Be 1
        }

        It 'Should return -1 when Version1 is lesser (major)' {
            Compare-SemanticVersions -Version1 '1.0.0' -Version2 '2.0.0' | Should -Be -1
        }

        It 'Should return 1 when Version1 is greater (minor)' {
            Compare-SemanticVersions -Version1 '1.2.0' -Version2 '1.1.0' | Should -Be 1
        }

        It 'Should return -1 when Version1 is lesser (minor)' {
            Compare-SemanticVersions -Version1 '1.1.0' -Version2 '1.2.0' | Should -Be -1
        }

        It 'Should return 1 when Version1 is greater (patch)' {
            Compare-SemanticVersions -Version1 '1.0.2' -Version2 '1.0.1' | Should -Be 1
        }

        It 'Should handle v prefix' {
            Compare-SemanticVersions -Version1 'v1.0.0' -Version2 '1.0.0' | Should -Be 0
        }

        It 'Should compare calendar display versions' {
            Compare-SemanticVersions -Version1 '2026050902' -Version2 '2026050901' | Should -Be 1
            Compare-SemanticVersions -Version1 '2026050901' -Version2 '2026050902' | Should -Be -1
        }

        It 'Should normalize Heimdall-style calendar tags' {
            Compare-SemanticVersions -Version1 '2026.050901' -Version2 '2026050901' | Should -Be 0
            Compare-SemanticVersions -Version1 'v2026.050902' -Version2 '2026050901' | Should -Be 1
        }

        It 'Should handle prerelease (stable > prerelease)' {
            Compare-SemanticVersions -Version1 '1.0.0' -Version2 '1.0.0-beta' | Should -Be 1
        }

        It 'Should handle prerelease (prerelease < stable)' {
            Compare-SemanticVersions -Version1 '1.0.0-beta' -Version2 '1.0.0' | Should -Be -1
        }
    }

    Context 'Get-UpdateConfiguration' {
        It 'Should return configuration hashtable' {
            $config = Get-UpdateConfiguration
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have AutoCheckEnabled property' {
            $config = Get-UpdateConfiguration
            $config.Keys | Should -Contain 'AutoCheckEnabled'
        }

        It 'Should have CheckIntervalHours property' {
            $config = Get-UpdateConfiguration
            $config.Keys | Should -Contain 'CheckIntervalHours'
        }
    }

    Context 'Set-UpdateConfiguration' {
        It 'Should accept AutoCheckEnabled parameter' {
            { Set-UpdateConfiguration -AutoCheckEnabled $true } | Should -Not -Throw
        }

        It 'Should accept CheckIntervalHours parameter' {
            { Set-UpdateConfiguration -CheckIntervalHours 12 } | Should -Not -Throw
        }

        It 'Should accept IncludePrerelease parameter' {
            { Set-UpdateConfiguration -IncludePrerelease $false } | Should -Not -Throw
        }
    }

    Context 'Get-AvailableBackups' {
        It 'Should return array or empty' {
            $backups = @(Get-AvailableBackups)
            # Can be empty if no backups exist
            { $backups.Count } | Should -Not -Throw
        }
    }
}

Describe 'Compare-SemanticVersions Extended' {
    Context 'Edge Cases' {
        It 'Should handle single digit versions' {
            Compare-SemanticVersions -Version1 '1' -Version2 '1' | Should -Be 0
        }

        It 'Should handle two-part versions' {
            Compare-SemanticVersions -Version1 '1.0' -Version2 '1.0' | Should -Be 0
        }

        It 'Should handle four-part versions (Windows style)' {
            Compare-SemanticVersions -Version1 '1.0.0.1' -Version2 '1.0.0.0' | Should -Be 1
        }

        It 'Should handle versions with build metadata' {
            Compare-SemanticVersions -Version1 '1.0.0+build123' -Version2 '1.0.0' | Should -Be 0
        }

        It 'Should sort alpha < beta' {
            Compare-SemanticVersions -Version1 '1.0.0-alpha' -Version2 '1.0.0-beta' | Should -Be -1
        }

        It 'Should sort beta < rc' {
            Compare-SemanticVersions -Version1 '1.0.0-beta' -Version2 '1.0.0-rc' | Should -Be -1
        }

        It 'Should handle null Version1 gracefully' {
            { Compare-SemanticVersions -Version1 $null -Version2 '1.0.0' } | Should -Not -Throw
        }

        It 'Should handle empty Version1' {
            { Compare-SemanticVersions -Version1 '' -Version2 '1.0.0' } | Should -Not -Throw
        }

        It 'Should handle Version2 prefix V uppercase' {
            Compare-SemanticVersions -Version1 '1.0.0' -Version2 'V1.0.0' | Should -Be 0
        }
    }

    Context 'Numeric Comparisons' {
        It 'Should compare 10 > 9' {
            Compare-SemanticVersions -Version1 '1.10.0' -Version2 '1.9.0' | Should -Be 1
        }

        It 'Should compare 100 > 99' {
            Compare-SemanticVersions -Version1 '1.100.0' -Version2 '1.99.0' | Should -Be 1
        }

        It 'Should handle leading zeros' {
            Compare-SemanticVersions -Version1 '1.01.0' -Version2 '1.1.0' | Should -Be 0
        }
    }
}

Describe 'Test-UpdateAvailable' {
    Context 'Basic Functionality' {
        It 'Should return PSCustomObject' {
            $result = Test-UpdateAvailable
            $result | Should -BeOfType [PSCustomObject]
        }

        It 'Should have UpdateAvailable property' {
            $result = Test-UpdateAvailable
            $result.PSObject.Properties.Name | Should -Contain 'UpdateAvailable'
        }

        It 'Should have CurrentVersion property' {
            $result = Test-UpdateAvailable
            $result.PSObject.Properties.Name | Should -Contain 'CurrentVersion'
        }
    }
}

Describe 'Winget update batch cache' {
    Context 'Forced refresh and invalidation' {
        It 'Get-ApplicationUpdateStatus should pass Force to batch cache lookup' {
            Mock -CommandName Get-WingetUpdatesBatch -ModuleName UpdateManager -MockWith {
                param([switch]$Force)

                return @{}
            }

            $result = Get-ApplicationUpdateStatus -WingetId 'Vendor.Package' -Force

            $result.HasUpdate | Should -BeFalse
            Should -Invoke -CommandName Get-WingetUpdatesBatch -ModuleName UpdateManager -Times 1 -ParameterFilter {
                $Force.IsPresent
            }
        }

        It 'Clear-WingetUpdatesCache should clear cached batch results' {
            InModuleScope UpdateManager {
                $script:BatchUpdateCache = @{
                    'Vendor.Package' = [PSCustomObject]@{
                        Name             = 'Package'
                        PackageId        = 'Vendor.Package'
                        CurrentVersion   = '1.0.0'
                        AvailableVersion = '2.0.0'
                    }
                }
                $script:BatchUpdateCacheTime = Get-Date

                Clear-WingetUpdatesCache

                $script:BatchUpdateCache.Count | Should -Be 0
                $script:BatchUpdateCacheTime | Should -BeNullOrEmpty
            }
        }
    }
}

Describe 'Test-IsNewerVersion' {
    Context 'Direct Comparison' {
        It 'Should export Test-IsNewerVersion function' {
            Get-Command Test-IsNewerVersion -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should return true when available is newer' {
            $result = Test-IsNewerVersion -Current '1.0.0' -Available '2.0.0'
            $result | Should -BeTrue
        }

        It 'Should return false when current is newer' {
            $result = Test-IsNewerVersion -Current '2.0.0' -Available '1.0.0'
            $result | Should -BeFalse
        }

        It 'Should return false when versions are equal' {
            $result = Test-IsNewerVersion -Current '1.0.0' -Available '1.0.0'
            $result | Should -BeFalse
        }

        It 'Should treat equivalent trailing-zero versions as not newer' {
            $result = Test-IsNewerVersion -Current '2.7.3' -Available '2.7.3.0'
            $result | Should -BeFalse
        }

        It 'Should handle prerelease comparison' {
            $result = Test-IsNewerVersion -Current '1.0.0-beta' -Available '1.0.0'
            $result | Should -BeTrue
        }
    }

    Context 'Batch cache reconciliation' {
        It 'Get-ApplicationUpdateStatus should suppress trailing-zero false positives from winget' {
            Mock -CommandName Get-WingetUpdatesBatch -ModuleName UpdateManager -MockWith {
                return @{
                    'Chocolatey.Chocolatey' = [PSCustomObject]@{
                        Name             = 'Chocolatey'
                        PackageId        = 'Chocolatey.Chocolatey'
                        CurrentVersion   = '2.7.3'
                        AvailableVersion = '2.7.3.0'
                    }
                }
            }

            $result = Get-ApplicationUpdateStatus -WingetId 'Chocolatey.Chocolatey' -CurrentVersion '2.7.3' -Force

            $result.HasUpdate | Should -BeFalse
            $result.CurrentVersion | Should -Be '2.7.3'
            $result.AvailableVersion | Should -Be '2.7.3.0'
        }
    }
}

Describe 'UpdateManager Integration' {
    Context 'Configuration Persistence' {
        It 'Should persist AutoCheckEnabled setting' {
            $originalConfig = Get-UpdateConfiguration
            $originalValue = $originalConfig.AutoCheckEnabled

            # Toggle the value
            Set-UpdateConfiguration -AutoCheckEnabled (-not $originalValue)
            $newConfig = Get-UpdateConfiguration
            $newConfig.AutoCheckEnabled | Should -Be (-not $originalValue)

            # Restore original value
            Set-UpdateConfiguration -AutoCheckEnabled $originalValue
        }

        It 'Should validate CheckIntervalHours range' {
            # Should accept valid values
            { Set-UpdateConfiguration -CheckIntervalHours 1 } | Should -Not -Throw
            { Set-UpdateConfiguration -CheckIntervalHours 24 } | Should -Not -Throw
            { Set-UpdateConfiguration -CheckIntervalHours 168 } | Should -Not -Throw
        }
    }

    Context 'Version File Access' {
        It 'Should read version from Config/version.json' {
            $version = Get-CurrentVersion
            $version | Should -Match '^\d{10}$'
        }

        It 'Should return consistent version' {
            $version1 = Get-CurrentVersion
            $version2 = Get-CurrentVersion
            $version1 | Should -Be $version2
        }
    }
}

Describe 'UpdateManager Export Completeness' {
    Context 'All Expected Functions Exported' {
        It 'Should export <FunctionName> function' -TestCases @(
            @{ FunctionName = 'Get-CurrentVersion' }
            @{ FunctionName = 'Compare-SemanticVersions' }
            @{ FunctionName = 'Test-IsNewerVersion' }
            @{ FunctionName = 'Test-UpdateAvailable' }
            @{ FunctionName = 'Get-UpdateConfiguration' }
            @{ FunctionName = 'Set-UpdateConfiguration' }
            @{ FunctionName = 'Get-AvailableBackups' }
            @{ FunctionName = 'Clear-WingetUpdatesCache' }
            @{ FunctionName = 'Backup-CurrentVersion' }
            @{ FunctionName = 'Restore-PreviousVersion' }
        ) {
            param($FunctionName)
            Get-Command -Module UpdateManager -Name $FunctionName -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'UpdateManager Security' {
    Context 'Input Validation' {
        It 'Should handle version strings with special characters safely' {
            { Compare-SemanticVersions -Version1 '1.0.0; rm -rf' -Version2 '1.0.0' } | Should -Not -Throw
        }

        It 'Should handle unicode in version strings' {
            { Compare-SemanticVersions -Version1 '1.0.0-\u00e9' -Version2 '1.0.0' } | Should -Not -Throw
        }
    }
}

Describe 'Backup Functions' {
    Context 'Backup-CurrentVersion' {
        It 'Should be available' {
            Get-Command Backup-CurrentVersion -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Restore-PreviousVersion' {
        It 'Should be available' {
            Get-Command Restore-PreviousVersion -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }
}
