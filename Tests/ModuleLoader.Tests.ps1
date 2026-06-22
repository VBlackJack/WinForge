#
# Tests for ModuleLoader module
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
    $modulePath = Join-Path $PSScriptRoot '..\Core\ModuleLoader.psm1'
    Import-Module $modulePath -Force
}

AfterAll {
    Remove-Module ModuleLoader -ErrorAction SilentlyContinue
}

Describe 'ModuleLoader Module' {
    Context 'Get-WinForgeRepositoryRoot' {
        It 'Should return a valid path' {
            $result = Get-WinForgeRepositoryRoot
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [string]
        }

        It 'Should return an existing directory' {
            $result = Get-WinForgeRepositoryRoot
            Test-Path $result -PathType Container | Should -Be $true
        }

        It 'Should return consistent results' {
            $result1 = Get-WinForgeRepositoryRoot
            $result2 = Get-WinForgeRepositoryRoot
            $result1 | Should -Be $result2
        }

        It 'Should contain Core directory' {
            $root = Get-WinForgeRepositoryRoot
            $corePath = Join-Path $root 'Core'
            Test-Path $corePath -PathType Container | Should -Be $true
        }
    }

    Context 'Import-CoreDependency' {
        It 'Should import module for Write-Status command' {
            $result = Import-CoreDependency -CommandName 'Write-Status'
            $result | Should -Be $true
        }

        It 'Should import module for Get-LocalizedString command' {
            $result = Import-CoreDependency -CommandName 'Get-LocalizedString'
            $result | Should -Be $true
        }

        It 'Should handle already available commands gracefully' {
            Import-CoreDependency -CommandName 'Write-Status'
            $result = Import-CoreDependency -CommandName 'Write-Status'
            $result | Should -Be $true
        }

        It 'Should return false for unknown commands' {
            $result = Import-CoreDependency -CommandName 'NonExistentCommand12345'
            $result | Should -Be $false
        }

        It 'Should support Force parameter' {
            $result = Import-CoreDependency -CommandName 'Write-Status' -Force
            $result | Should -Be $true
        }
    }

    Context 'Import-CoreDependencies' {
        It 'Should import multiple commands' {
            $result = Import-CoreDependencies -CommandNames @('Write-Status', 'Get-LocalizedString')
            $result | Should -Be $true
        }

        It 'Should return false if any command fails' {
            $result = Import-CoreDependencies -CommandNames @('Write-Status', 'NonExistentCommand12345')
            $result | Should -Be $false
        }

        It 'Should throw with ThrowOnFailure for missing commands' {
            { Import-CoreDependencies -CommandNames @('NonExistentCommand12345') -ThrowOnFailure } | Should -Throw
        }
    }

    Context 'Initialize-WinForgeModule' {
        It 'Should initialize module context' {
            $result = Initialize-WinForgeModule
            $result | Should -Not -BeNullOrEmpty
            $result.RepositoryRoot | Should -Not -BeNullOrEmpty
        }

        It 'Should return hashtable with expected keys' {
            $result = Initialize-WinForgeModule
            $result.Keys | Should -Contain 'RepositoryRoot'
            $result.Keys | Should -Contain 'Success'
            $result.Keys | Should -Contain 'LoadedCommands'
        }

        It 'Should accept AdditionalCommands parameter' {
            $result = Initialize-WinForgeModule -AdditionalCommands @('Test-ApplicationInstalled')
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should accept IncludeFeatureFlags switch' {
            $result = Initialize-WinForgeModule -IncludeFeatureFlags
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should accept IncludeTimeouts switch' {
            $result = Initialize-WinForgeModule -IncludeTimeouts
            $result | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ModuleLoadStatus' {
        It 'Should return status information' {
            $result = Get-ModuleLoadStatus
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should include LoadedModuleCount' {
            $result = Get-ModuleLoadStatus
            $result.LoadedModuleCount | Should -BeOfType [int]
        }

        It 'Should include LoadedModules key' {
            $result = Get-ModuleLoadStatus
            $result.Keys | Should -Contain 'LoadedModules'
        }

        It 'Should include AvailableCommands' {
            $result = Get-ModuleLoadStatus
            $result.AvailableCommands | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Module Integration' {
        It 'Should have all expected functions exported' {
            $expectedFunctions = @(
                'Get-WinForgeRepositoryRoot',
                'Import-CoreDependency',
                'Import-CoreDependencies',
                'Initialize-WinForgeModule',
                'Get-ModuleLoadStatus'
            )

            $module = Get-Module ModuleLoader
            foreach ($func in $expectedFunctions) {
                $module.ExportedFunctions.Keys | Should -Contain $func
            }
        }
    }

    Context 'Repository Root Structure' {
        It 'Should find Core subdirectory' {
            $root = Get-WinForgeRepositoryRoot
            $corePath = Join-Path $root 'Core'
            Test-Path $corePath -PathType Container | Should -Be $true
        }

        It 'Should find Modules subdirectory' {
            $root = Get-WinForgeRepositoryRoot
            $modulesPath = Join-Path $root 'Modules'
            Test-Path $modulesPath -PathType Container | Should -Be $true
        }

        It 'Should return a valid WinForge module root' {
            $root = Get-WinForgeRepositoryRoot
            # Should contain Core.psm1
            $coreModule = Join-Path $root 'Core\Core.psm1'
            Test-Path $coreModule | Should -Be $true
        }
    }
}
