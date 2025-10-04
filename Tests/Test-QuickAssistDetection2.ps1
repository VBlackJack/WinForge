# Test Quick Assist Detection with new logic
$ErrorActionPreference = 'Continue'

Write-Host "=== Testing Quick Assist Detection (New Logic) ===" -ForegroundColor Cyan
Write-Host ""

# Simulate app object
$app = @{
    Name = 'Microsoft Quick Assist'
    Sources = @{ Store = '9P7BP5VNWKX5' }
    Detection = @{ PackageName = 'MicrosoftCorporationII.QuickAssist' }
}

Write-Host "App Name: $($app.Name)" -ForegroundColor Yellow
Write-Host "Store ID: $($app.Sources.Store)" -ForegroundColor Yellow
Write-Host "Package Name: $($app.Detection.PackageName)" -ForegroundColor Yellow
Write-Host ""

# Get winget list once
$wingetList = & winget list --accept-source-agreements 2>&1 | Out-String

# Strategy 1: Store ID
Write-Host "Strategy 1: Detect by Store ID ($($app.Sources.Store))" -ForegroundColor Cyan
$installed = $wingetList -match [regex]::Escape($app.Sources.Store) -and $wingetList -notmatch "No installed package"
if ($installed) {
    Write-Host "[OK] DETECTED" -ForegroundColor Green
} else {
    Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
}
Write-Host ""

# Strategy 2: PackageName
Write-Host "Strategy 2: Detect by PackageName ($($app.Detection.PackageName))" -ForegroundColor Cyan
$installed = $wingetList -match [regex]::Escape($app.Detection.PackageName)
if ($installed) {
    Write-Host "[OK] DETECTED" -ForegroundColor Green
    $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($app.Detection.PackageName) } | Select-Object -First 1
    Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
}
Write-Host ""

# Strategy 3: Base name (strip 'Microsoft ')
$baseAppName = $app.Name -replace '^Microsoft ', '' -replace ' Desktop$', '' -replace ' App$', ''
Write-Host "Strategy 3: Detect by base name ($baseAppName)" -ForegroundColor Cyan
$installed = $wingetList -match [regex]::Escape($baseAppName)
if ($installed) {
    Write-Host "[OK] DETECTED" -ForegroundColor Green
    $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($baseAppName) } | Select-Object -First 1
    Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Complete ===" -ForegroundColor Cyan
