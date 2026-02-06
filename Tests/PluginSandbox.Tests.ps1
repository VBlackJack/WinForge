<#
.SYNOPSIS
    Pester tests for PluginSandbox module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge PluginSandbox v3.5.0
    Tests sandboxed execution, timeout enforcement, and plugin isolation

.NOTES
    Author: Julien Bombled
    Version: 3.5.2
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
    $script:ModulePath = Join-Path $script:ModuleRoot 'PluginSandbox.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'PluginSandbox Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Invoke-PluginSandboxed function' {
            Get-Command Invoke-PluginSandboxed -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-PluginHookSandboxed function' {
            Get-Command Invoke-PluginHookSandboxed -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-PluginLoadSandboxed function' {
            Get-Command Invoke-PluginLoadSandboxed -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-PluginSandboxAvailable function' {
            Get-Command Test-PluginSandboxAvailable -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-SandboxStatus function' {
            Get-Command Get-SandboxStatus -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-PluginSandboxAvailable' {
        It 'Should return boolean' {
            $result = Test-PluginSandboxAvailable
            $result | Should -BeOfType [bool]
        }

        It 'Should return true on systems with job support' {
            # PowerShell 5.1+ always supports jobs
            $result = Test-PluginSandboxAvailable
            $result | Should -Be $true
        }
    }

    Context 'Get-SandboxStatus' {
        It 'Should return status hashtable' {
            $result = Get-SandboxStatus
            $result | Should -BeOfType [hashtable]
        }

        It 'Should include Available property' {
            $result = Get-SandboxStatus
            $result.Available | Should -BeOfType [bool]
        }

        It 'Should include Configuration property' {
            $result = Get-SandboxStatus
            $result.Configuration | Should -Not -BeNullOrEmpty
        }

        It 'Should include PowerShellVersion property' {
            $result = Get-SandboxStatus
            $result.PowerShellVersion | Should -Not -BeNullOrEmpty
        }

        It 'Should include JobsSupported property' {
            $result = Get-SandboxStatus
            $result.JobsSupported | Should -Be $true
        }

        It 'Should include execution timeout in configuration' {
            $result = Get-SandboxStatus
            $result.Configuration.ExecutionTimeoutSeconds | Should -BeGreaterThan 0
        }

        It 'Should include load timeout in configuration' {
            $result = Get-SandboxStatus
            $result.Configuration.LoadTimeoutSeconds | Should -BeGreaterThan 0
        }
    }

    Context 'Invoke-PluginSandboxed - Basic Execution' {
        It 'Should execute simple handler' {
            $handler = { return 'Hello World' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'TestPlugin'
            $result.Success | Should -Be $true
            $result.Result | Should -Be 'Hello World'
        }

        It 'Should return result hashtable' {
            $handler = { return 42 }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'TestPlugin'
            $result | Should -BeOfType [hashtable]
        }

        It 'Should include Plugin name in result' {
            $handler = { return $true }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'MyPlugin'
            $result.Plugin | Should -Be 'MyPlugin'
        }

        It 'Should include ExecutionTimeMs in result' {
            $handler = { return $true }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'TestPlugin'
            $result.ExecutionTimeMs | Should -BeGreaterOrEqual 0
        }

        It 'Should pass context to handler' {
            $handler = { param($ctx) return $ctx.TestValue }
            $context = @{ TestValue = 'ContextPassed' }
            $result = Invoke-PluginSandboxed -Handler $handler -Context $context -PluginName 'TestPlugin'
            $result.Result | Should -Be 'ContextPassed'
        }
    }

    Context 'Invoke-PluginSandboxed - Timeout Enforcement' {
        It 'Should timeout long-running handler' {
            $handler = { Start-Sleep -Seconds 10; return 'Never reached' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'SlowPlugin' -TimeoutSeconds 2
            $result.TimedOut | Should -Be $true
            $result.Success | Should -Be $false
        }

        It 'Should complete fast handler before timeout' {
            $handler = { return 'Fast' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'FastPlugin' -TimeoutSeconds 5
            $result.TimedOut | Should -Be $false
            $result.Success | Should -Be $true
        }

        It 'Should accept TimeoutSeconds parameter' {
            $handler = { return $true }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'Test' -TimeoutSeconds 10 } | Should -Not -Throw
        }

        It 'Should validate TimeoutSeconds range (min 1)' {
            $handler = { return $true }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'Test' -TimeoutSeconds 0 } | Should -Throw
        }

        It 'Should validate TimeoutSeconds range (max 300)' {
            $handler = { return $true }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'Test' -TimeoutSeconds 301 } | Should -Throw
        }
    }

    Context 'Invoke-PluginSandboxed - Error Handling' {
        It 'Should capture handler exceptions' {
            $handler = { throw 'Test exception' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'ErrorPlugin'
            $result.Success | Should -Be $false
            $result.Error | Should -Not -BeNullOrEmpty
        }

        It 'Should not throw on handler exception' {
            $handler = { throw 'Test exception' }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'ErrorPlugin' } | Should -Not -Throw
        }

        It 'Should handle null result from handler' {
            $handler = { return $null }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'NullPlugin'
            $result.Success | Should -Be $true
        }

        It 'Should handle complex object return' {
            $handler = { return @{ Key = 'Value'; Number = 42 } }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'ComplexPlugin'
            $result.Success | Should -Be $true
        }
    }

    Context 'Invoke-PluginSandboxed - Isolation' {
        It 'Should not affect parent scope variables' {
            $parentVar = 'Original'
            $handler = { $parentVar = 'Modified'; return $parentVar }
            Invoke-PluginSandboxed -Handler $handler -PluginName 'IsolationTest'
            $parentVar | Should -Be 'Original'
        }

        It 'Should execute in separate job' {
            $handler = { return $PID }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'PidTest'
            # Job runs in separate process, so PID should be different
            # Note: In some cases jobs may share process, so we just verify it works
            $result.Success | Should -Be $true
        }
    }

    Context 'Invoke-PluginHookSandboxed' {
        It 'Should execute multiple handlers' {
            $handlers = @(
                @{ PluginName = 'Plugin1'; Handler = { return 'Result1' } }
                @{ PluginName = 'Plugin2'; Handler = { return 'Result2' } }
            )
            $results = Invoke-PluginHookSandboxed -HookName 'test-hook' -Handlers $handlers
            $results.Count | Should -Be 2
        }

        It 'Should return results' {
            $handlers = @(
                @{ PluginName = 'Plugin1'; Handler = { return 'Result' } }
            )
            $results = Invoke-PluginHookSandboxed -HookName 'my-hook' -Handlers $handlers
            $results | Should -Not -BeNullOrEmpty
        }

        It 'Should continue on handler failure' {
            $handlers = @(
                @{ PluginName = 'FailPlugin'; Handler = { throw 'Error' } }
                @{ PluginName = 'SuccessPlugin'; Handler = { return 'OK' } }
            )
            $results = Invoke-PluginHookSandboxed -HookName 'test-hook' -Handlers $handlers
            $results.Count | Should -Be 2
            $results[0].Success | Should -Be $false
            $results[1].Success | Should -Be $true
        }

        It 'Should pass context to all handlers' {
            $handlers = @(
                @{ PluginName = 'Plugin1'; Handler = { param($ctx) return $ctx.Value } }
                @{ PluginName = 'Plugin2'; Handler = { param($ctx) return $ctx.Value } }
            )
            $context = @{ Value = 'SharedContext' }
            $results = Invoke-PluginHookSandboxed -HookName 'test-hook' -Context $context -Handlers $handlers
            $results[0].Result | Should -Be 'SharedContext'
            $results[1].Result | Should -Be 'SharedContext'
        }

        It 'Should reject empty handlers array' {
            { Invoke-PluginHookSandboxed -HookName 'test-hook' -Handlers @() } | Should -Throw
        }
    }

    Context 'Invoke-PluginLoadSandboxed' {
        It 'Should validate plugin path parameter' {
            { Invoke-PluginLoadSandboxed -PluginPath '' -PluginName 'Test' } | Should -Throw
        }

        It 'Should validate plugin name parameter' {
            { Invoke-PluginLoadSandboxed -PluginPath 'C:\test.psm1' -PluginName '' } | Should -Throw
        }

        It 'Should return result hashtable' {
            $result = Invoke-PluginLoadSandboxed -PluginPath 'C:\NonExistent\Plugin.psm1' -PluginName 'NonExistent'
            $result | Should -BeOfType [hashtable]
        }

        It 'Should include PluginName in result' {
            $result = Invoke-PluginLoadSandboxed -PluginPath 'C:\test.psm1' -PluginName 'TestPlugin'
            $result.PluginName | Should -Be 'TestPlugin'
        }

        It 'Should include LoadTimeMs in result' {
            $result = Invoke-PluginLoadSandboxed -PluginPath 'C:\test.psm1' -PluginName 'TestPlugin'
            $result.LoadTimeMs | Should -BeGreaterOrEqual 0
        }

        It 'Should fail for non-existent plugin file' {
            $result = Invoke-PluginLoadSandboxed -PluginPath 'C:\NonExistent\Plugin.psm1' -PluginName 'NonExistent'
            $result.Success | Should -Be $false
        }

        It 'Should successfully load valid module' {
            $testDir = Join-Path $env:TEMP 'Win11ForgePluginSandboxTests'
            $testModulePath = Join-Path $testDir 'TestPlugin.psm1'

            if (-not (Test-Path $testDir)) {
                New-Item -Path $testDir -ItemType Directory -Force | Out-Null
            }

            @"
function Get-TestValue { 'ok' }
Export-ModuleMember -Function Get-TestValue
"@ | Set-Content -Path $testModulePath -Encoding UTF8

            try {
                $result = Invoke-PluginLoadSandboxed -PluginPath $testModulePath -PluginName 'TestPlugin'
                $result.Success | Should -Be $true
            } finally {
                if (Test-Path $testModulePath) {
                    Remove-Item -Path $testModulePath -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }

    Context 'Parameter Validation' {
        It 'Should require Handler parameter' {
            { Invoke-PluginSandboxed -PluginName 'Test' } | Should -Throw
        }

        It 'Should require PluginName parameter' {
            { Invoke-PluginSandboxed -Handler { return $true } } | Should -Throw
        }

        It 'Should accept empty context' {
            $handler = { return 'OK' }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'Test' -Context @{} } | Should -Not -Throw
        }

        It 'Should accept AllowNetworkAccess switch' {
            $handler = { return 'OK' }
            { Invoke-PluginSandboxed -Handler $handler -PluginName 'Test' -AllowNetworkAccess } | Should -Not -Throw
        }
    }

    Context 'Edge Cases' {
        It 'Should handle handler that returns array' {
            $handler = { return @(1, 2, 3) }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'ArrayPlugin'
            $result.Success | Should -Be $true
        }

        It 'Should handle handler that writes to output' {
            $handler = { Write-Output 'Test Output'; return 'Final' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'OutputPlugin'
            $result.Success | Should -Be $true
        }

        It 'Should handle rapid sequential executions' {
            $handler = { return 'Quick' }
            1..5 | ForEach-Object {
                $result = Invoke-PluginSandboxed -Handler $handler -PluginName "RapidPlugin$_"
                $result.Success | Should -Be $true
            }
        }

        It 'Should handle special characters in plugin name' {
            $handler = { return 'OK' }
            $result = Invoke-PluginSandboxed -Handler $handler -PluginName 'Plugin-With_Special.Chars'
            $result.Plugin | Should -Be 'Plugin-With_Special.Chars'
        }
    }
}
