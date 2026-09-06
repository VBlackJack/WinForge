# Base d’applications : démarrage rapide

[English](QUICK_START.md)

Exécutez ces commandes depuis la racine du dépôt.

## Charger et interroger le catalogue

```powershell
Import-Module .\Modules\ApplicationDatabase.psm1 -Force
Get-DatabaseStatistics
Search-Applications -SearchTerm 'chrome'
Get-AllApplications -Category 'Browser'
Get-AllApplications -Tag 'essential'
Get-AllApplications -Verified
Get-ApplicationById -AppId 'GoogleChrome'
Get-ApplicationCategories | Format-Table CategoryId, DisplayName, Count
Get-ApplicationTags
```

Les statistiques retournées donnent la taille actuelle du catalogue et le nombre d’entrées vérifiées. Les exemples chiffrés des anciennes versions ne décrivent pas la base actuelle.

## Créer un profil

Sélectionnez les identifiants du catalogue ; la définition de chaque application reste dans la base :

```powershell
$deploymentProfile = @{
    Name = 'MyDevProfile'
    Description = 'Development tools'
    Version = '1.0.0'
    Applications = @('VSCode', 'Git')
}
$deploymentProfile | ConvertTo-Json -Depth 10 |
    Set-Content -LiteralPath 'Profiles/MyDevProfile.json' -Encoding UTF8
```

Relisez le JSON avant tout déploiement. Choisissez un nom de fichier distinct pour préserver les profils existants.

## Valider et exporter

```powershell
.\Tools\Validate-AppDatabase.ps1
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
Get-AllApplications |
    Select-Object Name, Category, @{N='Winget'; E={$_.Sources.Winget}}, Verified |
    Export-Csv -LiteralPath 'AppDatabase.csv' -NoTypeInformation -Encoding UTF8
```

La validation des sources nécessite les gestionnaires de paquets concernés et un accès réseau. Le filtrage du catalogue n’installe rien.

## Dépannage

Vérifiez la présence de `Apps/Database/applications.json` et sa lecture avec `ConvertFrom-Json`. Après modification, appelez `Reset-DatabaseCache`, puis rechargez la base.

Consultez la [référence du catalogue](README.fr.md), le [journal des modifications](../CHANGELOG.fr.md) et `Get-Help .\Tools\Validate-AppDatabase.ps1 -Full`.
