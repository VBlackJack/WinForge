<#
.SYNOPSIS
    Win11Forge GUI Launcher

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
    Requires: Administrator privileges, PowerShell 5.1+
#>

#
# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

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

# Import Localization module
$localizationModule = Join-Path $script:ScriptRoot 'Core\Localization.psm1'
if (Test-Path $localizationModule) {
    Import-Module $localizationModule -Force
    Initialize-Localization
}

# Helper function for localization (fallback if module not loaded)
function Get-Text {
    param([string]$Key, [hashtable]$Parameters = @{}, [string]$Default = $Key)
    if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
        return Get-LocalizedString -Key $Key -Parameters $Parameters -DefaultValue $Default
    }
    return $Default
}

function Get-FrameworkVersion {
    $versionFile = Join-Path $script:ScriptRoot 'Config\version.json'
    if (-not (Test-Path -Path $versionFile)) {
        throw "Version file not found: $versionFile"
    }

    $versionData = Get-Content -Path $versionFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$versionData.Version)) {
        throw "Version property missing in $versionFile"
    }

    return [string]$versionData.Version
}

function New-WpfGuiCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [int]$Priority
    )

    [pscustomobject]@{
        Name = $Name
        Path = Join-Path $script:ScriptRoot $RelativePath
        Priority = $Priority
    }
}

function Resolve-WpfGuiExecutable {
    $wpfGuiCandidates = @(
        New-WpfGuiCandidate -Name 'packaged root' -RelativePath 'Win11Forge.GUI.exe' -Priority 0
        New-WpfGuiCandidate -Name 'source Release' -RelativePath 'GUI\Win11Forge.GUI\bin\Release\net10.0-windows\Win11Forge.GUI.exe' -Priority 10
        New-WpfGuiCandidate -Name 'source publish' -RelativePath 'GUI\Win11Forge.GUI\bin\publish\Win11Forge.GUI.exe' -Priority 20
        New-WpfGuiCandidate -Name 'source Debug' -RelativePath 'GUI\Win11Forge.GUI\bin\Debug\net10.0-windows\Win11Forge.GUI.exe' -Priority 30
    )

    $existingCandidates = @(
        foreach ($candidate in $wpfGuiCandidates) {
            if (Test-Path -LiteralPath $candidate.Path -PathType Leaf) {
                $item = Get-Item -LiteralPath $candidate.Path
                [pscustomobject]@{
                    Name = $candidate.Name
                    Path = $item.FullName
                    Priority = $candidate.Priority
                    LastWriteTimeUtc = $item.LastWriteTimeUtc
                }
            }
        }
    )

    if ($existingCandidates.Count -eq 0) {
        return $null
    }

    return $existingCandidates |
        Sort-Object -Property @{ Expression = 'Priority'; Ascending = $true }, @{ Expression = 'LastWriteTimeUtc'; Descending = $true } |
        Select-Object -First 1
}

