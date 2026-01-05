# Win11Forge v3.0.0 Release Notes

**Release Date:** January 4, 2026
**License:** Apache 2.0
**Author:** Julien Bombled

---

## Overview

Win11Forge v3.0.0 is a major release introducing a modern graphical user interface built with WPF and MaterialDesign, while maintaining full compatibility with the battle-tested PowerShell engine. This release transforms Win11Forge from a command-line tool into a professional-grade deployment solution accessible to both technical and non-technical users.

---

## Highlights

### Modern WPF GUI

- **MaterialDesignThemes** integration for a polished, professional appearance
- **Dark/Light theme** support with instant switching and persistence
- **Internationalization (i18n)** with English and French languages
- **Responsive layout** optimized for various screen sizes

### Hybrid Architecture

- **PowerShell Bridge** seamlessly connects the C# GUI to the proven PowerShell engine
- **Non-blocking operations** using async/await patterns throughout
- **Real-time progress** updates via IProgress<T> callbacks
- **Zero code duplication** - GUI calls existing PowerShell functions

### Enhanced User Experience

- **Dashboard** with system information, stats cards, and recent activity
- **Visual deployment** with progress bars, parallel thread visualization, and logs
- **Application Manager** with search, filtering, and installation status scanning
- **Profile Editor** with visual inheritance management

---

## New Features

### 1. Dashboard View

- System information panel (Windows version, build, environment type)
- Statistics cards showing apps installed, profiles available, recent deployments
- Recent activity feed with deployment history
- Quick action buttons for common operations

### 2. Deployment View

- Profile selection with inheritance visualization
- Application list with checkboxes for selective installation
- Real-time progress tracking with per-application status
- **Pause/Resume** functionality for long deployments
- **Cancel** with graceful cleanup
- Parallel installation visualization (up to 5 concurrent threads)
- Live log output during deployment

### 3. Application Manager

- Searchable application list with instant filtering
- Category and status filters (All/Installed/Available)
- Installation status detection via PowerShell bridge
- Scan functionality to refresh installation status
- Individual app actions (Install, Remove, Update)

### 4. Profile Editor

- Create new profiles or edit existing ones
- Visual inheritance chain (Parent profile selection)
- Add/Remove applications with database browser
- Clear separation of inherited vs. local applications
- Profile validation before save

### 5. Settings

- Theme switching (Dark/Light) with immediate effect
- Language selection with restart notification
- Settings persistence to `%LOCALAPPDATA%\Win11Forge\settings.json`
- Deployment history management (view/clear)

---

## Technical Specifications

### Architecture

```
Win11Forge v3.0.0
├── Core/                    # PowerShell engine (unchanged)
├── Modules/                 # PowerShell modules (unchanged)
├── GUI/
│   ├── Win11Forge.GUI/      # WPF Application
│   │   ├── Views/           # XAML views (5 main views)
│   │   ├── ViewModels/      # MVVM ViewModels
│   │   ├── Models/          # Data models
│   │   ├── Services/        # PowerShellBridge, Settings, History
│   │   ├── Converters/      # XAML value converters
│   │   └── Resources/       # i18n (Resources.resx, Resources.fr.resx)
│   └── Win11Forge.GUI.Tests/ # xUnit test project
└── Build-Release.ps1        # Release builder script
```

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| .NET | 8.0 | Runtime framework |
| MaterialDesignThemes | 5.1.0 | UI styling |
| CommunityToolkit.Mvvm | 8.3.2 | MVVM infrastructure |
| Microsoft.PowerShell.SDK | 7.4.6 | PowerShell integration |

### System Requirements

- **OS:** Windows 10 21H2+ / Windows 11
- **Runtime:** .NET 8.0 (included in self-contained build)
- **RAM:** 200 MB minimum
- **Disk:** 150 MB for installation

---

## Performance

### Deployment Engine

- **Parallel installation** with configurable thread count (default: 5)
- **SemaphoreSlim** for controlled concurrent operations
- **ManualResetEventSlim** for pause/resume without thread blocking
- **CancellationToken** support for graceful cancellation

