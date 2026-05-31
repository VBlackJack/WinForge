<#
.SYNOPSIS
    Builds Win11Forge GUI for release distribution.

.DESCRIPTION
    This script compiles the Win11Forge WPF application as a self-contained
    single-file executable and creates a complete release package.

    Steps:
    1. Run all tests (abort on failure)
    2. Publish GUI as self-contained single-file
    3. Create distribution folder with PowerShell infrastructure
    4. Clean unnecessary files
    5. Create ZIP archive

.PARAMETER Version
    Optional forced calendar release version. Accepts YYYYMMDDxx, or Heimdall-style
    YYYY.MMDDxx. When omitted, the next YYYYMMDDxx sequence for today is used.

.PARAMETER Configuration
    Build configuration: Release or Debug (default: Release).

.PARAMETER SkipTests
    Skip the test validation step (not recommended).

.PARAMETER NoZip
    Skip the ZIP archive creation.

.EXAMPLE
    .\Build-Release.ps1
    .\Build-Release.ps1 -Version "2026050901"
    .\Build-Release.ps1 -SkipTests

.NOTES
    Author: Julien Bombled
    License: Apache 2.0
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
    [string]$Version,

    [Parameter()]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

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

# Paths
$ScriptRoot = $PSScriptRoot
$GuiProjectPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\Win11Forge.GUI.csproj"
$TestProjectPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI.Tests\Win11Forge.GUI.Tests.csproj"
$PublishPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\bin\publish"
$DistRoot = Join-Path $ScriptRoot "Dist"

# Resolve and persist the release version before build/publish/package steps.
$versionScript = Join-Path $ScriptRoot "Tools\Update-CalendarVersion.ps1"
if (-not (Test-Path $versionScript)) {
    throw "Versioning script not found: $versionScript"
}

$versionArgs = @{
    RootPath = $ScriptRoot
    PassThru = $true
}
if ($Version) {
    $versionArgs.Version = $Version
}

$versionInfo = & $versionScript @versionArgs
$Version = $versionInfo.DisplayVersion

$manifestScript = Join-Path $ScriptRoot "Tools\Update-ManifestVersions.ps1"
if (Test-Path $manifestScript) {
    & $manifestScript -RootPath $ScriptRoot
}

$ReleaseName = "Win11Forge_v$Version"
$ReleasePath = Join-Path $DistRoot $ReleaseName
$ZipPath = Join-Path $DistRoot "$ReleaseName.zip"

# Header
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'build.banner_title' -Parameters @{ Version = $Version } -Default "Win11Forge v$Version - Release Builder")" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  $(Get-Text -Key 'build.configuration' -Parameters @{ Config = $Configuration } -Default "Configuration: $Configuration")" -ForegroundColor Gray
Write-Host "  $(Get-Text -Key 'build.output_path' -Parameters @{ Path = $ReleasePath } -Default "Output: $ReleasePath")" -ForegroundColor Gray
Write-Host ""

$stepCount = if ($SkipTests) { 6 } else { 7 }
$currentStep = 0

# ============================================
# Step 1: Run Tests (unless skipped)
# ============================================
if (-not $SkipTests) {
    $currentStep++
    Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_running_tests' -Default 'Running tests...')" -ForegroundColor Yellow

    $testResult = & dotnet test $TestProjectPath --configuration $Configuration --verbosity minimal 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host (Get-Text -Key 'build.tests_failed' -Default 'ERROR: Tests failed! Aborting release.') -ForegroundColor Red
        Write-Host ""
        Write-Host $testResult -ForegroundColor Red
        Write-Host ""
        Write-Host (Get-Text -Key 'build.tests_fix_hint' -Default 'Fix the failing tests before creating a release.') -ForegroundColor Yellow
        exit 1
    }

    Write-Host "  $(Get-Text -Key 'build.tests_passed' -Default 'Tests passed!')" -ForegroundColor Green
}

# ============================================
# Step 2: Clean previous builds
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_cleaning' -Default 'Cleaning previous builds...')" -ForegroundColor Yellow

if (Test-Path $PublishPath) {
    Remove-Item $PublishPath -Recurse -Force
    Write-Host "  $(Get-Text -Key 'build.removed_path' -Parameters @{ Path = $PublishPath } -Default "Removed: $PublishPath")" -ForegroundColor Gray
}
if (Test-Path $ReleasePath) {
    Remove-Item $ReleasePath -Recurse -Force
    Write-Host "  $(Get-Text -Key 'build.removed_path' -Parameters @{ Path = $ReleasePath } -Default "Removed: $ReleasePath")" -ForegroundColor Gray
}
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
    Write-Host "  $(Get-Text -Key 'build.removed_path' -Parameters @{ Path = $ZipPath } -Default "Removed: $ZipPath")" -ForegroundColor Gray
}

$cleanArgs = @(
    "clean"
    $GuiProjectPath
    "--configuration", $Configuration
    "--verbosity", "minimal"
)

