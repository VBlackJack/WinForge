<#
.SYNOPSIS
    Pester tests for ScheduledDeployment module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge ScheduledDeployment v3.5.0
    Tests scheduled deployment creation, listing, and management

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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
    # Import modules under test
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:ScheduledDeploymentPath = Join-Path $script:ModuleRoot 'ScheduledDeployment.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first (provides Write-Status)
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import ScheduledDeployment
    Import-Module $script:ScheduledDeploymentPath -Force -ErrorAction Stop

    # Check if we have admin rights for integration tests
    $script:IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Describe 'ScheduledDeployment Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ScheduledDeploymentPath -Force } | Should -Not -Throw
        }

        It 'Should export New-ScheduledDeployment function' {
            Get-Command New-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ScheduledDeployment function' {
            Get-Command Get-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-ScheduledDeployment function' {
            Get-Command Remove-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Enable-ScheduledDeployment function' {
            Get-Command Enable-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-ScheduledDeployment function' {
            Get-Command Disable-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Start-ScheduledDeployment function' {
            Get-Command Start-ScheduledDeployment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ScheduledDeploymentSummary function' {
            Get-Command Get-ScheduledDeploymentSummary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ScheduledTasksAvailable function' {
            Get-Command Test-ScheduledTasksAvailable -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-ScheduledTasksAvailable' {
        It 'Should return boolean' {
            $result = Test-ScheduledTasksAvailable
            $result | Should -BeOfType [bool]
        }

        It 'Should return true on Windows with ScheduledTasks module' {
            # On Windows, this should typically be true
            if ($IsWindows -or $env:OS -eq 'Windows_NT') {
                Test-ScheduledTasksAvailable | Should -BeTrue
            }
        }
    }

    Context 'New-ScheduledDeployment Parameter Validation' {
        It 'Should require ProfileName parameter' {
            { New-ScheduledDeployment -ScheduledTime (Get-Date).AddHours(1) } | Should -Throw
        }

        It 'Should require ScheduledTime parameter' {
            { New-ScheduledDeployment -ProfileName 'Base' } | Should -Throw
        }

        It 'Should accept valid TriggerType values' {
            # Validate that the parameter set validation works for each valid type
            $validTypes = @('OneTime', 'Daily', 'Weekly', 'AtStartup', 'AtLogon')
            foreach ($type in $validTypes) {
                # We only test parameter binding here - actual execution will fail due to admin check or profile validation
                $cmd = Get-Command New-ScheduledDeployment
                $paramSet = $cmd.Parameters['TriggerType']
                $paramSet.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] } |
                    ForEach-Object { $_.ValidValues } | Should -Contain $type -Because "TriggerType should accept '$type'"
            }
        }

        It 'Should reject invalid TriggerType' {
            {
                New-ScheduledDeployment -ProfileName 'Base' -ScheduledTime (Get-Date).AddHours(1) -TriggerType 'InvalidType'
            } | Should -Throw
        }
    }

    Context 'Get-ScheduledDeployment' {
        It 'Should return array or empty without errors' {
            { Get-ScheduledDeployment } | Should -Not -Throw
        }

        It 'Should accept Id parameter' {
            { Get-ScheduledDeployment -Id 'test123' } | Should -Not -Throw
        }

        It 'Should accept ProfileName parameter' {
            { Get-ScheduledDeployment -ProfileName 'Base' } | Should -Not -Throw
        }

        It 'Should accept IncludeCompleted switch' {
            { Get-ScheduledDeployment -IncludeCompleted } | Should -Not -Throw
        }
    }

    Context 'Get-ScheduledDeploymentSummary' {
        It 'Should return hashtable with expected keys' {
            $summary = Get-ScheduledDeploymentSummary

            $summary | Should -BeOfType [hashtable]
            $summary.Keys | Should -Contain 'Total'
            $summary.Keys | Should -Contain 'Pending'
            $summary.Keys | Should -Contain 'Running'
            $summary.Keys | Should -Contain 'Completed'
            $summary.Keys | Should -Contain 'Failed'
            $summary.Keys | Should -Contain 'Cancelled'
        }

        It 'Should return non-negative counts' {
            $summary = Get-ScheduledDeploymentSummary

            $summary.Total | Should -BeGreaterOrEqual 0
            $summary.Pending | Should -BeGreaterOrEqual 0
            $summary.Running | Should -BeGreaterOrEqual 0
        }
    }
}

