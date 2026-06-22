<#
.SYNOPSIS
    Catalog freshness check - detects broken external sources in applications.json.

.DESCRIPTION
    Runs scheduled freshness checks against the centralized application database.
    For each application, probes the declared sources (Winget, Chocolatey, DirectUrl)
    and reports broken or suspect entries. Designed to be invoked locally by
    maintainers or from a scheduled GitHub Actions workflow. Does NOT modify the
    database - output only.

    Status semantics (no separate "Healthy" status - Ok is the healthy state):
      Ok      - source verified healthy (winget/choco package found, HTTP reachable)
      Broken  - source clearly broken (package not found, HTTP 404/410)
      Suspect - indeterminate; needs review (HTTP 4xx CDN-protected, exit non-zero, network errors)
      Skipped - not probed in v1 (Microsoft Store, Windows Features, missing CLI on runner)

    Microsoft Store IDs are always reported as Skipped in v1 (no clean public API).
    Apps relying on Windows Features (no external sources) are also Skipped.

    Coverage gap detection: when winget/choco CLIs are missing on the runner, all
    associated probes return Skipped. The script emits a prominent warning in the
    console, in CI annotations, and at the top of the GitHub Step Summary so that
    a "green" run with empty verification cannot pass unnoticed.

.PARAMETER DatabasePath
    Path to the applications.json file. Defaults to Apps\Database\applications.json.

.PARAMETER Checks
    Subset of checks to run. One or more of: All, Winget, Chocolatey, DirectUrl, SchemaLint.

.PARAMETER AppIdFilter
    Optional list of AppIds to restrict the run to. Useful for controlled smoke tests
    (e.g. -AppIdFilter GoogleChrome,VSCode). Empty = all apps.

.PARAMETER JsonReportPath
    Optional path for the structured JSON report (artifact for CI).

.PARAMETER CachePath
    Optional path to a JSON cache file. Recent results within CacheTtlHours are reused.

.PARAMETER CacheTtlHours
    Cache time-to-live in hours. Default: 168 (7 days).

.PARAMETER ThrottleMs
    Delay between external calls, in milliseconds. Default: 150.

.PARAMETER CI
    Emit GitHub Actions annotations and Step Summary. Sets exit code to 1 on Broken.

.PARAMETER FailOnWarning
    Treat Suspect results as failures (exit 1) in addition to Broken.

.EXAMPLE
    .\Tools\Test-CatalogFreshness.ps1 -Checks Winget,Chocolatey,DirectUrl,SchemaLint -JsonReportPath .\report.json

.EXAMPLE
    .\Tools\Test-CatalogFreshness.ps1 -CI -CachePath .\Tools\.cache\freshness-cache.json

.EXAMPLE
    # Controlled smoke test - exercise real probes for one well-known app:
    .\Tools\Test-CatalogFreshness.ps1 -AppIdFilter GoogleChrome -Checks Winget,Chocolatey,DirectUrl -ThrottleMs 0
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

