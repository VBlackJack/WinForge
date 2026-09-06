# WinForge Tools

[Français](README.fr.md)

Run PowerShell commands from the repository root. Tool parameters are documented by `Get-Help <script> -Full`.

## Profile and startup tools

| Tool | Purpose |
|---|---|
| `ProfileCreator.html` | Local browser wizard for profile metadata, inheritance, bundled applications, custom sources, system configuration, and JSON export |
| `applications-data.js` | Standalone wizard catalog; separate from the runtime catalog |
| `Launch-StartupManager.ps1`, `StartupManager.html` | Review startup entries and export a blacklist for `Config/startup-blacklist.json` |
| `Launch-AsTrustedInstaller.bat`, `Launch-TrustedInstallerGUI.ps1` | Launch selected administrative tools with TrustedInstaller privileges after explicit confirmation |

The TrustedInstaller launcher requires `NtObjectManager`. Review its requested operation before elevation. See the [profile wizard guide](ProfileCreator_Features.md).

## Build dependencies

```powershell
.\Tools\Resolve-ThemeForge.ps1
dotnet build GUI\WinForge.slnx -c Release
```

The resolver creates `ThemeForge/` inside this checkout at the exact commit recorded in `Config/build-dependencies.json`. CI reads the same file. An existing dirty checkout or a different revision is rejected without resetting it. The sibling repository is not modified. For a deliberately different development source, pass `-p:ThemeForgeRoot=<path>` to MSBuild; that build is outside the pinned dependency baseline.

## Validation

```powershell
.\Tools\Validate-Framework.ps1 -Detailed -Offline
.\Tools\Invoke-PSScriptAnalyzer.ps1
.\Tools\Verify-VersionConsistency.ps1
.\Tools\Validate-AppDatabase.ps1
.\Tests\Invoke-Tests.ps1 -OutputFormat NUnitXml
```

Offline framework validation checks repository structure, modules, profiles, schemas, and locally detectable prerequisites. It omits live network checks and host write-permission probes. Run without `-Offline` when checking a deployment host. CI does not depend on ICMP availability.

For remote package-source checks:

```powershell
.\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey -GenerateReport
```

`Search-ApplicationSources.ps1` helps find package identifiers. Consult its help for the available source filters.

## Package verification

```powershell
.\Tools\Test-ReleasePackage.ps1 -ArchivePath '<release.zip>'
```

The check compares required schema and entry-document content against this checkout without executing the archive. `Build-Release.ps1` includes `Schemas/`, English and French README/changelog files, and runs this gate before writing the archive checksum. Package creation changes version metadata; use the release workflow deliberately.

## Desktop checks

```powershell
.\Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot '<path-to-winsight>'
```

This opt-in check builds and launches the real GUI, drives navigation, and saves screenshots under `TestResults/winsight`. It requires an interactive Windows desktop and a WinSight checkout. See the [visual checklist](../Docs/GUI_VM_VISUAL_CHECKLIST.md) and [UIA tests](../GUI/WinForge.GUI.UITests/README.md).

## System monitoring

`Launch-SystemAudit.bat` starts the system-audit tool interactively. The PowerShell interface supports process, log, and timed observation:

```powershell
.\Tools\System-Audit.ps1 -MonitorProcessName 'msiexec' -GenerateReport -AuditName 'ManualInstall'
.\Tools\System-Audit.ps1 -MonitorLogPath '.\Logs' -GenerateReport
.\Tools\System-Audit.ps1 -Duration 30 -SampleInterval 5 -GenerateReport
```

See the [system-audit reference](System-Audit-README.md) for stop conditions, report paths, optional monitoring, and limitations. Installation operations are separate from these monitoring commands.
