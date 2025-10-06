# Application Database - Quick Start Guide

## 🚀 Démarrage Rapide (5 minutes)

### 1. Charger la Base de Données

```powershell
# Importer le module
Import-Module .\Modules\ApplicationDatabase.psm1 -Force

# Statistiques rapides
Get-DatabaseStatistics
```

**Résultat** :
```
DatabaseVersion    : 2.3.0
TotalApplications  : 66
TotalCategories    : 19
VerifiedApps       : 65
```

---

### 2. Rechercher des Applications

```powershell
# Par nom
Search-Applications -SearchTerm "chrome"

# Par catégorie
Get-AllApplications -Category "Browser"

# Par tag
Get-AllApplications -Tag "essential"

# Toutes les apps vérifiées
Get-AllApplications -Verified
```

---

### 3. Récupérer une Application Spécifique

```powershell
$chrome = Get-ApplicationById -AppId "GoogleChrome"

# Afficher les infos
Write-Host "Name: $($chrome.Name)"
Write-Host "Winget: $($chrome.Sources.Winget)"
Write-Host "Category: $($chrome.Category)"
Write-Host "Tags: $($chrome.Tags -join ', ')"
```

**Résultat** :
```
Name: Google Chrome
Winget: Google.Chrome
Category: Browser
Tags: browser, popular, essential
```

---

### 4. Lister les Catégories

```powershell
Get-ApplicationCategories | Format-Table CategoryId, DisplayName, Count -AutoSize
```

**Résultat** :
```
CategoryId      DisplayName              Count
----------      -----------              -----
Browser         🌐 Navigateurs               3
Development     💻 Développement             7
Gaming          🎮 Gaming                    4
Utility         🛠️ Utilitaires               8
...
```

---

### 5. Valider la Base de Données

```powershell
# Validation complète
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

**Résultat** :
```
✅ Database loaded successfully
Total Apps  : 66
Valid       : 66 ✅
Success Rate: 100%
✅ Report generated: .\Tools\ValidationReport.html
```

---

## 📋 Cas d'Usage Courants

### Créer un Profil Custom

```powershell
# 1. Sélectionner les apps
$myApps = @()
$myApps += Get-ApplicationById "VSCode"
$myApps += Get-ApplicationById "Git"
$myApps += Get-ApplicationById "Python3"
$myApps += Get-AllApplications -Tag "essential"

# 2. Créer le profil
$profile = @{
    Name = "MyDevProfile"
    Applications = $myApps | ForEach-Object { $_.AppId }
}

# 3. Sauvegarder
$profile | ConvertTo-Json | Out-File "Profiles/MyDevProfile.json"
```

---

### Trouver Toutes les Apps Open Source

```powershell
$openSource = Get-AllApplications -Tag "open-source"

Write-Host "Found $($openSource.Count) open-source applications:"
$openSource | Select-Object Name, Category, Homepage | Format-Table -AutoSize
```

---

### Vérifier si une App Existe

```powershell
$app = Get-ApplicationById -AppId "GoogleChrome"

if ($null -ne $app) {
    Write-Host "✅ $($app.Name) is available"
    Write-Host "   Winget: $($app.Sources.Winget)"
} else {
    Write-Host "❌ Application not found"
}
```

---

### Lister les Apps par Catégorie

```powershell
$categories = Get-ApplicationCategories

foreach ($cat in $categories) {
    Write-Host "`n$($cat.DisplayName) ($($cat.Count) apps)" -ForegroundColor Cyan

    $apps = Get-AllApplications -Category $cat.CategoryId
    foreach ($app in $apps) {
        $sources = @()
        if ($app.Sources.Winget) { $sources += "Winget" }
        if ($app.Sources.Chocolatey) { $sources += "Choco" }

        Write-Host "  - $($app.Name) [$($sources -join ', ')]"
    }
}
```

---

## 🔧 Fonctions Essentielles

| Fonction | Usage |
|----------|-------|
| `Get-ApplicationDatabase` | Charger la DB |
| `Get-ApplicationById` | Récupérer 1 app par ID |
| `Get-AllApplications` | Lister toutes (+ filtres) |
| `Search-Applications` | Rechercher par nom |
| `Get-ApplicationCategories` | Lister catégories |
| `Get-ApplicationTags` | Lister tags |
| `Get-DatabaseStatistics` | Stats globales |

---

## 📊 Exemples de Filtres

```powershell
# Apps essentielles
Get-AllApplications -Tag "essential"

# Apps Microsoft
Get-AllApplications -Tag "microsoft"

# Apps de développement
Get-AllApplications -Category "Development"

# Apps de sécurité vérifiées
Get-AllApplications -Category "Security" -Verified

# Apps populaires open-source
Get-AllApplications -Tag "popular" | Where-Object { $_.Tags -contains "open-source" }
```

---

## 🎯 Commandes Utiles

### Compter les Apps par Source

```powershell
$all = Get-AllApplications

$withWinget = ($all | Where-Object { $_.Sources.Winget }).Count
$withChoco = ($all | Where-Object { $_.Sources.Chocolatey }).Count
$withStore = ($all | Where-Object { $_.Sources.Store }).Count
$withUrl = ($all | Where-Object { $_.Sources.DirectUrl }).Count

Write-Host "Winget     : $withWinget apps"
Write-Host "Chocolatey : $withChoco apps"
Write-Host "Store      : $withStore apps"
Write-Host "DirectUrl  : $withUrl apps"
```

### Trouver les Apps Sans Winget

```powershell
$noWinget = Get-AllApplications | Where-Object { -not $_.Sources.Winget }

Write-Host "Apps without Winget: $($noWinget.Count)"
$noWinget | Select-Object Name, @{N='HasChoco';E={$null -ne $_.Sources.Chocolatey}}, @{N='HasUrl';E={$null -ne $_.Sources.DirectUrl}} | Format-Table
```

### Export vers CSV

```powershell
$apps = Get-AllApplications

$apps | Select-Object Name, Category, @{N='Winget';E={$_.Sources.Winget}}, @{N='Chocolatey';E={$_.Sources.Chocolatey}}, Verified | Export-Csv -Path "AppDatabase.csv" -NoTypeInformation
```

---

## 🆘 Dépannage

### Erreur: "Database not found"

```powershell
# Vérifier le chemin
$dbPath = Join-Path $PSScriptRoot "..\Apps\Database\applications.json"
Test-Path $dbPath
```

### Rafraîchir le Cache

```powershell
Reset-DatabaseCache
$db = Get-ApplicationDatabase
```

### Valider l'Intégrité JSON

```powershell
try {
    $json = Get-Content "Apps\Database\applications.json" -Raw | ConvertFrom-Json
    Write-Host "✅ JSON valid"
} catch {
    Write-Host "❌ JSON error: $_"
}
```

---

## 📚 Plus d'Informations

- **Documentation complète** : [Apps/README.md](README.md)
- **Rapport d'implémentation** : [RELEASE_NOTES_v2.4.0.md](../RELEASE_NOTES_v2.4.0.md)
- **Script de validation** : `.\Tools\Validate-AppDatabase.ps1`

---

**Quick Start v1.0**
**Date** : 2025-10-06
