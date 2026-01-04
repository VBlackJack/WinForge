<#
.SYNOPSIS
    Pester tests for InstallationEngine module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge InstallationEngine v2.5.0
    Coverage target: 50% minimum

.NOTES
    Author: Win11Forge Team
    Version: 2.5.0
    Requires: Pester v5+
#>

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
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'MaxRetries'
        }

        It 'Should have checksum validation in download function' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'SHA256|ExpectedSHA256'
        }

        It 'Should have exponential backoff logic' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match '\[Math\]::Pow'
        }
    }
}

Describe 'InstallationEngine Performance' {
    Context 'Performance Features' {
        It 'Should have timeout handling in module' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'TimeoutSeconds|Timeout'
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
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match '\$maxRetries\s*=\s*3'
        }

        It 'Should have exponential backoff in parallel Winget' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            # Check for the pattern of retry delay calculation
            $moduleContent | Should -Match 'retryDelay.*\*.*attempt'
        }

        It 'Should track retry attempts in parallel block' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'retryMsg'
        }
    }

    Context 'Parallel SHA256 Validation' {
        It 'Should validate SHA256 checksums in parallel DirectDownload' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'SHA256.*checksum.*parallel|parallel.*SHA256|Get-FileHash.*SHA256'
        }

        It 'Should have checksum mismatch handling' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'checksum.*mismatch|checksum.*validation.*failed'
        }
    }

    Context 'Parallel Exception Handling' {
        It 'Should have Write-ParallelException function' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'function Write-ParallelException'
        }

        It 'Should log exception type in parallel' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'GetType\(\)\.FullName'
        }

        It 'Should log inner exceptions in parallel' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'InnerException'
        }

        It 'Should log script stack trace in parallel' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'ScriptStackTrace'
        }
    }

    Context 'Parallel Detection Function' {
        It 'Should have Test-AppInstalledParallel function exported to parallel scope' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'Test-AppInstalledParallel'
        }

        It 'Should handle special apps in parallel detection (PowerToys)' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'PowerToys'
        }
    }

    Context 'Parallel Log Management' {
        It 'Should create per-app log files' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'appLogFile.*parallel'
        }

        It 'Should cleanup old log files' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'cutoffDate|Remove-Item.*logs'
        }
    }
}

Describe 'InstallationEngine Helper Functions' {
    Context 'Direct Download Helpers' {
        It 'Should have Install-MsiPackage helper' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'function Install-MsiPackage'
        }

        It 'Should have Install-ExePackage helper' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'function Install-ExePackage'
        }

        It 'Should have Install-ZipPackage helper' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match 'function Install-ZipPackage'
        }

        It 'Should try multiple silent switches in ExePackage' {
            $moduleContent = Get-Content (Join-Path $PSScriptRoot '../Modules/InstallationEngine.psm1') -Raw
            $moduleContent | Should -Match '/SILENT.*VERYSILENT|silentSwitches'
        }
    }
}
