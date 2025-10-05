Write-Host '=== Win11Forge Profile Validation Test ===' -ForegroundColor Cyan
Write-Host "Testing all profiles in TestMode + Parallel mode`n" -ForegroundColor Yellow

$profiles = @('Base', 'Office', 'Gaming', 'Personnel')
$results = @()

foreach ($profile in $profiles) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Testing Profile: $profile" -ForegroundColor Yellow
    Write-Host "========================================`n" -ForegroundColor Cyan

    try {
        $output = & "$PSScriptRoot\Deploy-Win11Environment.ps1" -ProfileName $profile -TestMode -Parallel -ErrorAction Stop 2>&1 | Out-String

        # Extract key metrics
        $totalApps = if ($output -match 'Total applications to process:\s+(\d+)') { $Matches[1] } else { 'N/A' }
        $hasErrors = $output -match '(Error|Exception|FAILED.*:)'

        $result = [PSCustomObject]@{
            Profile = $profile
            TotalApps = $totalApps
            HasErrors = $hasErrors
            Status = if ($hasErrors) { 'FAILED' } else { 'PASSED' }
        }

        $results += $result

        if ($hasErrors) {
            Write-Host "  ❌ ERRORS DETECTED:" -ForegroundColor Red
            $output -split "`n" | Where-Object { $_ -match '(Error|Exception|FAILED)' } | Select-Object -First 5 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Red
            }
        } else {
            Write-Host "  ✅ PASSED - No errors detected" -ForegroundColor Green
            Write-Host "  Total Apps: $totalApps" -ForegroundColor Green
        }

    } catch {
        Write-Host "  ❌ EXCEPTION: $($_.Exception.Message)" -ForegroundColor Red
        $results += [PSCustomObject]@{
            Profile = $profile
            TotalApps = 'N/A'
            HasErrors = $true
            Status = 'EXCEPTION'
        }
    }
}

Write-Host "`n`n========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Cyan

$results | Format-Table -AutoSize

$passed = ($results | Where-Object { $_.Status -eq 'PASSED' }).Count
$total = $results.Count

Write-Host "`nResult: $passed/$total profiles passed" -ForegroundColor $(if ($passed -eq $total) { 'Green' } else { 'Yellow' })