param(
    [string]$DatabasePath,
    [ValidateSet('All', 'Winget', 'Chocolatey', 'DirectUrl', 'SchemaLint')]
    [string[]]$Checks = @('All'),
    [string[]]$AppIdFilter,
    [string]$JsonReportPath,
    [string]$CachePath,
    [int]$CacheTtlHours = 168,
    [int]$ThrottleMs = 150,
    [switch]$CI,
    [switch]$FailOnWarning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Constants ---------------------------------------------------------------

$Script:StatusOk      = 'Ok'
$Script:StatusBroken  = 'Broken'
$Script:StatusSuspect = 'Suspect'
$Script:StatusSkipped = 'Skipped'

$Script:SourceWinget       = 'Winget'
$Script:SourceChocolatey   = 'Chocolatey'
$Script:SourceStore        = 'Store'
$Script:SourceDirectUrl    = 'DirectUrl'
$Script:SourceSchemaLint   = 'SchemaLint'

$Script:HttpUserAgent = 'WinForge-FreshnessCheck/1.0'
$Script:HttpTimeoutSeconds = 15

# winget return code APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND (0x8A150014).
# Emitted when --exact matches nothing - distinct from transient failures.
$Script:WingetExitNoMatch = -1978335212

# --- Helpers -----------------------------------------------------------------

function Get-PropertyValueOrNull {
    param(
        [Parameter()] $InputObject,
        [Parameter(Mandatory)] [string] $PropertyName
    )

    if ($null -eq $InputObject) { return $null }
    $prop = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-IsoTimestamp {
    return (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function ConvertTo-AnnotationSafeText {
    param([Parameter(Mandatory)] [string] $Text)

    return ($Text -replace "`r", '' -replace "`n", ' ' -replace '%', '%25' -replace ',', '%2C' -replace ':', '%3A')
}

# --- Plan building -----------------------------------------------------------

function Test-CheckSelected {
    param(
        [Parameter(Mandatory)] [string[]] $Selected,
        [Parameter(Mandatory)] [string] $Source
    )

    if ($Selected -contains 'All') { return $true }
    return $Selected -contains $Source
}

function Get-FreshnessCheckPlan {
    <#
    .SYNOPSIS
        Builds the per-(app, source) work list from the loaded database.
    #>
    param(
        [Parameter(Mandatory)] [object[]] $Applications,
        [Parameter(Mandatory)] [string[]] $Selected
    )

    $plan = [System.Collections.Generic.List[object]]::new()

    foreach ($app in $Applications) {
        $sources = Get-PropertyValueOrNull -InputObject $app -PropertyName 'Sources'
        $installMethod = Get-PropertyValueOrNull -InputObject $app -PropertyName 'InstallMethod'

        $winget = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Winget'
        $choco  = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Chocolatey'
        $store  = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Store'
        $direct = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'DirectUrl'

        $hasAnySource = ($winget) -or ($choco) -or ($store) -or ($direct)

        # Apps without any source AND not flagged as WindowsFeature → still record one Skipped row
        # so the report shows them. WindowsFeature apps are intentionally Skipped.
        if (-not $hasAnySource) {
            $reason = if ($installMethod -eq 'WindowsFeature') { 'windows-feature' } else { 'no-source' }
            [void]$plan.Add([PSCustomObject]@{
                AppId      = $app.AppId
                Source     = 'None'
                Identifier = $null
                Action     = 'Skip'
                Reason     = $reason
            })
            continue
        }

        if ($winget -and (Test-CheckSelected -Selected $Selected -Source $Script:SourceWinget)) {
            [void]$plan.Add([PSCustomObject]@{
                AppId      = $app.AppId
                Source     = $Script:SourceWinget
                Identifier = $winget
                Action     = 'Probe'
                Reason     = $null
            })
        }
        if ($choco -and (Test-CheckSelected -Selected $Selected -Source $Script:SourceChocolatey)) {
            [void]$plan.Add([PSCustomObject]@{
                AppId      = $app.AppId
                Source     = $Script:SourceChocolatey
                Identifier = $choco
                Action     = 'Probe'
                Reason     = $null
            })
        }
        if ($store) {
            # Store always reported as Skipped in v1 - no clean public API.
            [void]$plan.Add([PSCustomObject]@{
                AppId      = $app.AppId
                Source     = $Script:SourceStore
                Identifier = $store
                Action     = 'Skip'
                Reason     = 'store-not-validated-v1'
            })
        }
        if ($direct -and (Test-CheckSelected -Selected $Selected -Source $Script:SourceDirectUrl)) {
            [void]$plan.Add([PSCustomObject]@{
                AppId      = $app.AppId
                Source     = $Script:SourceDirectUrl
                Identifier = $direct
                Action     = 'Probe'
                Reason     = $null
            })
        }
    }

    return $plan
}

# --- Cache -------------------------------------------------------------------

function Get-CacheKey {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $Identifier
    )
    return "$Source|$Identifier"
}

function Read-FreshnessCache {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return @{}
    }

    try {
        $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($raw)) { return @{} }
        $parsed = $raw | ConvertFrom-Json
        $entries = Get-PropertyValueOrNull -InputObject $parsed -PropertyName 'Entries'
        if ($null -eq $entries) { return @{} }

        $map = @{}
        foreach ($prop in $entries.PSObject.Properties) {
            $map[$prop.Name] = $prop.Value
        }
        return $map
    } catch {
        Write-Warning "Cache file at '$Path' could not be parsed; ignoring. $_"
        return @{}
    }
}

function Test-CacheEntryFresh {
    param(
        [Parameter(Mandatory)] [object] $Entry,
        [Parameter(Mandatory)] [int] $TtlHours
    )

    $checkedAt = Get-PropertyValueOrNull -InputObject $Entry -PropertyName 'CheckedAt'
    if ([string]::IsNullOrWhiteSpace([string]$checkedAt)) { return $false }

    try {
        $checkedAtDt = [DateTime]::Parse($checkedAt, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal)
    } catch {
        return $false
    }

    $age = (Get-Date).ToUniversalTime() - $checkedAtDt
    return ($age.TotalHours -lt $TtlHours)
}

function Save-FreshnessCache {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [hashtable] $Entries
    )

    $entriesObj = [PSCustomObject]@{}
    foreach ($key in ($Entries.Keys | Sort-Object)) {
        $entriesObj | Add-Member -NotePropertyName $key -NotePropertyValue $Entries[$key]
    }

    $payload = [PSCustomObject]@{
        Version   = 1
        WrittenAt = Get-IsoTimestamp
        Entries   = $entriesObj
    }

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $payload | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

# --- Probes ------------------------------------------------------------------

function Test-WingetIdentifier {
    <#
    .SYNOPSIS
        Probes a winget package ID via the local winget CLI.
    .OUTPUTS
        [PSCustomObject] @{ Status; Reason; Detail }
    #>
    param([Parameter(Mandatory)] [string] $Identifier)

    if (-not (Get-Command -Name winget -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{
            Status = $Script:StatusSkipped
            Reason = 'winget-cli-missing'
            Detail = 'winget CLI not found on PATH'
        }
    }

    try {
        $output = winget search --id $Identifier --exact --source winget 2>&1 | Out-String
        $code = $LASTEXITCODE

        $literal = [regex]::Escape($Identifier)
        if ($code -eq 0 -and $output -match $literal) {
            return [PSCustomObject]@{
                Status = $Script:StatusOk
                Reason = 'found'
                Detail = "winget search --id $Identifier → match"
            }
        }

        # winget v1.6+ returns APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND when --exact misses.
        # Older versions return exit 0 with empty output. Both unambiguously mean "broken".
        if ($code -eq $Script:WingetExitNoMatch) {
            return [PSCustomObject]@{
                Status = $Script:StatusBroken
                Reason = 'not-found'
                Detail = "winget search --id $Identifier returned no applications (exit $code)"
            }
        }

        if ($code -eq 0 -and $output -notmatch $literal) {
            return [PSCustomObject]@{
                Status = $Script:StatusBroken
                Reason = 'not-found'
                Detail = "winget search --id $Identifier returned no exact match"
            }
        }

        # Other non-zero codes = transient failures (network, source errors, etc.) - needs review.
        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'exit-non-zero'
            Detail = "winget exit $code"
        }
    } catch {
        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'invocation-error'
            Detail = $_.Exception.Message
        }
    }
}

