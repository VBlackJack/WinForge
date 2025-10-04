# Test Quick Assist Detection Logic
$ErrorActionPreference = 'Continue'

Write-Host "=== Testing Quick Assist Detection ===" -ForegroundColor Cyan
Write-Host ""

$appName = 'Microsoft Quick Assist'
$baseAppName = $appName -replace ' Desktop$', '' -replace ' App$', ''

Write-Host "App Name: $appName" -ForegroundColor Yellow
Write-Host "Base Name: $baseAppName" -ForegroundColor Yellow
Write-Host ""

# Test: Detection by base name
Write-Host "Test: winget list (search for '$baseAppName')" -ForegroundColor Cyan
$wingetList = & winget list --accept-source-agreements 2>&1 | Out-String
Write-Host "Searching in winget output..."

$patterns = @(
    [regex]::Escape($baseAppName),
    [regex]::Escape($appName),
    'Quick Assist',
    'Assistance rapide',
    'QuickAssist'
)

$found = $false
foreach ($pattern in $patterns) {
    if ($wingetList -match $pattern) {
        Write-Host "[OK] DETECTED with pattern: $pattern" -ForegroundColor Green
        $matchingLine = ($wingetList -split "`n") | Where-Object { $_ -match $pattern } | Select-Object -First 1
        Write-Host "Matching line: $matchingLine" -ForegroundColor Gray
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "[FAIL] NOT DETECTED with any pattern" -ForegroundColor Red
    Write-Host ""
    Write-Host "Full winget list output:" -ForegroundColor Yellow
    Write-Host $wingetList
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
