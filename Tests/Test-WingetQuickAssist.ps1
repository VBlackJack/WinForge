# Check what winget shows for Quick Assist
$output = winget list --accept-source-agreements 2>&1 | Out-String

Write-Host "=== Searching for Quick Assist patterns ===" -ForegroundColor Cyan
Write-Host ""

$patterns = @(
    'Assist',
    'QuickAssist',
    'MicrosoftCorporation',
    'rapide',
    'Assistance'
)

foreach ($pattern in $patterns) {
    Write-Host "Pattern: $pattern" -ForegroundColor Yellow
    $lines = $output -split "`n" | Where-Object { $_ -match $pattern }
    if ($lines) {
        foreach ($line in $lines) {
            Write-Host "  $line" -ForegroundColor Gray
        }
    } else {
        Write-Host "  [No matches]" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "=== Full winget list output (first 30 lines) ===" -ForegroundColor Cyan
($output -split "`n" | Select-Object -First 30) | ForEach-Object { Write-Host $_ }
