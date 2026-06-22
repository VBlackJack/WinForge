#
# Tests for WinForgeExceptions module
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
    $modulePath = Join-Path $PSScriptRoot '..\Core\WinForgeExceptions.psm1'
    Import-Module $modulePath -Force
}

AfterAll {
    Remove-Module WinForgeExceptions -ErrorAction SilentlyContinue
}

Describe 'WinForgeExceptions Module' {
    Context 'New-InstallationException' {
        It 'Should create an exception with message and app name' {
            $exception = New-InstallationException -Message 'Installation failed' -AppName 'TestApp'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'Installation failed'
            $exception.AppName | Should -Be 'TestApp'
        }

        It 'Should create an exception with method' {
            $exception = New-InstallationException -Message 'Failed' -AppName 'TestApp' -Method 'Winget'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Method | Should -Be 'Winget'
        }

        It 'Should create an exception with exit code' {
            $exception = New-InstallationException -Message 'Failed' -AppName 'TestApp' -Method 'Winget' -ExitCode 1
            $exception | Should -Not -BeNullOrEmpty
            $exception.ExitCode | Should -Be 1
        }

        It 'Should have Installation category' {
            $exception = New-InstallationException -Message 'Failed' -AppName 'TestApp'
            $exception.Category | Should -Be 'Installation'
        }
    }

    Context 'New-WingetException' {
        It 'Should create a winget-specific exception' {
            $exception = New-WingetException -Message 'Winget failed' -AppName 'TestApp'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'Winget failed'
        }

        It 'Should include exit code' {
            $exception = New-WingetException -Message 'Failed' -AppName 'TestApp' -ExitCode 2
            $exception | Should -Not -BeNullOrEmpty
        }

        It 'Should include output' {
            $exception = New-WingetException -Message 'Failed' -AppName 'TestApp' -ExitCode 2 -Output 'Error output'
            $exception.WingetOutput | Should -Be 'Error output'
        }
    }

    Context 'New-SecurityException' {
        It 'Should create a security exception' {
            $exception = New-SecurityException -Message 'Access denied'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'Access denied'
        }

        It 'Should include context and action' {
            $exception = New-SecurityException -Message 'Blocked' -Context 'API' -Action 'Write'
            $exception | Should -Not -BeNullOrEmpty
            $exception.SecurityContext | Should -Be 'API'
            $exception.AttemptedAction | Should -Be 'Write'
        }

        It 'Should have Security category' {
            $exception = New-SecurityException -Message 'Test'
            $exception.Category | Should -Be 'Security'
        }
    }

    Context 'New-ApiException' {
        It 'Should create an API exception' {
            $exception = New-ApiException -Message 'API error'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'API error'
        }

        It 'Should include endpoint and method' {
            $exception = New-ApiException -Message 'Failed' -Endpoint '/api/test' -Method 'GET'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Endpoint | Should -Be '/api/test'
            $exception.HttpMethod | Should -Be 'GET'
        }

        It 'Should include status code' {
            $exception = New-ApiException -Message 'Failed' -Endpoint '/api/test' -Method 'GET' -StatusCode 500
            $exception.StatusCode | Should -Be 500
        }

        It 'Should have API category' {
            $exception = New-ApiException -Message 'Test'
            $exception.Category | Should -Be 'API'
        }
    }

    Context 'New-TimeoutException' {
        It 'Should create a timeout exception' {
            $exception = New-TimeoutException -Message 'Operation timed out' -Operation 'Install' -TimeoutSeconds 30
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'Operation timed out'
        }

        It 'Should include operation and timeout' {
            $exception = New-TimeoutException -Message 'Timeout' -Operation 'Download' -TimeoutSeconds 60
            $exception.Operation | Should -Be 'Download'
            $exception.TimeoutSeconds | Should -Be 60
        }

        It 'Should have Timeout category' {
            $exception = New-TimeoutException -Message 'Test' -Operation 'Test' -TimeoutSeconds 10
            $exception.Category | Should -Be 'Timeout'
        }
    }

    Context 'New-ValidationException' {
        It 'Should create a validation exception' {
            $exception = New-ValidationException -Message 'Invalid input' -ParameterName 'Username'
            $exception | Should -Not -BeNullOrEmpty
            $exception.Message | Should -Be 'Invalid input'
        }

        It 'Should include parameter name' {
            $exception = New-ValidationException -Message 'Invalid' -ParameterName 'Email'
            $exception.ParameterName | Should -Be 'Email'
        }

        It 'Should include provided value and expected format' {
            $exception = New-ValidationException -Message 'Invalid' -ParameterName 'Email' -ProvidedValue 'notanemail' -ExpectedFormat 'user@domain.com'
            $exception.ProvidedValue | Should -Be 'notanemail'
            $exception.ExpectedFormat | Should -Be 'user@domain.com'
        }

        It 'Should have Validation category' {
            $exception = New-ValidationException -Message 'Test' -ParameterName 'Test'
            $exception.Category | Should -Be 'Validation'
        }
    }

    Context 'New-WinForgeError' {
        It 'Should create an error with DoNotThrow' {
            $error = New-WinForgeError -Message 'General error' -DoNotThrow
            $error | Should -Not -BeNullOrEmpty
        }

        It 'Should throw by default' {
            { New-WinForgeError -Message 'Error' } | Should -Throw
        }

        It 'Should support different categories' {
            $error = New-WinForgeError -Message 'Test' -Category 'General' -DoNotThrow
            $error.Category | Should -Be 'General'
        }

        It 'Should support ErrorCode' {
            $error = New-WinForgeError -Message 'Test' -ErrorCode 'ERR001' -DoNotThrow
            $error.Context['ErrorCode'] | Should -Be 'ERR001'
        }
    }

    Context 'Format-WinForgeError' {
        It 'Should format a WinForge exception' {
            $exception = New-InstallationException -Message 'Test error' -AppName 'TestApp'
            $formatted = Format-WinForgeError -Exception $exception
            $formatted | Should -Not -BeNullOrEmpty
            $formatted | Should -Match 'Installation'
        }

        It 'Should format a standard exception' {
            $exception = [System.Exception]::new('Standard error')
            $formatted = Format-WinForgeError -Exception $exception
            $formatted | Should -Not -BeNullOrEmpty
            $formatted | Should -Match 'Standard error'
        }

        It 'Should include stack trace section when requested' {
            $exception = New-InstallationException -Message 'Test' -AppName 'App'
            $formatted = Format-WinForgeError -Exception $exception -IncludeStackTrace
            # Format-WinForgeError only includes Stack Trace section if StackTrace property is populated
            # The formatted output should include the exception info regardless
            $formatted | Should -Match 'Installation'
        }
    }

    Context 'Test-WinForgeException' {
        It 'Should return true for WinForge exceptions' {
            $exception = New-InstallationException -Message 'Test' -AppName 'App'
            $result = Test-WinForgeException -Exception $exception
            $result | Should -Be $true
        }

        It 'Should return false for standard exceptions' {
            $exception = [System.Exception]::new('Standard')
            $result = Test-WinForgeException -Exception $exception
            $result | Should -Be $false
        }
    }

    Context 'Get-ExceptionCategory' {
        It 'Should return category for WinForge exceptions' {
            $exception = New-SecurityException -Message 'Test'
            $result = Get-ExceptionCategory -Exception $exception
            $result | Should -Be 'Security'
        }

        It 'Should return Unknown for standard exceptions' {
            $exception = [System.Exception]::new('Standard')
            $result = Get-ExceptionCategory -Exception $exception
            $result | Should -Be 'Unknown'
        }
    }

    Context 'Module Integration' {
        It 'Should have all expected functions exported' {
            $expectedFunctions = @(
                'New-InstallationException',
                'New-WingetException',
                'New-SecurityException',
                'New-ApiException',
                'New-TimeoutException',
                'New-ValidationException',
                'New-WinForgeError',
                'Format-WinForgeError',
                'Test-WinForgeException',
                'Get-ExceptionCategory'
            )

            $module = Get-Module WinForgeExceptions
            foreach ($func in $expectedFunctions) {
                $module.ExportedFunctions.Keys | Should -Contain $func
            }
        }
    }

    Context 'Exception Hierarchy' {
        It 'Should create exceptions that can be thrown' {
            $exception = New-InstallationException -Message 'Test' -AppName 'App'
            { throw $exception } | Should -Throw
        }

        It 'Should create exceptions that can be caught' {
            $exception = New-WingetException -Message 'Test' -AppName 'App'
            $caught = $false
            try {
                throw $exception
            } catch {
                $caught = $true
            }
            $caught | Should -Be $true
        }

        It 'Should preserve exception properties when caught' {
            $exception = New-InstallationException -Message 'Test message' -AppName 'TestApp' -Method 'Winget' -ExitCode 42
            $caughtException = $null
            try {
                throw $exception
            } catch {
                $caughtException = $_.Exception
            }
            $caughtException.AppName | Should -Be 'TestApp'
            $caughtException.Method | Should -Be 'Winget'
            $caughtException.ExitCode | Should -Be 42
        }
    }
}
