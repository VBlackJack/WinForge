# Win11Forge Framework v2.4.0

**Framework modulaire d'automatisation du déploiement post-installation Windows 11 24H2**

[![Version](https://img.shields.io/badge/version-2.4.0-blue.svg)](CHANGELOG.md)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.4+-blue.svg)](https://github.com/PowerShell/PowerShell)
[![Windows](https://img.shields.io/badge/Windows-11%2024H2-0078D4.svg)](https://www.microsoft.com/windows)
[![Status](https://img.shields.io/badge/status-Production%20Ready-success.svg)](CHANGELOG.md)

## 📋 Vue d'ensemble

Win11Forge est un framework PowerShell conçu pour automatiser entièrement le déploiement d'environnements Windows 11 après une installation fraîche. Il gère intelligemment l'installation de prérequis, d'applications et la configuration système selon des profils JSON personnalisables.

### ✨ Caractéristiques principales

- ✅ **Interface GUI PowerShell** : Menu interactif complet pour navigation facile
- ✅ **Base de données centralisée** : 66 applications en base de données (64 dans les profils actifs)
- ✅ **Détection d'environnement** : Sandbox Windows, VMware, Hyper-V, VirtualBox, PC physique
- ✅ **Installation multi-sources** : Winget → Chocolatey → Microsoft Store → Téléchargement direct
- ✅ **Installation parallèle** : Jusqu'à 5 applications simultanées (PowerShell 7+)
- ✅ **Détection StoreApp améliorée** : Support complet Microsoft Store avec détection multilingue
- ✅ **Profils hiérarchiques** : Base → Office → Gaming → Personnel (héritage automatique)
- ✅ **Start Menu Pinning** : Épinglage fiable via start2.bin (Windows 11 22H2+)
- ✅ **Logs parallèles** : Logs individuels par application en mode parallèle
- ✅ **ProfileCreator HTML** : Créez et éditez des profils via interface web (file://)
- ✅ **Ajout d'applications** : Recherche automatique dans tous les stores
- ✅ **Prérequis automatiques** : PowerShell 7, .NET, VC++ Redistributables, Java
- ✅ **Gestion intelligente** : Détection d'applications installées, exclusions par environnement
- ✅ **Logging avancé** : Logs détaillés avec tracking d'erreurs, statistiques complètes

## 🏗️ Architecture

```
Win11Forge/
├── Start-Win11ForgeGUI-Admin.bat    # ⭐ Lanceur GUI avec auto-élévation
├── Start-Win11ForgeGUI.ps1          # Script GUI PowerShell
├── Deploy-Win11Forge.bat            # ⭐ Déploiement rapide avec auto-élévation
├── Deploy-Win11Environment.ps1      # Script principal de déploiement
├── Cleanup-Framework.ps1            # Nettoyage et maintenance
├── Cleanup-ObsoleteFiles.ps1        # Nettoyage fichiers obsolètes
├── Disable-AutostartApps.ps1        # Désactiver apps au démarrage
├── Setup-Framework.ps1              # Installation framework
│
├── Apps/
│   └── Database/
│       └── applications.json        # Base centralisée (66 apps)
│
├── Core/
│   └── Core.psm1                    # Fonctions communes
│
├── Modules/
│   ├── ApplicationDatabase.psm1     # Gestion base de données
│   ├── Prerequisites.psm1           # Prérequis système
│   ├── EnvironmentDetection.psm1    # Détection environnement
│   ├── ProfileManager.psm1          # Gestion profils + héritage
│   ├── InstallationEngine.psm1      # Moteur d'installation (StoreApp support)
│   ├── SystemConfig.psm1            # Configuration système
│   ├── StartMenuLayout.psm1         # Organisation Start Menu par catégorie
│   ├── StartMenuPinning.psm1        # Épinglage Start Menu (start2.bin)
│   ├── StartupManager.psm1          # Gestion applications au démarrage
│   └── Win11ForgeGUI.psm1           # Interface graphique PowerShell
│
├── Profiles/
│   ├── Base.json                    # 30 apps essentielles
│   ├── Office.json                  # Base + 5 apps (35 total)
│   ├── Gaming.json                  # Office + 4 apps (39 total)
│   └── Personnel.json               # Gaming + 25 apps (64 total)
│
├── Tools/
│   ├── ProfileCreator.html          # ⭐ Créateur de profils web
│   ├── StartupManager.html          # Interface gestion démarrage
│   ├── Launch-AsTrustedInstaller.bat # ⭐ Lanceur TrustedInstaller
│   ├── Launch-TrustedInstallerGUI.ps1 # Script PowerShell TrustedInstaller
│   ├── Launch-StartupManager.ps1    # Lancer interface StartupManager
│   ├── Search-ApplicationSources.ps1 # ⭐ Recherche dans stores
│   ├── Validate-AppDatabase.ps1     # Validation base de données
│   └── Validate-Framework.ps1       # Validation framework complet
│
├── Backups/                         # Sauvegardes système
│   └── StartMenuLayouts/            # Layouts Start Menu
│
├── Archive/                         # Fichiers archivés (générés)
│   └── Profiles-v2.0-*/             # Anciens profils
│
└── Logs/                            # Logs de déploiement (générés)
    ├── deployment_*.log             # Logs principaux
    └── Parallel/                    # Logs mode parallèle
        └── AppName_*.log            # Logs par app
```

## 🚀 Démarrage Rapide

### 🎮 **Méthode 1 : GUI PowerShell (Recommandé)**

```batch
# Double-clic sur :
Start-Win11ForgeGUI-Admin.bat
```

**Menu principal :**
1. Deploy Profile - Déployer un profil
2. Browse Applications Database - Parcourir les 66 apps
3. Browse Profiles - Voir les profils disponibles
4. Create Custom Profile - Créer un profil personnalisé
5. Database Statistics - Statistiques de la base
6. Validate Database - Valider la base de données
7. **Add New Application** - Ajouter une app (recherche auto)

### ⚡ **Méthode 2 : Déploiement Console Rapide**

```batch
# Double-clic sur :
Deploy-Win11Forge.bat

# Choisir le profil :
# 1 = Base (30 apps)
# 2 = Office (35 apps)
# 3 = Gaming (39 apps)
# 4 = Personnel (64 apps)
```

### 💻 **Méthode 3 : PowerShell Direct**

```powershell
# Déploiement standard
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming"

# Déploiement parallèle (3-5x plus rapide)
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel

# Mode test (dry run)
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -TestMode -Verbose
```

## 🛠️ Créer et Éditer des Profils

### **ProfileCreator.html - Interface Web**

```batch
# Ouvrir dans un navigateur :
Tools\ProfileCreator.html
```

**Fonctionnalités :**
- ✅ **66 applications** disponibles (base de données centralisée)
- ✅ **Création de profils** en 6 étapes guidées
- ✅ **Édition de profils** existants (charger JSON)
- ✅ **Filtrage par catégorie** et tags
- ✅ **Configuration système** (Explorer, Taskbar, Privacy)
- ✅ **Compatible file://** (pas de serveur web requis)
- ✅ **Export JSON** au format v2.4.0

### **Via GUI PowerShell**

```
Menu principal → 4. Create Custom Profile
```

## 🔍 Ajouter une Application

### **Via GUI (Recommandé)**

```
Menu principal → 7. Add New Application
```

1. Entrez le nom de l'application (ex: "Discord")
2. Le système recherche automatiquement dans :
   - Winget
   - Chocolatey
   - Microsoft Store
   - URLs de téléchargement direct
3. Complétez les informations (catégorie, détection, etc.)
4. L'application est ajoutée à la base de données
5. Mise à jour automatique de ProfileCreator.html

### **Via Script**

```powershell
.\Tools\Search-ApplicationSources.ps1 -AppName "Spotify"
```

## 📦 Profils de Déploiement

### **Base (30 applications)**
Fondation universelle avec outils essentiels

- **Navigateurs** : Chrome, Firefox, Brave
- **Média** : VLC, MPC-HC, FastStone Viewer, Paint.NET
- **Utilitaires** : ShareX, 7-Zip, Notepad++, PowerToys, Everything, WizTree, LockHunter
- **Support** : Quick Assist, Windows Sandbox, Windows Terminal
- **Diagnostic** : HWiNFO64, CrystalDiskInfo, Process Hacker, Autoruns, BlueScreenView
- **Sécurité** : Windows Firewall Control, KeePassXC, Malwarebytes
- **Réseau** : WiFi Analyzer, Advanced IP Scanner
- **Recovery** : Recuva
- **Config** : WinAero Tweaker

### **Office (Base + 5 = 35 apps)**
Productivité professionnelle

- Hérite de **Base**
- Microsoft Office 365
- PDF-XChange Editor
- Signal Desktop
- WhatsApp Desktop
- OBS Studio

### **Gaming (Office + 4 = 39 apps)**
Plateforme gaming complète

- Hérite d'**Office** (donc aussi Base)
- Steam
- Discord
- Epic Games Launcher
- Battle.net

### **Personnel (Gaming + 25 = 64 apps)**
Environnement développeur avancé

- Hérite de **Gaming** (donc aussi Office et Base)
- **Développement** : VS Code, MobaXterm, Git, Python 3, Node.js, .NET SDK
- **Réseau** : PuTTY, WinSCP, Wireshark
- **Système** : Sysinternals Suite
- **Sécurité** : YubiKey Manager, Proton VPN, Proton Drive, Proton Mail Bridge, Proton Pass, Roboform
- **Cloud Storage** : pCloud Drive, Google Drive for Desktop
- **Virtualisation** : Sandboxie Plus, LDPlayer
- **Utilitaires** : Internet Download Manager, WinRAR
- **Multimédia** : Mp3tag, MediaMonkey, TV Rename

## 🎯 Base de Données Centralisée v2.4.0

### **Structure**

```json
{
  "DatabaseVersion": "2.4.0",
  "TotalApplications": 66,
  "Applications": {
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
      "Tags": ["browser", "popular", "essential"],
      "Verified": true,
      "Homepage": "https://www.google.com/chrome/"
    }
  }
}
```

### **Format des Profils v2.4.0**

Les profils référencent simplement les AppIds :

```json
{
  "Name": "Gaming",
  "Description": "Profil gaming complet",
  "Version": "2.4.0",
  "Inherits": ["Office"],
  "Applications": [
    "Steam",
    "Discord",
    "EpicGamesLauncher",
    "BattleNet"
  ],
  "SystemConfig": {
    "Performance": {
      "GameMode": true,
      "HighPerformancePower": true
    }
  }
}
```

**Avantages :**
- ✅ Aucune duplication de données
- ✅ Mise à jour centralisée des sources
- ✅ Profils ultra-compacts
- ✅ Héritage optimisé

## 🔧 Paramètres de Déploiement

| Paramètre | Description |
|-----------|-------------|
| `-ProfileName` | Nom du profil ou chemin JSON |
| `-Parallel` | ⭐ Installation parallèle (max 5 apps, PS7+) |
| `-MaxParallelJobs` | Nombre max d'apps simultanées (défaut: 5) |
| `-TestMode` | Mode test sans installation |
| `-SkipPrerequisites` | Sauter les prérequis |
| `-SkipSystemConfig` | Sauter la config système |
| `-Force` | Forcer la réinstallation |
| `-Verbose` | Affichage détaillé |

## 🎯 Détection d'Environnement

Le framework détecte automatiquement l'environnement et adapte le déploiement :

### **Windows Sandbox**
- ❌ Drivers, Hyper-V, virtualisation imbriquée
- ⚠️ Non-persistant

### **VMware / Hyper-V / VirtualBox**
- ❌ Virtualisation imbriquée
- ❌ Outils matériels (imprimantes 3D, YubiKey)

### **PC Physique**
- ✅ Toutes fonctionnalités disponibles

### **Exclusions Automatiques**

```json
{
  "Name": "YubiKey Manager",
  "EnvironmentRestrictions": ["WindowsSandbox"]
}
```

## 📊 Logs et Statistiques

### **Logs de Déploiement**

```
Logs/deployment_20251003_230045.log
```

### **Rapport de Déploiement**

```
=== Deployment Summary ===
Profile: Personnel
Environment: Physical PC
Start Time: 2025-10-03 23:00:45
End Time: 2025-10-03 23:45:22
Duration: 00:44:37
Mode: Parallel (5 concurrent jobs)

Applications Statistics:
  Total: 66
  Installed: 62
  Already Installed: 3
  Skipped: 1 (environment restriction)
  Failed: 0

Success Rate: 100%
```

## 🧹 Maintenance

### **Nettoyer les Logs**

```powershell
.\Cleanup-Framework.ps1
```

### **Archiver les Fichiers Obsolètes**

```powershell
# Preview
.\Cleanup-ObsoleteFiles.ps1 -DryRun

# Execute
.\Cleanup-ObsoleteFiles.ps1
```

### **Valider la Base de Données**

```powershell
.\Tools\Validate-AppDatabase.ps1
```

## 🆕 Nouveautés v2.4.0

✨ **Détection Store Apps Améliorée**
- Support complet méthode `StoreApp` avec PackageName
- Détection multilingue (Quick Assist FR/EN, etc.)
- Compatible PowerShell 7 mode parallèle et séquentiel
- Évite les conflits module Appx via winget list

✨ **Logs Parallèles Individuels**
- Logs séparés par application en mode parallèle (`Logs/Parallel/`)
- Tracking temps réel de chaque installation
- Stack traces détaillées avec numéros de ligne
- Facilite le debugging des crashs et erreurs

✨ **Start Menu Pinning (start2.bin)**
- Méthode fiable pour Windows 11 22H2+
- Épinglage items au Start Menu
- Support Default profile + utilisateur courant
- Remplace LayoutModification.json (déprécié)

✨ **Corrections Majeures**
- WhatsApp Desktop: Détection StoreApp avec fallback nom de base
- Quick Assist: Détection par préfixe PackageName vendor
- Epic Games Launcher: Chemin File corrigé (Win32 vs Win64)
- Proton Apps: Détection par nom winget (chemins incorrects supprimés)
- CUE Splitter: Corrigé de CUETools vers app Store correcte
- InstallArguments: Accès sécurisé PSObject.Properties (StrictMode)

✨ **Stabilité PowerShell 7**
- Résolution conflits assembly Appx en mode séquentiel
- Détection StoreApp via winget au lieu de Get-AppxPackage
- Support complet PS7 parallèle sans crashes

## 📖 Documentation

- **[PROJET_STRUCTURE.md](PROJET_STRUCTURE.md)** - Structure détaillée du projet
- **[GUI_README.md](GUI_README.md)** - Guide complet de l'interface GUI
- **[CHANGELOG.md](CHANGELOG.md)** - Historique des versions
- **[Apps/README.md](Apps/README.md)** - Documentation de la base de données
- **[Tools/README.md](Tools/README.md)** - Outils utilitaires

## ⚠️ Limitations Connues

- Applications Store nécessitent un compte Microsoft
- Windows Sandbox non-persistant (reset après redémarrage)
- Virtualisation imbriquée non supportée en VM
- Certaines apps nécessitent configuration post-installation

## 🤝 Contribution

Pour ajouter une application :

1. Via GUI : `Menu → 7. Add New Application`
2. Via Script : `.\Tools\Search-ApplicationSources.ps1 -AppName "NomApp"`
3. Testez dans Sandbox → VM → PC physique
4. Validez : `.\Tools\Validate-AppDatabase.ps1`

## 📄 Licence

Win11Forge Framework - Outil de productivité personnel

## 🔗 Ressources

- [Winget Documentation](https://learn.microsoft.com/en-us/windows/package-manager/)
- [Chocolatey Packages](https://community.chocolatey.org/packages)
- [PowerShell 7](https://github.com/PowerShell/PowerShell)

---

## 📊 Statut du Projet

**Version** : 2.4.0 ✅
**Dernière mise à jour** : 2025-10-06
**Compatibilité** : Windows 11 24H2 (22H2+)
**Statut** : Production Ready
**Applications** : 66 dans la base de données (64 en profils actifs)
**Profils** : 4 (Base, Office, Gaming, Personnel)
**PowerShell** : 5.1+ (7.5+ recommandé pour mode parallèle)

📖 **[CHANGELOG complet](CHANGELOG.md)** | 📁 **[Structure du projet](PROJET_STRUCTURE.md)** | 🎮 **[Guide GUI](GUI_README.md)**
