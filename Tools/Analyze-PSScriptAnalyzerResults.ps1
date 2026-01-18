<#
.SYNOPSIS
    Analyzes PSScriptAnalyzer results for Win11Forge

.DESCRIPTION
    Runs PSScriptAnalyzer on all key framework files and
    aggregates the results for review.

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

$files = @(
    '../Deploy-Win11Environment.ps1',
    '../Modules/InstallationEngine.psm1',
    '../Modules/Win11ForgeGUI.psm1',
    '../Modules/ApplicationDatabase.psm1',
    '../Modules/ProfileManager.psm1',
    '../Modules/SystemConfig.psm1',
    '../Core/Core.psm1',
    '../Modules/Prerequisites.psm1',
    '../Modules/EnvironmentDetection.psm1',
    '../Modules/StartMenuLayout.psm1',
    '../Modules/StartMenuPinning.psm1',
    '../Modules/StartupManager.psm1'
)

$allIssues = @()
foreach ($file in $files) {
    if (Test-Path $file) {
        $issues = Invoke-ScriptAnalyzer -Path $file -Severity Warning,Error
        $allIssues += $issues
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  PSScriptAnalyzer Detailed Report" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "Total Issues Found: $($allIssues.Count)" -ForegroundColor Yellow
Write-Host ""

Write-Host "=== TOP 10 ISSUES BY FREQUENCY ===" -ForegroundColor Cyan
$grouped = $allIssues | Group-Object RuleName | Sort-Object Count -Descending | Select-Object -First 10
$grouped | Format-Table @{L='Count';E={$_.Count}}, @{L='Rule Name';E={$_.Name}} -AutoSize

Write-Host ""
Write-Host "=== SEVERITY BREAKDOWN ===" -ForegroundColor Cyan
$allIssues | Group-Object Severity | Format-Table @{L='Severity';E={$_.Name}}, @{L='Count';E={$_.Count}} -AutoSize

Write-Host ""
Write-Host "=== CRITICAL SECURITY ISSUES ===" -ForegroundColor Red
$securityIssues = $allIssues | Where-Object { $_.RuleName -match 'Invoke|Credential|Password|SecureString|Injection' }
if ($securityIssues.Count -gt 0) {
    $securityIssues | Select-Object @{L='File';E={Split-Path $_.ScriptName -Leaf}}, Line, RuleName, Message | Format-Table -AutoSize -Wrap
} else {
    Write-Host "  No critical security issues found!" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== FILES WITH MOST ISSUES ===" -ForegroundColor Cyan
$allIssues | Group-Object ScriptName | Sort-Object Count -Descending | Select-Object -First 5 | Format-Table @{L='Count';E={$_.Count}}, @{L='File';E={Split-Path $_.Name -Leaf}} -AutoSize

Write-Host ""
Write-Host "=== SAMPLE ISSUES (First 10) ===" -ForegroundColor Yellow
$allIssues | Select-Object -First 10 | Format-Table @{L='File';E={Split-Path $_.ScriptName -Leaf}}, Line, RuleName, @{L='Message';E={$_.Message.Substring(0, [Math]::Min(60, $_.Message.Length))}} -AutoSize

Write-Host ""
Write-Host "Report generated successfully!" -ForegroundColor Green
