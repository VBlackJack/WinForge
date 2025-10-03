<#
.SYNOPSIS
    Validates the application database
.DESCRIPTION
    Tests all application IDs in the centralized database and generates a validation report
.PARAMETER ValidateWinget
    Validate Winget IDs (requires winget installed)
.PARAMETER ValidateChocolatey
    Validate Chocolatey packages (requires choco installed)
.PARAMETER GenerateReport
    Generate HTML report of validation results
.PARAMETER ReportPath
    Path for the HTML report (default: Tools\ValidationReport.html)
.EXAMPLE
    .\Tools\Validate-AppDatabase.ps1 -ValidateWinget -ValidateChocolatey
.EXAMPLE
    .\Tools\Validate-AppDatabase.ps1 -GenerateReport
#>

param(
    [switch]$ValidateWinget,
    [switch]$ValidateChocolatey,
    [switch]$GenerateReport,
    [string]$ReportPath = ".\Tools\ValidationReport.html"
)

# Import required modules
$ModulePath = Join-Path $PSScriptRoot "..\Modules\ApplicationDatabase.psm1"
if (-not (Test-Path $ModulePath)) {
    Write-Error "ApplicationDatabase module not found at: $ModulePath"
    exit 1
}

Import-Module $ModulePath -Force

# Display banner
Write-Host "`n=======================================" -ForegroundColor Cyan
Write-Host "  Application Database Validator v1.0" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Get database statistics
Write-Host "`nLoading database..." -ForegroundColor Yellow
$stats = Get-DatabaseStatistics

