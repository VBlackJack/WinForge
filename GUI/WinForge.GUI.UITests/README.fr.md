# Tests UIA de WinForge

[English](README.md)

Ces tests lancent l’application WPF réelle, la pilotent avec UI Automation et produisent des captures PNG. Ils sont ignorés par défaut, car ils exigent un bureau Windows interactif.

Le banc de test démarre `WinForge.GUI.dll` via `dotnet` pour éviter l’élévation UAC imposée par le manifeste `requireAdministrator` de l’exécutable produit.

Depuis la racine du dépôt :

```powershell
$env:WINFORGE_RUN_UIA = '1'
$env:WINFORGE_UIA_ARTIFACTS = '<repo-root>\TestResults\ui-screenshots'
dotnet test GUI\WinForge.GUI.UITests\WinForge.GUI.UITests.csproj --configuration Release
```

Les captures sont enregistrées dans `WINFORGE_UIA_ARTIFACTS`, ou à défaut dans un dossier horodaté sous `%TEMP%\WinForge\UIA`.

## Contrôle WinSight

WinSight fournit un parcours exploratoire facultatif. Son dépôt est distinct et n’est pas requis par la CI habituelle.

```powershell
.\Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
```

Le script compile le serveur MCP WinSight et l’interface WinForge, lance `WinForge.GUI.dll` via `dotnet`, utilise `list_windows`, `inspect_ui_tree`, `click_element` et `capture_screenshot`, puis écrit les captures dans `TestResults\winsight`.

Vous pouvez remplacer le paramètre par une variable :

```powershell
$env:WINSIGHT_ROOT = '<path-to-winsight>'
.\Tools\Invoke-WinsightSmoke.ps1
```

Ce parcours complète les tests xUnit UIA lorsqu’une modification nécessite une inspection plus détaillée du bureau.