### Application Scanning

- **Parallel detection** with 8 concurrent threads
- **Progress reporting** during scan operations
- **Efficient caching** to avoid redundant PowerShell calls

### Startup

- Settings applied before UI rendering
- Theme and language loaded from persisted JSON
- Target startup time: < 3 seconds

---

## Security

All security features from v2.6.0 are preserved:

- **SHA256 validation** for direct URL downloads
- **Trusted URL whitelist** for download sources
- **No elevation persistence** - admin rights requested only when needed
- **Secure settings storage** in LocalAppData

---

## Quality Assurance

### Test Coverage

| Component | Tests | Status |
|-----------|-------|--------|
| PowerShell Core | 479 | Passing |
| GUI ViewModels | 36 | Passing |
| Total | 515 | Passing |

### Static Analysis

- **PSScriptAnalyzer:** 0 warnings
- **C# Compiler:** 0 warnings (Release build)

---

## Migration from v2.x

### Compatibility

- **Full backward compatibility** with existing profiles
- **CLI remains available** via `Win11Forge.ps1`
- **Same PowerShell modules** - no changes to Core/Modules

### New Files

```
GUI/                         # New WPF project
Build-Release.ps1            # Release builder
Docs/RELEASE_NOTES_v3.0.0.md # This file
```

### Settings Migration

User settings are now persisted in:
```
%LOCALAPPDATA%\Win11Forge\
├── settings.json            # Theme, language preferences
└── deployment_history.json  # Deployment history
```

---

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- PowerShell 7.4+

### Build Commands

```powershell
# Run tests
dotnet test GUI/Win11Forge.GUI.Tests

# Build for development
dotnet build GUI/Win11Forge.GUI -c Debug

# Create release package
.\Build-Release.ps1

# Create release without tests (not recommended)
.\Build-Release.ps1 -SkipTests
```

### Release Output

```
Dist/Win11Forge_v3.0.0/
├── Win11Forge.GUI.exe       # Self-contained executable (~120 MB)
├── Start-Win11Forge.ps1     # Launcher script (GUI or CLI)
├── Win11Forge.ps1           # CLI entry point
├── Modules/                 # PowerShell modules
├── Core/                    # Core scripts
├── Apps/                    # Application database
├── Profiles/                # Profile definitions
└── Config/                  # Configuration files

Dist/Win11Forge_v3.0.0.zip   # Distribution archive
```

---

## Recent Updates (v3.0.0 Final)

### Application Detection Improvements
- **Winget fallback detection** - When Registry/File detection fails, automatically tries `winget list --id` as fallback
- **Office installation wait** - Polls for Office executables after Click-to-Run async installation completes
- **Increased timeouts** - Default 30 minutes, Office-specific 45 minutes for slow VMs

### Bug Fixes
- Fixed PowerShell script execution deadlock (concurrent stdout/stderr reading)
- Fixed system info retrieval using native .NET instead of PowerShell SDK
- Fixed Sources column not displaying in Applications view
- Fixed Scan button staying greyed out after loading applications
- Fixed winget IDs: ProtonVPN, RoboForm, Mp3tag, WinAero Tweaker

### New Views
- **Prerequisites page** - Visual prerequisite checker with one-click installation

---

## Known Limitations

1. **Language change** requires application restart
2. **Profile inheritance visualization** limited to single parent display
3. **Deployment history** stored as JSON (SQLite planned for v3.1)
4. **No auto-update** mechanism (planned for v3.1)

---

## Roadmap

### v3.1.0 (Planned)

- SQLite deployment history
- Auto-update mechanism
- Export deployment reports (HTML/PDF)
- Windows Task Scheduler integration

### v3.2.0 (Planned)

- Custom application definitions via GUI
- Batch profile operations
- Advanced filtering options

---

## Contributors

- **Julien Bombled** - Project lead and development

---

## License

Copyright 2026 Julien Bombled

Licensed under the Apache License, Version 2.0. See [LICENSE](../LICENSE) for details.
