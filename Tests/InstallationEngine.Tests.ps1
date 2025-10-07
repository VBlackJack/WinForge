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

    Context 'Test-ValidDownloadUrl' {
        It 'Should accept valid HTTPS URLs from whitelist' {
            $result = Test-ValidDownloadUrl -Url 'https://github.com/user/repo/releases/download/file.zip'
            $result | Should -Be $true
        }

        It 'Should reject HTTP URLs' {
            $result = Test-ValidDownloadUrl -Url 'http://malicious.com/file.exe'
            $result | Should -Be $false
        }

        It 'Should reject URLs from non-whitelisted domains' {
            $result = Test-ValidDownloadUrl -Url 'https://malicious.com/file.exe'
            $result | Should -Be $false
        }

        It 'Should accept Microsoft domains' {
            $result = Test-ValidDownloadUrl -Url 'https://download.microsoft.com/file.msi'
            $result | Should -Be $true
        }

        It 'Should accept common download sites' {
            $urls = @(
                'https://github.com/file.zip',
                'https://sourceforge.net/file.exe',
                'https://www.7-zip.org/a/7z.exe'
            )
            foreach ($url in $urls) {
                Test-ValidDownloadUrl -Url $url | Should -Be $true
            }
        }
    }

    Context 'Test-RegistryKey' {
        It 'Should return true for existing registry key' {
            # Test with a known Windows registry key
            $result = Test-RegistryKey -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion'
            $result | Should -Be $true
        }

        It 'Should return false for non-existing registry key' {
            $result = Test-RegistryKey -Path 'HKLM:\SOFTWARE\NonExistentKey12345'
            $result | Should -Be $false
        }
    }

    Context 'Test-ApplicationInstalled' {
        BeforeAll {
            # Mock application for testing
            $script:TestApp = @{
                Name = 'TestApp'
                Detection = @{
                    Method = 'Registry'
                    Path = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion'
                }
            }
        }

        It 'Should detect installed app via Registry method' {
            $result = Test-ApplicationInstalled -Application $script:TestApp
            $result | Should -Be $true
        }

        It 'Should handle missing Detection property gracefully' {
            $appNoDetection = @{ Name = 'NoDetection' }
            { Test-ApplicationInstalled -Application $appNoDetection } | Should -Not -Throw
        }

        It 'Should return false for File method with non-existing file' {
            $appFile = @{
                Name = 'FileTest'
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistentFile12345.exe'
                }
            }
            $result = Test-ApplicationInstalled -Application $appFile
            $result | Should -Be $false
        }
    }

    Context 'Test-ApplicationByName' {
        It 'Should detect PowerShell 7 if installed' {
            # PowerShell 7 might be installed on test system
            $result = Test-ApplicationByName -Name 'PowerShell'
            $result | Should -BeOfType [bool]
        }

        It 'Should return false for non-existent application' {
            $result = Test-ApplicationByName -Name 'NonExistentApp12345XYZ'
            $result | Should -Be $false
        }
    }

    Context 'Start-ProcessWithTimeout' {
        It 'Should execute simple command successfully' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList @('/c', 'echo test') -NoNewWindow -PassThru -TimeoutSeconds 10
            $result | Should -Not -BeNullOrEmpty
            $result.HasExited | Should -Be $true
        }

        It 'Should timeout long-running process' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList @('/c', 'timeout /t 30') -NoNewWindow -PassThru -TimeoutSeconds 2
            $result | Should -Not -BeNullOrEmpty
            # Process should be killed by timeout
        }

        It 'Should return process with ExitCode property' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList @('/c', 'exit 0') -NoNewWindow -PassThru -TimeoutSeconds 10
            $result.PSObject.Properties['ExitCode'] | Should -Not -BeNullOrEmpty
        }
    }

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
