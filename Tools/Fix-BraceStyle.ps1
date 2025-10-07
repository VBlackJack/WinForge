# Fix brace placement style issues
Import-Module PSScriptAnalyzer -Force

$files = Get-ChildItem -Path . -Include *.ps1,*.psm1 -Recurse |
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Logs\*' }

$fixedCount = 0

foreach ($file in $files) {
    $issues = Invoke-ScriptAnalyzer -Path $file.FullName -IncludeRule PSPlaceCloseBrace

    if ($issues.Count -gt 0) {
        Write-Host "Found $($issues.Count) brace issues in $($file.Name)" -ForegroundColor Yellow

        # Use Invoke-Formatter to fix brace placement
        $content = Get-Content $file.FullName -Raw
        $settings = @{
            Rules = @{
                PSPlaceCloseBrace = @{
                    Enable = $true
                    NoEmptyLineBefore = $false
                    IgnoreOneLineBlock = $true
                    NewLineAfter = $true
                }
                PSPlaceOpenBrace = @{
                    Enable = $true
                    OnSameLine = $true
                    NewLineAfter = $true
                    IgnoreOneLineBlock = $true
                }
            }
        }

        try {
            $formatted = Invoke-Formatter -ScriptDefinition $content -Settings $settings
            Set-Content -Path $file.FullName -Value $formatted -NoNewline
            $fixedCount++
            Write-Host "  Fixed $($file.Name)" -ForegroundColor Green
        } catch {
            Write-Host "  Error formatting $($file.Name): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`nTotal files formatted: $fixedCount" -ForegroundColor Cyan
