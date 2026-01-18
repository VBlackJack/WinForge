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
    Version: 2.5.0
    Requires: Administrator privileges
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
    [string]$InstallPath = 'C:\Win11Forge',

    [Parameter()]
    [string]$SourcePath,

    [Parameter()]
    [switch]$SkipValidation
)

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# === LOCALIZATION ===

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

# Load version dynamically
$versionInfo = & "$PSScriptRoot\Tools\Get-Win11ForgeVersion.ps1"
$version = $versionInfo.Version

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'setup.banner_title' -Parameters @{ Version = $version } -Default "Win11Forge Framework Setup v$version")" -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'setup.banner_subtitle' -Default 'Automated Installation & Configuration')" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# === PREREQUISITES CHECK ===

Write-SetupStatus (Get-Text -Key 'setup.checking_prereq' -Default 'Checking prerequisites...') -Level Info

if (-not (Test-AdminPrivileges)) {
    Write-SetupStatus (Get-Text -Key 'setup.error.admin_required' -Default 'Administrator privileges required!') -Level Error
    Write-SetupStatus (Get-Text -Key 'setup.error.run_as_admin' -Default 'Please run this script as Administrator') -Level Error
    exit 1
}

Write-SetupStatus (Get-Text -Key 'setup.admin_confirmed' -Default 'Administrator privileges confirmed') -Level Success

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
Write-SetupStatus (Get-Text -Key 'setup.ps_version' -Parameters @{ Version = $psVersion } -Default "PowerShell version: $psVersion") -Level Info

if ($psVersion.Major -lt 5) {
    Write-SetupStatus (Get-Text -Key 'setup.error.ps_version_required' -Default 'PowerShell 5.1 or higher required!') -Level Error
    exit 1
}

# === DIRECTORY STRUCTURE CREATION ===

Write-Host ""
Write-SetupStatus (Get-Text -Key 'setup.creating_dirs' -Default 'Creating directory structure...') -Level Info

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
        Write-SetupStatus (Get-Text -Key 'setup.dir_exists' -Parameters @{ Path = $dir } -Default "Directory exists: $dir") -Level Warning
    } else {
        try {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-SetupStatus (Get-Text -Key 'setup.dir_created' -Parameters @{ Path = $dir } -Default "Created: $dir") -Level Success
        } catch {
            Write-SetupStatus (Get-Text -Key 'setup.dir_failed' -Parameters @{ Path = $dir; Error = $_.Exception.Message } -Default "Failed to create: $dir - $($_.Exception.Message)") -Level Error
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
Write-SetupStatus (Get-Text -Key 'setup.copying_files' -Default 'Copying framework files...') -Level Info

$totalFiles = 0
$copiedFiles = 0
$skippedFiles = 0
$failedFiles = 0

# Determine source
if (-not $SourcePath) {
    $SourcePath = $PSScriptRoot
}

if (-not (Test-Path -Path $SourcePath)) {
    Write-SetupStatus (Get-Text -Key 'setup.source_not_found' -Parameters @{ Path = $SourcePath } -Default "Source path not found: $SourcePath") -Level Error
    Write-SetupStatus (Get-Text -Key 'setup.specify_source' -Default 'Please specify -SourcePath parameter') -Level Error
    exit 1
}

Write-SetupStatus (Get-Text -Key 'setup.source_path' -Parameters @{ Path = $SourcePath } -Default "Source path: $SourcePath") -Level Info

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
                Write-SetupStatus (Get-Text -Key 'setup.file_copied' -Parameters @{ File = $file } -Default "Copied: $file") -Level Success
                $copiedFiles++
            } catch {
                Write-SetupStatus (Get-Text -Key 'setup.file_copy_failed' -Parameters @{ File = $file; Error = $_.Exception.Message } -Default "Failed to copy: $file - $($_.Exception.Message)") -Level Error
                $failedFiles++
            }
        } else {
            Write-SetupStatus (Get-Text -Key 'setup.file_not_found' -Parameters @{ File = $file } -Default "Not found: $file (will need manual copy)") -Level Warning
            $skippedFiles++
        }
    }
}

# === COPY SUMMARY ===

Write-Host ""
Write-SetupStatus (Get-Text -Key 'setup.copy_summary' -Default 'File copy summary:') -Level Info
Write-Host "  $(Get-Text -Key 'setup.total_files' -Default 'Total files:')   $totalFiles"
Write-Host "  $(Get-Text -Key 'setup.copied_count' -Default 'Copied:')        $copiedFiles" -ForegroundColor Green
Write-Host "  $(Get-Text -Key 'setup.skipped_count' -Default 'Skipped:')       $skippedFiles" -ForegroundColor Yellow
Write-Host "  $(Get-Text -Key 'setup.failed_count' -Default 'Failed:')        $failedFiles" -ForegroundColor Red

