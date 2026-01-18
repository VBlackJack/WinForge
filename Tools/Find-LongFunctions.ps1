<#
.SYNOPSIS
    Find functions longer than specified line count

.DESCRIPTION
    Analyzes PowerShell modules to find functions exceeding line limit
    Helps identify refactoring candidates

.PARAMETER MaxLines
    Maximum lines threshold (default: 150)

.EXAMPLE
    .\Find-LongFunctions.ps1

.EXAMPLE
    .\Find-LongFunctions.ps1 -MaxLines 100

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
param(
    [int]$MaxLines = 150
)

Set-StrictMode -Version Latest

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Finding Long Functions (>$MaxLines lines)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$RootPath = Split-Path $PSScriptRoot -Parent

$modules = @(
    'Modules\InstallationEngine.psm1',
    'Modules\ApplicationDatabase.psm1',
    'Modules\ProfileManager.psm1',
    'Modules\Win11ForgeGUI.psm1',
    'Core\Core.psm1',
    'Deploy-Win11Environment.ps1'
)

$longFunctions = @()

foreach ($modulePath in $modules) {
    $fullPath = Join-Path $RootPath $modulePath

    if (-not (Test-Path $fullPath)) {
        Write-Host "⚠️  Skipping: $modulePath (not found)" -ForegroundColor Yellow
        continue
    }

    Write-Host "Analyzing: $modulePath" -ForegroundColor Gray

    $content = Get-Content $fullPath
    $inFunction = $false
    $functionName = ''
    $functionStart = 0
    $braceCount = 0

    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]

        # Detect function start
        if ($line -match '^function\s+([A-Za-z0-9-]+)') {
            $functionName = $matches[1]
            $functionStart = $i + 1
            $inFunction = $true
            $braceCount = 0
        }

        if ($inFunction) {
            # Count braces
            $braceCount += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
            $braceCount -= ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

            # Function end (when braces balance)
            if ($braceCount -eq 0 -and $line -match '}') {
                $functionEnd = $i + 1
                $functionLines = $functionEnd - $functionStart

                if ($functionLines -gt $MaxLines) {
                    $longFunctions += [PSCustomObject]@{
                        Module = Split-Path $modulePath -Leaf
                        Function = $functionName
                        StartLine = $functionStart
                        EndLine = $functionEnd
                        Lines = $functionLines
                        Excess = $functionLines - $MaxLines
                    }
                }

                $inFunction = $false
            }
        }
    }
}

Write-Host ""
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($longFunctions.Count -eq 0) {
    Write-Host "✅ No functions exceed $MaxLines lines!" -ForegroundColor Green
} else {
    Write-Host "Functions exceeding $MaxLines lines:" -ForegroundColor Yellow
    Write-Host ""

    $longFunctions | Sort-Object Lines -Descending | Format-Table Module, Function, Lines, @{
        Label = 'Location'
        Expression = { "$($_.StartLine)-$($_.EndLine)" }
    }, @{
        Label = 'Excess'
        Expression = { "+$($_.Excess)" }
    } -AutoSize

    Write-Host ""
    Write-Host "Total: $($longFunctions.Count) functions to refactor" -ForegroundColor Yellow
    Write-Host ""

    # Priority ranking
    Write-Host "Priority (by size):" -ForegroundColor Cyan
    $priority = 1
    foreach ($func in ($longFunctions | Sort-Object Lines -Descending | Select-Object -First 5)) {
        Write-Host "  $priority. $($func.Module):$($func.Function) ($($func.Lines) lines)" -ForegroundColor White
        $priority++
    }
}

Write-Host ""
