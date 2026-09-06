# Base d’applications WinForge

[English](README.md)

`Database/applications.json` est le catalogue de référence du framework et de l’interface. Les profils utilisent des identifiants stables pour éviter de répéter les informations d’installation.

## Format

La racine contient `DatabaseVersion`, `LastUpdated`, `TotalApplications`, `Applications`, `Categories` et `Tags`. Consultez le fichier et `Get-DatabaseStatistics` pour connaître les valeurs actuelles.

| Champ | Rôle |
|---|---|
| `Name`, `Description`, `Category` | Métadonnées affichées |
| `Sources.Winget`, `Sources.Chocolatey`, `Sources.Store`, `Sources.DirectUrl` | Sources d’installation |
| `Detection` | Détection Registry, File, Command, StoreApp ou WindowsFeature |
| `DefaultPriority`, `DefaultRequired` | Paramètres de déploiement par défaut |
| `EnvironmentRestrictions` | Environnements exclus |
| `InstallMethod`, `InstallArguments` | Surcharges facultatives de l’installation |
| `Tags`, `Homepage`, `Notes` | Recherche et maintenance |
| `LastVerified`, `Verified` | Date et état de vérification enregistrés |

Le schéma `Schemas/applications-database.schema.json` définit la structure et les contraintes. Un téléchargement direct doit comporter les métadonnées de vérification requises par le validateur. La détection par commande est limitée par `Config/detection-allowlist.json` ; une commande arbitraire ne constitue pas une stratégie de détection valide.

## Interface PowerShell

```powershell
Import-Module .\Modules\ApplicationDatabase.psm1 -Force
Get-ApplicationDatabase
Get-ApplicationById -AppId 'GoogleChrome'
Get-AllApplications -Category 'Development'
Get-AllApplications -Tag 'open-source'
Search-Applications -SearchTerm 'chrome'
Get-ApplicationCategories
Get-ApplicationTags
Get-DatabaseStatistics
Reset-DatabaseCache
```

`ConvertTo-ProfileApplication` convertit une application du catalogue en entrée de profil avec surcharges. `Test-ApplicationSources` contrôle les sources d’installation.

## Profils

Un profil peut référencer des identifiants :

```json
{
  "Name": "Development",
  "Description": "Development tools",
  "Version": "1.0.0",
  "Inherits": ["Base"],
  "Applications": ["VSCode", "Git"]
}
```

Utilisez un objet avec `AppId` pour modifier la priorité ou le caractère obligatoire d’une application. Le chargeur accepte aussi les anciennes définitions intégrées au profil. Conservez les applications héritées dans leur parent ; une mise à jour du profil enfant enregistre sa sélection directe. Les mises à jour dans l’interface préservent `SystemConfig` et les propriétés personnalisées non modifiées.

## Maintenance

1. Ajouter ou modifier l’entrée dans `Database/applications.json`.
2. Conserver les identifiants et vérifier les métadonnées, catégories et étiquettes.
3. Valider le JSON et les sources d’installation.
4. Renseigner `LastVerified` et `Verified` uniquement après vérification.
5. Revalider les profils qui référencent cette entrée.

```powershell
.\Tools\Validate-AppDatabase.ps1
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

La deuxième commande contacte les sources externes. Les rapports décrivent cette exécution, sans garantir la disponibilité future des paquets.

## Dépannage

Pour une application absente, vérifiez son identifiant exact et rechargez le cache. Pour un JSON mal formé, utilisez `ConvertFrom-Json` et le validateur de schéma. Pour un paquet indisponible, vérifiez son identifiant de source et la sortie du gestionnaire.

Consultez le [démarrage rapide](QUICK_START.fr.md), les [outils](../Tools/README.fr.md) et la [documentation des profils](../Docs/ARCHITECTURE.fr.md).
