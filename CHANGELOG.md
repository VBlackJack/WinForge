# Win11Forge Framework - Changelog

Note: la source de vérité de la version du framework est `Config/version.json`. Les lanceurs et la GUI lisent dynamiquement cette valeur.

## [3.1.4] - 2026-01-16

### Critical Security Fixes

#### Command Injection Prevention
- **Fixed**: CRITICAL - Replaced `cmd /c` string interpolation with `Start-Process` argument arrays in `Invoke-Rollback`
- **Fixed**: Winget/Chocolatey uninstall now use safe argument passing

#### State File Security
- **Added**: `Test-ValidStateData` function validates deployment state files before loading
- **Added**: SessionId GUID format validation
- **Added**: ProfileName path traversal and character validation
- **Added**: App name shell metacharacter detection

#### Parallel Detection Security
- **Fixed**: HIGH - Added path traversal protection to parallel `Test-AppInstalledParallel`
- **Added**: Registry and File detection now validate paths against `..` sequences

#### Command Detection Hardening
- **Added**: Executable whitelist for Command detection method (java, dotnet, python, node, git, etc.)
- **Blocked**: Arbitrary executables can no longer be run via applications.json Detection.Command

#### C# PowerShellBridge Security
- **Added**: `ValidateAppId` method prevents injection via malicious app IDs
- **Added**: AppId character validation (alphanumeric, dots, hyphens, underscores only)

#### Configuration Consistency
- **Fixed**: Parallel install timeout now uses configurable `$script:ParallelInstallTimeoutMs`
- **Standardized**: All timeout values defined in module configuration section

---

## [3.1.3] - 2026-01-16

### Security Hardening Update

#### Path Traversal Protection
- **Added**: Expand-DetectionPath now validates paths against traversal attacks (`..`)
- **Added**: Blocks relative paths and validates absolute path requirements
- **Added**: Double-check after environment variable expansion

#### URL Validation Improvements
- **Changed**: Test-ValidDownloadUrl now blocks non-whitelisted domains by default
- **Added**: Trusted domains loaded from `Config/download-sources.json`
- **Added**: `-AllowUntrusted` parameter for explicit override when needed
- **Added**: Fallback whitelist for common CDNs when config unavailable

#### Temp Directory Security
- **Changed**: Full 32-character GUID for temp directories (was 8 characters)
- **Improved**: Reduces collision risk from 1/4B to 1/340 undecillion

#### Code Quality
- **Reviewed**: SilentlyContinue usage - confirmed legitimate for existence checks

---

## [3.1.2] - 2026-01-16

### Installation & Detection Improvements

#### Real-time Installation Streaming
- **Added**: Live installation logs in GUI with real-time output streaming
- **Added**: `Write-Output` statements throughout InstallationEngine for status updates

#### Download Improvements
- **Added**: curl.exe as fallback download method (built into Windows 10/11)
- **Added**: Browser-like User-Agent headers to avoid download blocks
- **Fixed**: Battle.net installation - DirectUrl only with curl fallback

#### Detection Fixes
- **Fixed**: AnyDesk detection (portable via Chocolatey bin)
- **Fixed**: CutePDF Writer registry key (`CutePDF Writer Installation`)
- **Fixed**: Eclipse IDE path (`Eclipse*\eclipse\eclipse.exe`)
- **Renamed**: Creality Slicer → Creality Print with correct detection path

#### Settings & UI
- **Added**: Parallel installs configurable up to 10 (was 5)
- **Added**: Parallel scans configurable up to 20
- **Added**: Context menu scan options (Scan/Scan Selected/Scan All)
- **Fixed**: Dark mode toggle button visibility

#### Database
- **Updated**: 70+ applications (vs 66 in v3.0.0)
- **Updated**: Multiple detection paths corrected

---

## [3.0.0] - 2026-01-05

### Major Release - Modern WPF GUI

Win11Forge v3.0.0 introduces a complete graphical interface while maintaining full CLI compatibility.

### Recent Fixes (2026-01-05)

#### Installation Engine Reliability
- **Fixed**: SHA256 property access in StrictMode for DirectUrl installations (Battle.net)
- **Fixed**: Chocolatey "already installed" detection - no longer triggers unnecessary retries
- **Fixed**: Winget "No available upgrade" detection - treats as success
- **Fixed**: Store "already installed" detection - same improvement
- **Fixed**: WebClient Timeout property error - removed invalid property assignment

#### Localization
- **Fixed**: Hardcoded "Win11Forge" text in Settings view - now uses localized App_Name key

### New Features

#### WPF GUI Application
- **Dashboard** - System info, stats cards, recent deployments history
- **Prerequisites** - Visual prerequisite checker with one-click installation
- **Deployment** - Profile selection, parallel installation with progress tracking
- **Applications Manager** - Search, filter, scan installed apps, batch installation
- **Profile Editor** - Create/edit profiles with inheritance support
- **Settings** - Dark/Light theme, English/French language

