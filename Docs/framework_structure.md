# Win11Forge Framework - Directory Structure

## Complete Directory Tree

```
Win11Forge/
│
├── Deploy-Win11Forge.bat                 ✅ Batch launcher with menu
├── Deploy-Win11Environment.ps1           ✅ Main PowerShell deployment script
├── README.md                             ✅ Complete documentation
├── STRUCTURE.md                          📄 This file
│
├── Core/
│   └── Core.psm1                         ✅ Core functions module
│       ├── Logging functions
│       ├── Error handling
│       ├── Validation functions
│       └── Utility functions
│
├── Modules/
│   ├── Prerequisites.psm1                ✅ Prerequisites installation
│   │   ├── Chocolatey
│   │   ├── PowerShell 7
│   │   ├── .NET Runtimes
│   │   ├── VC++ Redistributables
│   │   ├── Java Runtime
│   │   └── Environment refresh
│   │
│   ├── EnvironmentDetection.psm1        ✅ Environment detection
│   │   ├── Windows Sandbox detection
│   │   ├── VMware detection
│   │   ├── Hyper-V detection
│   │   ├── VirtualBox detection
│   │   └── Capabilities assessment
│   │
│   ├── ProfileManager.psm1              ✅ Profile management
│   │   ├── JSON loading
│   │   ├── Inheritance resolution
│   │   ├── Application merging
│   │   └── Configuration merging
│   │
│   ├── InstallationEngine.psm1          ✅ Installation engine
│   │   ├── Application detection
│   │   ├── Winget installation
│   │   ├── Chocolatey installation
│   │   ├── Microsoft Store installation
│   │   ├── Direct download installation
│   │   ├── Windows Features
│   │   └── Windows Capabilities
│   │
│   └── SystemConfig.psm1                ✅ System configuration
│       ├── Explorer settings
│       ├── Taskbar configuration
│       ├── Network settings (DNS)
│       ├── Privacy settings
│       ├── Performance optimization
│       └── Security settings
│
├── Profiles/
│   ├── Base.json                        ✅ Base profile (31 apps)
│   ├── Office.json                      ✅ Office profile (Base + 5)
│   ├── Gaming.json                      ✅ Gaming profile (Office + 4)
│   └── Personnel.json                   ✅ Personnel profile (Gaming + 33)
│
└── Logs/                                📁 Auto-created
    └── deployment_YYYYMMDD_HHMMSS.log   📄 Generated logs
```

## Module Dependencies

```
Deploy-Win11Environment.ps1
    │
    ├─► Core.psm1 (required first)
    │   └─► All other modules depend on this
    │
    ├─► EnvironmentDetection.psm1
    │   └─► Requires: Core.psm1
    │
    ├─► Prerequisites.psm1
    │   └─► Requires: Core.psm1
    │
    ├─► ProfileManager.psm1
    │   └─► Requires: Core.psm1
    │
    ├─► InstallationEngine.psm1
    │   └─► Requires: Core.psm1
    │
    └─► SystemConfig.psm1
        └─► Requires: Core.psm1
```

## File Creation Checklist

### ✅ Already Created (via artifacts)
- [x] Deploy-Win11Forge.bat
- [x] Deploy-Win11Environment.ps1
- [x] README.md
- [x] Core/Core.psm1
- [x] Modules/Prerequisites.psm1 (Enhanced)
- [x] Modules/EnvironmentDetection.psm1
- [x] Modules/ProfileManager.psm1
- [x] Modules/InstallationEngine.psm1
- [x] Modules/SystemConfig.psm1
- [x] Profiles/Base.json (Updated)

### 📋 Need to be copied from your documents
- [ ] Profiles/Office.json
- [ ] Profiles/Gaming.json
- [ ] Profiles/Personnel.json

### 📁 Directories to Create
```powershell
# Run this in PowerShell to create directory structure
New-Item -ItemType Directory -Path "Win11Forge" -Force
New-Item -ItemType Directory -Path "Win11Forge\Core" -Force
New-Item -ItemType Directory -Path "Win11Forge\Modules" -Force
New-Item -ItemType Directory -Path "Win11Forge\Profiles" -Force
New-Item -ItemType Directory -Path "Win11Forge\Logs" -Force
```

