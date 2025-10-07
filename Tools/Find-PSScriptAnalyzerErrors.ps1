Import-Module PSScriptAnalyzer -Force

$files = Get-ChildItem -Path . -Include *.ps1,*.psm1 -Recurse |
    Where-Object { $_.FullName -notlike '*\.git\*' -and $_.FullName -notlike '*\Tests\*' -and $_.FullName -notlike '*\Logs\*' }

$errors = @()
foreach ($file in $files) {
    $issues = Invoke-ScriptAnalyzer -Path $file.FullName -Severity Error
    $errors += $issues
}

Write-Host "`nTotal Errors Found: $($errors.Count)" -ForegroundColor Red
Write-Host ""

foreach ($issue in $errors) {
    Write-Host "File: " -NoNewline -ForegroundColor Yellow
    Write-Host (Split-Path $issue.ScriptName -Leaf)
    Write-Host "Line: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.Line
    Write-Host "Rule: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.RuleName
    Write-Host "Message: " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.Message
    Write-Host ""
}
