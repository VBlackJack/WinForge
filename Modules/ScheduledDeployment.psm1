<#
.SYNOPSIS
    Win11Forge - Scheduled Deployment Manager v3.6.8

.DESCRIPTION
    Module for managing scheduled deployments:
    - Create scheduled tasks for automated deployments
    - List, edit, and cancel scheduled deployments
    - Support for one-time and recurring schedules
    - Integration with Windows Task Scheduler

.NOTES
    Author: Julien Bombled
    v3.6.8
    Requires: PowerShell 5.1+, Administrator privileges
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

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module for i18n support
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === CONSTANTS ===
$script:TaskNamePrefix = 'Win11Forge_Deployment_'
$script:TaskFolder = '\Win11Forge\'
$script:ScheduledDeploymentsPath = Join-Path $script:RepositoryRoot 'Config\scheduled-deployments.json'

# === SCHEDULED DEPLOYMENT CLASS ===

class ScheduledDeploymentInfo {
    [string]$Id
    [string]$ProfileName
    [datetime]$ScheduledTime
    [string]$TriggerType  # OneTime, Daily, Weekly, AtStartup, AtLogon
    [string]$Status       # Pending, Running, Completed, Failed, Cancelled
    [hashtable]$Options
    [datetime]$CreatedAt
    [string]$CreatedBy
    [string]$TaskName
    [datetime]$LastRunTime
    [string]$LastRunResult

    ScheduledDeploymentInfo() {
        $this.Id = [System.Guid]::NewGuid().ToString('N').Substring(0, 8)
        $this.CreatedAt = Get-Date
        $this.CreatedBy = [Environment]::UserName
        $this.Status = 'Pending'
        $this.Options = @{}
    }
}

# === VALIDATION FUNCTIONS ===