## Installation Steps

### 1. Create Directory Structure
```powershell
# Create main directory
New-Item -ItemType Directory -Path "C:\Win11Forge" -Force

# Create subdirectories
@('Core', 'Modules', 'Profiles', 'Logs') | ForEach-Object {
    New-Item -ItemType Directory -Path "C:\Win11Forge\$_" -Force
}
```

### 2. Copy Files

#### Main Scripts
- Copy `Deploy-Win11Forge.bat` → `C:\Win11Forge\`
- Copy `Deploy-Win11Environment.ps1` → `C:\Win11Forge\`
- Copy `README.md` → `C:\Win11Forge\`

#### Core Module
- Copy `Core.psm1` → `C:\Win11Forge\Core\`

#### Framework Modules
- Copy `Prerequisites.psm1` → `C:\Win11Forge\Modules\`
- Copy `EnvironmentDetection.psm1` → `C:\Win11Forge\Modules\`
- Copy `ProfileManager.psm1` → `C:\Win11Forge\Modules\`
- Copy `InstallationEngine.psm1` → `C:\Win11Forge\Modules\`
- Copy `SystemConfig.psm1` → `C:\Win11Forge\Modules\`

#### Profiles
- Copy `Base.json` → `C:\Win11Forge\Profiles\`
- Copy `Office.json` → `C:\Win11Forge\Profiles\`
- Copy `Gaming.json` → `C:\Win11Forge\Profiles\`
- Copy `Personnel.json` → `C:\Win11Forge\Profiles\`

### 3. Verify Installation
```powershell
# Run validation script
C:\Win11Forge\Deploy-Win11Environment.ps1 -ProfileName "Base" -TestMode
```

## File Sizes Reference

| File | Approx Size | Lines |
|------|-------------|-------|
| Core.psm1 | ~15 KB | ~450 |
| Prerequisites.psm1 | ~20 KB | ~600 |
| EnvironmentDetection.psm1 | ~12 KB | ~350 |
| ProfileManager.psm1 | ~15 KB | ~450 |
| InstallationEngine.psm1 | ~18 KB | ~550 |
| SystemConfig.psm1 | ~16 KB | ~500 |
| Deploy-Win11Environment.ps1 | ~12 KB | ~350 |
| Deploy-Win11Forge.bat | ~2 KB | ~80 |

## Version Control

If using Git:

```bash
# Initialize repository
cd C:\Win11Forge
git init

# Create .gitignore
echo "Logs/
*.log
.vs/
.vscode/" > .gitignore

# First commit
git add .
git commit -m "Initial commit: Win11Forge Framework v2.0"
```

## Module Loading Order

The framework loads modules in this specific order:

1. **Core.psm1** - Must be first (provides base functions)
2. **EnvironmentDetection.psm1** - Detects environment type
3. **Prerequisites.psm1** - Installs prerequisites
4. **ProfileManager.psm1** - Loads and merges profiles
5. **InstallationEngine.psm1** - Installs applications
6. **SystemConfig.psm1** - Applies system configuration

## Testing Workflow

### Phase 1: Windows Sandbox
```powershell
# Test in Sandbox (non-persistent, safe)
.\Deploy-Win11Forge.bat
# Select: 6. Test Mode
```

### Phase 2: VM Testing
```powershell
# Test in VMware/Hyper-V
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -Verbose
```

### Phase 3: Production
```powershell
# Deploy on physical machine
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel"
```

## Troubleshooting

### Common Issues

**Module not found:**
```powershell
# Verify module exists
Test-Path "C:\Win11Forge\Core\Core.psm1"
Test-Path "C:\Win11Forge\Modules\Prerequisites.psm1"
```

**Permission denied:**
```powershell
# Run as Administrator
# Right-click → "Run as Administrator"
```

**Execution policy:**
```powershell
# Set execution policy
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

## Support Files

Additional files you may want to create:

- `CHANGELOG.md` - Version history
- `CONTRIBUTING.md` - Contribution guidelines
- `LICENSE` - License information
- `.gitignore` - Git exclusions
- `TESTING.md` - Testing procedures

---

**Framework Version:** 2.0.0  
**Last Updated:** 2025-01-15  
**Status:** Production Ready ✅
