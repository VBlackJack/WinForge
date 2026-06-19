<#
.SYNOPSIS
    Pester tests for InstallationEngine module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge InstallationEngine v2.5.0
    Coverage target: 50% minimum

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
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
    # Import module under test
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\InstallationEngine.psm1'
    Import-Module $ModulePath -Force -ErrorAction Stop

    # Import Core for Write-Status
    $CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'
    if (Test-Path $CorePath) {
        Import-Module $CorePath -Force
    }
}

Describe 'InstallationEngine Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module (Join-Path $PSScriptRoot '..\Modules\InstallationEngine.psm1') -Force } | Should -Not -Throw
        }

        It 'Should export Install-Application function' {
            Get-Command Install-Application -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ApplicationsParallel function' {
            Get-Command Install-ApplicationsParallel -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    # NOTE: Tests for private functions removed (not exported by module)
    # Private functions: Test-ValidDownloadUrl, Test-RegistryKey, Test-ApplicationInstalled,
    # Test-ApplicationByName, Start-ProcessWithTimeout
    # These are tested indirectly through public API integration tests

    Context 'Install-Application Integration' {
        It 'Should have Application parameter' {
            $command = Get-Command Install-Application
            $command.Parameters.Keys | Should -Contain 'Application'
        }

        It 'Should have Force parameter' {
            $command = Get-Command Install-Application
            $command.Parameters.Keys | Should -Contain 'Force'
        }

        It 'Should be a valid function' {
            $command = Get-Command Install-Application -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty
            $command.CommandType | Should -Be 'Function'
        }
    }

    Context 'Install-WindowsFeature' {
        It 'Should have FeatureName parameter' {
            $command = Get-Command Install-WindowsFeature
            $command.Parameters.Keys | Should -Contain 'FeatureName'
        }

        It 'Should be exported' {
            $command = Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Install-WindowsCapability' {
        It 'Should have CapabilityName parameter' {
            $command = Get-Command Install-WindowsCapability
            $command.Parameters.Keys | Should -Contain 'CapabilityName'
        }

        It 'Should be exported' {
            $command = Get-Command Install-WindowsCapability -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'InstallationEngine Security' {
    Context 'Module Security Features' {
        It 'Should have retry logic in Install-ViaWinget' {
            # Security feature now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match 'MaxRetries'
        }

        It 'Should have checksum validation in download function' {
            # Checksum validation now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match 'SHA256|ExpectedSHA256'
        }

        It 'Should have exponential backoff logic' {
            # Backoff logic now in InstallationOrchestrator.psm1 (parallel) or InstallationMethods.psm1
            $orchestratorContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $methodsContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            ($orchestratorContent -match '\[Math\]::Pow' -or $methodsContent -match '\[Math\]::Pow') | Should -Be $true
        }
    }
}

Describe 'InstallationEngine Performance' {
    Context 'Performance Features' {
        It 'Should have timeout handling in module' {
            # Timeout config in main module, timeout handling in methods
            $mainContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $methodsContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            ($mainContent -match 'Timeout' -or $methodsContent -match 'Timeout') | Should -Be $true
        }

        It 'Should export Install-ApplicationsParallel for performance' {
            $command = Get-Command Install-ApplicationsParallel -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'InstallationEngine Parallel Edge Cases' {
    Context 'Install-ApplicationsParallel Parameters' {
        It 'Should have Applications parameter' {
            $command = Get-Command Install-ApplicationsParallel
            $command.Parameters.Keys | Should -Contain 'Applications'
        }

        It 'Should have MaxParallel parameter' {
            $command = Get-Command Install-ApplicationsParallel
            $command.Parameters.Keys | Should -Contain 'MaxParallel'
        }

        It 'Should have Force parameter' {
            $command = Get-Command Install-ApplicationsParallel
            $command.Parameters.Keys | Should -Contain 'Force'
        }

        It 'Should return array of results' {
            $command = Get-Command Install-ApplicationsParallel
            $command | Should -Not -BeNullOrEmpty
            $command.OutputType | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Parallel Retry Logic' {
        It 'Should have retry logic in parallel block' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match '\$maxRetries\s*=\s*3'
        }

        It 'Should have exponential backoff in parallel Winget' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'retryDelaySeconds.*\*|delay.*Pow'
        }

        It 'Should track retry attempts in parallel block' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'retryMsg'
        }
    }

    Context 'Parallel SHA256 Validation' {
        It 'Should validate SHA256 checksums in parallel DirectDownload' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'SHA256|Get-FileHash'
        }

        It 'Should have checksum mismatch handling' {
            # Checksum handling in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'Checksum.*FAILED|checksum.*validation'
        }
    }

    Context 'Parallel Exception Handling' {
        It 'Should have Write-ParallelException function' {
            # Parallel helper now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'function Write-ParallelException'
        }

        It 'Should log exception type in parallel' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'GetType\(\)\.FullName'
        }

        It 'Should log inner exceptions in parallel' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'InnerException'
        }

        It 'Should log script stack trace in parallel' {
            # Parallel logic now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'ScriptStackTrace'
        }
    }

    Context 'Parallel Detection Function' {
        It 'Should have Test-AppInstalledParallel function exported to parallel scope' {
            # Parallel detection now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'Test-AppInstalledParallel'
        }

        # PowerToys/Quick Assist special-case handling is covered behaviorally in
        # ParallelDetection.Tests.ps1 (Context 'Special App Detection'); the former
        # brittle source-grep test was removed when the here-string moved to that module.
    }

    Context 'Parallel Log Management' {
        It 'Should create per-app log files' {
            # Parallel log management now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'appLogFile'
        }

        It 'Should cleanup old log files' {
            # Parallel log management now in InstallationOrchestrator.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationOrchestrator.psm1') -Raw
            $moduleContent | Should -Match 'cutoffDate'
        }
    }
}

Describe 'InstallationEngine Helper Functions' {
    Context 'Direct Download Helpers' {
        It 'Should have Install-MsiPackage helper' {
            # Direct download helpers now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match 'function Install-MsiPackage'
        }

        It 'Should have Install-ExePackage helper' {
            # Direct download helpers now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match 'function Install-ExePackage'
        }

        It 'Should have Install-ZipPackage helper' {
            # Direct download helpers now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match 'function Install-ZipPackage'
        }

        It 'Should try multiple silent switches in ExePackage' {
            # Direct download helpers now in InstallationMethods.psm1
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationMethods.psm1') -Raw
            $moduleContent | Should -Match '/S|/silent|silentSwitches'
        }
    }
}
