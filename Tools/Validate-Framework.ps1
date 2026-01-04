<#
.SYNOPSIS
    Win11Forge Framework Validation Script

.DESCRIPTION
    Validates the Win11Forge framework installation:
    - Checks directory structure
    - Verifies all required files
    - Tests module loading
    - Validates profile JSON files
    - Tests basic functionality

.EXAMPLE
    .\Validate-Framework.ps1

.EXAMPLE
    .\Validate-Framework.ps1 -Detailed

.NOTES
    Author: Julien Bombled
    Version: 2.1.0
    Run this after initial framework setup
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

# === INITIALIZATION ===

# Navigate to framework root (parent directory of Tools)
$script:ScriptRoot = Split-Path -Parent $PSScriptRoot
$script:ValidationResults = @{
    Passed = 0
    Failed = 0
    Warnings = 0
    Errors = @()
}

# === HELPER FUNCTIONS ===

function Write-ValidationResult {
    param(
        [string]$Test,
        [bool]$Passed,
        [string]$Message = ''
    )

    if ($Passed) {
        Write-Host "  " -NoNewline
        Write-Host "[OK]" -ForegroundColor Green -NoNewline
        Write-Host " $Test" -ForegroundColor White
        $script:ValidationResults.Passed++
    } else {
        Write-Host "  " -NoNewline
        Write-Host "[FAIL]" -ForegroundColor Red -NoNewline
        Write-Host " $Test" -ForegroundColor White
        if ($Message) {
            Write-Host "    -> $Message" -ForegroundColor Yellow
            $script:ValidationResults.Errors += "$Test : $Message"
        }
        $script:ValidationResults.Failed++
    }
}

function Write-ValidationWarning {
    param([string]$Message)
    Write-Host "  " -NoNewline
    Write-Host "[WARN]" -ForegroundColor Yellow -NoNewline
    Write-Host " $Message" -ForegroundColor White
    $script:ValidationResults.Warnings++
}

