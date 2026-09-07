# Copyright 2026 Julien Bombled. Licensed under the Apache License, Version 2.0.
# All package managers and Task Scheduler mutations are replaced by inert doubles.
BeforeAll {
    $script:Root = Split-Path $PSScriptRoot -Parent
    Import-Module "$script:Root/Modules/InstallationEngine.psd1" -Force
    Import-Module "$script:Root/Modules/InstallationOrchestrator.psm1" -Force
    Import-Module "$script:Root/Modules/RollbackManager.psm1" -Force
    Import-Module "$script:Root/Modules/ScheduledDeployment.psm1" -Force
}

Describe 'Durable rollback integration' {
    BeforeEach {
        $modules = @(Get-Module StateManager -All) + @((Get-Command Get-RollbackState).ScriptBlock.Module) + @(
            & (Get-Module InstallationOrchestrator) { (Get-Command Get-RollbackState).ScriptBlock.Module }
        )
        $script:StatePaths = @()
        foreach ($module in $modules) {
            $script:StatePaths += @{ Module=$module; Path=(& $module { $script:RollbackStateFile }) }
        }
        foreach ($module in $modules) {
            & $module { param($Path) $script:RollbackStateFile = $Path } "$TestDrive/rollback.json"
        }
        Clear-RollbackState
        Mock Write-Status {} -ModuleName StateManager
        Mock Write-Status {} -ModuleName InstallationOrchestrator
        Mock Test-EnvironmentRestriction { @{ Restricted = $false } } -ModuleName InstallationOrchestrator
        Mock Test-ApplicationInstalled { $false } -ModuleName InstallationOrchestrator
        Mock Install-ViaWinget { $true } -ModuleName InstallationOrchestrator
        $script:App = [pscustomobject]@{
            Name = 'Fixture'; Sources = [pscustomobject]@{
                Winget='Fixture.Id'; Chocolatey=$null; Store=$null; DirectUrl=$null
            }
        }
    }

    AfterEach {
        foreach ($saved in $script:StatePaths) {
            & $saved.Module { param($Path) $script:RollbackStateFile = $Path } $saved.Path
        }
    }

    It 'journals the actual successful orchestration result' {
        $result = InModuleScope InstallationOrchestrator -Parameters @{ App=$script:App } { param($App) Install-Application -Application $App }
        $result.Success | Should -BeTrue
        $state = Get-RollbackState
        $state.InstalledApps.Count | Should -Be 1
        $state.InstalledApps[0].Identifier | Should -Be 'Fixture.Id'
    }

    It 'keeps plan-owned installations out of the legacy rollback journal' {
        $result = Install-Application -Application $script:App -SkipRollbackJournal
        $result.Success | Should -BeTrue
        @((Get-RollbackState).InstalledApps).Count | Should -Be 0
    }

    It 'does not journal a forced reinstall of an existing application' {
        Mock Test-ApplicationInstalled { $true } -ModuleName InstallationOrchestrator
        $result = InModuleScope InstallationOrchestrator -Parameters @{ App=$script:App } { param($App) Install-Application -Application $App -Force }
        $result.Success | Should -BeTrue
        (Get-RollbackState).InstalledApps.Count | Should -Be 0
    }

    It 'preserves the previous bytes and propagates a failed atomic replacement' {
        $null = Initialize-RollbackSession
        $path = "$TestDrive/rollback.json"
        $before = [IO.File]::ReadAllText($path)
        $stream = [IO.File]::Open($path, 'Open', 'Read', 'Read')
        try { { Add-RollbackEntry -AppName Fixture -Method Winget -Identifier Fixture.Id } | Should -Throw }
        finally { $stream.Dispose() }
        [IO.File]::ReadAllText($path) | Should -BeExactly $before
        @(Get-ChildItem $TestDrive -Filter '*.tmp').Count | Should -Be 0
    }

    It 'refuses to overwrite corrupt recovery evidence' {
        [IO.File]::WriteAllText("$TestDrive/rollback.json", '{broken')
        { Add-RollbackEntry -AppName Fixture -Method Winget -Identifier Fixture.Id } | Should -Throw
        [IO.File]::ReadAllText("$TestDrive/rollback.json") | Should -BeExactly '{broken'
    }

    It 'does not return a session id when its destination cannot be written' {
        $module = (Get-Command Initialize-RollbackSession).ScriptBlock.Module
        & $module { param($Path) $script:RollbackStateFile=$Path } $TestDrive
        { Initialize-RollbackSession } | Should -Throw
    }

    It 'keeps failed entries and removes only successfully uninstalled entries' {
        Add-RollbackEntry -AppName Good -Method Winget -Identifier Fixture.Good
        Add-RollbackEntry -AppName Unsupported -Method DirectDownload
        Mock Get-Command { [pscustomobject]@{ Name='winget' } } -ModuleName InstallationOrchestrator -ParameterFilter { $Name -eq 'winget' }
        Mock Invoke-NativeCommandUtf8 { @{ ExitCode=0 } } -ModuleName InstallationOrchestrator
        $receipt = InModuleScope InstallationOrchestrator { Invoke-Rollback -Force }
        $receipt.Success | Should -BeFalse
        $receipt.RolledBack | Should -Contain Good
        $receipt.Failed | Should -Contain Unsupported
        $remaining = (Get-RollbackState).InstalledApps
        $remaining.Count | Should -Be 1
        $remaining[0].AppName | Should -Be Unsupported
        Should -Invoke Invoke-NativeCommandUtf8 -ModuleName InstallationOrchestrator -Times 1 -Exactly
    }

    It 'normalizes legacy PackageId records' {
        $null = Initialize-RollbackSession
        Add-RollbackEntry -AppName Fixture -Method Winget -Identifier Fixture.Id
        $json = [IO.File]::ReadAllText("$TestDrive/rollback.json").Replace('"Identifier"', '"PackageId"')
        [IO.File]::WriteAllText("$TestDrive/rollback.json", $json)
        (Get-RollbackState).InstalledApps[0].Identifier | Should -Be Fixture.Id
    }

    It 'renders the canonical Identifier in the rollback summary' {
        Mock Import-Module {} -ModuleName RollbackManager
        Mock Get-RollbackState {
            @{ SessionId=[guid]::NewGuid().ToString(); StartTime='2026-09-07'; InstalledApps=@(
                @{ AppName='Fixture'; Method='Winget'; Identifier='Fixture.Id'; InstalledAt='2026-09-07' }
            ) }
        } -ModuleName RollbackManager
        (Get-RollbackSummary).Applications[0].PackageId | Should -Be Fixture.Id
    }
}

