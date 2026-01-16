# Win11Forge Architecture

## Overview

Win11Forge is a PowerShell-based framework for automated Windows 11 deployment and configuration. It supports profile-based installations with inheritance, parallel application installations, and comprehensive system configuration.

## Directory Structure

```
Win11Forge/
├── Core/                    # Core utilities and shared functions
│   ├── Core.psm1           # 17 functions (logging, status, validation)
│   └── Localization.psm1   # i18n functions (Get-LocalizedString)
├── Modules/                 # Feature modules
│   ├── ApplicationDatabase.psm1   # 14 functions (app DB management)
│   ├── EnvironmentDetection.psm1  # 7 functions (VM/environment detection)
│   ├── InstallationEngine.psm1    # 36 functions (installation logic)
│   ├── Prerequisites.psm1         # 12 functions (prerequisite checks)
│   ├── ProfileManager.psm1        # 11 functions (profile loading/merging)
│   ├── StartMenuLayout.psm1       # 8 functions (Start menu configuration)
│   ├── StartMenuPinning.psm1      # 7 functions (app pinning)
│   ├── StartupManager.psm1        # 5 functions (startup app management)
│   ├── SystemConfig.psm1          # 15 functions (system configuration)
│   └── Win11ForgeGUI.psm1         # 30 functions (console GUI)
├── Profiles/                # Deployment profiles
│   ├── Base.json           # Base profile (common applications)
│   ├── Office.json         # Office profile (inherits Base)
│   ├── Gaming.json         # Gaming profile (inherits Office)
│   └── Personnel.json      # Personal profile (inherits Gaming)
├── Apps/Database/           # Application database
│   └── applications.json   # 70+ application definitions
├── Config/                  # Configuration files
│   ├── version.json        # Framework version
│   ├── global-optimizations.json  # System optimization settings
│   ├── startup-blacklist.json     # Startup app blacklist
│   ├── download-sources.json      # Externalized download URLs
│   └── Locales/            # i18n translation files
│       ├── en.json         # English translations
│       └── fr.json         # French translations
├── Tests/                   # Pester test suite (458 tests)
├── Tools/                   # Utility scripts
└── .github/workflows/       # CI/CD pipeline
```

## Core Components

### 1. Installation Engine (InstallationEngine.psm1)

The heart of the framework, handling application installations.

**Key Functions:**
- `Install-Application` - Sequential single-app installation
- `Install-ApplicationsParallel` - Parallel multi-app installation with throttling
- `Install-ViaWinget` - Winget package manager integration
- `Install-ViaChocolatey` - Chocolatey package manager integration
- `Install-ViaDirectDownload` - Direct URL downloads with SHA256 validation

**Features:**
- Retry logic with exponential backoff (3 attempts)
- SHA256 checksum validation for direct downloads
- Multiple detection methods (Registry, File, Command, WindowsFeature, StoreApp)
- Environment-aware restrictions (VM vs physical)

### 2. Profile Manager (ProfileManager.psm1)

Manages deployment profiles with inheritance support.

**Key Functions:**
- `Import-DeploymentProfile` - Load and validate profile JSON
- `Resolve-ProfileInheritance` - Resolve profile inheritance chains
- `Merge-ProfileApplications` - Merge applications from parent profiles

**Inheritance Model:**
```
Base.json
  └── Office.json
        └── Gaming.json
              └── Personnel.json
```

### 3. Application Database (ApplicationDatabase.psm1)

Centralized application definitions.

**Key Functions:**
- `Get-ApplicationDatabase` - Load applications.json
- `Get-ApplicationByName` - Find app by name
- `Get-ApplicationsByCategory` - Filter by category

**Application Schema:**
```json
{
  "AppId": "unique-id",
  "Name": "Application Name",
  "Category": "Category",
  "Description": "...",
  "Sources": {
    "Winget": "winget-id",
    "Chocolatey": "choco-id",
    "DirectUrl": "https://..."
  },
  "Detection": {
    "Method": "Registry|File|Command|...",
    "Path": "detection-path"
  }
}
```

### 4. Environment Detection (EnvironmentDetection.psm1)

Detects VM environments and hardware characteristics.

**Detected Environments:**
- Hyper-V, VMware, VirtualBox, QEMU/KVM
- AWS, Azure, GCP cloud instances
- Physical machines

### 5. System Configuration (SystemConfig.psm1)

Applies system-wide settings from profiles.

**Configurable Areas:**
- Explorer settings
- Privacy settings
- Power management
- Windows Update policies
- Telemetry settings

