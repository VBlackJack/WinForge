<!--
Copyright 2026 Julien Bombled

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
-->

# Win11Forge Architecture

Win11Forge combines a PowerShell automation framework with a WPF desktop GUI.
The repository keeps the installation engine, application catalogue, profiles,
GUI, tests, and maintenance tooling together so a release can be built and
validated from one checkout.

## Runtime Layout

- `Core/`: shared PowerShell framework modules, logging, secure storage, REST API support, and module loading.
- `Modules/`: install, update, rollback, profile, prerequisite, scheduled deployment, and system-configuration modules.
- `Apps/Database/applications.json`: canonical application catalogue consumed by both PowerShell and the GUI.
- `Profiles/`: built-in deployment profile definitions.
- `Config/`: version, feature flags, API settings, timeouts, localization, and source configuration.
- `GUI/Win11Forge.GUI/`: WPF .NET 10 application.
- `GUI/Win11Forge.GUI.Tests/`: unit and static-analysis tests for GUI behavior, accessibility, localization, and services.
- `GUI/Win11Forge.GUI.UITests/`: opt-in desktop smoke tests.
- `Tools/`: validation and maintenance scripts.

## GUI Architecture

The GUI follows MVVM with service-backed ViewModels:

- ViewModels expose commands and observable state.
- Services own filesystem, PowerShell, dialogs, settings, profile, catalogue, and deployment concerns.
- Batch app operations are delegated to coordinators for scanning, installation, update, and uninstall workflows.
- User-facing strings are localized through `Resources.resx` and `Resources.fr.resx`.
- Visual tokens live in `App.xaml` and theme bridge resources rather than inline view literals.

## Profiles

Profiles are JSON documents with:

- `Name`
- `Description`
- `Version`
- `Inherits`
- `Applications`
- optional `SystemConfig`

The GUI migrates bundled profiles into the user data profile directory on first run. The Applications page can:

- apply a profile to the app selection,
- save the current selection as a new profile,
- update the selected profile from the current checked apps.

When a profile inherits from a parent, inherited applications remain owned by the parent. Updating the child profile writes only the direct application delta for that child.

## Theming

`Resources/FluentThemeBridge.xaml` provides design-time fallback brushes. At runtime, `ThemeService` applies the selected ThemeForge palette and bridges semantic Win11Forge brushes from the active palette. This keeps status colors, row backgrounds, text, borders, icons, and selection states aligned with the current theme and accent.

## Public API

The PowerShell REST API is configured through `Config/api-settings.json` and implemented in `Core/RestApiServer.psm1`. It is local-first by default and supports API-key authentication, CSRF protection for mutating requests, rate limiting, version checks, profile discovery, application discovery, deployment, rollback, and cache status endpoints.

## Validation

Common validation commands:

```powershell
dotnet build GUI/Win11Forge.GUI/Win11Forge.GUI.csproj -c Release
dotnet test GUI/Win11Forge.GUI.Tests/Win11Forge.GUI.Tests.csproj
dotnet test GUI/Win11Forge.GUI.UITests/Win11Forge.GUI.UITests.csproj --no-restore
```

Additional PowerShell and catalogue validation tools are documented in [`../Tools/README.md`](../Tools/README.md) and [`../Tests/README.md`](../Tests/README.md).
