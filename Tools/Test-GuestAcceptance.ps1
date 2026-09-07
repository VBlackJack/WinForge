# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

<# .SYNOPSIS Runs privileged acceptance only inside an explicitly named disposable VMware guest. #>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
param(
    [Parameter(Mandatory)][string]$ExpectedComputerName,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$BaselineVersion = '24.09',
    [ValidateRange(10,600)][int]$ScheduledTaskTimeoutSeconds = 120
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$system=Get-CimInstance Win32_ComputerSystem
if ($env:COMPUTERNAME -ine $ExpectedComputerName -or $system.Model -notmatch 'VMware') {
    throw 'Acceptance requires the explicitly named disposable VMware guest.'
}
$principal=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run acceptance elevated inside the guest.' }
if (-not $PSCmdlet.ShouldProcess($ExpectedComputerName,'Install/update 7-Zip, enable Telnet Client, exercise partial rollback and a SYSTEM task')) { return }
$root=Split-Path $PSScriptRoot -Parent
$reportDirectory=Split-Path ([IO.Path]::GetFullPath($ReportPath)) -Parent
$null=New-Item -ItemType Directory -Force -Path $reportDirectory
$results=[Collections.Generic.List[object]]::new()
$task=$null
try {
    Import-Module "$root/Modules/ApplicationDatabase.psm1" -ErrorAction Stop
    Import-Module "$root/Modules/InstallationOrchestrator.psm1" -ErrorAction Stop
    Import-Module "$root/Modules/ApplicationDetection.psm1" -ErrorAction Stop
    Import-Module "$root/Modules/DeploymentPlanning.psm1" -ErrorAction Stop
    $null=Get-Command winget -ErrorAction Stop
    $package=ConvertTo-ProfileApplication -App (Get-ApplicationById -AppId '7Zip')
    if (Test-ApplicationInstalled -Application $package) { throw '7-Zip is already installed; restore the clean guest checkpoint first.' }
    if ((Get-WindowsOptionalFeature -Online -FeatureName TelnetClient).State -ne 'Disabled') { throw 'Telnet Client must initially be disabled.' }
    $package | Add-Member SourceLock ([pscustomobject]@{Method='Winget';Identifier=$package.Sources.Winget;Version=$BaselineVersion})
    $feature=[pscustomobject]@{AppId='AcceptanceTelnet';Name='Acceptance Telnet Client';Priority=2;EnvironmentRestrictions=@();InstallMethod='WindowsFeature';Sources=[pscustomobject]@{Winget=$null;Chocolatey=$null;Store=$null;DirectUrl=$null};Detection=[pscustomobject]@{Method='WindowsFeature';Feature='TelnetClient'}}
    $plan=New-DeploymentPlan -Applications @($package,$feature) -InstalledState @{
        '7Zip'=@{IsInstalled=$false;Version=$null};AcceptanceTelnet=@{IsInstalled=$false;Version=$null}
    }
    $planPath=Join-Path $reportDirectory 'acceptance-plan.json'
    if (Test-Path -LiteralPath $planPath) { throw 'Use a new report directory for each acceptance run.' }
    Save-DeploymentPlan $plan $planPath
    $plan=Invoke-DeploymentPlan -Path $planPath -Confirm:$false
    if (@($plan.Results | Where-Object Status -eq 'Succeeded').Count -ne 2) { throw 'Baseline installation failed; inspect acceptance-plan.json.' }
    $results.Add(@{Scenario='Install';Passed=$true;DeploymentId=$plan.DeploymentId})

    $beforeUpdate=Get-ApplicationsInstallationStatus -Applications @($package) -Refresh
    $beforeVersion=$beforeUpdate['7Zip'].Version
    if (-not $beforeVersion) { throw 'The baseline installed version could not be independently observed.' }

    $package.PSObject.Properties.Remove('SourceLock')
    $update=Install-Application -Application $package -ForceUpdate
    if (-not $update.Success) { throw 'Update was not demonstrated.' }
    $current=Get-ApplicationsInstallationStatus -Applications @($package) -Refresh
    if (-not $current['7Zip'].Version -or $current['7Zip'].Version -eq $beforeVersion) { throw 'The installed version did not change; update acceptance failed.' }
    $results.Add(@{Scenario='Update';Passed=$true;ObservedVersion=$current['7Zip'].Version})

    $plan=Undo-DeploymentPlan -Path $planPath -Confirm:$false
    $recovery=@(Get-DeploymentRecovery $plan)
    if (@($recovery | Where-Object Status -eq 'RolledBack').Count -ne 1 -or
        @($recovery | Where-Object { $_.AppId -eq 'AcceptanceTelnet' -and -not $_.CanRollback }).Count -ne 1) {
        throw 'Partial rollback did not retain the unsupported feature.'
    }
    $results.Add(@{Scenario='PartialRollback';Passed=$true;Unsupported='AcceptanceTelnet'})

    # Register through the production scheduler; the guest-only launcher records its real identity.
    Import-Module "$root/Modules/ScheduledDeployment.psm1" -ErrorAction Stop
    $fixture=Join-Path $reportDirectory 'scheduled fixture'
    $null=New-Item -ItemType Directory -Path $fixture
    $receipt=Join-Path $fixture 'system.json'
    '{"Name":"Acceptance","Inherits":[],"Applications":[]}' | Set-Content -LiteralPath (Join-Path $fixture 'Acceptance.json')
    @'
param([string]$ProfileName)
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
@{Identity=$identity;ProfilePath=$ProfileName;ProfileExists=(Test-Path -LiteralPath $ProfileName)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'system.json')
'@ | Set-Content -LiteralPath (Join-Path $fixture 'Deploy-Win11Environment.ps1')
    $module=Get-Module ScheduledDeployment
    $previous=& $module { $script:RepositoryRoot }
    try {
        & $module { param($path) $script:RepositoryRoot=$path } $fixture
        $task=New-ScheduledDeployment -ProfileName Acceptance -ProfileDirectories @($fixture) -ScheduledTime (Get-Date).AddDays(1) -Confirm:$false
        $taskFolder = & $module { $script:TaskFolder }
        Start-ScheduledTask -TaskName $task.TaskName -TaskPath $taskFolder
        $deadline=(Get-Date).AddSeconds($ScheduledTaskTimeoutSeconds)
        while (-not (Test-Path -LiteralPath $receipt) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
        Get-ScheduledTaskInfo -TaskName $task.TaskName -TaskPath $taskFolder |
            Select-Object LastRunTime, LastTaskResult, NextRunTime |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'scheduled-task-info.json') -Encoding UTF8
        $actual=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        if ($actual.Identity -ne 'S-1-5-18' -or -not $actual.ProfileExists) { throw 'SYSTEM execution or profile snapshot failed.' }
        $results.Add(@{Scenario='ScheduledSystem';Passed=$true;Identity=$actual.Identity})
    } finally { & $module { param($path) $script:RepositoryRoot=$path } $previous }
} catch {
    $results.Add(@{Scenario='AcceptanceFailure';Passed=$false;Message=$_.Exception.Message})
} finally {
    if ($task) { Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $taskFolder -Confirm:$false }
    @{Computer=$env:COMPUTERNAME;Model=$system.Model;At=[datetime]::UtcNow.ToString('o');Passed=(@($results | Where-Object { -not $_.Passed }).Count -eq 0 -and $results.Count -eq 4);Results=$results.ToArray()} |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}
if (@($results | Where-Object { -not $_.Passed }).Count) { exit 1 }

