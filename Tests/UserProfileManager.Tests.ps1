<#
.SYNOPSIS
    Pester tests for UserProfileManager module

.DESCRIPTION
    Unit tests for Win11Forge UserProfileManager v3.1.4
    Tests profile saving, loading, import/export functions

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'UserProfileManager.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'UserProfileManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-UserProfileManager function' {
            Get-Command Initialize-UserProfileManager -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-UserProfile function' {
            Get-Command Save-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfiles function' {
            Get-Command Get-UserProfiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfile function' {
            Get-Command Get-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-UserProfile function' {
            Get-Command Remove-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Export-UserProfile function' {
            Get-Command Export-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Import-UserProfile function' {
            Get-Command Import-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Copy-UserProfile function' {
            Get-Command Copy-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Merge-UserProfiles function' {
            Get-Command Merge-UserProfiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfileStatistics function' {
            Get-Command Get-UserProfileStatistics -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-UserProfileManager' {
        It 'Should initialize without errors' {
            { Initialize-UserProfileManager } | Should -Not -Throw
        }
    }

    Context 'Save-UserProfile' {
        It 'Should save profile without errors' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile' -Applications @('VSCode', 'Git') -Overwrite } | Should -Not -Throw
        }

        It 'Should accept Description parameter' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile2' -Applications @('App1') -Description 'Test description' -Overwrite } | Should -Not -Throw
        }

        It 'Should accept Tags parameter' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile3' -Applications @('App1') -Tags @('dev', 'test') -Overwrite } | Should -Not -Throw
        }

        It 'Should throw on invalid name characters' {
            { Save-UserProfile -Name 'Invalid Name!' -Applications @('App1') } | Should -Throw
        }
    }

    Context 'Get-UserProfiles' {
        BeforeAll {
            Initialize-UserProfileManager
            Save-UserProfile -Name 'ListTest1' -Applications @('App1') -Overwrite
        }

        It 'Should return profiles' {
            $profiles = @(Get-UserProfiles)
            $profiles.Count | Should -BeGreaterThan 0 -Because 'ListTest1 was created'
        }

        It 'Should support wildcard filtering' {
            { Get-UserProfiles -Name 'List*' } | Should -Not -Throw
        }
    }

    Context 'Get-UserProfile' {
        BeforeAll {
            Initialize-UserProfileManager
            Save-UserProfile -Name 'GetTest' -Applications @('VSCode', 'Git') -Overwrite
        }

        It 'Should return profile by name' {
            $profile = Get-UserProfile -Name 'GetTest'
            $profile | Should -Not -BeNullOrEmpty
        }

        It 'Should return null for non-existent profile' {
            $profile = Get-UserProfile -Name 'NonExistentProfile'
            $profile | Should -BeNullOrEmpty
        }
    }

    Context 'Get-UserProfileStatistics' {
        BeforeAll {
            Initialize-UserProfileManager
        }

        It 'Should return statistics object' {
            $stats = Get-UserProfileStatistics
            $stats | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalProfiles property' {
            $stats = Get-UserProfileStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'TotalProfiles'
        }

        It 'Should have UniqueApplications property' {
            $stats = Get-UserProfileStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'UniqueApplications'
        }
    }

    AfterAll {
        # Cleanup test profiles
        Initialize-UserProfileManager
        @('TestProfile', 'TestProfile2', 'TestProfile3', 'ListTest1', 'GetTest') | ForEach-Object {
            try { Remove-UserProfile -Name $_ -Confirm -ErrorAction SilentlyContinue } catch {}
        }
    }
}
