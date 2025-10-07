Import-Module ./Modules/ApplicationDatabase.psm1 -Force

$problematicApps = @('WindowsSandbox', 'ProtonDrive', 'ProtonMailBridge', 'ProtonPass')

foreach ($appId in $problematicApps) {
    $app = Get-ApplicationById -AppId $appId
    if ($app) {
        Write-Host "`n=== $appId ===" -ForegroundColor Cyan
        Write-Host "Name: $($app.Name)"
        Write-Host "Category: $($app.Category)"

        $sources = @()
        if ($app.Sources.Winget) { $sources += "Winget: $($app.Sources.Winget)" }
        if ($app.Sources.Chocolatey) { $sources += "Chocolatey: $($app.Sources.Chocolatey)" }
        if ($app.Sources.Store) { $sources += "Store: $($app.Sources.Store)" }
        if ($app.Sources.DirectUrl) { $sources += "DirectUrl: $($app.Sources.DirectUrl)" }

        if ($sources.Count -eq 0) {
            Write-Host "Sources: NONE" -ForegroundColor Red
        } else {
            Write-Host "Sources:" -ForegroundColor Green
            $sources | ForEach-Object { Write-Host "  $_" }
        }

        if ($app.Detection) {
            Write-Host "Detection: Present" -ForegroundColor Green
        } else {
            Write-Host "Detection: MISSING" -ForegroundColor Red
        }
    } else {
        Write-Host "`n=== $appId ===" -ForegroundColor Red
        Write-Host "NOT FOUND IN DATABASE" -ForegroundColor Red
    }
}
