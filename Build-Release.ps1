<#
.SYNOPSIS
    Builds Win11Forge GUI for release distribution.

.DESCRIPTION
    This script compiles the Win11Forge WPF application as a self-contained
    single-file executable and creates a complete release package.

.PARAMETER Version
    The version number for the release (default: 3.0.0).

.PARAMETER Configuration
    Build configuration: Release or Debug (default: Release).

.PARAMETER SelfContained
    Whether to create a self-contained executable (default: true).

.EXAMPLE
    .\Build-Release.ps1
    .\Build-Release.ps1 -Version "3.1.0"
    .\Build-Release.ps1 -Configuration Debug

.NOTES
    Author: Julien Bombled
    License: Apache 2.0
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version = "3.0.0",

    [Parameter()]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [Parameter()]
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

# Paths
$ScriptRoot = $PSScriptRoot
$GuiProjectPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\Win11Forge.GUI.csproj"
$PublishPath = Join-Path $ScriptRoot "GUI\Win11Forge.GUI\bin\publish"
$ReleasePath = Join-Path $ScriptRoot "Release\v$Version"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Win11Forge v$Version - Release Builder" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean previous builds
Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    Remove-Item $PublishPath -Recurse -Force
}
if (Test-Path $ReleasePath) {
    Remove-Item $ReleasePath -Recurse -Force
}

# Step 2: Publish the GUI project
Write-Host "[2/5] Publishing Win11Forge.GUI..." -ForegroundColor Yellow

$publishArgs = @(
    "publish"
    $GuiProjectPath
    "--configuration", $Configuration
    "--runtime", "win-x64"
    "--output", $PublishPath
    "-p:PublishSingleFile=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:EnableCompressionInSingleFile=true"
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-p:PublishTrimmed=false"
} else {
    $publishArgs += "--self-contained", "false"
}

$result = & dotnet @publishArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Write-Host "  Build successful!" -ForegroundColor Green

# Step 3: Create Release folder structure
Write-Host "[3/5] Creating release folder structure..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $ReleasePath -Force | Out-Null

# Copy the compiled executable
$exePath = Join-Path $PublishPath "Win11Forge.GUI.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath -Destination $ReleasePath
    Write-Host "  Copied: Win11Forge.GUI.exe" -ForegroundColor Gray
} else {
    Write-Host "ERROR: Win11Forge.GUI.exe not found at $exePath" -ForegroundColor Red
    exit 1
}

# Step 4: Copy PowerShell infrastructure
Write-Host "[4/5] Copying PowerShell infrastructure..." -ForegroundColor Yellow

$foldersToCopy = @("Modules", "Core", "Apps", "Profiles", "Config")
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
    "Win11Forge.ps1",
    "Win11Forge.psd1",
    "Deploy-Win11Forge.bat",
    "LICENSE"
)

foreach ($file in $rootFiles) {
    $sourcePath = Join-Path $ScriptRoot $file
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination $ReleasePath
        Write-Host "  Copied: $file" -ForegroundColor Gray
    }
}

# Step 5: Create launcher script
Write-Host "[5/5] Creating launcher script..." -ForegroundColor Yellow

$launcherContent = @'
<#
.SYNOPSIS
    Launches Win11Forge GUI.

.DESCRIPTION
    This script launches the Win11Forge WPF application.
    It can also fall back to CLI mode if the GUI is not available.

.PARAMETER CLI
    Launch in CLI mode instead of GUI.

.EXAMPLE
    .\Start-Win11Forge.ps1
    .\Start-Win11Forge.ps1 -CLI
#>

[CmdletBinding()]
param(
    [switch]$CLI
)

$ScriptRoot = $PSScriptRoot
$GuiExe = Join-Path $ScriptRoot "Win11Forge.GUI.exe"
$CliScript = Join-Path $ScriptRoot "Win11Forge.ps1"

if ($CLI -or -not (Test-Path $GuiExe)) {
    if (Test-Path $CliScript) {
        Write-Host "Launching Win11Forge CLI..." -ForegroundColor Cyan
        & $CliScript @args
    } else {
        Write-Host "ERROR: Neither GUI nor CLI found!" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Launching Win11Forge GUI..." -ForegroundColor Cyan
    Start-Process $GuiExe -WorkingDirectory $ScriptRoot
}
'@

$launcherPath = Join-Path $ReleasePath "Start-Win11Forge.ps1"
Set-Content -Path $launcherPath -Value $launcherContent -Encoding UTF8

Write-Host "  Created: Start-Win11Forge.ps1" -ForegroundColor Gray

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Release package created at:" -ForegroundColor White
Write-Host "  $ReleasePath" -ForegroundColor Cyan
Write-Host ""

# Show package contents
$releaseSize = (Get-ChildItem $ReleasePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$fileCount = (Get-ChildItem $ReleasePath -Recurse -File).Count
Write-Host "Package Statistics:" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor Gray
Write-Host "  Size:  $([math]::Round($releaseSize, 2)) MB" -ForegroundColor Gray
Write-Host ""

# List main files
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
