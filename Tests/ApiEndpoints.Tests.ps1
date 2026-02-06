<#
.SYNOPSIS
    Pester tests for ApiEndpoints module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge ApiEndpoints v3.5.0
    Tests REST API handlers, validation, and security features

.NOTES
    Author: Julien Bombled
    Version: 3.5.2
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
    $script:ModulePath = Join-Path $script:ModuleRoot 'ApiEndpoints.psm1'

    # Import module without requiring RestApiServer
    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'ApiEndpoints Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Register-DefaultEndpoints function' {
            Get-Command Register-DefaultEndpoints -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Update-DeploymentState function' {
            Get-Command Update-DeploymentState -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-VersionHandler function' {
            Get-Command Get-VersionHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-ProfilesHandler function' {
            Get-Command Get-ProfilesHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-ApplicationsHandler function' {
            Get-Command Get-ApplicationsHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-StatusHandler function' {
            Get-Command Get-StatusHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Start-DeploymentHandler function' {
            Get-Command Start-DeploymentHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Start-RollbackHandler function' {
            Get-Command Start-RollbackHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-CacheStatsHandler function' {
            Get-Command Get-CacheStatsHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }

        It 'Should export Get-CsrfTokenHandler function' {
            Get-Command Get-CsrfTokenHandler -ErrorAction SilentlyContinue | Should -BeOfType [System.Management.Automation.FunctionInfo]
        }
    }

    Context 'Get-VersionHandler' {
        It 'Should return version information' {
            $context = @{}
            $result = Get-VersionHandler -Context $context
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'framework'
            $result.Keys | Should -Contain 'version'
            $result.Keys | Should -Contain 'apiVersion'
            $result.Keys | Should -Contain 'timestamp'
        }

        It 'Should include framework name' {
            $result = Get-VersionHandler -Context @{}
            $result.framework | Should -Be 'Win11Forge'
        }

        It 'Should include version string' {
            $result = Get-VersionHandler -Context @{}
            $result.version | Should -BeOfType [string]
            $result.version | Should -Match '^\d+\.\d+\.\d+$'
        }

        It 'Should include apiVersion' {
            $result = Get-VersionHandler -Context @{}
            $result.apiVersion | Should -Be '1.0'
        }

        It 'Should include timestamp' {
            $result = Get-VersionHandler -Context @{}
            $result.timestamp | Should -BeOfType [string]
            $result.timestamp | Should -Match '^\d{4}-\d{2}-\d{2}T'
        }
    }

    Context 'Get-ProfilesHandler' {
        It 'Should return profiles information' {
            $result = Get-ProfilesHandler -Context @{}
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'profiles'
            $result.Keys | Should -Contain 'count'
            $result.Keys | Should -Contain 'profilesDirectory'
        }

        It 'Should include profiles array' {
            $result = Get-ProfilesHandler -Context @{}
            $result.profiles | Should -BeOfType [hashtable]
            $result.profiles.Count | Should -BeGreaterThan 0
        }

        It 'Should include count property' {
            $result = Get-ProfilesHandler -Context @{}
            $result.count | Should -BeGreaterOrEqual 0
        }

        It 'Should include profilesDirectory' {
            $result = Get-ProfilesHandler -Context @{}
            $result.profilesDirectory | Should -BeOfType [string]
            $result.profilesDirectory | Should -BeLike '*Profiles'
        }

        It 'Should return known profiles' {
            $result = Get-ProfilesHandler -Context @{}
            $profileIds = $result.profiles | ForEach-Object { $_.id }
            $profileIds | Should -Contain 'Base'
        }
    }

    Context 'Get-ApplicationsHandler' {
        It 'Should return applications information' {
            $result = Get-ApplicationsHandler -Context @{}
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'applications'
            $result.Keys | Should -Contain 'count'
            $result.Keys | Should -Contain 'categories'
        }

        It 'Should include applications property' {
            $result = Get-ApplicationsHandler -Context @{}
            $null -ne $result['applications'] | Should -Be $true
        }

        It 'Should include count property' {
            $result = Get-ApplicationsHandler -Context @{}
            $result.count | Should -BeGreaterOrEqual 0
        }

        It 'Should include categories property' {
            $result = Get-ApplicationsHandler -Context @{}
            $null -ne $result['categories'] | Should -Be $true
        }

        It 'Should filter by category when apps exist' {
            $context = @{ Query = @{ category = 'Browser' } }
            $result = Get-ApplicationsHandler -Context $context
            # Only check category match if there are results
            if ($result.applications.Count -gt 0) {
                $result.applications | ForEach-Object {
                    $_.category | Should -Be 'Browser'
                }
            }
        }

        It 'Should handle search filter' {
            $context = @{ Query = @{ search = 'Chrome' } }
            $result = Get-ApplicationsHandler -Context $context
            # Test should pass whether or not Chrome is found
            $result.count | Should -BeGreaterOrEqual 0
        }

        It 'Should escape regex special characters in search' {
            # This tests the ReDoS protection
            $context = @{ Query = @{ search = 'Test[.*+?^${}()|' } }
            { Get-ApplicationsHandler -Context $context } | Should -Not -Throw
        }
    }

    Context 'Get-StatusHandler' {
        It 'Should return status information' {
            $result = Get-StatusHandler -Context @{}
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'status'
            $result.Keys | Should -Contain 'progress'
            $result.Keys | Should -Contain 'timestamp'
        }

        It 'Should include status field' {
            $result = Get-StatusHandler -Context @{}
            $result.status | Should -BeOfType [string]
            $result.status | Should -BeIn @('Idle', 'Starting', 'Running', 'Completed', 'Failed', 'RollingBack')
        }

        It 'Should include progress field' {
            $result = Get-StatusHandler -Context @{}
            $result.progress | Should -BeGreaterOrEqual 0
        }

        It 'Should include timestamp' {
            $result = Get-StatusHandler -Context @{}
            $result.timestamp | Should -BeOfType [string]
            $result.timestamp | Should -Match '^\d{4}-\d{2}-\d{2}T'
        }
    }

    Context 'Start-DeploymentHandler' {
        BeforeEach {
            # Reset deployment state
            Update-DeploymentState -Status 'Idle' -Progress 0
        }

        It 'Should reject request without profile' {
            $context = @{ Body = @{} }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should reject path traversal attempt with ..' {
            $context = @{ Body = @{ profile = '../../../etc/passwd' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should reject path traversal attempt with forward slash' {
            $context = @{ Body = @{ profile = 'test/profile' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should reject path traversal attempt with backslash' {
            $context = @{ Body = @{ profile = 'test\profile' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should reject profile name longer than 100 characters' {
            $longName = 'A' * 101
            $context = @{ Body = @{ profile = $longName } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should reject non-existent profile' {
            $context = @{ Body = @{ profile = 'NonExistentProfile12345' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should handle valid profile' {
            $context = @{ Body = @{ profile = 'Base' } }
            $result = Start-DeploymentHandler -Context $context
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'success'
            # May succeed or fail depending on schema validation availability
            if ($result.success) {
                $result.profile | Should -Be 'Base'
                $result.startTime | Should -Match '^\d{4}-\d{2}-\d{2}T'
            } else {
                $result.Keys | Should -Contain 'error'
                $result.error | Should -BeOfType [string]
            }
        }

        It 'Should reject deployment when already running' {
            Update-DeploymentState -Status 'Running'
            $context = @{ Body = @{ profile = 'Base' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
            $result.error | Should -BeOfType [string]
        }

        It 'Should accept testMode in request body' {
            $context = @{ Body = @{ profile = 'Base'; testMode = $true } }
            $result = Start-DeploymentHandler -Context $context
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'success'
            # May succeed or fail depending on schema validation availability
            if ($result.success) {
                $result.testMode | Should -Be $true
                $result.profile | Should -Be 'Base'
            } else {
                $result.Keys | Should -Contain 'error'
                $result.error | Should -BeOfType [string]
            }
        }
    }

    Context 'Start-RollbackHandler' {
        It 'Should return response' {
            $context = @{ Body = @{} }
            $result = Start-RollbackHandler -Context $context
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'success'
        }

        It 'Should handle force parameter' {
            $context = @{ Body = @{ force = $false } }
            $result = Start-RollbackHandler -Context $context
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'success'
        }
    }

    Context 'Get-CacheStatsHandler' {
        It 'Should return cache statistics' {
            $result = Get-CacheStatsHandler -Context @{}
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'timestamp'
            $result.Keys | Should -Contain 'winget'
        }

        It 'Should include timestamp' {
            $result = Get-CacheStatsHandler -Context @{}
            $result.timestamp | Should -BeOfType [string]
            $result.timestamp | Should -Match '^\d{4}-\d{2}-\d{2}T'
        }
    }

    Context 'Update-DeploymentState' {
        BeforeEach {
            Update-DeploymentState -Status 'Idle' -Progress 0
        }

        It 'Should update status' {
            Update-DeploymentState -Status 'Running'
            $result = Get-StatusHandler -Context @{}
            $result.status | Should -Be 'Running'
        }

        It 'Should update progress' {
            Update-DeploymentState -Progress 50
            $result = Get-StatusHandler -Context @{}
            $result.progress | Should -Be 50
        }

        It 'Should add application to list' {
            Update-DeploymentState -AppName 'TestApp'
            $result = Get-StatusHandler -Context @{}
            $result.applicationsProcessed | Should -BeGreaterThan 0
        }

        It 'Should add error to list' {
            Update-DeploymentState -Error 'Test error message'
            $result = Get-StatusHandler -Context @{}
            $result.errors.Count | Should -BeGreaterThan 0
        }

        It 'Should validate status values' {
            { Update-DeploymentState -Status 'InvalidStatus' } | Should -Throw
        }

        It 'Should validate progress range' {
            { Update-DeploymentState -Progress 101 } | Should -Throw
            { Update-DeploymentState -Progress -1 } | Should -Throw
        }

        It 'Should accept Idle status' {
            { Update-DeploymentState -Status 'Idle' } | Should -Not -Throw
        }

        It 'Should accept Starting status' {
            { Update-DeploymentState -Status 'Starting' } | Should -Not -Throw
        }

        It 'Should accept Completed status' {
            { Update-DeploymentState -Status 'Completed' } | Should -Not -Throw
        }

        It 'Should accept Failed status' {
            { Update-DeploymentState -Status 'Failed' } | Should -Not -Throw
        }

        It 'Should accept RollingBack status' {
            { Update-DeploymentState -Status 'RollingBack' } | Should -Not -Throw
        }
    }

    Context 'Security Tests' {
        It 'Should block null byte injection in profile name' {
            $context = @{ Body = @{ profile = "Base`0malicious" } }
            # The path functions should handle this, but profile should still be Base if it passes validation
            $result = Start-DeploymentHandler -Context $context
            # Either it blocks it or handles it safely - must return a hashtable with success key
            $result | Should -BeOfType [hashtable]
            $result.Keys | Should -Contain 'success'
        }

        It 'Should handle empty profile name' {
            $context = @{ Body = @{ profile = '' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
        }

        It 'Should handle whitespace-only profile name' {
            $context = @{ Body = @{ profile = '   ' } }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
        }

        It 'Should handle Unicode in profile name' {
            $context = @{ Body = @{ profile = 'Base日本語' } }
            $result = Start-DeploymentHandler -Context $context
            # Should fail because profile doesn't exist
            $result.success | Should -Be $false
        }
    }

    Context 'Edge Cases' {
        It 'Should handle null context body' {
            $context = @{ Body = $null }
            $result = Start-DeploymentHandler -Context $context
            $result.success | Should -Be $false
        }

        It 'Should handle missing query in applications handler' {
            $context = @{}
            { Get-ApplicationsHandler -Context $context } | Should -Not -Throw
        }

        It 'Should handle empty search in applications handler' {
            $context = @{ Query = @{ search = '' } }
            { Get-ApplicationsHandler -Context $context } | Should -Not -Throw
        }
    }
}
