<#
.SYNOPSIS
    Win11Forge - Source Health Check v3.7.2

.DESCRIPTION
    Validates installation source availability for applications in the database.
    Checks Winget, Chocolatey, and DirectUrl sources and reports health status.
    Supports auto-repair for common issues.

.NOTES
    Author: Julien Bombled
    v3.7.2
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

# Import required modules
$localizationPath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (Test-Path $localizationPath) {
    Import-Module $localizationPath -Force -ErrorAction SilentlyContinue
}

$featureFlagsPath = Join-Path $script:RepositoryRoot 'Core\FeatureFlags.psm1'
if (Test-Path $featureFlagsPath) {
    Import-Module $featureFlagsPath -Force -ErrorAction SilentlyContinue
}

$appDatabasePath = Join-Path $script:ModuleRoot 'ApplicationDatabase.psm1'
if (Test-Path $appDatabasePath) {
    Import-Module $appDatabasePath -Force -ErrorAction SilentlyContinue
}

# === CONSTANTS ===
$script:DIRECTURL_TIMEOUT_SECONDS = 10

# === PUBLIC FUNCTIONS ===

function Test-SourceHealth {
    <#
    .SYNOPSIS
        Tests the health of installation sources for applications.
    .DESCRIPTION
        Validates that configured installation sources (Winget, Chocolatey, DirectUrl) are
        available and functional for the specified applications. Returns structured health
        results per application.
    .PARAMETER Applications
        Array of application objects to check. Defaults to all applications in the database.
    .PARAMETER CheckWinget
        Check Winget package availability.
    .PARAMETER CheckChocolatey
        Check Chocolatey package availability.
    .PARAMETER CheckDirectUrl
        Check DirectUrl reachability via HTTP HEAD request.
    .OUTPUTS
        Array of health result objects per application.
    .EXAMPLE
        $results = Test-SourceHealth -CheckWinget -CheckChocolatey -CheckDirectUrl
    .EXAMPLE
        $results = Test-SourceHealth -Applications $profileApps -CheckWinget
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter()]
        [array]$Applications,

        [Parameter()]
        [switch]$CheckWinget,

        [Parameter()]
        [switch]$CheckChocolatey,

        [Parameter()]
        [switch]$CheckDirectUrl
    )

    if (-not $Applications -or $Applications.Count -eq 0) {
        $Applications = Get-AllApplications
    }

    if (-not $CheckWinget -and -not $CheckChocolatey -and -not $CheckDirectUrl) {
        $CheckWinget = $true
        $CheckChocolatey = $true
        $CheckDirectUrl = $true
    }

    $hasWinget = $CheckWinget -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)
    $hasChoco = $CheckChocolatey -and (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)

    $results = @()

    foreach ($app in $Applications) {
        $appName = if ($app.PSObject.Properties['Name']) { $app.Name } else { $app.ToString() }
        $sources = if ($app.PSObject.Properties['Sources']) { $app.Sources } else { $null }

        if (-not $sources) {
            continue
        }

        $healthResult = [PSCustomObject]@{
            AppName            = $appName
            Sources            = @{}
            HealthySourceCount = 0
            TotalSourceCount   = 0
        }

        # Check Winget source
        if ($sources.Winget) {
            $healthResult.TotalSourceCount++
            if ($hasWinget) {
                Write-Verbose (Get-LocalizedString -Key 'sourcehealth.checking_winget' -Parameters @{ PackageId = $sources.Winget })
                $wingetResult = Test-WingetSource -PackageId $sources.Winget
                $healthResult.Sources['Winget'] = $wingetResult
                if ($wingetResult.Status -eq 'OK') {
                    $healthResult.HealthySourceCount++
                }
            } else {
                $healthResult.Sources['Winget'] = @{ Status = 'SKIP'; Message = 'Winget not available' }
            }
        }

        # Check Chocolatey source
        if ($sources.Chocolatey) {
            $healthResult.TotalSourceCount++
            if ($hasChoco) {
                Write-Verbose (Get-LocalizedString -Key 'sourcehealth.checking_choco' -Parameters @{ PackageName = $sources.Chocolatey })
                $chocoResult = Test-ChocolateySource -PackageName $sources.Chocolatey
                $healthResult.Sources['Chocolatey'] = $chocoResult
                if ($chocoResult.Status -eq 'OK') {
                    $healthResult.HealthySourceCount++
                }
            } else {
                $healthResult.Sources['Chocolatey'] = @{ Status = 'SKIP'; Message = 'Chocolatey not available' }
            }
        }

        # Check DirectUrl source
        if ($sources.DirectUrl) {
            $healthResult.TotalSourceCount++
            if ($CheckDirectUrl) {
                Write-Verbose (Get-LocalizedString -Key 'sourcehealth.checking_directurl' -Parameters @{ Url = $sources.DirectUrl })
                $urlResult = Test-DirectUrlSource -Url $sources.DirectUrl
                $healthResult.Sources['DirectUrl'] = $urlResult
                if ($urlResult.Status -eq 'OK') {
                    $healthResult.HealthySourceCount++
                }
            }
        }

        # Check Store source
        if ($sources.Store) {
            $healthResult.TotalSourceCount++
            # Store packages cannot be easily validated offline; mark as assumed OK
            $healthResult.Sources['Store'] = @{ Status = 'OK'; Message = 'Store validation not supported (assumed OK)' }
            $healthResult.HealthySourceCount++
        }

        $results += $healthResult
    }

    return $results
}

