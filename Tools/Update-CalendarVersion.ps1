<#
.SYNOPSIS
    Updates WinForge calendar release version metadata.

.DESCRIPTION
    Config/version.json stores the user-facing calendar version as YYYYMMDDxx.
    .NET assembly/package metadata uses a Win32-safe version derived from it:
    1.0.MMDD.sequence.

.PARAMETER Version
    Optional forced display version. Accepts YYYYMMDDxx, or Heimdall-style
    YYYY.MMDDxx and normalizes it to YYYYMMDDxx.

.PARAMETER RootPath
    Repository root. Defaults to the parent of this script directory.

.PARAMETER PassThru
    Returns the computed version object without status text.

.EXAMPLE
    .\Tools\Update-CalendarVersion.ps1
    .\Tools\Update-CalendarVersion.ps1 -Version 2026050901
    .\Tools\Update-CalendarVersion.ps1 -Version 2026.050901
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version,

    [Parameter()]
    [string]$RootPath = (Split-Path -Parent $PSScriptRoot),

    [Parameter()]
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

function ConvertTo-CalendarVersion {
    param(
        [Parameter(Mandatory)]
        [string]$InputVersion
    )

    $trimmed = $InputVersion.Trim()
    if ($trimmed -match '^(?<year>\d{4})(?<mmdd>\d{4})(?<sequence>\d{2})$') {
        $year = [int]$Matches.year
        $mmdd = $Matches.mmdd
        $sequence = [int]$Matches.sequence
    } elseif ($trimmed -match '^(?<year>\d{4})\.(?<mmdd>\d{4})(?<sequence>\d{2})$') {
        $year = [int]$Matches.year
        $mmdd = $Matches.mmdd
        $sequence = [int]$Matches.sequence
    } else {
        throw "Version must use YYYYMMDDxx or YYYY.MMDDxx format. Received: '$InputVersion'"
    }

    if ($sequence -lt 1 -or $sequence -gt 99) {
        throw "Version sequence must be between 01 and 99. Received: '$InputVersion'"
    }

    $month = [int]$mmdd.Substring(0, 2)
    $day = [int]$mmdd.Substring(2, 2)
    try {
        $releaseDate = [datetime]::new($year, $month, $day)
    } catch {
        throw "Version date is invalid. Received: '$InputVersion'"
    }

    $displayVersion = '{0}{1}{2:D2}' -f $year, $mmdd, $sequence
    return [PSCustomObject]@{
        DisplayVersion       = $displayVersion
        InformationalVersion = $displayVersion
        AssemblyVersion      = '1.0.{0}.{1}' -f $mmdd, $sequence
        ReleaseDate          = $releaseDate.ToString('yyyy-MM-dd')
        Sequence             = $sequence
    }
}

function Get-ExistingSequence {
    param(
        [Parameter(Mandatory)]
        [string]$Candidate,

        [Parameter(Mandatory)]
        [string]$DisplayPrefix,

        [Parameter(Mandatory)]
        [string]$DottedPrefix
    )

    if ($Candidate -match "^$([regex]::Escape($DisplayPrefix))(?<sequence>\d{2})$") {
        return [int]$Matches.sequence
    }

    if ($Candidate -match "^$([regex]::Escape($DottedPrefix))(?<sequence>\d{2})$") {
        return [int]$Matches.sequence
    }

    return 0
}

