# Test PackageName prefix extraction
$packageName = "MicrosoftCorporationII.QuickAssist"

Write-Host "=== Testing PackageName Prefix Extraction ===" -ForegroundColor Cyan
Write-Host "Full PackageName: $packageName" -ForegroundColor Yellow

if ($packageName -match '^([^.]+)\.') {
    $packagePrefix = $matches[1]
    Write-Host "Extracted Prefix: $packagePrefix" -ForegroundColor Yellow
    Write-Host ""

    $wingetList = winget list --accept-source-agreements 2>&1 | Out-String

    Write-Host "Testing match..." -ForegroundColor Cyan
    $installed = $wingetList -match [regex]::Escape($packagePrefix)

    if ($installed) {
        Write-Host "[OK] DETECTED" -ForegroundColor Green
        $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($packagePrefix) } | Select-Object -First 5
        Write-Host "Matching lines:" -ForegroundColor Gray
        $matchingLine | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    } else {
        Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
    }
} else {
    Write-Host "[ERROR] Pattern did not match" -ForegroundColor Red
}
