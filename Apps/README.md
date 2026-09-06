# WinForge Application Database

[Français](README.fr.md)

`Database/applications.json` is the canonical catalog used by the framework and GUI. Profiles reference stable application IDs, avoiding repeated installation metadata.

## Format

The root contains `DatabaseVersion`, `LastUpdated`, `TotalApplications`, `Applications`, `Categories`, and `Tags`. Consult the file and `Get-DatabaseStatistics` for current values.

An application includes these fields:

| Field | Purpose |
|---|---|
| `Name`, `Description`, `Category` | Display metadata |
| `Sources.Winget`, `Sources.Chocolatey`, `Sources.Store`, `Sources.DirectUrl` | Installation sources |
| `Detection` | Registry, File, Command, StoreApp, or WindowsFeature detection |
| `DefaultPriority`, `DefaultRequired` | Default deployment settings |
| `EnvironmentRestrictions` | Environments excluded from installation |
| `InstallMethod`, `InstallArguments` | Optional installation overrides |
| `Tags`, `Homepage`, `Notes` | Search and maintenance metadata |
| `LastVerified`, `Verified` | Recorded verification date and state |

Use `Schemas/applications-database.schema.json` for the accepted structure and constraints. A direct-download entry must provide the verification metadata required by the catalog validator. Command detection is restricted by `Config/detection-allowlist.json`; an arbitrary command is not a valid detection strategy.

## PowerShell interface

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

`ConvertTo-ProfileApplication` converts a catalog application into a profile entry with overrides. `Test-ApplicationSources` checks installation sources.

## Profiles

A profile can contain ID references:

```json
{
  "Name": "Development",
  "Description": "Development tools",
  "Version": "1.0.0",
  "Inherits": ["Base"],
  "Applications": ["VSCode", "Git"]
}
```

Use objects with `AppId` when overriding an application's priority or required state. Existing inline application definitions are also supported by the profile loader. Keep inherited applications in their parent profile; child updates store the direct selection. GUI updates preserve `SystemConfig` and unedited custom properties.

## Maintenance

1. Add or update the entry in `Database/applications.json`.
2. Keep IDs stable and reconcile catalog metadata, categories, and tags.
3. Validate the JSON and installation sources.
4. Record `LastVerified` and `Verified` only after verification.
5. Revalidate profiles that reference the entry.

```powershell
.\Tools\Validate-AppDatabase.ps1
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

The second command contacts external sources. Generated reports are evidence for that run, not a guarantee that packages will remain available.

## Troubleshooting

For a missing application, check the exact ID and reload the cache. For malformed JSON, use `ConvertFrom-Json` and the schema validator. For an unavailable package, check its source ID and the package manager's output.

See the [quick start](QUICK_START.md), [tools](../Tools/README.md), and [profile documentation](../Docs/ARCHITECTURE.md).