function Test-ScheduledTasksAvailable {
    <#
    .SYNOPSIS
        Tests if the ScheduledTasks module is available.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        $null = Get-Command -Name Get-ScheduledTask -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-AdministratorPrivileges {
    <#
    .SYNOPSIS
        Tests if the current session has administrator privileges.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# === TASK SCHEDULER INTEGRATION ===

function New-ScheduledDeployment {
    <#
    .SYNOPSIS
        Creates a new scheduled deployment.

    .DESCRIPTION
        Schedules a Win11Forge deployment to run at a specified time using
        Windows Task Scheduler.

    .PARAMETER ProfileName
        Name of the deployment profile to use.

    .PARAMETER ScheduledTime
        DateTime when the deployment should run.

    .PARAMETER TriggerType
        Type of trigger: OneTime (default), Daily, Weekly, AtStartup, AtLogon.

    .PARAMETER DaysOfWeek
        For Weekly triggers, which days to run (Sunday, Monday, etc.).

    .PARAMETER Parallel
        If specified, run deployment in parallel mode.

    .PARAMETER TestMode
        If specified, run in test mode (dry run).

    .PARAMETER Description
        Optional description for the scheduled deployment.

    .OUTPUTS
        [ScheduledDeploymentInfo] Information about the created scheduled deployment.

    .EXAMPLE
        New-ScheduledDeployment -ProfileName 'Office' -ScheduledTime (Get-Date).AddHours(2)

    .EXAMPLE
        New-ScheduledDeployment -ProfileName 'Base' -TriggerType Daily -ScheduledTime '03:00'
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([ScheduledDeploymentInfo])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ProfileName,

        [Parameter(Mandatory)]
        [datetime]$ScheduledTime,

        [Parameter()]
        [ValidateSet('OneTime', 'Daily', 'Weekly', 'AtStartup', 'AtLogon')]
        [string]$TriggerType = 'OneTime',

        [Parameter()]
        [ValidateSet('Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday')]
        [string[]]$DaysOfWeek,

        [Parameter()]
        [switch]$Parallel,

        [Parameter()]
        [switch]$TestMode,

        [Parameter()]
        [string]$Description
    )

    # Validate prerequisites
    if (-not (Test-ScheduledTasksAvailable)) {
        $msg = Get-LocalizedString -Key 'scheduledDeployment.error.taskSchedulerUnavailable' -DefaultValue 'Windows Task Scheduler module is not available'
        throw [System.NotSupportedException]::new($msg)
    }

    if (-not (Test-AdministratorPrivileges)) {
        $msg = Get-LocalizedString -Key 'core.admin_required' -DefaultValue 'Administrator privileges required'
        throw [System.UnauthorizedAccessException]::new($msg)
    }

    # Validate profile exists
    $profilesDir = Join-Path $script:RepositoryRoot 'Profiles'
    $profilePath = Join-Path $profilesDir "$ProfileName.json"
    if (-not (Test-Path $profilePath)) {
        $msg = Get-LocalizedString -Key 'profile.not_found' -DefaultValue 'Profile not found: {Name}' -Parameters @{ Name = $ProfileName }
        throw [System.IO.FileNotFoundException]::new($msg)
    }

    # Create deployment info
    $deployment = [ScheduledDeploymentInfo]::new()
    $deployment.ProfileName = $ProfileName
    $deployment.ScheduledTime = $ScheduledTime
    $deployment.TriggerType = $TriggerType
    $deployment.TaskName = "$script:TaskNamePrefix$($deployment.Id)"
    $deployment.Options = @{
        Parallel = $Parallel.IsPresent
        TestMode = $TestMode.IsPresent
        DaysOfWeek = $DaysOfWeek
    }

    # Build the action command
    $launcherPath = Join-Path $script:RepositoryRoot 'Deploy-Win11Environment.ps1'
    $arguments = "-ProfileName '$ProfileName' -NonInteractive"

    if ($Parallel) {
        $arguments += ' -Parallel'
    }
    if ($TestMode) {
        $arguments += ' -TestMode'
    }

    # Create the scheduled task
    if ($PSCmdlet.ShouldProcess($deployment.TaskName, 'Create scheduled deployment task')) {
        try {
            # Create the action
            $action = New-ScheduledTaskAction -Execute 'pwsh.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$launcherPath`" $arguments" -WorkingDirectory $script:RepositoryRoot

            # Create the trigger based on type
            $trigger = switch ($TriggerType) {
                'OneTime' {
                    New-ScheduledTaskTrigger -Once -At $ScheduledTime
                }
                'Daily' {
                    New-ScheduledTaskTrigger -Daily -At $ScheduledTime
                }
                'Weekly' {
                    if (-not $DaysOfWeek) {
                        $DaysOfWeek = @('Monday')
                    }
                    New-ScheduledTaskTrigger -Weekly -DaysOfWeek $DaysOfWeek -At $ScheduledTime
                }
                'AtStartup' {
                    New-ScheduledTaskTrigger -AtStartup
                }
                'AtLogon' {
                    New-ScheduledTaskTrigger -AtLogon
                }
            }

            # Create principal (run as SYSTEM for reliability)
            $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest

            # Create settings
            $settings = New-ScheduledTaskSettingsSet `
                -AllowStartIfOnBatteries `
                -DontStopIfGoingOnBatteries `
                -StartWhenAvailable `
                -ExecutionTimeLimit (New-TimeSpan -Hours 4) `
                -RestartCount 3 `
                -RestartInterval (New-TimeSpan -Minutes 5)

            # Build description
            $taskDescription = if ($Description) {
                $Description
            } else {
                "Win11Forge scheduled deployment: Profile '$ProfileName', Created by $($deployment.CreatedBy) on $($deployment.CreatedAt.ToString('yyyy-MM-dd HH:mm'))"
            }

            # Register the task
            $null = Register-ScheduledTask `
                -TaskName $deployment.TaskName `
                -TaskPath $script:TaskFolder `
                -Action $action `
                -Trigger $trigger `
                -Principal $principal `
                -Settings $settings `
                -Description $taskDescription `
                -Force

            # Log success
            $msg = Get-LocalizedString -Key 'scheduledDeployment.created' -DefaultValue 'Scheduled deployment created: {Id}' -Parameters @{ Id = $deployment.Id }
            Write-Status -Message $msg -Level 'Success'

            # Save deployment info to local registry
            Save-ScheduledDeploymentInfo -Deployment $deployment

            return $deployment
        }
        catch {
            $msg = Get-LocalizedString -Key 'scheduledDeployment.error.createFailed' -DefaultValue 'Failed to create scheduled deployment: {Error}' -Parameters @{ Error = $_.Exception.Message }
            Write-Status -Message $msg -Level 'Error'
            throw
        }
    }
}

function Get-ScheduledDeployment {
    <#
    .SYNOPSIS
        Gets information about scheduled deployments.

    .DESCRIPTION
        Retrieves scheduled deployment information from Windows Task Scheduler.

    .PARAMETER Id
        Optional deployment ID to filter by.

    .PARAMETER ProfileName
        Optional profile name to filter by.

    .PARAMETER IncludeCompleted
        Include completed/historical deployments.

    .OUTPUTS
        [ScheduledDeploymentInfo[]] Array of scheduled deployment information.

    .EXAMPLE
        Get-ScheduledDeployment

    .EXAMPLE
        Get-ScheduledDeployment -ProfileName 'Office'
    #>
    [CmdletBinding()]
    [OutputType([ScheduledDeploymentInfo[]])]
    param(
        [Parameter()]
        [string]$Id,

        [Parameter()]
        [string]$ProfileName,

        [Parameter()]
        [switch]$IncludeCompleted
    )

    if (-not (Test-ScheduledTasksAvailable)) {
        $msg = Get-LocalizedString -Key 'scheduledDeployment.error.taskSchedulerUnavailable' -DefaultValue 'Windows Task Scheduler module is not available'
        Write-Status -Message $msg -Level 'Warning'
        return @()
    }

    $results = @()

    try {
        # Get all Win11Forge tasks
        $tasks = Get-ScheduledTask -TaskPath $script:TaskFolder -ErrorAction SilentlyContinue |
            Where-Object { $_.TaskName -like "$script:TaskNamePrefix*" }

        foreach ($task in $tasks) {
            $taskInfo = Get-ScheduledTaskInfo -TaskName $task.TaskName -TaskPath $script:TaskFolder -ErrorAction SilentlyContinue

            # Parse deployment ID from task name
            $deploymentId = $task.TaskName -replace "^$script:TaskNamePrefix", ''

            # Load saved deployment info
            $savedInfo = Get-SavedDeploymentInfo -Id $deploymentId

            $deployment = [ScheduledDeploymentInfo]::new()
            $deployment.Id = $deploymentId
            $deployment.TaskName = $task.TaskName
            $deployment.Status = switch ($task.State) {
                'Ready' { 'Pending' }
                'Running' { 'Running' }
                'Disabled' { 'Cancelled' }
                default { 'Unknown' }
            }

            # Extract info from saved data or task
            if ($savedInfo) {
                $deployment.ProfileName = $savedInfo.ProfileName
                $deployment.TriggerType = $savedInfo.TriggerType
                $deployment.CreatedAt = $savedInfo.CreatedAt
                $deployment.CreatedBy = $savedInfo.CreatedBy
                $deployment.Options = $savedInfo.Options
            }
            else {
                # Parse from task description
                if ($task.Description -match "Profile '([^']+)'") {
                    $deployment.ProfileName = $Matches[1]
                }
                $deployment.TriggerType = 'Unknown'
            }

            # Get trigger time
            if ($task.Triggers.Count -gt 0) {
                $trigger = $task.Triggers[0]
                if ($trigger.StartBoundary) {
                    $deployment.ScheduledTime = [datetime]$trigger.StartBoundary
                }
            }

            # Get last run info
            if ($taskInfo) {
                if ($taskInfo.LastRunTime -and $taskInfo.LastRunTime -ne [datetime]::MinValue) {
                    $deployment.LastRunTime = $taskInfo.LastRunTime
                }
                $deployment.LastRunResult = switch ($taskInfo.LastTaskResult) {
                    0 { 'Success' }
                    267009 { 'Running' }
                    267011 { 'Queued' }
                    default { "Error ($($taskInfo.LastTaskResult))" }
                }
            }

            # Apply filters
            if ($Id -and $deployment.Id -ne $Id) { continue }
            if ($ProfileName -and $deployment.ProfileName -ne $ProfileName) { continue }
            if (-not $IncludeCompleted -and $deployment.Status -eq 'Completed') { continue }

            $results += $deployment
        }
    }
    catch {
        $msg = Get-LocalizedString -Key 'scheduledDeployment.error.listFailed' -DefaultValue 'Failed to list scheduled deployments: {Error}' -Parameters @{ Error = $_.Exception.Message }
        Write-Status -Message $msg -Level 'Warning'
    }

    return $results
}

function Remove-ScheduledDeployment {
    <#
    .SYNOPSIS
        Removes a scheduled deployment.

    .DESCRIPTION
        Cancels and removes a scheduled deployment from Windows Task Scheduler.

    .PARAMETER Id
        The deployment ID to remove.

    .PARAMETER Force
        Remove without confirmation.

    .EXAMPLE
        Remove-ScheduledDeployment -Id 'abc12345'

    .EXAMPLE
        Get-ScheduledDeployment | Remove-ScheduledDeployment -Force
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [string]$Id,

        [Parameter()]
        [switch]$Force
    )

    process {
        if (-not (Test-AdministratorPrivileges)) {
            $msg = Get-LocalizedString -Key 'core.admin_required' -DefaultValue 'Administrator privileges required'
            throw [System.UnauthorizedAccessException]::new($msg)
        }

        $taskName = "$script:TaskNamePrefix$Id"

        if ($Force -or $PSCmdlet.ShouldProcess($taskName, 'Remove scheduled deployment')) {
            try {
                Unregister-ScheduledTask -TaskName $taskName -TaskPath $script:TaskFolder -Confirm:$false -ErrorAction Stop

                # Remove saved info
                Remove-SavedDeploymentInfo -Id $Id

                $msg = Get-LocalizedString -Key 'scheduledDeployment.removed' -DefaultValue 'Scheduled deployment removed: {Id}' -Parameters @{ Id = $Id }
                Write-Status -Message $msg -Level 'Success'
            }
            catch {
                $msg = Get-LocalizedString -Key 'scheduledDeployment.error.removeFailed' -DefaultValue 'Failed to remove scheduled deployment: {Error}' -Parameters @{ Error = $_.Exception.Message }
                Write-Status -Message $msg -Level 'Error'
                throw
            }
        }
    }
}

