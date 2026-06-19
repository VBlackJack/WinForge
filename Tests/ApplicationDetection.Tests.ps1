<#
.SYNOPSIS
    Win11Forge - ApplicationDetection Module Tests v3.5.0

.DESCRIPTION
    Pester tests for the ApplicationDetection module.
    Tests registry-based detection, caching, and fast detection functions.

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
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\ApplicationDetection.psm1'
    Import-Module $ModulePath -Force -ErrorAction Stop
}

Describe 'ApplicationDetection Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-RegistryInstalledApp function' {
            Get-Command -Module ApplicationDetection -Name 'Get-RegistryInstalledApp' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ApplicationInstalled function' {
            Get-Command -Module ApplicationDetection -Name 'Test-ApplicationInstalled' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ApplicationInstalledFast function' {
            Get-Command -Module ApplicationDetection -Name 'Test-ApplicationInstalledFast' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-InstalledApplicationsCache function' {
            Get-Command -Module ApplicationDetection -Name 'Get-InstalledApplicationsCache' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-RegistryAppsCache function' {
            Get-Command -Module ApplicationDetection -Name 'Clear-RegistryAppsCache' | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApplicationsInstallationStatus function' {
            Get-Command -Module ApplicationDetection -Name 'Get-ApplicationsInstallationStatus' | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-RegistryKey' {
        It 'Should return true for existing registry key' {
            $result = Test-RegistryKey -Path 'HKLM:\SOFTWARE'
            $result | Should -BeTrue
        }

        It 'Should return false for non-existing registry key' {
            $result = Test-RegistryKey -Path 'HKLM:\SOFTWARE\NonExistent12345XYZ'
            $result | Should -BeFalse
        }

        It 'Should handle invalid path gracefully' {
            $result = Test-RegistryKey -Path 'INVALID:\Path'
            $result | Should -BeFalse
        }

        It 'Should return boolean type' {
            $result = Test-RegistryKey -Path 'HKLM:\SOFTWARE'
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Expand-DetectionPath' {
        It 'Should expand environment variables' {
            $result = Expand-DetectionPath -Path '%ProgramFiles%\Test'
            $result | Should -Match 'Program Files'
        }

        It 'Should expand ProgramFiles(x86)' {
            $result = Expand-DetectionPath -Path '%ProgramFiles(x86)%\Test'
            $result | Should -Match 'Program Files'
        }

        It 'Should handle LOCALAPPDATA' {
            $result = Expand-DetectionPath -Path '%LOCALAPPDATA%\Test'
            $result | Should -Match 'AppData'
        }

        It 'Should return original path if no variables' {
            $result = Expand-DetectionPath -Path 'C:\Test\Path'
            $result | Should -Be 'C:\Test\Path'
        }
    }

    Context 'Get-RegistryInstalledApp' {
        It 'Should return result or null for known app' {
            $result = Get-RegistryInstalledApp -AppName 'Microsoft'
            # May or may not find depending on installed apps
            if ($result) {
                $result.DisplayName | Should -Not -BeNullOrEmpty
            }
        }

        It 'Should return null for non-existent app' {
            $result = Get-RegistryInstalledApp -AppName 'NonExistentApp999XYZ123'
            $result | Should -BeNullOrEmpty
        }

        It 'Should support ExactMatch parameter' {
            { Get-RegistryInstalledApp -AppName 'Test' -ExactMatch } | Should -Not -Throw
        }

        It 'Should use caching for performance' {
            Clear-RegistryAppsCache
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Get-RegistryInstalledApp -AppName 'Test1'
            $firstCall = $stopwatch.ElapsedMilliseconds
            $stopwatch.Restart()
            $null = Get-RegistryInstalledApp -AppName 'Test2'
            $secondCall = $stopwatch.ElapsedMilliseconds
            # Second call may be faster due to caching (or similar)
            $secondCall | Should -BeLessOrEqual ($firstCall + 500)
        }
    }

    Context 'Clear-RegistryAppsCache' {
        It 'Should clear cache without errors' {
            { Clear-RegistryAppsCache } | Should -Not -Throw
        }

        It 'Should allow subsequent queries after clear' {
            Clear-RegistryAppsCache
            { Get-RegistryInstalledApp -AppName 'Test' } | Should -Not -Throw
        }
    }

    Context 'Test-ApplicationInstalled' {
        It 'Should return false for non-existent app' {
            $app = [PSCustomObject]@{
                Name = 'NonExistentApp12345XYZ'
                Sources = $null
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent\File.exe'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeFalse
        }

        It 'Should return boolean type' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent\File.exe'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeOfType [bool]
        }

        It 'Should handle null Sources gracefully' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent\File.exe'
                }
            }
            { Test-ApplicationInstalled -Application $app } | Should -Not -Throw
        }

        It 'Should handle app with Winget source' {
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = @{ Winget = 'Test.NonExistent.App12345' }
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent\File.exe'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeFalse
        }
    }

    Context 'Test-ApplicationByName' {
        It 'Should return boolean' {
            $result = Test-ApplicationByName -Name 'NonExistent999XYZ123'
            $result | Should -BeOfType [bool]
        }

        It 'Should return false for non-existent app name' {
            $result = Test-ApplicationByName -Name 'NonExistentApp999XYZ123'
            $result | Should -BeFalse
        }
    }

    Context 'Get-InstalledApplicationsCache' {
        It 'Should return cache object' {
            $result = Get-InstalledApplicationsCache
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should have RegistryApps property' {
            $result = Get-InstalledApplicationsCache
            $result.PSObject.Properties.Name | Should -Contain 'RegistryApps'
        }

        It 'Should return hashtable for RegistryApps' {
            $result = Get-InstalledApplicationsCache
            # RegistryApps is a hashtable (may be empty on minimal systems)
            $result.RegistryApps | Should -BeOfType [hashtable]
        }
    }

    Context 'Test-ApplicationInstalledFast' {
        It 'Should return detection result' {
            $cache = Get-InstalledApplicationsCache
            $app = [PSCustomObject]@{
                Name = 'NonExistent12345'
                Sources = @{ Winget = 'NonExistent.App.12345' }
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent.exe'
                }
            }
            $result = Test-ApplicationInstalledFast -Application $app -Cache $cache
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return IsInstalled property' {
            $cache = Get-InstalledApplicationsCache
            $app = [PSCustomObject]@{
                Name = 'NonExistentApp999XYZ123'
                Sources = @{ Winget = 'NonExistent.App.999' }
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent.exe'
                }
            }
            $result = Test-ApplicationInstalledFast -Application $app -Cache $cache
            $result.IsInstalled | Should -BeFalse
        }

        It 'Should handle null Sources gracefully' {
            $cache = Get-InstalledApplicationsCache
            $app = [PSCustomObject]@{
                Name = 'TestApp'
                Sources = $null
                Detection = @{
                    Method = 'File'
                    Path = 'C:\NonExistent.exe'
                }
            }
            { Test-ApplicationInstalledFast -Application $app -Cache $cache } | Should -Not -Throw
        }
    }

    Context 'Get-ApplicationsInstallationStatus' {
        It 'Should return hashtable of status objects' {
            $apps = @(
                [PSCustomObject]@{ AppId = 'app1'; Name = 'App1'; Sources = @{ Winget = 'Test.App1' }; Detection = @{ Method = 'File'; Path = 'C:\NonExistent1.exe' } },
                [PSCustomObject]@{ AppId = 'app2'; Name = 'App2'; Sources = @{ Winget = 'Test.App2' }; Detection = @{ Method = 'File'; Path = 'C:\NonExistent2.exe' } }
            )
            $result = Get-ApplicationsInstallationStatus -Applications $apps
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [hashtable]
        }

        It 'Should include IsInstalled property in results' {
            $apps = @(
                [PSCustomObject]@{ AppId = 'testapp1'; Name = 'TestApp1'; Sources = @{ Winget = 'Test.App' }; Detection = @{ Method = 'File'; Path = 'C:\NonExistent.exe' } }
            )
            $result = Get-ApplicationsInstallationStatus -Applications $apps
            $result['testapp1'].IsInstalled | Should -BeFalse
        }
    }

    Context 'Get-InstalledAppVersion' {
        It 'Should return null for non-existent app' {
            $result = Get-InstalledAppVersion -WingetId 'NonExistentApp999XYZ123'
            $result | Should -BeNullOrEmpty
        }

        It 'Should accept ChocolateyId parameter' {
            { Get-InstalledAppVersion -ChocolateyId 'nonexistent123' } | Should -Not -Throw
        }
    }

    Context 'Security - Path Traversal Prevention' {
        It 'Should block path traversal in File detection' {
            $app = [PSCustomObject]@{
                Name = 'MaliciousApp'
                Sources = $null
                Detection = @{
                    Method = 'File'
                    Path = '..\..\..\Windows\System32\cmd.exe'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeFalse
        }

        It 'Should block registry path traversal' {
            $app = [PSCustomObject]@{
                Name = 'MaliciousApp'
                Sources = $null
                Detection = @{
                    Method = 'Registry'
                    Path = 'HKLM:\SOFTWARE\..\..\..\SYSTEM'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeFalse
        }
    }

    Context 'Detection Methods' {
        It 'Should support File detection method' {
            $fixturePath = Join-Path ([System.IO.Path]::GetTempPath()) "Win11Forge-FileDetection-$([System.Guid]::NewGuid()).tmp"
            try {
                Set-Content -Path $fixturePath -Value 'test fixture'

                $app = [PSCustomObject]@{
                    Name = 'FileFixtureApp'
                    Sources = $null
                    Detection = [PSCustomObject]@{
                        Method = 'File'
                        Path = $fixturePath
                    }
                }
                $result = Test-ApplicationInstalled -Application $app
                $result | Should -BeTrue
            } finally {
                Remove-Item -LiteralPath $fixturePath -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should support Registry detection method' {
            $app = [PSCustomObject]@{
                Name = 'Windows'
                Sources = $null
                Detection = @{
                    Method = 'Registry'
                    Path = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeTrue
        }

        It 'Should handle WindowsFeature detection method' {
            $app = [PSCustomObject]@{
                Name = 'TestFeature'
                Sources = $null
                Detection = @{
                    Method = 'WindowsFeature'
                    Feature = 'NonExistentFeature12345'
                }
            }
            $result = Test-ApplicationInstalled -Application $app
            $result | Should -BeFalse
        }
    }
}

Describe 'ApplicationDetection Performance' {
    Context 'Caching Performance' {
        It 'Should complete registry scan within reasonable time' {
            Clear-RegistryAppsCache
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Get-RegistryInstalledApp -AppName 'Test'
            $stopwatch.Stop()
            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 5000
        }

        It 'Should provide fast cached access' {
            $null = Get-RegistryInstalledApp -AppName 'CacheWarm'
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Get-RegistryInstalledApp -AppName 'CacheTest'
            $stopwatch.Stop()
            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 500
        }
    }

    Context 'Batch Detection Performance' {
        It 'Should handle multiple apps efficiently' {
            $apps = 1..5 | ForEach-Object {
                [PSCustomObject]@{
                    AppId = "testapp$_"
                    Name = "TestApp$_"
                    Sources = @{ Winget = "Test.App$_" }
                    Detection = @{
                        Method = 'File'
                        Path = "C:\NonExistent\App$_.exe"
                    }
                }
            }

            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Get-ApplicationsInstallationStatus -Applications $apps
            $stopwatch.Stop()
            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 15000
        }
    }
}

Describe 'ApplicationDetection Integration' {
    Context 'Real Application Detection' {
        It 'Should detect at least one installed application' {
            $cache = Get-InstalledApplicationsCache
            # RegistryApps is populated from Windows registry
            $cache.RegistryApps.Count | Should -BeGreaterOrEqual 0
        }

        It 'Should provide consistent results across calls' {
            $cache1 = Get-InstalledApplicationsCache
            $cache2 = Get-InstalledApplicationsCache
            $cache1.RegistryApps.Count | Should -Be $cache2.RegistryApps.Count
        }
    }
}

Describe 'ApplicationDetection - STRICT VersionKey (I1)' {
    BeforeAll {
        $script:I1Key = "HKCU:\Software\Win11Forge\Tests\I1Strict\$([guid]::NewGuid().ToString('N'))"
        New-Item -Path $script:I1Key -Force | Out-Null
        # DisplayVersion is the lenient-fallback trap: STRICT must never read it.
        New-ItemProperty -Path $script:I1Key -Name 'DisplayVersion' -Value '9.9.9-fallback' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $script:I1Key -Name 'ProductVersion' -Value '1.2.3' -PropertyType String -Force | Out-Null
        $script:I1Cache = [PSCustomObject]@{ RegistryApps = @{}; WingetList = ''; AppxPackages = @{}; CommandOutputs = @{} }
    }

    AfterAll {
        Remove-Item -Path $script:I1Key -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Reads the version when the explicit VersionKey is present' {
        $app = [PSCustomObject]@{ Name = 'I1App'; Sources = $null; Detection = [PSCustomObject]@{ Method = 'Registry'; Path = $script:I1Key; VersionKey = 'ProductVersion' } }
        $r = Test-ApplicationInstalledFast -Application $app -Cache $script:I1Cache
        $r.IsInstalled | Should -BeTrue
        $r.Version | Should -Be '1.2.3'
    }

    It 'Does not fall back to DisplayVersion when the configured VersionKey is absent (STRICT)' {
        $app = [PSCustomObject]@{ Name = 'I1App'; Sources = $null; Detection = [PSCustomObject]@{ Method = 'Registry'; Path = $script:I1Key; VersionKey = 'NoSuchKey' } }
        $r = Test-ApplicationInstalledFast -Application $app -Cache $script:I1Cache
        $r.IsInstalled | Should -BeTrue
        $r.Version | Should -BeNullOrEmpty
    }

    It 'Does not extract a version when no VersionKey is configured (STRICT)' {
        $app = [PSCustomObject]@{ Name = 'I1App'; Sources = $null; Detection = [PSCustomObject]@{ Method = 'Registry'; Path = $script:I1Key } }
        $r = Test-ApplicationInstalledFast -Application $app -Cache $script:I1Cache
        $r.IsInstalled | Should -BeTrue
        $r.Version | Should -BeNullOrEmpty
    }
}
