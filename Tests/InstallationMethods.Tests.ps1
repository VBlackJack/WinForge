<#
.SYNOPSIS
    Pester tests for InstallationMethods module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge InstallationMethods v3.5.0
    Tests installation methods: Winget, Chocolatey, Store, DirectDownload

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
    $script:InstallationMethodsPath = Join-Path $script:ModuleRoot 'InstallationMethods.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import InstallationMethods
    Import-Module $script:InstallationMethodsPath -Force -ErrorAction Stop
}

Describe 'InstallationMethods Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:InstallationMethodsPath -Force } | Should -Not -Throw
        }

        It 'Should export Test-ValidDownloadUrl function' {
            Get-Command Test-ValidDownloadUrl -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ViaWinget function' {
            Get-Command Install-ViaWinget -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ViaChocolatey function' {
            Get-Command Install-ViaChocolatey -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ViaStore function' {
            Get-Command Install-ViaStore -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ViaDirectDownload function' {
            Get-Command Install-ViaDirectDownload -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-MsiPackage function' {
            Get-Command Install-MsiPackage -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ExePackage function' {
            Get-Command Install-ExePackage -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Install-ZipPackage function' {
            Get-Command Install-ZipPackage -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Start-ProcessWithTimeout function' {
            Get-Command Start-ProcessWithTimeout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Format-FileSize function' {
            Get-Command Format-FileSize -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-ValidDownloadUrl' {
        It 'Should return true for valid HTTPS URL with AllowUntrusted' {
            # Use AllowUntrusted since example.com is not in trusted domains
            $result = Test-ValidDownloadUrl -Url 'https://example.com/file.exe' -AllowUntrusted
            $result | Should -BeTrue
        }

        It 'Should return true for valid HTTP URL with AllowUntrusted' {
            $result = Test-ValidDownloadUrl -Url 'http://example.com/file.msi' -AllowUntrusted
            $result | Should -BeTrue
        }

        It 'Should return false for invalid URL format' {
            $result = Test-ValidDownloadUrl -Url 'not-a-url'
            $result | Should -BeFalse
        }

        It 'Should return false for file:// protocol' {
            $result = Test-ValidDownloadUrl -Url 'file:///C:/test.exe'
            $result | Should -BeFalse
        }

        It 'Should reject FTP URLs' {
            $result = Test-ValidDownloadUrl -Url 'ftp://example.com/file.exe'
            $result | Should -BeFalse
        }

        It 'Should handle URL with query parameters' {
            $result = Test-ValidDownloadUrl -Url 'https://example.com/file.exe?version=1.0&token=abc' -AllowUntrusted
            $result | Should -BeTrue
        }
    }

    Context 'Format-FileSize' {
        It 'Should format bytes correctly' {
            $result = Format-FileSize -Bytes 500
            $result | Should -Match '500.*B'
        }

        It 'Should format kilobytes correctly' {
            $result = Format-FileSize -Bytes 1024
            $result | Should -Match '1.*KB'
        }

        It 'Should format megabytes correctly' {
            $result = Format-FileSize -Bytes (1024 * 1024)
            $result | Should -Match '1.*MB'
        }

        It 'Should format gigabytes correctly' {
            $result = Format-FileSize -Bytes (1024 * 1024 * 1024)
            $result | Should -Match '1.*GB'
        }

        It 'Should handle zero bytes' {
            $result = Format-FileSize -Bytes 0
            $result | Should -Match '0'
        }
    }

    Context 'Install-ViaWinget - Parameter Validation' {
        It 'Should require PackageId parameter' {
            { Install-ViaWinget -PackageId $null } | Should -Throw
        }

        It 'Should accept valid PackageId' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['PackageId'] | Should -Not -BeNullOrEmpty
        }

        It 'Should have Silent parameter' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['Silent'] | Should -Not -BeNullOrEmpty
        }

        It 'Should have MaxRetries parameter' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['MaxRetries'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Install-ViaChocolatey - Parameter Validation' {
        It 'Should require PackageName parameter' {
            { Install-ViaChocolatey -PackageName $null } | Should -Throw
        }

        It 'Should accept valid PackageName' {
            $cmd = Get-Command Install-ViaChocolatey
            $cmd.Parameters['PackageName'] | Should -Not -BeNullOrEmpty
        }

        It 'Should have MaxRetries parameter' {
            $cmd = Get-Command Install-ViaChocolatey
            $cmd.Parameters['MaxRetries'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Install-ViaStore - Parameter Validation' {
        It 'Should require ProductId parameter' {
            { Install-ViaStore -ProductId $null } | Should -Throw
        }

        It 'Should accept valid ProductId' {
            $cmd = Get-Command Install-ViaStore
            $cmd.Parameters['ProductId'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Install-ViaDirectDownload - Parameter Validation' {
        It 'Should require Url parameter' {
            { Install-ViaDirectDownload -Url $null } | Should -Throw
        }

        It 'Should have InstallerType parameter' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['InstallerType'] | Should -Not -BeNullOrEmpty
        }

        It 'Should have ExpectedSHA256 parameter' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['ExpectedSHA256'] | Should -Not -BeNullOrEmpty
        }

        It 'Should have CustomArguments parameter' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['CustomArguments'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Install-MsiPackage - Parameter Validation' {
        It 'Should require InstallerPath parameter' {
            { Install-MsiPackage -InstallerPath $null } | Should -Throw
        }

        It 'Should have InstallerPath as mandatory' {
            $cmd = Get-Command Install-MsiPackage
            $cmd.Parameters['InstallerPath'].Attributes.Mandatory | Should -BeTrue
        }
    }

    Context 'Install-ExePackage - Parameter Validation' {
        It 'Should require InstallerPath parameter' {
            { Install-ExePackage -InstallerPath $null } | Should -Throw
        }

        It 'Should have CustomArguments parameter' {
            $cmd = Get-Command Install-ExePackage
            $cmd.Parameters['CustomArguments'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Start-ProcessWithTimeout' {
        It 'Should require FilePath parameter' {
            { Start-ProcessWithTimeout -FilePath $null } | Should -Throw
        }

        It 'Should have TimeoutSeconds parameter' {
            $cmd = Get-Command Start-ProcessWithTimeout
            $cmd.Parameters['TimeoutSeconds'] | Should -Not -BeNullOrEmpty
        }

        It 'Should execute a simple command successfully' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c echo test' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return process with ExitCode property' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c exit 0' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result.ExitCode | Should -Be 0
        }

        It 'Should handle non-zero exit codes' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c exit 1' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result.ExitCode | Should -Be 1
        }
    }
}
