<#
.SYNOPSIS
    Win11Forge Framework Setup Script

.DESCRIPTION
    Automated setup script for Win11Forge framework:
    - Creates directory structure
    - Downloads/copies required files
    - Validates installation
    - Runs initial tests

.PARAMETER InstallPath
    Installation path (default: C:\Win11Forge)

.PARAMETER SourcePath
    Source path containing framework files

.PARAMETER SkipValidation
    Skip validation after installation

.EXAMPLE
    .\Setup-Framework.ps1

.EXAMPLE
    .\Setup-Framework.ps1 -InstallPath "D:\Tools\Win11Forge"

.EXAMPLE
    .\Setup-Framework.ps1 -SourcePath "E:\Downloads\Win11Forge"

.NOTES
    Author: Julien Bombled
    Version: 2.4.0
    Requires: Administrator privileges
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$InstallPath = 'C:\Win11Forge',

    [Parameter()]
    [string]$SourcePath,

    [Parameter()]
    [switch]$SkipValidation
)

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# === HELPER FUNCTIONS ===

function Write-SetupStatus {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
    }

    $prefix = switch ($Level) {
        'Info'    { '[INFO]' }
        'Success' { '[OK]' }
        'Warning' { '[WARN]' }
        'Error'   { '[ERROR]' }
    }

    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Test-AdminPrivileges {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# === SETUP BANNER ===

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        Win11Forge Framework Setup v2.3.0                    ║" -ForegroundColor Cyan
Write-Host "║        Automated Installation & Configuration               ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# === PREREQUISITES CHECK ===

Write-SetupStatus "Checking prerequisites..." -Level Info

if (-not (Test-AdminPrivileges)) {
    Write-SetupStatus "Administrator privileges required!" -Level Error
    Write-SetupStatus "Please run this script as Administrator" -Level Error
    exit 1
}

Write-SetupStatus "Administrator privileges confirmed" -Level Success

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
Write-SetupStatus "PowerShell version: $psVersion" -Level Info

if ($psVersion.Major -lt 5) {
    Write-SetupStatus "PowerShell 5.1 or higher required!" -Level Error
    exit 1
}

# === DIRECTORY STRUCTURE CREATION ===

Write-Host ""
Write-SetupStatus "Creating directory structure..." -Level Info

$directories = @(
    $InstallPath,
    (Join-Path $InstallPath 'Core'),
    (Join-Path $InstallPath 'Modules'),
    (Join-Path $InstallPath 'Profiles'),
    (Join-Path $InstallPath 'Tools'),
    (Join-Path $InstallPath 'Logs')
)

foreach ($dir in $directories) {
    if (Test-Path -Path $dir) {
        Write-SetupStatus "Directory exists: $dir" -Level Warning
    } else {
        try {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-SetupStatus "Created: $dir" -Level Success
        } catch {
            Write-SetupStatus "Failed to create: $dir - $($_.Exception.Message)" -Level Error
            exit 1
        }
    }
}

# === FILE STRUCTURE DEFINITION ===

$fileStructure = @{
    'Root' = @(
        'Deploy-Win11Forge.bat',
        'Deploy-Win11Environment.ps1',
        'README.md',
        'CHANGELOG.md',
        'PROJET_STRUCTURE.md'
    )
    'Core' = @(
        'Core.psm1'
    )
    'Modules' = @(
        'Prerequisites.psm1',
        'EnvironmentDetection.psm1',
        'ProfileManager.psm1',
        'InstallationEngine.psm1',
        'SystemConfig.psm1'
    )
    'Profiles' = @(
        'Base.json',
        'Office.json',
        'Gaming.json',
        'Personnel.json'
    )
    'Tools' = @(
        'Validate-Framework.ps1',
        'Search-ApplicationSources.ps1',
        'System-Audit.ps1',
        'Launch-SystemAudit.bat',
        'Launch-AsTrustedInstaller.bat',
        'README.md',
        'System-Audit-README.md'
    )
}

# === FILE COPYING ===

Write-Host ""
Write-SetupStatus "Copying framework files..." -Level Info

$totalFiles = 0
$copiedFiles = 0
$skippedFiles = 0
$failedFiles = 0

# Determine source
if (-not $SourcePath) {
    $SourcePath = $PSScriptRoot
}

if (-not (Test-Path -Path $SourcePath)) {
    Write-SetupStatus "Source path not found: $SourcePath" -Level Error
    Write-SetupStatus "Please specify -SourcePath parameter" -Level Error
    exit 1
}

Write-SetupStatus "Source path: $SourcePath" -Level Info

foreach ($category in $fileStructure.Keys) {
    $destPath = if ($category -eq 'Root') { $InstallPath } else { Join-Path $InstallPath $category }
    
    foreach ($file in $fileStructure[$category]) {
        $totalFiles++
        
        # Try to find source file
        $sourceFile = $null
        $possiblePaths = @(
            (Join-Path $SourcePath $file),
            (Join-Path $SourcePath "$category\$file")
        )
        
        foreach ($path in $possiblePaths) {
            if (Test-Path -Path $path) {
                $sourceFile = $path
                break
            }
        }
        
        $destFile = Join-Path $destPath $file
        
        if ($sourceFile) {
            try {
                Copy-Item -Path $sourceFile -Destination $destFile -Force
                Write-SetupStatus "Copied: $file" -Level Success
                $copiedFiles++
            } catch {
                Write-SetupStatus "Failed to copy: $file - $($_.Exception.Message)" -Level Error
                $failedFiles++
            }
        } else {
            Write-SetupStatus "Not found: $file (will need manual copy)" -Level Warning
            $skippedFiles++
        }
    }
}

# === COPY SUMMARY ===

Write-Host ""
Write-SetupStatus "File copy summary:" -Level Info
Write-Host "  Total files:   $totalFiles"
Write-Host "  Copied:        $copiedFiles" -ForegroundColor Green
Write-Host "  Skipped:       $skippedFiles" -ForegroundColor Yellow
Write-Host "  Failed:        $failedFiles" -ForegroundColor Red

if ($skippedFiles -gt 0) {
    Write-Host ""
    Write-SetupStatus "Some files were not found in source" -Level Warning
    Write-SetupStatus "You may need to copy them manually" -Level Warning
    Write-SetupStatus "Required files are listed in PROJET_STRUCTURE.md" -Level Info
}

# === PERMISSIONS CHECK ===

Write-Host ""
Write-SetupStatus "Checking permissions..." -Level Info

try {
    $testFile = Join-Path $InstallPath "test_$(Get-Random).tmp"
    "test" | Out-File -FilePath $testFile -Force
    Remove-Item -Path $testFile -Force
    Write-SetupStatus "Write permissions confirmed" -Level Success
} catch {
    Write-SetupStatus "Insufficient write permissions!" -Level Error
    exit 1
}

# === CREATE DESKTOP SHORTCUT ===

Write-Host ""
$createShortcut = Read-Host "Create desktop shortcut? (Y/N)"

if ($createShortcut -match '^[Yy]') {
    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Win11Forge.lnk'
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $InstallPath 'Deploy-Win11Forge.bat'
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.Description = 'Win11Forge Framework Launcher'
        $shortcut.Save()
        
        Write-SetupStatus "Desktop shortcut created" -Level Success
    } catch {
        Write-SetupStatus "Failed to create shortcut: $($_.Exception.Message)" -Level Warning
    }
}

