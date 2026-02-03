# Win11Forge Architecture Documentation

**Version:** 3.5.2
**Last Updated:** 2026-02-03
**Author:** Julien Bombled

## Overview

Win11Forge is a Windows 11 deployment automation framework with a modular PowerShell backend and a .NET 8 WPF GUI. This document describes the architectural design, module organization, and key patterns used throughout the codebase.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Win11Forge GUI                           │
│                    (.NET 8 WPF + MVVM)                         │
├─────────────────────────────────────────────────────────────────┤
│                      REST API Layer                             │
│              (RestApiServer + ApiEndpoints)                     │
├─────────────────────────────────────────────────────────────────┤
│                   PowerShell Backend                            │
│   ┌──────────────┐  ┌──────────────────────────────────────┐   │
│   │  Core (13)   │  │        Feature Modules (22+)         │   │
│   │  - Logging   │  │  - InstallationOrchestrator          │   │
│   │  - i18n      │  │  - ApplicationDetection              │   │
│   │  - Config    │  │  - ProfileManager                    │   │
│   │  - Security  │  │  - StateManager                      │   │
│   │  - Plugins   │  │  - RollbackManager                   │   │
│   └──────────────┘  └──────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│                    Installation Methods                         │
│        Winget │ Chocolatey │ Microsoft Store │ Direct          │
└─────────────────────────────────────────────────────────────────┘
```

## Module Organization

### Core Modules (13)

Located in `Core/`, these provide foundational infrastructure:

| Module | Purpose | Key Functions |
|--------|---------|---------------|
| `Core.psm1` | Logging, utilities | `Write-Status`, `Get-LocalizedString` |
| `Localization.psm1` | Internationalization | `Get-LocalizedString` (alias: `t`) |
| `DirectoryConstants.psm1` | Path constants | Framework paths, never hardcode |
| `SecureStorage.psm1` | DPAPI encryption | `Protect-SecureData`, `Unprotect-SecureData` |
| `RestApiServer.psm1` | HTTP REST server | `Start-ApiServer`, `Register-ApiEndpoint` |
| `ApiEndpoints.psm1` | REST handlers | `Get-VersionHandler`, `Start-DeploymentHandler` |
| `PluginManager.psm1` | Plugin lifecycle | `Register-Plugin`, `Invoke-PluginHook` |
| `PluginSandbox.psm1` | Sandboxed execution | `Invoke-PluginSandboxed` |
| `TimeoutSettings.psm1` | Timeout configuration | `Get-TimeoutSetting` |
| `FeatureFlags.psm1` | Feature toggles | `Get-FeatureFlag`, `Set-FeatureFlag` |
| `ModuleLoader.psm1` | Dependency loading | `Import-CoreModules` |
| `Win11ForgeExceptions.psm1` | Exception types | Custom exception classes |
| `StructuredLogging.psm1` | Structured logs | JSON log output |

### Feature Modules (22+)

Located in `Modules/`, these implement domain functionality:

| Module | Purpose | Key Functions |
|--------|---------|---------------|
| `InstallationOrchestrator.psm1` | High-level orchestration | `Start-Deployment`, `Invoke-ParallelInstallation` |
| `InstallationMethods.psm1` | Install implementations | `Install-ViaWinget`, `Install-ViaChocolatey` |
| `ApplicationDetection.psm1` | Detection methods | `Test-ApplicationInstalled`, `Get-InstalledApplicationsCache` |
| `ProfileManager.psm1` | Profile handling | `Get-DeploymentProfile`, `Test-ProfileCycles` |
| `StateManager.psm1` | State machine | `Get-ApplicationState`, `Set-ApplicationState` |
| `RollbackManager.psm1` | Rollback operations | `Start-Rollback`, `Register-CriticalFailureHandler` |
| `UpdateManager.psm1` | Update orchestration | `Get-WingetUpdatesBatch`, `Install-Update` |
| `ParallelDetection.psm1` | Batch detection | `Get-ApplicationStatusBatch` (PS7+) |
| `JsonSchemaValidation.psm1` | Schema validation | `Test-JsonSchema` (Draft 7, Draft 2020-12) |
| `ScheduledDeployment.psm1` | Scheduled tasks | `New-ScheduledDeployment` |
| `StartupManager.psm1` | Startup apps | `Get-StartupApplications`, `Disable-StartupApplications` |

## Key Design Patterns

### 1. Installation Priority Chain

Applications are installed using a priority-based method selection:

```
Winget → Chocolatey → Microsoft Store → DirectDownload
```

Each method is tried in order; if one fails, the next is attempted with exponential backoff retry (3 attempts).

### 2. Profile Inheritance

Profiles use a hierarchical inheritance model:

```
Base
├── Office (inherits Base)
│   └── Gaming (inherits Office)
│       └── Personnel (inherits Gaming)
└── Enterprise (inherits Base)
```

Profile resolution is handled by `ProfileManager.psm1` with cycle detection via `Test-ProfileCycles`.

### 3. Detection Methods (Registry-First)

Application detection prioritizes registry checks for performance:

1. **Registry** (~20ms) - Checked FIRST
2. **File** - File existence
3. **Command** - Command availability
4. **WindowsFeature** - Windows Features
5. **StoreApp** - Microsoft Store apps

Batch parallel detection is available via `ParallelDetection.psm1` (PowerShell 7+).

### 4. Plugin Sandboxing

Plugins execute in isolated PowerShell jobs with:

- Configurable timeout (default: 30s)
- Separate process isolation
- Error capture without affecting main process
- Optional network access restriction

### 5. MVVM Pattern (GUI)

The WPF GUI follows strict MVVM separation:

```
Views (XAML) → ViewModels (CommunityToolkit.Mvvm) → Models (DataAnnotations)
                           ↓
                    Services (ISP)
