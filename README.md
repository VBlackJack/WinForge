# WinForge v2026090703

[Français](README.fr.md)

**Set up a Windows 10/11 PC with reproducible application profiles.**

[![Version](https://img.shields.io/badge/version-2026090703-blue.svg)](CHANGELOG.md)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

WinForge automates Windows application installation and updates using JSON profiles. Its WPF interface lets you choose a profile, adjust the selection, scan existing installations, run batch installations, and maintain the application catalog.

## Quick Start

1. Download the latest archive from [releases](https://github.com/VBlackJack/WinForge/releases/latest), together with its `.zip.sha256` file.
2. Verify the archive before extracting it. Replace `XXXXXXXXXX` with the downloaded version:

   ```powershell
   $expected = ((Get-Content .\WinForge_vXXXXXXXXXX.zip.sha256 -Raw).Trim() -split '\s+')[0]
   $actual = (Get-FileHash .\WinForge_vXXXXXXXXXX.zip -Algorithm SHA256).Hash
   if ($expected -eq $actual) { 'OK' } else { 'CHECKSUM MISMATCH' }
   ```

3. Extract the archive into a local folder only if the checksum matches.
4. Run `WinForge.cmd` or `WinForge.GUI.exe`.
5. Choose a profile, adjust the application selection if needed, and start installation.

## Included Profiles

| Profile | Purpose | Contents |
| --- | --- | --- |
| `Base` | general setup | browsers, multimedia, system utilities, diagnostics, and security |
| `Office` | productivity | `Base` + office suite, PDF, collaboration |
| `Gaming` | gaming | `Office` + game platforms and communication |
| `Personnel` | advanced workstation | `Gaming` + development tools, cloud, VPN, and personal productivity |
| `Enterprise` | professional workstation | `Base` + IT tools, security, collaboration, and hardened configuration |

Profiles can inherit from one another. An application inherited from a parent profile must be removed from that parent, not from the child.

## Features

- Modern WPF interface with light and dark themes, in English and French.
- Catalog of 195 applications with Winget, Chocolatey, Microsoft Store, or direct-download sources depending on the entry.
- Detection of installed applications and available updates.
- Batch installation, update, and uninstallation with progress, logs, and cooperative cancellation.
- Application catalog editing from the interface.
- New profile creation and direct updates to an existing profile from the Applications grid selection.
- Scheduled deployments from settings.
- Local PowerShell REST API for advanced automation.

## Edit a Profile

1. Open **Applications**.
2. Select a profile in the **Profile** card.
3. Check or uncheck applications in the grid.
4. Click **Update profile** to save the selection to that profile.

Use **Save Profile** to create a new profile or save a selection under another name.

## Requirements

- Windows 10 21H2 or later, or Windows 11.
- Internet access for package sources.
- Administrator privileges for system operations and some installations.
- PowerShell is used by the installation modules included with the project.

## Documentation

- [User guide](Docs/USER_GUIDE.md)
- [Documentation index](Docs/README.md)
- [Architecture](Docs/ARCHITECTURE.md)
- [API documentation](Docs/API_DOCUMENTATION.md)
- [Contribution guide](CONTRIBUTING.md)
- [Changelog](CHANGELOG.md)

## Support

Report problems and feature requests through [GitHub issues](https://github.com/VBlackJack/WinForge/issues).

**License:** Apache 2.0