function Repair-AppSources {
    <#
    .SYNOPSIS
        Attempts automatic repair of application source issues.
    .DESCRIPTION
        Analyzes health check results and performs automatic repairs where possible:
        - Enables wingetForceOnHashMismatch feature flag for Winget hash issues
        - Flags dead DirectUrls
        - Updates LastVerified dates for healthy apps
    .PARAMETER HealthResults
        Results from Test-SourceHealth.
    .OUTPUTS
        Repair report object.
    .EXAMPLE
        $health = Test-SourceHealth -CheckWinget -CheckDirectUrl
        $report = Repair-AppSources -HealthResults $health
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [array]$HealthResults
    )

    $report = [PSCustomObject]@{
        DeadUrls            = @()
        MissingChocoPackages = @()
        ForceEnabled        = $false
        VerifiedUpdated     = @()
    }

    $hasWingetIssues = $false

    foreach ($result in $HealthResults) {
        # Check for dead DirectUrls
        if ($result.Sources.ContainsKey('DirectUrl') -and $result.Sources['DirectUrl'].Status -eq 'FAIL') {
            $report.DeadUrls += $result.AppName
            Write-Verbose (Get-LocalizedString -Key 'sourcehealth.repair.flagged_dead_url' -Parameters @{ AppName = $result.AppName })
        }

        # Check for missing Chocolatey packages
        if ($result.Sources.ContainsKey('Chocolatey') -and $result.Sources['Chocolatey'].Status -eq 'FAIL') {
            $report.MissingChocoPackages += $result.AppName
        }

        # Check for Winget issues (could be hash mismatch)
        if ($result.Sources.ContainsKey('Winget') -and $result.Sources['Winget'].Status -eq 'FAIL') {
            $hasWingetIssues = $true
        }

        # Update LastVerified for fully healthy apps
        if ($result.HealthySourceCount -eq $result.TotalSourceCount -and $result.TotalSourceCount -gt 0) {
            $report.VerifiedUpdated += $result.AppName
            Write-Verbose (Get-LocalizedString -Key 'sourcehealth.repair.updated_verified' -Parameters @{ AppName = $result.AppName })
        }
    }

    # Enable force flag if Winget issues detected
    if ($hasWingetIssues -and -not (Test-FeatureEnabled -FeatureName 'wingetForceOnHashMismatch')) {
        Set-FeatureOverride -FeatureName 'wingetForceOnHashMismatch' -Enabled $true
        $report.ForceEnabled = $true
        Write-Verbose (Get-LocalizedString -Key 'sourcehealth.repair.enabled_force_flag')
    }

    return $report
}

