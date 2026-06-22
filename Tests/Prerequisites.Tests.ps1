<#
.SYNOPSIS
    Pester tests for Prerequisites module

.DESCRIPTION
    Comprehensive unit tests for WinForge Prerequisites v2.5.0
    Tests prerequisite detection, environment refresh, and installation helpers

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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:PrereqPath = Join-Path $script:ModuleRoot 'Prerequisites.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import Localization (provides Get-LocalizedString / t alias)
    $script:LocalizationPath = Join-Path $PSScriptRoot '..\Core\Localization.psm1'
    if (Test-Path $script:LocalizationPath) {
        Import-Module $script:LocalizationPath -Force -ErrorAction Stop
    }

    if (Get-Command -Name Initialize-Localization -ErrorAction SilentlyContinue) {
        Initialize-Localization -Locale 'en'
    }

    Import-Module $script:PrereqPath -Force -ErrorAction Stop
}

Describe 'Prerequisites Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:PrereqPath -Force } | Should -Not -Throw
        }

        It 'Should export Install-Chocolatey function' {
            Get-Command Install-Chocolatey -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-PowerShell7 function' {
            Get-Command Install-PowerShell7 -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-DotNetRuntime function' {
            Get-Command Install-DotNetRuntime -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-VCRedist function' {
            Get-Command Install-VCRedist -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-JavaRuntime function' {
            Get-Command Install-JavaRuntime -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-Prerequisites function' {
            Get-Command Test-Prerequisites -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Update-EnvironmentPath function' {
            Get-Command Update-EnvironmentPath -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-EnvironmentRefresh function' {
            Get-Command Invoke-EnvironmentRefresh -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Update-EnvironmentPath' {
        It 'Should return a boolean' {
            $result = Update-EnvironmentPath
            $result | Should -BeOfType [bool]
        }

        It 'Should complete without throwing' {
            { Update-EnvironmentPath } | Should -Not -Throw
        }

        It 'Should update PATH environment variable' {
            Update-EnvironmentPath
            # PATH should still be set (may or may not change)
            $env:PATH | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Invoke-EnvironmentRefresh' {
        It 'Should complete without throwing' {
            { Invoke-EnvironmentRefresh } | Should -Not -Throw
        }

        It 'Should maintain valid PATH' {
            Invoke-EnvironmentRefresh
            $env:PATH | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-Prerequisites' {
        It 'Should return an ordered dictionary' {
            $result = Test-Prerequisites
            $result | Should -BeOfType [System.Collections.Specialized.OrderedDictionary]
        }

        It 'Should have Chocolatey key' {
            $result = Test-Prerequisites
            $result.Contains('Chocolatey') | Should -BeTrue
        }

        It 'Should have PowerShell7 key' {
            $result = Test-Prerequisites
            $result.Contains('PowerShell7') | Should -BeTrue
        }

        It 'Should have Winget key' {
            $result = Test-Prerequisites
            $result.Contains('Winget') | Should -BeTrue
        }

        It 'Should have DotNet key' {
            $result = Test-Prerequisites
            $result.Contains('DotNet') | Should -BeTrue
        }

        It 'Should have DotNetFramework key' {
            $result = Test-Prerequisites
            $result.Contains('DotNetFramework') | Should -BeTrue
        }

        It 'Should have Java key' {
            $result = Test-Prerequisites
            $result.Contains('Java') | Should -BeTrue
        }

        It 'Should have VCRedist key' {
            $result = Test-Prerequisites
            $result.Contains('VCRedist') | Should -BeTrue
        }

        It 'Each prerequisite should have Installed property' {
            $result = Test-Prerequisites
            foreach ($key in $result.Keys) {
                $result[$key].Contains('Installed') | Should -BeTrue -Because "$key should have Installed property"
            }
        }

        It 'Each prerequisite should have Version property' {
            $result = Test-Prerequisites
            foreach ($key in $result.Keys) {
                $result[$key].Contains('Version') | Should -BeTrue -Because "$key should have Version property"
            }
        }

        It 'Installed properties should be boolean' {
            $result = Test-Prerequisites
            foreach ($key in $result.Keys) {
                $result[$key].Installed | Should -BeOfType [bool] -Because "$key.Installed should be boolean"
            }
        }

        It 'Should complete within reasonable time' {
            $duration = Measure-Command { Test-Prerequisites }
            $duration.TotalSeconds | Should -BeLessThan 30
        }
    }

    Context 'Install-Chocolatey' {
        It 'Should have Force parameter' {
            $cmd = Get-Command Install-Chocolatey
            $cmd.Parameters.ContainsKey('Force') | Should -BeTrue
        }

        It 'Should return boolean or not throw when already installed' {
            # If Chocolatey is installed, should return true without action
            # If not installed, would attempt installation (requires admin)
            $chocoInstalled = $null -ne (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)
            if ($chocoInstalled) {
                $result = Install-Chocolatey
                $result | Should -BeTrue
            }
        }

        It 'Should fail closed before running Chocolatey bootstrap when install script signature is invalid' {
            $previousChocolateyInstall = $env:ChocolateyInstall
            $env:ChocolateyInstall = Join-Path -Path $TestDrive -ChildPath 'ChocolateyInstall'
            $tempRoot = Join-Path -Path $TestDrive -ChildPath 'ChocolateyTemp'
            $script:ChocolateyBootstrapMarker = Join-Path -Path $TestDrive -ChildPath 'chocolatey-bootstrap-ran.txt'

            try {
                New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null

                Mock Write-Status { } -ModuleName Prerequisites
                Mock Get-LocalizedString { return $Key } -ModuleName Prerequisites
                Mock Set-ExecutionPolicy { } -ModuleName Prerequisites
                Mock Test-CommandAvailable { return $false } -ModuleName Prerequisites -ParameterFilter { $Name -eq 'choco' }
                Mock Get-ShellFolder { return $tempRoot } -ModuleName Prerequisites -ParameterFilter { $FolderType -eq 'Temp' }
                Mock Get-DownloadSources {
                    return [pscustomobject]@{
                        prerequisites = [pscustomobject]@{
                            chocolatey = [pscustomobject]@{
                                downloadUrl       = 'https://community.chocolatey.org/api/v2/package/chocolatey'
                                expectedPublisher = 'Chocolatey Software'
                            }
                        }
                    }
                } -ModuleName Prerequisites
                Mock Invoke-WebRequest {
                    $packageRoot = Join-Path -Path $TestDrive -ChildPath "ChocoPackage_$([guid]::NewGuid().ToString('N'))"
                    $toolsRoot = Join-Path -Path $packageRoot -ChildPath 'tools'
                    New-Item -Path $toolsRoot -ItemType Directory -Force | Out-Null

                    $markerPath = $script:ChocolateyBootstrapMarker.Replace("'", "''")
                    Set-Content -Path (Join-Path -Path $toolsRoot -ChildPath 'chocolateyInstall.ps1') `
                        -Value "Set-Content -LiteralPath '$markerPath' -Value 'executed' -NoNewline" `
                        -NoNewline `
                        -Encoding UTF8

                    $zipPath = [System.IO.Path]::ChangeExtension($OutFile, '.zip')
                    Compress-Archive -Path (Join-Path -Path $packageRoot -ChildPath '*') -DestinationPath $zipPath -Force
                    Move-Item -Path $zipPath -Destination $OutFile -Force
                } -ModuleName Prerequisites
                Mock Test-InstallerSignature { return $false } -ModuleName Prerequisites -ParameterFilter {
                    $FilePath -like '*chocolateyInstall.ps1' -and $ExpectedPublisher -eq 'Chocolatey Software'
                }
                Mock Copy-Item { } -ModuleName Prerequisites
                Mock Invoke-EnvironmentRefresh { } -ModuleName Prerequisites

                $thrown = $null
                try {
                    Install-Chocolatey -Force
                } catch {
                    $thrown = $_.Exception
                }

                $thrown | Should -Not -BeNullOrEmpty
                $thrown.GetType().Name | Should -Be 'InstallationException'
                $thrown.Message | Should -Match 'Chocolatey installation failed'
                Test-Path -Path $script:ChocolateyBootstrapMarker | Should -BeFalse
                Should -Invoke -CommandName Test-InstallerSignature -ModuleName Prerequisites -Times 1
                Should -Invoke -CommandName Copy-Item -ModuleName Prerequisites -Times 0
                Should -Invoke -CommandName Invoke-EnvironmentRefresh -ModuleName Prerequisites -Times 0
            } finally {
                if ($null -eq $previousChocolateyInstall) {
                    Remove-Item -Path Env:\ChocolateyInstall -ErrorAction SilentlyContinue
                } else {
                    $env:ChocolateyInstall = $previousChocolateyInstall
                }
            }
        }
    }

    Context 'Install-PowerShell7' {
        It 'Should have Force parameter' {
            $cmd = Get-Command Install-PowerShell7
            $cmd.Parameters.ContainsKey('Force') | Should -BeTrue
        }

        It 'Should return true if PS7+ already running' {
            if ($PSVersionTable.PSVersion.Major -ge 7) {
                $result = Install-PowerShell7
                $result | Should -BeTrue
            }
        }

        It 'Should not invoke msiexec when direct download checksum mismatches' {
            $tempRoot = Join-Path -Path $TestDrive -ChildPath 'PrerequisitesChecksum'
            New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null

            Mock Write-Status { } -ModuleName Prerequisites
            Mock Get-LocalizedString { return $Key } -ModuleName Prerequisites
            Mock Test-CommandAvailable { return $false } -ModuleName Prerequisites
            Mock Get-ShellFolder { return $tempRoot } -ModuleName Prerequisites -ParameterFilter { $FolderType -eq 'Temp' }
            Mock Get-DownloadSources {
                return [pscustomobject]@{
                    prerequisites = [pscustomobject]@{
                        powershell7 = [pscustomobject]@{
                            downloadUrl = 'https://example.test/PowerShell-7.4.0-win-x64.msi'
                            sha256      = '0' * 64
                        }
                    }
                }
            } -ModuleName Prerequisites
            Mock Invoke-WebRequest {
                [System.IO.File]::WriteAllText($OutFile, 'payload-with-wrong-hash')
            } -ModuleName Prerequisites
            Mock Invoke-ExternalProcess { return $true } -ModuleName Prerequisites

            $result = Install-PowerShell7 -Force

            $result | Should -BeFalse
            Should -Invoke -CommandName Invoke-ExternalProcess -ModuleName Prerequisites -Times 0 -ParameterFilter {
                $FilePath -eq 'msiexec.exe'
            }
            Test-Path -Path (Join-Path -Path $tempRoot -ChildPath 'PowerShell-7.4.0-win-x64.msi') | Should -BeFalse
        }
    }

    Context 'Install-DotNetRuntime' {
        It 'Should have Force parameter' {
            $cmd = Get-Command Install-DotNetRuntime
            $cmd.Parameters.ContainsKey('Force') | Should -BeTrue
        }

        It 'Should have CmdletBinding' {
            $cmd = Get-Command Install-DotNetRuntime
            $cmd.CmdletBinding | Should -BeTrue
        }
    }

    Context 'Install-VCRedist' {
        It 'Should have Force parameter' {
            $cmd = Get-Command Install-VCRedist
            $cmd.Parameters.ContainsKey('Force') | Should -BeTrue
        }
    }

    Context 'Install-JavaRuntime' {
        It 'Should have Force parameter' {
            $cmd = Get-Command Install-JavaRuntime
            $cmd.Parameters.ContainsKey('Force') | Should -BeTrue
        }
    }
}

Describe 'Prerequisites Integration Tests' {
    Context 'Detection Consistency' {
        It 'Test-Prerequisites should be deterministic' {
            $first = Test-Prerequisites
            $second = Test-Prerequisites

            foreach ($key in $first.Keys) {
                $first[$key].Installed | Should -Be $second[$key].Installed -Because "$key detection should be consistent"
            }
        }
    }

    Context 'Environment Refresh' {
        It 'Environment refresh should not break PATH' {
            Invoke-EnvironmentRefresh
            $env:PATH | Should -Not -BeNullOrEmpty
            # PATH should contain at least some of the original entries
            $env:PATH.Length | Should -BeGreaterThan 10
        }

        It 'Multiple refreshes should be safe' {
            {
                Invoke-EnvironmentRefresh
                Invoke-EnvironmentRefresh
                Invoke-EnvironmentRefresh
            } | Should -Not -Throw
        }
    }

    Context 'PowerShell Version Detection' {
        It 'Should correctly detect PowerShell version' {
            $result = Test-Prerequisites
            $currentMajor = $PSVersionTable.PSVersion.Major

            if ($currentMajor -ge 7) {
                $result.PowerShell7.Installed | Should -BeTrue
            }
        }

        It 'Should report PowerShell version string' {
            $result = Test-Prerequisites
            $result.PowerShell7.Version | Should -Not -BeNullOrEmpty
            $result.PowerShell7.Version | Should -Match '\d+\.\d+'
        }
    }
}
