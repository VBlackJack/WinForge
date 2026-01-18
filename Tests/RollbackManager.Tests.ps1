<#
.SYNOPSIS
    Pester tests for RollbackManager module

.DESCRIPTION
    Unit tests for Win11Forge RollbackManager v3.1.4
    Tests auto-rollback, failure tracking, and reporting functions

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'RollbackManager.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'RollbackManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-RollbackConfig function' {
            Get-Command Get-RollbackConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Enable-AutoRollbackOnFailure function' {
            Get-Command Enable-AutoRollbackOnFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-AutoRollbackOnFailure function' {
            Get-Command Disable-AutoRollbackOnFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-CriticalFailureHandler function' {
            Get-Command Register-CriticalFailureHandler -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-InstallationFailure function' {
            Get-Command Register-InstallationFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RollbackSummary function' {
            Get-Command Get-RollbackSummary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-RollbackCapability function' {
            Get-Command Test-RollbackCapability -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-RollbackConfig' {
        It 'Should return configuration hashtable' {
            $config = Get-RollbackConfig
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have AutoRollbackEnabled property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'AutoRollbackEnabled'
        }

        It 'Should have AutoRollbackThreshold property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'AutoRollbackThreshold'
        }
    }

    Context 'Test-RollbackCapability' {
        It 'Should return true for Winget method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Winget' | Should -Be $true
        }

        It 'Should return true for Chocolatey method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Chocolatey' | Should -Be $true
        }

        It 'Should return false for DirectDownload method' {
            Test-RollbackCapability -AppName 'Test' -Method 'DirectDownload' | Should -Be $false
        }

        It 'Should return false for WindowsFeature method' {
            Test-RollbackCapability -AppName 'Test' -Method 'WindowsFeature' | Should -Be $false
        }
    }

    Context 'Enable-AutoRollbackOnFailure' {
        It 'Should enable without errors' {
            { Enable-AutoRollbackOnFailure } | Should -Not -Throw
        }

        It 'Should accept custom threshold' {
            { Enable-AutoRollbackOnFailure -Threshold 5 } | Should -Not -Throw
        }
    }

    Context 'Disable-AutoRollbackOnFailure' {
        It 'Should disable without errors' {
            { Disable-AutoRollbackOnFailure } | Should -Not -Throw
        }
    }

    Context 'Register-InstallationFailure' {
        It 'Should register failure and return details' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Test error'
            $result | Should -Not -BeNullOrEmpty
            $result.AppName | Should -Be 'TestApp'
        }

        It 'Should increment failure count' {
            Reset-FailureCount
            Register-InstallationFailure -AppName 'Test1' -ErrorMessage 'Error1'
            Register-InstallationFailure -AppName 'Test2' -ErrorMessage 'Error2'
            $result = Register-InstallationFailure -AppName 'Test3' -ErrorMessage 'Error3'
            $result.FailureNumber | Should -Be 3
        }
    }

    Context 'Get-RollbackSummary' {
        It 'Should return summary object' {
            $summary = Get-RollbackSummary
            $summary | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalApps property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'TotalApps'
        }
    }
}