function Enable-ScheduledDeployment {
    <#
    .SYNOPSIS
        Enables a disabled scheduled deployment.

    .PARAMETER Id
        The deployment ID to enable.

    .EXAMPLE
        Enable-ScheduledDeployment -Id 'abc12345'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [string]$Id
    )

    process {
        if (-not (Test-AdministratorPrivileges)) {
            $msg = Get-LocalizedString -Key 'core.admin_required' -DefaultValue 'Administrator privileges required'
            throw [System.UnauthorizedAccessException]::new($msg)
        }

        $taskName = "$script:TaskNamePrefix$Id"

        try {
            Enable-ScheduledTask -TaskName $taskName -TaskPath $script:TaskFolder -ErrorAction Stop

            $msg = Get-LocalizedString -Key 'scheduledDeployment.enabled' -DefaultValue 'Scheduled deployment enabled: {Id}' -Parameters @{ Id = $Id }
            Write-Status -Message $msg -Level 'Success'
        }
        catch {
            $msg = Get-LocalizedString -Key 'scheduledDeployment.error.enableFailed' -DefaultValue 'Failed to enable scheduled deployment: {Error}' -Parameters @{ Error = $_.Exception.Message }
            Write-Status -Message $msg -Level 'Error'
            throw
        }
    }
}

