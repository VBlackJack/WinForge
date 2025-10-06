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

## 🔍 Surveillance et Audit

### Launch-SystemAudit.bat ⭐
**Lanceur interactif pour System-Audit.ps1 avec auto-élévation admin**

```bash
# Depuis le répertoire Tools :
.\Launch-SystemAudit.bat
```

**Menu interactif avec 8 modes** :
1. Win11Forge Deployment (auto-stop à la fin)
2. Monitor Process by Name
3. Monitor Process by PID
4. Monitor Log File
5. Monitor Log Directory
6. Timed Audit (30 minutes)
7. Custom Parameters (avancé)
8. Launch with PowerShell ISE

**Fonctionnalités** :
- Auto-élévation admin si nécessaire
- Interface guidée pour tous les modes
- Génération automatique de rapports
- Retour au menu après chaque audit

---

### System-Audit.ps1 ⭐ v2.1.0 - UNIVERSAL
**Outil d'audit système universel - Fonctionne avec N'IMPORTE QUEL script ou processus**

```powershell
# ═══ Win11Forge Deployment ═══
.\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" -LogCompletionMarkers "Deployment completed|Summary" -GenerateReport

# ═══ Surveiller un processus spécifique (par nom) ═══
.\Tools\System-Audit.ps1 -MonitorProcessName "powershell" -AuditName "PowerShellAudit" -GenerateReport

# ═══ Surveiller un processus spécifique (par PID) ═══
$proc = Start-Process powershell -ArgumentList "-File", "MonScript.ps1" -PassThru
.\Tools\System-Audit.ps1 -MonitorProcessId $proc.Id -AuditName "MonScript" -GenerateReport

# ═══ Surveiller un fichier log spécifique ═══
.\Tools\System-Audit.ps1 -MonitorLogFile "C:\Logs\app.log" -LogCompletionMarkers "DONE|COMPLETED" -GenerateReport

# ═══ Audit temporisé (sans auto-stop) ═══
.\Tools\System-Audit.ps1 -Duration 30 -AuditName "PerformanceTest" -GenerateReport

# ═══ Audit complet avec toutes les options ═══
.\Tools\System-Audit.ps1 -MonitorProcessName "installer" `
    -MonitorRegistry -MonitorFileSystem `
    -SampleInterval 5 -GenerateReport `
    -AuditName "CompleteInstallAudit"
```

**🆕 Fonctionnalités v2.1.0** :
- ✅ **100% Générique** : Fonctionne avec n'importe quel script, processus ou log
- ✅ **Monitor par Process** : Surveille un processus (nom ou PID) et s'arrête à sa terminaison
- ✅ **Monitor par Log** : Surveille un fichier log et détecte sa complétion
- ✅ **Monitor par Directory** : Détecte nouveau log dans un dossier et surveille sa complétion
- ✅ **Marqueurs personnalisables** : Regex pour détecter la fin (default: "completed|finished|Summary")
- ✅ **Inactivité configurable** : Temps sans écriture = complet (default: 2 min)
- ✅ **Nommage personnalisé** : `-AuditName` pour identifier vos audits
- ✅ **Event Viewer amélioré** : Critical/Error/Warning + Winget/DesktopAppInstaller
- ✅ **Rapports nommés** : `AuditName_YYYYMMDD_HHMMSS.json/html`

**Métriques surveillées** :
- **Performance** : CPU, RAM, Disk I/O en temps réel
- **Processus** : Création/terminaison, PID, chemins d'exécution
- **Applications** : Installations/désinstallations détectées automatiquement
- **Registre** : Surveillance des clés critiques (optionnel)
- **Fichiers** : Activité système de fichiers (optionnel)
- **Réseau** : Connexions actives, statistiques par processus
- **Event Viewer** : Critical/Error/Warning (Application + System)
- **Installations** : MSI Installer + Winget/DesktopAppInstaller
- **Anomalies** : Détection automatique (CPU >90%, RAM >90%, Disk I/O >100MB/s)

**Rapports générés** :
- `AuditReports/audit_YYYYMMDD_HHMMSS.json` - Données complètes
- `AuditReports/audit_YYYYMMDD_HHMMSS.html` - Rapport visuel (avec -GenerateReport)

**Affichage temps réel** :
```
[20:15:32] Starting system audit...
[Performance] CPU: 45% | RAM: 62% (8.3GB) | Processes: 187
[20:15:34] New process: winget.exe (PID: 12345)
[20:15:45] Application installed: Recuva 1.53.2083
[20:15:50] ALERT: High CPU usage at 92%
```

**Utilité** :
- Surveiller les déploiements Win11Forge en parallèle
- Diagnostiquer les problèmes d'installation
- Analyser l'impact performance des applications
- Détecter les anomalies système
- Audit de conformité et sécurité

---

## 📊 Fichiers de Support

| Fichier | Description |
|---------|-------------|
| `applications-data.js` | Base de données JS pour ProfileCreator (66 apps) |
| `ProfileCreator_Features.md` | Documentation des fonctionnalités du ProfileCreator |
| `StartupManager.html` | Interface de gestion du démarrage automatique |
| `Launch-TrustedInstallerGUI.ps1` | Script PowerShell pour TrustedInstaller launcher |
| `Launch-SystemAudit.bat` | Lanceur interactif pour System-Audit avec menu guidé |
| `System-Audit-README.md` | Documentation complète de System-Audit (30+ pages) |

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

**Launch-SystemAudit.bat** :
- Lancement rapide de System-Audit avec menu guidé
- Mode débutant sans ligne de commande
- Tous les modes pré-configurés (Win11Forge, Process, Log, etc.)

**System-Audit.ps1** (v2.1.0 - UNIVERSEL) :
- Surveiller N'IMPORTE QUEL script, processus ou déploiement
- Analyser l'impact performance de toute opération
- Diagnostiquer des problèmes d'installation ou d'exécution
- Audit de sécurité et conformité
- Monitoring automatique avec arrêt intelligent
- Rapports détaillés (JSON + HTML)

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