if ($skippedFiles -gt 0) {
    Write-Host ""
    Write-SetupStatus (Get-Text -Key 'setup.files_missing_warning' -Default 'Some files were not found in source') -Level Warning
    Write-SetupStatus (Get-Text -Key 'setup.manual_copy_hint' -Default 'You may need to copy them manually') -Level Warning
    Write-SetupStatus (Get-Text -Key 'setup.files_listed_in' -Default 'Required files are listed in PROJET_STRUCTURE.md') -Level Info
}

# === PERMISSIONS CHECK ===

Write-Host ""
Write-SetupStatus (Get-Text -Key 'setup.checking_perms' -Default 'Checking permissions...') -Level Info

try {
    $testFile = Join-Path $InstallPath "test_$(Get-Random).tmp"
    "test" | Out-File -FilePath $testFile -Force
    Remove-Item -Path $testFile -Force
    Write-SetupStatus (Get-Text -Key 'setup.write_perms_ok' -Default 'Write permissions confirmed') -Level Success
} catch {
    Write-SetupStatus (Get-Text -Key 'setup.write_perms_fail' -Default 'Insufficient write permissions!') -Level Error
    exit 1
}

# === CREATE DESKTOP SHORTCUT ===

Write-Host ""
$createShortcut = Read-Host (Get-Text -Key 'setup.create_shortcut_prompt' -Default 'Create desktop shortcut? (Y/N)')

if ($createShortcut -match '^[Yy]') {
    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Win11Forge.lnk'
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $InstallPath 'Deploy-Win11Forge.bat'
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.Description = 'Win11Forge Framework Launcher'
        $shortcut.Save()

        Write-SetupStatus (Get-Text -Key 'setup.shortcut_created' -Default 'Desktop shortcut created') -Level Success
    } catch {
        Write-SetupStatus (Get-Text -Key 'setup.shortcut_failed' -Parameters @{ Error = $_.Exception.Message } -Default "Failed to create shortcut: $($_.Exception.Message)") -Level Warning
    }
}

# === VALIDATION ===

if (-not $SkipValidation) {
    Write-Host ""
    Write-SetupStatus (Get-Text -Key 'setup.running_validation' -Default 'Running framework validation...') -Level Info
    Write-Host ""

    $validationScript = Join-Path $InstallPath 'Tools\Validate-Framework.ps1'

    if (Test-Path -Path $validationScript) {
        try {
            & $validationScript
        } catch {
            Write-SetupStatus (Get-Text -Key 'setup.validation_errors' -Default 'Validation encountered errors') -Level Warning
        }
    } else {
        Write-SetupStatus (Get-Text -Key 'setup.validation_not_found' -Default 'Validation script not found - skipping validation') -Level Warning
    }
}

# === COMPLETION ===

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  $(Get-Text -Key 'setup.completed_title' -Default 'Setup Completed Successfully!')" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""

Write-SetupStatus (Get-Text -Key 'setup.install_path' -Parameters @{ Path = $InstallPath } -Default "Installation path: $InstallPath") -Level Info
Write-Host ""
Write-Host (Get-Text -Key 'setup.next_steps' -Default 'Next steps:') -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'setup.step_review' -Default '1. Review configuration in Profiles/*.json')"
Write-Host "  $(Get-Text -Key 'setup.step_test' -Default "2. Run: .\Deploy-Win11Environment.ps1 -ProfileName 'Base' -TestMode")"
Write-Host "  $(Get-Text -Key 'setup.step_deploy' -Default '3. For full deployment: .\Deploy-Win11Forge.bat')"
Write-Host ""
Write-Host (Get-Text -Key 'setup.documentation' -Default 'Documentation:') -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'setup.doc_readme' -Default 'README.md            - Complete documentation')"
Write-Host "  $(Get-Text -Key 'setup.doc_structure' -Default 'PROJET_STRUCTURE.md  - Framework architecture')"
Write-Host "  $(Get-Text -Key 'setup.doc_changelog' -Default 'CHANGELOG.md         - Version history')"
Write-Host ""

# Open documentation
$openDocs = Read-Host (Get-Text -Key 'setup.open_readme_prompt' -Default 'Open README.md now? (Y/N)')

if ($openDocs -match '^[Yy]') {
    $readmePath = Join-Path $InstallPath 'README.md'
    if (Test-Path -Path $readmePath) {
        Start-Process -FilePath $readmePath
    }
}

Write-Host ""
Write-SetupStatus (Get-Text -Key 'setup.setup_complete' -Default 'Setup complete! Happy deploying!') -Level Success
Write-Host ""