#### Technical Highlights
- .NET 8.0 with MaterialDesignThemes
- MVVM architecture with CommunityToolkit.Mvvm
- PowerShell Bridge for CLI integration
- Self-contained deployment (no .NET install required)
- i18n support (EN/FR)

### Improvements

#### Application Detection
- **Winget fallback detection** - If Registry/File detection fails, uses `winget list --id` as fallback
- **Office installation wait** - Polls for Office executables after Click-to-Run async install
- **Increased timeouts** - Default 30min, Office-specific 45min for slow VMs

#### GUI Fixes
- Fixed PowerShell script execution deadlock (concurrent stdout/stderr reading)
- Fixed system info retrieval using native .NET instead of PowerShell SDK
- Fixed Sources column not displaying (JSON parsing correction)
- Fixed Scan button staying greyed out after loading apps

#### Database Updates
- Fixed winget IDs: ProtonVPN, RoboForm, Mp3tag, WinAero Tweaker
- Updated Signal/ProtonVPN detection paths

### Dependencies
| Package | Version |
|---------|---------|
| .NET | 8.0 |
| MaterialDesignThemes | 5.1.0 |
| CommunityToolkit.Mvvm | 8.3.2 |

### Breaking Changes
- None - Full backward compatibility with v2.x profiles and CLI

---

## [2.4.0] - 2025-10-06

### 🎉 Compatibility & Performance Release

Cette version apporte des améliorations majeures de compatibilité PowerShell 5.1, des optimisations de performance pour System-Audit, et des corrections critiques pour la stabilité du mode séquentiel.

### ✨ Nouvelles Fonctionnalités

#### System-Audit v2.4.0 - Performance Optimized
- **Ajouté**: `Tools/System-Audit.ps1` v2.4.0
  - Overhead réduit de 67% : 3000ms → ~750ms par échantillon (60% → 20%)
  - Intervalle d'échantillonnage ajusté de 2s à 5s par défaut
  - Fréquences de scan optimisées : Apps (30s), Events (60s), Network (120s)
  - Nouveau paramètre `-SkipApplicationMonitoring` (réduit overhead de 40%)
  - Affichage temps réel des performances (avg/max par échantillon)

#### TrustedInstaller Launcher Improvements
- **Ajouté**: `Tools/Launch-AsTrustedInstaller.bat`
  - Menu interactif avec 8 options (PowerShell, CMD, Registry, Task Manager, etc.)
  - Exécution avec privilèges NT AUTHORITY\SYSTEM
  - Support automatique des fichiers .msc via mmc.exe
  - Auto-installation du module NtObjectManager si nécessaire

### 🔧 Corrections Majeures

#### PowerShell 5.1 Sequential Mode Compatibility
- **Corrigé**: `Modules/InstallationEngine.psm1` - StrictMode PropertyNotFoundException
  - Remplacement des conditions chainées par des conditions imbriquées
  - Accès sécurisé aux propriétés PSObject : `$app.PSObject.Properties['PropertyName']`
  - Compatible avec StrictMode en PowerShell 5.1 et 7.x
  - Fixes appliqués aux propriétés : InstallationOptions, IgnoreExitCodeIfFileExists, ValidExitCodes

#### PowerShell 7 Auto-Restart Enhancement
- **Ajouté**: `Deploy-Win11Environment.ps1` - Auto-restart en PowerShell 7
  - Détection automatique de PowerShell 5.1
  - Redémarrage automatique avec préservation des paramètres
  - Support modes Parallel et Sequential
  - Message informatif avant redémarrage

#### System-Audit Bug Fixes (v2.2.0)
- **Corrigé**: `Tools/System-Audit.ps1` - Bugs critiques
  - Processus terminés comptés avant calcul overhead (timing fix)
  - Protection division par zéro dans génération rapport HTML
  - Gestionnaire Ctrl+C gracieux avec génération automatique du rapport
  - Mode `-Quiet` pour exécution silencieuse (scripts automatisés)
  - Session CIM réutilisable pour +20% de performance
  - Optimisation HashSet pour comparaisons O(1) au lieu de O(n²)

#### TrustedInstaller Launcher Fixes
- **Corrigé**: `Tools/Launch-AsTrustedInstaller.bat` - Gestion des chemins avec espaces
  - Correction du quoting pour paths avec espaces
  - Suppression du code mort (delayed expansion inutilisée)
  - Robustesse assignment ARGS avec quoted set statement
  - Validation correcte des paramètres personnalisés

