<#
.SYNOPSIS
    Pester tests for EnvironmentDetection module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge EnvironmentDetection v2.5.0
    Tests environment detection, capabilities, and compatibility checks

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
    # Import modules under test
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:EnvDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first (provides Write-Status)
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import EnvironmentDetection
    Import-Module $script:EnvDetectionPath -Force -ErrorAction Stop
}

Describe 'EnvironmentDetection Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:EnvDetectionPath -Force } | Should -Not -Throw
        }

        It 'Should export Get-SystemEnvironmentType function' {
            Get-Command Get-SystemEnvironmentType -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-WindowsSandbox function' {
            Get-Command Test-WindowsSandbox -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-IsVirtualMachine function' {
            Get-Command Test-IsVirtualMachine -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-EnvironmentCapabilities function' {
            Get-Command Get-EnvironmentCapabilities -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ApplicationCompatibleWithEnvironment function' {
            Get-Command Test-ApplicationCompatibleWithEnvironment -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-EnvironmentReport function' {
            Get-Command Get-EnvironmentReport -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-SystemEnvironmentType' {
        It 'Should return a valid environment type' {
            $envType = Get-SystemEnvironmentType

            $envType | Should -Not -BeNullOrEmpty
            $validTypes = @('Physical', 'WindowsSandbox', 'VMware', 'HyperV', 'VirtualBox', 'Unknown')
            $envType.ToString() | Should -BeIn $validTypes
        }

        It 'Should be deterministic (same result on multiple calls)' {
            $first = Get-SystemEnvironmentType
            $second = Get-SystemEnvironmentType

            $first | Should -Be $second
        }

        It 'Should complete within reasonable time' {
            $duration = Measure-Command { Get-SystemEnvironmentType }

            $duration.TotalSeconds | Should -BeLessThan 5
        }
    }

    Context 'Test-WindowsSandbox' {
        It 'Should return a boolean value' {
            $result = Test-WindowsSandbox

            $result | Should -BeOfType [bool]
        }

        It 'Should be consistent with environment type' {
            $isSandbox = Test-WindowsSandbox
            $envType = Get-SystemEnvironmentType

            if ($isSandbox) {
                $envType | Should -Be 'WindowsSandbox'
            }
        }

        It 'Should complete without errors' {
            { Test-WindowsSandbox } | Should -Not -Throw
        }
    }

    Context 'Test-IsVirtualMachine' {
        It 'Should return a boolean value' {
            $result = Test-IsVirtualMachine

            $result | Should -BeOfType [bool]
        }

        It 'Should be consistent with environment type' {
            $isVM = Test-IsVirtualMachine
            $envType = Get-SystemEnvironmentType

            if ($envType -eq 'Physical') {
                $isVM | Should -BeFalse
            } else {
                $isVM | Should -BeTrue
            }
        }

        It 'Should complete without errors' {
            { Test-IsVirtualMachine } | Should -Not -Throw
        }
    }

    Context 'Get-EnvironmentCapabilities' {
        It 'Should return a hashtable' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities | Should -BeOfType [hashtable]
        }

        It 'Should have EnvironmentType property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('EnvironmentType') | Should -BeTrue
            $capabilities.EnvironmentType | Should -Not -BeNullOrEmpty
        }

        It 'Should have CanInstallDrivers property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallDrivers') | Should -BeTrue
            $capabilities.CanInstallDrivers | Should -BeOfType [bool]
        }

        It 'Should have CanInstallServices property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallServices') | Should -BeTrue
            $capabilities.CanInstallServices | Should -BeOfType [bool]
        }

        It 'Should have CanModifyRegistry property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanModifyRegistry') | Should -BeTrue
            $capabilities.CanModifyRegistry | Should -BeOfType [bool]
        }

        It 'Should have CanInstallHyperV property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallHyperV') | Should -BeTrue
            $capabilities.CanInstallHyperV | Should -BeOfType [bool]
        }

        It 'Should have CanInstallWSL property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallWSL') | Should -BeTrue
            $capabilities.CanInstallWSL | Should -BeOfType [bool]
        }

        It 'Should have CanInstallVirtualization property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallVirtualization') | Should -BeTrue
            $capabilities.CanInstallVirtualization | Should -BeOfType [bool]
        }

        It 'Should have CanInstallHardwareSpecific property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('CanInstallHardwareSpecific') | Should -BeTrue
            $capabilities.CanInstallHardwareSpecific | Should -BeOfType [bool]
        }

        It 'Should have IsPersistent property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('IsPersistent') | Should -BeTrue
            $capabilities.IsPersistent | Should -BeOfType [bool]
        }

        It 'Should have RecommendedPackageSource property' {
            $capabilities = Get-EnvironmentCapabilities

            $capabilities.ContainsKey('RecommendedPackageSource') | Should -BeTrue
            $capabilities.RecommendedPackageSource | Should -Not -BeNullOrEmpty
        }

        It 'Physical environment should have full capabilities' {
            $capabilities = Get-EnvironmentCapabilities

            if ($capabilities.EnvironmentType -eq 'Physical') {
                $capabilities.CanInstallDrivers | Should -BeTrue
                $capabilities.CanInstallServices | Should -BeTrue
                $capabilities.CanModifyRegistry | Should -BeTrue
                $capabilities.CanInstallHyperV | Should -BeTrue
                $capabilities.CanInstallWSL | Should -BeTrue
                $capabilities.CanInstallVirtualization | Should -BeTrue
                $capabilities.CanInstallHardwareSpecific | Should -BeTrue
                $capabilities.IsPersistent | Should -BeTrue
            }
        }
    }

    Context 'Test-ApplicationCompatibleWithEnvironment' {
        It 'Should return a hashtable with Compatible property' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'TestApp'

            $result | Should -BeOfType [hashtable]
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should return a hashtable with Reason property' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'TestApp'

            $result.ContainsKey('Reason') | Should -BeTrue
        }

        It 'Should return a hashtable with Recommendation property' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'TestApp'

            $result.ContainsKey('Recommendation') | Should -BeTrue
        }

        It 'Should allow regular applications by default' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'NotepadPlusPlus'

            $result.Compatible | Should -BeTrue
        }

        It 'Should allow regular applications with category' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'VLC' -Category 'Media'

            $result.Compatible | Should -BeTrue
        }

        It 'Should check VMware compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'VMware Workstation'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check VirtualBox compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'VirtualBox'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check Docker compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'Docker Desktop'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check Hyper-V compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'Hyper-V Manager'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check WSL compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'WSL 2'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check driver category compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'Printer Driver' -Category 'Drivers'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }

        It 'Should check 3D printing category compatibility' {
            $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName 'PrusaSlicer' -Category '3DPrinting'

            # Result depends on current environment
            $result.ContainsKey('Compatible') | Should -BeTrue
        }
    }

    Context 'Get-EnvironmentReport' {
        It 'Should return a hashtable' {
            $report = Get-EnvironmentReport

            $report | Should -BeOfType [hashtable]
        }

        It 'Should have EnvironmentType property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('EnvironmentType') | Should -BeTrue
            $report.EnvironmentType | Should -Not -BeNullOrEmpty
        }

        It 'Should have ComputerName property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('ComputerName') | Should -BeTrue
            $report.ComputerName | Should -Not -BeNullOrEmpty
        }

        It 'Should have UserName property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('UserName') | Should -BeTrue
            $report.UserName | Should -Not -BeNullOrEmpty
        }

        It 'Should have OSVersion property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('OSVersion') | Should -BeTrue
            $report.OSVersion | Should -Not -BeNullOrEmpty
        }

        It 'Should have OSBuild property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('OSBuild') | Should -BeTrue
            $report.OSBuild | Should -Not -BeNullOrEmpty
        }

        It 'Should have Manufacturer property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('Manufacturer') | Should -BeTrue
        }

        It 'Should have Model property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('Model') | Should -BeTrue
        }

        It 'Should have BIOSVersion property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('BIOSVersion') | Should -BeTrue
        }

        It 'Should have TotalMemoryGB property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('TotalMemoryGB') | Should -BeTrue
            $report.TotalMemoryGB | Should -BeGreaterOrEqual 0
        }

        It 'Should have Capabilities property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('Capabilities') | Should -BeTrue
            $report.Capabilities | Should -BeOfType [hashtable]
        }

        It 'Should have IsVirtual property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('IsVirtual') | Should -BeTrue
            $report.IsVirtual | Should -BeOfType [bool]
        }

        It 'Should have DetectionTimestamp property' {
            $report = Get-EnvironmentReport

            $report.ContainsKey('DetectionTimestamp') | Should -BeTrue
            $report.DetectionTimestamp | Should -Not -BeNullOrEmpty
        }

        It 'Should have consistent IsVirtual with environment type' {
            $report = Get-EnvironmentReport

            if ($report.EnvironmentType -eq 'Physical') {
                $report.IsVirtual | Should -BeFalse
            } else {
                $report.IsVirtual | Should -BeTrue
            }
        }

        It 'Should complete within reasonable time' {
            $duration = Measure-Command { Get-EnvironmentReport }

            $duration.TotalSeconds | Should -BeLessThan 10
        }
    }
}

