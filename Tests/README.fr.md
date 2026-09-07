# Tests WinForge

[English](README.md)

## Périmètre

Ce dossier contient les tests PowerShell/Pester. Les tests unitaires de l’interface sont dans `GUI/WinForge.GUI.Tests`. Les [tests UIA](../GUI/WinForge.GUI.UITests/README.fr.md) sont facultatifs.

Mesure du 2026-09-06 : 1978 tests Pester, 1972 réussis, 0 échec et 6 ignorés. La version du framework est `2026090702`, définie dans `Config/version.json`. Les résultats habituels sont écrits dans `Tests/Results/`. Ces nombres décrivent une exécution, pas une contrainte sur le nombre futur de tests.

## Installation

```powershell
.\Tests\Install-Pester.ps1
```

Ou :

```powershell
Install-Module -Name Pester -RequiredVersion 5.7.1 -Force -Scope CurrentUser
Import-Module Pester -RequiredVersion 5.7.1
```

Les fichiers sont en UTF-8. Les sources PowerShell contenant des caractères non ASCII utilisent un BOM pour Windows PowerShell 5.1. Si la console affiche mal les accents :

```powershell
chcp 65001
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
```

## Commandes

```powershell
pwsh -NoProfile -File Tests\Invoke-Tests.ps1
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -Coverage
Invoke-Pester -Path Tests\ApplicationDatabase.Tests.ps1
```

Validation générale depuis la racine :

```powershell
.\Tools\Resolve-ThemeForge.ps1
dotnet build GUI\WinForge.slnx -c Release
dotnet test GUI\WinForge.GUI.Tests\WinForge.GUI.Tests.csproj -c Release --no-build
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
pwsh -NoProfile -File Tools\Invoke-PSScriptAnalyzer.ps1
pwsh -NoProfile -File Tools\lint-fr-diacritics.ps1 -Path GUI\WinForge.GUI\Resources\Resources.fr.resx
pwsh -NoProfile -File Tools\Verify-VersionConsistency.ps1
git diff --check
```

Pour une recette du bureau :

```powershell
pwsh -NoProfile -File Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
```

## Écrire un test

```powershell
BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\Modules\Module.psm1'
    Import-Module $modulePath -Force
}
Describe 'Module Name' {
    It 'Returns the expected value' {
        Get-Something | Should -Be 'expected'
    }
}
```

Utilisez un dossier temporaire distinct, nettoyez les ressources et évitez un état partagé mutable. Préférez les assertions comportementales. Les appels système modificateurs doivent être remplacés par des doublures.

Les régressions de l’audit couvrent la copie immuable des plugins, le travail asynchrone de l’API, la conservation des propriétés des profils et l’interruption des processus dépassant leur délai.

## Dépannage

Si Pester 3 masque la version attendue, importez explicitement `Pester -RequiredVersion 5.7.1` dans une nouvelle session. Vérifiez les chemins de modules avec `Test-Path`. En cas de problème de couverture, consultez `Get-Module Pester -ListAvailable` et le rapport complet avant de modifier les seuils.
