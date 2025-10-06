<#
.SYNOPSIS
    Test runner for Win11Forge v2.5.0

.DESCRIPTION
    Executes all Pester tests and generates coverage report

.PARAMETER Coverage
    Generate code coverage report

.PARAMETER OutputFormat
    Output format: NUnitXml, JUnitXml, or None (default: None)

.EXAMPLE
    .\Invoke-Tests.ps1

.EXAMPLE
    .\Invoke-Tests.ps1 -Coverage

.EXAMPLE
    .\Invoke-Tests.ps1 -OutputFormat NUnitXml

.NOTES
    Author: Win11Forge Team
    Version: 2.5.0
    Requires: Pester v5+
#>

[CmdletBinding()]
param(
    [switch]$Coverage,
    [ValidateSet('None', 'NUnitXml', 'JUnitXml')]
    [string]$OutputFormat = 'None'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# === PREREQUISITES CHECK ===
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Win11Forge v2.5.0 - Test Runner" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check Pester installation
$pesterVersion = Get-Module -Name Pester -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1

if (-not $pesterVersion) {
    Write-Host "❌ Pester module not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install Pester v5+ with:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Yellow
    exit 1
}

if ($pesterVersion.Version.Major -lt 5) {
    Write-Host "❌ Pester v5+ required (found v$($pesterVersion.Version))" -ForegroundColor Red
    Write-Host ""
    Write-Host "Update Pester with:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Pester v$($pesterVersion.Version) found" -ForegroundColor Green
Write-Host ""

# === CONFIGURATION ===
$TestsPath = $PSScriptRoot
$ResultsPath = Join-Path $TestsPath 'Results'

# Create results directory
if (-not (Test-Path $ResultsPath)) {
    New-Item -Path $ResultsPath -ItemType Directory -Force | Out-Null
}

# === PESTER CONFIGURATION ===
$config = New-PesterConfiguration

# Test discovery
$config.Run.Path = $TestsPath
$config.Run.PassThru = $true

# Output
$config.Output.Verbosity = 'Detailed'

# Test results
if ($OutputFormat -ne 'None') {
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $resultFile = Join-Path $ResultsPath "TestResults_$timestamp.$OutputFormat"

    if ($OutputFormat -eq 'NUnitXml') {
        $config.TestResult.Enabled = $true
        $config.TestResult.OutputFormat = 'NUnitXml'
        $config.TestResult.OutputPath = $resultFile
    } elseif ($OutputFormat -eq 'JUnitXml') {
        $config.TestResult.Enabled = $true
        $config.TestResult.OutputFormat = 'JUnitXml'
        $config.TestResult.OutputPath = $resultFile
    }
}

# Code coverage
if ($Coverage) {
    Write-Host "📊 Code coverage enabled" -ForegroundColor Yellow
    Write-Host ""

    $config.CodeCoverage.Enabled = $true
    $config.CodeCoverage.Path = @(
        (Join-Path $PSScriptRoot '..\Modules\InstallationEngine.psm1'),
        (Join-Path $PSScriptRoot '..\Modules\ApplicationDatabase.psm1'),
        (Join-Path $PSScriptRoot '..\Modules\ProfileManager.psm1'),
        (Join-Path $PSScriptRoot '..\Modules\EnvironmentDetection.psm1')
    )
    $config.CodeCoverage.OutputPath = Join-Path $ResultsPath "Coverage_$(Get-Date -Format 'yyyyMMdd_HHmmss').xml"
    $config.CodeCoverage.OutputFormat = 'JaCoCo'
}

# === RUN TESTS ===
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Running Tests" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$result = Invoke-Pester -Configuration $config

# === RESULTS SUMMARY ===
Write-Host ""
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Test Results Summary" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "Total Tests      : $($result.TotalCount)" -ForegroundColor White
Write-Host "Passed           : $($result.PassedCount)" -ForegroundColor Green
Write-Host "Failed           : $($result.FailedCount)" -ForegroundColor $(if ($result.FailedCount -gt 0) { 'Red' } else { 'Green' })
Write-Host "Skipped          : $($result.SkippedCount)" -ForegroundColor Yellow
Write-Host "Duration         : $($result.Duration.TotalSeconds) seconds" -ForegroundColor White
Write-Host ""

# Coverage summary
if ($Coverage -and $result.CodeCoverage) {
    $coverage = $result.CodeCoverage
    $coveragePercent = [math]::Round(($coverage.CoveredPercent), 2)

    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Code Coverage Summary" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Coverage         : $coveragePercent%" -ForegroundColor $(if ($coveragePercent -ge 50) { 'Green' } else { 'Yellow' })
    Write-Host "Commands Covered : $($coverage.CoveredCommands) / $($coverage.CommandsAnalyzed)" -ForegroundColor White
    Write-Host ""

    if ($coveragePercent -ge 50) {
        Write-Host "✅ Coverage target (50%) achieved!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Coverage below target (50%)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# === EXIT CODE ===
if ($result.FailedCount -gt 0) {
    Write-Host "❌ Tests FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "✅ All tests PASSED" -ForegroundColor Green
    exit 0
}
