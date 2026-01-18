<#
.SYNOPSIS
    Opens the Win11Forge Telemetry Dashboard

.DESCRIPTION
    Exports current telemetry data and opens the dashboard in the default browser.

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

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$SkipExport
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path $scriptDir -Parent
$dashboardPath = Join-Path $repoRoot 'Assets\Dashboard\index.html'
$telemetryModulePath = Join-Path $repoRoot 'Modules\TelemetryCollector.psm1'

Write-Host "Win11Forge Telemetry Dashboard" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

# Import and export telemetry data
if (-not $SkipExport) {
    if (Test-Path $telemetryModulePath) {
        Write-Host "Exporting telemetry data..." -ForegroundColor Yellow
        Import-Module $telemetryModulePath -Force
        Initialize-TelemetryCollector
        $exportPath = Export-TelemetryReport
        Write-Host "Data exported to: $exportPath" -ForegroundColor Green
    } else {
        Write-Host "Warning: TelemetryCollector module not found" -ForegroundColor Yellow
    }
}

# Open dashboard in browser
if (Test-Path $dashboardPath) {
    Write-Host ""
    Write-Host "Opening dashboard in browser..." -ForegroundColor Yellow
    Start-Process $dashboardPath
    Write-Host "Dashboard opened: $dashboardPath" -ForegroundColor Green
} else {
    Write-Host "Error: Dashboard not found at $dashboardPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Note: The dashboard reads local data only. No information is transmitted." -ForegroundColor Gray
