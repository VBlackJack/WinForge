# Contribuer à WinForge

[English](CONTRIBUTING.md)

Les contributions doivent rester respectueuses et constructives. Consultez les tickets et la documentation avant d’ouvrir une modification.

## Environnement

Prérequis : Windows, PowerShell 5.1 ou ultérieur, SDK .NET 10 et Git. PowerShell 7 est recommandé pour les fonctionnalités parallèles.

```powershell
git clone https://github.com/VBlackJack/WinForge.git
cd WinForge
Install-Module -Name Pester -RequiredVersion 5.7.1 -Force -Scope CurrentUser
Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser
.\Tools\Resolve-ThemeForge.ps1
dotnet build ./GUI/WinForge.slnx
pwsh -NoProfile -File Tests/Invoke-Tests.ps1
```

Le script de dépendance récupère le commit ThemeForge fixé dans `Config/build-dependencies.json`. Il refuse de réinitialiser un dossier existant différent ou modifié.

## Organisation et code

`Core/` contient l’infrastructure, `Modules/` les fonctionnalités PowerShell, `Config/` les paramètres, `Profiles/` les profils, `GUI/` l’application WPF et ses tests, et `Tests/` les tests Pester.

Activez le mode strict, utilisez les verbes PowerShell approuvés et validez les paramètres. Chaque fonction publique doit comporter une aide avec synopsis, description, paramètres, résultat et exemple. Gérez les exceptions au niveau approprié et propagez celles que l’appelant doit traiter.

Conventions : fonctions et paramètres en PascalCase, variables en camelCase, variables de module dans `$script:`, constantes en UPPER_SNAKE_CASE. Les sources nouvelles portent l’en-tête Apache 2.0.

Les textes affichés proviennent des ressources de traduction ; les URL, chemins et délais utilisent la configuration et les constantes du projet. Les commentaires, identifiants et messages de commit sont en anglais.

## Vérification

```powershell
dotnet build GUI/WinForge.slnx -c Release
dotnet test GUI/WinForge.GUI.Tests/WinForge.GUI.Tests.csproj -c Release
pwsh -NoProfile -File Tests/Invoke-Tests.ps1 -OutputFormat NUnitXml
.\Tools\Invoke-PSScriptAnalyzer.ps1
.\Tools\Validate-Framework.ps1 -Detailed -Offline
git diff --check
```

Utilisez le script d’analyse du dépôt : il applique les mêmes règles que la CI. Les exclusions doivent être justifiées dans `PSScriptAnalyzerSettings.psd1`. Ne masquez pas un contrôle en échec. Une validation hors ligne ne démontre pas la disponibilité des sources externes.

Les tests doivent couvrir le comportement modifié, les entrées invalides, les erreurs et les protections de sécurité pertinentes. Utilisez des fichiers temporaires isolés et des doublures pour les opérations système. Les tests UIA réels sont facultatifs et exigent un bureau interactif.

## Commits et demandes de fusion

Format : `<type>(<scope>): <description>`, avec un corps et des références de tickets si utiles. Types usuels : `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `style`, `chore`.

Avant une demande de fusion, vérifier :

- Tests et compilation réussis.
- Aucune nouvelle erreur ni avertissement non justifié de l’analyse.
- Description du problème, du comportement obtenu et des validations.
- Documentation actualisée dans les deux langues.
- Ressources de traduction ajoutées pour les nouveaux textes affichés.

## Documentation et traduction

Les README, documents, journaux de modifications et notes de version sont en anglais par défaut. Les versions françaises sont séparées dans des fichiers `.fr.md`, avec des liens réciproques. Mettez les deux versions à jour ensemble.

N’utilisez pas de tiret cadratin ni de crédits d’outillage ou de génération. La description GitHub est en anglais ; les sujets du dépôt doivent refléter ses fonctionnalités réelles. Suivez la [politique de publication](Docs/PUBLICATION_POLICY.fr.md).

Pour PowerShell, ajoutez chaque clé dans `Config/Locales/en.json` et `Config/Locales/fr.json`, puis utilisez le service de localisation. Les clés suivent `<module>.<feature>.<element>.<action>`, par exemple `install.app.starting`. Les chaînes WPF se trouvent dans les fichiers `.resx`.

Consultez l’[architecture](Docs/ARCHITECTURE.fr.md) et le [guide de tests](Tests/README.fr.md) pour les détails.
