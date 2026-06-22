<#
.SYNOPSIS
    WinForge - Telemetry Collector v3.7.2

.DESCRIPTION
    Collects and manages local deployment telemetry for WinForge:
    - Deployment success/failure tracking
    - Installation method statistics
    - Application popularity metrics
    - Performance measurements
    - Local-only data (no external transmission)

.NOTES
    Author: Julien Bombled
    v3.7.2
    Privacy: All telemetry data is stored locally and never transmitted.
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
# Import DirectoryConstants for path management
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-WinForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

$script:TelemetryPath = Get-StatePath -PathKey 'TelemetryData'

# Import Core module for logging
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

# === TELEMETRY STATE ===
$script:TelemetryData = @{
    Version = '1.0'
    CreatedAt = $null
    LastUpdated = $null
    Deployments = @{
        Total = 0
        Successful = 0
        Failed = 0
        RolledBack = 0
        Sessions = @()
    }
    Applications = @{
        TotalInstalled = 0
        ByMethod = @{}
        ByCategory = @{}
        TopInstalled = @{}
        FailedInstalls = @{}
    }
    Performance = @{
        AverageDeploymentSeconds = 0
        AverageAppInstallSeconds = 0
        TotalDeploymentTime = 0
    }
    Profiles = @{
        Used = @{}
    }
}

# === INITIALIZATION ===

function Initialize-TelemetryCollector {
    <#
    .SYNOPSIS
        Initializes the telemetry collector.

    .DESCRIPTION
        Loads existing telemetry data from disk or creates a new data store.

    .EXAMPLE
        Initialize-TelemetryCollector
    #>
    [CmdletBinding()]
    param()

    if (Test-Path $script:TelemetryPath) {
        try {
            $loaded = Get-Content $script:TelemetryPath -Raw | ConvertFrom-Json

            # Convert JSON to hashtables
            $script:TelemetryData.Version = $loaded.Version
            $script:TelemetryData.CreatedAt = $loaded.CreatedAt
            $script:TelemetryData.LastUpdated = $loaded.LastUpdated

            if ($loaded.Deployments) {
                $script:TelemetryData.Deployments.Total = $loaded.Deployments.Total
                $script:TelemetryData.Deployments.Successful = $loaded.Deployments.Successful
                $script:TelemetryData.Deployments.Failed = $loaded.Deployments.Failed
                $script:TelemetryData.Deployments.RolledBack = $loaded.Deployments.RolledBack
                $script:TelemetryData.Deployments.Sessions = @($loaded.Deployments.Sessions)
            }

            if ($loaded.Applications) {
                $script:TelemetryData.Applications.TotalInstalled = $loaded.Applications.TotalInstalled

                # Convert PSCustomObject to hashtable
                if ($loaded.Applications.ByMethod) {
                    $script:TelemetryData.Applications.ByMethod = @{}
                    foreach ($prop in $loaded.Applications.ByMethod.PSObject.Properties) {
                        $script:TelemetryData.Applications.ByMethod[$prop.Name] = $prop.Value
                    }
                }

                if ($loaded.Applications.ByCategory) {
                    $script:TelemetryData.Applications.ByCategory = @{}
                    foreach ($prop in $loaded.Applications.ByCategory.PSObject.Properties) {
                        $script:TelemetryData.Applications.ByCategory[$prop.Name] = $prop.Value
                    }
                }

                if ($loaded.Applications.TopInstalled) {
                    $script:TelemetryData.Applications.TopInstalled = @{}
                    foreach ($prop in $loaded.Applications.TopInstalled.PSObject.Properties) {
                        $script:TelemetryData.Applications.TopInstalled[$prop.Name] = $prop.Value
                    }
                }

                if ($loaded.Applications.FailedInstalls) {
                    $script:TelemetryData.Applications.FailedInstalls = @{}
                    foreach ($prop in $loaded.Applications.FailedInstalls.PSObject.Properties) {
                        $script:TelemetryData.Applications.FailedInstalls[$prop.Name] = $prop.Value
                    }
                }
            }

            if ($loaded.Performance) {
                $script:TelemetryData.Performance.AverageDeploymentSeconds = $loaded.Performance.AverageDeploymentSeconds
                $script:TelemetryData.Performance.AverageAppInstallSeconds = $loaded.Performance.AverageAppInstallSeconds
                $script:TelemetryData.Performance.TotalDeploymentTime = $loaded.Performance.TotalDeploymentTime
            }

            if ($loaded.Profiles -and $loaded.Profiles.Used) {
                $script:TelemetryData.Profiles.Used = @{}
                foreach ($prop in $loaded.Profiles.Used.PSObject.Properties) {
                    $script:TelemetryData.Profiles.Used[$prop.Name] = $prop.Value
                }
            }

            Write-Verbose "Loaded telemetry data from disk"
        } catch {
            Write-Verbose "Failed to load telemetry data, starting fresh: $($_.Exception.Message)"
            $script:TelemetryData.CreatedAt = (Get-Date).ToString('o')
        }
    } else {
        $script:TelemetryData.CreatedAt = (Get-Date).ToString('o')
    }
}