function Disable-ScheduledDeployment {
    <#
    .SYNOPSIS
        Disables a scheduled deployment without removing it.

    .PARAMETER Id
        The deployment ID to disable.

    .EXAMPLE
        Disable-ScheduledDeployment -Id 'abc12345'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [string]$Id
    )

    process {
        if (-not (Test-AdministratorPrivileges)) {
            $msg = Get-LocalizedString -Key 'core.admin_required' -DefaultValue 'Administrator privileges required'
            throw [System.UnauthorizedAccessException]::new($msg)
        }

        $taskName = "$script:TaskNamePrefix$Id"

        try {
            Disable-ScheduledTask -TaskName $taskName -TaskPath $script:TaskFolder -ErrorAction Stop

            $msg = Get-LocalizedString -Key 'scheduledDeployment.disabled' -DefaultValue 'Scheduled deployment disabled: {Id}' -Parameters @{ Id = $Id }
            Write-Status -Message $msg -Level 'Success'
        }
        catch {
            $msg = Get-LocalizedString -Key 'scheduledDeployment.error.disableFailed' -DefaultValue 'Failed to disable scheduled deployment: {Error}' -Parameters @{ Error = $_.Exception.Message }
            Write-Status -Message $msg -Level 'Error'
            throw
        }
    }
}

