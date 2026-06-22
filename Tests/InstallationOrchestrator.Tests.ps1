<#
.SYNOPSIS
    WinForge - InstallationOrchestrator Module Tests v3.5.0

.DESCRIPTION
    Pester tests for the InstallationOrchestrator module.
    Tests rollback, deployment state, and orchestration functions.

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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
    # Import via InstallationEngine which loads StateManager + Orchestrator as nested modules.
    # This makes both state management and orchestration functions available.
    $EnginePath = Join-Path $PSScriptRoot '..\Modules\InstallationEngine.psd1'
    Import-Module $EnginePath -Force -ErrorAction Stop

    # Test data directory
    $script:TestStateDir = Join-Path $env:TEMP "WinForgeTest_$([Guid]::NewGuid().ToString('N'))"
    if (-not (Test-Path $script:TestStateDir)) {
        New-Item -Path $script:TestStateDir -ItemType Directory -Force | Out-Null
    }
}

AfterAll {
    # Cleanup test state directory
    if (Test-Path $script:TestStateDir) {
        Remove-Item -Path $script:TestStateDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe 'InstallationOrchestrator Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $EnginePath -Force } | Should -Not -Throw
        }

        It 'Should have Initialize-RollbackSession available' {
            Get-Command -Name 'Initialize-RollbackSession' -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should have Initialize-DeploymentSession available' {
            Get-Command -Name 'Initialize-DeploymentSession' -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should have Install-Application available' {
            Get-Command -Name 'Install-Application' -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should have Install-ApplicationsParallel available' {
            Get-Command -Name 'Install-ApplicationsParallel' -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should have Get-ApplicationSources available' {
            Get-Command -Name 'Get-ApplicationSources' -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-ValidStateData' {
        It 'Should validate correct state data with all properties' {
            $validData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'TestProfile'
                StartTime = (Get-Date).ToString('o')
                TotalApps = 5
                CompletedApps = @()
                FailedApps = @()
                PendingApps = @()
            }
            $result = Test-ValidStateData -StateData $validData
            $result | Should -BeTrue
        }

        It 'Should reject data with path traversal in SessionId' {
            $invalidData = @{
                SessionId = '../../../etc/passwd'
                ProfileName = 'TestProfile'
                StartTime = (Get-Date).ToString('o')
                CompletedApps = @()
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject data with invalid SessionId format' {
            $invalidData = @{
                SessionId = 'not-a-guid'
                ProfileName = 'TestProfile'
                CompletedApps = @()
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }
    }

    Context 'Test-EnvironmentRestriction' {
        It 'Should return hashtable' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result | Should -BeOfType [hashtable]
        }

        It 'Should have Restricted property' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Keys | Should -Contain 'Restricted'
        }

        It 'Should allow apps without environment restrictions' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Restricted | Should -BeFalse
        }
    }

    Context 'Get-ApplicationSources' {
        It 'Should return sources from app' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = @{
                    Winget = 'TestApp.ID'
                    Chocolatey = 'testapp'
                }
            }
            $result = Get-ApplicationSources -Application $app
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return null for app without sources' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
            }
            $result = Get-ApplicationSources -Application $app
            $result | Should -BeNullOrEmpty
        }
    }
}

Describe 'Rollback Functions' {
    Context 'Initialize-RollbackSession' {
        It 'Should initialize without errors' {
            { Initialize-RollbackSession } | Should -Not -Throw
        }
    }

    Context 'Get-RollbackState' {
        BeforeEach {
            Clear-RollbackState -ErrorAction SilentlyContinue
        }

        It 'Should return hashtable or null' {
            $result = Get-RollbackState
            if ($result) {
                $result | Should -BeOfType [hashtable]
            }
        }
    }

    Context 'Add-RollbackEntry' {
        BeforeEach {
            Clear-RollbackState -ErrorAction SilentlyContinue
            Initialize-RollbackSession
        }

        It 'Should add entry without error' {
            { Add-RollbackEntry -AppName 'TestApp' -Method 'Winget' -Identifier 'Test.App' } | Should -Not -Throw
        }

        It 'Should track multiple entries' {
            Add-RollbackEntry -AppName 'App1' -Method 'Winget' -Identifier 'Test.App1'
            Add-RollbackEntry -AppName 'App2' -Method 'Chocolatey' -Identifier 'app2'
            $state = Get-RollbackState
            if ($state -and $state.InstalledApps) {
                $state.InstalledApps.Count | Should -BeGreaterOrEqual 2
            }
        }
    }

    Context 'Clear-RollbackState' {
        It 'Should clear state without error' {
            Initialize-RollbackSession
            { Clear-RollbackState } | Should -Not -Throw
        }
    }
}