```

Services follow Interface Segregation Principle:
- `IApplicationManagementService`
- `IProfileManagementService`
- `IDeploymentOrchestrationService`
- `IPrerequisitesService`
- `ISystemInfoService`

## Security Architecture

### DPAPI Encryption

Sensitive configuration (API keys, credentials) uses Windows DPAPI:

```powershell
# SecureStorage.psm1
$encrypted = Protect-SecureData -PlainText $apiKey -Scope CurrentUser
$decrypted = Unprotect-SecureData -EncryptedData $encrypted
```

### REST API Security

- **Authentication**: API key via `X-API-Key` header
- **CSRF Protection**: Token required for state-changing requests
- **Rate Limiting**: 60 req/min, 1000 req/hour per IP
- **Localhost Only**: Default binding to 127.0.0.1

### Input Validation

- Profile names validated against path traversal
- JSON Schema validation (Draft 7, Draft 2020-12)
- DataAnnotations on GUI models
- Parameter validation with `[ValidateNotNullOrEmpty()]`

## Internationalization (i18n)

All user-facing strings use the i18n system:

```powershell
# Using Get-LocalizedString
Write-Status -Message (Get-LocalizedString -Key 'install.starting' -Parameters @{ AppName = $app }) -Level 'Info'

# Using alias 't'
$msg = t 'common.success'
```

**Key format**: `<module>.<feature>.<element>.<action>`

Locale files: `Config/Locales/en.json`, `Config/Locales/fr.json` (1,318 keys each)

## Configuration Management

### Single Source of Truth

| Configuration | File | Purpose |
|--------------|------|---------|
| Framework Version | `Config/version.json` | Version number |
| Timeouts | `Config/timeouts-settings.json` | Operation timeouts |
| Feature Flags | `Config/feature-flags.json` | Runtime toggles |
| API Settings | `Config/api-settings.json` | REST API config (DPAPI) |
| Translations | `Config/Locales/*.json` | i18n strings |

### Data Files

| Data | File | Purpose |
|------|------|---------|
| Applications | `Apps/Database/applications.json` | 175+ app definitions |
| Profiles | `Profiles/*.json` | Deployment profiles |
| Schemas | `Schemas/*.json` | JSON Schema validation |

## Testing Strategy

### PowerShell (Pester v5+)

- 20 test files in `Tests/`
- 1,047+ test cases
- 80% minimum coverage enforced

```powershell
.\Tests\Invoke-Tests.ps1 -Coverage
```

### .NET (xUnit)

- GUI component tests
- ViewModel tests
- Service tests

```powershell
dotnet test ./GUI/Win11Forge.GUI.Tests
```

## Performance Optimizations

### Caching

- **Winget List Cache**: Cached package list with TTL
- **Registry Cache**: Batch registry queries
- **Detection Cache**: Per-session detection results

### Parallel Processing

- Parallel installation via `ForEach-Object -Parallel` (PS7+)
- Configurable throttle limit
- Per-app logging to separate files

## Deployment Flow

```
1. Profile Selection
   └── Resolve inheritance chain
       └── Merge applications list

2. Prerequisites Check
   └── Winget, Chocolatey, PowerShell version

3. Detection Phase
   └── Batch detect all applications
       └── Build installed cache

4. Installation Phase
   └── For each app not installed:
       └── Try Winget → Chocolatey → Store → Direct
           └── Retry with exponential backoff
               └── Update state machine

5. Post-Installation
   └── Verify installations
       └── Generate report
           └── Optional rollback
```

## Version History

| Version | Notable Changes |
|---------|-----------------|
| 3.5.2 | REST API, Plugin sandbox, DPAPI encryption |
| 3.5.1 | JSON Schema validation, batch detection |
| 3.5.0 | Enterprise profile, state machine, 175 apps |
| 3.0.0 | .NET 8 GUI, MVVM rewrite |

---

**License:** Apache 2.0
