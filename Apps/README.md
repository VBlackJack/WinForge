# Win11Forge - Application Database

## 📋 Vue d'ensemble

La **base de données centralisée d'applications** est le cœur du système Win11Forge. Elle contient toutes les informations sur les applications disponibles, leurs sources d'installation, et leurs métadonnées.

**Version** : 2.4.0
**Dernière mise à jour** : 2025-10-06
**Total d'applications** : 66

---

## 🎯 Avantages

### ✅ Avant (Profils JSON individuels)
```json
// Base.json
{
  "Applications": [
    { "Name": "Chrome", "Sources": { "Winget": "Google.Chrome" } }
  ]
}

// Office.json
{
  "Applications": [
    { "Name": "Chrome", "Sources": { "Winget": "Google.Chrome" } }  // Duplication!
  ]
}
```

### ✅ Après (Base de données centralisée)
```json
// Database/applications.json (source unique de vérité)
{
  "Applications": {
    "GoogleChrome": {
      "Name": "Google Chrome",
      "Sources": { "Winget": "Google.Chrome" },
      "Tags": ["essential", "browser"],
      "LastVerified": "2025-10-03",
      "Verified": true
    }
  }
}

// Profiles/Base.json (référence seulement)
{
  "Applications": ["GoogleChrome", "Firefox", "VSCode"]
}
```

**Bénéfices** :
- ✅ **Pas de duplication** : Une seule définition par app
- ✅ **Maintenance centralisée** : Modifier une fois, propager partout
- ✅ **Validation automatique** : Vérifier tous les IDs facilement
- ✅ **Métadonnées riches** : Tags, catégories, homepage, etc.
- ✅ **Versioning** : Track des modifications dans le temps

---

## 📂 Structure

```
Apps/
├── Database/
│   └── applications.json        # Base de données principale (66 apps)
├── README.md                    # Cette documentation
└── SCHEMA.md                    # Schéma JSON détaillé
```

---

## 🗂️ Format de la Base de Données

### Structure Globale

```json
{
  "$schema": "https://json-schema.org/draft-07/schema#",
  "DatabaseVersion": "2.4.0",
  "LastUpdated": "2025-10-06",
  "TotalApplications": 66,
  "Applications": { ... },
  "Categories": { ... },
  "Tags": { ... }
}
```

### Application Entry

Chaque application est définie avec :

```json
"GoogleChrome": {
  "Name": "Google Chrome",
  "Category": "Browser",
  "Description": "Fast, secure web browser by Google",

  "Sources": {
    "Winget": "Google.Chrome",
    "Chocolatey": "googlechrome",
    "Store": null,
    "DirectUrl": null
  },

  "Detection": {
    "Method": "Registry",
    "Path": "HKLM:\\SOFTWARE\\Google\\Chrome"
  },

  "DefaultPriority": 1,
  "DefaultRequired": true,
  "EnvironmentRestrictions": [],

  "Tags": ["browser", "popular", "essential"],
  "LastVerified": "2025-10-03",
  "Verified": true,
  "Homepage": "https://www.google.com/chrome/"
}
```

### Champs Disponibles

| Champ | Type | Description |
|-------|------|-------------|
| `Name` | String | Nom affiché de l'application |
| `Category` | String | Catégorie (Browser, Development, etc.) |
| `Description` | String | Description courte |
| `Sources` | Object | IDs pour chaque source d'installation |
| `Sources.Winget` | String | ID Winget |
| `Sources.Chocolatey` | String | Package Chocolatey |
| `Sources.Store` | String | ID Microsoft Store |
| `Sources.DirectUrl` | String | URL de téléchargement direct |
| `Detection` | Object | Méthode de détection d'installation |
| `Detection.Method` | String | Registry, File, Command, StoreApp, WindowsFeature |
| `Detection.Path` | String | Chemin pour Registry/File |
| `Detection.Command` | String | Commande pour Command |
| `Detection.PackageName` | String | Nom du package Store |
| `Detection.Feature` | String | Nom de la feature Windows |
| `DefaultPriority` | Int | Priorité par défaut (1-100) |
| `DefaultRequired` | Boolean | Requis par défaut |
| `EnvironmentRestrictions` | Array | Environnements où ne pas installer |
| `InstallMethod` | String | Méthode d'installation spéciale |
| `InstallArguments` | String | Arguments d'installation personnalisés |
| `Tags` | Array | Tags pour filtrage |
| `LastVerified` | String | Date de dernière vérification (YYYY-MM-DD) |
| `Verified` | Boolean | ID vérifié |
| `Notes` | String | Notes additionnelles |
| `Homepage` | String | Site web officiel |

