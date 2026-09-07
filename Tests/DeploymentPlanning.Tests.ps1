# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

BeforeAll { Import-Module "$PSScriptRoot/../Modules/DeploymentPlanning.psm1" -Force }
Describe 'Deployment preparation and recovery' {
    BeforeEach {
        $app = [pscustomobject]@{AppId='Fixture';Name='Fixture';Sources=[pscustomobject]@{Winget='Fixture.Id'};Verified=$true}
        $state = @{Fixture=@{IsInstalled=$false;Version=$null}}
    }
    It 'does not treat a legacy verified flag as installation evidence' {
        $plan = New-DeploymentPlan -Applications @($app) -InstalledState $state
        $plan.Items[0].Sources[0].InstallationTestedAt | Should -BeNullOrEmpty
        $plan.Items[0].Sources[0].Availability | Should -Be Unknown
    }
    It 'invalidates evidence when its source identifier changes' {
        $app | Add-Member SourceEvidence @{Winget=@{Identifier='Old.Id';Availability='Available';InstallationTestedAt='2026-09-07'}}
        (Get-DeploymentSourceEvidence $app Winget).InstallationTestedAt | Should -BeNullOrEmpty
    }
    It 'freezes definitions independently of subsequent catalog edits' {
        $plan = New-DeploymentPlan -Applications @($app) -InstalledState $state
        $app.Sources.Winget='Changed.Id'
        $plan.Items[0].Definition.Sources.Winget | Should -Be Fixture.Id
    }
    It 'refuses to invent an absent state after a failed detection' {
        { New-DeploymentPlan -Applications @($app) -InstalledState @{} } | Should -Throw '*state unavailable*'
    }
    It 'excludes preexisting software from the rollback promise' {
        $state.Fixture.IsInstalled=$true
        $plan=New-DeploymentPlan -Applications @($app) -InstalledState $state -ForceUpdate
        $plan.Items[0].Action | Should -Be Update
        $plan.Items[0].Rollback | Should -Be NotSupportedForExistingApplication
    }
    It 'round trips atomic receipts and distinguishes source changes' {
        $left=New-DeploymentPlan -Applications @($app) -InstalledState $state
        Save-DeploymentPlan $left "$TestDrive/plan.json"
        $right=Read-DeploymentPlan "$TestDrive/plan.json"
        $right.Items[0].Definition.Sources.Winget='New.Id'
        (Compare-DeploymentPlan $left $right).Change | Should -Be DefinitionChanged
        Save-DeploymentPlan $right "$TestDrive/plan.json"
        (Read-DeploymentPlan "$TestDrive/plan.json").Items[0].Definition.Sources.Winget | Should -Be New.Id
    }
    It 'keeps failed, pending and unsupported rollbacks visible' {
        $plan=New-DeploymentPlan -Applications @($app) -InstalledState $state
        (Get-DeploymentRecovery $plan).Retry | Should -BeTrue
        $plan.Results=@([pscustomobject]@{AppId='Fixture';Status='Succeeded';WasInstalled=$false;Method='Store';Identifier='Fixture.Id'})
        (Get-DeploymentRecovery $plan).CanRollback | Should -BeFalse
        $plan.Results[0].Method='Winget'
        (Get-DeploymentRecovery $plan).CanRollback | Should -BeTrue
        $plan.Results[0].WasInstalled=$true
        (Get-DeploymentRecovery $plan).CanRollback | Should -BeFalse
    }
    It 'refuses duplicate application identities' {
        { New-DeploymentPlan -Applications @($app,$app) -InstalledState $state } | Should -Throw '*duplicate*'
    }
    It 'refuses a source lock that can be bypassed by a custom installer' {
        $app | Add-Member SourceLock @{Method='Winget';Identifier='Fixture.Id';Version='1.2'}
        $app | Add-Member InstallMethod 'WindowsFeature'
        { New-DeploymentPlan -Applications @($app) -InstalledState $state } | Should -Throw '*source lock*'
    }
    It 'marks an unavailable locked source unavailable even with an available fallback' {
        $app.Sources | Add-Member Chocolatey 'fixture'
        $app | Add-Member SourceLock @{Method='Winget';Identifier='Fixture.Id';Version='1.2'}
        $app | Add-Member SourceEvidence @{Winget=@{Identifier='Fixture.Id';Availability='Unavailable'}}
        $plan=New-DeploymentPlan -Applications @($app) -InstalledState $state
        $plan.Items[0].Action | Should -Be Unavailable
        $plan.Items[0].Sources.Count | Should -Be 1
    }
    It 'compares final observed versions and system settings' {
        $left=New-DeploymentPlan -Applications @($app) -InstalledState $state
        $right=New-DeploymentPlan -Applications @($app) -InstalledState $state
        $left.Results=@([pscustomobject]@{AppId='Fixture';Status='Succeeded';InstalledVersion='1.0'})
        $right.Results=@([pscustomobject]@{AppId='Fixture';Status='Succeeded';InstalledVersion='2.0'})
        $left | Add-Member SystemConfig @{Theme='Dark'}
        $right | Add-Member SystemConfig @{Theme='Light'}
        $changes=@(Compare-DeploymentPlan $left $right)
        $changes.Change | Should -Contain ObservedVersionChanged
        $changes.Change | Should -Contain SystemConfigChanged
        $changes[0].AfterVersion | Should -Be '2.0'
    }
}