Describe 'Deployment State Functions' {
    Context 'Initialize-DeploymentSession' {
        It 'Should initialize with required parameters' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Sources = @{ Winget = 'Test.App1' } }
            )
            { Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps } | Should -Not -Throw
        }
    }

    Context 'Get-DeploymentState' {
        BeforeEach {
            Clear-DeploymentState -ErrorAction SilentlyContinue
        }

        It 'Should return null when no state exists' {
            $result = Get-DeploymentState
            $result | Should -BeNullOrEmpty
        }
    }

    Context 'Test-IncompleteDeployment' {
        BeforeEach {
            Clear-DeploymentState -ErrorAction SilentlyContinue
        }

        It 'Should return boolean' {
            $result = Test-IncompleteDeployment
            $result | Should -BeOfType [bool]
        }

        It 'Should return false when no state exists' {
            $result = Test-IncompleteDeployment
            $result | Should -BeFalse
        }
    }

    Context 'Clear-DeploymentState' {
        It 'Should clear without error' {
            { Clear-DeploymentState } | Should -Not -Throw
        }
    }
}

Describe 'Installation Orchestration' {
    # Note: Invoke-InstallationMethodSequence and Install-Application require complete app objects
    # with all properties from the database. Testing with minimal objects causes property access errors.
    # These functions are tested through integration tests with real database objects.

    Context 'Function Exports' {
        It 'Should export Invoke-InstallationMethodSequence function' {
            Get-Command -Name 'Invoke-InstallationMethodSequence' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-Application function' {
            Get-Command -Name 'Install-Application' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ApplicationsParallel function' {
            Get-Command -Name 'Install-ApplicationsParallel' | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'InstallationOrchestrator Security' {
    Context 'State Data Validation' {
        It 'Should reject malicious SessionId' {
            $maliciousData = @{
                SessionId = '; rm -rf /'
                ProfileName = 'Test'
            }
            $result = Test-ValidStateData -StateData $maliciousData
            $result | Should -BeFalse
        }

        It 'Should validate GUID format strictly' {
            $invalidData = @{
                SessionId = 'invalid-guid-format'
                ProfileName = 'Test'
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }
    }

    Context 'App Name Sanitization' {
        It 'Should handle special characters in app names' {
            $app = [PSCustomObject]@{
                Name = 'Test App (x64) [Special]'
                Sources = @{}
            }
            { Get-ApplicationSources -Application $app } | Should -Not -Throw
        }

        It 'Should handle unicode in app names' {
            $app = [PSCustomObject]@{
                Name = 'Application Fran' + [char]231 + 'aise'
                Sources = @{}
            }
            { Get-ApplicationSources -Application $app } | Should -Not -Throw
        }
    }
}

Describe 'InstallationOrchestrator Error Handling' {
    Context 'Graceful Failure' {
        It 'Should handle null Sources gracefully in Get-ApplicationSources' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
            }
            { Get-ApplicationSources -Application $app } | Should -Not -Throw
        }

        It 'Should return null for app with null Sources' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
            }
            $result = Get-ApplicationSources -Application $app
            $result | Should -BeNullOrEmpty
        }

        It 'Should handle app with empty EnvironmentRestrictions' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = @{ Winget = 'Test.App' }
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Restricted | Should -BeFalse
        }
    }
}

Describe 'InstallationOrchestrator Performance' {
    Context 'State Operations' {
        It 'Should initialize session quickly' {
            $apps = @([PSCustomObject]@{ Name = 'App1'; Sources = $null })
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Initialize-DeploymentSession -ProfileName 'PerfTest' -Applications $apps
            $stopwatch.Stop()
            # Should complete within 1000ms
            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 1000
        }
    }
}