---

## 📊 Catégories (20 catégories)

| Catégorie | Icône | Apps | Description |
|-----------|-------|------|-------------|
| 3DPrint | 🖨️ | 1 | Impression 3D et slicing |
| Browser | 🌐 | 3 | Navigateurs web |
| Media | 🎬 | 4 | Lecteurs et visionneuses |
| Utility | 🛠️ | 8 | Utilitaires généraux |
| Support | 🆘 | 4 | Outils de support système |
| Diagnostic | 🔍 | 5 | Outils de diagnostic |
| Security | 🔒 | 9 | Sécurité et confidentialité |
| Network | 🌐 | 5 | Outils réseau |
| Recovery | 💾 | 1 | Récupération de données |
| Configuration | ⚙️ | 1 | Configuration système |
| Productivity | 📊 | 2 | Productivité et bureautique |
| Communication | 💬 | 3 | Messagerie et communication |
| Recording | 🎥 | 1 | Enregistrement et streaming |
| Gaming | 🎮 | 3 | Plateformes de jeux |
| Development | 💻 | 6 | Développement logiciel |
| System | ⚡ | 1 | Utilitaires système |
| Virtualization | 📦 | 2 | Virtualisation et sandboxing |
| CloudStorage | ☁️ | 2 | Stockage cloud |
| Compression | 📦 | 1 | Compression de fichiers |
| Multimedia | 🎵 | 4 | Gestion multimédia |

---

## 🏷️ Tags Disponibles

| Tag | Description | Apps |
|-----|-------------|------|
| `essential` | Applications essentielles | ~15 |
| `popular` | Applications très utilisées | ~10 |
| `open-source` | Logiciels open source | ~12 |
| `microsoft` | Applications Microsoft officielles | ~8 |
| `privacy` | Axé sur la confidentialité | ~5 |
| `encrypted` | Support chiffrement | ~6 |
| `subscription` | Nécessite abonnement | ~2 |

---

## 🔧 Module PowerShell : ApplicationDatabase.psm1

### Fonctions Disponibles

#### 1. Get-ApplicationDatabase
Charge la base de données depuis le JSON.

```powershell
$db = Get-ApplicationDatabase
```

#### 2. Get-ApplicationById
Récupère une application par son ID.

```powershell
$chrome = Get-ApplicationById -AppId "GoogleChrome"
Write-Host $chrome.Name
Write-Host $chrome.Sources.Winget
```

#### 3. Get-AllApplications
Récupère toutes les applications (avec filtres optionnels).

```powershell
# Toutes les apps
$all = Get-AllApplications

# Filtre par catégorie
$browsers = Get-AllApplications -Category "Browser"

# Filtre par tag
$essential = Get-AllApplications -Tag "essential"

# Seulement vérifiées
$verified = Get-AllApplications -Verified
```

#### 4. Search-Applications
Recherche par nom.

```powershell
$results = Search-Applications -SearchTerm "chrome"
```

#### 5. ConvertTo-ProfileApplication
Convertit une app database vers format profil.

```powershell
$dbApp = Get-ApplicationById -AppId "GoogleChrome"
$profileApp = ConvertTo-ProfileApplication -App $dbApp -Priority 1 -Required $true
```

#### 6. Get-ApplicationCategories
Liste toutes les catégories.

```powershell
$categories = Get-ApplicationCategories
foreach ($cat in $categories) {
    Write-Host "$($cat.DisplayName): $($cat.Count) apps"
}
```

#### 7. Get-ApplicationTags
Liste tous les tags.

```powershell
$tags = Get-ApplicationTags
```

#### 8. Test-ApplicationSources
Valide toutes les sources (Winget/Choco).

```powershell
$results = Test-ApplicationSources
```

#### 9. Get-DatabaseStatistics
Statistiques de la base de données.

```powershell
$stats = Get-DatabaseStatistics
Write-Host "Total: $($stats.TotalApplications)"
Write-Host "Verified: $($stats.VerifiedApps)"
```

#### 10. Reset-DatabaseCache
Rafraîchit le cache de la base de données.

```powershell
Reset-DatabaseCache
```