Describe 'EnvironmentDetection Integration Tests' {
    Context 'Environment Consistency' {
        It 'All detection functions should agree on environment type' {
            $envType = Get-SystemEnvironmentType
            $capabilities = Get-EnvironmentCapabilities
            $report = Get-EnvironmentReport

            $capabilities.EnvironmentType | Should -Be $envType
            $report.EnvironmentType | Should -Be $envType
        }

        It 'All VM detection functions should agree' {
            $isVM = Test-IsVirtualMachine
            $report = Get-EnvironmentReport
            $envType = Get-SystemEnvironmentType

            $report.IsVirtual | Should -Be $isVM

            if ($envType -eq 'Physical') {
                $isVM | Should -BeFalse
            }
        }

        It 'Sandbox detection should be consistent with environment type' {
            $isSandbox = Test-WindowsSandbox
            $envType = Get-SystemEnvironmentType

            if ($isSandbox) {
                $envType | Should -Be 'WindowsSandbox'
            }

            if ($envType -eq 'WindowsSandbox') {
                $isSandbox | Should -BeTrue
            }
        }
    }

    Context 'Capabilities Logic' {
        It 'Non-persistent environments should have limited capabilities' {
            $capabilities = Get-EnvironmentCapabilities

            if (-not $capabilities.IsPersistent) {
                # Windows Sandbox is non-persistent
                $capabilities.EnvironmentType | Should -Be 'WindowsSandbox'
                $capabilities.CanInstallHyperV | Should -BeFalse
                $capabilities.CanInstallWSL | Should -BeFalse
                $capabilities.CanInstallVirtualization | Should -BeFalse
            }
        }

        It 'Virtual environments should have limited virtualization capabilities' {
            $capabilities = Get-EnvironmentCapabilities

            if ($capabilities.EnvironmentType -ne 'Physical') {
                # VMs typically cannot nest virtualization
                if ($capabilities.EnvironmentType -in @('VMware', 'HyperV', 'VirtualBox')) {
                    $capabilities.CanInstallVirtualization | Should -BeFalse
                    $capabilities.CanInstallHardwareSpecific | Should -BeFalse
                }
            }
        }
    }

    Context 'Application Compatibility Scenarios' {
        It 'Should handle multiple compatibility checks efficiently' {
            $apps = @('Chrome', 'Firefox', 'VSCode', 'Notepad++', 'VLC')

            $duration = Measure-Command {
                foreach ($app in $apps) {
                    $null = Test-ApplicationCompatibleWithEnvironment -ApplicationName $app
                }
            }

            # Allow more time for slower environments
            $duration.TotalSeconds | Should -BeLessThan 30
        }

        It 'Regular apps should be compatible in any environment' {
            $regularApps = @('Google Chrome', 'Mozilla Firefox', 'Visual Studio Code')

            foreach ($app in $regularApps) {
                $result = Test-ApplicationCompatibleWithEnvironment -ApplicationName $app
                $result.Compatible | Should -BeTrue -Because "$app should be compatible everywhere"
            }
        }
    }
}
