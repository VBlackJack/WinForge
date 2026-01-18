<#
.SYNOPSIS
    Analyzes priority gaps in the application database

.DESCRIPTION
    Examines the applications database to identify priority assignments
    and potential gaps in the priority numbering.

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

Write-Host "=== ANALYSE DES PRIORITES DANS LA BASE DE DONNEES ===" -ForegroundColor Cyan
Write-Host ""

$db = Get-Content 'Apps/Database/applications.json' | ConvertFrom-Json

# Extraire toutes les priorités
$priorities = @()
$db.Applications.PSObject.Properties | ForEach-Object {
    $app = $_.Value
    if ($app.DefaultPriority) {
        $priorities += [PSCustomObject]@{
            App = $_.Name
            Priority = $app.DefaultPriority
        }
    }
}

# Trier par priorité
$sorted = $priorities | Sort-Object Priority

Write-Host "Plage de priorités: " -NoNewline
Write-Host "$($sorted[0].Priority) - $($sorted[-1].Priority)" -ForegroundColor Yellow

# Identifier les gaps
$allPriorities = $sorted.Priority | Sort-Object -Unique
$gaps = @()

for ($i = $allPriorities[0]; $i -le $allPriorities[-1]; $i++) {
    if ($i -notin $allPriorities) {
        $gaps += $i
    }
}

Write-Host "Total apps avec priorité: $($priorities.Count)" -ForegroundColor Cyan
Write-Host "Priorités uniques utilisées: $($allPriorities.Count)" -ForegroundColor Cyan
Write-Host "Gaps identifiés: $($gaps.Count)" -ForegroundColor $(if ($gaps.Count -gt 0) { 'Yellow' } else { 'Green' })

if ($gaps.Count -gt 0) {
    Write-Host ""
    Write-Host "Gaps de priorités:" -ForegroundColor Yellow

    # Grouper les gaps consécutifs
    $ranges = @()
    $start = $gaps[0]
    $end = $gaps[0]

    for ($i = 1; $i -lt $gaps.Count; $i++) {
        if ($gaps[$i] -eq $end + 1) {
            $end = $gaps[$i]
        } else {
            if ($start -eq $end) {
                $ranges += "  • $start"
            } else {
                $ranges += "  • $start-$end"
            }
            $start = $gaps[$i]
            $end = $gaps[$i]
        }
    }

    # Ajouter le dernier range
    if ($start -eq $end) {
        $ranges += "  • $start"
    } else {
        $ranges += "  • $start-$end"
    }

    $ranges | ForEach-Object { Write-Host $_ -ForegroundColor Gray }

    Write-Host ""
    Write-Host "Recommandation:" -ForegroundColor Cyan
    Write-Host "  Option 1: Compacter les priorités pour éliminer les gaps"
    Write-Host "  Option 2: Documenter les gaps comme réservés pour catégories futures"
}

Write-Host ""
Write-Host "Distribution par tranche:" -ForegroundColor Cyan
Write-Host "  1-10 (Essentiels):   $(($priorities | Where-Object { $_.Priority -ge 1 -and $_.Priority -le 10 }).Count) apps"
Write-Host "  11-20 (Très haute):  $(($priorities | Where-Object { $_.Priority -ge 11 -and $_.Priority -le 20 }).Count) apps"
Write-Host "  21-40 (Haute):       $(($priorities | Where-Object { $_.Priority -ge 21 -and $_.Priority -le 40 }).Count) apps"
Write-Host "  41-60 (Moyenne):     $(($priorities | Where-Object { $_.Priority -ge 41 -and $_.Priority -le 60 }).Count) apps"
Write-Host "  61-80 (Basse):       $(($priorities | Where-Object { $_.Priority -ge 61 -and $_.Priority -le 80 }).Count) apps"
Write-Host "  81-100 (Optionnel):  $(($priorities | Where-Object { $_.Priority -ge 81 -and $_.Priority -le 100 }).Count) apps"
