<#
.SYNOPSIS
    Find duplicate functions across modules

.DESCRIPTION
    Scans all PowerShell modules and scripts to identify
    functions defined in multiple locations.

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

Write-Host "=== IDENTIFICATION DES FONCTIONS DUPLIQUÉES ===" -ForegroundColor Cyan
Write-Host ""

$allFunctions = @{}
$duplicates = @()

Get-ChildItem -Path 'Modules','Core','Tools' -Include '*.psm1','*.ps1' -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    $file = $_
    $content = Get-Content $_.FullName -Raw
    $functions = [regex]::Matches($content, 'function\s+([A-Z][a-z]+-[A-Z]\w+)')

    foreach ($match in $functions) {
        $funcName = $match.Groups[1].Value
        if ($allFunctions.ContainsKey($funcName)) {
            $duplicates += [PSCustomObject]@{
                Function = $funcName
                FirstLocation = $allFunctions[$funcName]
                DuplicateIn = $file.Name
            }
        } else {
            $allFunctions[$funcName] = $file.Name
        }
    }
}

if ($duplicates.Count -gt 0) {
    Write-Host "⚠ Fonctions dupliquées trouvées:" -ForegroundColor Yellow
    Write-Host ""
    $duplicates | Format-Table Function, FirstLocation, DuplicateIn -AutoSize

    Write-Host ""
    Write-Host "Action recommandée:" -ForegroundColor Cyan
    Write-Host "  • Vérifier si les fonctions sont identiques ou différentes"
    Write-Host "  • Si identiques: consolider dans un module partagé"
    Write-Host "  • Si différentes: renommer pour éviter les conflits"
} else {
    Write-Host "✓ Aucune fonction dupliquée trouvée" -ForegroundColor Green
}

Write-Host ""
Write-Host "Total de fonctions uniques: $($allFunctions.Count)" -ForegroundColor Cyan
