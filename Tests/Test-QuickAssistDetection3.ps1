# Test Quick Assist Detection with keyword fallback
$ErrorActionPreference = 'Continue'

Write-Host "=== Testing Quick Assist Detection (Keyword Fallback) ===" -ForegroundColor Cyan
Write-Host ""

$appName = 'Microsoft Quick Assist'
$baseAppName = $appName -replace '^Microsoft ', '' -replace ' Desktop$', '' -replace ' App$', ''

Write-Host "App Name: $appName" -ForegroundColor Yellow
Write-Host "Base Name: $baseAppName" -ForegroundColor Yellow

# Extract keyword
$keyWord = ($baseAppName -split '\s+' | Where-Object { $_.Length -gt 4 }) | Select-Object -Last 1
Write-Host "Keyword: $keyWord" -ForegroundColor Yellow
Write-Host ""

$wingetList = & winget list --accept-source-agreements 2>&1 | Out-String

# Test keyword match
Write-Host "Test: Detect by keyword '$keyWord'" -ForegroundColor Cyan
$installed = $wingetList -match [regex]::Escape($keyWord)
if ($installed) {
    Write-Host "[OK] DETECTED" -ForegroundColor Green
    $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($keyWord) } | Select-Object -First 1
    Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] NOT DETECTED" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Complete ===" -ForegroundColor Cyan
