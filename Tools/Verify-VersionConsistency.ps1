<#
.SYNOPSIS
  Verifies calendar display and compatible assembly/module versions.

.DESCRIPTION
  - Reads YYYYMMDDxx from Config/version.json (display source of truth)
  - Verifies GUI project assembly metadata uses 1.0.MMDD.sequence
  - Verifies PowerShell manifests use the same compatible version
  - Checks static launcher patterns still read the dynamic framework version
  - Prints mismatches and exits with non-zero code if any

.EXAMPLE
  pwsh -NoProfile -File .\Tools\Verify-VersionConsistency.ps1

.NOTES
  Author: Julien Bombled
  Version: 1.0.0
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
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionPath = Join-Path $repoRoot 'Config\version.json'
if (-not (Test-Path $versionPath)) {
    Write-Error "Version file not found: $versionPath"
    exit 2
}

$version = (Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json).Version
if (-not $version) {
    Write-Error "Version missing from $versionPath"
    exit 2
}

function ConvertTo-VersionInfo {
    param([Parameter(Mandatory)][string]$DisplayVersion)

    if ($DisplayVersion -notmatch '^(?<year>\d{4})(?<mmdd>\d{4})(?<sequence>\d{2})$') {
        throw "Config/version.json Version must use YYYYMMDDxx format. Found: $DisplayVersion"
    }

    $year = [int]$Matches.year
    $mmdd = $Matches.mmdd
    $sequence = [int]$Matches.sequence
    if ($sequence -lt 1 -or $sequence -gt 99) {
        throw "Calendar version sequence must be between 01 and 99. Found: $DisplayVersion"
    }

    $month = [int]$mmdd.Substring(0, 2)
    $day = [int]$mmdd.Substring(2, 2)
    try {
        $releaseDate = [datetime]::new($year, $month, $day)
    } catch {
        throw "Calendar version date is invalid. Found: $DisplayVersion"
    }

    return [PSCustomObject]@{
        DisplayVersion  = $DisplayVersion
        AssemblyVersion = '1.0.{0}.{1}' -f $mmdd, $sequence
        ReleaseDate     = $releaseDate.ToString('yyyy-MM-dd')
    }
}

$versionInfo = $null
try {
    $versionInfo = ConvertTo-VersionInfo -DisplayVersion ([string]$version)
} catch {
    Write-Host "[MISMATCH] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$versionData = Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$fail = $false

if ($versionData.ReleaseDate -ne $versionInfo.ReleaseDate) {
    Write-Host "[MISMATCH] $versionPath ReleaseDate should be $($versionInfo.ReleaseDate), found $($versionData.ReleaseDate)" -ForegroundColor Red
    $fail = $true
} else {
    Write-Host "[OK] $versionPath uses display version $($versionInfo.DisplayVersion)" -ForegroundColor Green
}

# Files to check and simple patterns that should contain the version
# Note: PS1/PSM1 files read version dynamically from Config/version.json at runtime
# We only verify static files (batch launchers) that have hardcoded version references
$files = @(
    @{ Path = Join-Path $repoRoot 'Deploy-Win11Forge.bat'; Pattern = "Win11Forge Framework v%FRAMEWORK_VERSION%" },
    @{ Path = Join-Path $repoRoot 'Start-Win11ForgeGUI-Admin.bat'; Pattern = "GUI Launcher v%FRAMEWORK_VERSION%" },
    @{ Path = Join-Path $repoRoot 'Config\version.json'; Pattern = '"Version"\s*:\s*"%version%"' }
)

foreach ($f in $files) {
    if (-not (Test-Path $f.Path)) { continue }
    $content = Get-Content -Path $f.Path -Raw -ErrorAction Stop
    $pattern = $f.Pattern -replace '%version%', [Regex]::Escape($version)
    if (-not [Regex]::IsMatch($content, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        Write-Host "[MISMATCH] $($f.Path) does not contain expected version pattern: $($f.Pattern)" -ForegroundColor Red
        $fail = $true
    } else {
        Write-Host "[OK] $($f.Path) matches version $version" -ForegroundColor Green
    }
}

$guiProjectPath = Join-Path $repoRoot 'GUI\Win11Forge.GUI\Win11Forge.GUI.csproj'
if (-not (Test-Path $guiProjectPath)) {
    Write-Host "[MISMATCH] GUI project not found: $guiProjectPath" -ForegroundColor Red
    $fail = $true
} else {
    [xml]$guiProject = Get-Content -Path $guiProjectPath -Raw -Encoding UTF8
    $propertyGroup = $guiProject.Project.PropertyGroup | Select-Object -First 1
    $projectVersions = @{
        AssemblyVersion      = [string]$propertyGroup.AssemblyVersion
        FileVersion          = [string]$propertyGroup.FileVersion
        Version              = [string]$propertyGroup.Version
        InformationalVersion = [string]$propertyGroup.InformationalVersion
    }

    foreach ($name in @('AssemblyVersion', 'FileVersion', 'Version')) {
        if ($projectVersions[$name] -ne $versionInfo.AssemblyVersion) {
            Write-Host "[MISMATCH] $guiProjectPath <$name> should be $($versionInfo.AssemblyVersion), found $($projectVersions[$name])" -ForegroundColor Red
            $fail = $true
        } else {
            Write-Host "[OK] $guiProjectPath <$name> matches $($versionInfo.AssemblyVersion)" -ForegroundColor Green
        }
    }

    if ($projectVersions.InformationalVersion -ne $versionInfo.DisplayVersion) {
        Write-Host "[MISMATCH] $guiProjectPath <InformationalVersion> should be $($versionInfo.DisplayVersion), found $($projectVersions.InformationalVersion)" -ForegroundColor Red
        $fail = $true
    } else {
        Write-Host "[OK] $guiProjectPath <InformationalVersion> matches $($versionInfo.DisplayVersion)" -ForegroundColor Green
    }
}

$manifestRoots = @(
    Join-Path $repoRoot 'Core',
    Join-Path $repoRoot 'Modules'
)

foreach ($manifest in Get-ChildItem -Path $manifestRoots -Filter *.psd1 -ErrorAction SilentlyContinue) {
    $content = Get-Content -Path $manifest.FullName -Raw -Encoding UTF8
    if ($content -match "ModuleVersion = '([^']+)'") {
        $moduleVersion = $Matches[1]
    } else {
        Write-Host "[MISMATCH] $($manifest.FullName) missing ModuleVersion" -ForegroundColor Red
        $fail = $true
        continue
    }

    if ($moduleVersion -ne $versionInfo.AssemblyVersion) {
        Write-Host "[MISMATCH] $($manifest.FullName) ModuleVersion should be $($versionInfo.AssemblyVersion), found $moduleVersion" -ForegroundColor Red
        $fail = $true
    }

    if ($content -match "ReleaseNotes = 'Win11Forge v([^']+)'") {
        $releaseNotesVersion = $Matches[1]
        if ($releaseNotesVersion -ne $versionInfo.DisplayVersion) {
            Write-Host "[MISMATCH] $($manifest.FullName) ReleaseNotes should use Win11Forge v$($versionInfo.DisplayVersion), found Win11Forge v$releaseNotesVersion" -ForegroundColor Red
            $fail = $true
        }
    }
}

if ($fail) { exit 1 } else { exit 0 }
