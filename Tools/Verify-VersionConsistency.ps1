<#
.SYNOPSIS
  Verifies that version strings across key scripts match Config/version.json.

.DESCRIPTION
  - Reads version from Config/version.json (source of truth)
  - Checks a whitelist of files for the same version string in headers/banners
  - Prints mismatches and exits with non-zero code if any

.USAGE
  pwsh -NoProfile -File .\Tools\Verify-VersionConsistency.ps1
#>

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

# Files to check and simple patterns that should contain the version
$files = @(
    @{ Path = Join-Path $repoRoot 'Start-Win11ForgeGUI.ps1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Deploy-Win11Environment.ps1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Modules\\InstallationEngine.psm1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Modules\\ProfileManager.psm1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Modules\\ApplicationDatabase.psm1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Modules\\EnvironmentDetection.psm1'; Pattern = "Version\s*[:=]?\s*%version%" },
    @{ Path = Join-Path $repoRoot 'Deploy-Win11Forge.bat'; Pattern = "Win11Forge Framework v%FRAMEWORK_VERSION%" },
    @{ Path = Join-Path $repoRoot 'Start-Win11ForgeGUI-Admin.bat'; Pattern = "GUI Launcher v%FRAMEWORK_VERSION%" }
)

$fail = $false
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

if ($fail) { exit 1 } else { exit 0 }
