<#
.SYNOPSIS
    Pester tests for RestApiServer module

.DESCRIPTION
    Unit tests for Win11Forge RestApiServer v3.7.2
    Tests server lifecycle, endpoint registration, configuration,
    CSRF protection, rate limiting, auth blocking, and handler security

.NOTES
    Author: Julien Bombled
    Version: 3.6.8
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
    $script:ModulePath = Join-Path $script:ModuleRoot 'RestApiServer.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'RestApiServer Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-ApiConfig function' {
            Get-Command Get-ApiConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-ApiEndpoint function' {
            Get-Command Register-ApiEndpoint -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Unregister-ApiEndpoint function' {
            Get-Command Unregister-ApiEndpoint -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RegisteredEndpoints function' {
            Get-Command Get-RegisteredEndpoints -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApiServerStatus function' {
            Get-Command Get-ApiServerStatus -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export CSRF functions' {
            Get-Command New-CsrfToken -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            Get-Command Test-CsrfToken -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            Get-Command Clear-CsrfTokens -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export rate limit functions' {
            Get-Command Test-RateLimit -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            Get-Command Test-ApiKeyRateLimit -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            Get-Command Clear-RateLimitState -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export auth blocking functions' {
            Get-Command Test-FailedAuthBlock -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
            Get-Command Add-FailedAuthAttempt -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ApiConfig' {
        It 'Should return configuration hashtable' {
            $config = Get-ApiConfig
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have Port property' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'Port'
        }

        It 'Should have Host property' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'Host'
        }

        It 'Should default to localhost' {
            $config = Get-ApiConfig
            $config.Host | Should -Be 'localhost'
        }

        It 'Should default to port 5170' {
            $config = Get-ApiConfig
            $config.Port | Should -Be 5170
        }

        It 'Should have CSRF configuration' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'CsrfEnabled'
            $config.Keys | Should -Contain 'CsrfTokenTtlMinutes'
        }

        It 'Should have rate limit configuration' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'RateLimitEnabled'
            $config.Keys | Should -Contain 'MaxRequestsPerMinute'
            $config.Keys | Should -Contain 'MaxRequestsPerHour'
        }

        It 'Should have authentication configuration' {
            $config = Get-ApiConfig
            $config.Keys | Should -Contain 'RequireAuthentication'
            $config.Keys | Should -Contain 'MaxFailedAuthPerHour'
            $config.Keys | Should -Contain 'BlockDurationMinutes'
        }
    }

    Context 'Register-ApiEndpoint' {
        It 'Should register endpoint without errors' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'GET' -Handler { return @{} } } | Should -Not -Throw
        }

        It 'Should register POST endpoint' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'POST' -Handler { return @{} } } | Should -Not -Throw
        }

        It 'Should accept Description parameter' {
            { Register-ApiEndpoint -Path '/api/test' -Method 'GET' -Handler { return @{} } -Description 'Test endpoint' } | Should -Not -Throw
        }

        It 'Should reject handler with Invoke-Expression' {
            { Register-ApiEndpoint -Path '/api/dangerous' -Method 'GET' -Handler { Invoke-Expression $args[0] } } | Should -Throw
        }

        It 'Should reject handler with Start-Process RunAs' {
            { Register-ApiEndpoint -Path '/api/dangerous' -Method 'GET' -Handler { Start-Process cmd -Verb RunAs } } | Should -Throw
        }

        It 'Should reject handler with Set-ExecutionPolicy' {
            { Register-ApiEndpoint -Path '/api/dangerous' -Method 'GET' -Handler { Set-ExecutionPolicy Unrestricted } } | Should -Throw
        }
    }

    Context 'Get-RegisteredEndpoints' {
        BeforeEach {
            Register-ApiEndpoint -Path '/api/test1' -Method 'GET' -Handler { return @{} }
        }

        It 'Should return array of endpoints' {
            $endpoints = Get-RegisteredEndpoints
            $endpoints | Should -Not -BeNullOrEmpty
        }

        It 'Should include registered endpoint' {
            $endpoints = Get-RegisteredEndpoints
            $endpoints.Path | Should -Contain '/api/test1'
        }
    }

    Context 'Unregister-ApiEndpoint' {
        It 'Should unregister without errors' {
            Register-ApiEndpoint -Path '/api/tounregister' -Method 'GET' -Handler { return @{} }
            { Unregister-ApiEndpoint -Path '/api/tounregister' -Method 'GET' } | Should -Not -Throw
        }
    }

    Context 'Get-ApiServerStatus' {
        It 'Should return status object' {
            $status = Get-ApiServerStatus
            $status | Should -Not -BeNullOrEmpty
        }

        It 'Should have Running property' {
            $status = Get-ApiServerStatus
            $status.PSObject.Properties.Name | Should -Contain 'Running'
        }

        It 'Should have Port property' {
            $status = Get-ApiServerStatus
            $status.PSObject.Properties.Name | Should -Contain 'Port'
        }

        It 'Should show not running when server is stopped' {
            $status = Get-ApiServerStatus
            $status.Running | Should -Be $false
        }
    }

    Context 'Async API Server Lifecycle' {
        BeforeEach {
            InModuleScope -ModuleName 'RestApiServer' {
                $script:ServerState.Listener = $null
                $script:ServerState.Running = $false
                $script:ServerState.StartTime = $null
                $script:ServerState.RequestCount = 0
                $script:ServerState.BackgroundJob = $null
            }
        }

        It 'Should start in async mode and set BackgroundJobState to Running' {
            InModuleScope -ModuleName 'RestApiServer' {
                Mock Start-Job {
                    [PSCustomObject]@{
                        Id = 4242
                        State = 'Running'
                        ChildJobs = @()
                    }
                }
                Mock Get-Job {
                    [PSCustomObject]@{
                        Id = 4242
                        State = 'Running'
                        ChildJobs = @()
                    }
                }
                Mock Start-Sleep { }
                Mock Write-Status { }

                $job = Start-ApiServer -Port 5188 -Async
                $status = Get-ApiServerStatus

                Should -Invoke -CommandName Start-Job -Times 1
                $job | Should -Not -BeNullOrEmpty
                $status.Running | Should -Be $true
                $status.BackgroundJobState | Should -Be 'Running'
            }
        }

        It 'Should mark server as not running when background job is completed' {
            InModuleScope -ModuleName 'RestApiServer' {
                $script:ServerState.Running = $true
                $script:ServerState.StartTime = Get-Date
                $script:ServerState.BackgroundJob = [PSCustomObject]@{
                    Id = 4243
                    State = 'Completed'
                    ChildJobs = @()
                }

                Mock Get-Job {
                    [PSCustomObject]@{
                        Id = 4243
                        State = 'Completed'
                        ChildJobs = @()
                    }
                }

                $status = Get-ApiServerStatus
                $status.BackgroundJobState | Should -Be 'Completed'
                $status.Running | Should -Be $false
            }
        }

        It 'Should throw and reset async state when background job fails at startup' {
            InModuleScope -ModuleName 'RestApiServer' {
                $failedJob = [PSCustomObject]@{
                    Id = 4244
                    State = 'Failed'
                    ChildJobs = @()
                }

                Mock Start-Job { $failedJob }
                Mock Get-Job { $failedJob }
                Mock Start-Sleep { }
                Mock Receive-Job { @('Mock async startup failure') }
                Mock Stop-Job { }
                Mock Remove-Job { }
                Mock Write-Status { }

                { Start-ApiServer -Port 5189 -Async } | Should -Throw

                $script:ServerState.Running | Should -Be $false
                $script:ServerState.BackgroundJob | Should -Be $null
                $script:ServerState.StartTime | Should -Be $null
            }
        }
    }

    Context 'New-CsrfToken' {
        BeforeEach {
            Clear-CsrfTokens
        }

        It 'Should generate a token string' {
            $token = New-CsrfToken -ApiKeyId 'test-key'
            $token | Should -Not -BeNullOrEmpty
            $token | Should -BeOfType [string]
        }

        It 'Should generate token with csrf_ prefix' {
            $token = New-CsrfToken -ApiKeyId 'test-key'
            $token | Should -BeLike 'csrf_*'
        }

        It 'Should generate unique tokens per call' {
            $token1 = New-CsrfToken -ApiKeyId 'test-key'
            $token2 = New-CsrfToken -ApiKeyId 'test-key'
            $token1 | Should -Not -Be $token2
        }

        It 'Should track token in status' {
            $null = New-CsrfToken -ApiKeyId 'test-key'
            $status = @(Get-CsrfTokenStatus)
            $status | Should -Not -BeNullOrEmpty
            $status.Count | Should -BeGreaterOrEqual 1
        }
    }

    Context 'Test-CsrfToken' {
        BeforeEach {
            Clear-CsrfTokens
        }

        It 'Should validate a valid token' {
            $token = New-CsrfToken -ApiKeyId 'test-key'
            $result = Test-CsrfToken -Token $token -ApiKeyId 'test-key'
            $result.Valid | Should -Be $true
        }

        It 'Should reject empty or whitespace token' {
            $result = Test-CsrfToken -Token ' ' -ApiKeyId 'test-key'
            $result.Valid | Should -Be $false
        }

        It 'Should reject unknown token' {
            $result = Test-CsrfToken -Token 'csrf_nonexistent' -ApiKeyId 'test-key'
            $result.Valid | Should -Be $false
        }

        It 'Should enforce single-use (replay protection)' {
            $token = New-CsrfToken -ApiKeyId 'test-key'
            # First use succeeds
            $result1 = Test-CsrfToken -Token $token -ApiKeyId 'test-key'
            $result1.Valid | Should -Be $true
            # Second use fails (token consumed)
            $result2 = Test-CsrfToken -Token $token -ApiKeyId 'test-key'
            $result2.Valid | Should -Be $false
        }

        It 'Should reject token with wrong API key' {
            $token = New-CsrfToken -ApiKeyId 'key-a'
            $result = Test-CsrfToken -Token $token -ApiKeyId 'key-b'
            $result.Valid | Should -Be $false
        }

        It 'Should detect expired tokens' {
            InModuleScope -ModuleName 'RestApiServer' {
                # Manually insert an expired token
                $script:CsrfTokens['csrf_expired_test'] = @{
                    CreatedAt  = (Get-Date).AddMinutes(-120)
                    ApiKeyId   = 'test-key'
                    TtlMinutes = 60
                }
            }
            $result = Test-CsrfToken -Token 'csrf_expired_test' -ApiKeyId 'test-key'
            $result.Valid | Should -Be $false
            $result.Expired | Should -Be $true
        }

        It 'Should allow all tokens when CSRF is disabled' {
            InModuleScope -ModuleName 'RestApiServer' {
                $originalCsrfEnabled = $script:ServerState.Config.CsrfEnabled
                $script:ServerState.Config.CsrfEnabled = $false
                try {
                    $result = Test-CsrfToken -Token 'anything' -ApiKeyId 'any-key'
                    $result.Valid | Should -Be $true
                } finally {
                    $script:ServerState.Config.CsrfEnabled = $originalCsrfEnabled
                }
            }
        }
    }

    Context 'Test-RateLimit' {
        BeforeEach {
            Clear-RateLimitState
        }

        It 'Should allow requests within limits' {
            $result = Test-RateLimit -ClientIp '10.0.0.1'
            $result.Allowed | Should -Be $true
        }

        It 'Should return request counts' {
            $result = Test-RateLimit -ClientIp '10.0.0.2'
            $result.RequestsInMinute | Should -BeGreaterOrEqual 1
            $result.RequestsInHour | Should -BeGreaterOrEqual 1
        }

        It 'Should track separate clients independently' {
            $result1 = Test-RateLimit -ClientIp '10.0.0.3'
            $result2 = Test-RateLimit -ClientIp '10.0.0.4'
            $result1.Allowed | Should -Be $true
            $result2.Allowed | Should -Be $true
        }

        It 'Should block when minute limit exceeded' {
            InModuleScope -ModuleName 'RestApiServer' {
                $config = Get-ApiConfig
                $now = Get-Date
                # Pre-fill request timestamps to reach minute limit
                $script:ServerState.RateLimitState['10.0.0.5'] = @{
                    RequestTimestamps = [System.Collections.Generic.List[datetime]]::new()
                    LastAccess        = $now
                }
                for ($i = 0; $i -lt $config.MaxRequestsPerMinute; $i++) {
                    $script:ServerState.RateLimitState['10.0.0.5'].RequestTimestamps.Add($now.AddSeconds(-30))
                }
            }
            $result = Test-RateLimit -ClientIp '10.0.0.5'
            $result.Allowed | Should -Be $false
            $result.RetryAfterSeconds | Should -BeGreaterThan 0
        }

        It 'Should allow all when rate limiting is disabled' {
            InModuleScope -ModuleName 'RestApiServer' {
                $originalRateLimitEnabled = $script:ServerState.Config.RateLimitEnabled
                $script:ServerState.Config.RateLimitEnabled = $false
                try {
                    $result = Test-RateLimit -ClientIp '10.0.0.6'
                    $result.Allowed | Should -Be $true
                } finally {
                    $script:ServerState.Config.RateLimitEnabled = $originalRateLimitEnabled
                }
            }
        }
    }

    Context 'Test-ApiKeyRateLimit' {
        BeforeEach {
            InModuleScope -ModuleName 'RestApiServer' {
                $script:ServerState.ApiKeyRateLimitState = @{}
            }
        }

        It 'Should allow requests within limits' {
            $result = Test-ApiKeyRateLimit -ApiKeyId 'test-api-key-1'
            $result.Allowed | Should -Be $true
        }

        It 'Should return request count' {
            $result = Test-ApiKeyRateLimit -ApiKeyId 'test-api-key-2'
            $result.RequestCount | Should -BeGreaterOrEqual 1
        }

        It 'Should block when hour limit exceeded' {
            InModuleScope -ModuleName 'RestApiServer' {
                $config = Get-ApiConfig
                $now = Get-Date
                # Pre-fill timestamps to reach hour limit
                $script:ServerState.ApiKeyRateLimitState['test-api-key-3'] = @{
                    RequestTimestamps = [System.Collections.Generic.List[datetime]]::new()
                    LastAccess        = $now
                }
                for ($i = 0; $i -lt $config.MaxRequestsPerHour; $i++) {
                    $script:ServerState.ApiKeyRateLimitState['test-api-key-3'].RequestTimestamps.Add($now.AddMinutes(-30))
                }
            }
            $result = Test-ApiKeyRateLimit -ApiKeyId 'test-api-key-3'
            $result.Allowed | Should -Be $false
            $result.RetryAfterSeconds | Should -BeGreaterThan 0
        }

        It 'Should allow all when rate limiting is disabled' {
            InModuleScope -ModuleName 'RestApiServer' {
                $originalRateLimitEnabled = $script:ServerState.Config.RateLimitEnabled
                $script:ServerState.Config.RateLimitEnabled = $false
                try {
                    $result = Test-ApiKeyRateLimit -ApiKeyId 'test-api-key-4'
                    $result.Allowed | Should -Be $true
                } finally {
                    $script:ServerState.Config.RateLimitEnabled = $originalRateLimitEnabled
                }
            }
        }
    }

    Context 'Add-FailedAuthAttempt and Test-FailedAuthBlock' {
        BeforeEach {
            InModuleScope -ModuleName 'RestApiServer' {
                $script:ServerState.FailedAuthState = @{}
            }
        }

        It 'Should not block IP with no failures' {
            $result = Test-FailedAuthBlock -ClientIp '192.168.1.1'
            $result.Blocked | Should -Be $false
            $result.FailCount | Should -Be 0
        }

        It 'Should record failed attempt' {
            Add-FailedAuthAttempt -ClientIp '192.168.1.2'
            InModuleScope -ModuleName 'RestApiServer' {
                $state = $script:ServerState.FailedAuthState['192.168.1.2']
                $state | Should -Not -BeNullOrEmpty
                $state.FailCount | Should -Be 1
            }
        }

        It 'Should not block after a few failures' {
            for ($i = 0; $i -lt 3; $i++) {
                Add-FailedAuthAttempt -ClientIp '192.168.1.3'
            }
            $result = Test-FailedAuthBlock -ClientIp '192.168.1.3'
            $result.Blocked | Should -Be $false
        }

        It 'Should block IP after exceeding failure threshold' {
            $config = Get-ApiConfig
            $maxFailed = if ($config.MaxFailedAuthPerHour) { $config.MaxFailedAuthPerHour } else { 10 }
            for ($i = 0; $i -lt $maxFailed; $i++) {
                Add-FailedAuthAttempt -ClientIp '192.168.1.4'
            }
            $result = Test-FailedAuthBlock -ClientIp '192.168.1.4'
            $result.Blocked | Should -Be $true
            $result.RetryAfterSeconds | Should -BeGreaterThan 0
            $result.FailCount | Should -BeGreaterOrEqual $maxFailed
        }

        It 'Should unblock IP after block duration expires' {
            InModuleScope -ModuleName 'RestApiServer' {
                # Simulate a past block that has already expired
                $script:ServerState.FailedAuthState['192.168.1.5'] = @{
                    FailCount     = 15
                    FirstFailTime = (Get-Date).AddHours(-2)
                    BlockedUntil  = (Get-Date).AddMinutes(-1)
                    LastAccess    = (Get-Date).AddHours(-1)
                }
            }
            $result = Test-FailedAuthBlock -ClientIp '192.168.1.5'
            $result.Blocked | Should -Be $false
        }

        It 'Should track separate IPs independently' {
            Add-FailedAuthAttempt -ClientIp '192.168.1.6'
            Add-FailedAuthAttempt -ClientIp '192.168.1.7'
            InModuleScope -ModuleName 'RestApiServer' {
                $state6 = $script:ServerState.FailedAuthState['192.168.1.6']
                $state7 = $script:ServerState.FailedAuthState['192.168.1.7']
                $state6.FailCount | Should -Be 1
                $state7.FailCount | Should -Be 1
            }
        }
    }

    Context 'Test-SafeHandlerScriptblock (via InModuleScope)' {
        It 'Should accept safe handler' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = { return @{ Status = 'OK' } }
                $result = Test-SafeHandlerScriptblock -Handler $handler
                $result | Should -Be $true
            }
        }

        It 'Should accept handler with Get-* commands' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = { Get-Date; Get-Process }
                $result = Test-SafeHandlerScriptblock -Handler $handler
                $result | Should -Be $true
            }
        }

        It 'Should reject handler with Invoke-Expression' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('Invoke-Expression "dir"')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with iex alias' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('iex $cmd')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with ExecutionContext access' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('$ExecutionContext.InvokeCommand.InvokeScript("dir")')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with Add-Type TypeDefinition' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('Add-Type -TypeDefinition "public class Foo {}"')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with System.Reflection' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('[System.Reflection.Assembly]::LoadFile("test.dll")')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with scriptblock::Create' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('[scriptblock]::Create("dir")')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with DownloadString' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('(New-Object Net.WebClient).DownloadString("http://example.com")')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with environment variable modification' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('$env:PATH = "C:\malicious"')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }

        It 'Should reject handler with recursive force delete' {
            InModuleScope -ModuleName 'RestApiServer' {
                $handler = [scriptblock]::Create('Remove-Item C:\Windows -Recurse -Force')
                { Test-SafeHandlerScriptblock -Handler $handler } | Should -Throw
            }
        }
    }

    Context 'Clear-CsrfTokens' {
        It 'Should clear all tokens' {
            $null = New-CsrfToken -ApiKeyId 'test-key'
            $null = New-CsrfToken -ApiKeyId 'test-key-2'
            Clear-CsrfTokens
            $status = @(Get-CsrfTokenStatus)
            $status.Count | Should -Be 0
        }
    }

    Context 'Clear-RateLimitState' {
        It 'Should clear all rate limit tracking' {
            $null = Test-RateLimit -ClientIp '10.0.0.100'
            Clear-RateLimitState
            $status = @(Get-RateLimitStatus)
            $status.Count | Should -Be 0
        }
    }

    Context 'Get-CsrfTokenStatus' {
        BeforeEach {
            Clear-CsrfTokens
        }

        It 'Should return empty array when no tokens exist' {
            $status = @(Get-CsrfTokenStatus)
            $status.Count | Should -Be 0
        }

        It 'Should return status for each active token' {
            $null = New-CsrfToken -ApiKeyId 'status-key-1'
            $null = New-CsrfToken -ApiKeyId 'status-key-2'
            $status = Get-CsrfTokenStatus
            $status.Count | Should -Be 2
        }
    }

    Context 'Get-RateLimitStatus' {
        BeforeEach {
            Clear-RateLimitState
        }

        It 'Should return empty array when no clients tracked' {
            $status = @(Get-RateLimitStatus)
            $status.Count | Should -Be 0
        }

        It 'Should return status for tracked clients' {
            $null = Test-RateLimit -ClientIp '10.0.0.200'
            $status = @(Get-RateLimitStatus)
            $status.Count | Should -BeGreaterOrEqual 1
        }
    }
}