function New-CalendarVersion {
    param(
        [Parameter(Mandatory)]
        [string]$RootPath
    )

    $today = Get-Date
    $displayPrefix = $today.ToString('yyyyMMdd')
    $dottedPrefix = $today.ToString('yyyy.MMdd')
    $maxSequence = 0

    $versionPath = Join-Path $RootPath 'Config\version.json'
    if (Test-Path $versionPath) {
        try {
            $versionJson = Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $versionProperty = $versionJson.PSObject.Properties['Version']
            if ($versionProperty -and $versionProperty.Value) {
                $maxSequence = [Math]::Max($maxSequence, (Get-ExistingSequence -Candidate ([string]$versionProperty.Value) -DisplayPrefix $displayPrefix -DottedPrefix $dottedPrefix))
            }
        } catch {
            Write-Verbose "Unable to inspect existing version.json: $($_.Exception.Message)"
        }
    }

    $csprojPath = Join-Path $RootPath 'GUI\WinForge.GUI\WinForge.GUI.csproj'
    if (Test-Path $csprojPath) {
        $csprojContent = Get-Content -Path $csprojPath -Raw -Encoding UTF8
        $infoMatch = [regex]::Match($csprojContent, '<InformationalVersion>(?<version>[^<]+)</InformationalVersion>')
        if ($infoMatch.Success) {
            $maxSequence = [Math]::Max($maxSequence, (Get-ExistingSequence -Candidate $infoMatch.Groups['version'].Value -DisplayPrefix $displayPrefix -DottedPrefix $dottedPrefix))
        }
    }

    $distRoot = Join-Path $RootPath 'Dist'
    if (Test-Path $distRoot) {
        Get-ChildItem -Path $distRoot -Name -ErrorAction SilentlyContinue |
            ForEach-Object {
                if ($_ -match '^WinForge_v(?<version>\d{10}|\d{4}\.\d{6})(?:\.zip)?$') {
                    $candidate = $Matches.version
                    $maxSequence = [Math]::Max($maxSequence, (Get-ExistingSequence -Candidate $candidate -DisplayPrefix $displayPrefix -DottedPrefix $dottedPrefix))
                }
            }
    }

    $nextSequence = $maxSequence + 1
    if ($nextSequence -gt 99) {
        throw "No calendar version slots remain for $displayPrefix."
    }

    return ConvertTo-CalendarVersion -InputVersion ('{0}{1:D2}' -f $displayPrefix, $nextSequence)
}

function Set-ProjectProperty {
    param(
        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $pattern = '<{0}>[^<]*</{0}>' -f [regex]::Escape($Name)
    if ($Content -notmatch $pattern) {
        throw "Missing <$Name> in GUI project file."
    }

    return $Content -replace $pattern, "<$Name>$Value</$Name>"
}

function Update-GuiProjectVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [pscustomobject]$VersionInfo
    )

    $content = Get-Content -Path $Path -Raw -Encoding UTF8
    $lineEnding = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

    $content = Set-ProjectProperty -Content $content -Name 'AssemblyVersion' -Value $VersionInfo.AssemblyVersion
    $content = Set-ProjectProperty -Content $content -Name 'FileVersion' -Value $VersionInfo.AssemblyVersion
    $content = Set-ProjectProperty -Content $content -Name 'Version' -Value $VersionInfo.AssemblyVersion

    if ($content -match '<InformationalVersion>') {
        $content = Set-ProjectProperty -Content $content -Name 'InformationalVersion' -Value $VersionInfo.InformationalVersion
    } else {
        $versionRegex = [regex]::new('(<Version>[^<]*</Version>)')
        $replacement = "`$1$lineEnding    <InformationalVersion>$($VersionInfo.InformationalVersion)</InformationalVersion>"
        $content = $versionRegex.Replace($content, $replacement, 1)
    }

    Write-Utf8NoBom -Path $Path -Value $content
}

$resolvedRoot = (Resolve-Path -Path $RootPath).Path
$versionPath = Join-Path $resolvedRoot 'Config\version.json'
$guiProjectPath = Join-Path $resolvedRoot 'GUI\WinForge.GUI\WinForge.GUI.csproj'

if (-not (Test-Path $versionPath)) {
    throw "Version file not found: $versionPath"
}
if (-not (Test-Path $guiProjectPath)) {
    throw "GUI project file not found: $guiProjectPath"
}

$versionInfo = if ([string]::IsNullOrWhiteSpace($Version)) {
    New-CalendarVersion -RootPath $resolvedRoot
} else {
    ConvertTo-CalendarVersion -InputVersion $Version
}

$versionJson = Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$displayNameProperty = $versionJson.PSObject.Properties['DisplayName']
$displayName = if ($displayNameProperty -and -not [string]::IsNullOrWhiteSpace([string]$displayNameProperty.Value)) {
    [string]$displayNameProperty.Value
} else {
    'WinForge Framework'
}

$updatedVersionJson = [ordered]@{
    DisplayName = $displayName
    Version     = $versionInfo.DisplayVersion
    ReleaseDate = $versionInfo.ReleaseDate
}

$json = ($updatedVersionJson | ConvertTo-Json -Depth 5) -replace "`r`n", "`n"
Write-Utf8NoBom -Path $versionPath -Value ($json + "`n")
Update-GuiProjectVersion -Path $guiProjectPath -VersionInfo $versionInfo

if ($PassThru) {
    return $versionInfo
}

Write-Host "Updated WinForge version:" -ForegroundColor Green
Write-Host "  Display:       $($versionInfo.DisplayVersion)"
Write-Host "  Assembly:      $($versionInfo.AssemblyVersion)"
Write-Host "  Release date:  $($versionInfo.ReleaseDate)"
