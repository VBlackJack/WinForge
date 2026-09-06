# Outils WinForge

[English](README.md)

Exécutez les commandes PowerShell depuis la racine du dépôt. Les paramètres sont documentés par `Get-Help <script> -Full`.

## Profils et démarrage

| Outil | Rôle |
|---|---|
| `ProfileCreator.html` | Assistant local dans le navigateur : informations du profil, héritage, applications intégrées, sources personnalisées, configuration système et export JSON |
| `applications-data.js` | Catalogue de l’assistant autonome, distinct du catalogue d’exécution |
| `Launch-StartupManager.ps1`, `StartupManager.html` | Consultation du démarrage et export d’une liste pour `Config/startup-blacklist.json` |
| `Launch-AsTrustedInstaller.bat`, `Launch-TrustedInstallerGUI.ps1` | Lancement d’outils d’administration avec les privilèges TrustedInstaller après confirmation explicite |

Le lanceur TrustedInstaller nécessite `NtObjectManager`. Vérifiez l’opération demandée avant l’élévation. Consultez le [guide de création de profils](ProfileCreator_Features.fr.md).

## Dépendances de compilation

```powershell
.\Tools\Resolve-ThemeForge.ps1
dotnet build GUI\WinForge.slnx -c Release
```

Le script crée `ThemeForge/` dans ce dépôt, au commit exact enregistré dans `Config/build-dependencies.json`. La CI lit le même fichier. Un dossier existant modifié ou positionné sur une autre révision est refusé sans réinitialisation. Le dépôt voisin reste intact. Pour utiliser volontairement une autre source de développement, passez `-p:ThemeForgeRoot=<path>` à MSBuild ; cette compilation sort du périmètre de dépendance fixé.

## Validation

```powershell
.\Tools\Validate-Framework.ps1 -Detailed -Offline
.\Tools\Invoke-PSScriptAnalyzer.ps1
.\Tools\Verify-VersionConsistency.ps1
.\Tools\Validate-AppDatabase.ps1
.\Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
```

Le mode hors ligne vérifie la structure du dépôt, les modules, les profils, les schémas et les prérequis détectables localement. Il omet les contrôles réseau et les essais d’écriture sur l’hôte. Retirez `-Offline` pour contrôler une machine de déploiement. La CI ne dépend pas de la disponibilité d’ICMP.

Pour contrôler les sources de paquets distantes :

```powershell
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

`Search-ApplicationSources.ps1` aide à rechercher les identifiants des paquets. Son aide décrit les filtres de sources.

## Vérification du paquet

```powershell
.\Tools\Test-ReleasePackage.ps1 -ArchivePath '<release.zip>'
```

Ce contrôle compare les schémas et documents d’entrée requis avec ce dépôt, sans exécuter l’archive. `Build-Release.ps1` inclut `Schemas/`, les README et journaux de modifications anglais et français, puis applique ce contrôle avant de produire la somme de contrôle. La création du paquet modifie les métadonnées de version ; utilisez volontairement le processus de publication.

## Contrôles de l’interface

```powershell
.\Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot '<path-to-winsight>'
```

Ce contrôle facultatif compile et lance l’application réelle, pilote la navigation et enregistre les captures dans `TestResults/winsight`. Il nécessite un bureau Windows interactif et un dépôt WinSight. Consultez la [recette visuelle](../Docs/GUI_VM_VISUAL_CHECKLIST.fr.md) et les [tests UIA](../GUI/WinForge.GUI.UITests/README.fr.md).

## Surveillance du système

`Launch-SystemAudit.bat` démarre l’outil de façon interactive. L’interface PowerShell permet l’observation par processus, journal ou durée :

```powershell
.\Tools\System-Audit.ps1 -MonitorProcessName 'msiexec' -GenerateReport -AuditName 'ManualInstall'
.\Tools\System-Audit.ps1 -MonitorLogPath '.\Logs' -GenerateReport
.\Tools\System-Audit.ps1 -Duration 30 -SampleInterval 5 -GenerateReport
```

La [référence de l’audit système](System-Audit-README.fr.md) décrit les conditions d’arrêt, les rapports, les options et leurs limites. Les opérations d’installation sont distinctes de ces commandes de surveillance.
