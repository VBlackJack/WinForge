# Test Store App Detection Logic
# Author: Julien Bombled
$ErrorActionPreference = 'Continue'

Write-Host "=== Testing Store App Detection Logic ===" -ForegroundColor Cyan
Write-Host ""

$storeId = '9NKSQGP7F2NH'
$appName = 'WhatsApp Desktop'
$baseAppName = $appName -replace ' Desktop$', '' -replace ' App$', ''

Write-Host "Store ID: $storeId" -ForegroundColor Yellow
Write-Host "App Name: $appName" -ForegroundColor Yellow
Write-Host "Base Name: $baseAppName" -ForegroundColor Yellow
Write-Host ""

# Test 1: Detection by Store ID
Write-Host "Test 1: winget list --id $storeId" -ForegroundColor Cyan
$wingetList = & winget list --id $storeId --accept-source-agreements 2>&1 | Out-String
Write-Host "Output:"
Write-Host $wingetList
$installed = $wingetList -match [regex]::Escape($storeId) -and $wingetList -notmatch "No installed package"
if ($installed) {
    Write-Host "[OK] DETECTED via Store ID" -ForegroundColor Green
} else {
    Write-Host "[FAIL] NOT DETECTED via Store ID" -ForegroundColor Red
}
Write-Host ""

# Test 2: Detection by base name
Write-Host "Test 2: winget list (search for '$baseAppName')" -ForegroundColor Cyan
$wingetList = & winget list --accept-source-agreements 2>&1 | Out-String
$installed = $wingetList -match [regex]::Escape($baseAppName)
if ($installed) {
    Write-Host "[OK] DETECTED via base name" -ForegroundColor Green
    # Show matching line
    $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match [regex]::Escape($baseAppName) } | Select-Object -First 1
    Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
} else {
    Write-Host "[FAIL] NOT DETECTED via base name" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Complete ===" -ForegroundColor Cyan