---

## 🧪 Script de Validation : Validate-AppDatabase.ps1

### Usage

```powershell
# Validation basique (stats uniquement)
.\Tools\Validate-AppDatabase.ps1

# Validation complète (Winget + Chocolatey)
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey

# Génération de rapport HTML
.\Tools\Validate-AppDatabase.ps1 -GenerateReport

# Tout en un
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

### Résultat Attendu

```
=======================================
  Application Database Validator v1.0
=======================================

Loading database...
✅ Database loaded successfully

Database Statistics:
  Version          : 2.4.0
  Last Updated     : 2025-10-06
  Total Apps       : 66
  Categories       : 20
  Tags             : 7
  Verified Apps    : 65
  Apps with Winget : 58
  Apps with Choco  : 48
  Apps with Store  : 12
  Apps with DirUrl : 8

============================================================
  Source Validation
============================================================

Validating application sources (this may take a while)...

  Testing Google Chrome [Winget: Google.Chrome]... ✅
  Testing Mozilla Firefox [Winget: Mozilla.Firefox]... ✅
  ...

------------------------------------------------------------
Validation Summary:
  Total Apps  : 66
  Valid       : 66 ✅
  Invalid     : 0 ✅
  Success Rate: 100%

✅ Validation Complete!
```

---

## 📝 Workflow de Maintenance

### 1. Ajouter une Nouvelle Application

```powershell
# 1. Éditer Apps/Database/applications.json
{
  "NewApp": {
    "Name": "New Application",
    "Category": "Utility",
    "Sources": {
      "Winget": "Publisher.NewApp",
      "Chocolatey": "newapp"
    },
    "Detection": {
      "Method": "File",
      "Path": "C:\\Program Files\\NewApp\\app.exe"
    },
    "DefaultPriority": 100,
    "DefaultRequired": false,
    "Tags": ["utility"],
    "LastVerified": "2025-10-03",
    "Verified": false
  }
}

# 2. Incrémenter TotalApplications
"TotalApplications": 67

# 3. Valider
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey

# 4. Mettre Verified: true si validation OK
```

### 2. Mettre à Jour une Application

```powershell
# 1. Modifier l'entrée dans applications.json
# 2. Mettre à jour LastVerified
# 3. Revalider
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget
```

### 3. Validation Hebdomadaire Automatique

```powershell
# Créer une tâche planifiée
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-File C:\Win11Forge\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport"

$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 9am

Register-ScheduledTask -Action $action -Trigger $trigger -TaskName "Win11Forge-AppDB-Validation"
```

---

## 🔄 Migration : Profils → Base de Données

### Ancien Format (Profil JSON)

```json
{
  "Applications": [
    {
      "Name": "Google Chrome",
      "Priority": 1,
      "Required": true,
      "Category": "Browser",
      "Sources": {
        "Winget": "Google.Chrome",
        "Chocolatey": "googlechrome"
      },
      "Detection": {
        "Method": "Registry",
        "Path": "HKLM:\\SOFTWARE\\Google\\Chrome"
      }
    }
  ]
}
```

### Nouveau Format (Référence Database)

```json
{
  "Applications": [
    {
      "AppId": "GoogleChrome",
      "Priority": 1,
      "Required": true,
      "Overrides": {}
    }
  ]
}
```

**Ou encore plus simple** :

```json
{
  "Applications": ["GoogleChrome", "Firefox", "VSCode"]
}
```

---

## 📊 Statistiques Actuelles

- **Total Applications** : 66
- **Catégories** : 19
- **Tags** : 7
- **Sources** :
  - Winget : 58 apps (88%)
  - Chocolatey : 48 apps (73%)
  - Store : 12 apps (18%)
  - DirectUrl : 8 apps (12%)
- **Vérifiées** : 65 apps (98.5%)

---

## 🚀 Prochaines Étapes

1. **Automatisation** :
   - CI/CD validation hebdomadaire
   - Auto-update des LastVerified dates
   - Detection automatique de nouveaux packages

2. **Extensions** :
   - API REST pour accès distant
   - Interface web de gestion
   - Import/export communautaire

3. **Enrichissement** :
   - Screenshots des applications
   - Ratings et reviews
   - Usage statistics
   - Dependencies entre apps

---

**Version** : 2.4.0
**Date** : 2025-10-06
**Statut** : ✅ Production Ready
