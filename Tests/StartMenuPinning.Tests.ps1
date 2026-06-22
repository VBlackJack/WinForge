<#
.SYNOPSIS
    Pester tests for StartMenuPinning module

.DESCRIPTION
    Comprehensive unit tests for WinForge StartMenuPinning v2.5.0
    Tests Start Menu pinning using start2.bin method

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
    $script:PinningPath = Join-Path $script:ModuleRoot 'StartMenuPinning.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    Import-Module $script:PinningPath -Force -ErrorAction Stop
}

Describe 'StartMenuPinning Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:PinningPath -Force } | Should -Not -Throw
        }

        It 'Should export Get-StartMenuBinaryType function' {
            Get-Command Get-StartMenuBinaryType -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-CurrentUserStartMenuBinary function' {
            Get-Command Get-CurrentUserStartMenuBinary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-DefaultProfileStartMenuBinary function' {
            Get-Command Get-DefaultProfileStartMenuBinary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Backup-StartMenuLayout function' {
            Get-Command Backup-StartMenuLayout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Publish-StartMenuLayoutToDefault function' {
            Get-Command Publish-StartMenuLayoutToDefault -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-BackedUpLayouts function' {
            Get-Command Get-BackedUpLayouts -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-StartMenuPinning function' {
            Get-Command Invoke-StartMenuPinning -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-StartMenuBinaryType' {
        It 'Should return Start1, Start2, or null' {
            $result = Get-StartMenuBinaryType
            if ($null -ne $result) {
                $result | Should -BeIn @('Start1', 'Start2')
            }
        }

        It 'Should complete without throwing' {
            { Get-StartMenuBinaryType } | Should -Not -Throw
        }

        It 'Should be deterministic' {
            $first = Get-StartMenuBinaryType
            $second = Get-StartMenuBinaryType
            $first | Should -Be $second
        }
    }

    Context 'Get-CurrentUserStartMenuBinary' {
        It 'Should return path or null' {
            $result = Get-CurrentUserStartMenuBinary
            if ($null -ne $result) {
                $result | Should -BeOfType [string]
                $result | Should -Match '\.bin$'
            }
        }

        It 'Should complete without throwing' {
            { Get-CurrentUserStartMenuBinary } | Should -Not -Throw
        }

        It 'Should return path in LocalAppData' {
            $result = Get-CurrentUserStartMenuBinary
            if ($null -ne $result) {
                $result | Should -Match 'AppData\\Local'
            }
        }
    }

    Context 'Get-DefaultProfileStartMenuBinary' {
        It 'Should return a string path' {
            $result = Get-DefaultProfileStartMenuBinary
            $result | Should -BeOfType [string]
        }

        It 'Should return path in Default profile' {
            $result = Get-DefaultProfileStartMenuBinary
            $result | Should -Match 'Users\\Default'
        }

        It 'Should return .bin file path' {
            $result = Get-DefaultProfileStartMenuBinary
            $result | Should -Match '\.bin$'
        }
    }

    Context 'Backup-StartMenuLayout' {
        It 'Should have BackupName parameter' {
            $cmd = Get-Command Backup-StartMenuLayout
            $cmd.Parameters.ContainsKey('BackupName') | Should -BeTrue
        }

        It 'Should return path or false' {
            # Only test if start menu binary exists
            $binaryPath = Get-CurrentUserStartMenuBinary
            if ($binaryPath -and (Test-Path $binaryPath)) {
                $result = Backup-StartMenuLayout -BackupName "PesterTest_$(Get-Random)"
                if ($result) {
                    $result | Should -BeOfType [string]
                    # Clean up
                    Remove-Item $result -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }

    Context 'Publish-StartMenuLayoutToDefault' {
        It 'Should have SourcePath parameter' {
            $cmd = Get-Command Publish-StartMenuLayoutToDefault
            $cmd.Parameters.ContainsKey('SourcePath') | Should -BeTrue
        }

        It 'Should have UseCurrentUser parameter' {
            $cmd = Get-Command Publish-StartMenuLayoutToDefault
            $cmd.Parameters.ContainsKey('UseCurrentUser') | Should -BeTrue
        }

        It 'Should return false for non-existent source' {
            $result = Publish-StartMenuLayoutToDefault -SourcePath 'C:\NonExistent\File.bin'
            $result | Should -BeFalse
        }
    }

    Context 'Get-BackedUpLayouts' {
        It 'Should return array' {
            $result = Get-BackedUpLayouts
            # Result should be array (possibly empty)
            $result -is [array] -or $result.Count -ge 0 | Should -BeTrue
        }

        It 'Should return an empty array when the backup directory is empty' {
            InModuleScope StartMenuPinning {
                $originalBackupDirectory = $script:BackupDirectory
                $tempBackupDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "WinForgeStartMenuPinning_$([guid]::NewGuid())"
                New-Item -Path $tempBackupDirectory -ItemType Directory -Force | Out-Null

                try {
                    $script:BackupDirectory = $tempBackupDirectory
                    $result = Get-BackedUpLayouts

                    $result -is [array] | Should -BeTrue
                    $result.Count | Should -Be 0
                } finally {
                    $script:BackupDirectory = $originalBackupDirectory
                    Remove-Item -Path $tempBackupDirectory -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }

        It 'Should return an array when the backup directory has one file' {
            InModuleScope StartMenuPinning {
                $originalBackupDirectory = $script:BackupDirectory
                $tempBackupDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "WinForgeStartMenuPinning_$([guid]::NewGuid())"
                New-Item -Path $tempBackupDirectory -ItemType Directory -Force | Out-Null
                New-Item -Path (Join-Path $tempBackupDirectory 'Single.bin') -ItemType File -Force | Out-Null

                try {
                    $script:BackupDirectory = $tempBackupDirectory
                    $result = Get-BackedUpLayouts

                    $result -is [array] | Should -BeTrue
                    $result.Count | Should -Be 1
                    $result[0].Name | Should -Be 'Single.bin'
                } finally {
                    $script:BackupDirectory = $originalBackupDirectory
                    Remove-Item -Path $tempBackupDirectory -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }

        It 'Should complete without throwing' {
            { Get-BackedUpLayouts } | Should -Not -Throw
        }
    }

    Context 'Invoke-StartMenuPinning' {
        It 'Should have BackupName parameter' {
            $cmd = Get-Command Invoke-StartMenuPinning
            $cmd.Parameters.ContainsKey('BackupName') | Should -BeTrue
        }

        It 'Should have SkipBackup parameter' {
            $cmd = Get-Command Invoke-StartMenuPinning
            $cmd.Parameters.ContainsKey('SkipBackup') | Should -BeTrue
        }

        It 'Should have ApplyToCurrentUser parameter' {
            $cmd = Get-Command Invoke-StartMenuPinning
            $cmd.Parameters.ContainsKey('ApplyToCurrentUser') | Should -BeTrue
        }
    }
}

Describe 'StartMenuPinning Integration Tests' {
    Context 'Binary Detection' {
        It 'Binary type should match current user path' {
            $binaryType = Get-StartMenuBinaryType
            $currentPath = Get-CurrentUserStartMenuBinary

            if ($binaryType -and $currentPath) {
                if ($binaryType -eq 'Start2') {
                    $currentPath | Should -Match 'start2\.bin'
                } else {
                    $currentPath | Should -Match 'start\.bin'
                }
            }
        }

        It 'Default profile path should match binary type' {
            $binaryType = Get-StartMenuBinaryType
            $defaultPath = Get-DefaultProfileStartMenuBinary

            # Default path should contain the expected binary name
            if ($binaryType -eq 'Start2' -or $binaryType -eq $null) {
                $defaultPath | Should -Match 'start2\.bin'
            }
        }
    }

    Context 'Windows 11 Compatibility' {
        It 'Should detect Windows 11 start menu structure' {
            $binaryType = Get-StartMenuBinaryType
            # On Windows 11, we should find Start1 or Start2
            # On other systems, it might be null
            if ($binaryType) {
                $binaryType | Should -BeIn @('Start1', 'Start2')
            }
        }
    }
}