function Test-ChocolateyIdentifier {
    <#
    .SYNOPSIS
        Probes a chocolatey package ID via the local choco CLI.
    #>
    param([Parameter(Mandatory)] [string] $Identifier)

    if (-not (Get-Command -Name choco -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{
            Status = $Script:StatusSkipped
            Reason = 'choco-cli-missing'
            Detail = 'choco CLI not found on PATH'
        }
    }

    try {
        $output = choco search $Identifier --exact --limit-output --no-color 2>&1 | Out-String
        $code = $LASTEXITCODE

        $hasIdLine = $false
        foreach ($line in ($output -split "`r?`n")) {
            if ($line -match "^$([regex]::Escape($Identifier))\|") {
                $hasIdLine = $true
                break
            }
        }

        if ($code -eq 0 -and $hasIdLine) {
            return [PSCustomObject]@{
                Status = $Script:StatusOk
                Reason = 'found'
                Detail = "choco search $Identifier → match"
            }
        }

        if ($code -eq 0 -and -not $hasIdLine) {
            return [PSCustomObject]@{
                Status = $Script:StatusBroken
                Reason = 'not-found'
                Detail = "choco search $Identifier returned no exact match"
            }
        }

        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'exit-non-zero'
            Detail = "choco exit $code"
        }
    } catch {
        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'invocation-error'
            Detail = $_.Exception.Message
        }
    }
}

function Get-DirectUrlStatusFromCode {
    <#
    .SYNOPSIS
        Maps an HTTP status code to a freshness verdict.
    #>
    param([Parameter(Mandatory)] [int] $StatusCode)

    if ($StatusCode -in 200, 201, 202, 203, 204, 301, 302, 303, 307, 308) {
        return [PSCustomObject]@{ Status = $Script:StatusOk; Reason = 'http-ok' }
    }
    if ($StatusCode -in 401, 403, 405, 429) {
        return [PSCustomObject]@{ Status = $Script:StatusSuspect; Reason = "http-$StatusCode-cdn-protected" }
    }
    if ($StatusCode -in 404, 410) {
        return [PSCustomObject]@{ Status = $Script:StatusBroken; Reason = "http-$StatusCode" }
    }
    return [PSCustomObject]@{ Status = $Script:StatusSuspect; Reason = "http-$StatusCode" }
}

function Test-DirectUrlReachable {
    <#
    .SYNOPSIS
        Issues a HEAD request (with GET fallback) against a download URL.
    #>
    param([Parameter(Mandatory)] [string] $Url)

    $response = $null
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Head -MaximumRedirection 5 `
            -UserAgent $Script:HttpUserAgent -TimeoutSec $Script:HttpTimeoutSeconds `
            -UseBasicParsing -ErrorAction Stop
        $verdict = Get-DirectUrlStatusFromCode -StatusCode $response.StatusCode
        return [PSCustomObject]@{
            Status = $verdict.Status
            Reason = $verdict.Reason
            Detail = "HEAD $Url → $($response.StatusCode)"
        }
    } catch [System.Net.WebException], [Microsoft.PowerShell.Commands.HttpResponseException] {
        $statusCode = 0
        try {
            $rawResponse = $_.Exception.Response
            if ($rawResponse -and $rawResponse.StatusCode) {
                $statusCode = [int]$rawResponse.StatusCode
            }
        } catch {
            $statusCode = 0
        }

        if ($statusCode -in 405, 501) {
            try {
                $response = Invoke-WebRequest -Uri $Url -Method Get -MaximumRedirection 5 `
                    -UserAgent $Script:HttpUserAgent -TimeoutSec $Script:HttpTimeoutSeconds `
                    -UseBasicParsing -ErrorAction Stop
                $verdict = Get-DirectUrlStatusFromCode -StatusCode $response.StatusCode
                return [PSCustomObject]@{
                    Status = $verdict.Status
                    Reason = $verdict.Reason
                    Detail = "GET $Url → $($response.StatusCode) (HEAD rejected)"
                }
            } catch {
                return [PSCustomObject]@{
                    Status = $Script:StatusSuspect
                    Reason = 'fallback-get-failed'
                    Detail = $_.Exception.Message
                }
            }
        }

        if ($statusCode -gt 0) {
            $verdict = Get-DirectUrlStatusFromCode -StatusCode $statusCode
            return [PSCustomObject]@{
                Status = $verdict.Status
                Reason = $verdict.Reason
                Detail = "HEAD $Url → $statusCode"
            }
        }

        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'network-error'
            Detail = $_.Exception.Message
        }
    } catch {
        return [PSCustomObject]@{
            Status = $Script:StatusSuspect
            Reason = 'unexpected-error'
            Detail = $_.Exception.Message
        }
    }
}

