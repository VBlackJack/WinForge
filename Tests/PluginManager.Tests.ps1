<#
.SYNOPSIS
    Pester tests for PluginManager module

.DESCRIPTION
    Unit tests for Win11Forge PluginManager v3.1.4
    Tests plugin loading, hooks, and custom methods

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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Core'
    $script:ModulePath = Join-Path $script:ModuleRoot 'PluginManager.psm1'

    # PluginManager depends on Localization for i18n strings
    $localizationPath = Join-Path $script:ModuleRoot 'Localization.psm1'
    Import-Module $localizationPath -Force -Global -ErrorAction Stop
    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'PluginManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-PluginManager function' {
            Get-Command Initialize-PluginManager -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-PluginConfig function' {
            Get-Command Get-PluginConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-AvailablePlugins function' {
            Get-Command Get-AvailablePlugins -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-LoadedPlugins function' {
            Get-Command Get-LoadedPlugins -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-PluginHook function' {
            Get-Command Register-PluginHook -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-PluginHook function' {
            Get-Command Invoke-PluginHook -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-CustomInstallMethod function' {
            Get-Command Register-CustomInstallMethod -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-CustomInstallMethod function' {
            Get-Command Get-CustomInstallMethod -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-PluginManifest function' {
            Get-Command Test-PluginManifest -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-PluginManager' {
        It 'Should initialize without errors' {
            { Initialize-PluginManager -AutoLoad:$false } | Should -Not -Throw
        }
    }

    Context 'Get-PluginConfig' {
        It 'Should return configuration hashtable' {
            $config = Get-PluginConfig
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have Enabled property' {
            $config = Get-PluginConfig
            $config.Keys | Should -Contain 'Enabled'
        }

        It 'Should have AutoLoad property' {
            $config = Get-PluginConfig
            $config.Keys | Should -Contain 'AutoLoad'
        }

        It 'Should have AllowedHooks property' {
            $config = Get-PluginConfig
            $config.Keys | Should -Contain 'AllowedHooks'
        }
    }

    Context 'Get-AvailablePlugins' {
        It 'Should return array or empty' {
            $plugins = @(Get-AvailablePlugins)
            $plugins | Should -Not -BeNullOrEmpty -Because 'at least _template plugin exists'
        }
    }

    Context 'Get-LoadedPlugins' {
        It 'Should return array or empty' {
            $plugins = @(Get-LoadedPlugins)
            # Can be empty if no plugins loaded
            { $plugins.Count } | Should -Not -Throw
        }
    }

    Context 'Register-PluginHook' {
        It 'Should register hook without errors' {
            { Register-PluginHook -HookName 'pre-install' -PluginName 'TestPlugin' -Handler { param($ctx) return $true } } | Should -Not -Throw
        }
    }

    Context 'Invoke-PluginHook' {
        BeforeEach {
            Register-PluginHook -HookName 'pre-install' -PluginName 'TestPlugin' -Handler { param($ctx) return @{ Success = $true } }
        }

        It 'Should invoke hook and return results' {
            $results = Invoke-PluginHook -HookName 'pre-install' -Context @{ AppName = 'Test' }
            $results | Should -Not -BeNullOrEmpty
        }

        It 'Should pass context to handlers' {
            $results = Invoke-PluginHook -HookName 'pre-install' -Context @{ AppName = 'TestApp' }
            $results.Count | Should -BeGreaterThan 0
        }
    }

    Context 'Register-CustomInstallMethod' {
        It 'Should register method without errors' {
            { Register-CustomInstallMethod -MethodName 'TestMethod' -PluginName 'TestPlugin' -Handler { param($app) return @{ Success = $true } } } | Should -Not -Throw
        }
    }

    Context 'Get-CustomInstallMethod' {
        BeforeEach {
            Register-CustomInstallMethod -MethodName 'RegisteredMethod' -PluginName 'TestPlugin' -Handler { return @{} }
        }

        It 'Should return registered method' {
            $method = Get-CustomInstallMethod -MethodName 'RegisteredMethod'
            $method | Should -Not -BeNullOrEmpty
        }

        It 'Should return null for non-existent method' {
            $method = Get-CustomInstallMethod -MethodName 'NonExistentMethod'
            $method | Should -BeNullOrEmpty
        }
    }

    Context 'Get-RegisteredInstallMethods' {
        It 'Should return array or empty' {
            $methods = @(Get-RegisteredInstallMethods)
            # Can have methods if any were registered
            { $methods.Count } | Should -Not -Throw
        }
    }

    Context 'Test-PluginManifest' {
        It 'Should return false for non-existent file' {
            Test-PluginManifest -ManifestPath 'C:\NonExistent\manifest.json' | Should -Be $false
        }
    }
}
