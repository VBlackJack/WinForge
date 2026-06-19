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
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge"
            $result | Should -BeLike "$env:LOCALAPPDATA*"
        }

        It 'Should return Logs directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Logs'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge\Logs"
        }

        It 'Should return Cache directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Cache'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge\Cache"
        }

        It 'Should return Backups directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Backups'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge\Backups"
        }

        It 'Should return Plugins directory path' {
            $result = Get-Win11ForgeDirectory -DirectoryType 'Plugins'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Plugins"
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
            $result | Should -BeOfType [string]
            $result | Should -Be 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        }

        It 'Should return UninstallX64 registry path' {
            $result = Get-RegistryPath -PathKey 'UninstallX64'
            $result | Should -BeOfType [string]
            $result | Should -Be 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
        }

        It 'Should throw for unknown path key' {
            { Get-RegistryPath -PathKey 'NonExistentKey' } | Should -Throw
        }
    }

    Context 'Get-AllRegistryPaths' {
        It 'Should return a hashtable' {
            $result = Get-AllRegistryPaths
            $result | Should -BeOfType [hashtable]
            $result.Count | Should -BeGreaterOrEqual 15
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
            $result | Should -BeOfType [string]
            $result | Should -Match '[/\\]Config[/\\]version\.json$'
        }

        It 'Should return ApiSettings config path' {
            $result = Get-ConfigPath -PathKey 'ApiSettings'
            $result | Should -BeOfType [string]
            $result | Should -Match '[/\\]Config[/\\]api-settings\.json$'
        }

        It 'Should throw for unknown config key' {
            { Get-ConfigPath -PathKey 'NonExistentConfig' } | Should -Throw
        }
    }

    Context 'Get-StatePath' {
        It 'Should return RollbackState path' {
            $result = Get-StatePath -PathKey 'RollbackState'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge\RollbackState.json"
        }

        It 'Should return DeploymentState path' {
            $result = Get-StatePath -PathKey 'DeploymentState'
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*\Win11Forge\DeploymentState.json"
        }

        It 'Should throw for unknown state key' {
            { Get-StatePath -PathKey 'NonExistentState' } | Should -Throw
        }
    }

    Context 'Get-Timeout' {
        It 'Should return DefaultInstall timeout' {
            $result = Get-Timeout -TimeoutKey 'DefaultInstall'
            $result | Should -BeOfType [int]
            $result | Should -Be 1800
        }

        It 'Should return Download timeout' {
            $result = Get-Timeout -TimeoutKey 'Download'
            $result | Should -BeOfType [int]
            $result | Should -Be 300
        }

        It 'Should throw for unknown timeout key' {
            { Get-Timeout -TimeoutKey 'NonExistentTimeout' } | Should -Throw
        }
    }

    Context 'Get-ParallelLimit' {
        It 'Should return MaxInstallJobs limit' {
            $result = Get-ParallelLimit -LimitKey 'MaxInstallJobs'
            $result | Should -BeOfType [int]
            $result | Should -Be 5
        }

        It 'Should return MaxScanJobs limit' {
            $result = Get-ParallelLimit -LimitKey 'MaxScanJobs'
            $result | Should -BeOfType [int]
            $result | Should -Be 8
        }

        It 'Should throw for unknown limit key' {
            { Get-ParallelLimit -LimitKey 'NonExistentLimit' } | Should -Throw
        }
    }

    Context 'Get-NetworkDefault' {
        It 'Should return ConnectivityTestHost' {
            $result = Get-NetworkDefault -SettingKey 'ConnectivityTestHost'
            $result | Should -BeOfType [string]
            $result | Should -Be '8.8.8.8'
        }

        It 'Should return ConnectivityTestCount' {
            $result = Get-NetworkDefault -SettingKey 'ConnectivityTestCount'
            $result | Should -BeOfType [int]
            $result | Should -Be 1
        }

        It 'Should throw for unknown setting key' {
            { Get-NetworkDefault -SettingKey 'NonExistentSetting' } | Should -Throw
        }
    }

    Context 'Get-ShellFolder' {
        It 'Should return Desktop folder path' {
            $result = Get-ShellFolder -FolderType 'Desktop'
            $result | Should -BeOfType [string]
            $result | Should -Be ([Environment]::GetFolderPath('Desktop'))
            Test-Path $result | Should -Be $true
        }

        It 'Should return Documents folder path' {
            $result = Get-ShellFolder -FolderType 'Documents'
            $result | Should -BeOfType [string]
            $result | Should -Be ([Environment]::GetFolderPath('MyDocuments'))
        }

        It 'Should return StartMenu folder path' {
            $result = Get-ShellFolder -FolderType 'StartMenu'
            $result | Should -BeOfType [string]
            $result | Should -Be ([Environment]::GetFolderPath('StartMenu'))
        }

        It 'Should throw for invalid folder type' {
            { Get-ShellFolder -FolderType 'InvalidFolder' } | Should -Throw
        }
    }

    Context 'Get-RepositoryRoot' {
        It 'Should return a valid path' {
            $result = Get-RepositoryRoot
            $result | Should -BeOfType [string]
            $result | Should -BeLike "*Win11Forge*"
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
                'Get-NetworkDefault',
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
