# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

Set-StrictMode -Version Latest

function Get-PlanProperty {
    param($Value, [string]$Name, $Default = $null)
    if ($null -eq $Value) { return $Default }
    if ($Value -is [System.Collections.IDictionary]) {
        if ($Value.Contains($Name)) { return $Value[$Name] }
    } elseif ($Value.PSObject.Properties[$Name]) { return $Value.$Name }
    return $Default
}

function Get-DeploymentSourceEvidence {
    <# .SYNOPSIS Returns evidence for an exact source identity, without upgrading legacy verification claims. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Application, [Parameter(Mandatory)][string]$Source)
    $identifier = Get-PlanProperty (Get-PlanProperty $Application 'Sources') $Source
    $evidence = Get-PlanProperty (Get-PlanProperty $Application 'SourceEvidence') $Source
    $sourceMatches = $evidence -and (Get-PlanProperty $evidence 'Identifier') -ceq $identifier
    [pscustomobject]@{
        Source = $Source
        Identifier = $identifier
        Availability = if ($sourceMatches) { Get-PlanProperty $evidence 'Availability' 'Unknown' } else { 'Unknown' }
        PackageFoundAt = if ($sourceMatches) { Get-PlanProperty $evidence 'PackageFoundAt' } else { $null }
        DownloadVerifiedAt = if ($sourceMatches) { Get-PlanProperty $evidence 'DownloadVerifiedAt' } else { $null }
        InstallationTestedAt = if ($sourceMatches) { Get-PlanProperty $evidence 'InstallationTestedAt' } else { $null }
        Environment = if ($sourceMatches) { Get-PlanProperty $evidence 'Environment' } else { $null }
    }
}

function New-DeploymentPlan {
    <# .SYNOPSIS Previews package operations and freezes their definitions without installing anything. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][PSCustomObject[]]$Applications,
        [string]$ProfileName = 'Selection',
        [hashtable]$InstalledState,
        [switch]$ForceUpdate
    )
    if (-not $PSBoundParameters.ContainsKey('InstalledState')) {
        Import-Module (Join-Path $PSScriptRoot 'ApplicationDetection.psm1') -ErrorAction Stop
        $InstalledState = if ($Applications.Count) { Get-ApplicationsInstallationStatus -Applications $Applications } else { @{} }
    }
    $seen = @{}
    $items = @(foreach ($application in $Applications) {
        $id = [string](Get-PlanProperty $application 'AppId')
        if (-not $id -or $seen.ContainsKey($id)) { throw "Missing or duplicate application ID: $id" }
        $seen[$id] = $true
        $state = $InstalledState[$id]
        if ($null -eq $state -or $null -eq (Get-PlanProperty $state 'IsInstalled')) { throw "Installation state unavailable: $id" }
        $present = [bool](Get-PlanProperty $state 'IsInstalled')
        $custom = Get-PlanProperty $application 'InstallMethod'
        $sources = @(foreach ($source in @('Winget','Chocolatey','Store','DirectUrl')) {
            if (Get-PlanProperty (Get-PlanProperty $application 'Sources') $source) {
                Get-DeploymentSourceEvidence -Application $application -Source $source
            }
        })
        $action = if ($present) { if ($ForceUpdate) { 'Update' } else { 'Skip' } } else { 'Install' }
        if (Get-PlanProperty $application 'ManualInstallOnly' $false) { $action = 'Manual' }
        if (-not $present -and -not $custom -and $sources.Count -eq 0) { $action = 'Unavailable' }
        if (-not $present -and -not $custom -and $sources.Count -gt 0 -and
            @($sources | Where-Object { $_.Availability -ne 'Unavailable' }).Count -eq 0) { $action = 'Unavailable' }
        $sourceLock = Get-PlanProperty $application 'SourceLock'
        if ($sourceLock) {
            $sources = @($sources | Where-Object { $_.Source -eq $sourceLock.Method -and $_.Identifier -ceq $sourceLock.Identifier })
            if ($custom -or $sourceLock.Method -notin @('Winget','Chocolatey') -or $sources.Count -ne 1 -or
                $sourceLock.Version -notmatch '^[A-Za-z0-9][A-Za-z0-9._+-]*$') {
                throw "Invalid source lock: $id"
            }
            if (-not $present -and $sources[0].Availability -eq 'Unavailable') { $action = 'Unavailable' }
        }
        $first = if ($custom) { [string]$custom } elseif ($sources.Count) { $sources[0].Source } else { $null }
        [pscustomobject]@{
            AppId = $id
            Name = $application.Name
            Action = $action
            WasInstalled = $present
            InstalledVersion = Get-PlanProperty $state 'Version'
            PreferredSource = $first
            SourceLock = Get-PlanProperty $application 'SourceLock'
            OriginProfile = Get-PlanProperty $application 'OriginProfile' $ProfileName
            Sources = $sources
            Elevation = if ($action -in @('Skip','Unavailable','Manual')) { 'None' } else { 'MayBeRequired' }
            Reboot = if ($custom -in @('WindowsFeature','WindowsCapability')) { 'Possible' } else { 'Unknown' }
            Rollback = if ($present) { 'NotSupportedForExistingApplication' } elseif ($first -in @('Winget','Chocolatey')) { 'UninstallIfInstalledBySupportedSource' } else { 'Manual' }
            Definition = $application | ConvertTo-Json -Depth 30 | ConvertFrom-Json
        }
    })
    [pscustomobject]@{
        SchemaVersion = 1
        DeploymentId = [guid]::NewGuid().ToString('D')
        CreatedAt = [datetime]::UtcNow.ToString('o')
        ProfileName = $ProfileName
        Machine = $env:COMPUTERNAME
        User = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        ForceUpdate = $ForceUpdate.IsPresent
        State = 'Planned'
        Items = $items
        Results = @()
    }
}

