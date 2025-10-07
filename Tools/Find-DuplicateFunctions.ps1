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
