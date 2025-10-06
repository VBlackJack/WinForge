# System-Audit.ps1 v2.4.0 - Universal System Monitoring Tool

**Outil d'audit système universel pour surveiller n'importe quel script, processus ou déploiement**

---

## 📋 Table des Matières

- [Vue d'ensemble](#vue-densemble)
- [Installation](#installation)
- [Modes de Surveillance](#modes-de-surveillance)
- [Exemples d'Utilisation](#exemples-dutilisation)
- [Paramètres](#paramètres)
- [Métriques Surveillées](#métriques-surveillées)
- [Rapports Générés](#rapports-générés)
- [Cas d'Usage](#cas-dusage)
- [Dépannage](#dépannage)

---

## 🎯 Vue d'ensemble

System-Audit.ps1 est un outil de monitoring système complet qui peut surveiller **n'importe quel processus, script ou déploiement** et collecter automatiquement :

- Performance en temps réel (CPU, RAM, Disk I/O)
- Processus créés/terminés
- Applications installées/désinstallées
- Événements Windows (Critical, Error, Warning)
- Activité réseau
- Anomalies système

**L'outil s'arrête automatiquement** quand le processus/script surveillé se termine et génère des rapports détaillés (JSON + HTML).

---

## 📦 Installation

Aucune installation requise ! Le script est standalone.

**Prérequis** :
- Windows 10/11
- PowerShell 5.1+ (inclus par défaut)
- Droits Administrateur (recommandé pour accès complet aux événements)

**Emplacement** :
```
Win11Forge\Tools\System-Audit.ps1
```

---

## 🔍 Modes de Surveillance

System-Audit.ps1 offre **4 modes de surveillance automatique** :

### 1. Monitor par Process ID (PID)

Surveille un processus spécifique par son PID et s'arrête quand il se termine.

```powershell
# Lancer un script et surveiller son PID
$proc = Start-Process powershell -ArgumentList "-File", "MonScript.ps1" -PassThru
.\Tools\System-Audit.ps1 -MonitorProcessId $proc.Id -AuditName "MonScript" -GenerateReport
```

**Quand utiliser** :
- Vous lancez un script/processus et voulez le surveiller spécifiquement
- Vous connaissez le PID exact du processus

---

### 2. Monitor par Process Name

Surveille un processus par son nom (ex: "powershell", "installer", "winget").

```powershell
# Surveiller tous les processus PowerShell
.\Tools\System-Audit.ps1 -MonitorProcessName "powershell" -AuditName "PowerShellAudit" -GenerateReport

# Surveiller un installer
.\Tools\System-Audit.ps1 -MonitorProcessName "installer" -GenerateReport
```

**Quand utiliser** :
- Vous voulez surveiller un type de processus sans connaître son PID
- Plusieurs instances peuvent exister (surveille la première trouvée)

---

### 3. Monitor par Log File

Surveille un fichier log spécifique et détecte sa complétion.

```powershell
# Surveiller un log d'installation
.\Tools\System-Audit.ps1 -MonitorLogFile "C:\Logs\install.log" `
    -LogCompletionMarkers "SUCCESS|COMPLETED|DONE" `
    -GenerateReport

# Surveiller avec inactivité personnalisée (5 minutes)
.\Tools\System-Audit.ps1 -MonitorLogFile "C:\Backup\backup.log" `
    -LogInactivityMinutes 5 `
    -GenerateReport
```

**Quand utiliser** :
- Vous avez un fichier log existant à surveiller
- Vous connaissez les marqueurs de complétion du log
- Le processus écrit dans un log connu

**Critères d'arrêt** :
1. Détection d'un marqueur de complétion (regex)
2. OU inactivité du fichier (pas d'écriture pendant N minutes)

---

### 4. Monitor par Log Directory

Surveille un répertoire, détecte le dernier log créé et surveille sa complétion.

```powershell
# Surveiller les logs Win11Forge
.\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" `
    -LogCompletionMarkers "Deployment completed|Summary" `
    -GenerateReport

# Surveiller avec marqueurs personnalisés
.\Tools\System-Audit.ps1 -MonitorLogPath "C:\AppLogs" `
    -LogCompletionMarkers "FINISHED|EXIT|TERMINATED" `
    -LogInactivityMinutes 3 `
    -GenerateReport
```

**Quand utiliser** :
- Le script/processus crée un nouveau log à chaque exécution
- Vous ne connaissez pas le nom exact du log à l'avance
- Surveillance de déploiements automatisés (Win11Forge, etc.)

**Comportement** :
1. Attend qu'un nouveau log soit créé dans le répertoire
2. Surveille ce log en temps réel
3. S'arrête quand marqueur détecté OU inactivité

---

### 5. Audit Temporisé (sans auto-stop)

Audit pendant une durée fixe sans surveillance de processus/log.

```powershell
# Audit de 30 minutes
.\Tools\System-Audit.ps1 -Duration 30 -AuditName "PerformanceTest" -GenerateReport

# Audit illimité (s'arrête manuellement avec Ctrl+C)
.\Tools\System-Audit.ps1 -Duration 0 -AuditName "LongTermMonitoring"
```

**Quand utiliser** :
- Tests de performance
- Surveillance système générale
- Debugging sans processus spécifique

---

## 💡 Exemples d'Utilisation

### Exemple 1 : Surveiller un Déploiement Win11Forge

```powershell
# Lancer l'audit AVANT le déploiement
Start-Process powershell -ArgumentList "-NoExit", "-File", ".\Tools\System-Audit.ps1", `
    "-MonitorLogPath", ".\Logs", `
    "-LogCompletionMarkers", "Deployment completed|Summary", `
    "-GenerateReport", `
    "-AuditName", "Win11ForgeDeployment"

# Puis lancer le déploiement
.\Deploy-Win11Environment.ps1 -ProfileName Personnel
```

**Résultat** :
- L'audit détecte automatiquement le nouveau log
- Surveille en temps réel
- S'arrête quand "Deployment completed" apparaît
- Génère `Win11ForgeDeployment_20251002_220530.json` et `.html`

---

### Exemple 2 : Auditer une Installation Manuelle

```powershell
# Surveiller un installer par son nom
.\Tools\System-Audit.ps1 -MonitorProcessName "msiexec" `
    -AuditName "ManualInstall" `
    -GenerateReport
```

**Résultat** :
- Détecte quand msiexec démarre
- Surveille pendant toute l'installation
- S'arrête quand msiexec se termine
- Capture toutes les applications installées

---

### Exemple 3 : Surveiller un Script de Backup

```powershell
# Lancer le backup et capturer son PID
$backup = Start-Process powershell -ArgumentList "-File", ".\Backup-System.ps1" -PassThru

# Surveiller le backup
.\Tools\System-Audit.ps1 -MonitorProcessId $backup.Id `
    -AuditName "SystemBackup" `
    -SampleInterval 5 `
    -GenerateReport
```

**Résultat** :
- Monitoring du backup de A à Z
- Collecte CPU/RAM/Disk I/O toutes les 5 secondes
- Rapport complet avec statistiques

---

### Exemple 4 : Audit Complet avec Toutes les Options

```powershell
.\Tools\System-Audit.ps1 -MonitorProcessName "installer" `
    -MonitorRegistry `
    -MonitorFileSystem `
    -SampleInterval 2 `
    -GenerateReport `
    -AuditName "CompleteAudit" `
    -LogCompletionMarkers "DONE|SUCCESS|COMPLETED" `
    -LogInactivityMinutes 3
```

**Inclut** :
- Surveillance processus "installer"
- Monitoring registre (clés critiques)
- Monitoring système de fichiers
- Échantillonnage toutes les 2 secondes
- Rapport HTML + JSON

---

## ⚙️ Paramètres

### Paramètres Généraux

| Paramètre | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Duration` | int | 60 | Durée max en minutes (0 = illimité) |
| `-OutputPath` | string | `.\AuditReports` | Chemin pour les rapports |
| `-SampleInterval` | int | 2 | Intervalle d'échantillonnage (secondes) |
| `-GenerateReport` | switch | false | Générer rapport HTML en plus du JSON |
| `-RealTimeDisplay` | switch | true | Affichage temps réel dans la console |
| `-AuditName` | string | "SystemAudit" | Nom personnalisé pour l'audit |

### Paramètres de Surveillance (Auto-Stop)

| Paramètre | Type | Default | Description |
|-----------|------|---------|-------------|
| `-MonitorProcessName` | string | null | Nom du processus à surveiller |
| `-MonitorProcessId` | int | 0 | PID du processus à surveiller |
| `-MonitorLogFile` | string | null | Fichier log à surveiller |
| `-MonitorLogPath` | string | null | Répertoire de logs à surveiller |
| `-LogCompletionMarkers` | string | "completed\|finished\|Summary" | Regex pour détecter la fin |
| `-LogInactivityMinutes` | int | 2 | Minutes d'inactivité = complet |

### Paramètres de Surveillance Avancée

| Paramètre | Type | Default | Description |
|-----------|------|---------|-------------|
| `-MonitorRegistry` | switch | false | Surveiller modifications registre |
| `-MonitorFileSystem` | switch | false | Surveiller activité fichiers |

**⚠️ Note** : Les options Registry et FileSystem ont un overhead significatif. Utilisez uniquement si nécessaire.

---

## 📊 Métriques Surveillées

### Performance (toutes les N secondes)

- **CPU** : Utilisation processeur (%)
- **RAM** : Utilisation mémoire (% et GB)
- **Disk I/O** : Lectures/écritures (bytes/sec)
- **Processus** : Nombre total de processus actifs

**Alertes automatiques** :
- CPU > 90% → Warning
- RAM > 90% → Warning
- Disk I/O > 100 MB/s → Info

---

### Processus

- **Créés** : Tous les processus démarrés pendant l'audit
  - Nom, PID, StartTime, Path, WorkingSet
- **Terminés** : Tous les processus arrêtés
- **Capture** : Nom, PID, chemin d'exécution

---

### Applications

Détection automatique via :
- Registre (`HKLM/HKCU\...\Uninstall`)
- Packages Windows (`Get-AppxPackage`)

**Tracked** :
- Installations détectées
- Désinstallations détectées
- Nom, version, publisher, date

---

### Event Viewer (Enhanced)

**Event Logs surveillés** :
- Application (Critical, Error, Warning)
- System (Critical, Error, Warning)
- MSI Installer (tous événements)
- DesktopAppInstaller / Winget (tous événements)

**Affichage temps réel** :
- Événements **Critical** → Affichés immédiatement en rouge
- > 5 erreurs → Alerte warning

---

### Réseau

- **Connexions actives** : TCP established par processus
- **Statistiques** : Bytes sent/received par interface

---

### Registre (Optionnel)

Surveillance clés critiques :
- `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`
- `HKCU:\Software\Microsoft\Windows\CurrentVersion\Run`
- `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`

---

## 📄 Rapports Générés

### Fichier JSON (`{AuditName}_{timestamp}.json`)

**Toujours généré** - Contient toutes les données brutes :

```json
{
  "StartTime": "2025-10-02T22:15:30",
  "EndTime": "2025-10-02T22:45:12",
  "Duration": 30,
  "SystemInfo": { ... },
  "Performance": {
    "Samples": [ ... ],
    "Alerts": [ ... ]
  },
  "Processes": {
    "Created": [ ... ],
    "Terminated": [ ... ]
  },
  "Applications": {
    "Installed": [ ... ],
    "Uninstalled": [ ... ]
  },
  "Events": {
    "Errors": [ ... ],
    "Warnings": [ ... ],
    "Installations": [ ... ]
  },
  "Anomalies": [ ... ]
}
```

**Utilisation** :
- Analyse programmatique
- Import dans autres outils
- Parsing personnalisé

---

### Fichier HTML (`{AuditName}_{timestamp}.html`)

**Généré avec `-GenerateReport`** - Rapport visuel interactif :

**Sections** :
1. **Résumé** : Stats globales (samples, processus, apps, anomalies)
2. **Informations Système** : OS, CPU, RAM, PowerShell version
3. **Performance** : CPU/RAM moyens
4. **Processus Créés** : Top 50 avec timestamps
5. **Applications Installées** : Nom, version, publisher
6. **Anomalies & Alertes** : Événements critiques et warnings

**Style** :
- Moderne, responsive
- Couleurs Win11Forge (gradient violet-bleu)
- Tableaux triables
- Badges colorés pour statuts

---

### Emplacement des Rapports

```
Win11Forge\
└── AuditReports\
    ├── Win11ForgeDeployment_20251002_220530.json
    ├── Win11ForgeDeployment_20251002_220530.html
    ├── MonScript_20251002_183015.json
    ├── MonScript_20251002_183015.html
    └── ...
```

---

## 🎯 Cas d'Usage

### 1. Déploiement d'Applications

**Problème** : Vous déployez des applications et voulez savoir ce qui se passe vraiment.

**Solution** :
```powershell
.\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" -GenerateReport
```

**Bénéfices** :
- Capture toutes les installations
- Détecte les erreurs Event Viewer
- Mesure l'impact performance
- Rapport détaillé pour debugging

---

### 2. Debugging Script PowerShell

**Problème** : Votre script est lent ou consomme beaucoup de ressources.

**Solution** :
```powershell
$script = Start-Process pwsh -ArgumentList "-File", "MonScript.ps1" -PassThru
.\Tools\System-Audit.ps1 -MonitorProcessId $script.Id -AuditName "ScriptDebug"
```

**Bénéfices** :
- Profiling CPU/RAM/Disk en temps réel
- Détection pics de consommation
- Processus enfants créés
- Timeline complète

---

### 3. Audit de Sécurité

**Problème** : Vous voulez auditer les modifications système pendant une opération.

**Solution** :
```powershell
.\Tools\System-Audit.ps1 -MonitorProcessName "installer" `
    -MonitorRegistry `
    -Duration 60 `
    -GenerateReport `
    -AuditName "SecurityAudit"
```

**Bénéfices** :
- Modifications registre capturées
- Processus suspects détectés
- Événements Critical/Error
- Rapport d'audit complet

---

### 4. Tests de Performance

**Problème** : Vous testez la performance système sous charge.

**Solution** :
```powershell
.\Tools\System-Audit.ps1 -Duration 30 `
    -SampleInterval 1 `
    -AuditName "LoadTest" `
    -GenerateReport
```

**Bénéfices** :
- Échantillonnage rapide (1s)
- Détection anomalies automatique
- Graphiques de performance
- Statistiques min/max/avg

---

### 5. Monitoring Backup/Maintenance

**Problème** : Vos scripts de backup/maintenance sont longs et vous voulez les surveiller.

**Solution** :
```powershell
.\Tools\System-Audit.ps1 -MonitorLogFile "C:\Backup\backup.log" `
    -LogCompletionMarkers "BACKUP COMPLETED|SUCCESS" `
    -LogInactivityMinutes 5 `
    -GenerateReport
```

**Bénéfices** :
- Monitoring automatique sans intervention
- Arrêt intelligent quand backup terminé
- Vérification succès via marqueurs
- Rapport pour historique

---

## 🔧 Dépannage

### L'audit ne démarre pas

**Erreur** : "Cannot run scripts on this system"

**Solution** :
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

---

### L'audit ne s'arrête pas automatiquement

**Cause 1** : Le marqueur de complétion n'est pas détecté

**Solution** : Vérifiez votre regex `-LogCompletionMarkers`
```powershell
# Tester le marqueur
Get-Content "C:\Logs\monlog.log" -Tail 20 | Select-String "completed|finished"
```

**Cause 2** : Le log est toujours actif

**Solution** : Ajustez `-LogInactivityMinutes`
```powershell
-LogInactivityMinutes 5  # Au lieu de 2
```

---

### Le processus n'est pas détecté

**Cause** : Le processus démarre après l'audit

**Solution** : L'audit attend jusqu'à ce que le processus apparaisse
```powershell
# Normal - l'audit attend le processus
.\Tools\System-Audit.ps1 -MonitorProcessName "installer"
# Message: "Waiting for process 'installer' to start..."
```

---

### Permissions insuffisantes

**Erreur** : "Access denied" sur Event Viewer

**Solution** : Lancez PowerShell en Administrateur
```powershell
# Clic droit → "Run as Administrator"
```

---

### Rapport HTML ne s'affiche pas correctement

**Cause** : Navigateur bloque le fichier local

**Solution** :
1. Clic droit sur le fichier .html
2. Propriétés → Débloquer
3. OU copiez dans `C:\Temp\` et ouvrez

---

## 📚 Ressources

### Documentation

- **README principal** : `Win11Forge\Tools\README.md`
- **Ce fichier** : `Win11Forge\Tools\System-Audit-README.md`
- **Code source** : `Win11Forge\Tools\System-Audit.ps1`

### Aide Inline

```powershell
Get-Help .\Tools\System-Audit.ps1 -Full
Get-Help .\Tools\System-Audit.ps1 -Examples
```

### Support

- **Issues** : Win11Forge GitHub Issues
- **Documentation** : Win11Forge Wiki

---

## 📝 Changelog

### v2.0.0 (2025-10-02)
- ✅ **BREAKING** : Outil maintenant 100% universel
- ✅ Ajout `-MonitorProcessName` / `-MonitorProcessId`
- ✅ Ajout `-MonitorLogFile` / `-MonitorLogPath`
- ✅ Ajout `-AuditName` pour rapports nommés
- ✅ Ajout `-LogCompletionMarkers` regex personnalisable
- ✅ Ajout `-LogInactivityMinutes` configurable
- ✅ Event Viewer amélioré (Critical/Error/Warning)
- ✅ Support Winget/DesktopAppInstaller events
- ✅ Alertes temps réel pour événements critiques
- ✅ Rapports nommés `{AuditName}_{timestamp}`

### v1.1.0 (2025-10-02)
- ✅ Arrêt automatique pour Win11Forge
- ✅ Event Viewer enhanced
- ✅ Détection événements MSI Installer

### v1.0.0 (2025-10-01)
- 🎉 Release initiale
- Monitoring Win11Forge uniquement

---

## 💡 Conseils & Bonnes Pratiques

### 1. Nommez vos Audits

**❌ Mauvais** :
```powershell
.\Tools\System-Audit.ps1 -GenerateReport
# Rapport: SystemAudit_20251002_220530.html
```

**✅ Bon** :
```powershell
.\Tools\System-Audit.ps1 -AuditName "Win11Forge_Personnel_VM01" -GenerateReport
# Rapport: Win11Forge_Personnel_VM01_20251002_220530.html
```

---

### 2. Utilisez `-GenerateReport` pour Analyses

Le rapport HTML est parfait pour :
- Présentation aux autres
- Archivage visuel
- Analyse rapide

Le JSON est parfait pour :
- Parsing programmatique
- Import dans outils d'analyse
- Traitement automatisé

---

### 3. Ajustez `-SampleInterval` selon les Besoins

| Cas | Interval | Raison |
|-----|----------|--------|
| Production | 5-10s | Moins d'overhead |
| Debugging | 2s | Default, bon équilibre |
| Performance Testing | 1s | Maximum de détails |
| Long-term | 30-60s | Minimal overhead |

---

### 4. Marqueurs de Complétion Spécifiques

**❌ Trop général** :
```powershell
-LogCompletionMarkers "done"  # Peut matcher trop tôt
```

**✅ Spécifique** :
```powershell
-LogCompletionMarkers "Deployment completed successfully|=== Summary ==="
```

---

### 5. Monitoring en Arrière-Plan

Pour lancer sans bloquer votre console :

```powershell
Start-Process powershell -ArgumentList `
    "-NoExit", `
    "-File", ".\Tools\System-Audit.ps1", `
    "-MonitorLogPath", ".\Logs", `
    "-GenerateReport"
```

---

## 🏆 Exemples Avancés

### Exemple 1 : Pipeline d'Installation

```powershell
# Script: Install-Pipeline.ps1
# Installe plusieurs applications et génère un audit complet

$audit = Start-Process powershell -ArgumentList `
    "-NoExit", `
    "-ExecutionPolicy", "Bypass", `
    "-File", ".\Tools\System-Audit.ps1", `
    "-MonitorLogPath", ".\InstallLogs", `
    "-LogCompletionMarkers", "PIPELINE COMPLETED", `
    "-GenerateReport", `
    "-AuditName", "InstallationPipeline"

# Installer applications
winget install --id Git.Git
winget install --id Microsoft.VisualStudioCode
choco install notepadplusplus -y

# Marquer la fin
"PIPELINE COMPLETED" | Out-File ".\InstallLogs\pipeline.log" -Append
```

---

### Exemple 2 : Audit Comparatif

```powershell
# Auditer la même opération avec/sans optimisations

# Version 1 : Sans optimisation
.\Tools\System-Audit.ps1 -MonitorProcessName "MonApp" `
    -AuditName "MonApp_NoOptim" -GenerateReport

# Version 2 : Avec optimisation
.\Tools\System-Audit.ps1 -MonitorProcessName "MonApp" `
    -AuditName "MonApp_WithOptim" -GenerateReport

# Comparer les JSON pour voir l'impact
```

---

### Exemple 3 : Monitoring Multi-Process

```powershell
# Surveiller plusieurs processus simultanément

# Audit 1 : winget
Start-Process powershell -ArgumentList `
    "-File", ".\Tools\System-Audit.ps1", `
    "-MonitorProcessName", "winget", `
    "-AuditName", "Winget_Activity"

# Audit 2 : choco
Start-Process powershell -ArgumentList `
    "-File", ".\Tools\System-Audit.ps1", `
    "-MonitorProcessName", "choco", `
    "-AuditName", "Choco_Activity"

# Les 2 audits tournent en parallèle
```

---

## 📧 Contact & Contribution

**Auteur** : Julien Bombled
**Projet** : Win11Forge
**Version** : 2.1.0
**Date** : 2025-10-02

---

**🎉 Profitez de System-Audit.ps1 pour surveiller tout ce que vous voulez !**
