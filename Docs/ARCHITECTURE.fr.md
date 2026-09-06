# Architecture WinForge

[English](ARCHITECTURE.md)

WinForge associe un framework PowerShell et une interface WPF .NET 10. Le moteur, les profils, le catalogue, l’interface et les outils de validation sont regroupés dans le dépôt.

## Organisation

| Dossier | Contenu |
|---|---|
| `Core/` | Infrastructure PowerShell, journaux, stockage sécurisé, API et chargement des modules |
| `Modules/` | Installation, mise à jour, restauration, profils, prérequis, planification et configuration système |
| `Apps/Database/applications.json` | Catalogue partagé par PowerShell et l’interface |
| `Profiles/` | Profils intégrés |
| `Config/` | Version, fonctionnalités, API, délais, traductions et sources |
| `Schemas/` | Schémas de validation JSON |
| `GUI/WinForge.GUI/` | Application WPF |
| `GUI/WinForge.GUI.Tests/` | Tests unitaires et contrôles statiques |
| `GUI/WinForge.GUI.UITests/` | Tests de bureau facultatifs |
| `Tools/` | Validation et maintenance |

## Interface

L’interface suit MVVM. Les ViewModels exposent les commandes et l’état observable ; les services gèrent les fichiers, PowerShell, les dialogues, les paramètres, les profils et les déploiements. Des coordinateurs prennent en charge les opérations applicatives par lots.

Les textes affichés proviennent de `Resources.resx` et `Resources.fr.resx`. Les ressources de `App.xaml` et du pont de thème centralisent les jetons visuels.

## Profils

Un profil contient `Name`, `Description`, `Version`, `Inherits`, `Applications` et éventuellement `SystemConfig`. Au premier lancement, les profils intégrés sont migrés vers le dossier utilisateur.

La page Applications permet d’appliquer un profil, d’enregistrer une sélection et de mettre à jour le profil sélectionné. Les applications héritées restent définies dans le parent. La mise à jour d’un enfant écrit sa sélection directe, préserve les autres propriétés, dont `SystemConfig`, et remplace atomiquement le fichier complet.

## Thèmes

`Resources/FluentThemeBridge.xaml` fournit les pinceaux de secours pour le concepteur. À l’exécution, `ThemeService` applique la palette ThemeForge et actualise les pinceaux sémantiques : états, fonds, textes, bordures, icônes et sélection.

## API

L’API locale est configurée dans `Config/api-settings.json` et implémentée dans `Core/RestApiServer.psm1`. Elle utilise les clés d’API, la protection CSRF et la limitation de débit. Elle expose la version, les profils, les applications, le déploiement, la restauration et l’état des caches. Consultez la [référence API](API_DOCUMENTATION.fr.md) pour le cycle asynchrone du déploiement.

## Isolation des plugins

Les plugins sont découverts dans `Plugins/`. Quand l’isolation est activée, deux contrôles s’appliquent :

- L’analyse AST autorise une liste explicite de commandes et de types .NET. Les références à d’autres types et leurs accès statiques sont refusés.
- Le chargement et les hooks s’exécutent dans un processus de travail avec un runspace déclaré `ConstrainedLanguage` avant compilation du code.

`Import-Plugin` conserve le texte validé et les noms des fonctions exportées comme données. Il ne charge pas ce texte dans la session principale quand l’isolation est activée. Chaque hook importe la même copie dans le processus contraint, en conservant les variables de module et les fonctions auxiliaires. Une modification ultérieure du fichier source ne modifie pas le handler enregistré.

Lorsque `trustedPublishers` est renseigné, le point d’entrée doit aussi posséder une signature Authenticode valide provenant d’un éditeur autorisé.

## Détection par commande

`Detection.Command` est encadré par deux listes dans `Config/detection-allowlist.json` : `allowedExecutables` et `allowedArguments`. Elles sont appliquées à la détection graphique, à la vérification après mise à jour et aux modules PowerShell. Une configuration absente, illisible ou invalide refuse cette détection et produit un diagnostic.

L’autorisation d’un interpréteur ne suffit pas : ses arguments pourraient contenir du code. Les arguments autorisés sont donc limités aux requêtes de détection prévues.

## Validation et dépendance

`Test-AllConfigurationFiles` associe les fichiers de configuration à leurs schémas. Un schéma requis manquant provoque un échec. Ces contrôles sont intégrés à `Invoke-JsonSchemaValidation` et à `Tools/Validate-Framework.ps1`.

`Config/build-dependencies.json` fixe le commit ThemeForge utilisé en CI et par `Tools/Resolve-ThemeForge.ps1`. Une surcharge explicite de source MSBuild sert au développement et change ce périmètre.

```powershell
.\Tools\Resolve-ThemeForge.ps1
dotnet build GUI/WinForge.GUI/WinForge.GUI.csproj -c Release
dotnet test GUI/WinForge.GUI.Tests/WinForge.GUI.Tests.csproj
dotnet test GUI/WinForge.GUI.UITests/WinForge.GUI.UITests.csproj --no-restore
```

Les tests UIA nécessitent une activation explicite. Consultez les [outils](../Tools/README.fr.md) et les [tests](../Tests/README.fr.md).
