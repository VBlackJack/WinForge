<#
.SYNOPSIS
    Tests the health of application installation sources

.DESCRIPTION
    Validates that configured installation sources (Winget, Chocolatey, DirectUrl) are
    available and functional. Reports healthy, degraded, and critical applications.

.PARAMETER ValidateWinget
    Validate Winget package IDs (requires winget installed)

.PARAMETER ValidateChocolatey
    Validate Chocolatey packages (requires choco installed)

.PARAMETER ValidateDirectUrl
    Validate DirectUrl reachability via HTTP HEAD requests

.PARAMETER Repair
    Attempt automatic repair of detected issues

.PARAMETER GenerateReport
    Generate a detailed console report

.EXAMPLE
    .\Tools\Test-AppSources.ps1 -ValidateWinget -ValidateChocolatey -ValidateDirectUrl

.EXAMPLE
    .\Tools\Test-AppSources.ps1 -ValidateWinget -Repair

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

param(
    [switch]$ValidateWinget,
    [switch]$ValidateChocolatey,
    [switch]$ValidateDirectUrl,
    [switch]$Repair,
    [switch]$GenerateReport
)

# Import required modules
$scriptRoot = Split-Path -Parent $PSScriptRoot
$sourceHealthPath = Join-Path $scriptRoot "Modules\SourceHealthCheck.psm1"
if (-not (Test-Path $sourceHealthPath)) {
    Write-Error "SourceHealthCheck module not found at: $sourceHealthPath"
    exit 1
}

Import-Module $sourceHealthPath -Force

# Display banner
Write-Host "`n=======================================" -ForegroundColor Cyan
Write-Host "  Application Source Health Check v1.0" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# If no specific checks requested, enable all
if (-not $ValidateWinget -and -not $ValidateChocolatey -and -not $ValidateDirectUrl) {
    $ValidateWinget = $true
    $ValidateChocolatey = $true
    $ValidateDirectUrl = $true
}

# Check prerequisites
if ($ValidateWinget -and -not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "`nWinget not found - skipping Winget validation" -ForegroundColor Yellow
    $ValidateWinget = $false
}

if ($ValidateChocolatey -and -not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "`nChocolatey not found - skipping Chocolatey validation" -ForegroundColor Yellow
    $ValidateChocolatey = $false
}

if (-not $ValidateWinget -and -not $ValidateChocolatey -and -not $ValidateDirectUrl) {
    Write-Host "`nNo package managers available for validation." -ForegroundColor Red
    exit 1
}

Write-Host "`nRunning source health checks (this may take a while)..." -ForegroundColor Yellow
Write-Host "  Winget:     $(if ($ValidateWinget) { 'Enabled' } else { 'Disabled' })" -ForegroundColor $(if ($ValidateWinget) { 'Green' } else { 'DarkGray' })
Write-Host "  Chocolatey: $(if ($ValidateChocolatey) { 'Enabled' } else { 'Disabled' })" -ForegroundColor $(if ($ValidateChocolatey) { 'Green' } else { 'DarkGray' })
Write-Host "  DirectUrl:  $(if ($ValidateDirectUrl) { 'Enabled' } else { 'Disabled' })" -ForegroundColor $(if ($ValidateDirectUrl) { 'Green' } else { 'DarkGray' })

# Run health checks
$healthResults = Test-SourceHealth -CheckWinget:$ValidateWinget -CheckChocolatey:$ValidateChocolatey -CheckDirectUrl:$ValidateDirectUrl

# Display report
Get-SourceHealthReport -Results $healthResults

# Run repair if requested
if ($Repair) {
    Write-Host "Attempting automatic repair..." -ForegroundColor Yellow
    $repairReport = Repair-AppSources -HealthResults $healthResults

    Write-Host "`n=== Repair Report ===" -ForegroundColor Cyan

    if ($repairReport.ForceEnabled) {
        Write-Host "  Enabled wingetForceOnHashMismatch feature flag" -ForegroundColor Green
    }

    if ($repairReport.DeadUrls.Count -gt 0) {
        Write-Host "  Dead DirectUrls flagged: $($repairReport.DeadUrls -join ', ')" -ForegroundColor Yellow
    }

    if ($repairReport.MissingChocoPackages.Count -gt 0) {
        Write-Host "  Missing Chocolatey packages: $($repairReport.MissingChocoPackages -join ', ')" -ForegroundColor Yellow
    }

    if ($repairReport.VerifiedUpdated.Count -gt 0) {
        Write-Host "  Verified updated: $($repairReport.VerifiedUpdated.Count) applications" -ForegroundColor Green
    }

    Write-Host ""
}

# Summary exit code
$criticalApps = @($healthResults | Where-Object { $_.HealthySourceCount -eq 0 -and $_.TotalSourceCount -gt 0 })
if ($criticalApps.Count -gt 0) {
    Write-Host "CRITICAL: $($criticalApps.Count) application(s) have no healthy sources:" -ForegroundColor Red
    foreach ($app in $criticalApps) {
        Write-Host "  - $($app.AppName)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "All applications have at least one healthy source." -ForegroundColor Green
exit 0
