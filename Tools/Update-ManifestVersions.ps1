<#
 Copyright 2026 Julien Bombled

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
#>

# Update all manifest versions from the repo display version (Config/version.json)
param([string]$RootPath = (Split-Path $PSScriptRoot -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$versionPath = Join-Path $RootPath 'Config\version.json'
function ConvertTo-ManifestVersion {
    param([Parameter(Mandatory)][string]$DisplayVersion)

    $trimmed = $DisplayVersion.Trim()
    if ($trimmed -match '^(?<year>\d{4})(?<mmdd>\d{4})(?<sequence>\d{2})$') {
        $sequence = [int]$Matches.sequence
        if ($sequence -lt 1 -or $sequence -gt 99) {
            throw "Calendar version sequence must be between 01 and 99: $DisplayVersion"
        }

        return '1.0.{0}.{1}' -f $Matches.mmdd, $sequence
    }

    if ($trimmed -match '^\d+\.\d+\.\d+(?:\.\d+)?$') {
        return $trimmed
    }

    throw "Unsupported framework version format in Config/version.json: $DisplayVersion"
}

if (Test-Path $versionPath) {
    try {
        $versionJson = Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $versionProperty = $versionJson.PSObject.Properties['Version']
        if ($versionProperty -and $versionProperty.Value) {
            $targetDisplayVersion = [string]$versionProperty.Value
            $targetVersion = ConvertTo-ManifestVersion -DisplayVersion $targetDisplayVersion
        } else {
            throw "Version property missing in $versionPath"
        }
    } catch {
        throw "Failed to resolve manifest version from $versionPath`: $($_.Exception.Message)"
    }
} else {
    throw "Version file not found: $versionPath"
}

$updated = 0
Get-ChildItem -Path "$RootPath\Core","$RootPath\Modules" -Filter *.psd1 | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    $newContent = $content
    $changes = @()

    if ($content -match "ModuleVersion = '([^']+)'") {
        $oldVersion = $Matches[1]
        if ($oldVersion -ne $targetVersion) {
            $newContent = $newContent -replace "ModuleVersion = '[^']+'", "ModuleVersion = '$targetVersion'"
            $changes += "ModuleVersion $oldVersion -> $targetVersion"
        }
    }

    if ($content -match "ReleaseNotes = 'Win11Forge v([^']+)'") {
        $oldReleaseVersion = $Matches[1]
        if ($oldReleaseVersion -ne $targetDisplayVersion) {
            $newContent = $newContent -replace "ReleaseNotes = 'Win11Forge v[^']+'", "ReleaseNotes = 'Win11Forge v$targetDisplayVersion'"
            $changes += "ReleaseNotes $oldReleaseVersion -> $targetDisplayVersion"
        }
    }

    if ($newContent -ne $content) {
        Set-Content -Path $_.FullName -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "Updated $($_.Name): $($changes -join '; ')" -ForegroundColor Green
        $updated++
    }
}
Write-Host "Updated $updated manifests"