Describe 'Deployment Progress Tracking' {
    BeforeEach {
        Clear-DeploymentState -ErrorAction SilentlyContinue
    }

    Context 'Update-DeploymentProgress' {
        It 'Should track successful app installations' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Sources = @{ Winget = 'Test.App1' } },
                [PSCustomObject]@{ Name = 'App2'; Sources = @{ Winget = 'Test.App2' } }
            )
            Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps

            Update-DeploymentProgress -AppName 'App1' -Success $true

            $state = Get-DeploymentState
            $state.CompletedApps | Should -Contain 'App1'
            $state.PendingApps | Should -Not -Contain 'App1'
        }

        It 'Should track failed app installations' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Sources = @{ Winget = 'Test.App1' } }
            )
            Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps

            Update-DeploymentProgress -AppName 'App1' -Success $false

            $state = Get-DeploymentState
            $state.FailedApps | Should -Contain 'App1'
            $state.PendingApps | Should -Not -Contain 'App1'
        }

        It 'Should update LastUpdated timestamp' {
            $apps = @([PSCustomObject]@{ Name = 'App1'; Sources = $null })
            Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps

            $beforeUpdate = Get-Date
            Start-Sleep -Milliseconds 100
            Update-DeploymentProgress -AppName 'App1' -Success $true
            $state = Get-DeploymentState

            if ($state.LastUpdated) {
                [datetime]$state.LastUpdated | Should -BeGreaterThan $beforeUpdate
            }
        }
    }

    Context 'Resume-Deployment' {
        It 'Should return null when no incomplete deployment exists' {
            Clear-DeploymentState
            $result = Resume-Deployment
            $result | Should -BeNullOrEmpty
        }

        It 'Should return pending apps for incomplete deployment' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Sources = $null },
                [PSCustomObject]@{ Name = 'App2'; Sources = $null },
                [PSCustomObject]@{ Name = 'App3'; Sources = $null }
            )
            Initialize-DeploymentSession -ProfileName 'TestProfile' -Applications $apps
            Update-DeploymentProgress -AppName 'App1' -Success $true

            $pending = Resume-Deployment
            $pending | Should -Not -BeNullOrEmpty
            $pending | Should -Contain 'App2'
            $pending | Should -Contain 'App3'
            $pending | Should -Not -Contain 'App1'
        }
    }
}

Describe 'Rollback Operations' {
    BeforeEach {
        Clear-RollbackState -ErrorAction SilentlyContinue
    }

    Context 'Invoke-Rollback' {
        It 'Should return success result when no apps to rollback' {
            Initialize-RollbackSession
            $result = Invoke-Rollback -Force
            $result | Should -Not -BeNullOrEmpty
            $result.Success | Should -BeTrue
            $result.RolledBack.Count | Should -Be 0
        }

        It 'Should have correct result structure' {
            Initialize-RollbackSession
            $result = Invoke-Rollback -Force
            $result.Keys | Should -Contain 'Success'
            $result.Keys | Should -Contain 'RolledBack'
            $result.Keys | Should -Contain 'Failed'
        }
    }

    Context 'Save-RollbackState' {
        It 'Should not throw on save' {
            Initialize-RollbackSession
            { Save-RollbackState } | Should -Not -Throw
        }
    }
}

Describe 'State Data Validation Extended' {
    Context 'Profile Name Validation' {
        It 'Should reject profile name with forward slash' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test/Profile'
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject profile name with backslash' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test\Profile'
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject profile name longer than 100 characters' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'A' * 101
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should accept valid profile name' {
            $validData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Valid-Profile_Name123'
            }
            $result = Test-ValidStateData -StateData $validData
            $result | Should -BeTrue
        }
    }

    Context 'TotalApps Validation' {
        It 'Should reject negative TotalApps' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                TotalApps = -1
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject TotalApps over 1000' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                TotalApps = 1001
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should accept TotalApps of 0' {
            $validData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                TotalApps = 0
            }
            $result = Test-ValidStateData -StateData $validData
            $result | Should -BeTrue
        }
    }

    Context 'App Name Security Validation' {
        It 'Should reject app names with semicolon' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                CompletedApps = @('App1; malicious command')
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject app names with pipe' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                PendingApps = @('App1 | rm -rf')
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should reject app names with backtick' {
            $invalidData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                FailedApps = @('App`whoami`')
            }
            $result = Test-ValidStateData -StateData $invalidData
            $result | Should -BeFalse
        }

        It 'Should accept app names with parentheses and brackets' {
            $validData = @{
                SessionId = [Guid]::NewGuid().ToString()
                ProfileName = 'Test'
                CompletedApps = @('App (x64) [v2.0]')
            }
            $result = Test-ValidStateData -StateData $validData
            $result | Should -BeTrue
        }
    }
}

