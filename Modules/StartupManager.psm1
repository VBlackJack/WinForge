<#
.SYNOPSIS
    Win11Forge - Startup Manager Version: 2.6.0

.DESCRIPTION
    Module for managing Windows startup applications:
    - Detects startup entries (Registry, Startup folders, Task Scheduler)
    - Disables/enables startup applications
    - Lists all startup applications

.NOTES
    Author: Julien Bombled
    Version: 2.6.0
    Requires: PowerShell 5.1+, Administrator privileges
#>

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

# === STARTUP LOCATIONS ===
$script:StartupLocations = @{
    CurrentUserRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    CurrentUserRunOnce = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
    LocalMachineRun = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
    LocalMachineRunOnce = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce'
    LocalMachineRun64 = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'
    CurrentUserStartup = [Environment]::GetFolderPath('Startup')
    CommonStartup = [Environment]::GetFolderPath('CommonStartup')
}

# === HELPER FUNCTIONS ===

<#
.SYNOPSIS
    Gets all startup applications
.DESCRIPTION
    Scans registry and startup folders for applications that run at startup
.EXAMPLE
    $startupApps = Get-StartupApplications
#>
function Get-StartupApplications {
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param()

    $startupApps = @()

    # Registry locations
    foreach ($location in $script:StartupLocations.Keys) {
        if ($location -like '*Startup') {
            # Startup folder
            $path = $script:StartupLocations[$location]
            if (Test-Path $path) {
                $shortcuts = Get-ChildItem -Path $path -Filter "*.lnk" -ErrorAction SilentlyContinue
                foreach ($shortcut in $shortcuts) {
                    $startupApps += [PSCustomObject]@{
                        Name = [System.IO.Path]::GetFileNameWithoutExtension($shortcut.Name)
                        Location = $location
                        Path = $shortcut.FullName
                        Type = 'Shortcut'
                        Command = $null
                    }
                }
            }
        }
        else {
            # Registry key
            $regPath = $script:StartupLocations[$location]
            if (Test-Path $regPath) {
                $items = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
                if ($items) {
                    $items.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | ForEach-Object {
                        $startupApps += [PSCustomObject]@{
                            Name = $_.Name
                            Location = $location
                            Path = $regPath
                            Type = 'Registry'
                            Command = $_.Value
                        }
                    }
                }
            }
        }
    }

    # Task Scheduler startup tasks
    try {
        $tasks = Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object {
            $_.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }
        }

        foreach ($task in $tasks) {
            $startupApps += [PSCustomObject]@{
                Name = $task.TaskName
                Location = 'TaskScheduler'
                Path = $task.TaskPath
                Type = 'ScheduledTask'
                Command = $task.Actions[0].Execute
            }
        }
    }
    catch {
        Write-Status -Message "Could not enumerate scheduled tasks: $($_.Exception.Message)" -Level 'Verbose'
    }

    return $startupApps
}

<#
.SYNOPSIS
    Disables a startup application
.DESCRIPTION
    Removes or disables a startup entry
.PARAMETER Name
    Name of the application to disable (supports wildcards)
.PARAMETER Location
    Specific location to target (optional)
.EXAMPLE
    Disable-StartupApplication -Name "Discord"
.EXAMPLE
    Disable-StartupApplication -Name "*Battle*"
#>
function Disable-StartupApplication {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter()]
        [string]$Location
    )

    $startupApps = Get-StartupApplications

    # Filter by name (supports wildcards)
    $matchingApps = @($startupApps | Where-Object { $_.Name -like $Name })

    if ($Location) {
        $matchingApps = @($matchingApps | Where-Object { $_.Location -eq $Location })
    }

    if ($matchingApps.Count -eq 0) {
        Write-Status -Message "No startup application found matching: $Name" -Level 'Warning'
        return $false
    }

    $disabled = 0
    foreach ($app in $matchingApps) {
        try {
            switch ($app.Type) {
                'Registry' {
                    if ($PSCmdlet.ShouldProcess($app.Name, "Remove from registry: $($app.Path)")) {
                        Remove-ItemProperty -Path $app.Path -Name $app.Name -ErrorAction Stop
                        Write-Status -Message "Disabled: $($app.Name) (Registry: $($app.Location))" -Level 'Success'
                        $disabled++
                    }
                }
                'Shortcut' {
                    if ($PSCmdlet.ShouldProcess($app.Name, "Remove shortcut: $($app.Path)")) {
                        Remove-Item -Path $app.Path -Force -ErrorAction Stop
                        Write-Status -Message "Disabled: $($app.Name) (Shortcut removed)" -Level 'Success'
                        $disabled++
                    }
                }
                'ScheduledTask' {
                    if ($PSCmdlet.ShouldProcess($app.Name, "Disable scheduled task")) {
                        Disable-ScheduledTask -TaskName $app.Name -ErrorAction Stop | Out-Null
                        Write-Status -Message "Disabled: $($app.Name) (Scheduled Task)" -Level 'Success'
                        $disabled++
                    }
                }
            }
        }
        catch {
            Write-Status -Message "Failed to disable $($app.Name): $($_.Exception.Message)" -Level 'Error'
        }
    }

    return $disabled -gt 0
}

