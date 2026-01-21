<#
.SYNOPSIS
    Verifies the detection configuration changes in applications.json
#>

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptRoot
$jsonPath = Join-Path $repoRoot 'Apps\Database\applications.json'

Write-Host 'Testing applications.json parsing...' -ForegroundColor Cyan
try {
    $json = Get-Content -Path $jsonPath -Raw | ConvertFrom-Json
    Write-Host "Successfully loaded $($json.Applications.PSObject.Properties.Count) applications" -ForegroundColor Green
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}

# Count apps with new fields
$registryWithVersionKey = 0
$commandWithVersionRegex = 0
$fixedPaths = 0
$envVarPaths = @()

foreach ($appId in $json.Applications.PSObject.Properties.Name) {
    $app = $json.Applications.$appId
    if ($app.Detection) {
        if ($app.Detection.PSObject.Properties['VersionKey']) { $registryWithVersionKey++ }
        if ($app.Detection.PSObject.Properties['VersionRegex']) { $commandWithVersionRegex++ }
        if ($app.Detection.Path -and $app.Detection.Path -match '%') {
            $fixedPaths++
            $envVarPaths += "$appId : $($app.Detection.Path)"
        }
    }
}

Write-Host ''
Write-Host 'Verification Results:' -ForegroundColor Green
Write-Host "  - Apps with VersionKey: $registryWithVersionKey"
Write-Host "  - Apps with VersionRegex: $commandWithVersionRegex"
Write-Host "  - Apps with env var paths: $fixedPaths"

# Show sample of fixed paths
Write-Host ''
Write-Host 'Sample of fixed paths (first 5):' -ForegroundColor Cyan
$envVarPaths | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" }