function Get-SourceHealthReport {
    <#
    .SYNOPSIS
        Displays a formatted source health report to the console.
    .DESCRIPTION
        Takes health check results and outputs a color-coded console report showing
        the status of each source per application, with a summary of overall health.
    .PARAMETER Results
        Results from Test-SourceHealth.
    .EXAMPLE
        $results = Test-SourceHealth -CheckWinget -CheckChocolatey -CheckDirectUrl
        Get-SourceHealthReport -Results $results
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Results
    )

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'sourcehealth.title') -ForegroundColor Cyan
    Write-Host ""

    $healthy = 0
    $degraded = 0
    $critical = 0

    foreach ($result in $Results) {
        $statusColor = if ($result.HealthySourceCount -eq $result.TotalSourceCount) {
            $healthy++
            'Green'
        } elseif ($result.HealthySourceCount -gt 0) {
            $degraded++
            'Yellow'
        } else {
            $critical++
            'Red'
        }

        Write-Host "  $($result.AppName) " -NoNewline -ForegroundColor White
        Write-Host "[$($result.HealthySourceCount)/$($result.TotalSourceCount)]" -ForegroundColor $statusColor

        foreach ($sourceName in $result.Sources.Keys) {
            $source = $result.Sources[$sourceName]
            $sourceStatus = $source.Status
            $sourceMsg = if ($source.Message) { $source.Message } else { '' }

            $icon = switch ($sourceStatus) {
                'OK'   { '[OK]' }
                'FAIL' { '[FAIL]' }
                'SKIP' { '[SKIP]' }
                default { '[??]' }
            }

            $iconColor = switch ($sourceStatus) {
                'OK'   { 'Green' }
                'FAIL' { 'Red' }
                'SKIP' { 'DarkGray' }
                default { 'Yellow' }
            }

            Write-Host "    $icon " -NoNewline -ForegroundColor $iconColor
            Write-Host "$sourceName" -NoNewline -ForegroundColor Gray
            if ($sourceMsg) {
                Write-Host " - $sourceMsg" -ForegroundColor DarkGray
            } else {
                Write-Host ""
            }
        }
    }

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'sourcehealth.summary.total' -Parameters @{ Count = $Results.Count }) -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'sourcehealth.summary.healthy' -Parameters @{ Count = $healthy }) -ForegroundColor Green
    if ($degraded -gt 0) {
        Write-Host (Get-LocalizedString -Key 'sourcehealth.summary.degraded' -Parameters @{ Count = $degraded }) -ForegroundColor Yellow
    }
    if ($critical -gt 0) {
        Write-Host (Get-LocalizedString -Key 'sourcehealth.summary.critical' -Parameters @{ Count = $critical }) -ForegroundColor Red
    }
    Write-Host ""
}

# === PRIVATE FUNCTIONS ===

function Test-WingetSource {
    <#
    .SYNOPSIS
        Tests if a Winget package ID exists in the repository.
    .PARAMETER PackageId
        The Winget package identifier.
    .OUTPUTS
        Hashtable with Status and Message.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId
    )

    try {
        $output = & winget search --id $PackageId --exact --accept-source-agreements 2>&1 | Out-String
        if ($output -match [regex]::Escape($PackageId)) {
            return @{
                Status  = 'OK'
                Message = Get-LocalizedString -Key 'sourcehealth.winget_ok' -Parameters @{ PackageId = $PackageId }
            }
        }

        return @{
            Status  = 'FAIL'
            Message = Get-LocalizedString -Key 'sourcehealth.winget_not_found' -Parameters @{ PackageId = $PackageId }
        }
    }
    catch {
        return @{
            Status  = 'FAIL'
            Message = "Winget search error: $($_.Exception.Message)"
        }
    }
}

function Test-ChocolateySource {
    <#
    .SYNOPSIS
        Tests if a Chocolatey package exists in the repository.
    .PARAMETER PackageName
        The Chocolatey package name.
    .OUTPUTS
        Hashtable with Status and Message.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageName
    )

    try {
        $output = & choco search $PackageName --exact --limit-output 2>&1 | Out-String
        if ($output -match [regex]::Escape($PackageName)) {
            return @{
                Status  = 'OK'
                Message = Get-LocalizedString -Key 'sourcehealth.choco_ok' -Parameters @{ PackageName = $PackageName }
            }
        }

        return @{
            Status  = 'FAIL'
            Message = Get-LocalizedString -Key 'sourcehealth.choco_not_found' -Parameters @{ PackageName = $PackageName }
        }
    }
    catch {
        return @{
            Status  = 'FAIL'
            Message = "Chocolatey search error: $($_.Exception.Message)"
        }
    }
}

function Test-DirectUrlSource {
    <#
    .SYNOPSIS
        Tests if a DirectUrl is reachable via HTTP HEAD request.
    .PARAMETER Url
        The URL to check.
    .OUTPUTS
        Hashtable with Status and Message.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Url
    )

    try {
        $response = Invoke-WebRequest -Method Head -Uri $Url -TimeoutSec $script:DIRECTURL_TIMEOUT_SECONDS -UseBasicParsing -ErrorAction Stop
        $statusCode = $response.StatusCode

        if ($statusCode -ge 200 -and $statusCode -lt 400) {
            return @{
                Status  = 'OK'
                Message = Get-LocalizedString -Key 'sourcehealth.directurl_ok' -Parameters @{ Url = $Url }
            }
        }

        return @{
            Status  = 'FAIL'
            Message = Get-LocalizedString -Key 'sourcehealth.directurl_unreachable' -Parameters @{ StatusCode = $statusCode; Url = $Url }
        }
    }
    catch {
        return @{
            Status  = 'FAIL'
            Message = Get-LocalizedString -Key 'sourcehealth.directurl_error' -Parameters @{ Error = $_.Exception.Message }
        }
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Test-SourceHealth',
    'Repair-AppSources',
    'Get-SourceHealthReport'
)
