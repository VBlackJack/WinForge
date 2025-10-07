<#
.SYNOPSIS
    Analyzes Win11Forge project for inconsistencies

.DESCRIPTION
    Comprehensive analysis of:
    - Version numbers across all files
    - Database integrity (applications.json)
    - Module exports vs imports
    - File naming conventions
    - Documentation consistency
#>

$ErrorActionPreference = 'Continue'
$report = @{
    VersionInconsistencies = @()
    DatabaseIssues = @()
    ModuleIssues = @()
    NamingIssues = @()
    DocumentationIssues = @()
}

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  Win11Forge Project Consistency Analysis" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# === VERSION CONSISTENCY ===
Write-Host "[1/5] Checking version consistency..." -ForegroundColor Yellow

$versionPattern = 'v?(\d+\.\d+\.\d+)'
$versions = @{}

# Check README.md
$readmeContent = Get-Content 'README.md' -Raw
if ($readmeContent -match "Win11Forge Framework v$versionPattern") {
    $versions['README.md'] = $matches[1]
}

# Check Deploy-Win11Environment.ps1
$deployContent = Get-Content 'Deploy-Win11Environment.ps1' -Raw
if ($deployContent -match "Version:\s*$versionPattern") {
    $versions['Deploy-Win11Environment.ps1 (header)'] = $matches[1]
}
if ($deployContent -match "Win11Forge Framework v$versionPattern") {
    $versions['Deploy-Win11Environment.ps1 (log)'] = $matches[1]
}

# Check all modules
Get-ChildItem -Path 'Modules' -Filter '*.psm1' | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "Version:\s*$versionPattern") {
        $versions["$($_.Name)"] = $matches[1]
    }
}

# Check Core
$coreContent = Get-Content 'Core/Core.psm1' -Raw
if ($coreContent -match "Version:\s*$versionPattern") {
    $versions['Core/Core.psm1'] = $matches[1]
}

# Check database
$db = Get-Content 'Apps/Database/applications.json' | ConvertFrom-Json
$versions['applications.json'] = $db.DatabaseVersion

# Analyze versions
$uniqueVersions = @($versions.Values | Select-Object -Unique)  # Force array
if ($uniqueVersions.Count -gt 1) {
    $report.VersionInconsistencies += "Found $($uniqueVersions.Count) different versions: $($uniqueVersions -join ', ')"
    $versions.GetEnumerator() | ForEach-Object {
        $report.VersionInconsistencies += "  $($_.Key): $($_.Value)"
    }
} else {
    Write-Host "  ✓ All versions consistent: $($uniqueVersions[0])" -ForegroundColor Green
}

# === DATABASE CONSISTENCY ===
Write-Host "`n[2/5] Checking database consistency..." -ForegroundColor Yellow

$dbPath = 'Apps/Database/applications.json'
$db = Get-Content $dbPath | ConvertFrom-Json

# Check TotalApplications
$actualCount = ($db.Applications.PSObject.Properties | Measure-Object).Count
if ($db.TotalApplications -ne $actualCount) {
    $report.DatabaseIssues += "TotalApplications mismatch: declared=$($db.TotalApplications), actual=$actualCount"
}

# Check all apps have required properties
$requiredProps = @('Name', 'Category', 'Sources', 'Detection')
$db.Applications.PSObject.Properties | ForEach-Object {
    $app = $_.Value
    $appId = $_.Name

    foreach ($prop in $requiredProps) {
        if (-not $app.$prop) {
            $report.DatabaseIssues += "App '$appId' missing property: $prop"
        }
    }

    # Check at least one source (skip Windows Features - they don't need sources)
    if ($app.InstallMethod -ne 'WindowsFeature') {
        if (-not ($app.Sources.Winget -or $app.Sources.Chocolatey -or $app.Sources.Store -or $app.Sources.DirectUrl)) {
            $report.DatabaseIssues += "App '$appId' has no installation sources"
        }
    }
}

if ($report.DatabaseIssues.Count -eq 0) {
    Write-Host "  ✓ Database integrity validated: $actualCount apps" -ForegroundColor Green
} else {
    Write-Host "  ✗ Found $($report.DatabaseIssues.Count) database issues" -ForegroundColor Red
}

# === MODULE EXPORTS/IMPORTS ===
Write-Host "`n[3/5] Checking module exports..." -ForegroundColor Yellow

Get-ChildItem -Path 'Modules' -Filter '*.psm1' | ForEach-Object {
    $moduleName = $_.BaseName
    $content = Get-Content $_.FullName -Raw

    # Check if module has Export-ModuleMember
    if ($content -notmatch 'Export-ModuleMember') {
        $report.ModuleIssues += "${moduleName}: No Export-ModuleMember statement"
    }

    # Check function naming (should use approved verbs)
    $functions = [regex]::Matches($content, 'function\s+([A-Z][a-z]+-[A-Z]\w+)')
    foreach ($match in $functions) {
        $funcName = $match.Groups[1].Value
        $verb = $funcName.Split('-')[0]

        $approvedVerbs = Get-Verb | Select-Object -ExpandProperty Verb
        if ($verb -notin $approvedVerbs -and $verb -ne 'Check') {  # Check is legacy but common
            $report.ModuleIssues += "${moduleName}: Function '$funcName' uses non-approved verb '$verb'"
        }
    }
}

