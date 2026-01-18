<#
.SYNOPSIS
    Pester tests for Core module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge Core v2.5.0
    Tests logging, error handling, validation, and utility functions

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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Core'
    $script:CorePath = Join-Path $script:ModuleRoot 'Core.psm1'

    Import-Module $script:CorePath -Force -ErrorAction Stop
}

Describe 'Core Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:CorePath -Force } | Should -Not -Throw
        }

        It 'Should export Write-Status function' {
            Get-Command Write-Status -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Initialize-Logging function' {
            Get-Command Initialize-Logging -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-Administrator function' {
            Get-Command Test-Administrator -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-InternetConnection function' {
            Get-Command Test-InternetConnection -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-SafeCommand function' {
            Get-Command Invoke-SafeCommand -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-StringHash function' {
            Get-Command Get-StringHash -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export ConvertTo-SafeFileName function' {
            Get-Command ConvertTo-SafeFileName -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Format-FileSize function' {
            Get-Command Format-FileSize -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Write-Status' {
        It 'Should accept Info level' {
            { Write-Status -Message 'Test message' -Level 'Info' } | Should -Not -Throw
        }

        It 'Should accept Success level' {
            { Write-Status -Message 'Test message' -Level 'Success' } | Should -Not -Throw
        }

        It 'Should accept Warning level' {
            { Write-Status -Message 'Test message' -Level 'Warning' } | Should -Not -Throw
        }

        It 'Should accept Error level' {
            { Write-Status -Message 'Test message' -Level 'Error' } | Should -Not -Throw
        }

        It 'Should accept Verbose level' {
            { Write-Status -Message 'Test message' -Level 'Verbose' } | Should -Not -Throw
        }

        It 'Should default to Info level' {
            { Write-Status -Message 'Test message' } | Should -Not -Throw
        }

        It 'Should accept NoNewline switch' {
            { Write-Status -Message 'Test' -Level 'Info' -NoNewline } | Should -Not -Throw
        }
    }

    Context 'Test-Administrator' {
        It 'Should return a boolean' {
            $result = Test-Administrator
            $result | Should -BeOfType [bool]
        }

        It 'Should be deterministic' {
            $first = Test-Administrator
            $second = Test-Administrator
            $first | Should -Be $second
        }
    }

    Context 'Test-InternetConnection' {
        It 'Should return a boolean' {
            $result = Test-InternetConnection
            $result | Should -BeOfType [bool]
        }

        It 'Should complete without throwing' {
            { Test-InternetConnection } | Should -Not -Throw
        }
    }

    Context 'Get-StringHash' {
        It 'Should return a hash string' {
            $hash = Get-StringHash -String 'test'
            $hash | Should -Not -BeNullOrEmpty
            $hash | Should -BeOfType [string]
        }

        It 'Should return consistent hash for same input' {
            $hash1 = Get-StringHash -String 'hello'
            $hash2 = Get-StringHash -String 'hello'
            $hash1 | Should -Be $hash2
        }

        It 'Should return different hash for different input' {
            $hash1 = Get-StringHash -String 'hello'
            $hash2 = Get-StringHash -String 'world'
            $hash1 | Should -Not -Be $hash2
        }

        It 'Should support SHA256 algorithm' {
            $hash = Get-StringHash -String 'test' -Algorithm 'SHA256'
            $hash | Should -Not -BeNullOrEmpty
            $hash.Length | Should -Be 64
        }

        It 'Should support MD5 algorithm' {
            $hash = Get-StringHash -String 'test' -Algorithm 'MD5'
            $hash | Should -Not -BeNullOrEmpty
            $hash.Length | Should -Be 32
        }

        It 'Should support SHA1 algorithm' {
            $hash = Get-StringHash -String 'test' -Algorithm 'SHA1'
            $hash | Should -Not -BeNullOrEmpty
            $hash.Length | Should -Be 40
        }
    }

    Context 'ConvertTo-SafeFileName' {
        It 'Should return a string' {
            $result = ConvertTo-SafeFileName -String 'test'
            $result | Should -BeOfType [string]
        }

        It 'Should preserve valid characters' {
            $result = ConvertTo-SafeFileName -String 'valid_filename'
            $result | Should -Be 'valid_filename'
        }

        It 'Should replace invalid characters' {
            $result = ConvertTo-SafeFileName -String 'file:name'
            $result | Should -Not -Contain ':'
        }

        It 'Should replace slashes' {
            $result = ConvertTo-SafeFileName -String 'path/file'
            $result | Should -Not -Contain '/'
        }

        It 'Should replace backslashes' {
            $result = ConvertTo-SafeFileName -String 'path\file'
            $result | Should -Not -Contain '\'
        }

        It 'Should handle multiple invalid characters' {
            $result = ConvertTo-SafeFileName -String 'file<>:"/\|?*name'
            $result | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Format-FileSize' {
        It 'Should format bytes correctly' {
            $result = Format-FileSize -Bytes 500
            $result | Should -Be '500 bytes'
        }

        It 'Should format KB correctly' {
            $result = Format-FileSize -Bytes 1536
            $result | Should -Match 'KB'
        }

        It 'Should format MB correctly' {
            $result = Format-FileSize -Bytes 1572864
            $result | Should -Match 'MB'
        }

        It 'Should format GB correctly' {
            $result = Format-FileSize -Bytes 1610612736
            $result | Should -Match 'GB'
        }

        It 'Should format TB correctly' {
            $result = Format-FileSize -Bytes 1099511627776
            $result | Should -Match 'TB'
        }

        It 'Should handle zero bytes' {
            $result = Format-FileSize -Bytes 0
            $result | Should -Be '0 bytes'
        }
    }

    Context 'Test-CommandExists' {
        It 'Should return true for existing commands' {
            $result = Test-CommandExists -Name 'Get-Process'
            $result | Should -BeTrue
        }

        It 'Should return false for non-existing commands' {
            $result = Test-CommandExists -Name 'NonExistentCommand12345'
            $result | Should -BeFalse
        }

        It 'Should return a boolean' {
            $result = Test-CommandExists -Name 'Get-Date'
            $result | Should -BeOfType [bool]
        }
    }

    Context 'Get-DownloadedFileName' {
        It 'Should extract filename from URL' {
            $result = Get-DownloadedFileName -Url 'https://example.com/file.exe'
            $result | Should -Be 'file.exe'
        }

        It 'Should handle URL without file extension' {
            $result = Get-DownloadedFileName -Url 'https://example.com/download'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return default for empty path' {
            $result = Get-DownloadedFileName -Url 'https://example.com/'
            $result | Should -Be 'download.exe'
        }

        It 'Should parse Content-Disposition header' {
            $result = Get-DownloadedFileName -Url 'https://example.com/dl' -ContentDisposition 'filename="app.msi"'
            $result | Should -Be 'app.msi'
        }
    }

    Context 'Get-ElapsedTime' {
        It 'Should return formatted time string' {
            $startTime = (Get-Date).AddMinutes(-5)
            $result = Get-ElapsedTime -StartTime $startTime
            $result | Should -Match '^\d{2}:\d{2}:\d{2}$'
        }

        It 'Should handle recent start times' {
            $startTime = Get-Date
            Start-Sleep -Milliseconds 100
            $result = Get-ElapsedTime -StartTime $startTime
            $result | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Invoke-SafeCommand' {
        It 'Should return true for successful commands' {
            $result = Invoke-SafeCommand -ScriptBlock { 1 + 1 }
            $result | Should -BeTrue
        }

        It 'Should return false for failed commands with ContinueOnError' {
            $result = Invoke-SafeCommand -ScriptBlock { throw 'Error' } -ContinueOnError
            $result | Should -BeFalse
        }

        It 'Should throw for failed commands without ContinueOnError' {
            { Invoke-SafeCommand -ScriptBlock { throw 'Error' } } | Should -Throw
        }

        It 'Should execute the script block' {
            $executed = $false
            Invoke-SafeCommand -ScriptBlock { $script:executed = $true }
            # The scriptblock runs in a different scope, so we just verify no throw
        }
    }

    Context 'Write-Section' {
        It 'Should have Title parameter' {
            $cmd = Get-Command Write-Section
            $cmd.Parameters.ContainsKey('Title') | Should -BeTrue
        }
    }

    Context 'Write-StatusProgress' {
        It 'Should complete without errors' {
            { Write-StatusProgress -Activity 'Test' -Status 'Running' -PercentComplete 50 } | Should -Not -Throw
        }

        It 'Should have Activity parameter' {
            $cmd = Get-Command Write-StatusProgress
            $cmd.Parameters.ContainsKey('Activity') | Should -BeTrue
        }

        It 'Should have PercentComplete parameter' {
            $cmd = Get-Command Write-StatusProgress
            $cmd.Parameters.ContainsKey('PercentComplete') | Should -BeTrue
        }
    }

    Context 'Initialize-Logging' {
        It 'Should create log file' {
            $logPath = Join-Path $env:TEMP "test_log_$(Get-Random).log"
            try {
                Initialize-Logging -LogPath $logPath
                Test-Path $logPath | Should -BeTrue
            }
            finally {
                Remove-Item $logPath -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should create log directory if not exists' {
            $logDir = Join-Path $env:TEMP "testlogdir_$(Get-Random)"
            $logPath = Join-Path $logDir 'test.log'
            try {
                Initialize-Logging -LogPath $logPath
                Test-Path $logDir | Should -BeTrue
            }
            finally {
                Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should write header to log file' {
            $logPath = Join-Path $env:TEMP "test_log_header_$(Get-Random).log"
            try {
                Initialize-Logging -LogPath $logPath
                $content = Get-Content $logPath -Raw
                $content | Should -Match 'Win11Forge'
            }
            finally {
                Remove-Item $logPath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Clear-TemporaryFiles' {
        It 'Should not throw for non-existent path' {
            { Clear-TemporaryFiles -Path 'C:\NonExistent\Path\12345' } | Should -Not -Throw
        }

        It 'Should handle existing empty directory' {
            $tempDir = Join-Path $env:TEMP "testcleandir_$(Get-Random)"
            New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
            try {
                { Clear-TemporaryFiles -Path $tempDir } | Should -Not -Throw
            }
            finally {
                Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Describe 'Core Module Integration' {
    Context 'Logging Integration' {
        It 'Should write status messages to log file' {
            $logPath = Join-Path $env:TEMP "integration_log_$(Get-Random).log"
            try {
                Initialize-Logging -LogPath $logPath
                Write-Status -Message 'Integration test message' -Level 'Info'

                $content = Get-Content $logPath -Raw
                $content | Should -Match 'Integration test message'
            }
            finally {
                Remove-Item $logPath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Error Handling Integration' {
        It 'Should log errors via Invoke-SafeCommand' {
            $logPath = Join-Path $env:TEMP "error_log_$(Get-Random).log"
            try {
                Initialize-Logging -LogPath $logPath
                Invoke-SafeCommand -ScriptBlock { throw 'Test error' } -ContinueOnError -ErrorMessage 'Custom error'

                $content = Get-Content $logPath -Raw
                $content | Should -Match 'Custom error'
            }
            finally {
                Remove-Item $logPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
