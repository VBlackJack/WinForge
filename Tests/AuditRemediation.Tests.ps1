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

BeforeAll {
    $script:Repository = Split-Path $PSScriptRoot -Parent
}

Describe 'Plugin import isolation regression' {
    It 'Keeps module initialization and hooks constrained and uses the validated snapshot' {
        Import-Module (Join-Path $script:Repository 'Core/PluginManager.psm1') -Force
        $pluginDir = Join-Path $TestDrive 'SnapshotProbe'
        $null = New-Item -ItemType Directory -Path $pluginDir
        $marker = Join-Path $TestDrive 'host-execution.txt'
        $content = @'
if ($ExecutionContext.SessionState.LanguageMode -eq 'FullLanguage') {
    $null = $ExecutionContext.InvokeCommand.InvokeScript("'escaped' | Set-Content -LiteralPath '__MARKER__'")
}
$script:Prefix = 'snapshot'
function Invoke-SnapshotProbepreinstall {
    param([hashtable]$Context)
    return "$script:Prefix-$($Context.Value)-$($ExecutionContext.SessionState.LanguageMode)"
}
Export-ModuleMember -Function Invoke-SnapshotProbepreinstall
'@.Replace('__MARKER__', $marker.Replace("'", "''"))
        $entryPath = Join-Path $pluginDir 'Main.psm1'
        Set-Content -LiteralPath $entryPath -Value $content -Encoding UTF8
        @{ name = 'SnapshotProbe'; version = '1.0.0'; entryPoint = 'Main.psm1'
           hooks = @('pre-install'); installationMethods = @(); description = 'Fixture'; author = 'Julien Bombled'; dependencies = @() } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pluginDir 'manifest.json') -Encoding UTF8
        $manager = Get-Module PluginManager
        $previousRoot = & $manager { $script:PluginsDir }
        try {
            & $manager { param($root) $script:PluginsDir = $root } $TestDrive
            Import-Plugin -Name SnapshotProbe
            Test-Path -LiteralPath $marker | Should -BeFalse
            Get-Command Invoke-SnapshotProbepreinstall -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
            Set-Content -LiteralPath $entryPath -Value "throw 'changed source must not execute'" -Encoding UTF8
            $result = @(Invoke-PluginHook -HookName pre-install -Context @{ Value = 'test' })
            $result.Count | Should -Be 1
            $result[0].Success | Should -BeTrue
            $result[0].Result | Should -Be 'snapshot-test-ConstrainedLanguage'
            Test-Path -LiteralPath $marker | Should -BeFalse
        } finally {
            Remove-Plugin -Name SnapshotProbe
            & $manager { param($root) $script:PluginsDir = $root } $previousRoot
        }
    }
}

Describe 'Deployment API worker regression' {
    BeforeEach {
        Import-Module (Join-Path $script:Repository 'Core/ApiEndpoints.psm1') -Force
        $script:ApiModule = Get-Module ApiEndpoints
        $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Path (Join-Path $fixture 'Profiles') -Force
        Set-Content -LiteralPath (Join-Path $fixture 'Profiles/Probe.json') -Value '{"Name":"Probe","Applications":[]}'
        Set-Content -LiteralPath (Join-Path $fixture 'Deploy-Win11Environment.ps1') -Value @'
param($ProfileName, [switch]$TestMode, [switch]$SkipPrerequisites)
if (-not $TestMode -or -not $SkipPrerequisites -or -not (Test-Path -LiteralPath $ProfileName)) { throw 'Worker arguments were not forwarded.' }
Start-Sleep -Milliseconds 800
$global:LASTEXITCODE = 0
'@
        & $script:ApiModule {
            param($root)
            $script:RepositoryRoot = $root
            $script:ProfilesPath = Join-Path $root 'Profiles'
        } $fixture
    }
    AfterEach {
        & $script:ApiModule {
            if ($script:DeploymentJob) {
                $script:DeploymentJob | Stop-Job -PassThru | Remove-Job -Force
                $script:DeploymentJob = $null
            }
        }
    }
    It 'Starts a real worker, rejects overlap, and observes completion' {
        $response = Start-DeploymentHandler -Context @{ Body = @{ profile = 'Probe'; testMode = $true } }
        $response.success | Should -BeTrue
        (Start-DeploymentHandler -Context @{ Body = @{ profile = 'Probe'; testMode = $true } }).success | Should -BeFalse
        & $script:ApiModule { $null = Wait-Job -Job $script:DeploymentJob -Timeout 20 }
        $status = Get-StatusHandler -Context @{}
        $status.status | Should -Be Completed
        $status.progress | Should -Be 100
    }
    It 'Reports a worker failure instead of claiming completion' {
        & $script:ApiModule { Set-Content -LiteralPath (Join-Path $script:RepositoryRoot 'Deploy-Win11Environment.ps1') -Value "throw 'fixture failure'" }
        (Start-DeploymentHandler -Context @{ Body = @{ profile = 'Probe'; testMode = $true } }).success | Should -BeTrue
        & $script:ApiModule { $null = Wait-Job -Job $script:DeploymentJob -Timeout 20 }
        (Get-StatusHandler -Context @{}).status | Should -Be Failed
    }
    It 'Fails closed when a required schema is missing' {
        & $script:ApiModule { $script:SchemasPath = Join-Path $script:RepositoryRoot 'MissingSchemas' }
        (Start-DeploymentHandler -Context @{ Body = @{ profile = 'Probe'; testMode = $true } }).success | Should -BeFalse
        & $script:ApiModule { $script:DeploymentJob | Should -BeNullOrEmpty }
    }
}