Describe 'Environment Restriction Extended' {
    Context 'Restriction Checking' {
        It 'Should return Environment property' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Keys | Should -Contain 'Environment'
        }

        It 'Should return Message property' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = @()
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Keys | Should -Contain 'Message'
        }

        It 'Should handle null EnvironmentRestrictions' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                EnvironmentRestrictions = $null
            }
            $result = Test-EnvironmentRestriction -Application $app
            $result.Restricted | Should -BeFalse
        }
    }
}

Describe 'Module Export Completeness' {
    Context 'InstallationEngine exports all expected functions' {
        It 'Should export <FunctionName> function via InstallationEngine' -TestCases @(
            # Orchestration functions
            @{ FunctionName = 'Invoke-Rollback' }
            @{ FunctionName = 'Test-IncompleteDeployment' }
            @{ FunctionName = 'Resume-Deployment' }
            @{ FunctionName = 'Test-EnvironmentRestriction' }
            @{ FunctionName = 'Invoke-InstallationMethodSequence' }
            @{ FunctionName = 'Get-ApplicationSources' }
            @{ FunctionName = 'Invoke-ApplicationUpgrade' }
            @{ FunctionName = 'Install-Application' }
            @{ FunctionName = 'Install-ApplicationsParallel' }
            # State management functions (from StateManager)
            @{ FunctionName = 'Initialize-RollbackSession' }
            @{ FunctionName = 'Save-RollbackState' }
            @{ FunctionName = 'Add-RollbackEntry' }
            @{ FunctionName = 'Clear-RollbackState' }
            @{ FunctionName = 'Get-RollbackState' }
            @{ FunctionName = 'Initialize-DeploymentSession' }
            @{ FunctionName = 'Save-DeploymentState' }
            @{ FunctionName = 'Update-DeploymentProgress' }
            @{ FunctionName = 'Test-ValidStateData' }
            @{ FunctionName = 'Get-DeploymentState' }
            @{ FunctionName = 'Clear-DeploymentState' }
        ) {
            param($FunctionName)
            Get-Command -Name $FunctionName -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Parallel detection via the hardened ParallelDetection module (A3.4 swap)' {
        It 'Enforces I4/I5/I6 end-to-end in a parallel runspace through Import-Module' {
            # Mirrors the orchestrator runspace after the swap: it imports ParallelDetection
            # by path and calls Test-AppInstalledParallel (the former here-string copy is gone).
            $pdPath = (Resolve-Path (Join-Path $PSScriptRoot '..\Modules\ParallelDetection.psm1')).Path
            $out = @(1) | ForEach-Object -Parallel {
                Import-Module $using:pdPath -Force
                [PSCustomObject]@{
                    # I5: a sensitive hive outside the allowlist is rejected
                    RegistryBlocked = Test-AppInstalledParallel -App ([PSCustomObject]@{ Name = 'Z'; Detection = [PSCustomObject]@{ Method = 'Registry'; Path = 'HKLM:\SYSTEM\Foo' } })
                    # I4: dangerous arguments on an allowlisted exe are blocked before execution
                    CommandInjection = Test-AppInstalledParallel -App ([PSCustomObject]@{ Name = 'I'; Detection = [PSCustomObject]@{ Method = 'Command'; Command = 'git ; calc' } })
                    # The allowlist JSON resolves transitively: an allowlisted, installed CLI is detected
                    CommandAllowlistedInstalled = Test-AppInstalledParallel -App ([PSCustomObject]@{ Name = 'D'; Detection = [PSCustomObject]@{ Method = 'Command'; Command = 'dotnet --version' } })
                }
            }
            $out.RegistryBlocked | Should -BeFalse
            $out.CommandInjection | Should -BeFalse
            $out.CommandAllowlistedInstalled | Should -BeTrue
        }
    }
}
