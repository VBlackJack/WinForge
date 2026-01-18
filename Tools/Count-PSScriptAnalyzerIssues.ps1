<#
.SYNOPSIS
    Counts PSScriptAnalyzer issues in Win11Forge

.DESCRIPTION
    Runs PSScriptAnalyzer on framework files using the project settings
    and counts the number of issues found.

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
        $issues = Invoke-ScriptAnalyzer -Path $fullPath -Settings $settingsFile -Severity Warning,Error
        $allIssues += $issues
    }
}

Write-Host "`nTotal Issues: $($allIssues.Count)" -ForegroundColor Yellow
Write-Host "`nSeverity Breakdown:" -ForegroundColor Cyan
$allIssues | Group-Object Severity | Format-Table @{L='Severity';E={$_.Name}}, @{L='Count';E={$_.Count}} -AutoSize

Write-Host "Top 10 Issues:" -ForegroundColor Cyan
$allIssues | Group-Object RuleName | Sort-Object Count -Descending | Select-Object -First 10 | Format-Table @{L='Count';E={$_.Count}}, @{L='Rule';E={$_.Name}} -AutoSize