# === VALIDATION ===

if (-not $SkipValidation) {
    Write-Host ""
    Write-SetupStatus "Running framework validation..." -Level Info
    Write-Host ""

    $validationScript = Join-Path $InstallPath 'Tools\Validate-Framework.ps1'

    if (Test-Path -Path $validationScript) {
        try {
            & $validationScript
        } catch {
            Write-SetupStatus "Validation encountered errors" -Level Warning
        }
    } else {
        Write-SetupStatus "Validation script not found - skipping validation" -Level Warning
    }
}

# === COMPLETION ===

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║           Setup Completed Successfully!                     ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-SetupStatus "Installation path: $InstallPath" -Level Info
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review configuration in Profiles/*.json"
Write-Host "  2. Run: .\Deploy-Win11Environment.ps1 -ProfileName 'Base' -TestMode"
Write-Host "  3. For full deployment: .\Deploy-Win11Forge.bat"
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Cyan
Write-Host "  • README.md            - Complete documentation"
Write-Host "  • PROJET_STRUCTURE.md  - Framework architecture"
Write-Host "  • CHANGELOG.md         - Version history"
Write-Host ""

# Open documentation
$openDocs = Read-Host "Open README.md now? (Y/N)"

if ($openDocs -match '^[Yy]') {
    $readmePath = Join-Path $InstallPath 'README.md'
    if (Test-Path -Path $readmePath) {
        Start-Process -FilePath $readmePath
    }
}

Write-Host ""
Write-SetupStatus "Setup complete! Happy deploying! 🚀" -Level Success
Write-Host ""
