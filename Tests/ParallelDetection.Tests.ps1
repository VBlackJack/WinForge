<#
.SYNOPSIS
    Pester tests for ParallelDetection module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge ParallelDetection v3.5.0
    Tests parallel application detection methods

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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
    $script:ParallelDetectionPath = Join-Path $script:ModuleRoot 'ParallelDetection.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import ParallelDetection
    Import-Module $script:ParallelDetectionPath -Force -ErrorAction Stop
}

Describe 'ParallelDetection Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ParallelDetectionPath -Force } | Should -Not -Throw
        }

        It 'Should export Test-AppInstalledParallel function' {
            Get-Command Test-AppInstalledParallel -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-RegistryDetection function' {
            Get-Command Test-RegistryDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-FileDetection function' {
            Get-Command Test-FileDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-CommandDetection function' {
            Get-Command Test-CommandDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-WindowsFeatureDetection function' {
            Get-Command Test-WindowsFeatureDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-WindowsCapabilityDetection function' {
            Get-Command Test-WindowsCapabilityDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-StoreAppDetection function' {
            Get-Command Test-StoreAppDetection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-AppInstalledParallel - Basic Functionality' {
        It 'Should require App parameter' {
            { Test-AppInstalledParallel -App $null } | Should -Throw
        }

        It 'Should accept App object with Detection property' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = 'C:\NonExistent\Path\test.exe'
            }
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Detection = $detection
            }
            $result = Test-AppInstalledParallel -App $app
            $result | Should -BeOfType [bool]
        }

        It 'Should return false for non-existent file detection' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = 'C:\NonExistent\Path\app12345.exe'
            }
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Detection = $detection
            }
            $result = Test-AppInstalledParallel -App $app
            $result | Should -BeFalse
        }

        It 'Should return boolean for app without Detection config' {
            $app = [PSCustomObject]@{
                Name = 'NonExistentApp12345XYZ'
                Detection = $null
            }
            $result = Test-AppInstalledParallel -App $app
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Test-RegistryDetection' {
        It 'Should return false for non-existent registry key' {
            $detection = [PSCustomObject]@{
                Method = 'Registry'
                Path = 'HKLM:\SOFTWARE\NonExistentKey12345'
            }
            $result = Test-RegistryDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should return true for existing registry key' {
            $detection = [PSCustomObject]@{
                Method = 'Registry'
                Path = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion'
            }
            $result = Test-RegistryDetection -Detection $detection
            $result | Should -BeTrue
        }

        It 'Should return false when Path property is missing' {
            $detection = [PSCustomObject]@{
                Method = 'Registry'
            }
            $result = Test-RegistryDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should block path traversal attempts' {
            $detection = [PSCustomObject]@{
                Method = 'Registry'
                Path = 'HKLM:\SOFTWARE\..\Windows'
            }
            $result = Test-RegistryDetection -Detection $detection
            $result | Should -BeFalse
        }
    }

    Context 'Test-FileDetection' {
        It 'Should return false for non-existent file' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = 'C:\NonExistent\Path\file12345.exe'
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should return true for existing file' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = "$env:SystemRoot\System32\notepad.exe"
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeTrue
        }

        It 'Should expand environment variables in path' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = '%SystemRoot%\System32\notepad.exe'
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeTrue
        }

        It 'Should handle wildcard paths' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = "$env:SystemRoot\System32\note*.exe"
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeTrue
        }

        It 'Should block path traversal attempts' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = 'C:\Windows\..\Windows\System32\notepad.exe'
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should return false when Path is missing' {
            $detection = [PSCustomObject]@{
                Method = 'File'
            }
            $result = Test-FileDetection -Detection $detection
            $result | Should -BeFalse
        }
    }

    Context 'Test-CommandDetection' {
        # Note: The module has a whitelist of allowed executables (java, dotnet, python, node, git, docker, etc.)
        # cmd and powershell are NOT in the whitelist for security reasons

        It 'Should return false for non-whitelisted command' {
            $detection = [PSCustomObject]@{
                Method = 'Command'
                Command = 'cmd'
            }
            $result = Test-CommandDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should return false for non-existent command' {
            $detection = [PSCustomObject]@{
                Method = 'Command'
                Command = 'nonexistentcommand12345xyz'
            }
            $result = Test-CommandDetection -Detection $detection
            $result | Should -BeFalse
        }

        It 'Should accept whitelisted commands like git' {
            $detection = [PSCustomObject]@{
                Method = 'Command'
                Command = 'git --version'
            }
            # Will be true if git is installed, false otherwise
            $result = Test-CommandDetection -Detection $detection
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Test-WindowsFeatureDetection' {
        It 'Should handle Windows Sandbox feature' {
            $detection = [PSCustomObject]@{
                Method = 'WindowsFeature'
                Feature = 'Containers-DisposableClientVM'
            }
            $result = Test-WindowsFeatureDetection -Detection $detection
            $result | Should -BeOfType [bool]
        }

        It 'Should return false for non-existent feature' {
            $detection = [PSCustomObject]@{
                Method = 'WindowsFeature'
                Feature = 'NonExistentFeature12345'
            }
            $result = Test-WindowsFeatureDetection -Detection $detection
            $result | Should -BeFalse
        }
    }

    Context 'Test-WindowsCapabilityDetection' {
        It 'Should handle OpenSSH capability' {
            $detection = [PSCustomObject]@{
                Method = 'WindowsCapability'
                Capability = 'OpenSSH.Client'
            }
            $result = Test-WindowsCapabilityDetection -Detection $detection
            $result | Should -BeOfType [bool]
        }

        It 'Should return false for non-existent capability' {
            $detection = [PSCustomObject]@{
                Method = 'WindowsCapability'
                Capability = 'NonExistentCapability12345'
            }
            $result = Test-WindowsCapabilityDetection -Detection $detection
            $result | Should -BeFalse
        }
    }

    Context 'Test-StoreAppDetection' {
        It 'Should return boolean for Store app detection' {
            $app = [PSCustomObject]@{
                Name = 'Microsoft Store'
                Sources = [PSCustomObject]@{
                    Store = '9WZDNCRFJBMP'
                }
                Detection = [PSCustomObject]@{
                    Method = 'StoreApp'
                    PackageName = 'Microsoft.WindowsStore'
                }
            }
            $result = Test-StoreAppDetection -App $app
            $result | Should -BeOfType [bool]
        }

        It 'Should use WingetListCache when provided' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = [PSCustomObject]@{
                    Store = 'TestStoreId'
                }
                Detection = [PSCustomObject]@{
                    Method = 'StoreApp'
                    PackageName = 'Test.Package'
                }
            }
            $cache = 'Test.Package 1.0.0 installed'
            $result = Test-StoreAppDetection -App $app -WingetListCache $cache
            $result | Should -BeTrue
        }
    }

    Context 'Special App Detection' {
        It 'Should detect PowerToys if installed' {
            $detection = [PSCustomObject]@{
                Method = 'File'
                Path = "${env:ProgramFiles}\PowerToys\PowerToys.exe"
            }
            $app = [PSCustomObject]@{
                Name = 'Microsoft PowerToys'
                Detection = $detection
            }
            $result = Test-AppInstalledParallel -App $app
            $result | Should -BeOfType [bool]
        }

        It 'Should detect Quick Assist if installed' {
            $detection = [PSCustomObject]@{
                Method = 'StoreApp'
                PackageName = 'MicrosoftCorporationII.QuickAssist'
            }
            $app = [PSCustomObject]@{
                Name = 'Microsoft Quick Assist'
                Detection = $detection
            }
            $result = Test-AppInstalledParallel -App $app
            $result | Should -BeOfType [bool]
        }
    }

    Context 'WingetListCache Parameter' {
        It 'Should accept WingetListCache parameter' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Detection = $null
            }
            $cache = 'TestApp 1.0.0'
            $result = Test-AppInstalledParallel -App $app -WingetListCache $cache
            $result | Should -BeOfType [bool]
        }

        It 'Should use cache for detection when provided' {
            $app = [PSCustomObject]@{
                Name = 'CachedTestApp'
                Detection = $null
            }
            $cache = 'CachedTestApp 2.0.0 matched'
            $result = Test-AppInstalledParallel -App $app -WingetListCache $cache
            # Should find match in cache
            $result | Should -BeTrue
        }

        It 'Should return false when app not in cache' {
            $app = [PSCustomObject]@{
                Name = 'NotInCacheApp12345'
                Detection = $null
            }
            $cache = 'OtherApp 1.0.0'
            $result = Test-AppInstalledParallel -App $app -WingetListCache $cache
            $result | Should -BeFalse
        }
    }
}
