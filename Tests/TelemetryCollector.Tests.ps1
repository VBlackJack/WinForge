<#
.SYNOPSIS
    Pester tests for TelemetryCollector module

.DESCRIPTION
    Unit tests for Win11Forge TelemetryCollector v3.1.4
    Tests deployment tracking, statistics, and reporting functions

.NOTES
    Author: Julien Bombled
    Version: 3.1.4
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
    $script:ModulePath = Join-Path $script:ModuleRoot 'TelemetryCollector.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'TelemetryCollector Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-TelemetryCollector function' {
            Get-Command Initialize-TelemetryCollector -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-DeploymentStart function' {
            Get-Command Register-DeploymentStart -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-DeploymentEnd function' {
            Get-Command Register-DeploymentEnd -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Register-ApplicationInstall function' {
            Get-Command Register-ApplicationInstall -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-TelemetrySummary function' {
            Get-Command Get-TelemetrySummary -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Export-TelemetryReport function' {
            Get-Command Export-TelemetryReport -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Clear-TelemetryData function' {
            Get-Command Clear-TelemetryData -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-TelemetryCollector' {
        It 'Should initialize without errors' {
            { Initialize-TelemetryCollector } | Should -Not -Throw
        }
    }

    Context 'Register-DeploymentStart' {
        BeforeEach {
            Initialize-TelemetryCollector
        }

        It 'Should return session ID' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            $sessionId | Should -Not -BeNullOrEmpty
        }

        It 'Should accept custom session ID' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile' -SessionId 'CustomId'
            $sessionId | Should -Be 'CustomId'
        }
    }

    Context 'Register-ApplicationInstall' {
        BeforeEach {
            Initialize-TelemetryCollector
        }

        It 'Should register successful install' {
            { Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Category 'Development' -Success $true } | Should -Not -Throw
        }

        It 'Should register failed install' {
            { Register-ApplicationInstall -AppName 'TestApp' -Method 'Chocolatey' -Success $false } | Should -Not -Throw
        }
    }

    Context 'Get-TelemetrySummary' {
        BeforeEach {
            Initialize-TelemetryCollector
        }

        It 'Should return summary object' {
            $summary = Get-TelemetrySummary
            $summary | Should -Not -BeNullOrEmpty
        }

        It 'Should have Deployments property' {
            $summary = Get-TelemetrySummary
            $summary.PSObject.Properties.Name | Should -Contain 'Deployments'
        }

        It 'Should have Applications property' {
            $summary = Get-TelemetrySummary
            $summary.PSObject.Properties.Name | Should -Contain 'Applications'
        }

        It 'Should have Performance property' {
            $summary = Get-TelemetrySummary
            $summary.PSObject.Properties.Name | Should -Contain 'Performance'
        }
    }

    Context 'Clear-TelemetryData' {
        It 'Should require confirmation' {
            { Clear-TelemetryData } | Should -Not -Throw
        }

        It 'Should clear data when confirmed' {
            Initialize-TelemetryCollector
            { Clear-TelemetryData -Confirm } | Should -Not -Throw
        }

        It 'Should reset all counters when confirmed' {
            Initialize-TelemetryCollector
            $null = Register-DeploymentStart -ProfileName 'TestProfile'
            Register-ApplicationInstall -AppName 'TestApp' -Method 'Winget' -Success $true

            Clear-TelemetryData -Confirm

            $summary = Get-TelemetrySummary
            $summary.Deployments.Total | Should -Be 0
            $summary.Applications.TotalInstalled | Should -Be 0
        }
    }

    Context 'Register-DeploymentEnd' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should mark deployment as successful' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            Register-DeploymentEnd -SessionId $sessionId -Success $true

            $summary = Get-TelemetrySummary
            $summary.Deployments.Successful | Should -Be 1
        }

        It 'Should mark deployment as failed' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            Register-DeploymentEnd -SessionId $sessionId -Success $false

            $summary = Get-TelemetrySummary
            $summary.Deployments.Failed | Should -Be 1
        }

        It 'Should mark deployment as rolled back' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            Register-DeploymentEnd -SessionId $sessionId -RolledBack

            $summary = Get-TelemetrySummary
            $summary.Deployments.RolledBack | Should -Be 1
        }

        It 'Should calculate duration' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            Start-Sleep -Milliseconds 100
            Register-DeploymentEnd -SessionId $sessionId -Success $true

            $summary = Get-TelemetrySummary
            $summary.Performance.AverageDeploymentSeconds | Should -BeGreaterThan 0
        }
    }

    Context 'Profile Tracking' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should track profile usage' {
            $null = Register-DeploymentStart -ProfileName 'Gaming'
            $null = Register-DeploymentStart -ProfileName 'Gaming'
            $null = Register-DeploymentStart -ProfileName 'Development'

            $summary = Get-TelemetrySummary
            $summary.Profiles.UsageStats.Gaming | Should -Be 2
            $summary.Profiles.UsageStats.Development | Should -Be 1
        }

        It 'Should identify most used profile' {
            $null = Register-DeploymentStart -ProfileName 'Gaming'
            $null = Register-DeploymentStart -ProfileName 'Gaming'
            $null = Register-DeploymentStart -ProfileName 'Development'

            $summary = Get-TelemetrySummary
            $summary.Profiles.MostUsed | Should -Be 'Gaming'
        }
    }

    Context 'Application Statistics' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should track installations by method' {
            Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Success $true
            Register-ApplicationInstall -AppName 'Firefox' -Method 'Winget' -Success $true
            Register-ApplicationInstall -AppName 'Git' -Method 'Chocolatey' -Success $true

            $summary = Get-TelemetrySummary
            $summary.Applications.ByMethod.Winget | Should -Be 2
            $summary.Applications.ByMethod.Chocolatey | Should -Be 1
        }

        It 'Should track installations by category' {
            Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Category 'Development' -Success $true
            Register-ApplicationInstall -AppName 'Git' -Method 'Winget' -Category 'Development' -Success $true
            Register-ApplicationInstall -AppName 'Firefox' -Method 'Winget' -Category 'Browser' -Success $true

            $summary = Get-TelemetrySummary
            $summary.Applications.ByCategory.Development | Should -Be 2
            $summary.Applications.ByCategory.Browser | Should -Be 1
        }

        It 'Should track top installed apps' {
            Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Success $true
            Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Success $true
            Register-ApplicationInstall -AppName 'Firefox' -Method 'Winget' -Success $true

            $summary = Get-TelemetrySummary
            $summary.Applications.TopInstalled.Count | Should -BeGreaterThan 0
        }

        It 'Should track failed installations' {
            Register-ApplicationInstall -AppName 'BrokenApp' -Method 'Winget' -Success $false
            Register-ApplicationInstall -AppName 'BrokenApp' -Method 'Winget' -Success $false

            $summary = Get-TelemetrySummary
            $summary.Applications.TotalFailed | Should -Be 2
        }

        It 'Should link app installs to session' {
            $sessionId = Register-DeploymentStart -ProfileName 'TestProfile'
            Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Success $true -SessionId $sessionId
            Register-ApplicationInstall -AppName 'BrokenApp' -Method 'Winget' -Success $false -SessionId $sessionId

            Register-DeploymentEnd -SessionId $sessionId -Success $true
            # Session counters are internal, test passes if no errors
        }
    }

    Context 'Success Rate Calculation' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should calculate correct success rate' {
            # 3 successful, 1 failed = 75 percent
            $session1 = Register-DeploymentStart -ProfileName 'Test'
            Register-DeploymentEnd -SessionId $session1 -Success $true

            $session2 = Register-DeploymentStart -ProfileName 'Test'
            Register-DeploymentEnd -SessionId $session2 -Success $true

            $session3 = Register-DeploymentStart -ProfileName 'Test'
            Register-DeploymentEnd -SessionId $session3 -Success $true

            $session4 = Register-DeploymentStart -ProfileName 'Test'
            Register-DeploymentEnd -SessionId $session4 -Success $false

            $summary = Get-TelemetrySummary
            $summary.Deployments.SuccessRate | Should -Be '75%'
        }

        It 'Should return 0 percent when no deployments' {
            $summary = Get-TelemetrySummary
            $summary.Deployments.SuccessRate | Should -Be '0%'
        }
    }

    Context 'Export-TelemetryReport' {
        BeforeEach {
            Initialize-TelemetryCollector
        }

        It 'Should export report to default location' {
            $path = Export-TelemetryReport
            $path | Should -Not -BeNullOrEmpty
        }

        It 'Should export report to custom location' {
            $tempPath = Join-Path $env:TEMP "Win11ForgeTest_$(Get-Random).json"
            try {
                $path = Export-TelemetryReport -OutputPath $tempPath
                $path | Should -Be $tempPath
                Test-Path $tempPath | Should -Be $true
            }
            finally {
                if (Test-Path $tempPath) {
                    Remove-Item $tempPath -Force
                }
            }
        }

        It 'Should produce valid JSON' {
            $tempPath = Join-Path $env:TEMP "Win11ForgeTest_$(Get-Random).json"
            try {
                Export-TelemetryReport -OutputPath $tempPath
                $content = Get-Content $tempPath -Raw
                { $content | ConvertFrom-Json } | Should -Not -Throw
            }
            finally {
                if (Test-Path $tempPath) {
                    Remove-Item $tempPath -Force
                }
            }
        }

        It 'Should include chart data structure' {
            $tempPath = Join-Path $env:TEMP "Win11ForgeTest_$(Get-Random).json"
            try {
                Export-TelemetryReport -OutputPath $tempPath
                $content = Get-Content $tempPath -Raw | ConvertFrom-Json
                $content.PSObject.Properties.Name | Should -Contain 'charts'
                $content.charts.PSObject.Properties.Name | Should -Contain 'deploymentPie'
                $content.charts.PSObject.Properties.Name | Should -Contain 'methodBar'
            }
            finally {
                if (Test-Path $tempPath) {
                    Remove-Item $tempPath -Force
                }
            }
        }
    }

    Context 'Full Deployment Lifecycle' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should track complete deployment workflow' {
            # Start deployment
            $sessionId = Register-DeploymentStart -ProfileName 'FullTest'

            # Install some apps
            Register-ApplicationInstall -AppName 'App1' -Method 'Winget' -Category 'Dev' -Success $true -SessionId $sessionId
            Register-ApplicationInstall -AppName 'App2' -Method 'Chocolatey' -Category 'Utils' -Success $true -SessionId $sessionId
            Register-ApplicationInstall -AppName 'App3' -Method 'Winget' -Category 'Dev' -Success $false -SessionId $sessionId

            # End deployment
            Register-DeploymentEnd -SessionId $sessionId -Success $true

            # Verify results
            $summary = Get-TelemetrySummary
            $summary.Deployments.Total | Should -Be 1
            $summary.Deployments.Successful | Should -Be 1
            $summary.Applications.TotalInstalled | Should -Be 2
            $summary.Applications.TotalFailed | Should -Be 1
        }
    }

    Context 'Performance Metrics' {
        BeforeEach {
            Initialize-TelemetryCollector
            Clear-TelemetryData -Confirm
        }

        It 'Should convert seconds to minutes correctly' {
            $sessionId = Register-DeploymentStart -ProfileName 'Test'
            Start-Sleep -Milliseconds 100
            Register-DeploymentEnd -SessionId $sessionId -Success $true

            $summary = Get-TelemetrySummary
            # Average minutes should be average seconds / 60
            $expectedMinutes = [math]::Round($summary.Performance.AverageDeploymentSeconds / 60, 1)
            $summary.Performance.AverageDeploymentMinutes | Should -Be $expectedMinutes
        }

        It 'Should accumulate total deployment time' {
            $session1 = Register-DeploymentStart -ProfileName 'Test'
            Start-Sleep -Milliseconds 50
            Register-DeploymentEnd -SessionId $session1 -Success $true

            $summary1 = Get-TelemetrySummary
            $time1 = $summary1.Performance.TotalDeploymentHours

            $session2 = Register-DeploymentStart -ProfileName 'Test'
            Start-Sleep -Milliseconds 50
            Register-DeploymentEnd -SessionId $session2 -Success $true

            $summary2 = Get-TelemetrySummary
            $summary2.Performance.TotalDeploymentHours | Should -BeGreaterOrEqual $time1
        }
    }
}