# --- Schema lint -------------------------------------------------------------

function Invoke-SchemaLint {
    <#
    .SYNOPSIS
        Offline consistency checks. Always emits Suspect, never Broken.
    #>
    param([Parameter(Mandatory)] [object[]] $Applications)

    $now = Get-Date
    $findings = [System.Collections.Generic.List[object]]::new()

    foreach ($app in $Applications) {
        $sources = Get-PropertyValueOrNull -InputObject $app -PropertyName 'Sources'
        $detection = Get-PropertyValueOrNull -InputObject $app -PropertyName 'Detection'
        $installMethod = Get-PropertyValueOrNull -InputObject $app -PropertyName 'InstallMethod'
        $verified = Get-PropertyValueOrNull -InputObject $app -PropertyName 'Verified'
        $lastVerified = Get-PropertyValueOrNull -InputObject $app -PropertyName 'LastVerified'

        $detectionMethod = Get-PropertyValueOrNull -InputObject $detection -PropertyName 'Method'
        $store = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Store'
        $winget = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Winget'
        $choco  = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'Chocolatey'
        $direct = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'DirectUrl'

        # Rule 1: StoreApp detection without a Store source ID
        if ($detectionMethod -eq 'StoreApp' -and [string]::IsNullOrWhiteSpace([string]$store)) {
            [void]$findings.Add([PSCustomObject]@{
                AppId  = $app.AppId
                Source = $Script:SourceSchemaLint
                Status = $Script:StatusSuspect
                Reason = 'storeapp-detection-without-store-source'
                Detail = "Detection.Method=StoreApp but Sources.Store is null"
            })
        }

        # Rule 2: All sources null without InstallMethod=WindowsFeature
        $hasAnySource = $winget -or $choco -or $store -or $direct
        if (-not $hasAnySource -and $installMethod -ne 'WindowsFeature') {
            [void]$findings.Add([PSCustomObject]@{
                AppId  = $app.AppId
                Source = $Script:SourceSchemaLint
                Status = $Script:StatusSuspect
                Reason = 'no-source-and-not-windows-feature'
                Detail = 'All Sources are null but InstallMethod is not WindowsFeature'
            })
        }

        # Rule 3: Verified=true with stale or missing LastVerified
        if ($verified -eq $true) {
            if ([string]::IsNullOrWhiteSpace([string]$lastVerified)) {
                [void]$findings.Add([PSCustomObject]@{
                    AppId  = $app.AppId
                    Source = $Script:SourceSchemaLint
                    Status = $Script:StatusSuspect
                    Reason = 'verified-without-lastverified'
                    Detail = 'Verified=true but LastVerified is missing'
                })
            } else {
                try {
                    $lvDt = [DateTime]::ParseExact([string]$lastVerified, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)
                    if (($now - $lvDt).TotalDays -gt 365) {
                        [void]$findings.Add([PSCustomObject]@{
                            AppId  = $app.AppId
                            Source = $Script:SourceSchemaLint
                            Status = $Script:StatusSuspect
                            Reason = 'lastverified-stale'
                            Detail = "LastVerified $lastVerified is more than 12 months old"
                        })
                    }
                } catch {
                    [void]$findings.Add([PSCustomObject]@{
                        AppId  = $app.AppId
                        Source = $Script:SourceSchemaLint
                        Status = $Script:StatusSuspect
                        Reason = 'lastverified-unparseable'
                        Detail = "LastVerified '$lastVerified' is not in YYYY-MM-DD format"
                    })
                }
            }
        }
    }

    return $findings.ToArray()
}

# --- Reporting ---------------------------------------------------------------

function Get-ProbeEnvironment {
    <#
    .SYNOPSIS
        Snapshot of which probe CLIs are available on this runner.
    #>
    return [PSCustomObject]@{
        WingetAvailable     = [bool](Get-Command -Name winget -ErrorAction SilentlyContinue)
        ChocolateyAvailable = [bool](Get-Command -Name choco  -ErrorAction SilentlyContinue)
    }
}

