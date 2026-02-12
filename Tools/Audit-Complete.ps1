<#
.SYNOPSIS
    Complete audit script for Win11Forge

.DESCRIPTION
    Performs a complete audit including test coverage, PSScriptAnalyzer analysis,
    and other quality checks.

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

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = Join-Path $repoRoot 'Tests'

Write-Host "=== AUDIT COMPLET ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Racine analysee: $repoRoot" -ForegroundColor DarkGray

# 1. Tests coverage
Write-Host ""
Write-Host "1. COUVERTURE DES TESTS" -ForegroundColor Yellow
$moduleNames = @(
    'Core',
    'InstallationEngine',
    'ApplicationDatabase',
    'ProfileManager',
    'EnvironmentDetection',
    'Prerequisites',
    'SystemConfig',
    'StartMenuLayout',
    'StartMenuPinning',
    'StartupManager',
    'Win11ForgeGUI'
)

foreach ($mod in $moduleNames) {
    $testFile = Join-Path $testsRoot "$mod.Tests.ps1"
    $hasTest = Test-Path -Path $testFile -PathType Leaf
    $status = if ($hasTest) { "[OK]" } else { "[MISSING]" }
    $color = if ($hasTest) { 'Green' } else { 'Red' }
    Write-Host "  $status $mod" -ForegroundColor $color
}

# 2. PSScriptAnalyzer
Write-Host ""
Write-Host "2. PSSCRIPTANALYZER (Warning+Error)" -ForegroundColor Yellow
$issues = @(
    Invoke-ScriptAnalyzer -Path $repoRoot -Recurse -Severity Warning,Error 2>$null |
        Where-Object { $_.ScriptPath -notmatch '\\Tests\\' }
)
Write-Host "  Total issues: $($issues.Count)"
if ($issues.Count -gt 0) {
    $issues | Group-Object RuleName | Sort-Object Count -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Host "    - $($_.Name): $($_.Count)" -ForegroundColor Gray
    }
}

# 3. TODO/FIXME
Write-Host ""
Write-Host "3. TODO/FIXME DANS LE CODE" -ForegroundColor Yellow
$todos = @(
    Get-ChildItem -Path $repoRoot -Include '*.ps1', '*.psm1' -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\Tests\\' } |
        Select-String -Pattern 'TODO|FIXME'
)
Write-Host "  Found: $($todos.Count)"
foreach ($todo in $todos) {
    Write-Host "    $($todo.Filename):$($todo.LineNumber)" -ForegroundColor Gray -NoNewline
    $line = $todo.Line.Trim()
    if ($line.Length -gt 60) { $line = $line.Substring(0, 60) + "..." }
    Write-Host " $line" -ForegroundColor DarkGray
}

# 4. Check for CI/CD
Write-Host ""
Write-Host "4. CI/CD" -ForegroundColor Yellow
$hasGitHubActions = Test-Path -Path (Join-Path $repoRoot '.github\workflows')
$hasAzurePipelines = Test-Path -Path (Join-Path $repoRoot 'azure-pipelines.yml')
if ($hasGitHubActions) {
    Write-Host "  [OK] GitHub Actions found" -ForegroundColor Green
} elseif ($hasAzurePipelines) {
    Write-Host "  [OK] Azure Pipelines found" -ForegroundColor Green
} else {
    Write-Host "  [MISSING] No CI/CD pipeline" -ForegroundColor Red
}

# 5. Git status
Write-Host ""
Write-Host "5. GIT STATUS" -ForegroundColor Yellow
$gitStatusOutput = git -C $repoRoot status --short 2>&1
if ($LASTEXITCODE -eq 0) {
    $gitStatusOutput
} else {
    Write-Host "  [WARN] Unable to read git status in current context" -ForegroundColor Yellow
    $gitStatusOutput | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
}

# 6. Summary
Write-Host ""
Write-Host "=== RESUME ===" -ForegroundColor Cyan
$testsMissing = @($moduleNames | Where-Object { -not (Test-Path (Join-Path $testsRoot "$_.Tests.ps1")) }).Count
Write-Host "  Tests manquants: $testsMissing" -ForegroundColor $(if ($testsMissing -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Issues PSScriptAnalyzer: $($issues.Count)" -ForegroundColor $(if ($issues.Count -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  TODO/FIXME: $($todos.Count)" -ForegroundColor $(if ($todos.Count -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  CI/CD: $(if ($hasGitHubActions -or $hasAzurePipelines) { 'Present' } else { 'Manquant' })" -ForegroundColor $(if ($hasGitHubActions -or $hasAzurePipelines) { 'Green' } else { 'Red' })