## Data Flow

```
┌──────────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Profile JSON    │────▶│ ProfileManager  │────▶│ Application List │
│  (Base, Office)  │     │ (inheritance)   │     │ (merged)         │
└──────────────────┘     └─────────────────┘     └────────┬─────────┘
                                                          │
┌──────────────────┐     ┌─────────────────┐              │
│ applications.json│────▶│ AppDatabase     │──────────────┤
└──────────────────┘     └─────────────────┘              │
                                                          ▼
                         ┌─────────────────────────────────────────┐
                         │        InstallationEngine               │
                         │  ┌───────────┐  ┌────────────┐          │
                         │  │Sequential │  │ Parallel   │          │
                         │  │  Mode     │  │   Mode     │          │
                         │  └─────┬─────┘  └──────┬─────┘          │
                         │        │               │                │
                         │        ▼               ▼                │
                         │  ┌─────────────────────────────────┐    │
                         │  │ Winget → Choco → Store → Direct │    │
                         │  └─────────────────────────────────┘    │
                         └─────────────────────────────────────────┘
```

## Parallel Installation Architecture

```powershell
ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
    # Each thread receives:
    # - Application object
    # - Exported helper functions (as strings)
    # - Repository root path

    # Functions recreated via Invoke-Expression:
    # - Test-ValidDownloadUrl
    # - Test-AppInstalledParallel

    # Local log file per application
    # Retry logic: 3 attempts with exponential backoff
    # SHA256 validation for direct downloads
}
```

## Installation Method Priority

1. **Winget** - Preferred for modern apps
2. **Chocolatey** - Fallback for legacy apps
3. **Microsoft Store** - UWP apps
4. **Direct Download** - Custom installers

## Testing Strategy

- **Unit Tests**: Module function validation
- **Integration Tests**: Cross-module interactions
- **Coverage**: 458 tests across 12 test files
- **CI/CD**: GitHub Actions with PSScriptAnalyzer + Pester

## Configuration Files

### version.json
```json
{
  "DisplayName": "Win11Forge Framework",
  "Version": "3.1.2",
  "ReleaseDate": "2026-01-16"
}
```

### Profile Structure
```json
{
  "Name": "ProfileName",
  "Version": "3.1.2",
  "Description": "...",
  "Inherits": ["ParentProfile"],
  "Applications": [
    { "AppId": "app-id", "Priority": 1 }
  ],
  "SystemConfiguration": {
    "Explorer": { ... },
    "Privacy": { ... }
  }
}
```

## Security Features

- **URL Validation**: Whitelist of trusted domains
- **SHA256 Verification**: Checksum validation for downloads
- **Environment Restrictions**: Per-app VM/physical restrictions
- **Admin Rights Check**: Prerequisites validation

## Internationalization (i18n)

Win11Forge supports multiple languages through a JSON-based localization system.

### Architecture

```
Config/Locales/
├── en.json    # English (default/fallback)
└── fr.json    # French
```

### Usage in PowerShell

```powershell
# Initialize with auto-detected locale
Initialize-Localization

# Or specify locale
Initialize-Localization -Locale 'fr'

# Get translated string
$message = Get-LocalizedString -Key 'install.starting' -Parameters @{ AppName = 'Firefox' }
# Returns: "Starting installation of Firefox..."

# Short alias
$msg = t 'common.success'
```

### Translation Key Convention

Keys use dot notation following the pattern:
```
<module>.<feature>.<element>
```

Examples:
- `install.starting` - Installation started message
- `parallel.summary.title` - Parallel installation summary title
- `prerequisites.chocolatey.installing` - Chocolatey installation message

### Adding New Translations

1. Add keys to `Config/Locales/en.json` (always add English first)
2. Copy to other locale files (e.g., `fr.json`) and translate values
3. Use `Get-LocalizedString` in code instead of hardcoded strings

## Extension Points

1. **New Applications**: Add to `Apps/Database/applications.json`
2. **New Profiles**: Create JSON in `Profiles/`
3. **New Installation Methods**: Extend `InstallationEngine.psm1`
4. **New System Configs**: Extend `SystemConfig.psm1`

## GUI (v3.0.0)

Win11Forge v3.0.0 includes a modern WPF graphical interface:
- Profile selection and customization
- Real-time installation progress
- Application browsing and management
- System configuration preview
- Dark/Light theme support
- Bilingual interface (EN/FR)

**Launch:** `.\Start-Win11ForgeGUI.ps1` or `Win11Forge.GUI.exe`
