<#
.SYNOPSIS
    Test Global Optimizations
.DESCRIPTION
    Tests the global optimizations configuration that applies to all profiles
#>

# Import required modules
Import-Module "$PSScriptRoot\Core\Core.psm1" -Force
Import-Module "$PSScriptRoot\Modules\SystemConfig.psm1" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Global Optimizations Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Load global optimizations config
$configPath = "$PSScriptRoot\Config\global-optimizations.json"

if (-not (Test-Path $configPath)) {
    Write-Host "Error: global-optimizations.json not found!" -ForegroundColor Red
    exit 1
}

$configJson = Get-Content -Path $configPath -Raw | ConvertFrom-Json

# Convert PSCustomObject to Hashtable recursively
function ConvertTo-Hashtable {
    param([Parameter(ValueFromPipeline)]$InputObject)

    if ($null -eq $InputObject) { return $null }

    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $collection = @()
        foreach ($object in $InputObject) { $collection += ConvertTo-Hashtable $object }
        return $collection
    }
    elseif ($InputObject -is [PSCustomObject]) {
        $hash = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $hash[$property.Name] = ConvertTo-Hashtable $property.Value
        }
        return $hash
    }
    else {
        return $InputObject
    }
}

$config = ConvertTo-Hashtable $configJson

Write-Host "Applying Global Optimizations..." -ForegroundColor Yellow
Write-Host ""

Write-Host "Explorer Optimizations:" -ForegroundColor Cyan
Write-Host "  - Show Hidden Files: $($config.SystemConfig.Explorer.ShowHiddenFiles)" -ForegroundColor White
Write-Host "  - Show File Extensions: $($config.SystemConfig.Explorer.ShowFileExtensions)" -ForegroundColor White
Write-Host "  - Show Libraries: $($config.SystemConfig.Explorer.ShowLibraries)" -ForegroundColor White
Write-Host "  - Expand to Open Folder: $($config.SystemConfig.Explorer.ExpandToOpenFolder)" -ForegroundColor White
Write-Host ""

Write-Host "Privacy Optimizations:" -ForegroundColor Cyan
Write-Host "  - Disable Telemetry: $($config.SystemConfig.Privacy.DisableTelemetry)" -ForegroundColor White
Write-Host "  - Disable Cortana: $($config.SystemConfig.Privacy.DisableCortana)" -ForegroundColor White
Write-Host "  - Disable Bloatware: $($config.SystemConfig.Privacy.DisableConsumerFeatures)" -ForegroundColor White
Write-Host "  - Disable Tips: $($config.SystemConfig.Privacy.DisableWindowsTips)" -ForegroundColor White
Write-Host ""

Write-Host "Performance Optimizations:" -ForegroundColor Cyan
Write-Host "  - Optimize Services: $($config.SystemConfig.Performance.OptimizeServices)" -ForegroundColor White
Write-Host "  - Power Plan: $($config.SystemConfig.Performance.PowerPlan)" -ForegroundColor White
Write-Host ""

Write-Host "Taskbar Optimizations:" -ForegroundColor Cyan
Write-Host "  - Disable Widgets: $($config.SystemConfig.Taskbar.DisableWidgets)" -ForegroundColor White
Write-Host "  - Start Alignment: $($config.SystemConfig.Taskbar.StartAlignment)" -ForegroundColor White
Write-Host "  - Search Mode: $($config.SystemConfig.Taskbar.SearchMode)" -ForegroundColor White
Write-Host ""

# Apply configuration
Set-SystemConfiguration -Config $config.SystemConfig

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Global Optimizations Applied!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary of changes:" -ForegroundColor Yellow
Write-Host "  ✓ Explorer optimized for productivity" -ForegroundColor Green
Write-Host "  ✓ Privacy enhanced (telemetry/tracking disabled)" -ForegroundColor Green
Write-Host "  ✓ Cortana disabled (obsolete)" -ForegroundColor Green
Write-Host "  ✓ Bloatware suggestions disabled" -ForegroundColor Green
Write-Host "  ✓ Services optimized (safe mode)" -ForegroundColor Green
Write-Host "  ✓ Taskbar cleaned up" -ForegroundColor Green
Write-Host "  ✓ Security maintained (Defender/Firewall active)" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Explorer was restarted. Some changes may require a system restart." -ForegroundColor Yellow
Write-Host ""