#### GUI Stability Improvements
- **Corrigé**: `Modules/Win11ForgeGUI.psm1` - Module caching PropertyNotFoundException
  - Détection call operator vs direct execution
  - Correction crash au lancement avec paths contenant espaces
  - Validation AppId override et propagation exit codes

#### StrictMode and Parallel Mode Fixes
- **Corrigé**: `Deploy-Win11Environment.ps1` - Crash statistiques mode parallèle
  - Null-safe environment report avec fallbacks
  - Propriété Skipped correctement vérifiée dans stats
  - Apps skippées comptées correctement (pas comme Failed)
  - Affichage summary correct pour apps skippées (jaune au lieu de rouge)

#### Detection and Registry Fixes
- **Corrigé**: `Modules/ApplicationDatabase.psm1` - Validation et type coercion
  - Support valeurs numériques booléennes (0/1) pour champ Required
  - Validation type pour priority/required overrides
  - Prévention coercion type cassant default priority/required
  - Corrections critiques registry writes et handling priority 0

#### DirectDownload and ZIP Deployment
- **Corrigé**: `Modules/InstallationEngine.psm1` - Support multi-format
  - DirectDownload fonctionnel en mode parallèle pour PS7
  - Déploiement ZIP archive correct pour outils portables
  - Mode séquentiel ZIP deployment avec Detection.Path
  - Compatibilité PowerShell 5.1 pour DirectDownload
  - Suppression `-UseBasicParsing` en mode séquentiel

#### Setup and Validation Improvements
- **Corrigé**: `Setup-Framework.ps1` - Création répertoires et validation
  - Création correcte du répertoire Tools
  - Correction références documentation dans messages d'erreur
  - Cohérence version avec framework principal

### 🛠️ Améliorations

#### Documentation Consistency
- **Corrigé**: 50+ fichiers pour cohérence de version
  - Harmonisation toutes bannières console à v2.4.0
  - Correction counts applications dans Apps/README.md
  - Synchronisation statistiques CHANGELOG et PROJET_STRUCTURE
  - Correction documentation GUI tags et sources
  - Cohérence dates dernière mise à jour (2025-10-06)

#### EnvironmentDetection Module Path
- **Corrigé**: `Modules/InstallationEngine.psm1` - Utilisation RepositoryRoot
  - Remplacement calcul path relatif par variable $script:RepositoryRoot
  - Path module fiable en mode séquentiel
  - Plus maintenable avec variable centralisée

#### Module Encoding and Formatting
- **Corrigé**: Tous modules - Encodage UTF-8 BOM
  - UTF-8 BOM appliqué à tous modules et scripts
  - Formatage linter appliqué uniformément
  - Amélioration démarrage GUI

### 📊 Statistiques v2.4.0

- **100+ commits** depuis v2.3.0
- **50+ fichiers** corrigés pour cohérence
- **15+ bugs critiques** résolus (StrictMode, parallel, GUI)
- **4 versions System-Audit** (2.1.0 → 2.2.0 → 2.3.0 → 2.4.0)
- **67% réduction overhead** System-Audit (3000ms → 750ms)
- **100% compatibilité** PowerShell 5.1 + 7.x en modes séquentiel/parallèle

### 🔗 Liens Utiles

- **Documentation complète** : `README.md`
- **System-Audit docs** : `Tools/System-Audit-README.md` (30+ pages)
- **Structure projet** : `PROJET_STRUCTURE.md`
- **Quick Start** : `Apps/QUICK_START.md`

---

## [2.3.0] - 2025-10-04

### 🎉 Stability & Detection Improvements Release

Cette version corrige des problèmes majeurs de détection d'applications Store, améliore la stabilité PowerShell 7, et introduit le logging parallèle avec l'épinglage Start Menu fiable.

### ✨ Nouvelles Fonctionnalités

#### Start Menu Pinning (start2.bin)
- **Ajouté**: `Modules/StartMenuPinning.psm1`
  - Méthode fiable pour Windows 11 22H2+
  - Épinglage d'items au Start Menu via start2.bin
  - Support Default profile + utilisateur courant
  - Remplace LayoutModification.json (déprécié)
  - Intégration avec StartMenuLayout.psm1

#### Start Menu Layout Organisation
- **Ajouté**: `Modules/StartMenuLayout.psm1`
  - Organisation automatique par catégorie
  - Création de dossiers dans le Start Menu
  - Mapping applications → catégories
  - Compatible avec StartMenuPinning

#### Startup Manager
- **Ajouté**: `Modules/StartupManager.psm1`
  - Gestion applications au démarrage
  - Activation/désactivation au démarrage
  - Compatible mode parallèle et séquentiel

