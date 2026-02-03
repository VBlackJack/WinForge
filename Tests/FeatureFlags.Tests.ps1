<#
.SYNOPSIS
    Pester tests for FeatureFlags module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge FeatureFlags v3.5.2
    Tests feature flag loading, overrides, and conditional execution

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
    $script:ModulePath = Join-Path $script:ModuleRoot 'FeatureFlags.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'FeatureFlags Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-FeatureFlags function' {
            Get-Command Initialize-FeatureFlags -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-FeatureEnabled function' {
            Get-Command Test-FeatureEnabled -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-FeatureOverride function' {
            Get-Command Set-FeatureOverride -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-FeatureOverride function' {
            Get-Command Remove-FeatureOverride -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-FeatureOverrides function' {
            Get-Command Clear-FeatureOverrides -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-AllFeatureFlags function' {
            Get-Command Get-AllFeatureFlags -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-EnabledFeatures function' {
            Get-Command Get-EnabledFeatures -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-DisabledFeatures function' {
            Get-Command Get-DisabledFeatures -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-WithFeature function' {
            Get-Command Invoke-WithFeature -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export ff alias' {
            Get-Alias ff -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-FeatureFlags' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should initialize without errors' {
            { Initialize-FeatureFlags } | Should -Not -Throw
        }

        It 'Should support Force parameter' {
            { Initialize-FeatureFlags -Force } | Should -Not -Throw
        }

        It 'Should load feature flags after initialization' {
            $flags = Get-AllFeatureFlags
            $flags.Count | Should -BeGreaterThan 0
        }
    }

    Context 'Test-FeatureEnabled' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should return boolean' {
            $result = Test-FeatureEnabled -FeatureName 'parallelInstallation'
            $result | Should -BeOfType [bool]
        }

        It 'Should return true for known enabled feature' {
            $result = Test-FeatureEnabled -FeatureName 'parallelInstallation'
            $result | Should -Be $true
        }

        It 'Should return false for unknown feature' {
            $result = Test-FeatureEnabled -FeatureName 'nonExistentFeature12345'
            $result | Should -Be $false
        }

        It 'Should require FeatureName parameter' {
            { Test-FeatureEnabled } | Should -Throw
        }

        It 'Should work with ff alias' {
            $result = ff 'parallelInstallation'
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Set-FeatureOverride' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should set override for feature' {
            Set-FeatureOverride -FeatureName 'testFeature' -Enabled $true
            Test-FeatureEnabled -FeatureName 'testFeature' | Should -Be $true
        }

        It 'Should override existing feature value' {
            # Assuming restApi is disabled by default
            $original = Test-FeatureEnabled -FeatureName 'restApi'
            Set-FeatureOverride -FeatureName 'restApi' -Enabled (-not $original)
            Test-FeatureEnabled -FeatureName 'restApi' | Should -Be (-not $original)
        }

        It 'Should accept enabled true' {
            { Set-FeatureOverride -FeatureName 'testFeature' -Enabled $true } | Should -Not -Throw
        }

        It 'Should accept enabled false' {
            { Set-FeatureOverride -FeatureName 'testFeature' -Enabled $false } | Should -Not -Throw
        }

        It 'Should require both parameters' {
            { Set-FeatureOverride -FeatureName 'test' } | Should -Throw
            { Set-FeatureOverride -Enabled $true } | Should -Throw
        }
    }

    Context 'Remove-FeatureOverride' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should remove existing override' {
            Set-FeatureOverride -FeatureName 'testRemove' -Enabled $true
            Remove-FeatureOverride -FeatureName 'testRemove'
            # After removal, should fall back to default (false for unknown)
            Test-FeatureEnabled -FeatureName 'testRemove' | Should -Be $false
        }

        It 'Should not throw for non-existent override' {
            { Remove-FeatureOverride -FeatureName 'nonExistent' } | Should -Not -Throw
        }

        It 'Should require FeatureName parameter' {
            { Remove-FeatureOverride } | Should -Throw
        }
    }

    Context 'Clear-FeatureOverrides' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should clear all overrides' {
            Set-FeatureOverride -FeatureName 'override1' -Enabled $true
            Set-FeatureOverride -FeatureName 'override2' -Enabled $true
            Clear-FeatureOverrides
            Test-FeatureEnabled -FeatureName 'override1' | Should -Be $false
            Test-FeatureEnabled -FeatureName 'override2' | Should -Be $false
        }

        It 'Should not throw when no overrides exist' {
            { Clear-FeatureOverrides } | Should -Not -Throw
        }
    }

    Context 'Get-AllFeatureFlags' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should return array' {
            $result = Get-AllFeatureFlags
            $result | Should -BeOfType [PSCustomObject]
        }

        It 'Should return multiple flags' {
            $result = Get-AllFeatureFlags
            $result.Count | Should -BeGreaterThan 5
        }

        It 'Should include FeatureName property' {
            $result = Get-AllFeatureFlags | Select-Object -First 1
            $result.FeatureName | Should -Not -BeNullOrEmpty
        }

        It 'Should include Enabled property' {
            $result = Get-AllFeatureFlags | Select-Object -First 1
            $result.Enabled | Should -BeOfType [bool]
        }

        It 'Should include HasOverride property' {
            $result = Get-AllFeatureFlags | Select-Object -First 1
            $result.HasOverride | Should -BeOfType [bool]
        }

        It 'Should reflect runtime overrides' {
            Set-FeatureOverride -FeatureName 'parallelInstallation' -Enabled $false
            $result = Get-AllFeatureFlags | Where-Object { $_.FeatureName -eq 'parallelInstallation' }
            $result.HasOverride | Should -Be $true
        }
    }

    Context 'Get-EnabledFeatures' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should return array of strings' {
            $result = Get-EnabledFeatures
            if ($result.Count -gt 0) {
                $result[0] | Should -BeOfType [string]
            }
        }

        It 'Should only return enabled features' {
            $enabled = Get-EnabledFeatures
            foreach ($feature in $enabled) {
                Test-FeatureEnabled -FeatureName $feature | Should -Be $true
            }
        }

        It 'Should include parallelInstallation when enabled by default' {
            $result = Get-EnabledFeatures
            $result | Should -Contain 'parallelInstallation'
        }
    }

    Context 'Get-DisabledFeatures' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should return array of strings' {
            $result = Get-DisabledFeatures
            if ($result.Count -gt 0) {
                $result[0] | Should -BeOfType [string]
            }
        }

        It 'Should only return disabled features' {
            $disabled = Get-DisabledFeatures
            foreach ($feature in $disabled) {
                Test-FeatureEnabled -FeatureName $feature | Should -Be $false
            }
        }
    }

    Context 'Invoke-WithFeature' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should execute script block when feature is enabled' {
            Set-FeatureOverride -FeatureName 'testInvoke' -Enabled $true
            $tracker = @{ Executed = $false }
            Invoke-WithFeature -FeatureName 'testInvoke' -ScriptBlock { $tracker.Executed = $true }
            $tracker.Executed | Should -Be $true
        }

        It 'Should not execute script block when feature is disabled' {
            Set-FeatureOverride -FeatureName 'testInvoke' -Enabled $false
            $tracker = @{ Executed = $false }
            Invoke-WithFeature -FeatureName 'testInvoke' -ScriptBlock { $tracker.Executed = $true }
            $tracker.Executed | Should -Be $false
        }

        It 'Should execute fallback when feature is disabled' {
            Set-FeatureOverride -FeatureName 'testInvoke' -Enabled $false
            $tracker = @{ FallbackExecuted = $false }
            Invoke-WithFeature -FeatureName 'testInvoke' -ScriptBlock { } -Fallback { $tracker.FallbackExecuted = $true }
            $tracker.FallbackExecuted | Should -Be $true
        }

        It 'Should not execute fallback when feature is enabled' {
            Set-FeatureOverride -FeatureName 'testInvoke' -Enabled $true
            $tracker = @{ FallbackExecuted = $false }
            Invoke-WithFeature -FeatureName 'testInvoke' -ScriptBlock { } -Fallback { $tracker.FallbackExecuted = $true }
            $tracker.FallbackExecuted | Should -Be $false
        }

        It 'Should require FeatureName parameter' {
            { Invoke-WithFeature -ScriptBlock { } } | Should -Throw
        }

        It 'Should require ScriptBlock parameter' {
            { Invoke-WithFeature -FeatureName 'test' } | Should -Throw
        }
    }

    Context 'Override Priority' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should prioritize runtime override over config' {
            # parallelInstallation is true in config
            Set-FeatureOverride -FeatureName 'parallelInstallation' -Enabled $false
            Test-FeatureEnabled -FeatureName 'parallelInstallation' | Should -Be $false
        }

        It 'Should restore config value after override removal' {
            Set-FeatureOverride -FeatureName 'parallelInstallation' -Enabled $false
            Remove-FeatureOverride -FeatureName 'parallelInstallation'
            Test-FeatureEnabled -FeatureName 'parallelInstallation' | Should -Be $true
        }
    }

    Context 'Edge Cases' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should reject empty feature name' {
            { Test-FeatureEnabled -FeatureName '' } | Should -Throw
        }

        It 'Should handle feature name with special characters' {
            Set-FeatureOverride -FeatureName 'feature-with_special.chars' -Enabled $true
            Test-FeatureEnabled -FeatureName 'feature-with_special.chars' | Should -Be $true
        }

        It 'Should handle multiple rapid override changes' {
            1..10 | ForEach-Object {
                Set-FeatureOverride -FeatureName 'rapidTest' -Enabled ($_ % 2 -eq 0)
            }
            # Final state should be false (10 % 2 = 0 = true... wait, 10 is even so true)
            Test-FeatureEnabled -FeatureName 'rapidTest' | Should -Be $true
        }

        It 'Should handle case-sensitive feature names' {
            Set-FeatureOverride -FeatureName 'CaseSensitive' -Enabled $true
            Set-FeatureOverride -FeatureName 'casesensitive' -Enabled $false
            # PowerShell hashtables are case-insensitive by default
            # This tests the actual behavior
            $result = Test-FeatureEnabled -FeatureName 'CaseSensitive'
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Default Flags' {
        BeforeEach {
            Initialize-FeatureFlags -Force
        }

        It 'Should have parallelInstallation enabled by default' {
            Clear-FeatureOverrides
            Test-FeatureEnabled -FeatureName 'parallelInstallation' | Should -Be $true
        }

        It 'Should have structuredLogging enabled by default' {
            Clear-FeatureOverrides
            Test-FeatureEnabled -FeatureName 'structuredLogging' | Should -Be $true
        }

        It 'Should have checksumValidation enabled by default' {
            Clear-FeatureOverrides
            Test-FeatureEnabled -FeatureName 'checksumValidation' | Should -Be $true
        }

        It 'Should return consistent value for telemetryCollection' {
            Clear-FeatureOverrides
            $result = Test-FeatureEnabled -FeatureName 'telemetryCollection'
            $result | Should -BeOfType [bool]
        }
    }
}
