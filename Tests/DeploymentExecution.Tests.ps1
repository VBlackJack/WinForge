# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

Describe 'Deployment process interruption' {
    It 'resumes a killed worker without replaying completed packages and retains a failed rollback' {
        $fixture=Join-Path $TestDrive 'worker'
        $null=New-Item -ItemType Directory -Path "$fixture/Modules","$fixture/Core"
        Copy-Item "$PSScriptRoot/../Modules/DeploymentPlanning.psm1" "$fixture/Modules/DeploymentPlanning.psm1"
        @'
function Test-ApplicationInstalled { param($Application) Test-Path "$PSScriptRoot/../$($Application.AppId).installed" }
function Clear-RegistryAppsCache {}
function Get-ApplicationsInstallationStatus { param($Applications,[switch]$Refresh)
    $state=@{}
    if (Test-Path "$PSScriptRoot/../unknown-detection") { return $state }
    foreach($app in $Applications) { $state[$app.AppId]=@{IsInstalled=(Test-ApplicationInstalled $app);Version='1.0'} }
    return $state
}
'@ | Set-Content "$fixture/Modules/ApplicationDetection.psm1"
        @'
function Install-Application { param($Application,[switch]$ForceUpdate,[switch]$SkipRollbackJournal)
    if ($Application.AppId -eq 'Second' -and -not (Test-Path "$PSScriptRoot/../continue")) {
        Set-Content "$PSScriptRoot/../waiting" 'ready'
        while (-not (Test-Path "$PSScriptRoot/../continue")) { Start-Sleep -Milliseconds 50 }
    }
    Add-Content "$PSScriptRoot/../calls" $Application.AppId
    Set-Content "$PSScriptRoot/../$($Application.AppId).installed" 'installed'
    return @{Success=$true;AlreadyInstalled=$false;WasInstalled=$false;Method='Winget';Message='fixture'}
}
'@ | Set-Content "$fixture/Modules/InstallationOrchestrator.psm1"
        @'
function Invoke-NativeCommandUtf8 { param($FilePath,$ArgumentList)
    if (($ArgumentList -contains 'First.Id') -and (Test-Path "$PSScriptRoot/../allow-cleanup")) {
        Remove-Item -LiteralPath "$PSScriptRoot/../First.installed"
    }
    return @{ExitCode=$(if ($ArgumentList -contains 'First.Id') { 0 } else { 5 })}
}
'@ | Set-Content "$fixture/Core/Core.psm1"
        @'
param([switch]$Retry)
$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot/Modules/DeploymentPlanning.psm1"
Invoke-DeploymentPlan "$PSScriptRoot/plan.json" -RetryFailed:$Retry -Confirm:$false | Out-Null
'@ | Set-Content "$fixture/worker.ps1"
        Import-Module "$fixture/Modules/DeploymentPlanning.psm1" -Force
        $apps=@('First','Second') | ForEach-Object { [pscustomobject]@{AppId=$_;Name=$_;Sources=@{Winget="$_.Id"}} }
        $plan=New-DeploymentPlan -Applications $apps -InstalledState @{First=@{IsInstalled=$false};Second=@{IsInstalled=$false}}
        Save-DeploymentPlan $plan "$fixture/plan.json"
        $start=[Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
        $start.UseShellExecute=$false
        $start.CreateNoWindow=$true
        foreach($arg in @('-NoProfile','-File',"$fixture/worker.ps1")) { $start.ArgumentList.Add($arg) }
        $process=[Diagnostics.Process]::Start($start)
        try {
            $deadline=(Get-Date).AddSeconds(20)
            while (-not (Test-Path "$fixture/waiting") -and -not $process.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 50 }
            Test-Path "$fixture/waiting" | Should -BeTrue
            $process.Kill()
            $process.WaitForExit()
            $interrupted=Read-DeploymentPlan "$fixture/plan.json"
            (Get-DeploymentRecovery $interrupted | Where-Object AppId -eq Second).Status | Should -Be Interrupted
        } finally { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
        Set-Content "$fixture/continue" 'resume'
        $start.ArgumentList.Add('-Retry')
        $process=[Diagnostics.Process]::Start($start)
        try {
            $process.WaitForExit(20000) | Should -BeTrue
            $process.ExitCode | Should -Be 0
        } finally { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
        @(Get-Content "$fixture/calls" | Where-Object { $_ -eq 'First' }).Count | Should -Be 1
        @(Get-Content "$fixture/calls" | Where-Object { $_ -eq 'Second' }).Count | Should -Be 1
        (Read-DeploymentPlan "$fixture/plan.json").State | Should -Be Completed
        Set-Content "$fixture/unknown-detection" 'unavailable'
        $unknown=Undo-DeploymentPlan "$fixture/plan.json" -Confirm:$false
        $unknownRecovery=@(Get-DeploymentRecovery $unknown)
        ($unknownRecovery | Where-Object AppId -eq First).CanRollback | Should -BeTrue
        ($unknownRecovery | Where-Object AppId -eq First).Message | Should -Match 'could not be verified'
        Remove-Item -LiteralPath "$fixture/unknown-detection"
        $rolled=Undo-DeploymentPlan "$fixture/plan.json" -Confirm:$false
        $recovery=@(Get-DeploymentRecovery $rolled)
        ($recovery | Where-Object AppId -eq First).CanRollback | Should -BeTrue
        ($recovery | Where-Object AppId -eq First).Message | Should -Match 'still finds the application installed'
        Set-Content "$fixture/allow-cleanup" 'ready'
        $rolled=Undo-DeploymentPlan "$fixture/plan.json" -Confirm:$false
        $recovery=@(Get-DeploymentRecovery $rolled)
        ($recovery | Where-Object AppId -eq First).Status | Should -Be RolledBack
        ($recovery | Where-Object AppId -eq Second).CanRollback | Should -BeTrue
        ($recovery | Where-Object AppId -eq Second).Message | Should -Match 'exit code 5'
    }
}
