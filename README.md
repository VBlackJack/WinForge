# Win11Forge Framework v2.2.0

**Framework modulaire d'automatisation du déploiement post-installation Windows 11 24H2**

[![Version](https://img.shields.io/badge/version-2.2.0-blue.svg)](CHANGELOG.md)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5+-blue.svg)](https://github.com/PowerShell/PowerShell)
[![Windows](https://img.shields.io/badge/Windows-11%2024H2-0078D4.svg)](https://www.microsoft.com/windows)
[![Status](https://img.shields.io/badge/status-Production%20Ready-success.svg)](CHANGELOG.md)

## 📋 Vue d'ensemble

Win11Forge est un framework PowerShell conçu pour automatiser entièrement le déploiement d'environnements Windows 11 après une installation fraîche. Il gère intelligemment l'installation de prérequis, d'applications et la configuration système selon des profils JSON personnalisables.

### ✨ Caractéristiques principales

- ✅ **Interface GUI PowerShell** : Menu interactif complet pour navigation facile
- ✅ **Base de données centralisée** : 67 applications avec sources multiples
- ✅ **Détection d'environnement** : Sandbox Windows, VMware, Hyper-V, VirtualBox, PC physique
- ✅ **Installation multi-sources** : Winget → Chocolatey → Microsoft Store → Téléchargement direct
- ✅ **Installation parallèle** : Jusqu'à 5 applications simultanées (PowerShell 7+)
- ✅ **Profils hiérarchiques** : Base → Office → Gaming → Personnel (héritage automatique)
- ✅ **ProfileCreator HTML** : Créez et éditez des profils via interface web (file://)
- ✅ **Ajout d'applications** : Recherche automatique dans tous les stores
- ✅ **Prérequis automatiques** : PowerShell 7, .NET, VC++ Redistributables, Java
- ✅ **Gestion intelligente** : Détection d'applications installées, exclusions par environnement
- ✅ **Logging avancé** : Logs détaillés avec tracking d'erreurs, statistiques complètes

## 🏗️ Architecture

```
Win11Forge/
├── Start-Win11ForgeGUI-Admin.bat    # ⭐ Lanceur GUI avec auto-élévation
├── Deploy-Win11Forge.bat            # ⭐ Déploiement rapide avec auto-élévation
├── Deploy-Win11Environment.ps1      # Script principal de déploiement
│
├── Apps/
│   └── Database/
│       └── applications.json        # Base centralisée (67 apps)
│
├── Core/
│   └── Core.psm1                    # Fonctions communes
│
├── Modules/
│   ├── ApplicationDatabase.psm1     # Gestion base de données
│   ├── Prerequisites.psm1           # Prérequis système
│   ├── EnvironmentDetection.psm1    # Détection environnement
│   ├── ProfileManager.psm1          # Gestion profils + héritage
│   ├── InstallationEngine.psm1      # Moteur d'installation
│   ├── SystemConfig.psm1            # Configuration système
│   └── Win11ForgeGUI.psm1           # Interface graphique PowerShell
│
├── Profiles/
│   ├── Base.json                    # 31 apps essentielles
│   ├── Office.json                  # Base + 5 apps (36 total)
│   ├── Gaming.json                  # Office + 4 apps (40 total)
│   └── Personnel.json               # Gaming + 26 apps (66 total)
│
├── Tools/
│   ├── ProfileCreator.html          # ⭐ Créateur de profils web
│   ├── applications-data.js         # Base de données pour HTML
│   ├── Search-ApplicationSources.ps1 # ⭐ Recherche dans stores
│   ├── Validate-AppDatabase.ps1     # Validation base
│   └── Validate-Framework.ps1       # Validation framework
│
└── Logs/
    └── deployment_*.log             # Logs horodatés
```

## 🚀 Démarrage Rapide

### 🎮 **Méthode 1 : GUI PowerShell (Recommandé)**

```batch
# Double-clic sur :
Start-Win11ForgeGUI-Admin.bat
```

**Menu principal :**
1. Deploy Profile - Déployer un profil
2. Browse Applications Database - Parcourir les 67 apps
3. Browse Profiles - Voir les profils disponibles
4. Create Custom Profile - Créer un profil personnalisé
5. Database Statistics - Statistiques de la base
6. Validate Database - Valider la base de données
7. Settings & Options - Configuration
8. **Add New Application** - Ajouter une app (recherche auto)

### ⚡ **Méthode 2 : Déploiement Console Rapide**

```batch
# Double-clic sur :
Deploy-Win11Forge.bat

# Choisir le profil :
# 1 = Base (31 apps)
# 2 = Office (36 apps)
# 3 = Gaming (40 apps)
# 4 = Personnel (66 apps)
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
- ✅ **67 applications** disponibles (base de données centralisée)
- ✅ **Création de profils** en 6 étapes guidées
- ✅ **Édition de profils** existants (charger JSON)
- ✅ **Filtrage par catégorie** et tags
- ✅ **Configuration système** (Explorer, Taskbar, Privacy)
- ✅ **Compatible file://** (pas de serveur web requis)
- ✅ **Export JSON** au format v2.2.0

### **Via GUI PowerShell**

```
Menu principal → 4. Create Custom Profile
```

## 🔍 Ajouter une Application

### **Via GUI (Recommandé)**

```
Menu principal → 8. Add New Application
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

### **Base (31 applications)**
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

### **Office (Base + 5 = 36 apps)**
Productivité professionnelle

- Hérite de **Base**
- Microsoft Office 365
- PDF-XChange Editor
- Signal Desktop
- WhatsApp Desktop
- OBS Studio

### **Gaming (Office + 4 = 40 apps)**
Plateforme gaming complète

- Hérite d'**Office** (donc aussi Base)
- Steam
- Discord
- Epic Games Launcher
- Battle.net

### **Personnel (Gaming + 26 = 66 apps)**
Environnement développeur avancé

- Hérite de **Gaming** (donc aussi Office et Base)
- **Développement** : VS Code, MobaXterm, Git, Python 3, Node.js, .NET SDK
- **Réseau** : PuTTY, WinSCP, Wireshark
- **Système** : Sysinternals Suite
- **Sécurité** : YubiKey Manager, Proton VPN, Proton Drive, Proton Mail Bridge, Proton Pass, Roboform
- **Cloud Storage** : pCloud Drive, Google Drive for Desktop
- **Virtualisation** : Sandboxie Plus, LDPlayer
- **Utilitaires** : Internet Download Manager, WinRAR
- **Multimédia** : Mp3tag, CUETools, MediaMonkey, TV Rename
- **3D Printing** : Creality Slicer

## 🎯 Base de Données Centralisée v2.2.0

### **Structure**

```json
{
  "DatabaseVersion": "2.2.0",
  "TotalApplications": 67,
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

### **Format des Profils v2.2.0**

Les profils référencent simplement les AppIds :

```json
{
  "Name": "Gaming",
  "Description": "Profil gaming complet",
  "Version": "2.2.0",
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

## 🆕 Nouveautés v2.2.0

✨ **Interface GUI PowerShell**
- Menu interactif complet
- Navigation intuitive
- Ajout d'applications intégré

✨ **Base de Données Centralisée**
- 67 applications référencées
- Sources multiples (Winget, Choco, Store, Direct)
- Métadonnées complètes (tags, vérification, homepage)

✨ **ProfileCreator.html**
- Interface web pour créer/éditer des profils
- 67 applications dynamiques
- Compatible file:// (pas de serveur requis)
- Édition de profils existants

✨ **Search-ApplicationSources.ps1**
- Recherche automatique dans tous les stores
- Génère un template JSON prêt à l'emploi
- Détection des URLs de téléchargement direct

✨ **Auto-Élévation Admin**
- Tous les lanceurs s'auto-élèvent
- Plus besoin de clic-droit "Exécuter en tant qu'admin"

✨ **Format de Profils Optimisé**
- AppIds au lieu d'objets complets
- Profils ultra-compacts
- Héritage amélioré

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

1. Via GUI : `Menu → 8. Add New Application`
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

**Version** : 2.2.0 ✅
**Dernière mise à jour** : 2025-10-03
**Compatibilité** : Windows 11 24H2
**Statut** : Production Ready
**Applications** : 67 dans la base de données
**Profils** : 4 (Base, Office, Gaming, Personnel)

📖 **[CHANGELOG complet](CHANGELOG.md)** | 📁 **[Structure du projet](PROJET_STRUCTURE.md)** | 🎮 **[Guide GUI](GUI_README.md)**
