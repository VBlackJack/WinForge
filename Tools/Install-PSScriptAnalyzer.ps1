<#
.SYNOPSIS
    Install PSScriptAnalyzer for Win11Forge v2.5.0

.DESCRIPTION
    Installs PSScriptAnalyzer required for code quality analysis

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
Write-Host "  PSScriptAnalyzer Installation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check current installation
$pssa = Get-Module -Name PSScriptAnalyzer -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1

if ($pssa) {
    Write-Host "✅ PSScriptAnalyzer v$($pssa.Version) already installed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Ready to analyze code with:" -ForegroundColor White
    Write-Host "  .\Invoke-PSScriptAnalyzer.ps1" -ForegroundColor Cyan
    exit 0
}

Write-Host "Installing PSScriptAnalyzer..." -ForegroundColor Yellow
Write-Host ""

try {
    # Install PSScriptAnalyzer
    Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser

    Write-Host ""
    Write-Host "✅ PSScriptAnalyzer installed successfully!" -ForegroundColor Green

    # Verify installation
    $pssa = Get-Module -Name PSScriptAnalyzer -ListAvailable | Sort-Object Version -Descending | Select-Object -First 1
    Write-Host "Installed version: v$($pssa.Version)" -ForegroundColor Green

    Write-Host ""
    Write-Host "Ready to analyze code with:" -ForegroundColor White
    Write-Host "  .\Invoke-PSScriptAnalyzer.ps1" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "❌ Failed to install PSScriptAnalyzer" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try manual installation:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name PSScriptAnalyzer -Force" -ForegroundColor Cyan
    exit 1
}
