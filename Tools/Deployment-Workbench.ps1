# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

<#
.SYNOPSIS
    Previews, compares, executes and recovers frozen application deployment plans.
.DESCRIPTION
    Plan is read-only with respect to installed applications. Apply and Rollback
    support WhatIf. System configuration is captured for comparison but is not
    applied by this package-only workbench. Sources are frozen, remote package
    versions are not pinned; observed versions remain separate evidence.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
param(
    [Parameter(Mandatory)][ValidateSet('Plan','Compare','History','Apply','Retry','Rollback','Compliance')][string]$Action,
    [Parameter(Mandatory)][string]$Path,
    [string]$ProfileName,
    [string]$ProfilesDirectory,
    [string]$ReferencePath,
    [switch]$ForceUpdate
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../Modules/DeploymentPlanning.psm1') -ErrorAction Stop
switch ($Action) {
    'Plan' {
        if (-not $ProfileName) { throw 'ProfileName is required for Plan.' }
        if (Test-Path -LiteralPath $Path) { throw 'Choose a new plan path; existing receipts are not overwritten.' }
        Import-Module (Join-Path $PSScriptRoot '../Modules/ProfileManager.psm1') -ErrorAction Stop
        $resolvedProfile = Get-DeploymentProfile -ProfileName $ProfileName -ProfilesDirectory $ProfilesDirectory
        $plan = New-DeploymentPlan -Applications @($resolvedProfile.Applications) -ProfileName $resolvedProfile.Name -ForceUpdate:$ForceUpdate
        $plan | Add-Member -NotePropertyName SystemConfig -NotePropertyValue $resolvedProfile.SystemConfig
        $plan | Add-Member -NotePropertyName InheritanceChain -NotePropertyValue @($resolvedProfile.InheritanceChain)
        $plan | Add-Member -NotePropertyName ProfileVersion -NotePropertyValue $resolvedProfile.Version
        Save-DeploymentPlan -Plan $plan -Path $Path
        $plan.Items | Select-Object AppId,Action,InstalledVersion,PreferredSource,Elevation,Reboot,Rollback
    }
    'Compare' {
        if (-not $ReferencePath) { throw 'ReferencePath is required for Compare.' }
        Compare-DeploymentPlan -Reference (Read-DeploymentPlan $ReferencePath) -Difference (Read-DeploymentPlan $Path)
    }
    'History' { Get-DeploymentRecovery -Plan (Read-DeploymentPlan $Path) }
    'Apply' { if ($PSCmdlet.ShouldProcess($Path, 'Execute frozen application plan')) { Invoke-DeploymentPlan -Path $Path -Confirm:$false } }
    'Retry' { if ($PSCmdlet.ShouldProcess($Path, 'Retry failed or interrupted applications')) { Invoke-DeploymentPlan -Path $Path -RetryFailed -Confirm:$false } }
    'Rollback' { if ($PSCmdlet.ShouldProcess($Path, 'Uninstall new packages recorded by this deployment')) { Undo-DeploymentPlan -Path $Path -Confirm:$false } }
    'Compliance' {
        $plan = Read-DeploymentPlan $Path
        Import-Module (Join-Path $PSScriptRoot '../Modules/ApplicationDetection.psm1') -ErrorAction Stop
        $current = Get-ApplicationsInstallationStatus -Applications @($plan.Items.Definition) -Refresh
        foreach ($item in $plan.Items) {
            $receipt = @($plan.Results | Where-Object { $_.AppId -eq $item.AppId -and $_.Status -eq 'Succeeded' } | Select-Object -Last 1)
            $expected = if ($receipt.Count) { $receipt[0].InstalledVersion } else { $item.InstalledVersion }
            $observed = $current[$item.AppId]
            [pscustomobject]@{
                AppId=$item.AppId; ExpectedVersion=$expected; ObservedVersion=$observed.Version
                Status=if (-not $observed.IsInstalled) { 'Missing' } elseif (-not $expected -or -not $observed.Version) { 'VersionUnknown' } elseif ($expected -eq $observed.Version) { 'MatchesObservedBaseline' } else { 'VersionDrift' }
            }
        }
    }
    default { throw 'Unsupported workbench action.' }
}
