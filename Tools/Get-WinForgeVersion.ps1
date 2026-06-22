<#
.SYNOPSIS
    Get WinForge framework version from central configuration

.DESCRIPTION
    Loads version information from Config/version.json
    Provides a single source of truth for version management

.OUTPUTS
    PSCustomObject with Version, DisplayName, and ReleaseDate

.EXAMPLE
    $version = & .\Tools\Get-WinForgeVersion.ps1
    Write-Host "WinForge v$($version.Version)"

.NOTES
    Author: Julien Bombled
    Part of centralized version management system
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
param()

$ErrorActionPreference = 'Stop'

# Locate version.json relative to this script
$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptRoot
$versionPath = Join-Path $repoRoot 'Config\version.json'

if (-not (Test-Path $versionPath)) {
    Write-Error "Version file not found: $versionPath"
    exit 1
}

try {
    $versionData = Get-Content -Path $versionPath -Raw | ConvertFrom-Json

    # Validate required properties
    if (-not $versionData.Version) {
        Write-Error "Version property missing in version.json"
        exit 1
    }

    # Return version object
    return [PSCustomObject]@{
        Version = $versionData.Version
        DisplayName = $versionData.DisplayName
        ReleaseDate = $versionData.ReleaseDate
    }
} catch {
    Write-Error "Failed to load version: $_"
    exit 1
}