#### Logs Parallèles Individuels
- **Ajouté**: Logs séparés par application en mode parallèle
  - Nouveau dossier `Logs/Parallel/`
  - Logs individuels par application (ex: `Logs/Parallel/GoogleChrome_20251004_203045.log`)
  - Tracking temps réel de chaque installation
  - Stack traces détaillées avec numéros de ligne
  - Facilite le debugging des crashs et erreurs
  - Chaque runspace écrit dans son propre fichier

### 🔧 Corrections Majeures

#### Détection Store Apps Améliorée
- **Corrigé**: `Modules/InstallationEngine.psm1` - Support complet méthode `StoreApp`
  - **PackageName Detection**: Support complet pour applications Store
  - **Détection multilingue**: Quick Assist FR/EN, autres apps localisées
  - **Vendor Prefix Extraction**: Regex `^([^.]+)\.` pour extraire préfixe du PackageName
  - **Fallback nom de base**: Si PackageName complet non trouvé, essaie nom de base
  - **Méthode winget list**: Évite conflits module Appx en PowerShell 7
  - **Compatible PS7 parallèle**: Fonctionne en mode parallèle et séquentiel

#### WhatsApp Desktop
- **Corrigé**: `Apps/Database/applications.json`
  - Méthode: `StoreApp` avec `PackageName: "WhatsAppDesktop"`
  - Détection par nom de base (suffixe vendor tronqué pour compatibilité)
  - Fallback intelligent vers nom sans suffixe dans code de détection
  - Sources: Store prioritaire, sinon Winget/Chocolatey

#### Quick Assist
- **Corrigé**: `Apps/Database/applications.json`
  - Méthode: `StoreApp` avec `PackageName: "MicrosoftCorporationII.QuickAssist"`
  - Détection par préfixe vendor (suffixe hash tronqué pour compatibilité)
  - Support multilingue (FR: Assistance Rapide, EN: Quick Assist)
  - Résolution regex avancée pour noms tronqués dans code de détection

