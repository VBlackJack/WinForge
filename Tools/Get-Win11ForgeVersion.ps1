<#
.SYNOPSIS
    Get Win11Forge framework version from central configuration

.DESCRIPTION
    Loads version information from Config/version.json
    Provides a single source of truth for version management

.OUTPUTS
    PSCustomObject with Version, DisplayName, and ReleaseDate

.EXAMPLE
    $version = & .\Tools\Get-Win11ForgeVersion.ps1
    Write-Host "Win11Forge v$($version.Version)"

.NOTES
    Author: Win11Forge Team
    Part of centralized version management system
#>

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
