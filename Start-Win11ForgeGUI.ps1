<#
.SYNOPSIS
    Win11Forge GUI Launcher v1.0.0

.DESCRIPTION
    Launches the Win11Forge graphical user interface for interactive deployment management

.PARAMETER SkipModuleCheck
    Skip module verification and load directly

.EXAMPLE
    .\Start-Win11ForgeGUI.ps1

.NOTES
    Version: 1.0.0
    Requires: Administrator privileges, PowerShell 5.1+, Win11Forge v2.2.0+
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$SkipModuleCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# SCRIPT INITIALIZATION
# ============================================================================

$script:ScriptRoot = $PSScriptRoot
$script:GUIModule = Join-Path $script:ScriptRoot 'Modules\Win11ForgeGUI.psm1'

# ============================================================================
# ADMINISTRATOR CHECK
# ============================================================================

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

# ============================================================================
# VERSION CHECK
# ============================================================================

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Win11Forge v2.2.0 - Graphical User Interface" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

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
    Write-Host "    TIP: Install PowerShell 7 for parallel deployment" -ForegroundColor Gray
}

# ============================================================================
# MODULE LOADING
# ============================================================================

Write-Host ""
Write-Host "Loading modules..." -ForegroundColor Yellow

# Check if GUI module exists
if (-not (Test-Path $script:GUIModule)) {
    Write-Host ""
    Write-Host "ERROR: GUI module not found" -ForegroundColor Red
    Write-Host "Expected: $script:GUIModule" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure Win11Forge is properly installed." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Load GUI module
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

# Initialize required modules
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
# VERIFICATION
# ============================================================================

if (-not $SkipModuleCheck) {
    Write-Host ""
    Write-Host "Verifying installation..." -ForegroundColor Yellow

    # Check database
    $dbPath = Join-Path $script:ScriptRoot 'Apps\Database\applications.json'
    if (-not (Test-Path $dbPath)) {
        Write-Host ""
        Write-Host "WARNING: Application database not found" -ForegroundColor Yellow
        Write-Host "Expected: $dbPath" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Some features may not work correctly." -ForegroundColor Yellow
        Write-Host ""
        $continue = Read-Host "Continue anyway? (Y/N)"
        if ($continue -ne 'Y' -and $continue -ne 'y') {
            exit 0
        }
    }
    else {
        Write-Host "  [OK] Application database found (66 apps)" -ForegroundColor Green
    }

    # Check profiles
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
# LAUNCH GUI
# ============================================================================

Write-Host ""
Write-Host "Starting GUI..." -ForegroundColor Green
Write-Host ""

Start-Sleep -Milliseconds 500

try {
    # Start main menu
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

# Clean exit
Write-Host ""
Write-Host "Win11Forge GUI closed." -ForegroundColor Gray
Write-Host ""

exit 0
