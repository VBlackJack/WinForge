Import-Module PSScriptAnalyzer -Force

$files = @(
    'Deploy-Win11Environment.ps1',
    'Modules/InstallationEngine.psm1',
    'Modules/Win11ForgeGUI.psm1',
    'Modules/ApplicationDatabase.psm1',
    'Modules/ProfileManager.psm1',
    'Modules/SystemConfig.psm1',
    'Core/Core.psm1',
    'Modules/Prerequisites.psm1',
    'Modules/EnvironmentDetection.psm1',
    'Modules/StartMenuLayout.psm1',
    'Modules/StartMenuPinning.psm1',
    'Modules/StartupManager.psm1'
)

$allIssues = @()
foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot "..\$file"
    if (Test-Path $fullPath) {
        $issues = Invoke-ScriptAnalyzer -Path $fullPath -Severity Warning,Error
        $allIssues += $issues
    }
}

Write-Host "`nTotal Issues: $($allIssues.Count)" -ForegroundColor Yellow
Write-Host "`nSeverity Breakdown:" -ForegroundColor Cyan
$allIssues | Group-Object Severity | Format-Table @{L='Severity';E={$_.Name}}, @{L='Count';E={$_.Count}} -AutoSize

Write-Host "Top 10 Issues:" -ForegroundColor Cyan
$allIssues | Group-Object RuleName | Sort-Object Count -Descending | Select-Object -First 10 | Format-Table @{L='Count';E={$_.Count}}, @{L='Rule';E={$_.Name}} -AutoSize
