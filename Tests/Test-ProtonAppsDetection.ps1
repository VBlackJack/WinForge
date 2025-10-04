# Test Proton Apps Detection
# Author: Julien Bombled
Write-Host "=== Testing Proton Apps Detection ===" -ForegroundColor Cyan
Write-Host ""

$protonApps = @(
    @{ Name = "Proton Drive"; Path = "C:\Program Files\Proton\Drive\Proton Drive.exe" },
    @{ Name = "Proton Mail Bridge"; Path = "C:\Program Files\Proton\Mail Bridge\bridge.exe" },
    @{ Name = "Proton Pass"; Path = "C:\Program Files\Proton\Pass\Proton Pass.exe" }
)

foreach ($app in $protonApps) {
    Write-Host "App: $($app.Name)" -ForegroundColor Yellow
    Write-Host "Expected path: $($app.Path)" -ForegroundColor Gray

    $exists = Test-Path $app.Path
    if ($exists) {
        Write-Host "[OK] File found" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] File NOT found" -ForegroundColor Red

        # Try to find actual location
        $basePath = "C:\Program Files\Proton"
        if (Test-Path $basePath) {
            Write-Host "Searching in $basePath..." -ForegroundColor Cyan
            $found = Get-ChildItem -Path $basePath -Recurse -Filter "*.exe" -ErrorAction SilentlyContinue |
                     Where-Object { $_.Name -like "*$($app.Name.Split(' ')[1])*" } |
                     Select-Object -First 3 FullName

            if ($found) {
                Write-Host "Possible locations:" -ForegroundColor Yellow
                $found | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            }
        }
    }
    Write-Host ""
}

Write-Host "=== Winget Detection ===" -ForegroundColor Cyan
$wingetList = winget list 2>&1 | Out-String
$protonLines = $wingetList -split "`n" | Where-Object { $_ -match 'Proton' }
Write-Host "Proton apps in winget:" -ForegroundColor Yellow
$protonLines | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
