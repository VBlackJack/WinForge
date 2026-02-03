# Update all manifest versions to 3.5.2
param([string]$RootPath = (Split-Path $PSScriptRoot -Parent))

$updated = 0
Get-ChildItem -Path "$RootPath\Core","$RootPath\Modules" -Filter *.psd1 | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "ModuleVersion = '([^']+)'") {
        $oldVersion = $Matches[1]
        if ($oldVersion -ne '3.5.2') {
            $newContent = $content -replace "ModuleVersion = '[^']+'", "ModuleVersion = '3.5.2'"
            Set-Content -Path $_.FullName -Value $newContent -NoNewline
            Write-Host "Updated $($_.Name): $oldVersion -> 3.5.2" -ForegroundColor Green
            $script:updated++
        }
    }
}
Write-Host "Updated $updated manifests"
