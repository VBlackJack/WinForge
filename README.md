# WinForge

[Français](README.fr.md) · [Download](https://github.com/VBlackJack/WinForge/releases/latest) · [User guide](Docs/USER_GUIDE.md)

**Choose your applications, preview the changes, and set up your Windows PC.**

WinForge brings application installation, updates and reusable profiles into one desktop interface. Start with a ready-made profile or select just the applications you need, then follow each operation through its progress and logs.

Version **2026090801** · Windows 10/11 · English and French · Apache 2.0

## Download and start

1. Open the [latest release](https://github.com/VBlackJack/WinForge/releases/latest).
2. Under **Assets**, download `WinForge_v2026090801.zip` and its `.zip.sha256` file. Choose the packaged ZIP, rather than GitHub's **Source code** archives.
3. [Verify the download](#verify-your-download), then extract the entire ZIP into a local folder.
4. Open that folder and double-click **WinForge.cmd**. Keep the included files together.
5. Open **Applications** to choose your first installation.

The packaged GUI includes its .NET runtime; you do not need the .NET SDK. Internet access is required for package downloads. Some operations require administrator permission. Windows 10 21H2 or later, or Windows 11, is required.

## Your first installation

1. Choose a **Profile**, or select individual applications in the list.
2. Use **Scan Installed** to refresh what is already on your PC.
3. Review the checked applications. You can search and filter the list before proceeding.
4. Open **Preview plan** to review the proposed operations and their limitations.
5. Click **Install Selected** and follow the progress.
6. Open **Execution history** to inspect the result. If an operation fails, read its error and logs before retrying the affected applications.

Start with a small selection if you want to try WinForge before applying a full profile.

## Choose a starting profile

| Profile | Best for |
| --- | --- |
| **Base** | Everyday browsing, media and system utilities |
| **Office** | Productivity, documents, PDF and collaboration |
| **Gaming** | Game platforms and communication |
| **Personnel** | Development tools and an advanced personal workstation |
| **Enterprise** | IT tools, security and professional workstations |

Profiles can include applications inherited from a parent. Review the complete selection before installing; a profile is a starting point, not a requirement.

To reuse your selection, click **Save Profile**. To change the selected profile, adjust its checkboxes and click **Update profile**. Inherited applications must be removed from their parent profile. [Learn about profiles](Docs/USER_GUIDE.md#profiles).

## Everyday tasks

| I want to… | Where to start |
| --- | --- |
| Check available updates | **Applications → Scan for Updates** |
| Find an application's installation result | **Applications → Execution history**, then **Logs** for details |
| Change the theme, language or accessibility options | **Settings** |
| Add or correct an application definition | **App Catalog** |
| Automate deployments with PowerShell | [Deployment workbench](Docs/DEPLOYMENT_WORKBENCH.md) |

## What to expect

- The catalog contains **195 applications**. Sources and installation support vary by application; the entire catalog is not certified.
- Installation and runtime validation are different. Virtualization applications are outside the runtime coverage of our disposable VM tests. Validate them on suitable physical hardware or a supported nested-virtualization setup.
- Rollback can remove supported newly installed packages. It does not restore an older application version or Windows configuration.
- GUI execution receipts and PowerShell deployment plans are separate formats.
- The [validation report](Docs/Validation/20260908/README.md) records tested display combinations, screen-reader workflows, installation results and known gaps, including VLC version detection and unavailable LDPlayer sources.

## Upgrading

Close WinForge and extract the new release into a separate folder. Keep your previous folder and custom profiles until you have checked the new version. Read the [changelog](CHANGELOG.md) for changes affecting your workflow.

## Verify your download

Open PowerShell in the folder containing both downloaded files and run:

```powershell
$expected = ((Get-Content .\WinForge_v2026090801.zip.sha256 -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash .\WinForge_v2026090801.zip -Algorithm SHA256).Hash
if ($expected -eq $actual) { 'OK' } else { 'CHECKSUM MISMATCH' }
```

Extract and launch only when the result is **OK**. If it does not match, download both files again from the same release.

## Help and documentation

- [User guide](Docs/USER_GUIDE.md): profiles, deployment and troubleshooting.
- [Documentation index](Docs/README.md): administration, PowerShell and developer references.
- [Report a problem](https://github.com/VBlackJack/WinForge/issues): include your WinForge version, Windows version, application and error message. Remove secrets from any logs you share.
- [Contributing](CONTRIBUTING.md) · [License](LICENSE)
