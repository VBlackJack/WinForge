<#
.SYNOPSIS
    Fix trailing whitespace in PowerShell files

.DESCRIPTION
    Scans all PowerShell files and removes trailing whitespace
    from each line.

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

$files = Get-ChildItem -Path . -Include *.ps1,*.psm1 -Recurse |
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Logs\*' }

$fixedCount = 0
$totalLines = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Remove trailing whitespace from each line
    $lines = $content -split "`r?`n"
    $fixedLines = $lines | ForEach-Object { $_.TrimEnd() }
    $newContent = $fixedLines -join "`r`n"

    # Add final newline if missing
    if (-not $newContent.EndsWith("`r`n")) {
        $newContent += "`r`n"
    }

    if ($newContent -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        $fixedCount++
        $linesFixed = ($lines | Where-Object { $_ -match '\s+$' }).Count
        $totalLines += $linesFixed
        Write-Host "Fixed $($file.Name): $linesFixed lines" -ForegroundColor Green
    }
}

Write-Host "`nTotal files fixed: $fixedCount" -ForegroundColor Cyan
Write-Host "Total lines fixed: $totalLines" -ForegroundColor Cyan
