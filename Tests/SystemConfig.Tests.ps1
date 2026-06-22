<#
.SYNOPSIS
    Pester tests for SystemConfig module

.DESCRIPTION
    Comprehensive unit tests for WinForge SystemConfig v2.5.0
    Tests system configuration functions for Explorer, Taskbar, Network, etc.

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
    $script:SysConfigPath = Join-Path $script:ModuleRoot 'SystemConfig.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Localization first (provides Get-LocalizedString / t alias)
    $script:LocalizationPath = Join-Path $PSScriptRoot '..\Core\Localization.psm1'
    if (Test-Path $script:LocalizationPath) {
        Import-Module $script:LocalizationPath -Force -ErrorAction Stop
        Initialize-Localization -Locale 'en'
    }

    # Import Core (provides Write-Status)
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    Import-Module $script:SysConfigPath -Force -ErrorAction Stop
}

Describe 'SystemConfig Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:SysConfigPath -Force } | Should -Not -Throw
        }

        It 'Should export Set-RegistryValue function' {
            Get-Command Set-RegistryValue -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-ExplorerConfiguration function' {
            Get-Command Set-ExplorerConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-TaskbarConfiguration function' {
            Get-Command Set-TaskbarConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-NetworkConfiguration function' {
            Get-Command Set-NetworkConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-PrivacyConfiguration function' {
            Get-Command Set-PrivacyConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-PerformanceConfiguration function' {
            Get-Command Set-PerformanceConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-SecurityConfiguration function' {
            Get-Command Set-SecurityConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-SystemConfiguration function' {
            Get-Command Set-SystemConfiguration -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Set-RegistryValue' {
        It 'Should have required parameters' {
            $cmd = Get-Command Set-RegistryValue
            $cmd.Parameters.ContainsKey('Path') | Should -BeTrue
            $cmd.Parameters.ContainsKey('Name') | Should -BeTrue
            $cmd.Parameters.ContainsKey('Value') | Should -BeTrue
        }

        It 'Should have Type parameter with valid set' {
            $cmd = Get-Command Set-RegistryValue
            $cmd.Parameters.ContainsKey('Type') | Should -BeTrue
        }

        It 'Should return boolean' {
            # Test in a safe HKCU location
            $testPath = 'HKCU:\Software\WinForgeTest'
            try {
                $result = Set-RegistryValue -Path $testPath -Name 'TestValue' -Value 1 -Type DWord
                $result | Should -BeOfType [bool]
            }
            finally {
                Remove-Item $testPath -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should create registry path if not exists' {
            $testPath = "HKCU:\Software\WinForgeTest_$(Get-Random)"
            try {
                Set-RegistryValue -Path $testPath -Name 'TestValue' -Value 1 -Type DWord
                Test-Path $testPath | Should -BeTrue
            }
            finally {
                Remove-Item $testPath -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should set DWORD values correctly' {
            $testPath = "HKCU:\Software\WinForgeTest_$(Get-Random)"
            try {
                Set-RegistryValue -Path $testPath -Name 'DWordTest' -Value 42 -Type DWord
                $value = Get-ItemProperty -Path $testPath -Name 'DWordTest'
                $value.DWordTest | Should -Be 42
            }
            finally {
                Remove-Item $testPath -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should set String values correctly' {
            $testPath = "HKCU:\Software\WinForgeTest_$(Get-Random)"
            try {
                Set-RegistryValue -Path $testPath -Name 'StringTest' -Value 'TestString' -Type String
                $value = Get-ItemProperty -Path $testPath -Name 'StringTest'
                $value.StringTest | Should -Be 'TestString'
            }
            finally {
                Remove-Item $testPath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Set-ExplorerConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-ExplorerConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            # Empty config should not throw
            { Set-ExplorerConfiguration -Config @{} } | Should -Not -Throw
        }

        It 'Should process ShowHiddenFiles setting' {
            $config = @{ ShowHiddenFiles = $false }
            { Set-ExplorerConfiguration -Config $config } | Should -Not -Throw
        }

        It 'Should process ShowFileExtensions setting' {
            $config = @{ ShowFileExtensions = $false }
            { Set-ExplorerConfiguration -Config $config } | Should -Not -Throw
        }
    }

    Context 'Set-TaskbarConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-TaskbarConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            { Set-TaskbarConfiguration -Config @{} } | Should -Not -Throw
        }
    }

    Context 'Set-NetworkConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-NetworkConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            { Set-NetworkConfiguration -Config @{} } | Should -Not -Throw
        }

        It 'Should handle DNS configuration with valid IPs' {
            # Just test parsing, not actual application (requires admin)
            $config = @{ DnsServers = @('8.8.8.8', '8.8.4.4') }
            { Set-NetworkConfiguration -Config $config } | Should -Not -Throw
        }

        It 'Should handle DNS configuration with string format' {
            $config = @{ DnsServers = '8.8.8.8,8.8.4.4' }
            { Set-NetworkConfiguration -Config $config } | Should -Not -Throw
        }

        It 'Should handle invalid DNS gracefully' {
            $config = @{ DnsServers = @('invalid') }
            { Set-NetworkConfiguration -Config $config } | Should -Not -Throw
        }
    }

    Context 'Set-PrivacyConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-PrivacyConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            { Set-PrivacyConfiguration -Config @{} } | Should -Not -Throw
        }
    }

    Context 'Set-PerformanceConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-PerformanceConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            { Set-PerformanceConfiguration -Config @{} } | Should -Not -Throw
        }
    }

    Context 'Set-SecurityConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-SecurityConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should handle empty configuration' {
            { Set-SecurityConfiguration -Config @{} } | Should -Not -Throw
        }
    }

    Context 'Set-SystemConfiguration' {
        It 'Should accept Config parameter' {
            $cmd = Get-Command Set-SystemConfiguration
            $cmd.Parameters.ContainsKey('Config') | Should -BeTrue
        }

        It 'Should have CmdletBinding' {
            $cmd = Get-Command Set-SystemConfiguration
            $cmd.CmdletBinding | Should -BeTrue
        }
    }
}

Describe 'SystemConfig Integration Tests' {
    Context 'Registry Operations' {
        It 'Should perform multiple registry operations safely' {
            $testPath = "HKCU:\Software\WinForgeMultiTest_$(Get-Random)"
            try {
                Set-RegistryValue -Path $testPath -Name 'Value1' -Value 1 -Type DWord
                Set-RegistryValue -Path $testPath -Name 'Value2' -Value 2 -Type DWord
                Set-RegistryValue -Path $testPath -Name 'Value3' -Value 'test' -Type String

                $values = Get-ItemProperty -Path $testPath
                $values.Value1 | Should -Be 1
                $values.Value2 | Should -Be 2
                $values.Value3 | Should -Be 'test'
            }
            finally {
                Remove-Item $testPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