function Start-ScheduledDeployment {
    <#
    .SYNOPSIS
        Manually triggers a scheduled deployment to run immediately.

    .PARAMETER Id
        The deployment ID to start.

    .EXAMPLE
        Start-ScheduledDeployment -Id 'abc12345'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [string]$Id
    )

    process {
        if (-not (Test-AdministratorPrivileges)) {
            $msg = Get-LocalizedString -Key 'core.admin_required' -DefaultValue 'Administrator privileges required'
            throw [System.UnauthorizedAccessException]::new($msg)
        }

        $taskName = "$script:TaskNamePrefix$Id"

        try {
            Start-ScheduledTask -TaskName $taskName -TaskPath $script:TaskFolder -ErrorAction Stop

            $msg = Get-LocalizedString -Key 'scheduledDeployment.started' -DefaultValue 'Scheduled deployment started: {Id}' -Parameters @{ Id = $Id }
            Write-Status -Message $msg -Level 'Success'
        }
        catch {
            $msg = Get-LocalizedString -Key 'scheduledDeployment.error.startFailed' -DefaultValue 'Failed to start scheduled deployment: {Error}' -Parameters @{ Error = $_.Exception.Message }
            Write-Status -Message $msg -Level 'Error'
            throw
        }
    }
}

# === PERSISTENCE FUNCTIONS ===

function Save-ScheduledDeploymentInfo {
    <#
    .SYNOPSIS
        Saves deployment info to local storage.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ScheduledDeploymentInfo]$Deployment
    )

    try {
        $deployments = @{}

        if (Test-Path $script:ScheduledDeploymentsPath) {
            $content = Get-Content -Path $script:ScheduledDeploymentsPath -Raw -ErrorAction SilentlyContinue
            if ($content) {
                $existing = $content | ConvertFrom-Json -ErrorAction SilentlyContinue
                if ($existing) {
                    foreach ($prop in $existing.PSObject.Properties) {
                        $deployments[$prop.Name] = $prop.Value
                    }
                }
            }
        }

        $deployments[$Deployment.Id] = @{
            ProfileName = $Deployment.ProfileName
            TriggerType = $Deployment.TriggerType
            CreatedAt = $Deployment.CreatedAt.ToString('o')
            CreatedBy = $Deployment.CreatedBy
            Options = $Deployment.Options
        }

        # Ensure directory exists
        $configDir = Split-Path $script:ScheduledDeploymentsPath -Parent
        if (-not (Test-Path $configDir)) {
            New-Item -Path $configDir -ItemType Directory -Force | Out-Null
        }

        $deployments | ConvertTo-Json -Depth 10 | Set-Content -Path $script:ScheduledDeploymentsPath -Encoding UTF8
    }
    catch {
        Write-Status -Message "Could not save deployment info: $($_.Exception.Message)" -Level 'Warning'
    }
}

function Get-SavedDeploymentInfo {
    <#
    .SYNOPSIS
        Gets saved deployment info from local storage.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Id
    )

    try {
        if (-not (Test-Path $script:ScheduledDeploymentsPath)) {
            return $null
        }

        $content = Get-Content -Path $script:ScheduledDeploymentsPath -Raw -ErrorAction SilentlyContinue
        if (-not $content) {
            return $null
        }

        $deployments = $content | ConvertFrom-Json -ErrorAction SilentlyContinue
        if (-not $deployments) {
            return $null
        }

        if ($deployments.PSObject.Properties[$Id]) {
            $info = $deployments.$Id
            return @{
                ProfileName = $info.ProfileName
                TriggerType = $info.TriggerType
                CreatedAt = [datetime]$info.CreatedAt
                CreatedBy = $info.CreatedBy
                Options = if ($info.Options) {
                    $opts = @{}
                    foreach ($prop in $info.Options.PSObject.Properties) {
                        $opts[$prop.Name] = $prop.Value
                    }
                    $opts
                } else { @{} }
            }
        }

        return $null
    }
    catch {
        return $null
    }
}

