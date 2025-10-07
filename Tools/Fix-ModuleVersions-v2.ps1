# Fix all module versions to 2.5.0 (corrected version)

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

        # Fix malformed versions like $12.5.0
        $content = $content -replace '\$1\d+\.\d+\.\d+', "Version: $targetVersion"

        # Fix version patterns
        $content = $content -replace '(Module v)\d+\.\d+\.\d+', "`${1}$targetVersion"
        $content = $content -replace '(Version:\s*)\d+\.\d+\.\d+', "`${1}$targetVersion"
        $content = $content -replace 'v\d+\.\d+\.\d+(?=\s*\r?\n)', "v$targetVersion"

        if ($content -ne $originalContent) {
            Set-Content -Path $modulePath -Value $content -NoNewline
            $fixed++
            Write-Host "Fixed: $modulePath" -ForegroundColor Green
        } else {
            Write-Host "Already correct: $modulePath" -ForegroundColor Gray
        }
    }
}

Write-Host "`nFixed $fixed module(s) to version $targetVersion" -ForegroundColor Cyan
