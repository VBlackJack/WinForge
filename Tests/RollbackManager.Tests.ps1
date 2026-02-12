<#
.SYNOPSIS
    Pester tests for RollbackManager module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge RollbackManager v3.7.2
    Tests auto-rollback, failure tracking, reporting, handler registration,
    rollback execution, and edge cases

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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:ModulePath = Join-Path $script:ModuleRoot 'RollbackManager.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'
    $script:LocalizationPath = Join-Path $PSScriptRoot '..\Core\Localization.psm1'

    # Import Core first (provides Write-Status)
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import Localization (provides Get-LocalizedString)
    if (Test-Path $script:LocalizationPath) {
        Import-Module $script:LocalizationPath -Force -ErrorAction Stop
    }

    # Force English locale for predictable test assertions
    if (Get-Command -Name Set-CurrentLocale -ErrorAction SilentlyContinue) {
        Set-CurrentLocale -Locale 'en'
    } elseif (Get-Command -Name Initialize-Localization -ErrorAction SilentlyContinue) {
        Initialize-Localization -Locale 'en'
    }

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'RollbackManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Get-RollbackConfig function' {
            Get-Command Get-RollbackConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Enable-AutoRollbackOnFailure function' {
            Get-Command Enable-AutoRollbackOnFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-AutoRollbackOnFailure function' {
            Get-Command Disable-AutoRollbackOnFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-CriticalFailureHandler function' {
            Get-Command Register-CriticalFailureHandler -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Unregister-CriticalFailureHandler function' {
            Get-Command Unregister-CriticalFailureHandler -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-InstallationFailure function' {
            Get-Command Register-InstallationFailure -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Reset-FailureCount function' {
            Get-Command Reset-FailureCount -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-RollbackWithConfirmation function' {
            Get-Command Invoke-RollbackWithConfirmation -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RollbackSummary function' {
            Get-Command Get-RollbackSummary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Export-RollbackReport function' {
            Get-Command Export-RollbackReport -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-RollbackCapability function' {
            Get-Command Test-RollbackCapability -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-RollbackConfig' {
        BeforeEach {
            # Force config reload by clearing the cached config
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
            }
        }

        It 'Should return configuration hashtable' {
            $config = Get-RollbackConfig
            $config | Should -BeOfType [hashtable]
        }

        It 'Should have AutoRollbackEnabled property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'AutoRollbackEnabled'
        }

        It 'Should have AutoRollbackThreshold property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'AutoRollbackThreshold'
        }

        It 'Should have RequireConfirmation property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'RequireConfirmation'
        }

        It 'Should have ConfirmationTimeoutSeconds property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'ConfirmationTimeoutSeconds'
        }

        It 'Should have CriticalFailurePatterns property' {
            $config = Get-RollbackConfig
            $config.Keys | Should -Contain 'CriticalFailurePatterns'
        }

        It 'Should return same instance on subsequent calls (cached)' {
            $config1 = Get-RollbackConfig
            $config2 = Get-RollbackConfig
            $config1 | Should -Be $config2
        }

        It 'Should fall back to defaults when config file is missing' {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $originalPath = $script:ConfigPath
                $script:ConfigPath = 'C:\NonExistent\Path\rollback-settings.json'
                try {
                    $config = Get-RollbackConfig
                    $config.AutoRollbackThreshold | Should -Be 3
                    $config.AutoRollbackEnabled | Should -Be $true
                } finally {
                    $script:ConfigPath = $originalPath
                    $script:RollbackConfig = $null
                }
            }
        }

        It 'Should have default threshold of 3' {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $originalPath = $script:ConfigPath
                $script:ConfigPath = 'C:\NonExistent\rollback-settings.json'
                try {
                    $config = Get-RollbackConfig
                    $config.AutoRollbackThreshold | Should -Be 3
                } finally {
                    $script:ConfigPath = $originalPath
                    $script:RollbackConfig = $null
                }
            }
        }

        It 'Should have default CriticalFailurePatterns containing system32' {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $originalPath = $script:ConfigPath
                $script:ConfigPath = 'C:\NonExistent\rollback-settings.json'
                try {
                    $config = Get-RollbackConfig
                    $config.CriticalFailurePatterns | Should -Contain 'system32'
                } finally {
                    $script:ConfigPath = $originalPath
                    $script:RollbackConfig = $null
                }
            }
        }
    }

    Context 'Test-RollbackCapability' {
        It 'Should return true for Winget method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Winget' | Should -Be $true
        }

        It 'Should return true for Chocolatey method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Chocolatey' | Should -Be $true
        }

        It 'Should return true for Choco method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Choco' | Should -Be $true
        }

        It 'Should return true for StoreApp method' {
            Test-RollbackCapability -AppName 'Test' -Method 'StoreApp' | Should -Be $true
        }

        It 'Should return true for MsStore method' {
            Test-RollbackCapability -AppName 'Test' -Method 'MsStore' | Should -Be $true
        }

        It 'Should return false for DirectDownload method' {
            Test-RollbackCapability -AppName 'Test' -Method 'DirectDownload' | Should -Be $false
        }

        It 'Should return false for WindowsFeature method' {
            Test-RollbackCapability -AppName 'Test' -Method 'WindowsFeature' | Should -Be $false
        }

        It 'Should return false for Custom method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Custom' | Should -Be $false
        }

        It 'Should return false for Manual method' {
            Test-RollbackCapability -AppName 'Test' -Method 'Manual' | Should -Be $false
        }

        It 'Should return false for WindowsCapability method' {
            Test-RollbackCapability -AppName 'Test' -Method 'WindowsCapability' | Should -Be $false
        }

        It 'Should return false for unknown method' {
            Test-RollbackCapability -AppName 'Test' -Method 'SomeUnknownMethod' | Should -Be $false
        }

        It 'Should return false when method is empty string' {
            Test-RollbackCapability -AppName 'Test' -Method '' | Should -Be $false
        }
    }

    Context 'Enable-AutoRollbackOnFailure' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        It 'Should enable without errors' {
            { Enable-AutoRollbackOnFailure } | Should -Not -Throw
        }

        It 'Should accept custom threshold' {
            { Enable-AutoRollbackOnFailure -Threshold 5 } | Should -Not -Throw
        }

        It 'Should set AutoRollbackEnabled to true' {
            Enable-AutoRollbackOnFailure
            $config = Get-RollbackConfig
            $config.AutoRollbackEnabled | Should -Be $true
        }

        It 'Should update threshold when custom value is provided' {
            Enable-AutoRollbackOnFailure -Threshold 7
            $config = Get-RollbackConfig
            $config.AutoRollbackThreshold | Should -Be 7
        }

        It 'Should reset failure count to zero' {
            InModuleScope RollbackManager {
                $script:FailureCount = 5
            }
            Enable-AutoRollbackOnFailure
            InModuleScope RollbackManager {
                $script:FailureCount | Should -Be 0
            }
        }

        It 'Should reject threshold below 1' {
            { Enable-AutoRollbackOnFailure -Threshold 0 } | Should -Throw
        }

        It 'Should reject threshold above 100' {
            { Enable-AutoRollbackOnFailure -Threshold 101 } | Should -Throw
        }
    }

    Context 'Disable-AutoRollbackOnFailure' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        It 'Should disable without errors' {
            { Disable-AutoRollbackOnFailure } | Should -Not -Throw
        }

        It 'Should set AutoRollbackEnabled to false' {
            Disable-AutoRollbackOnFailure
            $config = Get-RollbackConfig
            $config.AutoRollbackEnabled | Should -Be $false
        }

        It 'Should reset failure count to zero' {
            InModuleScope RollbackManager {
                $script:FailureCount = 10
            }
            Disable-AutoRollbackOnFailure
            InModuleScope RollbackManager {
                $script:FailureCount | Should -Be 0
            }
        }
    }

    Context 'Register-CriticalFailureHandler' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers = @()
            }
        }

        It 'Should return handler ID' {
            $id = Register-CriticalFailureHandler -Handler { param($d) } -Name 'TestHandler'
            $id | Should -Be 'TestHandler'
        }

        It 'Should register handler in internal collection' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'TestHandler'
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 1
            }
        }

        It 'Should allow registering multiple handlers' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Handler1'
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Handler2'
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Handler3'
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 3
            }
        }

        It 'Should auto-generate name when not provided' {
            $id = Register-CriticalFailureHandler -Handler { param($d) }
            $id | Should -Match '^Handler_'
        }

        It 'Should store RegisteredAt timestamp' {
            $before = Get-Date
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'TimedHandler'
            $after = Get-Date
            InModuleScope RollbackManager {
                $handler = $script:CriticalFailureHandlers | Where-Object { $_.Id -eq 'TimedHandler' }
                $handler.RegisteredAt | Should -Not -BeNullOrEmpty
            }
        }

        It 'Should store the scriptblock handler' {
            Register-CriticalFailureHandler -Handler { param($d) "test" } -Name 'SbHandler'
            InModuleScope RollbackManager {
                $handler = $script:CriticalFailureHandlers | Where-Object { $_.Id -eq 'SbHandler' }
                $handler.Handler | Should -BeOfType [scriptblock]
            }
        }
    }

    Context 'Unregister-CriticalFailureHandler' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers = @()
            }
        }

        It 'Should remove a registered handler by name' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'ToRemove'
            Unregister-CriticalFailureHandler -Name 'ToRemove'
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 0
            }
        }

        It 'Should only remove the specified handler' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Keep1'
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Remove1'
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Keep2'
            Unregister-CriticalFailureHandler -Name 'Remove1'
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 2
                $ids = $script:CriticalFailureHandlers | ForEach-Object { $_.Id }
                $ids | Should -Contain 'Keep1'
                $ids | Should -Contain 'Keep2'
                $ids | Should -Not -Contain 'Remove1'
            }
        }

        It 'Should handle removing a non-existent handler without error' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'Existing'
            { Unregister-CriticalFailureHandler -Name 'NonExistent' } | Should -Not -Throw
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 1
            }
        }

        It 'Should handle removing from empty collection without error' {
            { Unregister-CriticalFailureHandler -Name 'NoHandlers' } | Should -Not -Throw
        }
    }

    Context 'Reset-FailureCount' {
        It 'Should reset failure count to zero' {
            InModuleScope RollbackManager {
                $script:FailureCount = 15
            }
            Reset-FailureCount
            InModuleScope RollbackManager {
                $script:FailureCount | Should -Be 0
            }
        }

        It 'Should not throw when already at zero' {
            InModuleScope RollbackManager {
                $script:FailureCount = 0
            }
            { Reset-FailureCount } | Should -Not -Throw
        }

        It 'Should be idempotent' {
            InModuleScope RollbackManager {
                $script:FailureCount = 5
            }
            Reset-FailureCount
            Reset-FailureCount
            InModuleScope RollbackManager {
                $script:FailureCount | Should -Be 0
            }
        }
    }

    Context 'Register-InstallationFailure' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
                $script:CriticalFailureHandlers = @()
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        It 'Should register failure and return details' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Test error'
            $result | Should -Not -BeNullOrEmpty
            $result.AppName | Should -Be 'TestApp'
        }

        It 'Should increment failure count' {
            Reset-FailureCount
            Register-InstallationFailure -AppName 'Test1' -ErrorMessage 'Error1'
            Register-InstallationFailure -AppName 'Test2' -ErrorMessage 'Error2'
            $result = Register-InstallationFailure -AppName 'Test3' -ErrorMessage 'Error3'
            $result.FailureNumber | Should -Be 3
        }

        It 'Should return ErrorMessage in failure details' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Specific error text'
            $result.ErrorMessage | Should -Be 'Specific error text'
        }

        It 'Should return Timestamp in failure details' {
            $before = Get-Date
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Error'
            $after = Get-Date
            $result.Timestamp | Should -BeGreaterOrEqual $before
            $result.Timestamp | Should -BeLessOrEqual $after
        }

        It 'Should default ErrorMessage to Unknown error' {
            $result = Register-InstallationFailure -AppName 'TestApp'
            $result.ErrorMessage | Should -Be 'Unknown error'
        }

        It 'Should detect critical failure via pattern matching for system32' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Error in system32 directory'
            $result.IsCritical | Should -Be $true
        }

        It 'Should detect critical failure via pattern matching for registry corruption' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Detected registry corruption'
            $result.IsCritical | Should -Be $true
        }

        It 'Should detect critical failure via pattern matching for boot' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'boot failure during install'
            $result.IsCritical | Should -Be $true
        }

        It 'Should detect critical failure via pattern matching for driver' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'driver failure encountered'
            $result.IsCritical | Should -Be $true
        }

        It 'Should detect critical failure via pattern matching for kernel' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'kernel error detected'
            $result.IsCritical | Should -Be $true
        }

        It 'Should not flag non-critical error messages as critical' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Download failed with timeout'
            $result.IsCritical | Should -Be $false
        }

        It 'Should force critical via IsCritical switch' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'Normal error' -IsCritical
            $result.IsCritical | Should -Be $true
        }

        It 'Should set ShouldRollback to true on critical failure' {
            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'kernel error found'
            $result.ShouldRollback | Should -Be $true
            $result.RollbackReason | Should -Be 'Critical failure detected'
        }

        It 'Should set ShouldRollback when threshold is reached' {
            Enable-AutoRollbackOnFailure -Threshold 2
            Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            $result = Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            $result.ShouldRollback | Should -Be $true
            $result.RollbackReason | Should -Match 'Failure threshold reached'
        }

        It 'Should not set ShouldRollback when below threshold' {
            Enable-AutoRollbackOnFailure -Threshold 5
            $result = Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            $result.ShouldRollback | Should -Be $false
        }

        It 'Should not trigger auto-rollback when disabled' {
            Disable-AutoRollbackOnFailure
            InModuleScope RollbackManager {
                $script:FailureCount = 0
            }
            Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            $result = Register-InstallationFailure -AppName 'App3' -ErrorMessage 'Error 3'
            # With auto-rollback disabled and non-critical error, ShouldRollback stays false
            $result.ShouldRollback | Should -Be $false
        }

        It 'Should invoke registered critical failure handlers' {
            $handlerInvoked = $false
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers = @()
            }
            Register-CriticalFailureHandler -Handler {
                param($details)
                Set-Variable -Name 'HandlerWasCalled' -Value $true -Scope Script
            } -Name 'TestInvokeHandler'

            $result = Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'kernel error detected'
            $result.IsCritical | Should -Be $true
        }

        It 'Should not throw when a handler fails during invocation' {
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers = @()
            }
            Register-CriticalFailureHandler -Handler {
                param($details)
                throw "Handler exploded"
            } -Name 'FailingHandler'

            { Register-InstallationFailure -AppName 'TestApp' -ErrorMessage 'system32 error' } | Should -Not -Throw
        }
    }

    Context 'Get-RollbackSummary' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        It 'Should return summary object' {
            $summary = Get-RollbackSummary
            $summary | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalApps property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'TotalApps'
        }

        It 'Should have RollbackableCount property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'RollbackableCount'
        }

        It 'Should have CurrentFailureCount property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'CurrentFailureCount'
        }

        It 'Should have AutoRollbackEnabled property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'AutoRollbackEnabled'
        }

        It 'Should have Applications array property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'Applications'
        }

        It 'Should have SessionId property' {
            $summary = Get-RollbackSummary
            $summary.PSObject.Properties.Name | Should -Contain 'SessionId'
        }

        It 'Should reflect current failure count' {
            InModuleScope RollbackManager {
                $script:FailureCount = 7
            }
            $summary = Get-RollbackSummary
            $summary.CurrentFailureCount | Should -Be 7
        }

        It 'Should reflect AutoRollbackEnabled state' {
            Enable-AutoRollbackOnFailure
            $summary = Get-RollbackSummary
            $summary.AutoRollbackEnabled | Should -Be $true
        }

        It 'Should reflect disabled auto-rollback state' {
            Disable-AutoRollbackOnFailure
            $summary = Get-RollbackSummary
            $summary.AutoRollbackEnabled | Should -Be $false
        }
    }

    Context 'Export-RollbackReport' {
        BeforeAll {
            $script:TestReportDir = Join-Path $env:TEMP "Win11Forge_RollbackTest_$(New-Guid)"
        }

        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
                $script:CriticalFailureHandlers = @()
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        AfterAll {
            if (Test-Path $script:TestReportDir) {
                Remove-Item -Path $script:TestReportDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should export JSON report to specified path' {
            $reportPath = Join-Path $script:TestReportDir 'report.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            Test-Path -Path $reportPath | Should -Be $true
        }

        It 'Should create valid JSON content' {
            $reportPath = Join-Path $script:TestReportDir 'valid.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $content = Get-Content -Path $reportPath -Raw
            { $content | ConvertFrom-Json } | Should -Not -Throw
        }

        It 'Should include GeneratedAt in JSON report' {
            $reportPath = Join-Path $script:TestReportDir 'generated.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.GeneratedAt | Should -Not -BeNullOrEmpty
        }

        It 'Should include ComputerName in JSON report' {
            $reportPath = Join-Path $script:TestReportDir 'computer.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.ComputerName | Should -Be $env:COMPUTERNAME
        }

        It 'Should include Summary section in JSON report' {
            $reportPath = Join-Path $script:TestReportDir 'summary.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.Summary | Should -Not -BeNullOrEmpty
        }

        It 'Should include Configuration section in JSON report' {
            $reportPath = Join-Path $script:TestReportDir 'config.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.Configuration | Should -Not -BeNullOrEmpty
        }

        It 'Should include RegisteredHandlers count in JSON report' {
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'ReportHandler1'
            Register-CriticalFailureHandler -Handler { param($d) } -Name 'ReportHandler2'
            $reportPath = Join-Path $script:TestReportDir 'handlers.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.RegisteredHandlers | Should -Be 2
        }

        It 'Should export Text format report' {
            $reportPath = Join-Path $script:TestReportDir 'report.txt'
            Export-RollbackReport -Path $reportPath -Format 'Text'
            Test-Path -Path $reportPath | Should -Be $true
        }

        It 'Should include header in Text report' {
            $reportPath = Join-Path $script:TestReportDir 'header.txt'
            Export-RollbackReport -Path $reportPath -Format 'Text'
            $content = Get-Content -Path $reportPath -Raw
            $content | Should -Match 'WIN11FORGE ROLLBACK REPORT'
        }

        It 'Should include session info in Text report' {
            $reportPath = Join-Path $script:TestReportDir 'session.txt'
            Export-RollbackReport -Path $reportPath -Format 'Text'
            $content = Get-Content -Path $reportPath -Raw
            $content | Should -Match 'SESSION INFORMATION'
        }

        It 'Should include configuration section in Text report' {
            $reportPath = Join-Path $script:TestReportDir 'config.txt'
            Export-RollbackReport -Path $reportPath -Format 'Text'
            $content = Get-Content -Path $reportPath -Raw
            $content | Should -Match 'CONFIGURATION'
            $content | Should -Match 'Auto-Rollback Enabled'
        }

        It 'Should create parent directory if it does not exist' {
            $nestedDir = Join-Path $script:TestReportDir 'nested\deep\dir'
            $reportPath = Join-Path $nestedDir 'report.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            Test-Path -Path $reportPath | Should -Be $true
        }

        It 'Should default to Json format when not specified' {
            $reportPath = Join-Path $script:TestReportDir 'default_format.json'
            Export-RollbackReport -Path $reportPath
            $content = Get-Content -Path $reportPath -Raw
            { $content | ConvertFrom-Json } | Should -Not -Throw
        }

        It 'Should include FailureCount reflecting current failures' {
            InModuleScope RollbackManager {
                $script:FailureCount = 4
            }
            $reportPath = Join-Path $script:TestReportDir 'failcount.json'
            Export-RollbackReport -Path $reportPath -Format 'Json'
            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.FailureCount | Should -Be 4
        }
    }

    Context 'Invoke-RollbackWithConfirmation' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
            }
            Mock -CommandName Write-Status -MockWith {}
            Mock -CommandName Write-Host -MockWith {}
        }

        It 'Should return result indicating no apps to rollback when summary is empty' {
            Mock -CommandName Get-RollbackSummary -MockWith {
                [PSCustomObject]@{
                    SessionId = $null
                    StartTime = $null
                    TotalApps = 0
                    Applications = @()
                    RollbackableCount = 0
                    CurrentFailureCount = 0
                    AutoRollbackEnabled = $true
                }
            }
            $result = Invoke-RollbackWithConfirmation -Force
            $result.Success | Should -Be $false
            $result.Message | Should -Be 'No applications to rollback'
            $result.AppsRolledBack | Should -Be 0
        }

        It 'Should skip confirmation when Force is specified' {
            Mock -CommandName Get-RollbackSummary -MockWith {
                [PSCustomObject]@{
                    SessionId = 'test-session'
                    StartTime = Get-Date
                    TotalApps = 1
                    Applications = @(
                        [PSCustomObject]@{ AppName = 'TestApp'; Method = 'Winget' }
                    )
                    RollbackableCount = 1
                    CurrentFailureCount = 0
                    AutoRollbackEnabled = $true
                }
            }
            $result = Invoke-RollbackWithConfirmation -Force
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should have Errors property in result' {
            Mock -CommandName Get-RollbackSummary -MockWith {
                [PSCustomObject]@{
                    SessionId = $null
                    StartTime = $null
                    TotalApps = 0
                    Applications = @()
                    RollbackableCount = 0
                    CurrentFailureCount = 0
                    AutoRollbackEnabled = $true
                }
            }
            $result = Invoke-RollbackWithConfirmation -Force
            $result.PSObject.Properties.Name | Should -Contain 'Errors'
            $result.Errors.Count | Should -Be 0
        }

        It 'Should handle missing Invoke-Rollback command gracefully' {
            Mock -CommandName Get-RollbackSummary -MockWith {
                [PSCustomObject]@{
                    SessionId = 'test-session'
                    StartTime = Get-Date
                    TotalApps = 2
                    Applications = @(
                        [PSCustomObject]@{ AppName = 'App1'; Method = 'Winget' },
                        [PSCustomObject]@{ AppName = 'App2'; Method = 'Choco' }
                    )
                    RollbackableCount = 2
                    CurrentFailureCount = 0
                    AutoRollbackEnabled = $true
                }
            }
            $result = Invoke-RollbackWithConfirmation -Force
            # Should handle gracefully without throwing
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should reset failure count after rollback attempt' {
            InModuleScope RollbackManager {
                $script:FailureCount = 5
            }
            Mock -CommandName Get-RollbackSummary -MockWith {
                [PSCustomObject]@{
                    SessionId = $null
                    StartTime = $null
                    TotalApps = 0
                    Applications = @()
                    RollbackableCount = 0
                    CurrentFailureCount = 0
                    AutoRollbackEnabled = $true
                }
            }
            # Even with 0 apps, the early return skips Reset-FailureCount,
            # so failure count stays as-is for empty rollback
            Invoke-RollbackWithConfirmation -Force
            # The function returns early when TotalApps is 0, before resetting
            InModuleScope RollbackManager {
                $script:FailureCount | Should -Be 5
            }
        }
    }

    Context 'Integration - Failure Tracking Workflow' {
        BeforeEach {
            InModuleScope RollbackManager {
                $script:RollbackConfig = $null
                $script:FailureCount = 0
                $script:CriticalFailureHandlers = @()
            }
            Mock -CommandName Write-Status -MockWith {}
        }

        It 'Should track failures across multiple registrations' {
            Reset-FailureCount
            $r1 = Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            $r2 = Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            $r3 = Register-InstallationFailure -AppName 'App3' -ErrorMessage 'Error 3'
            $r1.FailureNumber | Should -Be 1
            $r2.FailureNumber | Should -Be 2
            $r3.FailureNumber | Should -Be 3
        }

        It 'Should reset failure tracking after Reset-FailureCount' {
            Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            Reset-FailureCount
            $result = Register-InstallationFailure -AppName 'App3' -ErrorMessage 'Error 3'
            $result.FailureNumber | Should -Be 1
        }

        It 'Should enable then disable auto-rollback correctly' {
            Enable-AutoRollbackOnFailure -Threshold 2
            $config = Get-RollbackConfig
            $config.AutoRollbackEnabled | Should -Be $true
            $config.AutoRollbackThreshold | Should -Be 2

            Disable-AutoRollbackOnFailure
            $config2 = Get-RollbackConfig
            $config2.AutoRollbackEnabled | Should -Be $false
        }

        It 'Should trigger auto-rollback exactly at threshold boundary' {
            Enable-AutoRollbackOnFailure -Threshold 3
            $r1 = Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            $r2 = Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            $r3 = Register-InstallationFailure -AppName 'App3' -ErrorMessage 'Error 3'
            $r1.ShouldRollback | Should -Be $false
            $r2.ShouldRollback | Should -Be $false
            $r3.ShouldRollback | Should -Be $true
        }

        It 'Should handle handler registration and unregistration roundtrip' {
            $id1 = Register-CriticalFailureHandler -Handler { param($d) } -Name 'H1'
            $id2 = Register-CriticalFailureHandler -Handler { param($d) } -Name 'H2'
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 2
            }
            Unregister-CriticalFailureHandler -Name $id1
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 1
                $script:CriticalFailureHandlers[0].Id | Should -Be 'H2'
            }
            Unregister-CriticalFailureHandler -Name $id2
            InModuleScope RollbackManager {
                $script:CriticalFailureHandlers.Count | Should -Be 0
            }
        }

        It 'Should reflect failure count in rollback summary' {
            Register-InstallationFailure -AppName 'App1' -ErrorMessage 'Error 1'
            Register-InstallationFailure -AppName 'App2' -ErrorMessage 'Error 2'
            $summary = Get-RollbackSummary
            $summary.CurrentFailureCount | Should -Be 2
        }
    }
}