#### Epic Games Launcher
- **Corrigé**: `Apps/Database/applications.json`
  - Chemin File corrigé: `C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe`
  - Ancien chemin incorrect: `.../Win64/...` (n'existe pas)
  - Validation: Chemin vérifié sur installation réelle

#### Proton Apps (Drive, Mail Bridge, Pass)
- **Corrigé**: `Apps/Database/applications.json`
  - **Detection supprimée** pour les 3 apps Proton
  - Utilisation fallback `Test-ApplicationByName` via winget list
  - Chemins File incorrects supprimés (n'existaient pas)
  - Détection fiable par nom winget:
    - `Proton.ProtonDrive`
    - `Proton.ProtonMailBridge`
    - `Proton.ProtonPass`

#### CUE Splitter
- **Corrigé**: `Apps/Database/applications.json`
  - **App corrigée**: De CUETools vers CUE Splitter (app Store correcte)
  - AppId: `CUESplitter`
  - Source Store uniquement: `9NBLGGH43MH5`
  - Détection: `StoreApp` avec `PackageName: "CUESplitter"` (nom de base)

#### InstallArguments Access
- **Corrigé**: `Modules/InstallationEngine.psm1`
  - Accès sécurisé aux propriétés PSObject en StrictMode
  - Utilisation de `$app.PSObject.Properties['InstallArguments']` au lieu de `$app.InstallArguments`
  - Évite erreurs "property does not exist" en mode strict
  - Compatible avec toutes les versions PowerShell

### 🛠️ Améliorations

#### Stabilité PowerShell 7
- **Corrigé**: Conflits assembly Appx en mode séquentiel
  - Détection StoreApp via `winget list` au lieu de `Get-AppxPackage`
  - Évite conflit "Could not load file or assembly 'System.Runtime.WindowsRuntime'"
  - Support complet PS7 parallèle sans crashes
  - Utilisation systématique de winget pour cohérence

#### Test-ApplicationByName Fallback
- **Amélioré**: Fallback automatique pour apps sans Detection
  - Détection par `winget list --name "AppName"`
  - Alternative fiable quand chemins File incorrects
  - Exemple: Proton apps utilisent ce fallback avec succès
  - Mode par défaut pour nouvelles apps

#### Logging en Mode Parallèle
- **Amélioré**: Architecture de logging parallèle
  - Chaque runspace a son propre fichier log
  - Horodatage précis pour chaque opération
  - Stack traces complètes avec numéros de ligne
  - Résumé consolidé dans log principal
  - Facilite identification problèmes spécifiques par app

### 🧪 Tests et Validation

#### Test-ProtonAppsDetection.ps1
- **Ajouté**: `Tests/Test-ProtonAppsDetection.ps1`
  - Script de validation des 3 apps Proton
  - Vérifie chemins File (n'existent pas)
  - Vérifie détection winget (fonctionne)
  - Recherche emplacements réels si paths incorrects

#### Validation Déploiement Séquentiel
- **Testé**: Mode séquentiel PowerShell 7
  - Profil Personnel (66 apps)
  - Résultat: 64 apps traitées, 16 installées, 41 déjà présentes, 4 skipped, 3 échecs (Proton - maintenant corrigé)
  - Quick Assist: ✅ Détecté correctement
  - WhatsApp Desktop: ✅ Détecté correctement
  - Epic Games Launcher: ✅ Détecté correctement

#### Validation Déploiement Parallèle
- **Testé**: Mode parallèle PowerShell 7
  - 5 jobs concurrents
  - Logs individuels fonctionnels
  - Stabilité confirmée sans crashes Appx
  - Performance optimale maintenue

### 📊 Statistiques v2.3.0

**Applications** : 66 (stable vs v2.2.0)
**Profils** : 4 (Base, Office, Gaming, Personnel)
**Modules** : 10 (+3 vs v2.2.0: StartMenuLayout, StartMenuPinning, StartupManager)
**Tests** : +1 (Test-ProtonAppsDetection.ps1)

**Apps Corrigées** : 7
- WhatsApp Desktop (StoreApp)
- Quick Assist (StoreApp multilingue)
- Epic Games Launcher (File path)
- Proton Drive (Detection removed)
- Proton Mail Bridge (Detection removed)
- Proton Pass (Detection removed)
- CUE Splitter (App corrigée)

**Taux de Succès d'Installation** :
- v2.2.0: ~95% (3 échecs Proton)
- v2.3.0: ~99% (0-1 échec attendu)

### 🔧 Fichiers Modifiés

**Modules Ajoutés** :
- `Modules/StartMenuLayout.psm1`
- `Modules/StartMenuPinning.psm1`
- `Modules/StartupManager.psm1`

**Modules Modifiés** :
- `Modules/InstallationEngine.psm1` (StoreApp detection, PSObject safe access, parallel logging)

**Base de Données** :
- `Apps/Database/applications.json` (7 apps corrigées)

**Tests** :
- `Tests/Test-ProtonAppsDetection.ps1` (nouveau)

**Documentation** :
- `README.md` (v2.3.0)
- `CHANGELOG.md` (ce fichier)

### 🐛 Bugs Résolus

1. **WhatsApp Desktop pas détecté**
   - Cause: Détection par Registry incorrecte
   - Fix: StoreApp avec PackageName + fallback nom de base
   - Status: ✅ Résolu

2. **Quick Assist pas détecté (FR/EN)**
   - Cause: Nom multilingue, PackageName tronqué par winget
   - Fix: Vendor prefix extraction regex
   - Status: ✅ Résolu

3. **Epic Games Launcher File path incorrect**
   - Cause: Chemin Win64 au lieu de Win32
   - Fix: Correction vers `.../Win32/EpicGamesLauncher.exe`
   - Status: ✅ Résolu

4. **Proton Apps File paths invalides**
   - Cause: Chemins `C:\Program Files\Proton\...` n'existent pas
   - Fix: Suppression Detection, utilisation fallback winget
   - Status: ✅ Résolu

5. **CUE Splitter app incorrecte**
   - Cause: Référence à CUETools au lieu de CUE Splitter
   - Fix: App Store correcte avec PackageName
   - Status: ✅ Résolu

6. **PowerShell 7 crash avec Get-AppxPackage**
   - Cause: Conflit assembly Appx en mode séquentiel
   - Fix: Utilisation winget list pour détection StoreApp
   - Status: ✅ Résolu

7. **InstallArguments erreur StrictMode**
   - Cause: Accès direct propriété non existante
   - Fix: PSObject.Properties safe access
   - Status: ✅ Résolu

### ⚠️ Breaking Changes

Aucun breaking change. Version 100% rétrocompatible avec v2.2.0.

### 🚀 Migration depuis v2.2.0

Aucune migration requise. Mise à jour transparente :

```powershell
# 1. Pull derniers changements
git pull

# 2. Valider base de données
.\Tools\Validate-AppDatabase.ps1

# 3. Tester avec un profil
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -TestMode

# 4. Déployer
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel
```

### 📚 Pour Plus d'Informations

- Guide complet : [README.md](README.md)
- Structure projet : [PROJET_STRUCTURE.md](PROJET_STRUCTURE.md)
- Guide GUI : [GUI_README.md](GUI_README.md)
- Base de données : [Apps/README.md](Apps/README.md)

---

## [2.2.0] - 2025-10-03

### 🎉 Major Release - Architecture Refactoring

Cette version majeure introduit une refonte complète de l'architecture avec une base de données centralisée, une interface GUI, et des outils de gestion avancés.

### ✨ Nouvelles Fonctionnalités

#### Interface GUI PowerShell (`Win11ForgeGUI.psm1`)
- **Ajouté**: Interface utilisateur interactive complète
  - Menu de navigation principal avec 8 options
  - Déploiement de profils avec sélection mode parallèle/séquentiel
  - Navigateur d'applications (66 apps) avec filtrage par catégorie/tag
  - Navigateur de profils avec visualisation détaillée
  - Créateur de profils custom interactif
  - Statistiques de base de données en temps réel
  - Validation de base de données intégrée
  - **Nouveau**: Option "Add New Application" avec recherche automatique

#### Base de Données Centralisée (`Apps/Database/applications.json`)
- **Ajouté**: Base de données centralisée v2.2.0
  - 66 applications référencées
  - Sources multiples: Winget, Chocolatey, Microsoft Store, DirectUrl
  - Métadonnées complètes: tags, vérification, homepage, priorité
  - Détection intelligente par méthode (Registry, File, Command, StoreApp, WindowsFeature)
  - Restrictions d'environnement par application
  - Format optimisé pour réutilisation

- **Module**: `ApplicationDatabase.psm1`
  - Chargement et cache de la base de données
  - Fonctions de requête par catégorie, tag, AppId
  - Export de statistiques
  - Validation de structure

#### ProfileCreator.html - Interface Web
- **Ajouté**: Créateur/éditeur de profils web
  - Interface en 6 étapes guidées
  - **66 applications** chargées dynamiquement depuis `applications-data.js`
  - Création de profils au format v2.2.0
  - **Nouveau**: Édition de profils existants (charger JSON)
  - Filtrage par catégorie et recherche
  - Configuration système (Explorer, Taskbar, Privacy, Performance)
  - Compatible `file://` (pas de serveur web requis)
  - Export JSON téléchargeable

#### Search-ApplicationSources.ps1
- **Ajouté**: Outil de recherche automatique d'applications
  - Recherche simultanée dans Winget, Chocolatey, Microsoft Store
  - Détection des URLs de téléchargement direct (patterns connus)
  - Génération de template JSON prêt à l'emploi
  - Modes interactif et automatisé
  - Affichage coloré avec résumé des résultats

#### Lanceurs avec Auto-Élévation
- **Amélioré**: `Deploy-Win11Forge.bat`
  - Auto-élévation automatique (UAC)
  - Plus besoin de clic-droit "Exécuter en tant qu'admin"

- **Ajouté**: `Start-Win11ForgeGUI-Admin.bat`
  - Lanceur GUI avec auto-élévation
  - Double-clic et c'est parti

### 🔄 Changements Majeurs

#### Format des Profils v2.2.0
- **BREAKING**: Nouveau format ultra-compact
  - Applications référencées par AppId uniquement
  - Définitions chargées depuis la base de données centralisée
  - Profils 10x plus petits et lisibles

**Ancien format (v2.1.x)** :
```json
{
  "Applications": [
    {
      "Name": "Google Chrome",
      "Category": "Browser",
      "Sources": {...},
      "Detection": {...}
    }
  ]
}
```

**Nouveau format (v2.2.0)** :
```json
{
  "Applications": [
    "GoogleChrome",
    "MozillaFirefox",
    "BraveBrowser"
  ]
}
```

#### Migration Automatique
- **Ajouté**: Scripts de migration v2.0 → v2.2.0
  - `Switch-ToProduction.ps1` : Migration des profils
  - `Test-NewProfiles.ps1` : Validation post-migration
  - Backup automatique dans `Archive/Profiles-v2.0-*/`
  - Conversion automatique au nouveau format

### ✨ Améliorations

#### Gestion des Profils
- **Amélioré**: `ProfileManager.psm1`
  - Résolution d'AppIds via base de données centralisée
  - Validation de profils avec vérification d'AppIds
  - Héritage optimisé avec cache
  - Messages d'erreur détaillés

#### Interface Utilisateur
- **Ajouté**: Fonction `Read-Choice` améliorée
  - Support de l'option '0' pour retour/annulation universelle
  - Messages d'aide contextuels
  - Validation robuste des choix
  - Plus de situations bloquantes

#### Applications
- **Ajouté**: Creality Slicer (impression 3D)
  - Chocolatey: `creality-print`
  - Détection: File
  - Catégorie: 3DPrint

### 🧹 Nettoyage et Organisation

#### Cleanup-ObsoleteFiles.ps1
- **Ajouté**: Script de nettoyage automatique
  - Archive les fichiers de test/migration obsolètes
  - Réorganise `Validate-Framework.ps1` vers `Tools/`
  - Mode `-DryRun` pour prévisualisation
  - Rapport détaillé des opérations

#### Structure du Projet
- **Réorganisé**: Dossier `Tools/`
  - Tous les utilitaires regroupés
  - Scripts de validation consolidés
  - Outils web (ProfileCreator.html)

- **Archivé**: Rapports de développement
  - `Archive/Docs-Reports-20251003/`
  - DEBUG_*, DATABASE_*, VALIDATION_*, INTEGRATION_*

### 📝 Documentation

#### Nouvelle Documentation
- **Ajouté**: `PROJET_STRUCTURE.md`
  - Structure complète du projet
  - Guide d'utilisation rapide
  - Explication de l'architecture
  - Cas d'usage détaillés

- **Mis à jour**: `README.md` v2.2.0
  - Nouvelles fonctionnalités GUI
  - Base de données centralisée
  - ProfileCreator.html
  - Guides de démarrage rapide

- **Ajouté**: `GUI_README.md`
  - Documentation complète de l'interface GUI
  - Captures d'écran et workflows
  - Guide des 8 options du menu

### 🐛 Corrections de Bugs

#### GUI
- **Corrigé**: Menus sans option de retour (blocage utilisateur)
- **Corrigé**: Erreurs PSObject.Properties sur certaines applications
- **Corrigé**: Accès aux sources Winget/Choco/Store/DirectUrl
- **Corrigé**: Comptage incorrect d'applications

#### Profils
- **Corrigé**: Script `Deploy-Win11Environment.ps1` fermait au lieu de retourner au GUI
  - Remplacé tous les `exit` par `return`
  - GUI reste ouvert après déploiement

#### Modules
- **Corrigé**: Scope des modules en mode parallèle
  - Ajout du flag `-Global` sur tous les Import-Module

### 📊 Statistiques v2.2.0

**Applications** : 66 (vs v2.1.3)
**Profils** : 4 (Base, Office, Gaming, Personnel)
**Modules** : 7 (+1 GUI)
**Outils** : 6 (+2 vs v2.1.3)
**Scripts** : 8 (+4 vs v2.1.3)

**Composition des Profils** :
- Base: 30 apps
- Office: 35 apps (Base + 5)
- Gaming: 39 apps (Office + 4)
- Personnel: 64 apps (Gaming + 25)

### 🔧 Fichiers Modifiés

**Nouveaux Modules** :
- `Modules/ApplicationDatabase.psm1`
- `Modules/Win11ForgeGUI.psm1`

**Nouveaux Scripts** :
- `Start-Win11ForgeGUI.ps1`
- `Start-Win11ForgeGUI-Admin.bat`
- `Tools/Search-ApplicationSources.ps1`
- `Cleanup-ObsoleteFiles.ps1`

**Nouveaux Outils** :
- `Tools/ProfileCreator.html`
- `Tools/applications-data.js`

**Base de Données** :
- `Apps/Database/applications.json` (nouvelle architecture)

**Profils Migrés** :
- `Profiles/Base.json` (format v2.2.0)
- `Profiles/Office.json` (format v2.2.0)
- `Profiles/Gaming.json` (format v2.2.0)
- `Profiles/Personnel.json` (format v2.2.0)

**Documentation** :
- `README.md` (v2.2.0)
- `PROJET_STRUCTURE.md` (nouveau)
- `GUI_README.md` (nouveau)

### ⚠️ Breaking Changes

1. **Format de Profils** : Les profils v2.0/v2.1 doivent être migrés vers v2.2.0
   - Utiliser `Switch-ToProduction.ps1` pour migration automatique
   - Ou utiliser ProfileCreator.html pour recréer

2. **Base de Données** : Les applications sont maintenant centralisées
   - Pas de définitions inline dans les profils
   - Toutes les apps doivent être dans `Apps/Database/applications.json`

### 🚀 Migration depuis v2.1.x

```powershell
# Étape 1: Backup automatique
.\Switch-ToProduction.ps1

# Étape 2: Test des nouveaux profils
.\Test-NewProfiles.ps1

# Étape 3: Validation
.\Tools\Validate-AppDatabase.ps1

# Étape 4 (optionnel): Nettoyage
.\Cleanup-ObsoleteFiles.ps1
```

### 📚 Pour Plus d'Informations

- Guide complet : [README.md](README.md)
- Structure projet : [PROJET_STRUCTURE.md](PROJET_STRUCTURE.md)
- Guide GUI : [GUI_README.md](GUI_README.md)
- Base de données : [Apps/README.md](Apps/README.md)

---

## [2.1.3] - 2025-10-03

### 🐛 Bug Fixes

#### Installation Issues
- **Fixed**: Battle.net installation - Added custom silent install arguments support (`--lang=frFR --installpath=...`)
  - ✅ **VALIDATED**: Tested and confirmed 100% silent installation with Perplexity Pro verified switches
- **Fixed**: WhatsApp Desktop - Corrected Winget ID to `9NKSQGP7F2NH` (Store ID)
- **Fixed**: Proton Drive - Corrected Winget ID from `Proton.Drive` to `Proton.ProtonDrive`
- **Fixed**: Proton Mail Bridge - Corrected Winget ID from `ProtonTechnologies.ProtonMailBridge` to `Proton.ProtonMailBridge`
- **Fixed**: Proton Pass - Corrected Winget ID from `Proton.Pass` to `Proton.ProtonPass`
- **Verified**: Google Drive for Desktop - Winget `Google.GoogleDrive` and Chocolatey `googledrive`
- **Verified**: PDF-XChange Editor - Winget `TrackerSoftware.PDF-XChangeEditor` and Chocolatey `pdfxchangeeditor`

#### System Configuration
- **Fixed**: DNS configuration not parsing array types correctly
  - Added support for `System.Collections.ArrayList`
  - Added support for `System.Collections.Generic.List[object]`
  - Added fallback enumeration for unknown array types
  - Location: `Modules/SystemConfig.psm1` v2.0.4

### ✨ Enhancements

#### Installation Engine (v2.1.3)
- **Added**: Custom install arguments support for DirectDownload method
  - New parameter: `InstallArguments` in application JSON
  - Example: Battle.net uses `--lang=frFR --installpath="C:\Program Files (x86)\Battle.net"`
  - Location: `Modules/InstallationEngine.psm1`

- **Enhanced**: Error logging with detailed failure tracking
  - Tracks all attempted installation methods
  - Provides specific failure reasons for each method
  - Includes package IDs and names in verbose output
  - Improved final error messages with full context

#### Profile Updates
- `Profiles/Gaming.json`
  - Updated Battle.net with Store ID `XPDM5VSMTKQLBJ`
  - Added `InstallArguments` field for silent installation
  - Prioritizes DirectUrl over Store for better automation

- `Profiles/Office.json`
  - Updated WhatsApp Desktop with verified Store ID
  - Added fallback to Store source

- `Profiles/Personnel.json`
  - All Proton applications IDs corrected
  - Added notes for each verified ID

### 🧪 Testing & Validation

- **Added**: `Debug-FailedApps.ps1` - Automated ID validation script
  - Tests Winget, Chocolatey, and Store IDs
  - Color-coded output (Pass/Fail/Skip)
  - Success rate calculation
  - **Result**: 100% validation pass rate (10/10 tests passed)

- **Added**: Complete debugging documentation in CHANGELOG
  - Issue analysis
  - Corrections applied
  - Validation results
  - Testing recommendations

### 📊 Performance Improvements

**Expected Results** (compared to v2.1.2):
- Installation success rate: ~83% → ~95%
- Failed applications: 11 → 0-1 (excluding environment restrictions)
- New successful installs: +6-7 applications

### 📝 Files Modified

**Profiles**:
- `Profiles/Gaming.json` (Battle.net)
- `Profiles/Office.json` (WhatsApp Desktop)
- `Profiles/Personnel.json` (Proton apps + Google Drive)

**Modules**:
- `Modules/SystemConfig.psm1` v2.0.4 (DNS parsing)
- `Modules/InstallationEngine.psm1` v2.1.3 (Error handling + custom arguments)

**New Files**:
- `Debug-FailedApps.ps1` (Validation script)
- `CHANGELOG.md` (This file)

---

## [2.1.2] - 2025-10-02

### Fixed
- Empty `Write-Log` calls causing errors
- `InheritanceChain.Count` errors in profile loading
- PowerToys multi-path detection
- Quick Assist Store App detection

### Added
- Parallel installation support (up to 5 concurrent apps)
- PowerShell 7 detection and upgrade prompt

---

## [2.1.1] - 2025-10-01

### Fixed
- DNS array handling in SystemConfig
- Taskbar configuration error handling

---

## [2.1.0] - 2025-10-01

### Added
- Parallel installation mode with `-Parallel` parameter
- `MaxParallelJobs` parameter (default: 5)
- Installation mode logging (Sequential vs Parallel)

### Enhanced
- Installation Engine performance optimizations
- Better progress tracking for parallel installations

---

## [2.0.2] - 2025-09-30

### Fixed
- Base profile application priorities
- Detection methods for various applications

---

## [2.0.0] - 2025-09-30

### Initial Release
- Complete framework restructure
- Modular architecture (Core + 5 modules)
- Profile inheritance system (Base → Office → Gaming → Personnel)
- Multi-source installation (Winget → Chocolatey → Store → DirectUrl)
- Environment detection (Sandbox/VMware/Hyper-V/VirtualBox/Physical)
- Comprehensive logging and reporting

---

**Legend**:
- 🐛 Bug Fix
- ✨ Enhancement
- 🧪 Testing
- 📊 Performance
- 🔒 Security
- 📝 Documentation
