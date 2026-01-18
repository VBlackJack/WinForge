<#
.SYNOPSIS
    Fix brace placement style issues

.DESCRIPTION
    Uses PSScriptAnalyzer to identify and fix brace placement
    style issues in PowerShell files.

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
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Logs\*' }

$fixedCount = 0

foreach ($file in $files) {
    $issues = Invoke-ScriptAnalyzer -Path $file.FullName -IncludeRule PSPlaceCloseBrace

    if ($issues.Count -gt 0) {
        Write-Host "Found $($issues.Count) brace issues in $($file.Name)" -ForegroundColor Yellow

        # Use Invoke-Formatter to fix brace placement
        $content = Get-Content $file.FullName -Raw
        $settings = @{
            Rules = @{
                PSPlaceCloseBrace = @{
                    Enable = $true
                    NoEmptyLineBefore = $false
                    IgnoreOneLineBlock = $true
                    NewLineAfter = $true
                }
                PSPlaceOpenBrace = @{
                    Enable = $true
                    OnSameLine = $true
                    NewLineAfter = $true
                    IgnoreOneLineBlock = $true
                }
            }
        }

        try {
            $formatted = Invoke-Formatter -ScriptDefinition $content -Settings $settings
            Set-Content -Path $file.FullName -Value $formatted -NoNewline
            $fixedCount++
            Write-Host "  Fixed $($file.Name)" -ForegroundColor Green
        } catch {
            Write-Host "  Error formatting $($file.Name): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`nTotal files formatted: $fixedCount" -ForegroundColor Cyan
