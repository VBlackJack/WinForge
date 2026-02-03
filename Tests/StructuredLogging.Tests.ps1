<#
.SYNOPSIS
    Pester tests for StructuredLogging module

.DESCRIPTION
    Unit tests for Win11Forge StructuredLogging v3.1.4
    Tests JSON logging, buffering, and export functions

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'StructuredLogging.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'StructuredLogging Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-StructuredLogging function' {
            Get-Command Initialize-StructuredLogging -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Write-StructuredLog function' {
            Get-Command Write-StructuredLog -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-LogBuffer function' {
            Get-Command Clear-LogBuffer -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Export-LogsToJson function' {
            Get-Command Export-LogsToJson -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-StructuredLogs function' {
            Get-Command Get-StructuredLogs -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Close-StructuredLogging function' {
            Get-Command Close-StructuredLogging -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-LoggingStatistics function' {
            Get-Command Get-LoggingStatistics -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-StructuredLogging' {
        It 'Should initialize without errors' {
            { Initialize-StructuredLogging } | Should -Not -Throw
        }

        It 'Should accept custom SessionId' {
            { Initialize-StructuredLogging -SessionId 'TestSession' } | Should -Not -Throw
        }
    }

    Context 'Write-StructuredLog' {
        BeforeEach {
            Initialize-StructuredLogging -SessionId 'TestSession'
        }

        It 'Should write Info level log' {
            { Write-StructuredLog -Level 'Info' -Message 'Test message' } | Should -Not -Throw
        }

        It 'Should write Error level log' {
            { Write-StructuredLog -Level 'Error' -Message 'Error message' } | Should -Not -Throw
        }

        It 'Should accept Category parameter' {
            { Write-StructuredLog -Level 'Info' -Category 'Installation' -Message 'Test' } | Should -Not -Throw
        }

        It 'Should accept Data parameter' {
            { Write-StructuredLog -Level 'Info' -Message 'Test' -Data @{ AppName = 'VSCode' } } | Should -Not -Throw
        }
    }

    Context 'Get-LoggingStatistics' {
        It 'Should return statistics object' {
            Initialize-StructuredLogging
            $stats = Get-LoggingStatistics
            $stats | Should -Not -BeNullOrEmpty
        }

        It 'Should have Initialized property' {
            Initialize-StructuredLogging
            $stats = Get-LoggingStatistics
            $stats.Initialized | Should -Be $true
        }

        It 'Should have CurrentSessionId property' {
            Initialize-StructuredLogging
            $stats = Get-LoggingStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'CurrentSessionId'
        }
    }

    Context 'Close-StructuredLogging' {
        It 'Should close without errors' {
            Initialize-StructuredLogging
            { Close-StructuredLogging } | Should -Not -Throw
        }
    }
}
