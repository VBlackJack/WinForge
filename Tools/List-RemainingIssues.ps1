Import-Module PSScriptAnalyzer -Force

# Use framework PSScriptAnalyzer settings
$settingsFile = Join-Path $PSScriptRoot '..\PSScriptAnalyzerSettings.psd1'

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
        $issues = Invoke-ScriptAnalyzer -Path $fullPath -Settings $settingsFile -Severity Warning
        $allIssues += $issues
    }
}

# Exclude PSUseShouldProcessForStateChangingFunctions (too complex for now)
$criticalIssues = $allIssues | Where-Object { $_.RuleName -ne 'PSUseShouldProcessForStateChangingFunctions' }

Write-Host "`nCritical Issues to Fix: $($criticalIssues.Count)`n" -ForegroundColor Yellow

foreach ($issue in $criticalIssues) {
    Write-Host "File: " -NoNewline -ForegroundColor Cyan
    Write-Host (Split-Path $issue.ScriptName -Leaf)
    Write-Host "Line $($issue.Line): " -NoNewline -ForegroundColor Yellow
    Write-Host $issue.RuleName -ForegroundColor Red
    Write-Host "  $($issue.Message)" -ForegroundColor Gray
    Write-Host ""
}
