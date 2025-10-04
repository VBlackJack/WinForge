# Test PackageName prefix matching
$packageName = "MicrosoftCorporationII.QuickAssist"
$packagePrefix = $packageName.Substring(0, [Math]::Min(35, $packageName.Length))

Write-Host "=== Testing PackageName Prefix Matching ===" -ForegroundColor Cyan
Write-Host "Full PackageName: $packageName" -ForegroundColor Yellow
Write-Host "Prefix (35 chars): $packagePrefix" -ForegroundColor Yellow
Write-Host "Prefix length: $($packagePrefix.Length)" -ForegroundColor Gray
Write-Host ""

$wingetList = winget list --accept-source-agreements 2>&1 | Out-String

Write-Host "Testing match..." -ForegroundColor Cyan
$installed = $wingetList -match [regex]::Escape($packagePrefix)

if ($installed) {
    Write-Host "[OK] DETECTED" -ForegroundColor Green
    $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($packagePrefix) } | Select-Object -First 1
    Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
}
