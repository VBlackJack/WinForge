<#
.SYNOPSIS
  Win11Forge - Run All Checks (PowerShell version)

.DESCRIPTION
  Runs, in order:
    1) Tools/Verify-VersionConsistency.ps1
    2) Tools/Invoke-PSScriptAnalyzer.ps1
    3) Tools/Validate-Framework.ps1 -Detailed
    4) Tools/Validate-AppDatabase.ps1
    5) Tests/Invoke-Tests.ps1 -Coverage

.NOTES
  Use this when running from PowerShell: .\Run-All-Checks.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Banner {
  param([string]$Title)
  Write-Host ''
  Write-Host '============================================================================' -ForegroundColor Cyan
  Write-Host ('  {0}' -f $Title) -ForegroundColor Cyan
  Write-Host '============================================================================' -ForegroundColor Cyan
  Write-Host ''
}

$root = $PSScriptRoot
Write-Banner 'Win11Forge - Run All Checks'

$fail = $false

function Run-Step {
  param(
    [string]$Name,
    [scriptblock]$Action
  )
  Write-Host "[STEP] $Name" -ForegroundColor Yellow
  try {
    & $Action
    Write-Host "[OK]   $Name" -ForegroundColor Green
  } catch {
    Write-Host "[FAIL] $Name" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor DarkRed
    $global:fail = $true
  }
  Write-Host ''
}

# 1) Version consistency
$verScript = Join-Path $root 'Tools/Verify-VersionConsistency.ps1'
if (Test-Path $verScript) {
  Run-Step 'Version consistency check' { & $verScript; if ($LASTEXITCODE -ne 0) { throw 'Version mismatch' } }
} else { Write-Host "[SKIP] $verScript not found" -ForegroundColor DarkYellow }

# 2) PSScriptAnalyzer
$analyze = Join-Path $root 'Tools/Invoke-PSScriptAnalyzer.ps1'
if (Test-Path $analyze) {
  Run-Step 'Static analysis (PSScriptAnalyzer)' { & $analyze }
} else { Write-Host "[SKIP] $analyze not found" -ForegroundColor DarkYellow }

# 3) Framework validation
$validate = Join-Path $root 'Tools/Validate-Framework.ps1'
if (Test-Path $validate) {
  Run-Step 'Framework validation' { & $validate -Detailed; if ($LASTEXITCODE -ne 0) { throw 'Validation failed' } }
} else { Write-Host "[SKIP] $validate not found" -ForegroundColor DarkYellow }

# 4) App DB validation
$dbval = Join-Path $root 'Tools/Validate-AppDatabase.ps1'
if (Test-Path $dbval) {
  Run-Step 'Application database validation' { & $dbval; if ($LASTEXITCODE -ne 0) { throw 'App database validation failed' } }
} else { Write-Host "[SKIP] $dbval not found" -ForegroundColor DarkYellow }

# 5) Tests (Pester)
$tests = Join-Path $root 'Tests/Invoke-Tests.ps1'
if (Test-Path $tests) {
  Run-Step 'Pester tests' { & $tests; if ($LASTEXITCODE -ne 0) { throw 'Tests failed' } }
} else { Write-Host "[SKIP] $tests not found" -ForegroundColor DarkYellow }

if ($fail) {
  Write-Host '============================================================================' -ForegroundColor Cyan
  Write-Host 'Done with failures. See steps above.' -ForegroundColor Red
  exit 1
} else {
  Write-Host '============================================================================' -ForegroundColor Cyan
  Write-Host 'All checks completed successfully.' -ForegroundColor Green
  exit 0
}