function Save-TelemetryData {
    <#
    .SYNOPSIS
        Saves telemetry data to disk.
    .DESCRIPTION
        Serializes the in-memory telemetry data structure to a JSON file on disk,
        creating the parent directory if it does not exist. Failures are logged as
        verbose messages without interrupting execution.
    #>
    [CmdletBinding()]
    param()

    $script:TelemetryData.LastUpdated = (Get-Date).ToString('o')

    $directory = Split-Path $script:TelemetryPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    try {
        $script:TelemetryData | ConvertTo-Json -Depth 10 | Set-Content $script:TelemetryPath -Encoding UTF8
        Write-Verbose "Telemetry data saved"
    } catch {
        Write-Verbose "Failed to save telemetry data: $($_.Exception.Message)"
    }
}

# === EVENT RECORDING ===

function Register-DeploymentStart {
    <#
    .SYNOPSIS
        Records the start of a deployment session.
    .DESCRIPTION
        Creates a new deployment session record in the telemetry data, generates a unique
        session ID if not provided, increments the total deployment count, and tracks
        profile usage frequency.

    .PARAMETER ProfileName
        Name of the profile being deployed.

    .PARAMETER SessionId
        Optional session identifier.

    .OUTPUTS
        String containing the session ID.

    .EXAMPLE
        $sessionId = Register-DeploymentStart -ProfileName 'Gaming'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfileName,

        [Parameter()]
        [string]$SessionId
    )

    if (-not $SessionId) {
        $SessionId = [guid]::NewGuid().ToString()
    }

    $session = @{
        SessionId = $SessionId
        ProfileName = $ProfileName
        StartTime = (Get-Date).ToString('o')
        EndTime = $null
        Status = 'Running'
        AppsInstalled = 0
        AppsFailed = 0
        DurationSeconds = 0
    }

    $script:TelemetryData.Deployments.Sessions += $session
    $script:TelemetryData.Deployments.Total++

    # Track profile usage
    if (-not $script:TelemetryData.Profiles.Used.ContainsKey($ProfileName)) {
        $script:TelemetryData.Profiles.Used[$ProfileName] = 0
    }
    $script:TelemetryData.Profiles.Used[$ProfileName]++

    Save-TelemetryData

    return $SessionId
}

function Register-DeploymentEnd {
    <#
    .SYNOPSIS
        Records the end of a deployment session.
    .DESCRIPTION
        Finalizes a deployment session record by setting the end time, calculating duration,
        and updating the success, failure, or rollback counters in the telemetry data.

    .PARAMETER SessionId
        Session identifier from Register-DeploymentStart.

    .PARAMETER Success
        Whether the deployment was successful.

    .PARAMETER RolledBack
        Whether the deployment was rolled back.

    .EXAMPLE
        Register-DeploymentEnd -SessionId $sessionId -Success $true
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionId,

        [Parameter()]
        [bool]$Success = $true,

        [Parameter()]
        [switch]$RolledBack
    )

    $session = $script:TelemetryData.Deployments.Sessions | Where-Object { $_.SessionId -eq $SessionId } | Select-Object -First 1

    if ($session) {
        $session.EndTime = (Get-Date).ToString('o')
        $session.Status = if ($RolledBack) { 'RolledBack' } elseif ($Success) { 'Success' } else { 'Failed' }

        $startTime = [datetime]::Parse($session.StartTime)
        $session.DurationSeconds = ((Get-Date) - $startTime).TotalSeconds

        # Update counters
        if ($Success -and -not $RolledBack) {
            $script:TelemetryData.Deployments.Successful++
        } elseif ($RolledBack) {
            $script:TelemetryData.Deployments.RolledBack++
        } else {
            $script:TelemetryData.Deployments.Failed++
        }

        # Update performance metrics
        $script:TelemetryData.Performance.TotalDeploymentTime += $session.DurationSeconds

        $completedSessions = @($script:TelemetryData.Deployments.Sessions | Where-Object { $_.Status -ne 'Running' })
        if ($completedSessions.Count -gt 0) {
            # Calculate average manually since sessions are hashtables, not objects
            $totalDuration = 0
            foreach ($s in $completedSessions) {
                $totalDuration += $s.DurationSeconds
            }
            $script:TelemetryData.Performance.AverageDeploymentSeconds = [math]::Round(
                $totalDuration / $completedSessions.Count, 2
            )
        }

        Save-TelemetryData
    }
}

