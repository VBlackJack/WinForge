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
