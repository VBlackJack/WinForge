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
    The version number for the release (default: from Config/version.json).

.PARAMETER Configuration
    Build configuration: Release or Debug (default: Release).

.PARAMETER SkipTests
    Skip the test validation step (not recommended).

.PARAMETER NoZip
    Skip the ZIP archive creation.

.EXAMPLE
    .\Build-Release.ps1
    .\Build-Release.ps1 -Version "3.1.0"
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

# Paths
$ScriptRoot = $PSScriptRoot
$GuiProjectPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\Win11Forge.GUI.csproj"
$TestProjectPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI.Tests\Win11Forge.GUI.Tests.csproj"
$PublishPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\bin\publish"
$DistRoot = Join-Path $ScriptRoot "Dist"

# Get version from Config/version.json if not provided
if (-not $Version) {
    $versionFile = Join-Path $ScriptRoot "Config\version.json"
    if (Test-Path $versionFile) {
        $versionData = Get-Content $versionFile -Raw | ConvertFrom-Json
        $Version = $versionData.Version
    } else {
        $Version = "3.0.0"
    }
}

$ReleaseName = "Win11Forge_v$Version"
$ReleasePath = Join-Path $DistRoot $ReleaseName
$ZipPath = Join-Path $DistRoot "$ReleaseName.zip"

# Header
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Win11Forge v$Version - Release Builder" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Output: $ReleasePath" -ForegroundColor Gray
Write-Host ""

$stepCount = if ($SkipTests) { 6 } else { 7 }
$currentStep = 0

# ============================================
# Step 1: Run Tests (unless skipped)
# ============================================
if (-not $SkipTests) {
    $currentStep++
    Write-Host "[$currentStep/$stepCount] Running tests..." -ForegroundColor Yellow

    $testResult = & dotnet test $TestProjectPath --configuration $Configuration --verbosity minimal 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Tests failed! Aborting release." -ForegroundColor Red
        Write-Host ""
        Write-Host $testResult -ForegroundColor Red
        Write-Host ""
        Write-Host "Fix the failing tests before creating a release." -ForegroundColor Yellow
        exit 1
    }

    # Extract test count from output
    $testSummary = $testResult | Select-String -Pattern "Total tests:|Passed:" | Select-Object -Last 1
    Write-Host "  Tests passed!" -ForegroundColor Green
}

# ============================================
# Step 2: Clean previous builds
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] Cleaning previous builds..." -ForegroundColor Yellow

if (Test-Path $PublishPath) {
    Remove-Item $PublishPath -Recurse -Force
    Write-Host "  Removed: $PublishPath" -ForegroundColor Gray
}
if (Test-Path $ReleasePath) {
    Remove-Item $ReleasePath -Recurse -Force
    Write-Host "  Removed: $ReleasePath" -ForegroundColor Gray
}
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
    Write-Host "  Removed: $ZipPath" -ForegroundColor Gray
}

# ============================================
# Step 3: Publish the GUI project
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] Publishing Win11Forge.GUI (self-contained, single-file)..." -ForegroundColor Yellow

$publishArgs = @(
    "publish"
    $GuiProjectPath
    "--configuration", $Configuration
    "--runtime", "win-x64"
    "--output", $PublishPath
    "--self-contained", "true"
    "-p:PublishSingleFile=false"
    "-p:PublishTrimmed=false"
    "-p:PublishReadyToRun=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:DebugType=none"
    "-p:DebugSymbols=false"
)

$result = & dotnet @publishArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Write-Host "  Build successful!" -ForegroundColor Green

# ============================================
# Step 4: Create Release folder structure
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] Creating distribution folder..." -ForegroundColor Yellow

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
    Write-Host "  Copied: GUI/ ($fileCount files, $totalSize MB)" -ForegroundColor Gray
} else {
    Write-Host "ERROR: Win11Forge.GUI.exe not found at $exePath" -ForegroundColor Red
    exit 1
}

# ============================================
# Step 5: Copy PowerShell infrastructure
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] Copying PowerShell infrastructure..." -ForegroundColor Yellow

$foldersToCopy = @("Modules", "Core", "Apps", "Profiles", "Config", "Docs")
foreach ($folder in $foldersToCopy) {
    $sourcePath = Join-Path $ScriptRoot $folder
    $destPath = Join-Path $ReleasePath $folder
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination $destPath -Recurse
        $itemCount = (Get-ChildItem $sourcePath -Recurse -File).Count
        Write-Host "  Copied: $folder/ ($itemCount files)" -ForegroundColor Gray
    } else {
        Write-Host "  WARNING: $folder not found, skipping..." -ForegroundColor Yellow
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
        Write-Host "  Copied: $file" -ForegroundColor Gray
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
Write-Host "  Created: Win11Forge.cmd" -ForegroundColor Gray

# ============================================
# Step 6: Clean unnecessary files
# ============================================
$currentStep++
Write-Host "[$currentStep/$stepCount] Cleaning unnecessary files..." -ForegroundColor Yellow

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

Write-Host "  Removed $removedCount unnecessary files" -ForegroundColor Gray

# ============================================
# Step 7: Create ZIP archive
# ============================================
if (-not $NoZip) {
    $currentStep++
    Write-Host "[$currentStep/$stepCount] Creating ZIP archive..." -ForegroundColor Yellow

    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Compress-Archive -Path "$ReleasePath\*" -DestinationPath $ZipPath -CompressionLevel Optimal

    $zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "  Created: $ReleaseName.zip ($zipSize MB)" -ForegroundColor Green
}

# ============================================
# Summary
# ============================================
Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""

# Package statistics
$releaseSize = (Get-ChildItem $ReleasePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$fileCount = (Get-ChildItem $ReleasePath -Recurse -File).Count

Write-Host "Package Statistics:" -ForegroundColor White
Write-Host "  Files:    $fileCount" -ForegroundColor Gray
Write-Host "  Size:     $([math]::Round($releaseSize, 2)) MB (uncompressed)" -ForegroundColor Gray
if (-not $NoZip) {
    Write-Host "  ZIP Size: $zipSize MB" -ForegroundColor Gray
}
Write-Host ""

# Output locations
Write-Host "Output Locations:" -ForegroundColor White
Write-Host "  Folder: $ReleasePath" -ForegroundColor Cyan
if (-not $NoZip) {
    Write-Host "  ZIP:    $ZipPath" -ForegroundColor Cyan
}
Write-Host ""

# Contents summary
Write-Host "Main Contents:" -ForegroundColor White
Get-ChildItem $ReleasePath -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1KB, 1)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Gray
}
Get-ChildItem $ReleasePath -Directory | ForEach-Object {
    $count = (Get-ChildItem $_.FullName -Recurse -File).Count
    Write-Host "  $($_.Name)/ ($count files)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "To test the release:" -ForegroundColor Yellow
Write-Host "  cd `"$ReleasePath`"" -ForegroundColor Cyan
Write-Host "  .\Start-Win11Forge.ps1" -ForegroundColor Cyan
Write-Host ""
