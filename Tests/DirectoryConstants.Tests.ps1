#
# Tests for DirectoryConstants module
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
    $modulePath = Join-Path $PSScriptRoot '..\Core\DirectoryConstants.psm1'
    Import-Module $modulePath -Force
}

AfterAll {
    Remove-Module DirectoryConstants -ErrorAction SilentlyContinue
}

Describe 'DirectoryConstants Module' {
    Context 'Get-Win11ForgeDirectory' {
        It 'Should return Data directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Data'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [string]
        }

        It 'Should return Logs directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Logs'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'Logs'
        }

        It 'Should return Cache directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Cache'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'Cache'
        }

        It 'Should return Backups directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Backups'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'Backup'
        }

        It 'Should return Plugins directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Plugins'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'Plugin'
        }

        It 'Should create directory if it does not exist' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Data'
            Test-Path $result | Should -Be $true
        }

        It 'Should throw for invalid DirectoryType' {
            { Get-Win11ForgeDirectory -DirectoryType 'InvalidType' } | Should -Throw
        }
    }

    Context 'Get-RegistryPath' {
        It 'Should return CurrentUserRun registry path' {
            $result = Get-RegistryPath -PathKey 'CurrentUserRun'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match '^HKCU:'
        }

        It 'Should return UninstallX64 registry path' {
            $result = Get-RegistryPath -PathKey 'UninstallX64'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match '^HKLM:'
        }

        It 'Should throw for unknown path key' {
            { Get-RegistryPath -PathKey 'NonExistentKey' } | Should -Throw
        }
    }

    Context 'Get-AllRegistryPaths' {
        It 'Should return a hashtable' {
            $result = Get-AllRegistryPaths
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [hashtable]
        }

        It 'Should contain expected registry keys' {
            $result = Get-AllRegistryPaths
            $result.Keys | Should -Contain 'CurrentUserRun'
            $result.Keys | Should -Contain 'UninstallX64'
            $result.Keys | Should -Contain 'ExplorerAdvanced'
        }
    }

    Context 'Get-ConfigPath' {
        It 'Should return Version config path' {
            $result = Get-ConfigPath -PathKey 'Version'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'version\.json$'
        }

        It 'Should return ApiSettings config path' {
            $result = Get-ConfigPath -PathKey 'ApiSettings'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'api-settings\.json$'
        }

        It 'Should throw for unknown config key' {
            { Get-ConfigPath -PathKey 'NonExistentConfig' } | Should -Throw
        }
    }

    Context 'Get-StatePath' {
        It 'Should return RollbackState path' {
            $result = Get-StatePath -PathKey 'RollbackState'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'RollbackState\.json$'
        }

        It 'Should return DeploymentState path' {
            $result = Get-StatePath -PathKey 'DeploymentState'
            $result | Should -Not -BeNullOrEmpty
            $result | Should -Match 'DeploymentState\.json$'
        }

        It 'Should throw for unknown state key' {
            { Get-StatePath -PathKey 'NonExistentState' } | Should -Throw
        }
    }

    Context 'Get-Timeout' {
        It 'Should return DefaultInstall timeout' {
            $result = Get-Timeout -TimeoutKey 'DefaultInstall'
            $result | Should -BeOfType [int]
            $result | Should -BeGreaterThan 0
        }

        It 'Should return Download timeout' {
            $result = Get-Timeout -TimeoutKey 'Download'
            $result | Should -BeOfType [int]
            $result | Should -BeGreaterThan 0
        }

        It 'Should throw for unknown timeout key' {
            { Get-Timeout -TimeoutKey 'NonExistentTimeout' } | Should -Throw
        }
    }

    Context 'Get-ParallelLimit' {
        It 'Should return MaxInstallJobs limit' {
            $result = Get-ParallelLimit -LimitKey 'MaxInstallJobs'
            $result | Should -BeOfType [int]
            $result | Should -BeGreaterThan 0
        }

        It 'Should return MaxScanJobs limit' {
            $result = Get-ParallelLimit -LimitKey 'MaxScanJobs'
            $result | Should -BeOfType [int]
            $result | Should -BeGreaterThan 0
        }

        It 'Should throw for unknown limit key' {
            { Get-ParallelLimit -LimitKey 'NonExistentLimit' } | Should -Throw
        }
    }

    Context 'Get-AllowedDetectionExecutables' {
        It 'Should return an array of strings' {
            $result = Get-AllowedDetectionExecutables
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [string]
        }

        It 'Should include common executables' {
            $result = Get-AllowedDetectionExecutables
            $result | Should -Contain 'git'
            $result | Should -Contain 'python'
            $result | Should -Contain 'node'
        }
    }

    Context 'Get-ShellFolder' {
        It 'Should return Desktop folder path' {
            $result = Get-ShellFolder -FolderType 'Desktop'
            $result | Should -Not -BeNullOrEmpty
            Test-Path $result | Should -Be $true
        }

        It 'Should return Documents folder path' {
            $result = Get-ShellFolder -FolderType 'Documents'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return StartMenu folder path' {
            $result = Get-ShellFolder -FolderType 'StartMenu'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should throw for invalid folder type' {
            { Get-ShellFolder -FolderType 'InvalidFolder' } | Should -Throw
        }
    }

    Context 'Get-RepositoryRoot' {
        It 'Should return a valid path' {
            $result = Get-RepositoryRoot
            $result | Should -Not -BeNullOrEmpty
            Test-Path $result | Should -Be $true
        }

        It 'Should return consistent results' {
            $result1 = Get-RepositoryRoot
            $result2 = Get-RepositoryRoot
            $result1 | Should -Be $result2
        }
    }

    Context 'Module Integration' {
        It 'Should have all expected functions exported' {
            $expectedFunctions = @(
                'Get-Win11ForgeDirectory',
                'Get-RegistryPath',
                'Get-AllRegistryPaths',
                'Get-ConfigPath',
                'Get-StatePath',
                'Get-Timeout',
                'Get-ParallelLimit',
                'Get-AllowedDetectionExecutables',
                'Get-ShellFolder',
                'Get-RepositoryRoot'
            )

            $module = Get-Module DirectoryConstants
            foreach ($func in $expectedFunctions) {
                $module.ExportedFunctions.Keys | Should -Contain $func
            }
        }
    }
}