function Remove-SavedDeploymentInfo {
    <#
    .SYNOPSIS
        Removes saved deployment info from local storage.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Id
    )

    try {
        if (-not (Test-Path $script:ScheduledDeploymentsPath)) {
            return
        }

        $content = Get-Content -Path $script:ScheduledDeploymentsPath -Raw -ErrorAction SilentlyContinue
        if (-not $content) {
            return
        }

        $deployments = $content | ConvertFrom-Json -ErrorAction SilentlyContinue
        if (-not $deployments) {
            return
        }

        $newDeployments = @{}
        foreach ($prop in $deployments.PSObject.Properties) {
            if ($prop.Name -ne $Id) {
                $newDeployments[$prop.Name] = $prop.Value
            }
        }

        $newDeployments | ConvertTo-Json -Depth 10 | Set-Content -Path $script:ScheduledDeploymentsPath -Encoding UTF8
    }
    catch {
        Write-Status -Message "Could not remove deployment info: $($_.Exception.Message)" -Level 'Warning'
    }
}

# === UTILITY FUNCTIONS ===

function Get-ScheduledDeploymentSummary {
    <#
    .SYNOPSIS
        Gets a summary of all scheduled deployments.

    .OUTPUTS
        [hashtable] Summary statistics.

    .EXAMPLE
        Get-ScheduledDeploymentSummary
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $deployments = @(Get-ScheduledDeployment -IncludeCompleted)

    return @{
        Total = $deployments.Count
        Pending = @($deployments | Where-Object { $_.Status -eq 'Pending' }).Count
        Running = @($deployments | Where-Object { $_.Status -eq 'Running' }).Count
        Completed = @($deployments | Where-Object { $_.Status -eq 'Completed' }).Count
        Failed = @($deployments | Where-Object { $_.Status -eq 'Failed' }).Count
        Cancelled = @($deployments | Where-Object { $_.Status -eq 'Cancelled' }).Count
        NextScheduled = ($deployments | Where-Object { $_.Status -eq 'Pending' -and $_.ScheduledTime -gt (Get-Date) } | Sort-Object ScheduledTime | Select-Object -First 1)
    }
}

function Initialize-ScheduledDeploymentTaskFolder {
    <#
    .SYNOPSIS
        Initializes the Win11Forge task folder in Task Scheduler.

    .DESCRIPTION
        Creates the \Win11Forge\ folder in Task Scheduler if it doesn't exist.
    #>
    [CmdletBinding()]
    param()

    if (-not (Test-AdministratorPrivileges)) {
        return
    }

    try {
        $scheduleService = New-Object -ComObject 'Schedule.Service'
        $scheduleService.Connect()
        $rootFolder = $scheduleService.GetFolder('\')

        try {
            $null = $rootFolder.GetFolder('Win11Forge')
        }
        catch {
            $null = $rootFolder.CreateFolder('Win11Forge')
            Write-Status -Message 'Created Win11Forge task folder' -Level 'Verbose'
        }
    }
    catch {
        # Task folder initialization is non-critical (will be created when needed)
        Write-Verbose "Task folder initialization deferred: $($_.Exception.Message)"
    }
}

# === MODULE INITIALIZATION ===

# Initialize task folder on module load (if admin)
Initialize-ScheduledDeploymentTaskFolder

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'New-ScheduledDeployment',
    'Get-ScheduledDeployment',
    'Remove-ScheduledDeployment',
    'Enable-ScheduledDeployment',
    'Disable-ScheduledDeployment',
    'Start-ScheduledDeployment',
    'Get-ScheduledDeploymentSummary',
    'Test-ScheduledTasksAvailable'
)
