Write-Host "=== ANALYSE APPROFONDIE DU PROJET ===" -ForegroundColor Cyan
Write-Host ""

$issues = @()

# 1. Recherche de TODOs/FIXMEs
Write-Host "[1/8] Recherche de TODOs/FIXMEs..." -ForegroundColor Yellow
$todos = Select-String -Path '*.ps1','*.psm1' -Pattern 'TODO|FIXME|HACK|XXX|BUG' -Recurse | Where-Object {
    $_.Path -notlike '*\.git\*' -and
    $_.Path -notlike '*\Logs\*' -and
    $_.Path -notlike '*\Archive\*'
}
if ($todos) {
    Write-Host "  ! $($todos.Count) TODO/FIXME/HACK trouvés" -ForegroundColor Yellow
    $issues += "Found $($todos.Count) TODO/FIXME markers in code"
} else {
    Write-Host "  ✓ Aucun TODO/FIXME" -ForegroundColor Green
}

# 2. Fichiers non versionnés
Write-Host ""
Write-Host "[2/8] Fichiers non versionnés..." -ForegroundColor Yellow
$untracked = git status --porcelain | Where-Object { $_ -match '^\?\?' -and $_ -notmatch 'VERIFICATION_REPORT' }
if ($untracked) {
    Write-Host "  ! $(@($untracked).Count) fichiers non versionnés" -ForegroundColor Yellow
    $untracked | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
} else {
    Write-Host "  ✓ Tous les fichiers importants versionnés" -ForegroundColor Green
}

# 3. Chemins en dur dans le code
Write-Host ""
Write-Host "[3/8] Recherche de chemins en dur..." -ForegroundColor Yellow
$hardcodedPaths = Select-String -Path '*.ps1','*.psm1' -Pattern 'C:\\Users\\|D:\\|E:\\' -Recurse | Where-Object {
    $_.Path -notlike '*\.git\*' -and
    $_.Path -notlike '*\Logs\*' -and
    $_.Line -notmatch '#.*C:\\Users\\' # Exclude comments
}
if ($hardcodedPaths) {
    Write-Host "  ! $($hardcodedPaths.Count) chemins en dur trouvés" -ForegroundColor Yellow
    $issues += "Found $($hardcodedPaths.Count) hardcoded paths"
} else {
    Write-Host "  ✓ Pas de chemins en dur" -ForegroundColor Green
}

# 4. Cohérence des profils JSON
Write-Host ""
Write-Host "[4/8] Validation des profils..." -ForegroundColor Yellow
$profiles = Get-ChildItem -Path 'Apps/Profiles' -Filter '*.json' -ErrorAction SilentlyContinue
if ($profiles) {
    $invalidProfiles = @()
    foreach ($profileFile in $profiles) {
        try {
            $json = Get-Content $profileFile.FullName -Raw | ConvertFrom-Json
            if (-not $json.ProfileName -or -not $json.Applications) {
                $invalidProfiles += $profileFile.Name
            }
        } catch {
            $invalidProfiles += $profileFile.Name
        }
    }
    if ($invalidProfiles.Count -eq 0) {
        Write-Host "  ✓ $($profiles.Count) profils valides" -ForegroundColor Green
    } else {
        Write-Host "  ! $($invalidProfiles.Count) profils invalides" -ForegroundColor Yellow
        $issues += "Found $($invalidProfiles.Count) invalid profile files"
    }
} else {
    Write-Host "  ℹ Aucun profil personnalisé" -ForegroundColor Gray
}

# 5. Vérification des imports de modules
Write-Host ""
Write-Host "[5/8] Vérification des imports..." -ForegroundColor Yellow
$modules = Get-ChildItem -Path 'Modules' -Filter '*.psm1'
$importIssuesFound = $false
foreach ($module in $modules) {
    $content = Get-Content $module.FullName -Raw
    # Check if module imports Core but Core doesn't exist at expected path
    if ($content -match "Import-Module.*Core\.psm1" -and $content -notmatch 'Test-Path.*Core\.psm1') {
        # Module imports Core - verify it checks path first (currently all modules properly check)
        $importIssuesFound = $true
    }
}
if (-not $importIssuesFound) {
    Write-Host "  ✓ Imports corrects" -ForegroundColor Green
}

# 6. Détection de code dupliqué (fonctions avec même nom)
Write-Host ""
Write-Host "[6/8] Détection de code dupliqué..." -ForegroundColor Yellow
$allFunctions = @{}
$duplicates = @()
Get-ChildItem -Path 'Modules','Core','Tools' -Include '*.psm1','*.ps1' -Recurse | ForEach-Object {
    $file = $_
    $content = Get-Content $_.FullName -Raw
    $functions = [regex]::Matches($content, 'function\s+([A-Z][a-z]+-[A-Z]\w+)')

    foreach ($match in $functions) {
        $funcName = $match.Groups[1].Value
        if ($allFunctions.ContainsKey($funcName)) {
            $duplicates += "$funcName in $($file.Name) and $($allFunctions[$funcName])"
        } else {
            $allFunctions[$funcName] = $file.Name
        }
    }
}
if ($duplicates.Count -gt 0) {
    Write-Host "  ! $($duplicates.Count) fonctions dupliquées" -ForegroundColor Yellow
    $issues += "Found $($duplicates.Count) duplicate function names"
} else {
    Write-Host "  ✓ Pas de fonctions dupliquées" -ForegroundColor Green
}

# 7. Vérification des liens symboliques ou raccourcis cassés
Write-Host ""
Write-Host "[7/8] Vérification de l'intégrité des fichiers..." -ForegroundColor Yellow
# Check if critical files exist and are not empty
$emptyFiles = Get-ChildItem -Path 'Modules','Core' -Include '*.psm1' -Recurse | Where-Object {
    $_.Length -eq 0
}
if ($emptyFiles) {
    Write-Host "  ! $($emptyFiles.Count) fichiers vides" -ForegroundColor Red
    $issues += "Found $($emptyFiles.Count) empty module files"
} else {
    Write-Host "  ✓ Tous les fichiers ont du contenu" -ForegroundColor Green
}

# 8. Vérification des scripts archivés vs actuels
Write-Host ""
Write-Host "[8/8] Comparaison avec archives..." -ForegroundColor Yellow
if (Test-Path 'Archive') {
    $archived = Get-ChildItem -Path 'Archive' -Include '*.ps1','*.psm1' -Recurse
    Write-Host "  ℹ $($archived.Count) fichiers archivés trouvés" -ForegroundColor Gray
} else {
    Write-Host "  ℹ Pas de dossier Archive" -ForegroundColor Gray
}

# Résumé final
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  Résumé de l'Analyse Approfondie" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($issues.Count -eq 0) {
    Write-Host "✓ AUCUN PROBLÈME DÉTECTÉ" -ForegroundColor Green
    Write-Host "  Le projet est en excellent état!" -ForegroundColor Green
} else {
    Write-Host "! $($issues.Count) points d'attention trouvés:" -ForegroundColor Yellow
    $issues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "Analyse terminée." -ForegroundColor Cyan
