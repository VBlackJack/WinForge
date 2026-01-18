<#
.SYNOPSIS
    Find PSScriptAnalyzer errors in Win11Forge

.DESCRIPTION
    Scans all PowerShell files for Error-level issues
    using PSScriptAnalyzer.

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

$files = Get-ChildItem -Path . -Include *.ps1,*.psm1 -Recurse |
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Tests\*' -and $_.FullName -notlike '*\Logs\*' }

$errors = @()
foreach ($file in $files) {
    $issues = Invoke-ScriptAnalyzer -Path $file.FullName -Severity Error
    $errors += $issues
}

Write-Host "`nTotal Errors Found: $($errors.Count)" -ForegroundColor Red
Write-Host ""

foreach ($issue in $errors) {
    Write-Host "File: " -NoNewline -ForegroundColor Yellow
    Write-Host (Split-Path $issue.ScriptName -Leaf)
    Write-Host "Line: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.Line
    Write-Host "Rule: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.RuleName
    Write-Host "Message: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.Message
    Write-Host ""
}
