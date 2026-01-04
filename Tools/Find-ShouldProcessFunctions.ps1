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

$shouldProcessIssues = @()
foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot "..\$file"
    if (Test-Path $fullPath) {
        $issues = Invoke-ScriptAnalyzer -Path $fullPath -IncludeRule PSUseShouldProcessForStateChangingFunctions
        $shouldProcessIssues += $issues
    }
}

Write-Host "`nFunctions requiring ShouldProcess: $($shouldProcessIssues.Count)`n" -ForegroundColor Yellow

foreach ($issue in $shouldProcessIssues) {
    # Extract function name from message
    if ($issue.Message -match "Function '([^']+)'") {
        $functionName = $matches[1]
    } else {
        $functionName = "Unknown"
    }

    Write-Host "File: " -NoNewline -ForegroundColor Cyan
    Write-Host (Split-Path $issue.ScriptName -Leaf)
    Write-Host "  Line $($issue.Line): " -NoNewline -ForegroundColor Yellow
    Write-Host $functionName -ForegroundColor Magenta
    Write-Host "  $($issue.Message)" -ForegroundColor Gray
    Write-Host ""
}