Describe 'ScheduledDeployment Integration Tests' -Skip:(-not $script:IsAdmin) {
    # These tests require administrator privileges and actually create/remove tasks

    BeforeAll {
        $script:TestProfilesDir = Join-Path $PSScriptRoot '..\Profiles'
        $script:TestDeploymentId = $null
    }

    AfterAll {
        # Cleanup any test tasks
        if ($script:TestDeploymentId) {
            Remove-ScheduledDeployment -Id $script:TestDeploymentId -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Create and Manage Scheduled Deployment' {
        It 'Should create a one-time scheduled deployment' {
            $scheduledTime = (Get-Date).AddHours(24)

            $deployment = New-ScheduledDeployment `
                -ProfileName 'Base' `
                -ScheduledTime $scheduledTime `
                -TriggerType 'OneTime' `
                -TestMode

            $deployment | Should -Not -BeNullOrEmpty
            $deployment.Id | Should -Not -BeNullOrEmpty
            $deployment.ProfileName | Should -Be 'Base'
            $deployment.TriggerType | Should -Be 'OneTime'
            $deployment.Status | Should -Be 'Pending'

            $script:TestDeploymentId = $deployment.Id
        }

        It 'Should list the created deployment' {
            $deployments = Get-ScheduledDeployment -Id $script:TestDeploymentId

            $deployments | Should -Not -BeNullOrEmpty
            $deployments.Id | Should -Be $script:TestDeploymentId
        }

        It 'Should disable the deployment' {
            { Disable-ScheduledDeployment -Id $script:TestDeploymentId } | Should -Not -Throw

            $deployment = Get-ScheduledDeployment -Id $script:TestDeploymentId
            $deployment.Status | Should -Be 'Cancelled'
        }

        It 'Should enable the deployment' {
            { Enable-ScheduledDeployment -Id $script:TestDeploymentId } | Should -Not -Throw

            $deployment = Get-ScheduledDeployment -Id $script:TestDeploymentId
            $deployment.Status | Should -Be 'Pending'
        }

        It 'Should remove the deployment' {
            { Remove-ScheduledDeployment -Id $script:TestDeploymentId -Force } | Should -Not -Throw

            $deployment = Get-ScheduledDeployment -Id $script:TestDeploymentId
            $deployment | Should -BeNullOrEmpty

            $script:TestDeploymentId = $null
        }
    }

    Context 'Profile Validation' {
        It 'Should throw for non-existent profile' {
            {
                New-ScheduledDeployment `
                    -ProfileName 'NonExistentProfile12345' `
                    -ScheduledTime (Get-Date).AddHours(1)
            } | Should -Throw
        }
    }
}

Describe 'ScheduledDeployment Error Handling' {
    Context 'Non-Admin Operations' {
        It 'Should throw when creating deployment without admin rights' {
            # Skip this test if running as admin
            $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($isAdmin) {
                Set-ItResult -Skipped -Because 'Running as administrator - test requires non-admin context'
                return
            }

            {
                New-ScheduledDeployment `
                    -ProfileName 'Base' `
                    -ScheduledTime (Get-Date).AddHours(1)
            } | Should -Throw
        }
    }

    Context 'Admin Operations Cleanup' {
        AfterAll {
            # Cleanup any tasks that might have been created during testing
            $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($isAdmin) {
                Get-ScheduledDeployment | ForEach-Object {
                    Remove-ScheduledDeployment -Id $_.Id -Force -ErrorAction SilentlyContinue
                }
            }
        }

        It 'Admin detection works correctly' {
            # Just verify that admin detection works - this test always runs
            $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            $isAdmin | Should -BeOfType [bool]
        }
    }
}