if ($report.ModuleIssues.Count -eq 0) {
    Write-Host "  ✓ All modules properly configured" -ForegroundColor Green
}

# === NAMING CONVENTIONS ===
Write-Host "`n[4/5] Checking naming conventions..." -ForegroundColor Yellow

# Check profile naming
$profiles = Get-ChildItem -Path 'Profiles' -Filter '*.json' -ErrorAction SilentlyContinue
$profiles | ForEach-Object {
    if ($_.BaseName -notmatch '^[A-Z][a-zA-Z0-9_-]+$') {
        $report.NamingIssues += "Profile name doesn't follow convention: $($_.Name)"
    }
}

# Check for duplicate function names across modules
$allFunctions = @{}
Get-ChildItem -Path 'Modules','Core' -Include '*.psm1' -Recurse | ForEach-Object {
    $moduleName = $_.Name
    $content = Get-Content $_.FullName -Raw
    $functions = [regex]::Matches($content, 'function\s+([A-Z][a-z]+-[A-Z]\w+)')

    foreach ($match in $functions) {
        $funcName = $match.Groups[1].Value
        if ($allFunctions.ContainsKey($funcName)) {
            $report.NamingIssues += "Duplicate function '$funcName' in $moduleName and $($allFunctions[$funcName])"
        } else {
            $allFunctions[$funcName] = $moduleName
        }
    }
}

if ($report.NamingIssues.Count -eq 0) {
    Write-Host "  ✓ Naming conventions followed" -ForegroundColor Green
}

# === DOCUMENTATION CONSISTENCY ===
Write-Host "`n[5/5] Checking documentation..." -ForegroundColor Yellow

# Check if README mentions correct version
$readmeVersion = if ($readmeContent -match "v$versionPattern") { $matches[1] } else { "unknown" }
$actualVersion = $uniqueVersions[0]

if ($readmeVersion -ne $actualVersion) {
    $report.DocumentationIssues += "README.md version ($readmeVersion) doesn't match project version ($actualVersion)"
}

# Check if Apps/README.md is in sync with database
if (Test-Path 'Apps/README.md') {
    $appsReadme = Get-Content 'Apps/README.md' -Raw
    if ($appsReadme -match "Total d'applications\*\* : (\d+)") {
        $docCount = [int]$matches[1]
        if ($docCount -ne $actualCount) {
            $report.DocumentationIssues += "Apps/README.md app count ($docCount) doesn't match database ($actualCount)"
        }
    }
}

if ($report.DocumentationIssues.Count -eq 0) {
    Write-Host "  ✓ Documentation up to date" -ForegroundColor Green
}

# === GENERATE REPORT ===
Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  Analysis Summary" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

$totalIssues = $report.VersionInconsistencies.Count +
               $report.DatabaseIssues.Count +
               $report.ModuleIssues.Count +
               $report.NamingIssues.Count +
               $report.DocumentationIssues.Count

if ($totalIssues -eq 0) {
    Write-Host "✓ No inconsistencies found! Project is perfectly consistent." -ForegroundColor Green
} else {
    Write-Host "Found $totalIssues inconsistencies:`n" -ForegroundColor Yellow

    if ($report.VersionInconsistencies.Count -gt 0) {
        Write-Host "VERSION INCONSISTENCIES ($($report.VersionInconsistencies.Count)):" -ForegroundColor Red
        $report.VersionInconsistencies | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        Write-Host ""
    }

    if ($report.DatabaseIssues.Count -gt 0) {
        Write-Host "DATABASE ISSUES ($($report.DatabaseIssues.Count)):" -ForegroundColor Red
        $report.DatabaseIssues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        Write-Host ""
    }

    if ($report.ModuleIssues.Count -gt 0) {
        Write-Host "MODULE ISSUES ($($report.ModuleIssues.Count)):" -ForegroundColor Red
        $report.ModuleIssues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        Write-Host ""
    }

    if ($report.NamingIssues.Count -gt 0) {
        Write-Host "NAMING ISSUES ($($report.NamingIssues.Count)):" -ForegroundColor Red
        $report.NamingIssues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        Write-Host ""
    }

    if ($report.DocumentationIssues.Count -gt 0) {
        Write-Host "DOCUMENTATION ISSUES ($($report.DocumentationIssues.Count)):" -ForegroundColor Red
        $report.DocumentationIssues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
        Write-Host ""
    }
}

Write-Host "Analysis complete." -ForegroundColor Cyan