Describe 'Parallel worker journal integration' {
    It 'commits concurrent successful early returns and excludes preexisting apps' -Skip:($PSVersionTable.PSVersion.Major -lt 7) {
        $fixture = Join-Path $TestDrive 'parallel'
        $null = New-Item -ItemType Directory -Force "$fixture/Core", "$fixture/Modules", "$fixture/Config", "$fixture/state"
        Copy-Item "$script:Root/Modules/StateManager.psm1" "$fixture/Modules/StateManager.psm1"
        Copy-Item "$script:Root/Modules/InstallationOrchestrator.psm1" "$fixture/Modules/InstallationOrchestrator.psm1"
        'function Test-FeatureEnabled { param($FeatureName) return $false }' | Set-Content "$fixture/Core/FeatureFlags.psm1"
        'function Get-SystemEnvironmentType { return "Physical" }; function Test-IsWindowsSandbox { return $false }' | Set-Content "$fixture/Modules/EnvironmentDetection.psm1"
        'function Test-ApplicationInstalled { param($Application) return $Application.Name -eq "Existing" }' | Set-Content "$fixture/Modules/ApplicationDetection.psm1"
        'function Invoke-CustomInstallMethod { param($Application) return @{ ApplicationName=$Application.Name; Success=$true; AlreadyInstalled=$false; Method="WindowsFeature"; Message="Fixture" } }' | Set-Content "$fixture/Modules/InstallationMethods.psm1"
        '{"Version":"fixture"}' | Set-Content "$fixture/Config/version.json"
        @'
function Write-Status { param($Message,$Level,$Category) }
function Get-WinForgeDirectory { param($DirectoryType) return (Join-Path (Split-Path $PSScriptRoot -Parent) 'state') }
function Get-WindowsOptionalFeature { param([switch]$Online,$FeatureName) return @{ State='Disabled' } }
function Enable-WindowsOptionalFeature { param([switch]$Online,$FeatureName,[switch]$NoRestart) }
'@ | Set-Content "$fixture/Core/Core.psm1"
        'function Get-LogString { param($Key,$Parameters) return $Key }' | Set-Content "$fixture/Core/Localization.psm1"
        'function Test-AppInstalledParallel { param($App) return $App.Name -eq "Existing" }' | Set-Content "$fixture/Modules/ParallelDetection.psm1"
        $apps = @('First','Second','Existing') | ForEach-Object {
            [pscustomobject]@{ Name=$_; Priority=1; EnvironmentRestrictions=@(); InstallMethod='WindowsFeature'; Detection=[pscustomobject]@{ Feature='InertFixture' } }
        }
        $module = (Get-Command Install-ApplicationsParallel).ScriptBlock.Module
        $previous = & $module { $script:RepositoryRoot }
        try {
            & $module { param($Path) $script:RepositoryRoot=$Path } $fixture
            $results = @(Install-ApplicationsParallel -Applications $apps -MaxParallel 3 -Force)
            $results.Count | Should -Be 3
            @($results | Where-Object { -not $_.Success }).Count | Should -Be 0
            $state = Get-Content "$fixture/state/RollbackState.json" -Raw | ConvertFrom-Json
            $state.InstalledApps.Count | Should -Be 2
            $state.InstalledApps.AppName | Should -Contain First
            $state.InstalledApps.AppName | Should -Contain Second
            $state.InstalledApps.AppName | Should -Not -Contain Existing
        } finally { & $module { param($Path) $script:RepositoryRoot=$Path } $previous }
    }
}

