<#
.SYNOPSIS
    Test runner for Win11Forge

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
    Author: Julien Bombled
    Version: 3.7.2
    Requires: Pester v5+
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
    [switch]$Coverage,
    [ValidateSet('None', 'NUnitXml', 'JUnitXml')]
    [string]$OutputFormat = 'None'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$coverageTargetPercent = 40
$coverageGateFailed = $false

$frameworkVersion = 'unknown'
$versionFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'Config\version.json'
if (Test-Path $versionFile) {
    try {
        $versionData = Get-Content -Path $versionFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($versionData.Version) {
            $frameworkVersion = [string]$versionData.Version
        }
    } catch {
        # Keep fallback value when version file cannot be parsed
        Write-Verbose "Unable to read framework version: $($_.Exception.Message)"
    }
}

# Remove any pre-loaded Pester modules to avoid version conflicts
Get-Module Pester | Remove-Module -Force -ErrorAction SilentlyContinue

# Ensure user modules path is in PSModulePath
$userModulesPath = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\Modules'
if ($userModulesPath -and (Test-Path $userModulesPath) -and $env:PSModulePath -notlike "*$userModulesPath*") {
    $env:PSModulePath = "$userModulesPath;$env:PSModulePath"
}

# === PREREQUISITES CHECK ===
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Win11Forge v$frameworkVersion - Test Runner" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check Pester installation and import v5+
$pesterModules = @(Get-Module -Name Pester -ListAvailable -ErrorAction SilentlyContinue | Sort-Object Version -Descending)
$pesterV5 = $pesterModules | Where-Object { $_.Version.Major -ge 5 } | Select-Object -First 1

if (-not $pesterV5) {
    Write-Host "[ERROR] Pester v5+ not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install Pester v5+ with:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Yellow
    exit 1
}

# Import Pester v5+ explicitly
Import-Module Pester -RequiredVersion $pesterV5.Version -Force -ErrorAction Stop

Write-Host "[OK] Pester v$($pesterV5.Version) loaded" -ForegroundColor Green
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
    Write-Host "[INFO] Code coverage enabled" -ForegroundColor Yellow
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
    $config.CodeCoverage.CoveragePercentTarget = $coverageTargetPercent
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
if ($Coverage) {
    if ($result.CodeCoverage) {
        $coverageResult = $result.CodeCoverage
        $coveragePercent = $null
        $coveredCommands = $null
        $commandsAnalyzed = $null

        if ($coverageResult.PSObject.Properties['CoveredPercent']) {
            $coveragePercent = [math]::Round([double]$coverageResult.CoveredPercent, 2)
        } elseif ($coverageResult.PSObject.Properties['CoveragePercent']) {
            $coveragePercent = [math]::Round([double]$coverageResult.CoveragePercent, 2)
        }

        if ($coverageResult.PSObject.Properties['CoveredCommands']) {
            $coveredCommands = $coverageResult.CoveredCommands
        } elseif ($coverageResult.PSObject.Properties['NumberOfCommandsExecuted']) {
            $coveredCommands = $coverageResult.NumberOfCommandsExecuted
        }

        if ($coverageResult.PSObject.Properties['CommandsAnalyzed']) {
            $commandsAnalyzed = $coverageResult.CommandsAnalyzed
        } elseif ($coverageResult.PSObject.Properties['NumberOfCommandsAnalyzed']) {
            $commandsAnalyzed = $coverageResult.NumberOfCommandsAnalyzed
        }

        Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host "  Code Coverage Summary" -ForegroundColor Cyan
        Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host ""

        if ($null -ne $coveragePercent) {
            Write-Host "Coverage         : $($coveragePercent) percent" -ForegroundColor $(if ($coveragePercent -ge $coverageTargetPercent) { 'Green' } else { 'Yellow' })
        } else {
            Write-Host "Coverage         : unavailable (Pester object shape differs)" -ForegroundColor Yellow
        }

        if ($null -ne $coveredCommands -and $null -ne $commandsAnalyzed) {
            Write-Host "Commands Covered : $coveredCommands / $commandsAnalyzed" -ForegroundColor White
        }
        Write-Host ""

        if ($null -ne $coveragePercent -and $coveragePercent -ge $coverageTargetPercent) {
            Write-Host "[OK] Coverage target ($coverageTargetPercent percent) achieved!" -ForegroundColor Green
        } elseif ($null -ne $coveragePercent) {
            Write-Host "[WARN] Coverage below target ($coverageTargetPercent percent)" -ForegroundColor Yellow
            $coverageGateFailed = $true
        } else {
            Write-Host "[WARN] Coverage target check failed (percent unavailable)" -ForegroundColor Yellow
            $coverageGateFailed = $true
        }
        Write-Host ""
    } else {
        Write-Host "[WARN] Coverage was requested but no coverage result was produced" -ForegroundColor Yellow
        Write-Host ""
        $coverageGateFailed = $true
    }
}

# === EXIT CODE ===
if ($result.FailedCount -gt 0 -or $coverageGateFailed) {
    if ($result.FailedCount -gt 0) {
        Write-Host "[FAILED] Tests FAILED" -ForegroundColor Red
    }
    if ($coverageGateFailed) {
        Write-Host "[FAILED] Coverage gate FAILED (minimum: $coverageTargetPercent percent)" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "[OK] All tests PASSED" -ForegroundColor Green
    exit 0
}