function Save-DeploymentPlan {
    <# .SYNOPSIS Atomically saves a plan or receipt, preserving the previous file on failure. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)][string]$Path)
    $target = [IO.Path]::GetFullPath($Path)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    $temporary = $target + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporary, ($Plan | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
        if ([IO.File]::Exists($target)) { [IO.File]::Replace($temporary, $target, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $target) }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function Read-DeploymentPlan {
    <# .SYNOPSIS Reads a supported deployment receipt and validates identity and unique application IDs. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $plan = [IO.File]::ReadAllText([IO.Path]::GetFullPath($Path)) | ConvertFrom-Json -ErrorAction Stop
    if ($plan.SchemaVersion -ne 1) { throw 'Unsupported deployment plan schema.' }
    $null = [guid]::Parse($plan.DeploymentId)
    $ids = @{}
    foreach ($item in $plan.Items) {
        if (-not $item.AppId -or $ids.ContainsKey($item.AppId) -or $item.Definition.AppId -ne $item.AppId) { throw 'Invalid deployment plan identity.' }
        $ids[$item.AppId] = $true
    }
    return $plan
}

function Compare-DeploymentPlan {
    <# .SYNOPSIS Compares frozen package definitions and observed versions by stable application ID. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Reference, [Parameter(Mandatory)]$Difference)
    $left = @{}; $right = @{}
    foreach ($item in $Reference.Items) { $left[$item.AppId] = $item }
    foreach ($item in $Difference.Items) { $right[$item.AppId] = $item }
    $leftVersions = @{}; $rightVersions = @{}
    foreach ($item in $Reference.Items) { $leftVersions[$item.AppId] = $item.InstalledVersion }
    foreach ($item in $Difference.Items) { $rightVersions[$item.AppId] = $item.InstalledVersion }
    foreach ($receipt in @($Reference.Results)) {
        if ($receipt.Status -eq 'Succeeded') { $leftVersions[$receipt.AppId] = Get-PlanProperty $receipt 'InstalledVersion' }
    }
    foreach ($receipt in @($Difference.Results)) {
        if ($receipt.Status -eq 'Succeeded') { $rightVersions[$receipt.AppId] = Get-PlanProperty $receipt 'InstalledVersion' }
    }
    foreach ($id in @(@($left.Keys) + @($right.Keys) | Sort-Object -Unique)) {
        $change = if (-not $left.ContainsKey($id)) { 'Added' }
            elseif (-not $right.ContainsKey($id)) { 'Removed' }
            elseif (($left[$id].Definition | ConvertTo-Json -Depth 30 -Compress) -cne ($right[$id].Definition | ConvertTo-Json -Depth 30 -Compress)) { 'DefinitionChanged' }
            elseif ($leftVersions[$id] -cne $rightVersions[$id]) { 'ObservedVersionChanged' }
            else { 'Unchanged' }
        [pscustomobject]@{ AppId=$id; Change=$change; Before=$left[$id]; After=$right[$id]; BeforeVersion=$leftVersions[$id]; AfterVersion=$rightVersions[$id] }
    }
    foreach ($field in @('SystemConfig','InheritanceChain','ProfileVersion')) {
        $before = Get-PlanProperty $Reference $field
        $after = Get-PlanProperty $Difference $field
        if (($before | ConvertTo-Json -Depth 30 -Compress) -cne ($after | ConvertTo-Json -Depth 30 -Compress)) {
            [pscustomobject]@{ AppId=$null; Change=($field + 'Changed'); Before=$before; After=$after; BeforeVersion=$null; AfterVersion=$null }
        }
    }
}

function Get-DeploymentRecovery {
    <# .SYNOPSIS Lists failed, uncertain and remaining items, with precise supported rollback candidates. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan)
    foreach ($item in $Plan.Items) {
        $latest = @($Plan.Results | Where-Object { $_.AppId -eq $item.AppId } | Select-Object -Last 1)
        $result = if ($latest.Count) { $latest[0] } else { $null }
        $status = if ($result) { if ($result.Status -eq 'Running') { 'Interrupted' } else { $result.Status } } else { 'Pending' }
        $canUndo = $result -and $status -eq 'Succeeded' -and $result.WasInstalled -eq $false -and $result.Method -in @('Winget','Chocolatey') -and $result.Identifier
        [pscustomobject]@{
            AppId=$item.AppId; Name=$item.Name; Status=$status
            Retry=$status -in @('Pending','Failed','Interrupted')
            CanRollback=[bool]$canUndo
            Method=Get-PlanProperty $result 'Method'
            Identifier=Get-PlanProperty $result 'Identifier'
            InstalledVersion=Get-PlanProperty $result 'InstalledVersion'
            Message=Get-PlanProperty $result 'Message'
        }
    }
}

function Invoke-DeploymentPlan {
    <# .SYNOPSIS Executes frozen definitions and checkpoints each attempt; an interrupted attempt requires reconciliation. #>
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Path, [switch]$RetryFailed)
    $target = [IO.Path]::GetFullPath($Path)
    # Hold a separate exclusive lease across the entire operation, including atomic receipt replacement.
    $lease = [IO.File]::Open($target + '.lock', [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $plan = Read-DeploymentPlan -Path $target
        if ($plan.Machine -ne $env:COMPUTERNAME -or $plan.User -ne [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value) {
            throw 'Create a fresh plan on this machine and account before executing.'
        }
        Import-Module (Join-Path $PSScriptRoot 'InstallationOrchestrator.psm1') -ErrorAction Stop
        Import-Module (Join-Path $PSScriptRoot 'ApplicationDetection.psm1') -ErrorAction Stop
        foreach ($item in $plan.Items) {
            $latest = @($plan.Results | Where-Object { $_.AppId -eq $item.AppId } | Select-Object -Last 1)
            if ($latest.Count -and $latest[0].Status -notin @('Failed','Interrupted','Running')) { continue }
            if ($latest.Count -and -not $RetryFailed) { continue }
            if ($item.Action -in @('Manual','Unavailable')) { continue }
            if (-not $PSCmdlet.ShouldProcess($item.Name, $item.Action)) { continue }
            $before = Test-ApplicationInstalled -Application $item.Definition
            # Never attribute an installation found after an interrupted attempt to this journal.
            $attempt = [pscustomobject]@{
                AppId=$item.AppId; StartedAt=[datetime]::UtcNow.ToString('o'); CompletedAt=$null
                Status='Running'; WasInstalled=[bool]$before; Method=$null; Identifier=$null
                InstalledVersion=$null; Message=$null
            }
            $plan.Results = @($plan.Results) + @($attempt)
            $plan.State = 'Running'
            Save-DeploymentPlan -Plan $plan -Path $target
            try {
                $output = @(Install-Application -Application $item.Definition -ForceUpdate:([bool]$plan.ForceUpdate) -SkipRollbackJournal)
                $result = $output[-1]
                if ($result.AlreadyInstalled -or (Get-PlanProperty $result 'WasInstalled' $true)) {
                    $attempt.WasInstalled = $true
                }
                $attempt.Status = if ($result.Success) { 'Succeeded' } else { 'Failed' }
                $attempt.Method = $result.Method
                $source = if ($result.Method -eq 'DirectDownload') { 'DirectUrl' } else { $result.Method }
                if ($source) { $attempt.Identifier = Get-PlanProperty (Get-PlanProperty $item.Definition 'Sources') $source }
                $attempt.Message = $result.Message
                if ($result.Success) {
                    try {
                        Clear-RegistryAppsCache
                        $observed = Get-ApplicationsInstallationStatus -Applications @($item.Definition) -Refresh
                        $attempt.InstalledVersion = $observed[$item.AppId].Version
                    } catch { $attempt.Message += ' Version observation unavailable: ' + $_.Exception.Message }
                }
            } catch {
                $attempt.Status = 'Failed'
                $attempt.Message = $_.Exception.Message
            }
            $attempt.CompletedAt = [datetime]::UtcNow.ToString('o')
            Save-DeploymentPlan -Plan $plan -Path $target
        }
        if (-not $WhatIfPreference) {
            $remaining = @(Get-DeploymentRecovery -Plan $plan | Where-Object { $_.Status -ne 'Succeeded' -and $_.Status -ne 'RolledBack' })
            $plan.State = if ($remaining.Count) { 'Partial' } else { 'Completed' }
            Save-DeploymentPlan -Plan $plan -Path $target
        }
        return $plan
    } finally { $lease.Dispose() }
}

function Undo-DeploymentPlan {
    <# .SYNOPSIS Uninstalls only new packages recorded by this deployment using supported exact package identities. #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][string]$Path)
    $target = [IO.Path]::GetFullPath($Path)
    $lease = [IO.File]::Open($target + '.lock', [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $plan = Read-DeploymentPlan -Path $target
        if ($plan.Machine -ne $env:COMPUTERNAME -or $plan.User -ne [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value) { throw 'Rollback belongs to another machine or account.' }
        Import-Module (Join-Path (Split-Path $PSScriptRoot -Parent) 'Core/Core.psm1') -ErrorAction Stop
        Import-Module (Join-Path $PSScriptRoot 'ApplicationDetection.psm1') -ErrorAction Stop
        foreach ($entry in @(Get-DeploymentRecovery -Plan $plan | Where-Object CanRollback)) {
            if ($entry.Identifier -notmatch '^[A-Za-z0-9][A-Za-z0-9._+-]*$') { throw 'Unsafe rollback package identifier.' }
            if (-not $PSCmdlet.ShouldProcess("$($entry.Method):$($entry.Identifier)", 'Uninstall newly installed package')) { continue }
            $result = @($plan.Results | Where-Object { $_.AppId -eq $entry.AppId } | Select-Object -Last 1)[0]
            try {
            $native = if ($entry.Method -eq 'Winget') {
                Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList @('uninstall','--id',$entry.Identifier,'--exact','--source','winget','--silent','--accept-source-agreements')
            } else {
                Invoke-NativeCommandUtf8 -FilePath 'choco' -ArgumentList @('uninstall',$entry.Identifier,'-y','--no-progress')
            }
            if ($native.ExitCode -eq 0) {
                $item = @($plan.Items | Where-Object AppId -eq $entry.AppId)[0]
                $observed = Get-ApplicationsInstallationStatus -Applications @($item.Definition) -Refresh
                $state = $observed[$entry.AppId]
                if ($null -eq $state -or $null -eq (Get-PlanProperty $state 'IsInstalled')) {
                    $result.Message = 'Rollback could not be verified by fresh application detection.'
                } elseif (Get-PlanProperty $state 'IsInstalled') {
                    $result.Message = 'Rollback command succeeded, but fresh detection still finds the application installed.'
                } else {
                    $result.Status = 'RolledBack'
                    $result.Message = 'Rollback completed; fresh detection confirms the application is absent.'
                }
            }
            else { $result.Message = "Rollback failed with exit code $($native.ExitCode)." }
            } catch { $result.Message = 'Rollback failed: ' + $_.Exception.Message }
            Save-DeploymentPlan -Plan $plan -Path $target
        }
        return $plan
    } finally { $lease.Dispose() }
}

Export-ModuleMember -Function Get-DeploymentSourceEvidence, New-DeploymentPlan, Save-DeploymentPlan, Read-DeploymentPlan, Compare-DeploymentPlan, Get-DeploymentRecovery, Invoke-DeploymentPlan, Undo-DeploymentPlan