function Register-ApplicationInstall {
    <#
    .SYNOPSIS
        Records an application installation.
    .DESCRIPTION
        Tracks an individual application installation in the telemetry data, updating
        counters by installation method, category, and top-installed rankings. Failed
        installations are tracked separately, and session-level counters are updated when
        a session ID is provided.

    .PARAMETER AppName
        Name of the application.

    .PARAMETER Method
        Installation method (Winget, Chocolatey, etc.).

    .PARAMETER Category
        Application category.

    .PARAMETER Success
        Whether installation was successful.

    .PARAMETER DurationSeconds
        Time taken to install.

    .PARAMETER SessionId
        Associated deployment session.

    .EXAMPLE
        Register-ApplicationInstall -AppName 'VSCode' -Method 'Winget' -Category 'Development' -Success $true
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter()]
        [string]$Method = 'Unknown',

        [Parameter()]
        [string]$Category = 'Uncategorized',

        [Parameter()]
        [bool]$Success = $true,

        [Parameter()]
        [double]$DurationSeconds = 0,

        [Parameter()]
        [string]$SessionId
    )

    if ($Success) {
        $script:TelemetryData.Applications.TotalInstalled++

        # Track by method
        if (-not $script:TelemetryData.Applications.ByMethod.ContainsKey($Method)) {
            $script:TelemetryData.Applications.ByMethod[$Method] = 0
        }
        $script:TelemetryData.Applications.ByMethod[$Method]++

        # Track by category
        if (-not $script:TelemetryData.Applications.ByCategory.ContainsKey($Category)) {
            $script:TelemetryData.Applications.ByCategory[$Category] = 0
        }
        $script:TelemetryData.Applications.ByCategory[$Category]++

        # Track top installed
        if (-not $script:TelemetryData.Applications.TopInstalled.ContainsKey($AppName)) {
            $script:TelemetryData.Applications.TopInstalled[$AppName] = 0
        }
        $script:TelemetryData.Applications.TopInstalled[$AppName]++

        # Update session
        if ($SessionId) {
            $session = $script:TelemetryData.Deployments.Sessions | Where-Object { $_.SessionId -eq $SessionId } | Select-Object -First 1
            if ($session) {
                $session.AppsInstalled++
            }
        }
    } else {
        # Track failed installs
        if (-not $script:TelemetryData.Applications.FailedInstalls.ContainsKey($AppName)) {
            $script:TelemetryData.Applications.FailedInstalls[$AppName] = 0
        }
        $script:TelemetryData.Applications.FailedInstalls[$AppName]++

        if ($SessionId) {
            $session = $script:TelemetryData.Deployments.Sessions | Where-Object { $_.SessionId -eq $SessionId } | Select-Object -First 1
            if ($session) {
                $session.AppsFailed++
            }
        }
    }

    Save-TelemetryData
}

# === REPORTING ===

