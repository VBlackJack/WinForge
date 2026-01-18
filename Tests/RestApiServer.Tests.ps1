<#
.SYNOPSIS
    Pester tests for RestApiServer module

.DESCRIPTION
    Unit tests for Win11Forge RestApiServer v3.1.4
    Tests server lifecycle, endpoint registration, and configuration

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'RestApiServer.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'RestApiServer Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-ApiConfig function' {
            Get-Command Get-ApiConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-ApiEndpoint function' {
            Get-Command Register-ApiEndpoint -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Unregister-ApiEndpoint function' {
            Get-Command Unregister-ApiEndpoint -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RegisteredEndpoints function' {
            Get-Command Get-RegisteredEndpoints -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApiServerStatus function' {
            Get-Command Get-ApiServerStatus -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ApiConfig' {
        It 'Should return configuration hashtable' {
            $config = Get-ApiConfig
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have Port property' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'Port'
        }

        It 'Should have Host property' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'Host'
        }

        It 'Should default to localhost' {
            $config = Get-ApiConfig
            $config.Host | Should -Be 'localhost'
        }

        It 'Should default to port 5170' {
            $config = Get-ApiConfig
            $config.Port | Should -Be 5170
        }
    }

    Context 'Register-ApiEndpoint' {
        It 'Should register endpoint without errors' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'GET' -Handler { return @{} } } | Should -Not -Throw
        }

        It 'Should register POST endpoint' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'POST' -Handler { return @{} } } | Should -Not -Throw
        }

        It 'Should accept Description parameter' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'GET' -Handler { return @{} } -Description 'Test endpoint' } | Should -Not -Throw
        }
    }

    Context 'Get-RegisteredEndpoints' {
        BeforeEach {
            Register-ApiEndpoint -Path '/api/test1' -Method 'GET' -Handler { return @{} }
        }

        It 'Should return array of endpoints' {
            $endpoints = Get-RegisteredEndpoints
            $endpoints | Should -Not -BeNullOrEmpty
        }

        It 'Should include registered endpoint' {
            $endpoints = Get-RegisteredEndpoints
            $endpoints.Path | Should -Contain '/api/test1'
        }
    }

    Context 'Unregister-ApiEndpoint' {
        It 'Should unregister without errors' {
            Register-ApiEndpoint -Path '/api/tounregister' -Method 'GET' -Handler { return @{} }
            { Unregister-ApiEndpoint -Path '/api/tounregister' -Method 'GET' } | Should -Not -Throw
        }
    }

    Context 'Get-ApiServerStatus' {
        It 'Should return status object' {
            $status = Get-ApiServerStatus
            $status | Should -Not -BeNullOrEmpty
        }

        It 'Should have Running property' {
            $status = Get-ApiServerStatus
            $status.PSObject.Properties.Name | Should -Contain 'Running'
        }

        It 'Should have Port property' {
            $status = Get-ApiServerStatus
            $status.PSObject.Properties.Name | Should -Contain 'Port'
        }

        It 'Should show not running when server is stopped' {
            $status = Get-ApiServerStatus
            $status.Running | Should -Be $false
        }
    }
}
