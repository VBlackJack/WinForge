$readmeContent = Get-Content 'README.md' -Raw
$versionPattern = '(\d+\.\d+\.\d+)'

Write-Host "=== Testing version regex on README ===" -ForegroundColor Cyan

# Test 1: Line 35 pattern (what works)
if ($readmeContent -match "Win11Forge Framework v$versionPattern") {
    Write-Host "Pattern 1 (Win11Forge Framework v...): $($matches[1])" -ForegroundColor Green
}

# Test 2: Line 178 pattern (what's failing)
if ($readmeContent -match "v$versionPattern") {
    Write-Host "Pattern 2 (v...): $($matches[1])" -ForegroundColor Yellow
}

# Show what $uniqueVersions would be
$versions = @{}
$versions['README.md'] = '2.5.0'
$versions['Deploy'] = '2.5.0'
$versions['Module1'] = '2.5.0'

$uniqueVersions = $versions.Values | Select-Object -Unique
Write-Host "`nUnique versions array: $($uniqueVersions -join ', ')" -ForegroundColor Cyan
Write-Host "First unique version: $($uniqueVersions[0])" -ForegroundColor Cyan
