<#
.SYNOPSIS
    Automated Battle.net silent installation validator

.DESCRIPTION
    Tests if Battle.net installs truly silently with the recommended switches
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Continue'

Write-Host "`n=== Battle.net Silent Installation Validator ===" -ForegroundColor Cyan
Write-Host "Automated test for silent switches`n"

# Configuration
$BattleNetUrl = "https://downloader.battle.net/download/getInstallerForGame?os=win&gameProgram=BATTLENET_APP&version=Live"
$TempDir = Join-Path $env:TEMP "BattleNetTest_$(Get-Random)"
$InstallerPath = Join-Path $TempDir "Battle.net-Setup.exe"
$InstallPath = "C:\Program Files (x86)\Battle.net"
$ExePath = Join-Path $InstallPath "Battle.net.exe"

# Check if already installed
if (Test-Path $ExePath) {
    Write-Host "[WARNING] Battle.net is already installed at: $ExePath" -ForegroundColor Yellow
    Write-Host "Uninstall first if you want to test fresh installation`n"
    $continue = Read-Host "Continue anyway? (Y/N)"
    if ($continue -ne 'Y') {
        exit 0
    }
}

# Create temp directory
New-Item -Path $TempDir -ItemType Directory -Force | Out-Null

Write-Host "[1/4] Downloading Battle.net installer..." -ForegroundColor Yellow
try {
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $BattleNetUrl -OutFile $InstallerPath -UseBasicParsing
    $fileSize = (Get-Item $InstallerPath).Length / 1MB
    Write-Host "      Downloaded: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
} catch {
    Write-Host "      ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test configuration
$testConfigs = @(
    @{
        Name = "Perplexity Verified (--lang + --installpath)"
        Args = '--lang=frFR', "--installpath=`"$InstallPath`""
    },
    @{
        Name = "With --silent flag"
        Args = '--silent', '--lang=frFR', "--installpath=`"$InstallPath`""
    },
    @{
        Name = "NSIS style (/S)"
        Args = '/S', '--lang=frFR', "--installpath=`"$InstallPath`""
    }
)

$results = @()

foreach ($config in $testConfigs) {
    Write-Host "`n[2/4] Testing: $($config.Name)" -ForegroundColor Yellow
    Write-Host "      Args: $($config.Args -join ' ')" -ForegroundColor Gray

    $startTime = Get-Date
    $process = Start-Process -FilePath $InstallerPath -ArgumentList $config.Args -Wait -PassThru -WindowStyle Hidden
    $duration = ((Get-Date) - $startTime).TotalSeconds

    $installed = Test-Path $ExePath

    $result = [PSCustomObject]@{
        TestName = $config.Name
        ExitCode = $process.ExitCode
        Duration = [math]::Round($duration, 1)
        Installed = $installed
        Success = ($process.ExitCode -eq 0 -and $installed)
    }

    $results += $result

    # Display result
    $statusColor = if ($result.Success) { 'Green' } else { 'Red' }
    $statusSymbol = if ($result.Success) { '✓' } else { '✗' }

    Write-Host "      $statusSymbol Exit Code: $($result.ExitCode)" -ForegroundColor $statusColor
    Write-Host "      Duration: $($result.Duration)s"
    Write-Host "      Installed: $($result.Installed)" -ForegroundColor $(if ($installed) { 'Green' } else { 'Red' })

    if ($result.Success) {
        Write-Host "`n      SUCCESS! This configuration works silently." -ForegroundColor Green
        break  # Stop testing, we found a working config
    }

    Start-Sleep -Seconds 2
}

Write-Host "`n[3/4] Test Results Summary" -ForegroundColor Cyan
Write-Host "=" * 70

$results | Format-Table -AutoSize

$successfulConfig = $results | Where-Object { $_.Success } | Select-Object -First 1

if ($successfulConfig) {
    Write-Host "`n[4/4] Recommendation for Win11Forge" -ForegroundColor Green
    Write-Host "=" * 70

    $recommendedArgs = ($testConfigs | Where-Object { $_.Name -eq $successfulConfig.TestName }).Args -join ' '

    Write-Host "`nUse this in Gaming.json:" -ForegroundColor Yellow
    Write-Host @"
{
  "InstallArguments": "$recommendedArgs"
}
"@ -ForegroundColor White

    Write-Host "`nCopy/paste ready:" -ForegroundColor Yellow
    Write-Host "`"$recommendedArgs`"" -ForegroundColor Cyan

} else {
    Write-Host "`n[4/4] No fully silent configuration found" -ForegroundColor Red
    Write-Host "=" * 70
    Write-Host @"

The installer may require:
1. Additional undocumented switches
2. Registry pre-configuration
3. Manual interaction

Current best option:
- Use Microsoft Store version (if available)
- Or accept manual prompts during DirectUrl installation
"@ -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n[Cleanup] Removing temp files..." -ForegroundColor Gray
Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan

if (Test-Path $ExePath) {
    Write-Host "`nBattle.net installed at: $ExePath" -ForegroundColor Green

    $launch = Read-Host "`nLaunch Battle.net now? (Y/N)"
    if ($launch -eq 'Y') {
        Start-Process $ExePath
    }
}
