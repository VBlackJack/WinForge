<#
.SYNOPSIS
    Install Pester v5+ for WinForge tests

.DESCRIPTION
    Installs or updates Pester to v5+ required for WinForge v2.5.0 tests

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
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
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Pester Installation for WinForge v2.5.0" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check current version
$currentPester = Get-Module -Name Pester -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1

if ($currentPester) {
    Write-Host "Current Pester version: v$($currentPester.Version)" -ForegroundColor Yellow

    if ($currentPester.Version.Major -ge 5) {
        Write-Host "[OK] Pester v5+ already installed!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Ready to run tests with:" -ForegroundColor White
        Write-Host "  .\Invoke-Tests.ps1" -ForegroundColor Cyan
        exit 0
    }
} else {
    Write-Host "Pester not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Installing Pester v5+..." -ForegroundColor Yellow
Write-Host ""

try {
    # Install Pester v5+
    Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck -Scope CurrentUser

    Write-Host ""
    Write-Host "[OK] Pester v5+ installed successfully!" -ForegroundColor Green

    # Verify installation
    $newPester = Get-Module -Name Pester -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1
    Write-Host "Installed version: v$($newPester.Version)" -ForegroundColor Green

    Write-Host ""
    Write-Host "Ready to run tests with:" -ForegroundColor White
    Write-Host "  .\Invoke-Tests.ps1" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "[FAIL] Failed to install Pester" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try manual installation:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Cyan
    exit 1
}