function Get-NewestGuiSourceWriteTimeUtc {
    $guiSourceRoot = Join-Path $script:ScriptRoot 'GUI\Win11Forge.GUI'
    if (-not (Test-Path -LiteralPath $guiSourceRoot)) {
        return $null
    }

    $sourceExtensions = @('.cs', '.xaml', '.resx', '.csproj', '.props', '.targets', '.json', '.manifest')
    $sourceFiles = @(
        Get-ChildItem -LiteralPath $guiSourceRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $sourceExtensions -contains $_.Extension.ToLowerInvariant() -and
                $_.FullName -notmatch '\\(bin|obj)\\'
            }
    )

    if ($sourceFiles.Count -eq 0) {
        return $null
    }

    return ($sourceFiles | Sort-Object -Property LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

$wpfGuiCandidate = Resolve-WpfGuiExecutable
$wpfGuiExe = if ($wpfGuiCandidate) { $wpfGuiCandidate.Path } else { $null }
$script:GUIModule = Join-Path $script:ScriptRoot 'Modules\Win11ForgeGUI.psm1'

# ============================================================================
# VERSION DISPLAY
# ============================================================================

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
try {
    $FrameworkVersion = Get-FrameworkVersion
} catch {
    Write-Host "$(Get-Text -Key 'launcher.error.version_not_found' -Default 'ERROR: Unable to load Win11Forge version'): $($_.Exception.Message)" -ForegroundColor Red
    exit 1
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
        Write-Host (Get-Text -Key 'launcher.launching_cli' -Default 'Launching CLI mode...') -ForegroundColor Yellow
        & $cliScript @args
        exit $LASTEXITCODE
    } else {
        Write-Host "$(Get-Text -Key 'launcher.error.module_not_found' -Default 'ERROR'): $(Get-Text -Key 'launcher.cli_not_found' -Parameters @{ Path = $cliScript } -Default "CLI script not found at $cliScript")" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# WPF GUI MODE (DEFAULT)
# ============================================================================

if ($wpfGuiExe -and -not $Legacy) {
    Write-Host (Get-Text -Key 'launcher.launching_wpf' -Default 'Launching WPF GUI...') -ForegroundColor Green
    Write-Host "  $(Get-Text -Key 'launcher.path_label' -Parameters @{ Path = $wpfGuiExe } -Default "Path: $wpfGuiExe")" -ForegroundColor Gray
    Write-Host "  $($wpfGuiCandidate.Name)" -ForegroundColor Gray

    $newestGuiSourceWriteTimeUtc = Get-NewestGuiSourceWriteTimeUtc
    if ($newestGuiSourceWriteTimeUtc -and $wpfGuiCandidate.LastWriteTimeUtc -lt $newestGuiSourceWriteTimeUtc) {
        Write-Host "  WARNING: selected WPF executable is older than the newest GUI source file. Rebuild the GUI if the app looks stale." -ForegroundColor Yellow
    }

    Write-Host ""

    # Launch the WPF application
    Start-Process -FilePath $wpfGuiExe -WorkingDirectory $script:ScriptRoot
    exit 0
}

# ============================================================================
# LEGACY POWERSHELL GUI (FALLBACK)
# ============================================================================

Write-Host (Get-Text -Key 'launcher.fallback_legacy' -Default 'WPF GUI not found, falling back to legacy PowerShell GUI...') -ForegroundColor Yellow
Write-Host ""

# Administrator check for legacy mode
function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.admin_required' -Default 'ERROR: Administrator privileges required')" -ForegroundColor Red
    Write-Host (Get-Text -Key 'launcher.error.run_as_admin' -Default 'Please run this script as Administrator') -ForegroundColor Red
    Write-Host ""
    Write-Host (Get-Text -Key 'launcher.run_as_admin_hint' -Default "Right-click on PowerShell and select 'Run as Administrator'") -ForegroundColor Yellow
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

Write-Host (Get-Text -Key 'launcher.initializing' -Default 'Initializing...') -ForegroundColor Yellow
Write-Host "  - $(Get-Text -Key 'launcher.ps_version' -Parameters @{ Version = $PSVersionTable.PSVersion } -Default "PowerShell Version: $($PSVersionTable.PSVersion)")" -ForegroundColor Gray

if ($PSVersionTable.PSVersion.Major -lt 5) {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.ps_version_required' -Default 'ERROR: PowerShell 5.1 or higher required')" -ForegroundColor Red
    Write-Host "$(Get-Text -Key 'launcher.current_version' -Parameters @{ Version = $PSVersionTable.PSVersion } -Default "Current version: $($PSVersionTable.PSVersion)")" -ForegroundColor Red
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

if ($PSVersionTable.PSVersion.Major -ge 7) {
    Write-Host "  - $(Get-Text -Key 'launcher.ps7_available' -Default 'PowerShell 7+ detected - Parallel mode available')" -ForegroundColor Green
}
else {
    Write-Host "  - $(Get-Text -Key 'launcher.ps5_only' -Default 'PowerShell 5.x - Sequential mode only')" -ForegroundColor Yellow
}

# ============================================================================
# MODULE LOADING (LEGACY)
# ============================================================================

Write-Host ""
Write-Host (Get-Text -Key 'launcher.loading_modules' -Default 'Loading modules...') -ForegroundColor Yellow

if (-not (Test-Path $script:GUIModule)) {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.module_not_found' -Default 'ERROR: GUI module not found')" -ForegroundColor Red
    Write-Host "$(Get-Text -Key 'launcher.expected_path' -Parameters @{ Path = $script:GUIModule } -Default "Expected: $script:GUIModule")" -ForegroundColor Red
    Write-Host ""
    Write-Host (Get-Text -Key 'launcher.build_or_legacy' -Default 'Please build the WPF GUI or ensure legacy modules are installed.') -ForegroundColor Yellow
    Write-Host ""
    Write-Host (Get-Text -Key 'launcher.build_wpf_hint' -Default 'To build the WPF GUI:') -ForegroundColor Cyan
    Write-Host "  $(Get-Text -Key 'launcher.build_wpf_cmd' -Default '.\Build-Release.ps1')" -ForegroundColor Cyan
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

# Force unload cached modules
Get-Module -Name Core,EnvironmentDetection,Prerequisites,ProfileManager,ApplicationDatabase,InstallationEngine,SystemConfig,Win11ForgeGUI | Remove-Module -Force -ErrorAction SilentlyContinue

try {
    Import-Module $script:GUIModule -Force
    Write-Host "  [OK] $(Get-Text -Key 'launcher.module_loaded' -Default 'Win11ForgeGUI module loaded')" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.module_load_failed' -Default 'ERROR: Failed to load GUI module')" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

Write-Host "  - $(Get-Text -Key 'launcher.init_modules' -Default 'Initializing framework modules...')" -ForegroundColor Gray

$initResult = Initialize-GUIModules

if (-not $initResult) {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.init_failed' -Default 'ERROR: Failed to initialize framework modules')" -ForegroundColor Red
    Write-Host (Get-Text -Key 'launcher.ensure_modules' -Default 'Please ensure all modules are present in the Modules directory.') -ForegroundColor Yellow
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

Write-Host "  [OK] $(Get-Text -Key 'launcher.modules_loaded' -Default 'All modules initialized successfully')" -ForegroundColor Green

# ============================================================================
# VERIFICATION (LEGACY)
# ============================================================================

if (-not $SkipModuleCheck) {
    Write-Host ""
    Write-Host (Get-Text -Key 'launcher.verifying' -Default 'Verifying installation...') -ForegroundColor Yellow

    $dbPath = Join-Path $script:ScriptRoot 'Apps\Database\applications.json'
    if (-not (Test-Path $dbPath)) {
        Write-Host ""
        Write-Host "$(Get-Text -Key 'launcher.warning.db_not_found' -Default 'WARNING: Application database not found')" -ForegroundColor Yellow
        Write-Host "$(Get-Text -Key 'launcher.expected_path' -Parameters @{ Path = $dbPath } -Default "Expected: $dbPath")" -ForegroundColor Yellow
        Write-Host ""
        $continue = Read-Host (Get-Text -Key 'launcher.continue_prompt' -Default 'Continue anyway? (Y/N)')
        if ($continue -ne 'Y' -and $continue -ne 'y') {
            exit 0
        }
    }
    else {
        $appCount = ((Get-Content $dbPath -Raw | ConvertFrom-Json).applications).Count
        Write-Host "  [OK] $(Get-Text -Key 'launcher.db_found' -Parameters @{ Count = $appCount } -Default "Application database found ($appCount apps)")" -ForegroundColor Green
    }

    $profilesPath = Join-Path $script:ScriptRoot 'Profiles'
    if (Test-Path $profilesPath) {
        $profileCount = (Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }).Count
        Write-Host "  [OK] $(Get-Text -Key 'launcher.profiles_found' -Parameters @{ Count = $profileCount } -Default "Profiles directory found ($profileCount profiles)")" -ForegroundColor Green
    }
    else {
        Write-Host "  [WARN] $(Get-Text -Key 'launcher.warning.profiles_not_found' -Default 'Profiles directory not found')" -ForegroundColor Yellow
    }
}

# ============================================================================
# LAUNCH LEGACY GUI
# ============================================================================

Write-Host ""
Write-Host (Get-Text -Key 'launcher.launching_legacy' -Default 'Starting legacy GUI...') -ForegroundColor Green
Write-Host ""

Start-Sleep -Milliseconds 500

try {
    Show-MainMenu
}
catch {
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.error.gui_crashed' -Default 'ERROR: GUI crashed')" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "$(Get-Text -Key 'launcher.stack_trace' -Default 'Stack Trace:')" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    Write-Host ""
    Read-Host (Get-Text -Key 'launcher.press_enter' -Default 'Press Enter to exit')
    exit 1
}

Write-Host ""
Write-Host (Get-Text -Key 'launcher.gui_closed' -Default 'Win11Forge GUI closed.') -ForegroundColor Gray
Write-Host ""

exit 0