$cleanResult = & dotnet @cleanArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host (Get-Text -Key 'build.clean_failed' -Default 'ERROR: Clean failed!') -ForegroundColor Red
    Write-Host $cleanResult -ForegroundColor Red
    exit 1
}

# ============================================
# Step 3: Publish the GUI project
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_publishing' -Default 'Publishing Win11Forge.GUI (self-contained, single-file)...')" -ForegroundColor Yellow

# NuGetLockFilePath redirects the publish-time restore to a transient lock file in
# the project's obj/ folder (gitignored). NuGet still performs RID-aware resolution
# for the self-contained bundle, but does NOT touch the committed RID-neutral
# packages.lock.json — which would otherwise gain a "net10.0-windows7.0/win-x64"
# section incompatible with `dotnet restore --locked-mode` in CI (see PR #98).
# Setting RestorePackagesWithLockFile=false instead would error with NU1005 because
# a lock file is present at the default path.
$publishArgs = @(
    "publish"
    $GuiProjectPath
    "--configuration", $Configuration
    "--runtime", "win-x64"
    "--output", $PublishPath
    "--self-contained", "true"
    "-p:NuGetLockFilePath=obj/publish.packages.lock.json"
    "-p:PublishSingleFile=false"
    "-p:PublishTrimmed=false"
    "-p:PublishReadyToRun=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:DebugType=none"
    "-p:DebugSymbols=false"
    "-p:AssemblyVersion=$($versionInfo.AssemblyVersion)"
    "-p:FileVersion=$($versionInfo.AssemblyVersion)"
    "-p:Version=$($versionInfo.AssemblyVersion)"
    "-p:InformationalVersion=$($versionInfo.InformationalVersion)"
)

$result = & dotnet @publishArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host (Get-Text -Key 'build.build_failed' -Default 'ERROR: Build failed!') -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Write-Host "  $(Get-Text -Key 'build.build_success' -Default 'Build successful!')" -ForegroundColor Green

# ============================================
# Step 4: Create Release folder structure
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_creating_dist' -Default 'Creating distribution folder...')" -ForegroundColor Yellow

# Create Dist root if needed
if (-not (Test-Path $DistRoot)) {
    New-Item -ItemType Directory -Path $DistRoot -Force | Out-Null
}

New-Item -ItemType Directory -Path $ReleasePath -Force | Out-Null

# Copy the GUI to a subfolder (WPF apps require all DLLs)
$guiDestPath = Join-Path $ReleasePath "GUI"
New-Item -ItemType Directory -Path $guiDestPath -Force | Out-Null

$exePath = Join-Path $PublishPath "Win11Forge.GUI.exe"
if (Test-Path $exePath) {
    # Copy all files from publish folder to GUI subfolder
    Copy-Item "$PublishPath\*" -Destination $guiDestPath -Recurse
    $fileCount = (Get-ChildItem $PublishPath -File).Count
    $totalSize = [math]::Round((Get-ChildItem $PublishPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
    Write-Host "  $(Get-Text -Key 'build.copied_gui' -Parameters @{ Count = $fileCount; Size = $totalSize } -Default "Copied: GUI/ ($fileCount files, $totalSize MB)")" -ForegroundColor Gray
} else {
    Write-Host (Get-Text -Key 'build.exe_not_found' -Parameters @{ Path = $exePath } -Default "ERROR: Win11Forge.GUI.exe not found at $exePath") -ForegroundColor Red
    exit 1
}

# ============================================
# Step 5: Copy PowerShell infrastructure
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_copying_infra' -Default 'Copying PowerShell infrastructure...')" -ForegroundColor Yellow

$foldersToCopy = @("Modules", "Core", "Apps", "Profiles", "Config", "Docs")
foreach ($folder in $foldersToCopy) {
    $sourcePath = Join-Path $ScriptRoot $folder
    $destPath = Join-Path $ReleasePath $folder
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination $destPath -Recurse
        $itemCount = (Get-ChildItem $sourcePath -Recurse -File).Count
        Write-Host "  $(Get-Text -Key 'build.copied_folder' -Parameters @{ Folder = $folder; Count = $itemCount } -Default "Copied: $folder/ ($itemCount files)")" -ForegroundColor Gray
    } else {
        Write-Host "  $(Get-Text -Key 'build.folder_not_found' -Parameters @{ Folder = $folder } -Default "WARNING: $folder not found, skipping...")" -ForegroundColor Yellow
    }
}

# Copy essential root files
$rootFiles = @(
    "Deploy-Win11Environment.ps1",
    "CHANGELOG.md",
    "LICENSE",
    "README.md"
)

foreach ($file in $rootFiles) {
    $sourcePath = Join-Path $ScriptRoot $file
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination $ReleasePath
        Write-Host "  $(Get-Text -Key 'build.copied_file' -Parameters @{ File = $file } -Default "Copied: $file")" -ForegroundColor Gray
    }
}

