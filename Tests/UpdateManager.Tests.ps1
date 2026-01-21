<#
.SYNOPSIS
    Pester tests for UpdateManager module

.DESCRIPTION
    Unit tests for Win11Forge UpdateManager v3.1.4
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
    }

    Context 'Get-CurrentVersion' {
        It 'Should return version string' {
            $version = Get-CurrentVersion
            $version | Should -Not -BeNullOrEmpty
        }

        It 'Should return valid version format' {
            $version = Get-CurrentVersion
            $version | Should -Match '^\d+\.\d+\.\d+'
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
        It 'Should return hashtable' {
            $result = Test-UpdateAvailable
            $result | Should -BeOfType [hashtable]
        }

        It 'Should have UpdateAvailable key' {
            $result = Test-UpdateAvailable
            $result.Keys | Should -Contain 'UpdateAvailable'
        }

        It 'Should have CurrentVersion key' {
            $result = Test-UpdateAvailable
            $result.Keys | Should -Contain 'CurrentVersion'
        }
    }
}

Describe 'Test-IsNewerVersion' {
    Context 'Direct Comparison' {
        It 'Should export Test-IsNewerVersion function' {
            Get-Command Test-IsNewerVersion -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should return true when available is newer' {
            $result = Test-IsNewerVersion -CurrentVersion '1.0.0' -AvailableVersion '2.0.0'
            $result | Should -BeTrue
        }

        It 'Should return false when current is newer' {
            $result = Test-IsNewerVersion -CurrentVersion '2.0.0' -AvailableVersion '1.0.0'
            $result | Should -BeFalse
        }

        It 'Should return false when versions are equal' {
            $result = Test-IsNewerVersion -CurrentVersion '1.0.0' -AvailableVersion '1.0.0'
            $result | Should -BeFalse
        }

        It 'Should handle prerelease comparison' {
            $result = Test-IsNewerVersion -CurrentVersion '1.0.0-beta' -AvailableVersion '1.0.0'
            $result | Should -BeTrue
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
            $version | Should -Match '^\d+\.\d+\.\d+'
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
        $expectedFunctions = @(
            'Get-CurrentVersion',
            'Compare-SemanticVersions',
            'Test-IsNewerVersion',
            'Test-UpdateAvailable',
            'Get-UpdateConfiguration',
            'Set-UpdateConfiguration',
            'Get-AvailableBackups',
            'New-Win11ForgeBackup',
            'Restore-Win11ForgeBackup'
        )

        foreach ($func in $expectedFunctions) {
            It "Should export $func function" {
                Get-Command -Module UpdateManager -Name $func -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            }
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
    Context 'New-Win11ForgeBackup' {
        It 'Should be available' {
            Get-Command New-Win11ForgeBackup -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Restore-Win11ForgeBackup' {
        It 'Should be available' {
            Get-Command Restore-Win11ForgeBackup -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }
}
