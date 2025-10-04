# Win11Forge - Outils et Utilitaires

Ce dossier contient les scripts utilitaires et outils pour configurer, valider et maintenir le framework Win11Forge.

## 🚀 Outils Principaux

### 📋 ProfileCreator.html ⭐
**Créateur de profils JSON personnalisés**

```bash
# Double-clic sur ProfileCreator.html
# OU ouvrir dans le navigateur (file://)
```

**Fonctionnalités** :
- Interface web standalone (aucun serveur requis)
- Création guidée en 6 étapes
- Héritage de profils existants (Base → Office → Gaming → Personnel)
- Sélection parmi 66 applications prédéfinies
- Ajout d'applications personnalisées avec sources (Winget/Choco/Store/URL)
- Configuration système complète
- Aperçu JSON et téléchargement

**Base de données** : `applications-data.js` (66 applications)

---

### 🔧 Launch-AsTrustedInstaller.bat ⭐
**Lanceur d'outils système avec privilèges TrustedInstaller**

```bash
# Depuis le répertoire Tools :
.\Launch-AsTrustedInstaller.bat
```

**Menu interactif avec 8 options** :
1. PowerShell (TrustedInstaller)
2. Command Prompt (TrustedInstaller)
3. Registry Editor (TrustedInstaller)
4. Task Manager (TrustedInstaller)
5. Computer Management (TrustedInstaller)
6. Windows Explorer (TrustedInstaller)
7. Custom executable path
8. Win11Forge GUI (TrustedInstaller)

**Fonctionnalités** :
- Exécution avec privilèges NT AUTHORITY\SYSTEM
- GUI visible dans la session utilisateur (Session 1)
- Support automatique des fichiers .msc (via mmc.exe)
- Auto-installation du module NtObjectManager si nécessaire

**Script PowerShell associé** : `Launch-TrustedInstallerGUI.ps1`

---

### 🔍 Startup Manager
**Gestionnaire de démarrage automatique**

```bash
.\Launch-StartupManager.ps1
```

**Fonctionnalités** :
- Interface HTML pour gérer les applications au démarrage
- Sélection des apps à désactiver
- Export de la configuration en JSON
- À copier dans `Config/startup-blacklist.json`

**Interface** : `StartupManager.html`

---

## 🛠️ Scripts de Validation

### Validate-AppDatabase.ps1
**Validation de la base de données d'applications**

```powershell
# Validation basique
.\Tools\Validate-AppDatabase.ps1

# Avec validation Winget et Chocolatey
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey

# Génération de rapport HTML
.\Tools\Validate-AppDatabase.ps1 -GenerateReport
```

**Fonctionnalités** :
- Teste les 66 applications de la base de données
- Vérifie les IDs Winget, Chocolatey, Store
- Génère un rapport HTML de validation
- Calcule le taux de succès

**Rapport généré** : `Tools/ValidationReport.html`

---

### Validate-Framework.ps1
**Validation complète du framework**

```powershell
.\Tools\Validate-Framework.ps1

# Mode détaillé
.\Tools\Validate-Framework.ps1 -Detailed
```

**Vérifications** :
- Structure des répertoires
- Présence des fichiers requis
- Chargement des modules PowerShell
- Validation des profils JSON
- Tests de fonctionnalités de base

---

## 🔎 Recherche et Développement

### Search-ApplicationSources.ps1
**Recherche d'applications dans tous les gestionnaires de packages**

```powershell
# Recherche simple
.\Tools\Search-ApplicationSources.ps1 -AppName "Discord"

# Mode interactif avec détails
.\Tools\Search-ApplicationSources.ps1 -AppName "Notepad++" -Interactive
```

**Sources recherchées** :
- Winget (si installé)
- Chocolatey (si installé)
- Microsoft Store
- URLs de téléchargement direct

**Utilité** : Trouver les sources pour ajouter une nouvelle application à la base de données

---

## 📊 Fichiers de Support

| Fichier | Description |
|---------|-------------|
| `applications-data.js` | Base de données JS pour ProfileCreator (66 apps) |
| `ProfileCreator_Features.md` | Documentation des fonctionnalités du ProfileCreator |
| `StartupManager.html` | Interface de gestion du démarrage automatique |
| `Launch-TrustedInstallerGUI.ps1` | Script PowerShell pour TrustedInstaller launcher |

---

## 🎯 Quand Utiliser Ces Outils ?

**ProfileCreator.html** :
- Créer un profil de déploiement personnalisé
- Ajouter des applications custom au framework
- Modifier un profil existant

**Launch-AsTrustedInstaller.bat** :
- Maintenance système profonde
- Modification de registres protégés
- Accès à des fichiers système restreints
- Debug avec privilèges maximaux

**Validate-AppDatabase.ps1** :
- Avant un déploiement complet
- Après modification de la base de données
- Vérification périodique de la disponibilité des packages

**Validate-Framework.ps1** :
- Après installation initiale du framework
- Avant un déploiement important
- Diagnostic de problèmes de structure

**Search-ApplicationSources.ps1** :
- Recherche d'une nouvelle application à ajouter
- Vérification de sources alternatives
- Mise à jour des IDs d'applications

**Startup Manager** :
- Optimisation du démarrage Windows
- Désactivation d'applications au boot
- Création de blacklist personnalisée

---

## ✅ Résultats de Validation (v2.3.0)

- **66 applications** dans la base de données
- **100% de taux de validation** sur les sources principales
- **TrustedInstaller launcher** : Testé et fonctionnel
- **ProfileCreator** : Support complet des 66 apps + custom

---

**Version** : 2.3.0
**Dernière mise à jour** : 2025-10-04
**Auteur** : Julien Bombled
