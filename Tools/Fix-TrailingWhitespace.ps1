# Fix trailing whitespace in all PowerShell files
$files = Get-ChildItem -Path . -Include *.ps1,*.psm1 -Recurse |
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Logs\*' }

$fixedCount = 0
$totalLines = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Remove trailing whitespace from each line
    $lines = $content -split "`r?`n"
    $fixedLines = $lines | ForEach-Object { $_.TrimEnd() }
    $newContent = $fixedLines -join "`r`n"

    # Add final newline if missing
    if (-not $newContent.EndsWith("`r`n")) {
        $newContent += "`r`n"
    }

    if ($newContent -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        $fixedCount++
        $linesFixed = ($lines | Where-Object { $_ -match '\s+$' }).Count
        $totalLines += $linesFixed
        Write-Host "Fixed $($file.Name): $linesFixed lines" -ForegroundColor Green
    }
}

Write-Host "`nTotal files fixed: $fixedCount" -ForegroundColor Cyan
Write-Host "Total lines fixed: $totalLines" -ForegroundColor Cyan
