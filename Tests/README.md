# WinForge Tests

## Scope

This folder contains the PowerShell/Pester test suite for the framework modules. GUI unit tests live under `GUI/WinForge.GUI.Tests`, and opt-in desktop UI smoke tests are documented in `GUI/WinForge.GUI.UITests/README.md`.

Current baseline after the May 2026 backlog closure:

- Framework display version: `2026062301` (`Config/version.json`)
- Pester runner: `Tests/Invoke-Tests.ps1`
- Latest full Pester validation: `1842` tests total, `1836` passed, `0` failed, `6` skipped
- Normal output artifacts: `Tests/Results/`

## Encoding

Files are UTF-8. If accents or symbols render incorrectly in the Windows console, run:

```powershell
chcp 65001
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
```

## Setup

Install or update Pester v5:

```powershell
.\Tests\Install-Pester.ps1
```

Manual fallback:

```powershell
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck
```

## Common Commands

Run the full Pester suite:

```powershell
pwsh -NoProfile -File Tests\Invoke-Tests.ps1
```

Generate NUnit XML for CI/artifacts:

```powershell
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
```

Run with coverage:

```powershell
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -Coverage
```

Run a focused file while iterating:

```powershell
Invoke-Pester -Path Tests\ApplicationDatabase.Tests.ps1
```

## Repository-Level Validation

Use these commands before merging broad framework or tooling changes:

```powershell
dotnet build GUI\WinForge.slnx -c Release
dotnet test GUI\WinForge.GUI.Tests\WinForge.GUI.Tests.csproj -c Release --no-build
pwsh -NoProfile -File Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
pwsh -NoProfile -File Tools\Invoke-PSScriptAnalyzer.ps1
pwsh -NoProfile -File Tools\lint-fr-diacritics.ps1 -Path GUI\WinForge.GUI\Resources\Resources.fr.resx
pwsh -NoProfile -File Tools\Verify-VersionConsistency.ps1
git diff --check
```

Opt-in desktop smoke through WinSight:

```powershell
pwsh -NoProfile -File Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
```

## Structure

```text
Tests/
├── README.md
├── Install-Pester.ps1
├── Invoke-Tests.ps1
├── *.Tests.ps1
└── Results/
    ├── TestResults_*.NUnitXml
    └── Coverage_*.xml
```

## Test Authoring

Use the standard Pester shape:

```powershell
BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\Modules\Module.psm1'
    Import-Module $modulePath -Force
}

Describe 'Module Name' {
    Context 'Feature Group' {
        It 'Does the expected thing' {
            $result = Test-Function -Input 'value'
            $result | Should -Be 'expected'
        }
    }
}
```

Keep tests isolated:

- Use a unique temp directory per test when writing files.
- Avoid shared mutable fixture state across parallel-safe tests.
- Use explicit cleanup with retry when Windows file locks are plausible.
- Prefer behavioral assertions over line-count or timestamp assertions unless the timestamp is the behavior under test.

## Troubleshooting

If Pester v3 shadows v5:

```powershell
Uninstall-Module -Name Pester -AllVersions -Force
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck
```

If module imports fail, verify paths from the test file:

```powershell
$modulePath = Join-Path $PSScriptRoot '..\Modules\Module.psm1'
Test-Path $modulePath
```

If coverage fails unexpectedly, first confirm the installed Pester version:

```powershell
(Get-Module Pester -ListAvailable).Version
```
