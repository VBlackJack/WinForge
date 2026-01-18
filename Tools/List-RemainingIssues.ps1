<#
.SYNOPSIS
    List remaining PSScriptAnalyzer issues

.DESCRIPTION
    Runs PSScriptAnalyzer on framework files and lists
    all remaining issues that need to be addressed.

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

Import-Module PSScriptAnalyzer -Force

# Use framework PSScriptAnalyzer settings
$settingsFile = Join-Path $PSScriptRoot '..\PSScriptAnalyzerSettings.psd1'

$files = @(
    'Deploy-Win11Environment.ps1',
    'Modules/InstallationEngine.psm1',
    'Modules/Win11ForgeGUI.psm1',
    'Modules/ApplicationDatabase.psm1',
    'Modules/ProfileManager.psm1',
    'Modules/SystemConfig.psm1',
    'Core/Core.psm1',
    'Modules/Prerequisites.psm1',
    'Modules/EnvironmentDetection.psm1',
    'Modules/StartMenuLayout.psm1',
    'Modules/StartMenuPinning.psm1',
    'Modules/StartupManager.psm1'
)

$allIssues = @()
foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot "..\$file"
    if (Test-Path $fullPath) {
        $issues = Invoke-ScriptAnalyzer -Path $fullPath -Settings $settingsFile -Severity Warning
        $allIssues += $issues
    }
}

# Exclude PSUseShouldProcessForStateChangingFunctions (too complex for now)
$criticalIssues = $allIssues | Where-Object { $_.RuleName -ne 'PSUseShouldProcessForStateChangingFunctions' }

Write-Host "`nCritical Issues to Fix: $($criticalIssues.Count)`n" -ForegroundColor Yellow

foreach ($issue in $criticalIssues) {
    Write-Host "File: " -NoNewline -ForegroundColor Cyan
    Write-Host (Split-Path $issue.ScriptName -Leaf)
    Write-Host "Line $($issue.Line): " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.RuleName -ForegroundColor Red
    Write-Host "  $($issue.Message)" -ForegroundColor Gray
    Write-Host ""
}