function Get-TelemetrySummary {
    <#
    .SYNOPSIS
        Returns a summary of telemetry data.
    .DESCRIPTION
        Aggregates and formats the telemetry data into a structured summary including
        deployment totals with success rates, top installed applications, installation
        method breakdown, and profile usage statistics.

    .OUTPUTS
        PSCustomObject with telemetry summary.

    .EXAMPLE
        Get-TelemetrySummary | Format-List
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    # Ensure data is loaded
    if (-not $script:TelemetryData.CreatedAt) {
        Initialize-TelemetryCollector
    }

    $successRate = if ($script:TelemetryData.Deployments.Total -gt 0) {
        [math]::Round(($script:TelemetryData.Deployments.Successful / $script:TelemetryData.Deployments.Total) * 100, 1)
    } else { 0 }

    # Get top 10 installed apps
    $topApps = @()
    if ($script:TelemetryData.Applications.TopInstalled.Count -gt 0) {
        $topApps = $script:TelemetryData.Applications.TopInstalled.GetEnumerator() |
            Sort-Object Value -Descending |
            Select-Object -First 10 |
            ForEach-Object { @{ Name = $_.Key; Count = $_.Value } }
    }

    return [PSCustomObject]@{
        DataVersion = $script:TelemetryData.Version
        CreatedAt = $script:TelemetryData.CreatedAt
        LastUpdated = $script:TelemetryData.LastUpdated
        Deployments = @{
            Total = $script:TelemetryData.Deployments.Total
            Successful = $script:TelemetryData.Deployments.Successful
            Failed = $script:TelemetryData.Deployments.Failed
            RolledBack = $script:TelemetryData.Deployments.RolledBack
            SuccessRate = "$successRate%"
        }
        Applications = @{
            TotalInstalled = $script:TelemetryData.Applications.TotalInstalled
            ByMethod = $script:TelemetryData.Applications.ByMethod
            ByCategory = $script:TelemetryData.Applications.ByCategory
            TopInstalled = $topApps
            TotalFailed = ($script:TelemetryData.Applications.FailedInstalls.Values | Measure-Object -Sum).Sum
        }
        Performance = @{
            AverageDeploymentSeconds = $script:TelemetryData.Performance.AverageDeploymentSeconds
            AverageDeploymentMinutes = [math]::Round($script:TelemetryData.Performance.AverageDeploymentSeconds / 60, 1)
            TotalDeploymentHours = [math]::Round($script:TelemetryData.Performance.TotalDeploymentTime / 3600, 2)
        }
        Profiles = @{
            MostUsed = if ($script:TelemetryData.Profiles.Used.Count -gt 0) {
                ($script:TelemetryData.Profiles.Used.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
            } else { 'N/A' }
            UsageStats = $script:TelemetryData.Profiles.Used
        }
    }
}

function Export-TelemetryReport {
    <#
    .SYNOPSIS
        Exports telemetry data for the dashboard.
    .DESCRIPTION
        Generates a JSON file containing telemetry summary data with chart-friendly
        structures for deployment pie charts, method/category bar charts, and top
        applications rankings, suitable for consumption by the HTML dashboard.

    .PARAMETER OutputPath
        Path for the output JSON file.

    .EXAMPLE
        Export-TelemetryReport -OutputPath "C:\Dashboard\data.json"
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$OutputPath
    )

    if (-not $OutputPath) {
        $OutputPath = Join-Path $script:RepositoryRoot 'Assets\Dashboard\telemetry-data.json'
    }

    $summary = Get-TelemetrySummary

    # Add chart-friendly data structures
    $chartData = @{
        summary = $summary
        charts = @{
            deploymentPie = @{
                labels = @('Successful', 'Failed', 'Rolled Back')
                data = @(
                    $script:TelemetryData.Deployments.Successful,
                    $script:TelemetryData.Deployments.Failed,
                    $script:TelemetryData.Deployments.RolledBack
                )
            }
            methodBar = @{
                labels = @($script:TelemetryData.Applications.ByMethod.Keys)
                data = @($script:TelemetryData.Applications.ByMethod.Values)
            }
            categoryBar = @{
                labels = @($script:TelemetryData.Applications.ByCategory.Keys)
                data = @($script:TelemetryData.Applications.ByCategory.Values)
            }
            topAppsBar = @{
                labels = @($summary.Applications.TopInstalled | ForEach-Object { $_.Name })
                data = @($summary.Applications.TopInstalled | ForEach-Object { $_.Count })
            }
        }
        generatedAt = (Get-Date).ToString('o')
    }

    $directory = Split-Path $OutputPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    $chartData | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8

    Write-Verbose "Telemetry report exported to: $OutputPath"
    return $OutputPath
}

function Clear-TelemetryData {
    <#
    .SYNOPSIS
        Clears all telemetry data.
    .DESCRIPTION
        Resets all telemetry data to a fresh initial state and persists the empty structure
        to disk. Requires the Confirm switch to prevent accidental data loss.

    .PARAMETER Confirm
        Requires confirmation.

    .EXAMPLE
        Clear-TelemetryData -Confirm
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$Confirm
    )

    if (-not $Confirm) {
        Write-Warning (Get-LocalizedString -Key 'telemetry.clear_requires_confirm')
        return
    }

    $script:TelemetryData = @{
        Version = '1.0'
        CreatedAt = (Get-Date).ToString('o')
        LastUpdated = $null
        Deployments = @{
            Total = 0
            Successful = 0
            Failed = 0
            RolledBack = 0
            Sessions = @()
        }
        Applications = @{
            TotalInstalled = 0
            ByMethod = @{}
            ByCategory = @{}
            TopInstalled = @{}
            FailedInstalls = @{}
        }
        Performance = @{
            AverageDeploymentSeconds = 0
            AverageAppInstallSeconds = 0
            TotalDeploymentTime = 0
        }
        Profiles = @{
            Used = @{}
        }
    }

    Save-TelemetryData
    Write-Status -Message (Get-LocalizedString -Key 'telemetry.data_cleared') -Level 'Info' -Category 'Telemetry'
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-TelemetryCollector',
    'Register-DeploymentStart',
    'Register-DeploymentEnd',
    'Register-ApplicationInstall',
    'Get-TelemetrySummary',
    'Export-TelemetryReport',
    'Clear-TelemetryData'
)
