# Application Database: Quick Start

[Français](QUICK_START.fr.md)

Run these commands from the repository root.

## Load and query the catalog

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

Use the returned statistics for the current catalog size and verification counts. Example counts from older releases do not describe the current database.

## Create a profile

Choose IDs from the catalog and keep each definition in the database:

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

Inspect the JSON before deployment. Use a unique filename to preserve existing profiles.

## Validate and export

```powershell
.\Tools\Validate-AppDatabase.ps1
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
Get-AllApplications |
    Select-Object Name, Category, @{N='Winget'; E={$_.Sources.Winget}}, Verified |
    Export-Csv -LiteralPath 'AppDatabase.csv' -NoTypeInformation -Encoding UTF8
```

Source validation needs the corresponding package managers and network access. Catalog filtering alone does not install anything.

## Troubleshooting

Check that `Apps/Database/applications.json` exists and parses with `ConvertFrom-Json`. After editing it, call `Reset-DatabaseCache` and reload the database.

See the [catalog reference](README.md), [changelog](../CHANGELOG.md), and `Get-Help .\Tools\Validate-AppDatabase.ps1 -Full`.
