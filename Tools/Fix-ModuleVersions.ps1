# Fix all module versions to 2.5.0

$targetVersion = '2.5.0'
$modules = @(
    'Modules/StartMenuLayout.psm1',
    'Modules/StartupManager.psm1',
    'Modules/Prerequisites.psm1',
    'Modules/StartMenuPinning.psm1',
    'Modules/SystemConfig.psm1',
    'Modules/EnvironmentDetection.psm1',
    'Modules/ProfileManager.psm1',
    'Modules/ApplicationDatabase.psm1'
)

$fixed = 0

foreach ($modulePath in $modules) {
    if (Test-Path $modulePath) {
        $content = Get-Content $modulePath -Raw
        $originalContent = $content

        # Fix version in SYNOPSIS
        $content = $content -replace '(Module v)\d+\.\d+\.\d+', "`$1$targetVersion"

        # Fix version in Version: line
        $content = $content -replace '(Version:\s*)\d+\.\d+\.\d+', "`$1$targetVersion"

        if ($content -ne $originalContent) {
            Set-Content -Path $modulePath -Value $content -NoNewline
            $fixed++
            Write-Host "Fixed: $modulePath" -ForegroundColor Green
        } else {
            Write-Host "No changes needed: $modulePath" -ForegroundColor Gray
        }
    } else {
        Write-Host "Not found: $modulePath" -ForegroundColor Red
    }
}

Write-Host "`nFixed $fixed module(s) to version $targetVersion" -ForegroundColor Cyan