function Get-CoverageGap {
    <#
    .SYNOPSIS
        Identifies probes silently disabled by missing CLIs on the runner.
        Returns a list of { Source, SkippedCount, Reason } entries - empty if no gap.
        The leading-comma idiom on return prevents PowerShell from unwrapping
        an empty/single-element array to $null, so callers can rely on .Count.
    #>
    param([Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $Results)

    $gaps = [System.Collections.Generic.List[object]]::new()

    $wingetMissing = @($Results | Where-Object { $_.Source -eq $Script:SourceWinget -and $_.Reason -eq 'winget-cli-missing' })
    if ($wingetMissing.Count -gt 0) {
        [void]$gaps.Add([PSCustomObject]@{
            Source       = $Script:SourceWinget
            Reason       = 'winget-cli-missing'
            SkippedCount = $wingetMissing.Count
        })
    }

    $chocoMissing = @($Results | Where-Object { $_.Source -eq $Script:SourceChocolatey -and $_.Reason -eq 'choco-cli-missing' })
    if ($chocoMissing.Count -gt 0) {
        [void]$gaps.Add([PSCustomObject]@{
            Source       = $Script:SourceChocolatey
            Reason       = 'choco-cli-missing'
            SkippedCount = $chocoMissing.Count
        })
    }

    return ,$gaps.ToArray()
}

function New-FreshnessReport {
    param(
        [Parameter(Mandatory)] [string] $DatabaseVersion,
        [Parameter(Mandatory)] [int] $AppCount,
        [Parameter(Mandatory)] [object[]] $Results,
        [Parameter()] [PSCustomObject] $ProbeEnvironment,
        [Parameter()] [string[]] $AppIdFilter
    )

    $summary = [ordered]@{
        Apps     = $AppCount
        Checks   = $Results.Count
        Ok       = (@($Results | Where-Object { $_.Status -eq $Script:StatusOk })).Count
        Suspect  = (@($Results | Where-Object { $_.Status -eq $Script:StatusSuspect })).Count
        Broken   = (@($Results | Where-Object { $_.Status -eq $Script:StatusBroken })).Count
        Skipped  = (@($Results | Where-Object { $_.Status -eq $Script:StatusSkipped })).Count
    }

    $coverageGap = Get-CoverageGap -Results $Results

    return [PSCustomObject]@{
        DatabaseVersion  = $DatabaseVersion
        GeneratedAt      = Get-IsoTimestamp
        AppIdFilter      = if ($AppIdFilter) { @($AppIdFilter) } else { @() }
        ProbeEnvironment = $ProbeEnvironment
        Summary          = [PSCustomObject]$summary
        CoverageGap      = @($coverageGap)
        Results          = @($Results)
    }
}

function Write-CIAnnotation {
    param(
        [Parameter(Mandatory)] [string] $Level,
        [Parameter(Mandatory)] [string] $Title,
        [Parameter(Mandatory)] [string] $File,
        [Parameter(Mandatory)] [string] $Message
    )

    $safeTitle = ConvertTo-AnnotationSafeText -Text $Title
    $safeFile = ConvertTo-AnnotationSafeText -Text $File
    Write-Host "::$Level file=$safeFile,title=$safeTitle::$Message"
}

function Write-StepSummaryMarkdown {
    param(
        [Parameter(Mandatory)] [PSCustomObject] $Report
    )

    $summaryFile = $env:GITHUB_STEP_SUMMARY
    if ([string]::IsNullOrWhiteSpace($summaryFile)) { return }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('## Catalog Freshness Report')

    $gapArr    = @($Report.CoverageGap)
    $filterArr = @($Report.AppIdFilter)

    if ($gapArr.Count -gt 0) {
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('> [!WARNING]')
        [void]$sb.AppendLine('> **Coverage gap detected - this run did not actually verify all configured sources.**')
        [void]$sb.AppendLine('>')
        foreach ($gap in $gapArr) {
            [void]$sb.AppendLine("> - $($gap.Source): $($gap.SkippedCount) probe(s) skipped because of `"$($gap.Reason)`"")
        }
        [void]$sb.AppendLine('>')
        [void]$sb.AppendLine('> A green job here does **not** mean the catalog is healthy - it means the runner was incomplete.')
        [void]$sb.AppendLine('')
    }

    [void]$sb.AppendLine("- Generated: $($Report.GeneratedAt)")
    [void]$sb.AppendLine("- Database version: $($Report.DatabaseVersion)")
    if ($filterArr.Count -gt 0) {
        [void]$sb.AppendLine("- AppId filter: $($filterArr -join ', ')")
    }
    [void]$sb.AppendLine("- Probe environment: winget=$($Report.ProbeEnvironment.WingetAvailable), choco=$($Report.ProbeEnvironment.ChocolateyAvailable)")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('Status legend: **Ok** = source verified healthy. **Broken** = source clearly missing (404/410/not-found). **Suspect** = needs review. **Skipped** = not probed in v1.')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| Status | Count |')
    [void]$sb.AppendLine('| --- | --- |')
    [void]$sb.AppendLine("| Ok (healthy) | $($Report.Summary.Ok) |")
    [void]$sb.AppendLine("| Broken | $($Report.Summary.Broken) |")
    [void]$sb.AppendLine("| Suspect | $($Report.Summary.Suspect) |")
    [void]$sb.AppendLine("| Skipped | $($Report.Summary.Skipped) |")
    [void]$sb.AppendLine('')

    $broken = @($Report.Results | Where-Object { $_.Status -eq $Script:StatusBroken })
    if ($broken.Count -gt 0) {
        [void]$sb.AppendLine('### Broken')
        [void]$sb.AppendLine('| AppId | Source | Identifier | Reason |')
        [void]$sb.AppendLine('| --- | --- | --- | --- |')
        foreach ($r in $broken) {
            [void]$sb.AppendLine("| $($r.AppId) | $($r.Source) | $($r.Identifier) | $($r.Reason) |")
        }
        [void]$sb.AppendLine('')
    }

    $suspect = @($Report.Results | Where-Object { $_.Status -eq $Script:StatusSuspect })
    if ($suspect.Count -gt 0) {
        [void]$sb.AppendLine('### Suspect')
        [void]$sb.AppendLine('| AppId | Source | Identifier | Reason |')
        [void]$sb.AppendLine('| --- | --- | --- | --- |')
        foreach ($r in $suspect) {
            [void]$sb.AppendLine("| $($r.AppId) | $($r.Source) | $($r.Identifier) | $($r.Reason) |")
        }
    }

    Add-Content -LiteralPath $summaryFile -Value $sb.ToString() -Encoding UTF8
}

# --- Console output ----------------------------------------------------------

function Write-ConsoleReport {
    param([Parameter(Mandatory)] [PSCustomObject] $Report)

    # PowerShell sometimes unwraps single-element arrays through PSCustomObject
    # property access - wrap in @() so .Count works under StrictMode.
    $filterArr = @($Report.AppIdFilter)
    $gapArr    = @($Report.CoverageGap)

    $bar = '=' * 60
    Write-Host ''
    Write-Host $bar -ForegroundColor Cyan
    Write-Host '  Catalog Freshness Report' -ForegroundColor Cyan
    Write-Host $bar -ForegroundColor Cyan
    Write-Host "  Database  : $($Report.DatabaseVersion)"
    Write-Host "  Generated : $($Report.GeneratedAt)"
    if ($filterArr.Count -gt 0) {
        Write-Host "  Filter    : AppIds = $($filterArr -join ', ')" -ForegroundColor DarkCyan
    }
    Write-Host "  Probes    : winget=$($Report.ProbeEnvironment.WingetAvailable), choco=$($Report.ProbeEnvironment.ChocolateyAvailable)"
    Write-Host ''
    Write-Host "  Apps      : $($Report.Summary.Apps)"
    Write-Host "  Checks    : $($Report.Summary.Checks)"
    Write-Host "  Ok        : $($Report.Summary.Ok) (healthy)" -ForegroundColor Green
    Write-Host "  Broken    : $($Report.Summary.Broken)"      -ForegroundColor Red
    Write-Host "  Suspect   : $($Report.Summary.Suspect)"     -ForegroundColor Yellow
    Write-Host "  Skipped   : $($Report.Summary.Skipped)"     -ForegroundColor DarkGray

    if ($gapArr.Count -gt 0) {
        Write-Host ''
        Write-Host '############################################################' -ForegroundColor Yellow
        Write-Host '#  COVERAGE GAP - this run did NOT verify all sources    #' -ForegroundColor Yellow
        Write-Host '############################################################' -ForegroundColor Yellow
        foreach ($gap in $gapArr) {
            Write-Host "   - $($gap.Source): $($gap.SkippedCount) probe(s) silently skipped ($($gap.Reason))" -ForegroundColor Yellow
        }
        Write-Host '   A green exit here does NOT mean the catalog is healthy.' -ForegroundColor Yellow
    }

    $broken = @($Report.Results | Where-Object { $_.Status -eq $Script:StatusBroken })
    if ($broken.Count -gt 0) {
        Write-Host ''
        Write-Host '❌ Broken:' -ForegroundColor Red
        foreach ($r in $broken) {
            Write-Host "   - $($r.AppId) [$($r.Source)] $($r.Identifier) - $($r.Reason)" -ForegroundColor Red
        }
    }

    $suspect = @($Report.Results | Where-Object { $_.Status -eq $Script:StatusSuspect })
    if ($suspect.Count -gt 0) {
        Write-Host ''
        Write-Host '⚠️  Suspect:' -ForegroundColor Yellow
        foreach ($r in $suspect) {
            Write-Host "   - $($r.AppId) [$($r.Source)] $($r.Identifier) - $($r.Reason)" -ForegroundColor Yellow
        }
    }

    Write-Host ''
}

# --- Main --------------------------------------------------------------------

function Invoke-CatalogFreshness {
    [CmdletBinding()]
    param(
        [string]$DatabasePath,
        [string[]]$Checks,
        [string[]]$AppIdFilter,
        [string]$JsonReportPath,
        [string]$CachePath,
        [int]$CacheTtlHours,
        [int]$ThrottleMs,
        [switch]$CI,
        [switch]$FailOnWarning
    )

    $repoRoot = Split-Path -Parent $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
        $DatabasePath = Join-Path $repoRoot 'Apps\Database\applications.json'
    }
    if (-not (Test-Path -LiteralPath $DatabasePath)) {
        throw "Database not found at: $DatabasePath"
    }

    $modulePath = Join-Path $repoRoot 'Modules\ApplicationDatabase.psm1'
    if (-not (Test-Path -LiteralPath $modulePath)) {
        throw "ApplicationDatabase module not found at: $modulePath"
    }

    # Use ConvertFrom-Json directly so the script is decoupled from the module's
    # own caching/state (and works against arbitrary fixture databases).
    $jsonText = Get-Content -LiteralPath $DatabasePath -Raw -Encoding UTF8
    $database = $jsonText | ConvertFrom-Json

    $databaseVersion = Get-PropertyValueOrNull -InputObject $database -PropertyName 'DatabaseVersion'
    if ([string]::IsNullOrWhiteSpace([string]$databaseVersion)) { $databaseVersion = 'unknown' }

    $appsObj = Get-PropertyValueOrNull -InputObject $database -PropertyName 'Applications'
    if ($null -eq $appsObj) { throw "Database has no 'Applications' object." }

    $apps = @()
    foreach ($prop in $appsObj.PSObject.Properties) {
        $entry = $prop.Value
        $entry | Add-Member -NotePropertyName 'AppId' -NotePropertyValue $prop.Name -Force
        $apps += $entry
    }

    $totalAppsLoaded = $apps.Count
    Write-Host "Loaded $totalAppsLoaded applications from $DatabasePath" -ForegroundColor Cyan

    $effectiveFilter = @()
    if ($AppIdFilter -and $AppIdFilter.Count -gt 0) {
        $effectiveFilter = @($AppIdFilter | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($effectiveFilter.Count -gt 0) {
        $filterSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($id in $effectiveFilter) { [void]$filterSet.Add($id) }

        $apps = @($apps | Where-Object { $filterSet.Contains($_.AppId) })

        $missing = @($effectiveFilter | Where-Object {
            $needle = $_
            -not ($apps | Where-Object { $_.AppId -ieq $needle })
        })
        foreach ($id in $missing) {
            Write-Warning "AppIdFilter: '$id' not found in $DatabasePath (skipped)."
        }

        Write-Host "AppIdFilter: $($apps.Count)/$totalAppsLoaded apps retained" -ForegroundColor DarkCyan
    }

    $probeEnv = Get-ProbeEnvironment
    Write-Host "Probe environment: winget=$($probeEnv.WingetAvailable), choco=$($probeEnv.ChocolateyAvailable)" -ForegroundColor DarkCyan

    $plan = Get-FreshnessCheckPlan -Applications $apps -Selected $Checks
    Write-Host "Check plan: $($plan.Count) entries" -ForegroundColor Cyan

    $cache = Read-FreshnessCache -Path $CachePath
    $newCache = @{}
    $results = [System.Collections.Generic.List[object]]::new()

    foreach ($entry in $plan) {
        if ($entry.Action -eq 'Skip') {
            [void]$results.Add([PSCustomObject]@{
                AppId      = $entry.AppId
                Source     = $entry.Source
                Identifier = $entry.Identifier
                Status     = $Script:StatusSkipped
                Reason     = $entry.Reason
                Detail     = "Skipped: $($entry.Reason)"
                FromCache  = $false
                CheckedAt  = Get-IsoTimestamp
            })
            continue
        }

        $key = Get-CacheKey -Source $entry.Source -Identifier $entry.Identifier
        $cached = if ($cache.ContainsKey($key)) { $cache[$key] } else { $null }

        if ($null -ne $cached -and (Test-CacheEntryFresh -Entry $cached -TtlHours $CacheTtlHours)) {
            $cachedStatus = Get-PropertyValueOrNull -InputObject $cached -PropertyName 'Status'
            $cachedReason = Get-PropertyValueOrNull -InputObject $cached -PropertyName 'Reason'
            $cachedDetail = Get-PropertyValueOrNull -InputObject $cached -PropertyName 'Detail'
            $cachedAt     = Get-PropertyValueOrNull -InputObject $cached -PropertyName 'CheckedAt'
            [void]$results.Add([PSCustomObject]@{
                AppId      = $entry.AppId
                Source     = $entry.Source
                Identifier = $entry.Identifier
                Status     = $cachedStatus
                Reason     = $cachedReason
                Detail     = $cachedDetail
                FromCache  = $true
                CheckedAt  = $cachedAt
            })
            $newCache[$key] = $cached
            continue
        }

        switch ($entry.Source) {
            $Script:SourceWinget     { $probe = Test-WingetIdentifier -Identifier $entry.Identifier }
            $Script:SourceChocolatey { $probe = Test-ChocolateyIdentifier -Identifier $entry.Identifier }
            $Script:SourceDirectUrl  { $probe = Test-DirectUrlReachable -Url $entry.Identifier }
            default {
                $probe = [PSCustomObject]@{
                    Status = $Script:StatusSkipped
                    Reason = 'unknown-source'
                    Detail = "Unknown source type: $($entry.Source)"
                }
            }
        }

        $checkedAt = Get-IsoTimestamp
        [void]$results.Add([PSCustomObject]@{
            AppId      = $entry.AppId
            Source     = $entry.Source
            Identifier = $entry.Identifier
            Status     = $probe.Status
            Reason     = $probe.Reason
            Detail     = $probe.Detail
            FromCache  = $false
            CheckedAt  = $checkedAt
        })

        $newCache[$key] = [PSCustomObject]@{
            Status    = $probe.Status
            Reason    = $probe.Reason
            Detail    = $probe.Detail
            CheckedAt = $checkedAt
        }

        if ($ThrottleMs -gt 0) {
            Start-Sleep -Milliseconds $ThrottleMs
        }
    }

    if (Test-CheckSelected -Selected $Checks -Source $Script:SourceSchemaLint) {
        $lintFindings = Invoke-SchemaLint -Applications $apps
        foreach ($f in $lintFindings) {
            [void]$results.Add([PSCustomObject]@{
                AppId      = $f.AppId
                Source     = $f.Source
                Identifier = $null
                Status     = $f.Status
                Reason     = $f.Reason
                Detail     = $f.Detail
                FromCache  = $false
                CheckedAt  = Get-IsoTimestamp
            })
        }
    }

    $report = New-FreshnessReport -DatabaseVersion ([string]$databaseVersion) `
        -AppCount $apps.Count `
        -Results $results.ToArray() `
        -ProbeEnvironment $probeEnv `
        -AppIdFilter $effectiveFilter

    Write-ConsoleReport -Report $report

    if ($JsonReportPath) {
        $reportDir = Split-Path -Parent $JsonReportPath
        if ($reportDir -and -not (Test-Path -LiteralPath $reportDir)) {
            New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
        }
        $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $JsonReportPath -Encoding UTF8
        Write-Host "JSON report → $JsonReportPath" -ForegroundColor Cyan
    }

    if ($CachePath) {
        Save-FreshnessCache -Path $CachePath -Entries $newCache
        Write-Host "Cache → $CachePath" -ForegroundColor DarkCyan
    }

    if ($CI) {
        $relativeDb = if ($DatabasePath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $DatabasePath.Substring($repoRoot.Length).TrimStart('\','/').Replace('\','/')
        } else { 'Apps/Database/applications.json' }

        # Top-level coverage-gap warning - emitted FIRST so it cannot be missed
        # when a "green" run happened only because winget/choco were absent.
        $gapArr = @($report.CoverageGap)
        if ($gapArr.Count -gt 0) {
            $gapSummary = ($gapArr | ForEach-Object {
                "$($_.Source) ($($_.SkippedCount) probes silently skipped: $($_.Reason))"
            }) -join '; '
            Write-Host "::warning title=Catalog Freshness coverage gap::Run did not verify all sources - $gapSummary"
        }

        foreach ($r in $report.Results) {
            if ($r.Status -eq $Script:StatusBroken) {
                Write-CIAnnotation -Level 'error' -Title 'Catalog Freshness' -File $relativeDb `
                    -Message "$($r.AppId) [$($r.Source)] $($r.Reason): $($r.Detail)"
            } elseif ($r.Status -eq $Script:StatusSuspect) {
                Write-CIAnnotation -Level 'warning' -Title 'Catalog Freshness' -File $relativeDb `
                    -Message "$($r.AppId) [$($r.Source)] $($r.Reason): $($r.Detail)"
            }
        }

        Write-StepSummaryMarkdown -Report $report
    }

    $exitCode = 0
    if ($report.Summary.Broken -gt 0) { $exitCode = 1 }
    if ($FailOnWarning -and $report.Summary.Suspect -gt 0) { $exitCode = 1 }

    return [PSCustomObject]@{
        Report   = $report
        ExitCode = $exitCode
    }
}

# --- Entry point -------------------------------------------------------------
# Skip when dot-sourced (e.g. from Pester tests) so only the helpers are loaded.

if ($MyInvocation.InvocationName -ne '.') {
    $outcome = Invoke-CatalogFreshness `
        -DatabasePath $DatabasePath `
        -Checks $Checks `
        -AppIdFilter $AppIdFilter `
        -JsonReportPath $JsonReportPath `
        -CachePath $CachePath `
        -CacheTtlHours $CacheTtlHours `
        -ThrottleMs $ThrottleMs `
        -CI:$CI `
        -FailOnWarning:$FailOnWarning

    exit $outcome.ExitCode
}
