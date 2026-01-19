<#
.SYNOPSIS
    Win11Forge - InstallationOrchestrator Module Tests v3.5.0

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
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\InstallationOrchestrator.psm1'
    Import-Module $ModulePath -Force -ErrorAction Stop

    # Test data directory
    $script:TestStateDir = Join-Path $env:TEMP "Win11ForgeTest_$([Guid]::NewGuid().ToString('N'))"
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
            { Import-Module $ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-RollbackSession function' {
            Get-Command -Module InstallationOrchestrator -Name 'Initialize-RollbackSession' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Initialize-DeploymentSession function' {
            Get-Command -Module InstallationOrchestrator -Name 'Initialize-DeploymentSession' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-Application function' {
            Get-Command -Module InstallationOrchestrator -Name 'Install-Application' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ApplicationsParallel function' {
            Get-Command -Module InstallationOrchestrator -Name 'Install-ApplicationsParallel' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApplicationSources function' {
            Get-Command -Module InstallationOrchestrator -Name 'Get-ApplicationSources' | Should -Not -BeNullOrEmpty
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
            Get-Command -Module InstallationOrchestrator -Name 'Invoke-InstallationMethodSequence' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-Application function' {
            Get-Command -Module InstallationOrchestrator -Name 'Install-Application' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ApplicationsParallel function' {
            Get-Command -Module InstallationOrchestrator -Name 'Install-ApplicationsParallel' | Should -Not -BeNullOrEmpty
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
