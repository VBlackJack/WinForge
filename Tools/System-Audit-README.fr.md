# Audit du système

[English](System-Audit-README.md)

`Tools/System-Audit.ps1` observe l’activité du système pendant un déploiement, une maintenance ou une mesure à durée fixe. Il collecte des échantillons de performance, des changements de processus et d’applications, des événements et des informations réseau. Il ne démarre pas l’opération observée.

## Démarrage et arrêt

```powershell
.\Tools\System-Audit.ps1 -MonitorProcessId 1234 -AuditName 'ProcessAudit' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorProcessName 'msiexec' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorLogFile 'C:\Logs\install.log' -LogCompletionMarkers 'SUCCESS|COMPLETED' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorLogPath '.\Logs' -GenerateReport
.\Tools\System-Audit.ps1 -Duration 30 -SampleInterval 5 -GenerateReport
```

Remplacez les exemples de PID et de chemins. La surveillance par nom attend un processus correspondant et suit l’instance sélectionnée ; elle ne couvre pas une transaction englobant tous les processus homonymes. La surveillance d’un journal s’arrête sur un marqueur ou après la période d’inactivité configurée. Un marqueur ou une inactivité ne prouve pas la réussite de l’installation.

`-Duration 0` supprime la limite de durée. Arrêtez une session manuelle avec Ctrl+C. `Launch-SystemAudit.bat` fournit un lanceur interactif.

## Paramètres

| Paramètre | Valeur par défaut | Signification |
|---|---|---|
| `Duration` | 60 | Durée maximale en minutes ; 0 signifie illimitée |
| `OutputPath` | `./AuditReports` | Dossier des rapports |
| `SampleInterval` | 5 | Secondes entre deux échantillons |
| `GenerateReport` | Désactivé | Produire du HTML en plus du JSON |
| `RealTimeDisplay` | Activé sauf Quiet | État dans la console |
| `Quiet` | Désactivé | Masquer l’affichage continu |
| `SkipApplicationMonitoring` | Désactivé | Omettre la comparaison des inventaires applicatifs |
| `AuditName` | `SystemAudit` | Préfixe des rapports |
| `MonitorProcessName`, `MonitorProcessId` | Non renseignés | Processus à observer |
| `MonitorLogFile`, `MonitorLogPath` | Non renseignés | Journal ou dossier à observer |
| `LogCompletionMarkers` | `completed\|finished\|Summary` | Expression régulière de fin |
| `LogInactivityMinutes` | 2 | Seuil d’inactivité avant arrêt |
| `MonitorRegistry` | Désactivé | Observer certaines zones du registre |

`MonitorFileSystem` reste accepté par compatibilité, mais le script actuel ne possède pas de collecteur de changements de fichiers. Sa présence ne prouve pas une surveillance des fichiers.

## Données et rapports

Les rapports JSON contiennent les horodatages, les informations système, les mesures et alertes de performance, les changements de processus et d’applications, les événements et les anomalies. Avec `-GenerateReport`, une synthèse HTML est écrite à côté du JSON sous le nom `{AuditName}_{timestamp}.html`.

La surveillance du registre couvre certaines zones de démarrage et de désinstallation, pas toutes les clés. Les inventaires et événements sont échantillonnés ; des changements brefs peuvent échapper à la collecte. Certains journaux exigent des droits administrateur. Les mesures CPU, mémoire et disque peuvent inclure des processus sans rapport avec l’opération.

## Utilisation

Choisissez un nom distinct, démarrez la surveillance avant l’opération et conservez son résultat et ses journaux avec le rapport. Augmentez `SampleInterval` pour réduire le coût des longues observations. Choisissez un marqueur précis pour éviter une correspondance accidentelle.

Pour comparer les performances, conservez une durée, un intervalle, une configuration et une charge comparables. Pour plusieurs processus indépendants, utilisez des sessions séparées avec des noms distincts.

## Dépannage

- Processus introuvable : vérifier son nom, son PID et son démarrage.
- Absence d’arrêt automatique : tester l’expression régulière sur le journal réel et vérifier le seuil d’inactivité.
- Événements manquants : vérifier les droits sur les journaux Windows concernés.
- HTML absent : ajouter `-GenerateReport` et vérifier `OutputPath`.
- Script bloqué : consulter la stratégie d’exécution et suivre la procédure de signature ou d’exécution approuvée par votre organisation.

```powershell
Get-Help .\Tools\System-Audit.ps1 -Full
Get-Help .\Tools\System-Audit.ps1 -Examples
```

Consultez l’[index des outils](README.fr.md).