Describe 'Rollback UI receipt contract' {
    BeforeEach {
        Mock Import-Module {} -ModuleName RollbackManager
        Mock Get-RollbackSummary { @{ TotalApps=2; Applications=@() } } -ModuleName RollbackManager
        Mock Invoke-Rollback { @{ Success=$false; RolledBack=@('Good'); Failed=@('Bad') } } -ModuleName RollbackManager
    }
    It 'returns one failed receipt with the actual success count' {
        $receipt = @(Invoke-RollbackWithConfirmation -Force)
        $receipt.Count | Should -Be 1
        $receipt[0].Success | Should -BeFalse
        $receipt[0].AppsRolledBack | Should -Be 1
        $receipt[0].Errors | Should -Contain Bad
    }
}

Describe 'Scheduled profile snapshot and native arguments' {
    BeforeEach {
        $script:FixtureRoot = Join-Path $TestDrive 'repository with spaces'
        $script:UserProfiles = Join-Path $TestDrive 'user profiles'
        $script:Defaults = Join-Path $TestDrive 'defaults'
        $null = New-Item -ItemType Directory -Force $script:FixtureRoot, $script:UserProfiles, $script:Defaults
        '{"Name":"Office","Inherits":["Base"],"Applications":[]}' | Set-Content "$script:UserProfiles/Office.json"
        '{"Name":"Base","Applications":["User.App"]}' | Set-Content "$script:UserProfiles/Base.json"
        '{"Name":"Base","Applications":["Default.App"]}' | Set-Content "$script:Defaults/Base.json"
        '[CmdletBinding()] param([string]$ProfileName) [IO.File]::ReadAllText($ProfileName)' | Set-Content "$script:FixtureRoot/Deploy-Win11Environment.ps1"
        InModuleScope ScheduledDeployment -Parameters @{ Root=$script:FixtureRoot; Snapshot="$TestDrive/snapshots" } {
            param($Root,$Snapshot)
            $script:RepositoryRoot=$Root
            $script:ProfileSnapshotRoot=$Snapshot
        }
        Mock Test-ScheduledTasksAvailable { $true } -ModuleName ScheduledDeployment
        Mock Test-AdministratorPrivileges { $true } -ModuleName ScheduledDeployment
        Mock Protect-ScheduledProfileDirectory {} -ModuleName ScheduledDeployment
        Mock Save-ScheduledDeploymentInfo {} -ModuleName ScheduledDeployment
        Mock Register-ScheduledTask {} -ModuleName ScheduledDeployment
    }

    It 'captures inherited user profiles and executes the exact native action' {
        Mock Register-ScheduledTask {
            $start = [Diagnostics.ProcessStartInfo]::new('pwsh', $Action.Arguments)
            $start.UseShellExecute=$false
            $start.CreateNoWindow=$true
            $start.RedirectStandardOutput=$true
            $start.RedirectStandardError=$true
            $process=[Diagnostics.Process]::Start($start)
            $output=$process.StandardOutput.ReadToEnd()
            $errorText=$process.StandardError.ReadToEnd()
            $process.WaitForExit()
            try {
                $process.ExitCode | Should -Be 0 -Because $errorText
                ($output | ConvertFrom-Json).Name | Should -Be Office
            } finally { $process.Dispose() }
        } -ModuleName ScheduledDeployment
        $deployment = New-ScheduledDeployment -ProfileName Office -ProfileDirectories @($script:UserProfiles,$script:Defaults) -ScheduledTime (Get-Date).AddHours(1)
        $snapshot = "$TestDrive/snapshots/$($deployment.Id)"
        (Get-Content "$snapshot/Base.json" -Raw | ConvertFrom-Json).Applications | Should -Contain User.App
        '{"Name":"Base","Applications":[]}' | Set-Content "$script:UserProfiles/Base.json"
        (Get-Content "$snapshot/Base.json" -Raw | ConvertFrom-Json).Applications | Should -Contain User.App
        Should -Invoke Register-ScheduledTask -ModuleName ScheduledDeployment -Times 1 -Exactly
    }

    It 'rejects cyclic inheritance before registering a task' {
        '{"Name":"Base","Inherits":["Office"]}' | Set-Content "$script:UserProfiles/Base.json"
        { New-ScheduledDeployment -ProfileName Office -ProfileDirectories @($script:UserProfiles,$script:Defaults) -ScheduledTime (Get-Date).AddHours(1) } | Should -Throw
        Should -Invoke Register-ScheduledTask -ModuleName ScheduledDeployment -Times 0 -Exactly
    }
}
