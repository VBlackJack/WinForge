# Win11Forge v3.0.0

**Framework moderne de deploiement post-installation Windows 11**

[![Version](https://img.shields.io/badge/version-3.0.0-blue.svg)](CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.4+-blue.svg)](https://github.com/PowerShell/PowerShell)
[![Windows](https://img.shields.io/badge/Windows-11%2024H2-0078D4.svg)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/tests-479-green.svg)](Tests/README.md)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

## Vue d'ensemble

Win11Forge automatise le deploiement d'environnements Windows 11 avec une **interface graphique WPF moderne** et un moteur PowerShell robuste.

### Nouveautes v3.0.0

- **Interface WPF native** - Application .NET 8 avec Material Design
- **Dashboard temps reel** - Statistiques systeme et historique des deploiements
- **Deploiement parallele** - Jusqu'a 5 installations simultanees avec progress tracking
- **Gestion des profils** - Creation et edition visuelle avec support de l'heritage
- **Internationalisation** - Interface disponible en anglais et francais
- **Mode sombre/clair** - Theme adaptatif

## Demarrage Rapide

### Interface Graphique (Recommande)

```batch
Win11Forge.GUI.exe
```

Ou double-cliquez sur `Start-Win11ForgeGUI.bat`

### Mode Console (CLI)

```powershell
.\Start-Win11Forge.ps1 -CLI -ProfileName "Gaming"

# Deploiement parallele
.\Start-Win11Forge.ps1 -CLI -ProfileName "Personnel" -Parallel
```

## Architecture

```
Win11Forge/
|-- GUI/                              # Application WPF (.NET 8)
|   +-- Win11Forge.GUI/
|       |-- Views/                    # Vues XAML
|       |-- ViewModels/               # MVVM ViewModels
|       |-- Services/                 # PowerShellBridge, History
|       |-- Models/                   # Modeles de donnees
|       +-- Resources/                # i18n (EN/FR)
|
|-- Core/                             # Moteur PowerShell
|   +-- Core.psm1
|
|-- Modules/                          # Modules fonctionnels
|   |-- InstallationEngine.psm1       # Winget/Choco/Store/DirectUrl
|   |-- ProfileManager.psm1           # Gestion profils + heritage
|   |-- ApplicationDatabase.psm1      # Base de donnees apps
|   +-- ...
|
|-- Profiles/                         # Profils JSON
|   |-- Base.json                     # 30 apps essentielles
|   |-- Office.json                   # +5 apps productivite
|   |-- Gaming.json                   # +4 apps gaming
|   +-- Personnel.json                # +25 apps developpeur
|
|-- Apps/Database/                    # Base centralisee
|   +-- applications.json             # 66 applications
|
|-- Tests/                            # Tests Pester (479 tests)
+-- Docs/                             # Documentation
    |-- USER_GUIDE_v3.md              # Guide utilisateur v3
    +-- ROADMAP-v3.0.0.md             # Feuille de route
```

## Fonctionnalites

| Fonctionnalite | Description |
|----------------|-------------|
| **Multi-sources** | Winget -> Chocolatey -> Store -> DirectUrl |
| **Detection intelligente** | Registry, File, Command, StoreApp |
| **Profils hierarchiques** | Base -> Office -> Gaming -> Personnel |
| **Retry logic** | 3 tentatives avec backoff exponentiel |
| **Checksum SHA256** | Validation des telechargements directs |
| **Environnements** | Physical, Sandbox, VMware, Hyper-V |

## Documentation

- **[Guide Utilisateur v3](Docs/USER_GUIDE_v3.md)** - Documentation complete
- **[Roadmap v3.0.0](Docs/ROADMAP-v3.0.0.md)** - Fonctionnalites planifiees
- **[Changelog](CHANGELOG.md)** - Historique des versions
- **[Tests](Tests/README.md)** - Documentation des tests

## Profils de Deploiement

| Profil | Apps | Description |
|--------|------|-------------|
| **Base** | 30 | Navigateurs, utilitaires, diagnostic |
| **Office** | 35 | Base + productivite (Office 365, PDF) |
| **Gaming** | 39 | Office + Steam, Discord, Epic |
| **Personnel** | 64 | Gaming + dev tools, cloud storage |

## Prerequis

- Windows 11 22H2 ou superieur
- .NET 8.0 Runtime (pour le GUI)
- PowerShell 7.4+ (recommande) ou 5.1
- Droits administrateur

## Licence

Apache License 2.0 - Voir [LICENSE](LICENSE)

---

**Win11Forge v3.0.0** | [Documentation](Docs/USER_GUIDE_v3.md) | [Changelog](CHANGELOG.md)