<#
.SYNOPSIS
    Disables multiple startup applications
.DESCRIPTION
    Disables a list of startup applications
.PARAMETER ApplicationNames
    Array of application names to disable (supports wildcards)
.EXAMPLE
    Disable-StartupApplications -ApplicationNames @("Discord", "Battle.net", "Steam")
#>
function Disable-StartupApplications {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ApplicationNames
    )

    Write-Status -Message "Disabling startup applications..." -Level 'Info'

    $stats = @{
        Total = $ApplicationNames.Count
        Disabled = 0
        NotFound = 0
    }

    foreach ($appName in $ApplicationNames) {
        Write-Status -Message "Processing: $appName" -Level 'Verbose'

        $result = Disable-StartupApplication -Name $appName -WhatIf:$WhatIfPreference

        if ($result) {
            $stats.Disabled++
        }
        else {
            $stats.NotFound++
        }
    }

    # Summary
    Write-Status -Message "`n=== Startup Applications Management ===" -Level 'Info'
    Write-Status -Message "Total applications processed: $($stats.Total)" -Level 'Info'
    Write-Status -Message "Successfully disabled: $($stats.Disabled)" -Level 'Success'

    if ($stats.NotFound -gt 0) {
        Write-Status -Message "Not found: $($stats.NotFound)" -Level 'Warning'
    }
}

<#
.SYNOPSIS
    Lists all startup applications
.DESCRIPTION
    Displays all applications configured to run at startup
.EXAMPLE
    Show-StartupApplications
#>
function Show-StartupApplications {
    [CmdletBinding()]
    param()

    Write-Status -Message "=== Startup Applications ===" -Level 'Info'

    $startupApps = Get-StartupApplications

    if ($startupApps.Count -eq 0) {
        Write-Status -Message "No startup applications found" -Level 'Info'
        return
    }

    Write-Status -Message "Found $($startupApps.Count) startup applications:" -Level 'Info'
    Write-Host ""

    # Group by location
    $grouped = $startupApps | Group-Object -Property Location

    foreach ($group in $grouped) {
        Write-Host "[$($group.Name)]" -ForegroundColor Cyan
        foreach ($app in $group.Group) {
            Write-Host "  - $($app.Name)" -ForegroundColor Yellow
            if ($app.Command) {
                Write-Host "    Command: $($app.Command)" -ForegroundColor Gray
            }
            elseif ($app.Path -and $app.Type -eq 'Shortcut') {
                Write-Host "    Path: $($app.Path)" -ForegroundColor Gray
            }
        }
        Write-Host ""
    }
}

<#
.SYNOPSIS
    Applies startup blacklist from configuration file
.DESCRIPTION
    Reads startup-blacklist.json and disables configured applications
.PARAMETER ConfigPath
    Path to the startup-blacklist.json file
.EXAMPLE
    Invoke-StartupBlacklist
.EXAMPLE
    Invoke-StartupBlacklist -ConfigPath "C:\Custom\startup-blacklist.json"
#>
function Invoke-StartupBlacklist {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [string]$ConfigPath
    )

    # Default path if not specified
    if ([string]::IsNullOrEmpty($ConfigPath)) {
        $repoRoot = Split-Path $script:ModuleRoot -Parent
        $ConfigPath = Join-Path $repoRoot 'Config\startup-blacklist.json'
    }

    Write-Status -Message "=== Applying Startup Blacklist ===" -Level 'Info'

    # Check if config file exists
    if (-not (Test-Path $ConfigPath)) {
        Write-Status -Message "Startup blacklist config not found: $ConfigPath" -Level 'Warning'
        return
    }

    try {
        # Load configuration
        $config = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json

        Write-Status -Message "Loaded configuration: $($config.Description)" -Level 'Info'
        Write-Status -Message "Version: $($config.Version)" -Level 'Verbose'

        # Filter enabled applications
        $appsToDisable = $config.DisabledApplications | Where-Object { $_.Enabled -eq $true }

        if ($appsToDisable.Count -eq 0) {
            Write-Status -Message "No applications configured for startup removal" -Level 'Info'
            return
        }

        Write-Status -Message "Found $($appsToDisable.Count) applications to disable" -Level 'Info'

        # Extract just the names
        $appNames = $appsToDisable | ForEach-Object { $_.Name }

        # Show what will be disabled
        Write-Host ""
        Write-Host "Applications to disable from startup:" -ForegroundColor Yellow
        foreach ($app in $appsToDisable) {
            Write-Host "  - $($app.Name)" -ForegroundColor White
            if ($app.Reason) {
                Write-Host "    Reason: $($app.Reason)" -ForegroundColor Gray
            }
        }
        Write-Host ""

        # Disable the applications
        Disable-StartupApplications -ApplicationNames $appNames -WhatIf:$WhatIfPreference

        Write-Status -Message "Startup blacklist applied successfully" -Level 'Success'
    }
    catch {
        Write-Status -Message "Error applying startup blacklist: $($_.Exception.Message)" -Level 'Error'
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-StartupApplications',
    'Disable-StartupApplication',
    'Disable-StartupApplications',
    'Show-StartupApplications',
    'Invoke-StartupBlacklist'
)