if ($null -eq $stats) {
    Write-Host "❌ Failed to load database" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Database loaded successfully" -ForegroundColor Green
Write-Host "`nDatabase Statistics:" -ForegroundColor Cyan
Write-Host "  Version          : $($stats.DatabaseVersion)" -ForegroundColor White
Write-Host "  Last Updated     : $($stats.LastUpdated)" -ForegroundColor White
Write-Host "  Total Apps       : $($stats.TotalApplications)" -ForegroundColor White
Write-Host "  Categories       : $($stats.TotalCategories)" -ForegroundColor White
Write-Host "  Tags             : $($stats.TotalTags)" -ForegroundColor White
Write-Host "  Verified Apps    : $($stats.VerifiedApps)" -ForegroundColor Green
Write-Host "  Apps with Winget : $($stats.AppsWithWinget)" -ForegroundColor White
Write-Host "  Apps with Choco  : $($stats.AppsWithChocolatey)" -ForegroundColor White
Write-Host "  Apps with Store  : $($stats.AppsWithStore)" -ForegroundColor White
Write-Host "  Apps with DirUrl : $($stats.AppsWithDirectUrl)" -ForegroundColor White

# Validate source availability
if ($ValidateWinget -or $ValidateChocolatey) {
    Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
    Write-Host "  Source Validation" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan

    # Check prerequisites
    $canValidateWinget = $ValidateWinget -and (Get-Command winget -ErrorAction SilentlyContinue)
    $canValidateChoco = $ValidateChocolatey -and (Get-Command choco -ErrorAction SilentlyContinue)

    if ($ValidateWinget -and -not $canValidateWinget) {
        Write-Host "⚠️  Winget not found - skipping Winget validation" -ForegroundColor Yellow
    }

    if ($ValidateChocolatey -and -not $canValidateChoco) {
        Write-Host "⚠️  Chocolatey not found - skipping Chocolatey validation" -ForegroundColor Yellow
    }

    if ($canValidateWinget -or $canValidateChoco) {
        Write-Host "`nValidating application sources (this may take a while)...`n" -ForegroundColor Yellow

        $allApps = Get-AllApplications
        $totalApps = ($allApps | Measure-Object).Count
        $validApps = 0
        $invalidApps = 0
        $errors = @()

        foreach ($app in $allApps) {
            $hasValidSource = $false
            $appErrors = @()

            # Test Winget
            if ($canValidateWinget -and $app.Sources.Winget) {
                Write-Host "  Testing $($app.Name) [Winget: $($app.Sources.Winget)]..." -NoNewline
                try {
                    $result = winget search --id $app.Sources.Winget --exact 2>&1 | Out-String
                    if ($LASTEXITCODE -eq 0 -and $result -like "*$($app.Sources.Winget)*") {
                        Write-Host " ✅" -ForegroundColor Green
                        $hasValidSource = $true
                    }
                    else {
                        Write-Host " ❌" -ForegroundColor Red
                        $appErrors += "Winget ID not found: $($app.Sources.Winget)"
                    }
                }
                catch {
                    Write-Host " ❌" -ForegroundColor Red
                    $appErrors += "Winget validation error: $_"
                }
            }

            # Test Chocolatey
            if ($canValidateChoco -and $app.Sources.Chocolatey) {
                Write-Host "  Testing $($app.Name) [Choco: $($app.Sources.Chocolatey)]..." -NoNewline
                try {
                    $result = choco search $app.Sources.Chocolatey --exact --limit-output 2>&1
                    if ($LASTEXITCODE -eq 0 -and $result) {
                        Write-Host " ✅" -ForegroundColor Green
                        $hasValidSource = $true
                    }
                    else {
                        Write-Host " ❌" -ForegroundColor Red
                        $appErrors += "Chocolatey package not found: $($app.Sources.Chocolatey)"
                    }
                }
                catch {
                    Write-Host " ❌" -ForegroundColor Red
                    $appErrors += "Chocolatey validation error: $_"
                }
            }

            # Check DirectUrl
            if ($app.Sources.DirectUrl) {
                $hasValidSource = $true
            }

            if ($hasValidSource) {
                $validApps++
            }
            else {
                $invalidApps++
                if ($appErrors.Count -gt 0) {
                    $errors += [PSCustomObject]@{
                        App    = $app.Name
                        AppId  = $app.AppId
                        Errors = $appErrors -join "; "
                    }
                }
            }
        }

        Write-Host "`n" + ("-" * 60) -ForegroundColor Cyan
        Write-Host "Validation Summary:" -ForegroundColor Cyan
        Write-Host "  Total Apps  : $totalApps" -ForegroundColor White
        Write-Host "  Valid       : $validApps " -NoNewline
        Write-Host "✅" -ForegroundColor Green
        Write-Host "  Invalid     : $invalidApps " -NoNewline
        if ($invalidApps -gt 0) {
            Write-Host "❌" -ForegroundColor Red
        }
        else {
            Write-Host "✅" -ForegroundColor Green
        }

        $successRate = [math]::Round(($validApps / $totalApps) * 100, 2)
        Write-Host "  Success Rate: $successRate%" -ForegroundColor $(if ($successRate -ge 95) { "Green" } elseif ($successRate -ge 80) { "Yellow" } else { "Red" })

        if ($errors.Count -gt 0) {
            Write-Host "`n❌ Errors found:" -ForegroundColor Red
            foreach ($error in $errors) {
                Write-Host "  - $($error.App) ($($error.AppId)): $($error.Errors)" -ForegroundColor Red
            }
        }
    }
}

# Category breakdown
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "  Category Breakdown" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$categories = Get-ApplicationCategories | Sort-Object -Property Count -Descending

foreach ($cat in $categories) {
    $apps = Get-AllApplications -Category $cat.CategoryId
    $verified = ($apps | Where-Object { $_.Verified } | Measure-Object).Count
    Write-Host ("  {0,-20} : {1,2} apps " -f $cat.DisplayName, $cat.Count) -NoNewline
    Write-Host "($verified verified)" -ForegroundColor $(if ($verified -eq $cat.Count) { "Green" } else { "Yellow" })
}

# Tag breakdown
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "  Popular Tags" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$popularTags = @("essential", "popular", "open-source", "microsoft", "privacy")
foreach ($tag in $popularTags) {
    $count = (Get-AllApplications -Tag $tag | Measure-Object).Count
    if ($count -gt 0) {
        Write-Host ("  {0,-15} : {1,2} apps" -f $tag, $count) -ForegroundColor White
    }
}

# Generate HTML report
if ($GenerateReport) {
    Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
    Write-Host "  Generating HTML Report" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan

    $allApps = Get-AllApplications | Sort-Object -Property Category, Name

    $htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <title>Win11Forge - Application Database Report</title>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        h1 { color: #667eea; border-bottom: 3px solid #667eea; padding-bottom: 10px; }
        h2 { color: #764ba2; margin-top: 30px; }
        .stats { background: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .stat-item { display: inline-block; margin: 10px 20px 10px 0; }
        .stat-value { font-size: 2em; font-weight: bold; color: #667eea; }
        .stat-label { color: #666; font-size: 0.9em; }
        table { width: 100%; border-collapse: collapse; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        th { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #eee; }
        tr:hover { background: #f9f9f9; }
        .verified { color: #28a745; font-weight: bold; }
        .not-verified { color: #dc3545; }
        .source { display: inline-block; background: #e9ecef; padding: 3px 8px; border-radius: 4px; margin: 2px; font-size: 0.85em; }
        .tag { display: inline-block; background: #667eea; color: white; padding: 3px 8px; border-radius: 12px; margin: 2px; font-size: 0.75em; }
    </style>
</head>
<body>
    <h1>🛠️ Win11Forge - Application Database Report</h1>
    <p>Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>

    <div class="stats">
        <div class="stat-item">
            <div class="stat-value">$($stats.TotalApplications)</div>
            <div class="stat-label">Total Applications</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($stats.TotalCategories)</div>
            <div class="stat-label">Categories</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($stats.VerifiedApps)</div>
            <div class="stat-label">Verified</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($stats.AppsWithWinget)</div>
            <div class="stat-label">With Winget</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($stats.AppsWithChocolatey)</div>
            <div class="stat-label">With Chocolatey</div>
        </div>
    </div>

    <h2>📦 All Applications</h2>
    <table>
        <tr>
            <th>Name</th>
            <th>Category</th>
            <th>Sources</th>
            <th>Tags</th>
            <th>Verified</th>
        </tr>
"@

    foreach ($app in $allApps) {
        $sources = @()
        if ($app.Sources.Winget) { $sources += "<span class='source'>Winget</span>" }
        if ($app.Sources.Chocolatey) { $sources += "<span class='source'>Choco</span>" }
        if ($app.Sources.Store) { $sources += "<span class='source'>Store</span>" }
        if ($app.Sources.DirectUrl) { $sources += "<span class='source'>DirectUrl</span>" }

        $tags = ($app.Tags | ForEach-Object { "<span class='tag'>$_</span>" }) -join " "

        $verifiedStatus = if ($app.Verified) { "<span class='verified'>✅ Yes</span>" } else { "<span class='not-verified'>❌ No</span>" }

        $htmlContent += @"
        <tr>
            <td><strong>$($app.Name)</strong><br><small>$($app.AppId)</small></td>
            <td>$($app.Category)</td>
            <td>$($sources -join " ")</td>
            <td>$tags</td>
            <td>$verifiedStatus</td>
        </tr>
"@
    }

    $htmlContent += @"
    </table>
</body>
</html>
"@

    $htmlContent | Out-File -FilePath $ReportPath -Encoding UTF8
    Write-Host "✅ Report generated: $ReportPath" -ForegroundColor Green
}

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "✅ Validation Complete!" -ForegroundColor Green
Write-Host ("=" * 60) + "`n" -ForegroundColor Cyan
