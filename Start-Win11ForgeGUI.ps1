<#
.SYNOPSIS
    Win11Forge GUI Launcher v3.0.0

.DESCRIPTION
    Launches the Win11Forge graphical user interface.
    Prefers the WPF GUI (Win11Forge.GUI.exe) if available,
    otherwise falls back to the PowerShell-based GUI.

.PARAMETER CLI
    Force CLI mode instead of GUI.

.PARAMETER Legacy
    Force the legacy PowerShell GUI instead of WPF.

.PARAMETER SkipModuleCheck
    Skip module verification (legacy mode only).

.EXAMPLE
    .\Start-Win11ForgeGUI.ps1
    .\Start-Win11ForgeGUI.ps1 -Legacy
    .\Start-Win11ForgeGUI.ps1 -CLI

.NOTES
    Author: Julien Bombled
    Version: 3.0.0
    Requires: Administrator privileges, PowerShell 5.1+
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$CLI,

    [Parameter()]
    [switch]$Legacy,

    [Parameter()]
    [switch]$SkipModuleCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# SCRIPT INITIALIZATION
# ============================================================================

$script:ScriptRoot = $PSScriptRoot

# Check for WPF GUI executable in various locations
$wpfGuiPaths = @(
    (Join-Path $script:ScriptRoot 'Win11Forge.GUI.exe'),
    (Join-Path $script:ScriptRoot 'GUI\Win11Forge.GUI\bin\Debug\net8.0-windows\Win11Forge.GUI.exe'),
    (Join-Path $script:ScriptRoot 'GUI\Win11Forge.GUI\bin\Release\net8.0-windows\Win11Forge.GUI.exe'),
    (Join-Path $script:ScriptRoot 'GUI\Win11Forge.GUI\bin\publish\Win11Forge.GUI.exe')
)

$wpfGuiExe = $null
foreach ($path in $wpfGuiPaths) {
    if (Test-Path $path) {
        $wpfGuiExe = $path
        break
    }
}

$script:GUIModule = Join-Path $script:ScriptRoot 'Modules\Win11ForgeGUI.psm1'

# ============================================================================
# VERSION DISPLAY
# ============================================================================

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
try {
    $versionFile = Join-Path $script:ScriptRoot 'Config\version.json'
    if (Test-Path $versionFile) {
        $FrameworkVersion = (Get-Content -Path $versionFile -Raw -Encoding UTF8 | ConvertFrom-Json).Version
    } else {
        $FrameworkVersion = '3.0.0'
    }
} catch {
    $FrameworkVersion = '3.0.0'
}
Write-Host "  Win11Forge v$FrameworkVersion" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# CLI MODE
# ============================================================================

if ($CLI) {
    $cliScript = Join-Path $script:ScriptRoot 'Win11Forge.ps1'
    if (Test-Path $cliScript) {
        Write-Host "Launching CLI mode..." -ForegroundColor Yellow
        & $cliScript @args
        exit $LASTEXITCODE
    } else {
        Write-Host "ERROR: CLI script not found at $cliScript" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# WPF GUI MODE (DEFAULT)
# ============================================================================

if ($wpfGuiExe -and -not $Legacy) {
    Write-Host "Launching WPF GUI..." -ForegroundColor Green
    Write-Host "  Path: $wpfGuiExe" -ForegroundColor Gray
    Write-Host ""

    # Launch the WPF application
    Start-Process -FilePath $wpfGuiExe -WorkingDirectory $script:ScriptRoot
    exit 0
}

# ============================================================================
# LEGACY POWERSHELL GUI (FALLBACK)
# ============================================================================

Write-Host "WPF GUI not found, falling back to legacy PowerShell GUI..." -ForegroundColor Yellow
Write-Host ""

# Administrator check for legacy mode
function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host ""
    Write-Host "ERROR: Administrator privileges required" -ForegroundColor Red
    Write-Host "Please run this script as Administrator" -ForegroundColor Red
    Write-Host ""
    Write-Host "Right-click on PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Initializing..." -ForegroundColor Yellow
Write-Host "  - PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Gray

if ($PSVersionTable.PSVersion.Major -lt 5) {
    Write-Host ""
    Write-Host "ERROR: PowerShell 5.1 or higher required" -ForegroundColor Red
    Write-Host "Current version: $($PSVersionTable.PSVersion)" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

if ($PSVersionTable.PSVersion.Major -ge 7) {
    Write-Host "  - PowerShell 7+ detected - Parallel mode available" -ForegroundColor Green
}
else {
    Write-Host "  - PowerShell 5.x - Sequential mode only" -ForegroundColor Yellow
}

# ============================================================================
# MODULE LOADING (LEGACY)
# ============================================================================

Write-Host ""
Write-Host "Loading modules..." -ForegroundColor Yellow

if (-not (Test-Path $script:GUIModule)) {
    Write-Host ""
    Write-Host "ERROR: GUI module not found" -ForegroundColor Red
    Write-Host "Expected: $script:GUIModule" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please build the WPF GUI or ensure legacy modules are installed." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To build the WPF GUI:" -ForegroundColor Cyan
    Write-Host "  .\Build-Release.ps1" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Force unload cached modules
Get-Module -Name Core,EnvironmentDetection,Prerequisites,ProfileManager,ApplicationDatabase,InstallationEngine,SystemConfig,Win11ForgeGUI | Remove-Module -Force -ErrorAction SilentlyContinue

try {
    Import-Module $script:GUIModule -Force
    Write-Host "  [OK] Win11ForgeGUI module loaded" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "ERROR: Failed to load GUI module" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "  - Initializing framework modules..." -ForegroundColor Gray

$initResult = Initialize-GUIModules

if (-not $initResult) {
    Write-Host ""
    Write-Host "ERROR: Failed to initialize framework modules" -ForegroundColor Red
    Write-Host "Please ensure all modules are present in the Modules directory." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "  [OK] All modules initialized successfully" -ForegroundColor Green

# ============================================================================
# VERIFICATION (LEGACY)
# ============================================================================

if (-not $SkipModuleCheck) {
    Write-Host ""
    Write-Host "Verifying installation..." -ForegroundColor Yellow

    $dbPath = Join-Path $script:ScriptRoot 'Apps\Database\applications.json'
    if (-not (Test-Path $dbPath)) {
        Write-Host ""
        Write-Host "WARNING: Application database not found" -ForegroundColor Yellow
        Write-Host "Expected: $dbPath" -ForegroundColor Yellow
        Write-Host ""
        $continue = Read-Host "Continue anyway? (Y/N)"
        if ($continue -ne 'Y' -and $continue -ne 'y') {
            exit 0
        }
    }
    else {
        $appCount = ((Get-Content $dbPath -Raw | ConvertFrom-Json).applications).Count
        Write-Host "  [OK] Application database found ($appCount apps)" -ForegroundColor Green
    }

    $profilesPath = Join-Path $script:ScriptRoot 'Profiles'
    if (Test-Path $profilesPath) {
        $profileCount = (Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }).Count
        Write-Host "  [OK] Profiles directory found ($profileCount profiles)" -ForegroundColor Green
    }
    else {
        Write-Host "  [WARN] Profiles directory not found" -ForegroundColor Yellow
    }
}

# ============================================================================
# LAUNCH LEGACY GUI
# ============================================================================

Write-Host ""
Write-Host "Starting legacy GUI..." -ForegroundColor Green
Write-Host ""

Start-Sleep -Milliseconds 500

try {
    Show-MainMenu
}
catch {
    Write-Host ""
    Write-Host "ERROR: GUI crashed" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "Win11Forge GUI closed." -ForegroundColor Gray
Write-Host ""

exit 0
