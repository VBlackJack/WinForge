<#
.SYNOPSIS
    Updates applications.json with improved version detection configuration

.DESCRIPTION
    This script adds:
    - VersionKey to registry-based apps for proper version extraction
    - VersionRegex to command-based apps for version parsing
    - Replaces hardcoded C:\ paths with environment variables

.NOTES
    Author: Julien Bombled
    Version: 1.0.0
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

param(
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptRoot
$jsonPath = Join-Path $repoRoot 'Apps\Database\applications.json'

Write-Host "Loading applications.json..." -ForegroundColor Cyan
$json = Get-Content -Path $jsonPath -Raw | ConvertFrom-Json
$modified = $false

# Track changes for reporting
$changes = @{
    VersionKey = 0
    VersionRegex = 0
    PathFixes = 0
}

$appIds = $json.Applications.PSObject.Properties.Name
Write-Host "Processing $($appIds.Count) applications..." -ForegroundColor Cyan

foreach ($appId in $appIds) {
    $app = $json.Applications.$appId

    if (-not $app.Detection) { continue }

    $method = $app.Detection.Method

    switch ($method) {
        'Registry' {
            # Add VersionKey for registry-based apps
            if (-not $app.Detection.PSObject.Properties['VersionKey']) {
                $regPath = $app.Detection.Path

                # Determine the appropriate VersionKey based on registry path
                if ($regPath -match 'Uninstall') {
                    # Standard Uninstall keys use DisplayVersion
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'DisplayVersion' -Force
                } elseif ($regPath -match 'Google\\Chrome') {
                    # Chrome stores version in BLBeacon subkey
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                    $app.Detection.Path = $regPath + '\BLBeacon'
                } elseif ($regPath -match 'Mozilla') {
                    # Firefox/Thunderbird: version is in CurrentVersion
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'CurrentVersion' -Force
                } elseif ($regPath -match 'VideoLAN\\VLC') {
                    # VLC stores version directly
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                } elseif ($regPath -match '7-Zip') {
                    # 7-Zip stores version in Path64 as full path with version
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Path64' -Force
                } elseif ($regPath -match 'Valve\\Steam') {
                    # Steam stores InstallPath
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'InstallPath' -Force
                } elseif ($regPath -match 'Microsoft\\Office') {
                    # Office uses VersionToReport or similar
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'VersionToReport' -Force
                } elseif ($regPath -match 'DirectX') {
                    # DirectX uses Version
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                } elseif ($regPath -match 'VisualStudio.*Runtimes') {
                    # VC++ Runtimes use Version
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                } elseif ($regPath -match 'NET Framework') {
                    # .NET Framework uses Release (build number)
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                } elseif ($regPath -match 'KLCodecPack') {
                    # K-Lite Codec Pack
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'Version' -Force
                } else {
                    # Default to DisplayVersion (common in Uninstall keys)
                    $app.Detection | Add-Member -NotePropertyName 'VersionKey' -NotePropertyValue 'DisplayVersion' -Force
                }
                $changes.VersionKey++
                $modified = $true
                Write-Verbose "Added VersionKey to $appId"
            }
        }
        'File' {
            # Fix hardcoded paths - replace C:\Program Files with environment variables
            if ($app.Detection.Path) {
                $path = $app.Detection.Path
                $newPath = $path

                # Replace hardcoded paths with environment variables
                if ($path -match '^C:\\Program Files \(x86\)\\') {
                    $newPath = $path -replace '^C:\\Program Files \(x86\)\\', '%ProgramFiles(x86)%\'
                    $changes.PathFixes++
                } elseif ($path -match '^C:\\Program Files\\') {
                    $newPath = $path -replace '^C:\\Program Files\\', '%ProgramFiles%\'
                    $changes.PathFixes++
                } elseif ($path -match '^C:\\Users\\[^\\]+\\AppData\\Local\\') {
                    $newPath = $path -replace '^C:\\Users\\[^\\]+\\AppData\\Local\\', '%LOCALAPPDATA%\'
                    $changes.PathFixes++
                } elseif ($path -match '^C:\\Users\\[^\\]+\\AppData\\Roaming\\') {
                    $newPath = $path -replace '^C:\\Users\\[^\\]+\\AppData\\Roaming\\', '%APPDATA%\'
                    $changes.PathFixes++
                }

                if ($newPath -ne $path) {
                    $app.Detection.Path = $newPath
                    $modified = $true
                    Write-Verbose "Fixed path for $appId : $newPath"
                }
            }
        }
        'Command' {
            # Add VersionRegex for command-based detection
            if (-not $app.Detection.PSObject.Properties['VersionRegex']) {
                $cmd = $app.Detection.Command

                # Determine regex based on command type
                if ($cmd -match 'java|javac') {
                    # Java version: 'java version "17.0.1"' or 'openjdk version "17.0.1"'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue 'version "?([\d._]+)"?' -Force
                } elseif ($cmd -match 'dotnet') {
                    # .NET: '8.0.100'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue '(\d+\.\d+\.\d+)' -Force
                } elseif ($cmd -match 'python') {
                    # Python: 'Python 3.12.1'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue 'Python (\d+\.\d+\.\d+)' -Force
                } elseif ($cmd -match 'node') {
                    # Node: 'v20.10.0'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue 'v?(\d+\.\d+\.\d+)' -Force
                } elseif ($cmd -match 'git') {
                    # Git: 'git version 2.43.0.windows.1'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue 'git version (\d+\.\d+\.\d+)' -Force
                } elseif ($cmd -match 'rustc|cargo') {
                    # Rust: 'rustc 1.75.0'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue '(\d+\.\d+\.\d+)' -Force
                } elseif ($cmd -match 'go version') {
                    # Go: 'go version go1.21.5 windows/amd64'
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue 'go(\d+\.\d+\.\d+)' -Force
                } else {
                    # Generic version pattern
                    $app.Detection | Add-Member -NotePropertyName 'VersionRegex' -NotePropertyValue '(\d+\.\d+[\.\d]*)' -Force
                }
                $changes.VersionRegex++
                $modified = $true
                Write-Verbose "Added VersionRegex to $appId"
            }
        }
    }
}

# Output changes summary
Write-Host ""
Write-Host "Changes summary:" -ForegroundColor Green
Write-Host "  - VersionKey added: $($changes.VersionKey)" -ForegroundColor Yellow
Write-Host "  - VersionRegex added: $($changes.VersionRegex)" -ForegroundColor Yellow
Write-Host "  - Path fixes: $($changes.PathFixes)" -ForegroundColor Yellow

if ($WhatIf) {
    Write-Host ""
    Write-Host "WhatIf mode: No changes saved" -ForegroundColor Magenta
} elseif ($modified) {
    # Save the updated JSON with proper formatting
    $jsonContent = $json | ConvertTo-Json -Depth 20
    # Ensure UTF-8 without BOM
    [System.IO.File]::WriteAllText($jsonPath, $jsonContent, [System.Text.UTF8Encoding]::new($false))
    Write-Host ""
    Write-Host "applications.json updated successfully" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "No changes needed" -ForegroundColor Cyan
}
