<#
.SYNOPSIS
    PSScriptAnalyzer validation for Win11Forge

.DESCRIPTION
    Analyzes all PowerShell scripts and modules for code quality issues
    using PSScriptAnalyzer best practices

.PARAMETER Severity
    Minimum severity level: Error, Warning, Information (default: Warning)

.PARAMETER Fix
    Automatically fix issues where possible

.PARAMETER Report
    Generate HTML report

.EXAMPLE
    .\Invoke-PSScriptAnalyzer.ps1

.EXAMPLE
    .\Invoke-PSScriptAnalyzer.ps1 -Severity Error

.EXAMPLE
    .\Invoke-PSScriptAnalyzer.ps1 -Fix

.EXAMPLE
    .\Invoke-PSScriptAnalyzer.ps1 -Report

.NOTES
    Author: Julien Bombled
    Version: 3.7.2
    Requires: PSScriptAnalyzer
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

[CmdletBinding()]
param(
    [ValidateSet('Error', 'Warning', 'Information')]
    [string]$Severity = 'Warning',

    [switch]$Fix,

    [switch]$Report
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frameworkVersion = 'unknown'
$versionFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'Config\version.json'
if (Test-Path $versionFile) {
    try {
        $versionData = Get-Content -Path $versionFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($versionData.Version) {
            $frameworkVersion = [string]$versionData.Version
        }
    } catch {
        # Keep fallback when the version file cannot be read
    }
}

# === PREREQUISITES CHECK ===
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Win11Forge v$frameworkVersion - PSScriptAnalyzer" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check PSScriptAnalyzer installation
$pssa = Get-Module -Name PSScriptAnalyzer -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1

if (-not $pssa) {
    Write-Host "❌ PSScriptAnalyzer not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install with:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ PSScriptAnalyzer v$($pssa.Version) found" -ForegroundColor Green
Write-Host ""

Import-Module PSScriptAnalyzer -ErrorAction Stop

# === CONFIGURATION ===
$RootPath = Split-Path $PSScriptRoot -Parent
$ReportsPath = Join-Path $RootPath 'Reports'

# Create reports directory
if (-not (Test-Path $ReportsPath)) {
    New-Item -Path $ReportsPath -ItemType Directory -Force | Out-Null
}

# Files to analyze
$filesToAnalyze = @(
    # Main scripts
    (Join-Path $RootPath 'Deploy-Win11Environment.ps1'),
    (Join-Path $RootPath 'GUI.ps1'),

    # Core module
    (Join-Path $RootPath 'Core\Core.psm1'),

    # Modules
    (Join-Path $RootPath 'Modules\InstallationEngine.psm1'),
    (Join-Path $RootPath 'Modules\ApplicationDatabase.psm1'),
    (Join-Path $RootPath 'Modules\ProfileManager.psm1'),
    (Join-Path $RootPath 'Modules\EnvironmentDetection.psm1'),
    (Join-Path $RootPath 'Modules\Prerequisites.psm1'),
    (Join-Path $RootPath 'Modules\SystemConfig.psm1'),
    (Join-Path $RootPath 'Modules\StartMenuLayout.psm1'),
    (Join-Path $RootPath 'Modules\StartMenuPinning.psm1'),
    (Join-Path $RootPath 'Modules\StartupManager.psm1'),
    (Join-Path $RootPath 'Modules\Win11ForgeGUI.psm1')
)

# Filter existing files
$filesToAnalyze = $filesToAnalyze | Where-Object { Test-Path $_ }

Write-Host "Files to analyze: $($filesToAnalyze.Count)" -ForegroundColor White
Write-Host "Minimum severity: $Severity" -ForegroundColor White
Write-Host ""

# === ANALYSIS ===
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Running Analysis" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$allIssues = @()
$fileResults = @()

foreach ($file in $filesToAnalyze) {
    $fileName = Split-Path $file -Leaf
    Write-Host "Analyzing: $fileName" -ForegroundColor Yellow

    try {
        $settingsPath = Join-Path $RootPath 'PSScriptAnalyzerSettings.psd1'
        $invokeParams = @{
            Path = $file
            Severity = $Severity
        }
        if (Test-Path $settingsPath) {
            $invokeParams['Settings'] = $settingsPath
        }
        $issues = @(Invoke-ScriptAnalyzer @invokeParams)

        if ($issues) {
            $allIssues += $issues

            # Group by severity
            $errors = @($issues | Where-Object { $_.Severity -eq 'Error' })
            $warnings = @($issues | Where-Object { $_.Severity -eq 'Warning' })
            $information = @($issues | Where-Object { $_.Severity -eq 'Information' })

            # Ensure arrays are properly initialized
            if ($null -eq $errors) { $errors = @() }
            if ($null -eq $warnings) { $warnings = @() }
            if ($null -eq $information) { $information = @() }

            Write-Host "  Errors      : $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { 'Red' } else { 'Green' })
            Write-Host "  Warnings    : $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { 'Yellow' } else { 'Green' })
            Write-Host "  Information : $($information.Count)" -ForegroundColor Cyan

            $fileResults += [PSCustomObject]@{
                File = $fileName
                Errors = $errors.Count
                Warnings = $warnings.Count
                Information = $information.Count
                Total = $issues.Count
            }
        } else {
            Write-Host "  ✅ No issues found" -ForegroundColor Green
            $fileResults += [PSCustomObject]@{
                File = $fileName
                Errors = 0
                Warnings = 0
                Information = 0
                Total = 0
            }
        }

        Write-Host ""

    } catch {
        Write-Host "  ❌ Analysis failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""

        # Add to file results as failure
        $fileResults += [PSCustomObject]@{
            File = $fileName
            Errors = 0
            Warnings = 0
            Information = 0
            Total = 0
        }
    }
}

# === RESULTS SUMMARY ===
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Analysis Summary" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$totalErrors = @($allIssues | Where-Object { $_.Severity -eq 'Error' }).Count
$totalWarnings = @($allIssues | Where-Object { $_.Severity -eq 'Warning' }).Count
$totalInformation = @($allIssues | Where-Object { $_.Severity -eq 'Information' }).Count

Write-Host "Files Analyzed   : $($filesToAnalyze.Count)" -ForegroundColor White
Write-Host "Total Issues     : $($allIssues.Count)" -ForegroundColor White
Write-Host "  Errors         : $totalErrors" -ForegroundColor $(if ($totalErrors -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Warnings       : $totalWarnings" -ForegroundColor $(if ($totalWarnings -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  Information    : $totalInformation" -ForegroundColor Cyan
Write-Host ""

# === DETAILED ISSUES ===
if ($allIssues.Count -gt 0) {
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Detailed Issues" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    # Group by rule
    $issuesByRule = $allIssues | Group-Object RuleName | Sort-Object Count -Descending

    foreach ($group in $issuesByRule) {
        $ruleName = $group.Name
        $count = $group.Count
        $severity = $group.Group[0].Severity

        $color = switch ($severity) {
            'Error' { 'Red' }
            'Warning' { 'Yellow' }
            'Information' { 'Cyan' }
        }

        Write-Host "[$severity] $ruleName ($count occurrences)" -ForegroundColor $color

        # Show first 3 examples
        $examples = $group.Group | Select-Object -First 3
        foreach ($example in $examples) {
            $file = Split-Path $example.ScriptPath -Leaf
            Write-Host "  $file`:$($example.Line) - $($example.Message)" -ForegroundColor Gray
        }

        if ($group.Count -gt 3) {
            Write-Host "  ... and $($group.Count - 3) more" -ForegroundColor DarkGray
        }

        Write-Host ""
    }
}

# === AUTO-FIX ===
if ($Fix -and $allIssues.Count -gt 0) {
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Auto-Fix" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    foreach ($file in $filesToAnalyze) {
        $fileName = Split-Path $file -Leaf
        Write-Host "Fixing: $fileName" -ForegroundColor Yellow

        try {
            $fixed = Invoke-ScriptAnalyzer -Path $file -Fix
            if ($fixed) {
                Write-Host "  ✅ Fixed $($fixed.Count) issues" -ForegroundColor Green
            } else {
                Write-Host "  No auto-fixable issues" -ForegroundColor Gray
            }
        } catch {
            Write-Host "  ❌ Fix failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
}

# === REPORT GENERATION ===
if ($Report) {
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Generating Report" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $reportPath = Join-Path $ReportsPath "PSScriptAnalyzer_$timestamp.html"

    # Generate HTML report
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>PSScriptAnalyzer Report - Win11Forge v$frameworkVersion</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
        h1 { color: #0078d4; }
        h2 { color: #333; border-bottom: 2px solid #0078d4; padding-bottom: 5px; }
        table { border-collapse: collapse; width: 100%; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        th { background: #0078d4; color: white; padding: 10px; text-align: left; }
        td { padding: 8px; border-bottom: 1px solid #ddd; }
        .error { color: #d13438; font-weight: bold; }
        .warning { color: #ff8c00; font-weight: bold; }
        .information { color: #0078d4; }
        .summary { background: white; padding: 15px; margin: 20px 0; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .metric { display: inline-block; margin: 10px 20px 10px 0; }
        .metric-value { font-size: 24px; font-weight: bold; }
        .metric-label { color: #666; font-size: 14px; }
    </style>
</head>
<body>
    <h1>PSScriptAnalyzer Report</h1>
    <p><strong>Win11Forge Framework v$frameworkVersion</strong></p>
    <p>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>

    <div class="summary">
        <h2>Summary</h2>
        <div class="metric">
            <div class="metric-value">$($filesToAnalyze.Count)</div>
            <div class="metric-label">Files Analyzed</div>
        </div>
        <div class="metric">
            <div class="metric-value">$($allIssues.Count)</div>
            <div class="metric-label">Total Issues</div>
        </div>
        <div class="metric">
            <div class="metric-value error">$totalErrors</div>
            <div class="metric-label">Errors</div>
        </div>
        <div class="metric">
            <div class="metric-value warning">$totalWarnings</div>
            <div class="metric-label">Warnings</div>
        </div>
        <div class="metric">
            <div class="metric-value information">$totalInformation</div>
            <div class="metric-label">Information</div>
        </div>
    </div>

    <h2>Results by File</h2>
    <table>
        <tr>
            <th>File</th>
            <th>Errors</th>
            <th>Warnings</th>
            <th>Information</th>
            <th>Total</th>
        </tr>
"@

    foreach ($result in $fileResults) {
        $html += @"
        <tr>
            <td>$($result.File)</td>
            <td class="error">$($result.Errors)</td>
            <td class="warning">$($result.Warnings)</td>
            <td class="information">$($result.Information)</td>
            <td>$($result.Total)</td>
        </tr>
"@
    }

    $html += @"
    </table>

    <h2>Issues by Rule</h2>
    <table>
        <tr>
            <th>Rule</th>
            <th>Severity</th>
            <th>Count</th>
            <th>Description</th>
        </tr>
"@

    foreach ($group in $issuesByRule) {
        $ruleName = $group.Name
        $count = $group.Count
        $severity = $group.Group[0].Severity
        $message = $group.Group[0].Message

        $severityClass = $severity.ToLower()

        $html += @"
        <tr>
            <td>$ruleName</td>
            <td class="$severityClass">$severity</td>
            <td>$count</td>
            <td>$message</td>
        </tr>
"@
    }

    $html += @"
    </table>
</body>
</html>
"@

    $html | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "✅ Report generated: $reportPath" -ForegroundColor Green
    Write-Host ""
}

# === EXIT CODE ===
if ($totalErrors -gt 0) {
    Write-Host "❌ Analysis FAILED (errors found)" -ForegroundColor Red
    exit 1
} elseif ($totalWarnings -gt 0) {
    Write-Host "⚠️  Analysis completed with warnings" -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "✅ Analysis PASSED (no issues)" -ForegroundColor Green
    exit 0
}