# Create launcher script (simple .cmd)
$launcherContent = @'
@echo off
:: Win11Forge GUI Launcher
:: This script launches the Win11Forge GUI application
cd /d "%~dp0GUI"
start "" "Win11Forge.GUI.exe"
'@

$launcherPath = Join-Path $ReleasePath "Win11Forge.cmd"
Set-Content -Path $launcherPath -Value $launcherContent -Encoding ASCII
Write-Host "  $(Get-Text -Key 'build.created_launcher' -Parameters @{ File = 'Win11Forge.cmd' } -Default 'Created: Win11Forge.cmd')" -ForegroundColor Gray

# ============================================
# Step 6: Clean unnecessary files
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_cleaning_files' -Default 'Cleaning unnecessary files...')" -ForegroundColor Yellow

# Patterns to remove
$patternsToRemove = @(
    "*.pdb",           # Debug symbols
    "*.xml",           # XML documentation (in bin folders)
    "*.deps.json",     # Dependency files
    "*.runtimeconfig.json", # Runtime config
    ".git*",           # Git files
    "*.Tests*",        # Test files
    "Thumbs.db",       # Windows thumbnails
    ".DS_Store"        # macOS files
)

$removedCount = 0
foreach ($pattern in $patternsToRemove) {
    $files = Get-ChildItem $ReleasePath -Recurse -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
        $removedCount++
    }
}

# Remove empty directories
Get-ChildItem $ReleasePath -Recurse -Directory |
    Where-Object { (Get-ChildItem $_.FullName -Recurse -File).Count -eq 0 } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "  $(Get-Text -Key 'build.removed_files_count' -Parameters @{ Count = $removedCount } -Default "Removed $removedCount unnecessary files")" -ForegroundColor Gray

# ============================================
# Step 7: Create ZIP archive
# ============================================
if (-not $NoZip) {
    $currentStep++
    Write-Host "[$currentStep/$stepCount] $(Get-Text -Key 'build.step_creating_zip' -Default 'Creating ZIP archive...')" -ForegroundColor Yellow

    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Compress-Archive -Path "$ReleasePath\*" -DestinationPath $ZipPath -CompressionLevel Optimal

    $zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "  $(Get-Text -Key 'build.created_zip' -Parameters @{ Name = "$ReleaseName.zip"; Size = $zipSize } -Default "Created: $ReleaseName.zip ($zipSize MB)")" -ForegroundColor Green
}

# ============================================
# Summary
# ============================================
Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host "  $(Get-Text -Key 'build.complete_title' -Default 'Build Complete!')" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""

# Package statistics
$releaseSize = (Get-ChildItem $ReleasePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$fileCount = (Get-ChildItem $ReleasePath -Recurse -File).Count

Write-Host (Get-Text -Key 'build.stats_title' -Default 'Package Statistics:') -ForegroundColor White
Write-Host "  $(Get-Text -Key 'build.stats_files' -Parameters @{ Count = $fileCount } -Default "Files: $fileCount")" -ForegroundColor Gray
Write-Host "  $(Get-Text -Key 'build.stats_size' -Parameters @{ Size = [math]::Round($releaseSize, 2) } -Default "Size: $([math]::Round($releaseSize, 2)) MB (uncompressed)")" -ForegroundColor Gray
if (-not $NoZip) {
    Write-Host "  $(Get-Text -Key 'build.stats_zip_size' -Parameters @{ Size = $zipSize } -Default "ZIP Size: $zipSize MB")" -ForegroundColor Gray
}
Write-Host ""

# Output locations
Write-Host (Get-Text -Key 'build.output_title' -Default 'Output Locations:') -ForegroundColor White
Write-Host "  $(Get-Text -Key 'build.output_folder' -Parameters @{ Path = $ReleasePath } -Default "Folder: $ReleasePath")" -ForegroundColor Cyan
if (-not $NoZip) {
    Write-Host "  $(Get-Text -Key 'build.output_zip' -Parameters @{ Path = $ZipPath } -Default "ZIP: $ZipPath")" -ForegroundColor Cyan
}
Write-Host ""

# Contents summary
Write-Host (Get-Text -Key 'build.contents_title' -Default 'Main Contents:') -ForegroundColor White
Get-ChildItem $ReleasePath -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1KB, 1)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Gray
}
Get-ChildItem $ReleasePath -Directory | ForEach-Object {
    $count = (Get-ChildItem $_.FullName -Recurse -File).Count
    Write-Host "  $($_.Name)/ ($count files)" -ForegroundColor Gray
}

Write-Host ""
Write-Host (Get-Text -Key 'build.test_hint' -Default 'To test the release:') -ForegroundColor Yellow
Write-Host "  cd `"$ReleasePath`"" -ForegroundColor Cyan
Write-Host "  .\Win11Forge.cmd" -ForegroundColor Cyan
Write-Host ""
