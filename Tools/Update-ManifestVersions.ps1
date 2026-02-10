# Update all manifest versions to the repo version (Config/version.json)
param([string]$RootPath = (Split-Path $PSScriptRoot -Parent))

$versionPath = Join-Path $RootPath 'Config\version.json'
$targetVersion = '3.7.0'
if (Test-Path $versionPath) {
    try {
        $versionJson = Get-Content -Path $versionPath -Raw | ConvertFrom-Json
        if ($versionJson.version) {
            $targetVersion = $versionJson.version
        }
    } catch {
        # Fall back to default if version file is invalid
        $targetVersion = '3.7.0'
    }
}

$updated = 0
Get-ChildItem -Path "$RootPath\Core","$RootPath\Modules" -Filter *.psd1 | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "ModuleVersion = '([^']+)'") {
        $oldVersion = $Matches[1]
        if ($oldVersion -ne $targetVersion) {
            $newContent = $content -replace "ModuleVersion = '[^']+'", "ModuleVersion = '$targetVersion'"
            Set-Content -Path $_.FullName -Value $newContent -NoNewline
            Write-Host "Updated $($_.Name): $oldVersion -> $targetVersion" -ForegroundColor Green
            $script:updated++
        }
    }
}
Write-Host "Updated $updated manifests"
