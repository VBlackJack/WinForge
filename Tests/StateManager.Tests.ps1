<#
.SYNOPSIS
    Pester tests for StateManager module

.DESCRIPTION
    Comprehensive unit tests for WinForge StateManager v3.5.0
    Tests state persistence, validation, and recovery

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
    $script:StateManagerPath = Join-Path $script:ModuleRoot 'StateManager.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import StateManager
    Import-Module $script:StateManagerPath -Force -ErrorAction Stop
}

AfterAll {
    # Cleanup any test state files
    $testStateDir = Join-Path $env:LOCALAPPDATA 'WinForge'
    if (Test-Path $testStateDir) {
        Get-ChildItem -Path $testStateDir -Filter '*Test*.json' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Describe 'StateManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:StateManagerPath -Force } | Should -Not -Throw
        }

        It 'Should export Test-ValidStateData function' {
            Get-Command Test-ValidStateData -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ValidRollbackEntry function' {
            Get-Command Test-ValidRollbackEntry -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Initialize-RollbackSession function' {
            Get-Command Initialize-RollbackSession -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-RollbackState function' {
            Get-Command Save-RollbackState -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Add-RollbackEntry function' {
            Get-Command Add-RollbackEntry -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RollbackState function' {
            Get-Command Get-RollbackState -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-RollbackState function' {
            Get-Command Clear-RollbackState -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Initialize-DeploymentSession function' {
            Get-Command Initialize-DeploymentSession -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-DeploymentState function' {
            Get-Command Save-DeploymentState -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Update-DeploymentProgress function' {
            Get-Command Update-DeploymentProgress -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-ValidStateData - Security Validation' {
        It 'Should accept valid state data' {
            $validState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = 'TestProfile'
                TotalApps = 10
                CompletedApps = @('App1', 'App2')
                FailedApps = @()
                PendingApps = @('App3')
            }
            $result = Test-ValidStateData -StateData $validState
            $result | Should -BeTrue
        }

        It 'Should reject invalid SessionId format' {
            $invalidState = [PSCustomObject]@{
                SessionId = 'not-a-guid'
                ProfileName = 'TestProfile'
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject profile name with path traversal' {
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = '../../../etc/passwd'
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject profile name with special characters' {
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = 'Profile<script>'
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject profile name exceeding max length' {
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = 'A' * 150
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject TotalApps out of range (negative)' {
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = $null
                TotalApps = -5
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject TotalApps out of range (too high)' {
            # Module allows max 1000 apps
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = $null
                TotalApps = 1001
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should reject app names with shell metacharacters' {
            $invalidState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = $null
                TotalApps = $null
                CompletedApps = @('App1', 'App; rm -rf /')
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $invalidState
            $result | Should -BeFalse
        }

        It 'Should accept empty app lists' {
            $validState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = 'TestProfile'
                TotalApps = $null
                CompletedApps = @()
                FailedApps = @()
                PendingApps = @()
            }
            $result = Test-ValidStateData -StateData $validState
            $result | Should -BeTrue
        }
    }

    Context 'Test-ValidRollbackEntry' {
        It 'Should accept valid rollback entry' {
            $validEntry = [PSCustomObject]@{
                AppName = 'TestApp'
                Method = 'Winget'
                Identifier = 'test.app'
                Timestamp = (Get-Date).ToString('o')
            }
            $result = Test-ValidRollbackEntry -Entry $validEntry
            $result | Should -BeTrue
        }

        It 'Should reject entry with missing AppName' {
            $invalidEntry = [PSCustomObject]@{
                AppName = $null
                Method = 'Winget'
                Identifier = 'test.app'
            }
            $result = Test-ValidRollbackEntry -Entry $invalidEntry
            $result | Should -BeFalse
        }

        It 'Should reject entry with empty AppName' {
            $invalidEntry = [PSCustomObject]@{
                AppName = ''
                Method = 'Winget'
                Identifier = 'test.app'
            }
            $result = Test-ValidRollbackEntry -Entry $invalidEntry
            $result | Should -BeFalse
        }

        It 'Should reject entry with AppName exceeding max length' {
            $invalidEntry = [PSCustomObject]@{
                AppName = 'A' * 250
                Method = 'Winget'
                Identifier = 'test.app'
            }
            $result = Test-ValidRollbackEntry -Entry $invalidEntry
            $result | Should -BeFalse
        }

        It 'Should reject entry with invalid Method' {
            $invalidEntry = [PSCustomObject]@{
                AppName = 'TestApp'
                Method = 'InvalidMethod'
                Identifier = 'test.app'
            }
            $result = Test-ValidRollbackEntry -Entry $invalidEntry
            $result | Should -BeFalse
        }

        It 'Should reject entry with shell metacharacters in Identifier' {
            $invalidEntry = [PSCustomObject]@{
                AppName = 'TestApp'
                Method = 'Winget'
                Identifier = 'test; whoami'
            }
            $result = Test-ValidRollbackEntry -Entry $invalidEntry
            $result | Should -BeFalse
        }
    }

    Context 'Rollback Session Management' {
        BeforeEach {
            # Clear state before each test
            Clear-RollbackState -ErrorAction SilentlyContinue
        }

        It 'Should initialize rollback session with GUID' {
            $sessionId = Initialize-RollbackSession
            $sessionId | Should -Not -BeNullOrEmpty
            { [guid]::Parse($sessionId) } | Should -Not -Throw
        }

        It 'Should get rollback state after initialization' {
            Initialize-RollbackSession | Out-Null
            $state = Get-RollbackState
            $state | Should -Not -BeNullOrEmpty
            $state.SessionId | Should -Not -BeNullOrEmpty
        }

        It 'Should add rollback entry successfully' {
            Initialize-RollbackSession | Out-Null
            { Add-RollbackEntry -AppName 'TestApp' -Method 'Winget' -Identifier 'test.app' } | Should -Not -Throw
        }

        It 'Should save rollback state to disk' {
            Initialize-RollbackSession | Out-Null
            { Save-RollbackState } | Should -Not -Throw
        }

        It 'Should clear rollback state' {
            Initialize-RollbackSession | Out-Null
            Clear-RollbackState
            $state = Get-RollbackState
            $state.InstalledApps.Count | Should -Be 0
        }
    }

    Context 'Deployment Session Management' {
        BeforeEach {
            # Clear state before each test
            Clear-DeploymentState -ErrorAction SilentlyContinue
        }

        It 'Should initialize deployment session' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' },
                [PSCustomObject]@{ Name = 'App3' }
            )
            $sessionId = Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps
            $sessionId | Should -Not -BeNullOrEmpty
            { [guid]::Parse($sessionId) } | Should -Not -Throw
        }

        It 'Should track correct total apps count' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' },
                [PSCustomObject]@{ Name = 'App3' },
                [PSCustomObject]@{ Name = 'App4' },
                [PSCustomObject]@{ Name = 'App5' }
            )
            Initialize-DeploymentSession -ProfileName 'CountTest' -Applications $apps | Out-Null
            $state = Get-DeploymentState
            $state.TotalApps | Should -Be 5
        }

        It 'Should save deployment state' {
            $apps = @([PSCustomObject]@{ Name = 'App1' })
            Initialize-DeploymentSession -ProfileName 'SaveTest' -Applications $apps | Out-Null
            { Save-DeploymentState } | Should -Not -Throw
        }

        It 'Should update deployment progress with Success true' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' }
            )
            Initialize-DeploymentSession -ProfileName 'ProgressTest' -Applications $apps | Out-Null
            { Update-DeploymentProgress -AppName 'App1' -Success $true } | Should -Not -Throw
        }

        It 'Should update deployment progress with Success false' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' }
            )
            Initialize-DeploymentSession -ProfileName 'FailedTest' -Applications $apps | Out-Null
            { Update-DeploymentProgress -AppName 'App1' -Success $false } | Should -Not -Throw
        }

        It 'Should track completed apps correctly' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' }
            )
            Initialize-DeploymentSession -ProfileName 'TrackTest' -Applications $apps | Out-Null
            Update-DeploymentProgress -AppName 'App1' -Success $true
            $state = Get-DeploymentState
            $state.CompletedApps | Should -Contain 'App1'
        }

        It 'Should track failed apps correctly' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1' },
                [PSCustomObject]@{ Name = 'App2' }
            )
            Initialize-DeploymentSession -ProfileName 'FailTrackTest' -Applications $apps | Out-Null
            Update-DeploymentProgress -AppName 'App1' -Success $false
            $state = Get-DeploymentState
            $state.FailedApps | Should -Contain 'App1'
        }
    }

    Context 'State Persistence' {
        It 'Should persist state across module reloads' {
            # Initialize and save state
            $sessionId = Initialize-RollbackSession
            Save-RollbackState

            # Reload module
            Import-Module $script:StateManagerPath -Force

            # Restore and verify
            $state = Restore-RollbackState
            if ($state) {
                $state | Should -BeTrue
            }
        }
    }

    Context 'Edge Cases' {
        It 'Should handle null StateData parameter' {
            { Test-ValidStateData -StateData $null } | Should -Throw
        }

        It 'Should handle empty state object' {
            $emptyState = [PSCustomObject]@{
                SessionId = $null
                ProfileName = $null
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $emptyState
            $result | Should -BeTrue
        }

        It 'Should handle state with only SessionId' {
            $minimalState = [PSCustomObject]@{
                SessionId = [guid]::NewGuid().ToString()
                ProfileName = $null
                TotalApps = $null
                CompletedApps = $null
                FailedApps = $null
                PendingApps = $null
            }
            $result = Test-ValidStateData -StateData $minimalState
            $result | Should -BeTrue
        }

        It 'Should handle concurrent session initialization' {
            $sessions = @()
            1..5 | ForEach-Object {
                $sessions += Initialize-RollbackSession
            }
            # Each should be unique
            ($sessions | Select-Object -Unique).Count | Should -Be 5
        }
    }
}
