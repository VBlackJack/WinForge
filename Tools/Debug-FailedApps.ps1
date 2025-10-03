<#
.SYNOPSIS
    Debug script for testing failed application installations

.DESCRIPTION
    Tests the corrected Winget/Chocolatey/Store IDs for previously failed applications

.NOTES
    Version: 1.0.0
    Created for Win11Forge debugging
#>

param(
    [switch]$TestAll,
    [switch]$Verbose
)

$ErrorActionPreference = 'Continue'

# Color-coded output
function Write-TestResult {
    param(
        [string]$AppName,
        [string]$Method,
        [string]$ID,
        [string]$Status,
        [string]$Message = ''
    )

    $color = switch ($Status) {
        'PASS' { 'Green' }
        'FAIL' { 'Red' }
        'SKIP' { 'Yellow' }
        default { 'White' }
    }

    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] " -NoNewline
    Write-Host "[$Status] " -ForegroundColor $color -NoNewline
    Write-Host "$AppName " -NoNewline
    Write-Host "($Method`: $ID) " -ForegroundColor Cyan -NoNewline
    if ($Message) {
        Write-Host "- $Message" -ForegroundColor Gray
    } else {
        Write-Host ""
    }
}

Write-Host "`n=== Win11Forge - Application ID Validation ===" -ForegroundColor Cyan
Write-Host "Testing corrected IDs for previously failed applications`n"

# Test data with corrected IDs
$testApps = @(
    @{
        Name = "Battle.net"
        Winget = $null
        Chocolatey = $null
        Store = "XPDM5VSMTKQLBJ"
    },
    @{
        Name = "WhatsApp Desktop"
        Winget = "9NKSQGP7F2NH"
        Chocolatey = $null
        Store = "9NKSQGP7F2NH"
    },
    @{
        Name = "Proton Drive"
        Winget = "Proton.ProtonDrive"
        Chocolatey = $null
        Store = $null
    },
    @{
        Name = "Proton Mail Bridge"
        Winget = "Proton.ProtonMailBridge"
        Chocolatey = $null
        Store = $null
    },
    @{
        Name = "Proton Pass"
        Winget = "Proton.ProtonPass"
        Chocolatey = $null
        Store = $null
    },
    @{
        Name = "Google Drive for Desktop"
        Winget = "Google.GoogleDrive"
        Chocolatey = "googledrive"
        Store = $null
    },
    @{
        Name = "PDF-XChange Editor Pro"
        Winget = "TrackerSoftware.PDF-XChangeEditor"
        Chocolatey = "pdfxchangeeditor"
        Store = $null
    }
)

$results = @{
    Total = 0
    Passed = 0
    Failed = 0
    Skipped = 0
}

foreach ($app in $testApps) {
    Write-Host "`n--- Testing: $($app.Name) ---" -ForegroundColor Yellow

    # Test Winget
    if ($app.Winget) {
        $results.Total++
        try {
            $search = winget search --id $app.Winget --exact 2>&1 | Out-String
            if ($search -match $app.Winget) {
                Write-TestResult -AppName $app.Name -Method "Winget" -ID $app.Winget -Status "PASS" -Message "ID found in repository"
                $results.Passed++
            } else {
                Write-TestResult -AppName $app.Name -Method "Winget" -ID $app.Winget -Status "FAIL" -Message "ID not found"
                $results.Failed++
            }
        } catch {
            Write-TestResult -AppName $app.Name -Method "Winget" -ID $app.Winget -Status "FAIL" -Message $_.Exception.Message
            $results.Failed++
        }
    } else {
        Write-TestResult -AppName $app.Name -Method "Winget" -ID "N/A" -Status "SKIP" -Message "No Winget ID configured"
        $results.Skipped++
    }

    # Test Chocolatey
    if ($app.Chocolatey) {
        $results.Total++
        try {
            $search = choco search $app.Chocolatey --exact --limit-output 2>&1
            if ($LASTEXITCODE -eq 0 -and $search) {
                Write-TestResult -AppName $app.Name -Method "Chocolatey" -ID $app.Chocolatey -Status "PASS" -Message "Package found"
                $results.Passed++
            } else {
                Write-TestResult -AppName $app.Name -Method "Chocolatey" -ID $app.Chocolatey -Status "FAIL" -Message "Package not found"
                $results.Failed++
            }
        } catch {
            Write-TestResult -AppName $app.Name -Method "Chocolatey" -ID $app.Chocolatey -Status "FAIL" -Message $_.Exception.Message
            $results.Failed++
        }
    } else {
        Write-TestResult -AppName $app.Name -Method "Chocolatey" -ID "N/A" -Status "SKIP" -Message "No Chocolatey package configured"
        $results.Skipped++
    }

    # Test Store (basic validation - just check ID format)
    if ($app.Store) {
        $results.Total++
        if ($app.Store -match '^[A-Z0-9]{12,}$' -or $app.Store -match '^\d[A-Z0-9]{11,}$') {
            Write-TestResult -AppName $app.Name -Method "Store" -ID $app.Store -Status "PASS" -Message "Valid Store ID format"
            $results.Passed++
        } else {
            Write-TestResult -AppName $app.Name -Method "Store" -ID $app.Store -Status "FAIL" -Message "Invalid Store ID format"
            $results.Failed++
        }
    } else {
        Write-TestResult -AppName $app.Name -Method "Store" -ID "N/A" -Status "SKIP" -Message "No Store ID configured"
        $results.Skipped++
    }
}

# Summary
Write-Host "`n=== Validation Summary ===" -ForegroundColor Cyan
Write-Host "Total tests:   $($results.Total)"
Write-Host "Passed:        " -NoNewline
Write-Host "$($results.Passed)" -ForegroundColor Green
Write-Host "Failed:        " -NoNewline
Write-Host "$($results.Failed)" -ForegroundColor Red
Write-Host "Skipped:       " -NoNewline
Write-Host "$($results.Skipped)" -ForegroundColor Yellow

$successRate = if ($results.Total -gt 0) {
    [math]::Round(($results.Passed / $results.Total) * 100, 2)
} else {
    0
}

Write-Host "`nSuccess Rate:  " -NoNewline
$rateColor = if ($successRate -ge 80) { 'Green' } elseif ($successRate -ge 60) { 'Yellow' } else { 'Red' }
Write-Host "$successRate%" -ForegroundColor $rateColor

if ($results.Failed -gt 0) {
    Write-Host "`n⚠️  Some IDs failed validation. Review the profile JSONs." -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n✅ All IDs validated successfully!" -ForegroundColor Green
    exit 0
}
