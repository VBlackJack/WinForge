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
            $cmd = Get-Command Test-ValidDownloadUrl -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ViaWinget function' {
            $cmd = Get-Command Install-ViaWinget -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ViaChocolatey function' {
            $cmd = Get-Command Install-ViaChocolatey -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ViaStore function' {
            $cmd = Get-Command Install-ViaStore -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ViaDirectDownload function' {
            $cmd = Get-Command Install-ViaDirectDownload -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-MsiPackage function' {
            $cmd = Get-Command Install-MsiPackage -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ExePackage function' {
            $cmd = Get-Command Install-ExePackage -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Install-ZipPackage function' {
            $cmd = Get-Command Install-ZipPackage -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Start-ProcessWithTimeout function' {
            $cmd = Get-Command Start-ProcessWithTimeout -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
        }

        It 'Should export Format-FileSize function' {
            $cmd = Get-Command Format-FileSize -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.CommandType | Should -Be 'Function'
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
            $result | Should -BeOfType [string]
            $result | Should -Be '500 bytes'
        }

        It 'Should format kilobytes correctly' {
            $result = Format-FileSize -Bytes 1024
            $result | Should -BeOfType [string]
            # Use locale-independent pattern: decimal separator may be '.' or ','
            $result | Should -Match '^1[.,]00 KB$'
        }

        It 'Should format megabytes correctly' {
            $result = Format-FileSize -Bytes (1024 * 1024)
            $result | Should -BeOfType [string]
            $result | Should -Match '^1[.,]00 MB$'
        }

        It 'Should format gigabytes correctly' {
            $result = Format-FileSize -Bytes (1024 * 1024 * 1024)
            $result | Should -BeOfType [string]
            $result | Should -Match '^1[.,]00 GB$'
        }

        It 'Should handle zero bytes' {
            $result = Format-FileSize -Bytes 0
            $result | Should -BeOfType [string]
            $result | Should -Be '0 bytes'
        }
    }

    Context 'Install-ViaWinget - Parameter Validation' {
        It 'Should require PackageId parameter' {
            { Install-ViaWinget -PackageId $null } | Should -Throw
        }

        It 'Should accept valid PackageId as mandatory string parameter' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['PackageId'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['PackageId'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -BeGreaterThan 0
        }

        It 'Should have Silent as switch parameter' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['Silent'].ParameterType | Should -Be ([switch])
        }

        It 'Should have MaxRetries as optional int parameter' {
            $cmd = Get-Command Install-ViaWinget
            $cmd.Parameters['MaxRetries'].ParameterType | Should -Be ([int])
            $paramAttributes = $cmd.Parameters['MaxRetries'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }
    }

    Context 'Install-ViaChocolatey - Parameter Validation' {
        It 'Should require PackageName parameter' {
            { Install-ViaChocolatey -PackageName $null } | Should -Throw
        }

        It 'Should accept valid PackageName as mandatory string parameter' {
            $cmd = Get-Command Install-ViaChocolatey
            $cmd.Parameters['PackageName'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['PackageName'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -BeGreaterThan 0
        }

        It 'Should have MaxRetries as optional int parameter' {
            $cmd = Get-Command Install-ViaChocolatey
            $cmd.Parameters['MaxRetries'].ParameterType | Should -Be ([int])
            $paramAttributes = $cmd.Parameters['MaxRetries'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }
    }

    Context 'Install-ViaStore - Parameter Validation' {
        It 'Should require ProductId parameter' {
            { Install-ViaStore -ProductId $null } | Should -Throw
        }

        It 'Should accept valid ProductId as mandatory string parameter with regex validation' {
            $cmd = Get-Command Install-ViaStore
            $cmd.Parameters['ProductId'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['ProductId'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -BeGreaterThan 0
            $validatePattern = $cmd.Parameters['ProductId'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }
            $validatePattern | Should -Not -BeNullOrEmpty
            $validatePattern.RegexPattern | Should -Be '^[a-zA-Z0-9._-]+$'
        }
    }

    Context 'Install-ViaDirectDownload - Parameter Validation' {
        It 'Should require Url parameter' {
            { Install-ViaDirectDownload -Url $null } | Should -Throw
        }

        It 'Should have InstallerType as string parameter with ValidateSet' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['InstallerType'].ParameterType | Should -Be ([string])
            $validateSet = $cmd.Parameters['InstallerType'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
            $validateSet | Should -Not -BeNullOrEmpty
            $validateSet.ValidValues | Should -Contain 'exe'
            $validateSet.ValidValues | Should -Contain 'msi'
            $validateSet.ValidValues | Should -Contain 'zip'
            $validateSet.ValidValues | Should -Contain 'auto'
        }

        It 'Should have ExpectedSHA256 as optional string parameter' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['ExpectedSHA256'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['ExpectedSHA256'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }

        It 'Should have CustomArguments as optional string parameter' {
            $cmd = Get-Command Install-ViaDirectDownload
            $cmd.Parameters['CustomArguments'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['CustomArguments'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }
    }

    Context 'Install-MsiPackage - Parameter Validation' {
        It 'Should require InstallerPath parameter' {
            { Install-MsiPackage -InstallerPath $null } | Should -Throw
        }

        It 'Should have InstallerPath as mandatory' {
            $cmd = Get-Command Install-MsiPackage
            $paramAttributes = $cmd.Parameters['InstallerPath'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -BeGreaterThan 0
        }
    }

    Context 'Install-ExePackage - Parameter Validation' {
        It 'Should require InstallerPath parameter' {
            { Install-ExePackage -InstallerPath $null } | Should -Throw
        }

        It 'Should have CustomArguments as optional string parameter' {
            $cmd = Get-Command Install-ExePackage
            $cmd.Parameters['CustomArguments'].ParameterType | Should -Be ([string])
            $paramAttributes = $cmd.Parameters['CustomArguments'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }
    }

    Context 'Test-InstallerSignature' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Status { } -ModuleName InstallationMethods
        }

        It 'Should return true when signature is valid and publisher matches' {
            Mock Get-AuthenticodeSignature {
                [pscustomobject]@{
                    Status            = 'Valid'
                    SignerCertificate = [pscustomobject]@{
                        Subject = 'CN=Mozilla Corporation, O=Mozilla Corporation, C=US'
                    }
                }
            } -ModuleName InstallationMethods

            Test-InstallerSignature -FilePath 'x' -ExpectedPublisher 'Mozilla Corporation' | Should -BeTrue
        }

        It 'Should return false when signature status is not valid' {
            Mock Get-AuthenticodeSignature {
                [pscustomobject]@{
                    Status            = 'HashMismatch'
                    SignerCertificate = [pscustomobject]@{
                        Subject = 'CN=Mozilla Corporation, O=Mozilla Corporation, C=US'
                    }
                }
            } -ModuleName InstallationMethods

            Test-InstallerSignature -FilePath 'x' -ExpectedPublisher 'Mozilla Corporation' | Should -BeFalse
        }

        It 'Should return false when publisher does not match' {
            Mock Get-AuthenticodeSignature {
                [pscustomobject]@{
                    Status            = 'Valid'
                    SignerCertificate = [pscustomobject]@{
                        Subject = 'CN=Evil Corp'
                    }
                }
            } -ModuleName InstallationMethods

            Test-InstallerSignature -FilePath 'x' -ExpectedPublisher 'Mozilla Corporation' | Should -BeFalse
        }

        It 'Should return false when signature lookup throws' {
            Mock Get-AuthenticodeSignature { throw 'boom' } -ModuleName InstallationMethods

            Test-InstallerSignature -FilePath 'x' -ExpectedPublisher 'Mozilla Corporation' | Should -BeFalse
        }
    }

    Context 'Start-ProcessWithTimeout' {
        It 'Should require FilePath parameter' {
            { Start-ProcessWithTimeout -FilePath $null } | Should -Throw
        }

        It 'Should have TimeoutSeconds as optional int parameter' {
            $cmd = Get-Command Start-ProcessWithTimeout
            $cmd.Parameters['TimeoutSeconds'].ParameterType | Should -Be ([int])
            $paramAttributes = $cmd.Parameters['TimeoutSeconds'].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            @($paramAttributes | Where-Object { $_.Mandatory }).Count | Should -Be 0
        }

        It 'Should execute a simple command and return object with expected properties' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c echo test' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result | Should -BeOfType [PSCustomObject]
            $result.PSObject.Properties.Name | Should -Contain 'ExitCode'
            $result.PSObject.Properties.Name | Should -Contain 'Id'
            $result.PSObject.Properties.Name | Should -Contain 'HasExited'
            $result.ExitCode | Should -BeOfType [int]
            $result.HasExited | Should -BeTrue
        }

        It 'Should return process with ExitCode as integer' {
            $result = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c exit 0' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result.ExitCode | Should -BeOfType [int]
            $result.HasExited | Should -BeTrue
        }

        It 'Should return consistent ExitCode for different exit values' {
            $result0 = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c exit 0' -TimeoutSeconds 30 -PassThru -NoNewWindow
            $result1 = Start-ProcessWithTimeout -FilePath 'cmd.exe' -ArgumentList '/c exit 1' -TimeoutSeconds 30 -PassThru -NoNewWindow
            # Both should return integer exit codes and completed processes
            $result0.ExitCode | Should -BeOfType [int]
            $result1.ExitCode | Should -BeOfType [int]
            $result0.HasExited | Should -BeTrue
            $result1.HasExited | Should -BeTrue
        }
    }
}

Describe 'InstallationMethods Mock-Based Tests' {

    BeforeAll {
        # Suppress console output from Write-Status and Write-Host during mock tests
        Mock Write-Status { } -ModuleName InstallationMethods
        Mock Write-Host { }
    }

    Context 'Install-ViaWinget - Winget Unavailable' {
        BeforeEach {
            Mock Test-CommandExists { return $false } -ParameterFilter { $Name -eq 'winget' } -ModuleName InstallationMethods
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
        }

        It 'Should return false when winget is not installed' {
            $result = Install-ViaWinget -PackageId 'Test.Package'
            $result | Should -BeFalse
        }

        It 'Should call Test-CommandExists to check for winget' {
            Install-ViaWinget -PackageId 'Test.Package'
            Should -Invoke -CommandName Test-CommandExists -ModuleName InstallationMethods -Times 1 -ParameterFilter { $Name -eq 'winget' }
        }
    }

    Context 'Install-ViaChocolatey - Chocolatey Unavailable' {
        BeforeEach {
            Mock Test-CommandExists { return $false } -ParameterFilter { $Name -eq 'choco' } -ModuleName InstallationMethods
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
        }

        It 'Should return false when choco is not installed' {
            $result = Install-ViaChocolatey -PackageName 'test-package'
            $result | Should -BeFalse
        }

        It 'Should call Test-CommandExists to check for choco' {
            Install-ViaChocolatey -PackageName 'test-package'
            Should -Invoke -CommandName Test-CommandExists -ModuleName InstallationMethods -Times 1 -ParameterFilter { $Name -eq 'choco' }
        }
    }

    Context 'Install-ViaStore - Sandbox Environment Restriction' {
        BeforeEach {
            Mock Test-IsWindowsSandbox { return $true } -ModuleName InstallationMethods
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
        }

        It 'Should return false when running in Windows Sandbox' {
            $result = Install-ViaStore -ProductId 'test.product.id'
            $result | Should -BeFalse
        }

        It 'Should call Test-IsWindowsSandbox for sandbox detection' {
            Install-ViaStore -ProductId 'test.product.id'
            Should -Invoke -CommandName Test-IsWindowsSandbox -ModuleName InstallationMethods -Times 1
        }
    }

    Context 'Install-ViaStore - Non-Sandbox with Winget Unavailable' {
        BeforeEach {
            Mock Test-IsWindowsSandbox { return $false } -ModuleName InstallationMethods
            Mock Test-CommandExists { return $false } -ParameterFilter { $Name -eq 'winget' } -ModuleName InstallationMethods
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
            # Mock Start-Process to prevent opening the Store URI
            Mock Start-Process { } -ModuleName InstallationMethods
        }

        It 'Should fall back to Store URI when winget is unavailable' {
            $result = Install-ViaStore -ProductId 'test.product.id'
            $result | Should -BeFalse
        }

        It 'Should invoke Start-Process for store protocol' {
            Install-ViaStore -ProductId 'test.product.id'
            Should -Invoke -CommandName Start-Process -ModuleName InstallationMethods -Times 1
        }
    }

    Context 'Install-ViaDirectDownload - Invalid URL' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
        }

        It 'Should return false for FTP URL' {
            $result = Install-ViaDirectDownload -Url 'ftp://example.com/file.exe'
            $result | Should -BeFalse
        }

        It 'Should return false for file:// URL' {
            $result = Install-ViaDirectDownload -Url 'file:///C:/test.exe'
            $result | Should -BeFalse
        }
    }

    Context 'Install-ViaDirectDownload - Download Failure' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
            # Mock the download to fail
            Mock Invoke-FileDownloadWithProgress { return $false } -ModuleName InstallationMethods
            # Mock Test-ValidDownloadUrl to accept the URL
            Mock Test-ValidDownloadUrl { return $true } -ModuleName InstallationMethods
            # Mock temp directory creation
            Mock New-Item { return [PSCustomObject]@{ FullName = 'C:\Temp\Win11Forge_test' } } -ModuleName InstallationMethods -ParameterFilter { $ItemType -eq 'Directory' }
            Mock Test-Path { return $false } -ModuleName InstallationMethods -ParameterFilter { $Path -and $Path -like '*Win11Forge_*' }
            Mock Remove-Item { } -ModuleName InstallationMethods
        }

        It 'Should return false when download fails' {
            $result = Install-ViaDirectDownload -Url 'https://github.com/test/release/file.exe'
            $result | Should -BeFalse
        }

        It 'Should invoke Invoke-FileDownloadWithProgress' {
            Install-ViaDirectDownload -Url 'https://github.com/test/release/file.exe'
            Should -Invoke -CommandName Invoke-FileDownloadWithProgress -ModuleName InstallationMethods -Times 1
        }
    }

    Context 'Install-MsiPackage - Mock Process Execution' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
        }

        It 'Should return true when msiexec exits with code 0' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 0; Id = 1234; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-MsiPackage -InstallerPath 'C:\Temp\test.msi'
            $result | Should -BeTrue
        }

        It 'Should return false when msiexec exits with non-zero code' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 1603; Id = 1234; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-MsiPackage -InstallerPath 'C:\Temp\test.msi'
            $result | Should -BeFalse
        }

        It 'Should call Start-ProcessWithTimeout with msiexec.exe' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 0; Id = 1234; HasExited = $true }
            } -ModuleName InstallationMethods

            Install-MsiPackage -InstallerPath 'C:\Temp\test.msi'
            Should -Invoke -CommandName Start-ProcessWithTimeout -ModuleName InstallationMethods -Times 1 -ParameterFilter {
                $FilePath -eq 'msiexec.exe'
            }
        }
    }

    Context 'Install-ExePackage - Mock Process Execution' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
        }

        It 'Should return true with custom arguments and exit code 0' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 0; Id = 5678; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-ExePackage -InstallerPath 'C:\Temp\setup.exe' -CustomArguments '/S'
            $result | Should -BeTrue
        }

        It 'Should return false with custom arguments and non-zero exit code' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 1; Id = 5678; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-ExePackage -InstallerPath 'C:\Temp\setup.exe' -CustomArguments '/S'
            $result | Should -BeFalse
        }

        It 'Should try multiple silent switches when no custom arguments given' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 0; Id = 5678; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-ExePackage -InstallerPath 'C:\Temp\setup.exe'
            $result | Should -BeTrue
            # At least one call with a silent switch should succeed
            Should -Invoke -CommandName Start-ProcessWithTimeout -ModuleName InstallationMethods -Times 1 -Exactly
        }

        It 'Should return false when all silent switches fail' {
            Mock Start-ProcessWithTimeout {
                return [PSCustomObject]@{ ExitCode = 1; Id = 5678; HasExited = $true }
            } -ModuleName InstallationMethods

            $result = Install-ExePackage -InstallerPath 'C:\Temp\setup.exe'
            $result | Should -BeFalse
        }
    }

    Context 'Install-ZipPackage - Portable Deployment' {
        It 'Should expand DetectionPath environment variables and flatten a single root folder' {
            Mock Write-Status { } -ModuleName InstallationMethods
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods

            $testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "Win11Forge-ZipInstall-$([System.Guid]::NewGuid())"
            $sourceRoot = Join-Path $testRoot 'source\PortableRoot'
            $archivePath = Join-Path $testRoot 'portable.zip'
            $workPath = Join-Path $testRoot 'work'
            $destinationRoot = Join-Path $testRoot 'dest'
            $oldDestination = $env:WIN11FORGE_ZIP_TEST_DEST

            try {
                New-Item -Path $sourceRoot -ItemType Directory -Force | Out-Null
                Set-Content -Path (Join-Path $sourceRoot 'App.exe') -Value 'test' -Encoding UTF8
                Compress-Archive -Path $sourceRoot -DestinationPath $archivePath

                $env:WIN11FORGE_ZIP_TEST_DEST = $destinationRoot
                $result = Install-ZipPackage `
                    -InstallerPath $archivePath `
                    -TempDir $workPath `
                    -DetectionPath '%WIN11FORGE_ZIP_TEST_DEST%\PortableRoot\App.exe'

                $result | Should -BeTrue
                Test-Path -Path (Join-Path $destinationRoot 'PortableRoot\App.exe') | Should -BeTrue
                Test-Path -Path (Join-Path $destinationRoot 'PortableRoot\PortableRoot\App.exe') | Should -BeFalse
            } finally {
                if ($null -eq $oldDestination) {
                    Remove-Item -Path Env:\WIN11FORGE_ZIP_TEST_DEST -ErrorAction SilentlyContinue
                } else {
                    $env:WIN11FORGE_ZIP_TEST_DEST = $oldDestination
                }

                Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Test-ValidDownloadUrl - Mocked Domain Validation' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
        }

        It 'Should accept trusted GitHub domain' {
            $result = Test-ValidDownloadUrl -Url 'https://github.com/owner/repo/releases/download/v1.0/app.exe'
            $result | Should -BeTrue
        }

        It 'Should accept trusted Microsoft domain' {
            $result = Test-ValidDownloadUrl -Url 'https://download.microsoft.com/package.msi'
            $result | Should -BeTrue
        }

        It 'Should reject localhost URLs (SSRF protection)' {
            $result = Test-ValidDownloadUrl -Url 'https://localhost/malicious.exe'
            $result | Should -BeFalse
        }

        It 'Should reject 127.0.0.1 URLs (SSRF protection)' {
            $result = Test-ValidDownloadUrl -Url 'https://127.0.0.1/malicious.exe'
            $result | Should -BeFalse
        }
    }

    Context 'Invoke-FileDownloadWithProgress - Security Checks' {
        BeforeEach {
            Mock Get-LocalizedString { return $Key } -ModuleName InstallationMethods
            Mock Write-Output { } -ModuleName InstallationMethods
        }

        It 'Should reject URLs with dangerous characters (semicolon)' {
            $result = Invoke-FileDownloadWithProgress -Url 'https://example.com/file.exe;rm -rf /' -OutputPath 'C:\Temp\test.exe'
            $result | Should -BeFalse
        }

        It 'Should reject URLs with dangerous characters (pipe)' {
            $result = Invoke-FileDownloadWithProgress -Url 'https://example.com/file.exe|evil' -OutputPath 'C:\Temp\test.exe'
            $result | Should -BeFalse
        }

        It 'Should reject URLs with dangerous characters (backtick)' {
            $result = Invoke-FileDownloadWithProgress -Url 'https://example.com/file.exe`cmd' -OutputPath 'C:\Temp\test.exe'
            $result | Should -BeFalse
        }

        It 'Should reject FTP scheme' {
            $result = Invoke-FileDownloadWithProgress -Url 'ftp://example.com/file.exe' -OutputPath 'C:\Temp\test.exe'
            $result | Should -BeFalse
        }
    }

    Context 'Test-SafeExtractPath - Path Traversal Protection' {
        It 'Should allow paths within target directory' {
            InModuleScope InstallationMethods {
                $targetDir = $env:TEMP
                $safePath = Join-Path $targetDir 'safe_file.txt'
                Mock Write-Status { }
                $result = Test-SafeExtractPath -ExtractedPath $safePath -TargetDirectory $targetDir
                $result | Should -BeTrue
            }
        }

        It 'Should block path traversal attempts' {
            InModuleScope InstallationMethods {
                $targetDir = Join-Path $env:TEMP 'extract_target'
                $maliciousPath = Join-Path $targetDir '..\..\Windows\System32\evil.dll'
                Mock Write-Status { }
                Mock Get-LocalizedString { return $Key }
                $result = Test-SafeExtractPath -ExtractedPath $maliciousPath -TargetDirectory $targetDir
                $result | Should -BeFalse
            }
        }
    }
}