function Write-ValidationSection {
    param([string]$Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

# === VALIDATION TESTS ===

function Test-DirectoryStructure {
    Write-ValidationSection "Directory Structure"

    $requiredDirs = @(
        'Core',
        'Modules',
        'Profiles',
        'Logs',
        'Config',
        'Tools'
    )

    foreach ($dir in $requiredDirs) {
        $path = Join-Path -Path $script:ScriptRoot -ChildPath $dir
        $exists = Test-Path -Path $path -PathType Container
        Write-ValidationResult -Test "Directory: $dir" -Passed $exists -Message "Path: $path"
    }
}

function Test-RequiredFiles {
    Write-ValidationSection "Required Files"

    $requiredFiles = @{
        'Deploy-Win11Forge.bat' = 'Main launcher script'
        'Deploy-Win11Environment.ps1' = 'Main deployment script'
        'README.md' = 'Documentation'
        'Core\Core.psm1' = 'Core module'
        'Modules\Prerequisites.psm1' = 'Prerequisites module'
        'Modules\EnvironmentDetection.psm1' = 'Environment detection module'
        'Modules\ProfileManager.psm1' = 'Profile manager module'
        'Modules\InstallationEngine.psm1' = 'Installation engine module'
        'Modules\SystemConfig.psm1' = 'System configuration module'
    }

    foreach ($file in $requiredFiles.Keys) {
        $path = Join-Path -Path $script:ScriptRoot -ChildPath $file
        $exists = Test-Path -Path $path -PathType Leaf
        Write-ValidationResult -Test "$file" -Passed $exists -Message $requiredFiles[$file]

        if ($exists -and $Detailed) {
            $size = (Get-Item -Path $path).Length
            $lines = (Get-Content -Path $path).Count
            Write-Host "      Size: $size bytes, Lines: $lines" -ForegroundColor Gray
        }
    }
}

function Test-VersionConsistency {
    Write-ValidationSection "Version Consistency"

    $scriptPath = Join-Path -Path $script:ScriptRoot -ChildPath 'Tools\\Verify-VersionConsistency.ps1'
    if (-not (Test-Path $scriptPath)) {
        Write-ValidationResult -Test 'Verify-VersionConsistency.ps1 present' -Passed $false -Message 'Missing tools script'
        return
    }

    try {
        $p = Start-Process pwsh -ArgumentList @('-NoProfile','-File', $scriptPath) -PassThru -WindowStyle Hidden -Wait
        if ($p.ExitCode -eq 0) {
            Write-ValidationResult -Test 'All version strings match Config/version.json' -Passed $true
        } else {
            Write-ValidationResult -Test 'Version strings mismatch' -Passed $false -Message 'Run Tools/Verify-VersionConsistency.ps1 for details'
        }
    } catch {
        Write-ValidationResult -Test 'Version check execution' -Passed $false -Message $_.Exception.Message
    }
}

function Test-ProfileFiles {
    Write-ValidationSection "Profile Files"

    $profiles = @(
        'Base.json',
        'Office.json',
        'Gaming.json',
        'Personnel.json'
    )

    foreach ($profileName in $profiles) {
        $path = Join-Path -Path $script:ScriptRoot -ChildPath "Profiles\$profileName"
        $exists = Test-Path -Path $path -PathType Leaf

        if ($exists) {
            try {
                $content = Get-Content -Path $path -Raw | ConvertFrom-Json
                $valid = $content.Name -and $content.Applications
                Write-ValidationResult -Test "Profile: $profileName" -Passed $valid -Message "Valid JSON"

                if ($valid -and $Detailed) {
                    Write-Host "      Name: $($content.Name), Version: $($content.Version)" -ForegroundColor Gray
                    Write-Host "      Applications: $($content.Applications.Count)" -ForegroundColor Gray
                    if ($content.Inherits) {
                        Write-Host "      Inherits: $($content.Inherits -join ', ')" -ForegroundColor Gray
                    }
                }
            } catch {
                Write-ValidationResult -Test "Profile: $profileName" -Passed $false -Message "Invalid JSON: $($_.Exception.Message)"
            }
        } else {
            Write-ValidationResult -Test "Profile: $profileName" -Passed $false -Message "File not found"
        }
    }
}

function Test-ModuleLoading {
    Write-ValidationSection "Module Loading"

    $modules = @(
        'Core\Core.psm1',
        'Modules\Prerequisites.psm1',
        'Modules\EnvironmentDetection.psm1',
        'Modules\ProfileManager.psm1',
        'Modules\InstallationEngine.psm1',
        'Modules\SystemConfig.psm1'
    )

    foreach ($module in $modules) {
        $path = Join-Path -Path $script:ScriptRoot -ChildPath $module
        $moduleName = Split-Path -Path $module -Leaf

        if (Test-Path -Path $path) {
            try {
                Import-Module -Name $path -Force -ErrorAction Stop
                Write-ValidationResult -Test "Load: $moduleName" -Passed $true

                if ($Detailed) {
                    $loadedModule = Get-Module -Name $moduleName.Replace('.psm1', '')
                    if ($loadedModule) {
                        $exportedCount = $loadedModule.ExportedFunctions.Count
                        Write-Host "      Exported functions: $exportedCount" -ForegroundColor Gray
                    }
                }
            } catch {
                Write-ValidationResult -Test "Load: $moduleName" -Passed $false -Message $_.Exception.Message
            }
        } else {
            Write-ValidationResult -Test "Load: $moduleName" -Passed $false -Message "Module file not found"
        }
    }
}

function Test-CoreFunctions {
    Write-ValidationSection "Core Functions"

    $coreFunctions = @(
        'Write-Status',
        'Write-Section',
        'Test-Administrator',
        'Test-InternetConnection',
        'Invoke-SafeCommand'
    )

    foreach ($function in $coreFunctions) {
        $exists = Get-Command -Name $function -ErrorAction SilentlyContinue
        Write-ValidationResult -Test "Function: $function" -Passed ($null -ne $exists)
    }
}

function Test-EnvironmentDetection {
    Write-ValidationSection "Environment Detection"

    try {
        $envType = Get-SystemEnvironmentType
        Write-ValidationResult -Test "Detect environment type" -Passed $true
        Write-Host "      Detected: $envType" -ForegroundColor Gray

        $envReport = Get-EnvironmentReport
        Write-ValidationResult -Test "Generate environment report" -Passed ($null -ne $envReport)

        if ($Detailed -and $envReport) {
            Write-Host "      Computer: $($envReport.ComputerName)" -ForegroundColor Gray
            Write-Host "      OS: $($envReport.OSVersion)" -ForegroundColor Gray
            Write-Host "      Memory: $($envReport.TotalMemoryGB) GB" -ForegroundColor Gray
        }
    } catch {
        Write-ValidationResult -Test "Environment detection" -Passed $false -Message $_.Exception.Message
    }
}

function Test-ProfileLoading {
    Write-ValidationSection "Profile Loading"

    try {
        $profilesDir = Join-Path -Path $script:ScriptRoot -ChildPath 'Profiles'

        if (Test-Path "$profilesDir\Base.json") {
            $deploymentProfile = Get-DeploymentProfile -ProfileName "Base" -ProfilesDirectory $profilesDir
            Write-ValidationResult -Test "Load Base profile" -Passed ($null -ne $deploymentProfile)

            if ($deploymentProfile -and $Detailed) {
                Write-Host "      Applications: $($deploymentProfile.Applications.Count)" -ForegroundColor Gray
                Write-Host "      Config sections: $($deploymentProfile.SystemConfig.Keys.Count)" -ForegroundColor Gray
            }
        } else {
            Write-ValidationResult -Test "Load Base profile" -Passed $false -Message "Base.json not found"
        }

        # Test inheritance
        if (Test-Path "$profilesDir\Gaming.json") {
            $gamingProfile = Get-DeploymentProfile -ProfileName "Gaming" -ProfilesDirectory $profilesDir
            if ($gamingProfile) {
                $hasInheritance = $gamingProfile.InheritanceChain.Count -gt 1
                Write-ValidationResult -Test "Profile inheritance" -Passed $hasInheritance

                if ($hasInheritance -and $Detailed) {
                    Write-Host "      Chain: $($gamingProfile.InheritanceChain -join ' -> ')" -ForegroundColor Gray
                }
            }
        }
    } catch {
        Write-ValidationResult -Test "Profile loading" -Passed $false -Message $_.Exception.Message
    }
}

function Test-PrerequisitesValidation {
    Write-ValidationSection "Prerequisites Check"

    try {
        # Import Prerequisites module first
        $prereqModule = Join-Path -Path $script:ScriptRoot -ChildPath 'Modules\Prerequisites.psm1'
        if (Test-Path -Path $prereqModule) {
            Import-Module -Name $prereqModule -Force -ErrorAction SilentlyContinue
        }

        # Call the module function
        if (-not (Get-Command -Name 'Test-Prerequisites' -ErrorAction SilentlyContinue)) {
            Write-ValidationResult -Test "Prerequisites module" -Passed $false -Message "Test-Prerequisites function not found"
            return
        }

        $prereqs = Test-Prerequisites

        # Validate that we got a dictionary back (hashtable or OrderedDictionary)
        $isValidDictionary = ($prereqs -is [hashtable]) -or
                            ($prereqs -is [System.Collections.Specialized.OrderedDictionary]) -or
                            ($null -ne $prereqs -and $prereqs.GetType().Name -eq 'OrderedDictionary')

        if (-not $isValidDictionary) {
            $actualType = if ($null -eq $prereqs) { 'null' } else { $prereqs.GetType().FullName }
            Write-ValidationResult -Test "Prerequisites check" -Passed $false -Message "Invalid prerequisites data type: $actualType"
            return
        }

        if ($prereqs.Count -eq 0) {
            Write-ValidationResult -Test "Prerequisites check" -Passed $false -Message "No prerequisites data returned"
            return
        }

        foreach ($key in $prereqs.Keys) {
            # Ensure we have valid data
            if ($null -eq $prereqs[$key]) {
                Write-ValidationResult -Test "$key installed" -Passed $false -Message "No data available"
                continue
            }

            # Check if it's a dictionary-like object with properties
            $itemHasInstalled = $false
            $installedValue = $null

            # Try to get Installed property (works for hashtables and PSCustomObjects)
            try {
                if ($prereqs[$key] -is [hashtable] -or $prereqs[$key] -is [System.Collections.Specialized.OrderedDictionary]) {
                    $itemHasInstalled = $prereqs[$key].ContainsKey('Installed')
                    if ($itemHasInstalled) {
                        $installedValue = $prereqs[$key].Installed
                    }
                } elseif ($prereqs[$key].PSObject.Properties.Name -contains 'Installed') {
                    $itemHasInstalled = $true
                    $installedValue = $prereqs[$key].Installed
                }
            } catch {
                Write-ValidationResult -Test "$key installed" -Passed $false -Message "Unable to read Installed property"
                continue
            }

            if (-not $itemHasInstalled) {
                Write-ValidationResult -Test "$key installed" -Passed $false -Message "Installed status not found"
                continue
            }

            # Get installed status and convert to boolean
            $installed = $false
            if ($installedValue -is [bool]) {
                $installed = $installedValue
            } elseif ($installedValue -eq $true -or $installedValue -eq 'True' -or $installedValue -eq 1) {
                $installed = $true
            }

            Write-ValidationResult -Test "$key installed" -Passed $installed

            # Show version if available and in detailed mode
            if ($Detailed) {
                $versionValue = $null
                try {
                    if ($prereqs[$key] -is [hashtable] -or $prereqs[$key] -is [System.Collections.Specialized.OrderedDictionary]) {
                        if ($prereqs[$key].ContainsKey('Version')) {
                            $versionValue = $prereqs[$key].Version
                        }
                    } elseif ($prereqs[$key].PSObject.Properties.Name -contains 'Version') {
                        $versionValue = $prereqs[$key].Version
                    }
                }
                catch {
                    # Version read errors are expected for some prereq types
                    $null = $_
                }

                if ($versionValue) {
                    Write-Host "      Version: $versionValue" -ForegroundColor Gray
                }
            }
        }

        # Check PowerShell version mismatch
        if ($prereqs.Keys -contains 'PowerShell7') {
            $ps7Installed = $false
            try {
                if ($prereqs.PowerShell7 -is [hashtable] -or $prereqs.PowerShell7 -is [System.Collections.Specialized.OrderedDictionary]) {
                    if ($prereqs.PowerShell7.ContainsKey('Installed')) {
                        $installedValue = $prereqs.PowerShell7.Installed
                        if ($installedValue -is [bool]) {
                            $ps7Installed = $installedValue
                        } elseif ($installedValue -eq $true -or $installedValue -eq 'True') {
                            $ps7Installed = $true
                        }
                    }
                } elseif ($prereqs.PowerShell7.PSObject.Properties.Name -contains 'Installed') {
                    $ps7Installed = $prereqs.PowerShell7.Installed -eq $true
                }
            }
            catch {
                # PowerShell 7 check may fail on some configurations
                $null = $_
            }

            if ($ps7Installed -and $PSVersionTable.PSVersion.Major -lt 7) {
                Write-ValidationWarning "PowerShell 7 installed but current session is PowerShell $($PSVersionTable.PSVersion)"
            }
        }
    } catch {
        Write-ValidationResult -Test "Prerequisites check" -Passed $false -Message $_.Exception.Message
    }
}

function Test-SystemPermissions {
    Write-ValidationSection "System Permissions"

    $isAdmin = Test-Administrator
    Write-ValidationResult -Test "Administrator privileges" -Passed $isAdmin

    if (-not $isAdmin) {
        Write-ValidationWarning "Some features require administrator privileges"
    }

    try {
        $testPath = Join-Path -Path $env:ProgramFiles -ChildPath "Test_Write_$(Get-Random).tmp"
        $null = New-Item -Path $testPath -ItemType File -Force
        Remove-Item -Path $testPath -Force
        Write-ValidationResult -Test "Write to Program Files" -Passed $true
    } catch {
        Write-ValidationResult -Test "Write to Program Files" -Passed $false -Message "Insufficient permissions"
    }
}

function Test-NetworkConnectivity {
    Write-ValidationSection "Network Connectivity"

    $internet = Test-InternetConnection
    Write-ValidationResult -Test "Internet connectivity" -Passed $internet

    if (-not $internet) {
        Write-ValidationWarning "Internet connection required for package installation"
    }

    # Test package manager connectivity
    if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
        try {
            $null = & winget search "test" --accept-source-agreements 2>&1
            Write-ValidationResult -Test "Winget connectivity" -Passed ($LASTEXITCODE -eq 0)
        } catch {
            Write-ValidationResult -Test "Winget connectivity" -Passed $false
        }
    } else {
        Write-ValidationWarning "Winget not installed"
    }
}

# === MAIN VALIDATION ===

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "     Win11Forge Framework Validation v2.0.2                   " -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Framework Path: $script:ScriptRoot" -ForegroundColor Gray
Write-Host "Validation Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

# Run all tests
Test-DirectoryStructure
Test-RequiredFiles
Test-ProfileFiles
Test-ModuleLoading
Test-CoreFunctions
Test-EnvironmentDetection
Test-ProfileLoading
Test-PrerequisitesValidation
Test-SystemPermissions
Test-NetworkConnectivity

# === SUMMARY ===

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "                    VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

$total = $script:ValidationResults.Passed + $script:ValidationResults.Failed
$passRate = if ($total -gt 0) { [math]::Round(($script:ValidationResults.Passed / $total) * 100, 1) } else { 0 }

Write-Host "  Total Tests:  " -NoNewline
Write-Host "$total" -ForegroundColor White
Write-Host "  Passed:       " -NoNewline
Write-Host "$($script:ValidationResults.Passed)" -ForegroundColor Green
Write-Host "  Failed:       " -NoNewline
Write-Host "$($script:ValidationResults.Failed)" -ForegroundColor Red
Write-Host "  Warnings:     " -NoNewline
Write-Host "$($script:ValidationResults.Warnings)" -ForegroundColor Yellow
Write-Host "  Success Rate: " -NoNewline
Write-Host "$passRate%" -ForegroundColor $(if ($passRate -ge 90) { 'Green' } elseif ($passRate -ge 70) { 'Yellow' } else { 'Red' })

if ($script:ValidationResults.Failed -gt 0) {
    Write-Host ""
    Write-Host "Errors Found:" -ForegroundColor Red
    foreach ($errMsg in $script:ValidationResults.Errors) {
        Write-Host "  - $errMsg" -ForegroundColor Yellow
    }
}

Write-Host ""

if ($script:ValidationResults.Failed -eq 0) {
    Write-Host "[SUCCESS] Framework validation PASSED - Ready for deployment!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[FAILED] Framework validation FAILED - Please fix errors above" -ForegroundColor Red
    exit 1
}
