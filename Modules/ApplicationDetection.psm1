<#
.SYNOPSIS
    Win11Forge - Application Detection v3.7.2

.DESCRIPTION
    Application detection and installation verification functions:
    - Test-ApplicationInstalled: Main detection function with multiple methods
    - Test-ApplicationInstalledFast: Cache-based fast detection
    - Get-InstalledApplicationsCache: Build detection cache
    - Get-ApplicationsInstallationStatus: Batch detection

.NOTES
    Author: Julien Bombled
    v3.7.2

    Changelog v3.1.4:
    - Extracted from InstallationEngine.psm1 for modularity
    - Shared detection logic for sequential and parallel installations
#>

#
# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
$script:EnvironmentDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module for i18n support
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# Import EnvironmentDetection for sandbox detection
if (-not (Get-Command -Name Test-IsWindowsSandbox -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:EnvironmentDetectionPath) {
        Import-Module -Name $script:EnvironmentDetectionPath -Force
    }
}

# Import DirectoryConstants for centralized registry paths
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-RegistryPath -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# Import WingetCache for optimized winget list caching
$script:WingetCachePath = Join-Path $script:ModuleRoot 'WingetCache.psm1'
if (-not (Get-Command -Name Get-CachedWingetList -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:WingetCachePath) {
        Import-Module -Name $script:WingetCachePath -Force
    }
}

# === CONFIGURATION ===

# Import DetectionAllowlist: the shared command-detection allowlist (single source).
$script:DetectionAllowlistModulePath = Join-Path $script:ModuleRoot 'DetectionAllowlist.psm1'
if (-not (Get-Command -Name Get-DetectionAllowlist -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DetectionAllowlistModulePath) {
        Import-Module -Name $script:DetectionAllowlistModulePath -Force
    }
}

# Import DetectionArgumentGuard: the shared command-detection argument sanitizer (single source).
$script:DetectionArgumentGuardPath = Join-Path $script:ModuleRoot 'DetectionArgumentGuard.psm1'
if (-not (Get-Command -Name Test-DetectionArgumentDangerous -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DetectionArgumentGuardPath) {
        Import-Module -Name $script:DetectionArgumentGuardPath -Force
    }
}

# === REGISTRY-FIRST OPTIMIZATION ===

# Script-level cache for registry apps (populated once per session)
$script:RegistryAppsCache = $null
$script:RegistryAppsCacheTime = $null
$script:RegistryAppsCacheMaxAgeMinutes = 5

function Get-RegistryInstalledApp {
    <#
    .SYNOPSIS
        Fast registry-based application detection.
    .DESCRIPTION
        Scans Windows uninstall registry keys to find an application by DisplayName.
        This is much faster than calling winget or choco (~20ms vs ~2s).
    .PARAMETER AppName
        The application name to search for (partial match supported).
    .PARAMETER ExactMatch
        If true, requires exact DisplayName match instead of partial.
    .OUTPUTS
        PSCustomObject with DisplayName, Version, Publisher, InstallLocation or $null if not found.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$AppName,

        [Parameter()]
        [switch]$ExactMatch
    )

    # Refresh cache if expired or not populated
    $cacheExpired = $script:RegistryAppsCacheTime -and `
        ((Get-Date) - $script:RegistryAppsCacheTime).TotalMinutes -gt $script:RegistryAppsCacheMaxAgeMinutes

    if (-not $script:RegistryAppsCache -or $cacheExpired) {
        $script:RegistryAppsCache = @{}
        $script:RegistryAppsCacheTime = Get-Date

        $uninstallPaths = @(
            "$(Get-RegistryPath -PathKey 'UninstallX64')\*",
            "$(Get-RegistryPath -PathKey 'UninstallX86')\*",
            "$(Get-RegistryPath -PathKey 'UninstallCurrentUser')\*"
        )

        # Use parallel execution for PS7+ (each registry hive is independent)
        $isPSCore = $PSVersionTable.PSVersion.Major -ge 7

        if ($isPSCore) {
            # Parallel registry scan for PS7+ (~30% faster)
            $allEntries = $uninstallPaths | ForEach-Object -ThrottleLimit 3 -Parallel {
                $path = $_
                $results = @()
                try {
                    $entries = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
                    foreach ($entry in $entries) {
                        if ($entry.DisplayName) {
                            $results += [PSCustomObject]@{
                                DisplayName     = $entry.DisplayName
                                Version         = $entry.DisplayVersion
                                Publisher       = $entry.Publisher
                                InstallLocation = $entry.InstallLocation
                            }
                        }
                    }
                } catch {
                    Write-Verbose "Registry scan error for $path`: $($_.Exception.Message)"
                }
                $results
            }

            foreach ($entry in $allEntries) {
                if ($entry.DisplayName) {
                    $key = $entry.DisplayName.ToLowerInvariant()
                    if (-not $script:RegistryAppsCache.ContainsKey($key)) {
                        $script:RegistryAppsCache[$key] = $entry
                    }
                }
            }
        } else {
            # Sequential for PS5.1 compatibility
            foreach ($path in $uninstallPaths) {
                try {
                    $entries = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
                    foreach ($entry in $entries) {
                        if ($entry.DisplayName) {
                            $key = $entry.DisplayName.ToLowerInvariant()
                            if (-not $script:RegistryAppsCache.ContainsKey($key)) {
                                $script:RegistryAppsCache[$key] = [PSCustomObject]@{
                                    DisplayName     = $entry.DisplayName
                                    Version         = $entry.DisplayVersion
                                    Publisher       = $entry.Publisher
                                    InstallLocation = $entry.InstallLocation
                                }
                            }
                        }
                    }
                } catch {
                    # Permission denied or path not accessible - silently continue
                    Write-Verbose "Skipping inaccessible registry path '$path': $($_.Exception.Message)"
                }
            }
        }

        if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
            Write-Verbose (Get-LocalizedString -Key 'optimization.registry_cache_built' -Parameters @{ Count = $script:RegistryAppsCache.Count })
        }
    }

    # Search in cache
    $nameLower = $AppName.ToLowerInvariant()

    if ($ExactMatch) {
        if ($script:RegistryAppsCache.ContainsKey($nameLower)) {
            return $script:RegistryAppsCache[$nameLower]
        }
    } else {
        # Partial match - check if any key contains the search term
        foreach ($key in $script:RegistryAppsCache.Keys) {
            if ($key -like "*$nameLower*" -or $nameLower -like "*$key*") {
                return $script:RegistryAppsCache[$key]
            }
        }
    }

    return $null
}

function Clear-RegistryAppsCache {
    <#
    .SYNOPSIS
        Clears the registry applications cache to force a refresh.
    .DESCRIPTION
        Invalidates the in-memory registry applications cache and its associated
        timestamp, forcing the next registry-based detection query to perform a
        fresh scan of the installed application registry keys.
    #>
    [CmdletBinding()]
    param()

    $script:RegistryAppsCache = $null
    $script:RegistryAppsCacheTime = $null

    if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
        Write-Verbose (Get-LocalizedString -Key 'optimization.registry_cache_cleared')
    }
}

# === HELPER FUNCTIONS ===

# Security: Whitelist of allowed registry path patterns
# Only registry paths matching these patterns are allowed for detection
$script:AllowedRegistryPatterns = @(
    '^HK(LM|CU):\\SOFTWARE(\\|$)',                        # Standard software keys (with or without trailing path)
    '^HK(LM|CU):\\SOFTWARE\\WOW6432Node(\\|$)',           # 32-bit software keys
    '^HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)',  # Uninstall keys
    '^HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)',
    '^HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)'
)

# Security: Blocked registry paths (sensitive hives)
$script:BlockedRegistryPatterns = @(
    '\\SAM\\',           # Security Account Manager
    '\\SECURITY\\',      # Security settings
    '\\SYSTEM\\',        # System configuration (except specific subkeys)
    '\\\.DEFAULT\\',     # Default user profile
    'RunOnce',           # Startup entries (potential persistence)
    'Run$'               # Startup entries
)

function Test-RegistryPathAllowed {
    <#
    .SYNOPSIS
        Validates that a registry path is allowed for detection.
    .DESCRIPTION
        Checks registry path against whitelist and blocklist patterns.
        Returns $false for sensitive or disallowed paths.
    .PARAMETER Path
        The registry path to validate.
    .OUTPUTS
        Boolean indicating if the path is allowed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    # Normalize path separators
    $normalizedPath = $Path -replace '/', '\'

    # Check against blocked patterns first
    foreach ($blocked in $script:BlockedRegistryPatterns) {
        if ($normalizedPath -match $blocked) {
            Write-Verbose "Registry path blocked (sensitive): $Path"
            return $false
        }
    }

    # Check path length (prevent DoS via deep paths)
    if ($normalizedPath.Length -gt 512) {
        Write-Verbose "Registry path too long: $($normalizedPath.Length) chars"
        return $false
    }

    # Check against allowed patterns
    foreach ($allowed in $script:AllowedRegistryPatterns) {
        if ($normalizedPath -match $allowed) {
            return $true
        }
    }

    Write-Verbose "Registry path not in whitelist: $Path"
    return $false
}

function Test-RegistryKey {
    <#
    .SYNOPSIS
        Tests if a registry key exists with security validation.
    .DESCRIPTION
        Validates the registry path against security rules before testing existence.
    .PARAMETER Path
        The registry path to test.
    .OUTPUTS
        Boolean indicating if the key exists and is allowed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    # Security: Validate path is allowed
    if (-not (Test-RegistryPathAllowed -Path $Path)) {
        Write-Status -Message (Get-LocalizedString -Key 'detect.security.registry_path_blocked' -Parameters @{ Path = $Path }) -Level 'Warning'
        return $false
    }

    return Test-Path -Path $Path -ErrorAction SilentlyContinue
}

function Expand-DetectionPath {
    <#
    .SYNOPSIS
        Expands environment variables in detection paths with security validation.
    .DESCRIPTION
        Supports both %VAR% and $env:VAR syntax for environment variable expansion.
        Also handles common path aliases like %PROGRAMFILES%, %LOCALAPPDATA%, etc.
        Validates against path traversal attacks.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    # Security: Block path traversal attempts in input
    if ($Path -match '\.\.' -or $Path -match '[\\/]\.\.[\\/]?' -or $Path -match '^\.\.') {
        Write-Status -Message (Get-LocalizedString -Key 'detect.security.path_traversal_blocked' -Parameters @{ Path = $Path }) -Level 'Warning'
        return $null
    }

    # Expand %VAR% style environment variables only
    # Security: Do NOT use ExpandString() as it allows code injection via $() syntax
    # Only %VARNAME% format is supported for detection paths (not $env:VAR)
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)

    # Security: Block $env: or $() syntax in detection paths - these indicate potential injection
    if ($expanded -match '\$env:|\$\(') {
        Write-Status -Message (Get-LocalizedString -Key 'detect.security.variable_syntax_blocked' -Parameters @{ Path = $Path }) -Level 'Warning'
        return $null
    }

    # Security: Block path traversal attempts after expansion
    if ($expanded -match '\.\.' -or $expanded -match '[\\/]\.\.[\\/]?' -or $expanded -match '^\.\.') {
        Write-Status -Message (Get-LocalizedString -Key 'detect.security.path_traversal_after_expansion' -Parameters @{ Path = $expanded }) -Level 'Warning'
        return $null
    }

    # Security: Ensure path is absolute (not relative)
    if (-not [System.IO.Path]::IsPathRooted($expanded)) {
        # Allow wildcards in detection paths (e.g., "C:\Program Files\*\app.exe")
        if ($expanded -notmatch '^[A-Za-z]:[\\/]') {
            Write-Status -Message (Get-LocalizedString -Key 'detect.security.relative_path_blocked' -Parameters @{ Path = $expanded }) -Level 'Warning'
            return $null
        }
    }

    return $expanded
}

function Get-InstalledAppVersion {
    <#
    .SYNOPSIS
        Gets the installed version of an application via Winget or Chocolatey.
    .DESCRIPTION
        Attempts to retrieve the currently installed version of an application by
        querying Winget first (if a WingetId is provided and winget is available),
        then falling back to Chocolatey (if a ChocolateyId is provided and choco
        is available). Returns $null if the version cannot be determined from
        either package manager.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [string]$WingetId,

        [Parameter()]
        [string]$ChocolateyId
    )

    # Try Winget first
    if ($WingetId -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        try {
            $output = & winget list --id $WingetId --accept-source-agreements 2>&1 | Out-String
            # Parse version from winget list output (format: Name  Id  Version  Available  Source)
            # Version is typically after the ID column
            $lines = $output -split "`n" | Where-Object { $_ -match [regex]::Escape($WingetId) }
            if ($lines) {
                # Extract version using regex - version is usually a pattern like 1.2.3 or 1.2.3.4
                if ($lines[0] -match '\s(\d+[\.\d]+)\s') {
                    return $Matches[1]
                }
            }
        } catch {
            Write-Verbose "Winget version detection failed for $WingetId : $($_.Exception.Message)"
        }
    }

    # Try Chocolatey
    if ($ChocolateyId -and (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)) {
        try {
            $output = & choco list --local-only --exact $ChocolateyId 2>&1 | Out-String
            if ($output -match "$ChocolateyId\s+([\d\.]+)") {
                return $Matches[1]
            }
        } catch {
            Write-Verbose "Chocolatey version detection failed for $ChocolateyId : $($_.Exception.Message)"
        }
    }

    return $null
}

function Wait-ForOfficeInstallation {
    <#
    .SYNOPSIS
        Waits for Office Click-to-Run installation to complete.
    .DESCRIPTION
        Office installations via winget can take a long time due to Click-to-Run
        streaming technology. This function monitors the installation progress
        and waits until completion or timeout.

        Note: OfficeClickToRun.exe is a Windows service that runs permanently,
        not just during installation. We must check for Office installation
        first, regardless of whether processes are running.
    .PARAMETER TimeoutSeconds
        Maximum time to wait for installation in seconds (default: 2700 = 45 minutes for Office).
    .PARAMETER CheckIntervalSeconds
        How often to check installation status (default: 30 seconds).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [int]$TimeoutSeconds = 2700,

        [Parameter()]
        [int]$CheckIntervalSeconds = 30
    )

    Write-Status -Message (Get-LocalizedString -Key 'office.monitoring') -Level 'Info'

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $timeoutMs = $TimeoutSeconds * 1000

    # Registry paths that indicate Office is installed
    $officeRegistryPaths = @(
        (Get-RegistryPath -PathKey 'OfficeClickToRun'),
        # No centralized key for Office 16.0 InstallRoot sub-path - keeping as-is
        "$(Get-RegistryPath -PathKey 'OfficeInstallRoot')\16.0\Common\InstallRoot"
    )

    # Office executable paths
    $officePaths = @(
        "${env:ProgramFiles}\Microsoft Office\root\Office16\WINWORD.EXE",
        "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\WINWORD.EXE"
    )

    # Installation-specific processes (not the permanent C2R service)
    $installProcesses = @('setup', 'OfficeC2RClient')

    while ($stopwatch.ElapsedMilliseconds -lt $timeoutMs) {
        # ALWAYS check if Office is installed first (registry or files)
        # This is critical because OfficeClickToRun.exe runs as a permanent service
        foreach ($regPath in $officeRegistryPaths) {
            if (Test-Path $regPath -ErrorAction SilentlyContinue) {
                $elapsed = [math]::Round($stopwatch.Elapsed.TotalMinutes, 1)
                Write-Status -Message (Get-LocalizedString -Key 'office.completed_registry' -Parameters @{ Minutes = $elapsed }) -Level 'Success'
                return $true
            }
        }

        foreach ($officePath in $officePaths) {
            if (Test-Path $officePath -ErrorAction SilentlyContinue) {
                $elapsed = [math]::Round($stopwatch.Elapsed.TotalMinutes, 1)
                Write-Status -Message (Get-LocalizedString -Key 'office.completed_executable' -Parameters @{ Minutes = $elapsed }) -Level 'Success'
                return $true
            }
        }

        # Check if installation-specific processes are running (not the permanent service)
        $runningInstallProcesses = Get-Process -Name $installProcesses -ErrorAction SilentlyContinue

        if ($runningInstallProcesses) {
            $processNames = ($runningInstallProcesses | Select-Object -ExpandProperty Name -Unique) -join ', '
            Write-Status -Message (Get-LocalizedString -Key 'office.in_progress' -Parameters @{ Processes = $processNames; Minutes = [math]::Round($stopwatch.Elapsed.TotalMinutes, 1) }) -Level 'Verbose'
        } else {
            # No install processes and Office not detected yet
            $officeDetectionTimeoutMs = (Get-Timeout -TimeoutKey 'OfficeDetection') * 1000
            if ($stopwatch.ElapsedMilliseconds -gt $officeDetectionTimeoutMs) {
                Write-Status -Message (Get-LocalizedString -Key 'office.no_process_detected') -Level 'Warning'
                return $false
            }
        }

        Start-Sleep -Seconds $CheckIntervalSeconds
    }

    $timeoutMinutes = [math]::Round($TimeoutSeconds / 60, 1)
    Write-Status -Message (Get-LocalizedString -Key 'office.timed_out' -Parameters @{ Minutes = $timeoutMinutes }) -Level 'Warning'
    return $false
}

# === MAIN DETECTION FUNCTIONS ===

function Test-ApplicationByName {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    try {
        if (Test-CommandExists -Name 'winget') {
            # Use cached winget list for performance
            $wingetList = Get-CachedWingetList
            if ($wingetList -match [regex]::Escape($Name)) {
                return $true
            }
        }
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'detect.winget_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
    }

    try {
        if (Test-CommandExists -Name 'choco') {
            $chocoList = & choco list --local-only --exact $Name 2>&1 | Out-String
            if ($chocoList -match $Name -and $chocoList -notmatch '0 packages installed') {
                return $true
            }
        }
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'detect.choco_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
    }

    $programFiles = @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        "$env:LOCALAPPDATA\Programs"
    )

    foreach ($baseDir in $programFiles) {
        if (Test-Path -Path "$baseDir\$Name") {
            return $true
        }
    }

    return $false
}

function Test-ApplicationInstalled {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $appName = $Application.Name

    # === REGISTRY-FIRST OPTIMIZATION ===
    # Check registry first - this is ~100x faster than winget/choco CLI calls (~20ms vs ~2s)
    $registryApp = Get-RegistryInstalledApp -AppName $appName
    if ($registryApp) {
        # App found in registry - return immediately
        if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
            Write-Verbose (Get-LocalizedString -Key 'optimization.registry_hit' -Parameters @{ AppName = $appName; Version = $registryApp.Version })
        }
        return $true
    }

    # Special case: PowerToys - Check multiple paths
    if ($appName -eq 'Microsoft PowerToys') {
        $powerToysPaths = @(
            "${env:ProgramFiles}\PowerToys\PowerToys.exe",
            "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
            "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
        )

        foreach ($path in $powerToysPaths) {
            if (Test-Path $path -ErrorAction SilentlyContinue) {
                return $true
            }
        }

        if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) {
            return $true
        }
    }

    # Special case: Quick Assist - Store App (use winget to avoid Appx module conflicts)
    if ($appName -eq 'Microsoft Quick Assist') {
        if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            $wingetList = Get-CachedWingetList
            if ($wingetList -match 'MicrosoftCorporationII') {
                return $true
            }
        }
    }

    if (-not $Application.Detection) {
        $detected = Test-ApplicationByName -Name $appName
    } else {
        $method = $Application.Detection.Method
        $detected = $false

        switch ($method) {
            'Registry' {
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $detected = Test-RegistryKey -Path $Application.Detection.Path
                }
            }
            'File' {
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $expandedPath = Expand-DetectionPath -Path $Application.Detection.Path
                    $detected = Test-Path -Path $expandedPath -PathType Leaf
                }
            }
            'Command' {
                try {
                    # Secure command execution - parse command into executable and arguments
                    $commandParts = $Application.Detection.Command -split '\s+', 2
                    $executable = $commandParts[0]
                    $arguments = if ($commandParts.Count -gt 1) { $commandParts[1] } else { $null }

                    # Security: Only allow whitelisted executables for command detection
                    $exeBaseName = [System.IO.Path]::GetFileName($executable).ToLower()
                    if ($exeBaseName -notin (Get-DetectionAllowlist)) {
                        Write-Status -Message (Get-LocalizedString -Key 'detect.security.command_not_allowed' -Parameters @{ Executable = $executable }) -Level 'Verbose'
                        $detected = $false
                    }
                    # Security: Sanitize arguments - block dangerous patterns including newlines (command injection)
                    elseif (Test-DetectionArgumentDangerous -Arguments $arguments) {
                        Write-Status -Message (Get-LocalizedString -Key 'detect.security.dangerous_arguments' -Parameters @{ Executable = $executable }) -Level 'Warning'
                        $detected = $false
                    }
                    # Validate executable exists
                    elseif (Get-Command -Name $executable -ErrorAction SilentlyContinue) {
                        # Check if we need to verify output content (Arguments field contains expected pattern)
                        $expectedPattern = if ($Application.Detection.PSObject.Properties['Arguments']) { $Application.Detection.Arguments } else { $null }

                        # Security: Split arguments into array for safe execution (prevents shell interpretation)
                        $argArray = @(ConvertTo-DetectionArgumentArray -Arguments $arguments)

                        if ($expectedPattern) {
                            # Run command and capture output for pattern matching
                            # Security: Use array splatting to prevent argument injection
                            $output = if ($argArray.Count -gt 0) {
                                & $executable @argArray 2>&1 | Out-String
                            } else {
                                & $executable 2>&1 | Out-String
                            }
                            $detected = $output -match [regex]::Escape($expectedPattern)
                        } else {
                            # Execute securely with Start-Process for simple exit code check
                            $process = if ($argArray.Count -gt 0) {
                                Start-Process -FilePath $executable -ArgumentList $argArray -Wait -NoNewWindow -PassThru -ErrorAction Stop
                            } else {
                                Start-Process -FilePath $executable -Wait -NoNewWindow -PassThru -ErrorAction Stop
                            }
                            $detected = $process.ExitCode -eq 0
                        }
                    }
                } catch {
                    $detected = $false
                }
            }
            'WindowsFeature' {
                try {
                    $feature = Get-WindowsOptionalFeature -Online -FeatureName $Application.Detection.Feature -ErrorAction SilentlyContinue
                    $detected = $feature -and $feature.State -eq 'Enabled'
                } catch {
                    $detected = $false
                }
            }
            'WindowsCapability' {
                try {
                    $capability = Get-WindowsCapability -Online -Name "*$($Application.Detection.Capability)*" -ErrorAction SilentlyContinue
                    $detected = $capability -and $capability.State -eq 'Installed'
                } catch {
                    $detected = $false
                }
            }
            'StoreApp' {
                # Use cached winget list instead of Get-AppxPackage to avoid Appx module conflicts in PowerShell 7
                if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                    try {
                        $wingetList = Get-CachedWingetList

                        # Try 1: Match by Store ID (most specific)
                        if ($Application.Sources -and $Application.Sources.Store) {
                            if ($wingetList -match [regex]::Escape($Application.Sources.Store) -and $wingetList -notmatch "No installed package") {
                                $detected = $true
                            }
                        }

                        # Try 2: Match by full PackageName (specific, avoids false positives from vendor prefix)
                        if (-not $detected -and $Application.Detection.PackageName) {
                            if ($wingetList -match [regex]::Escape($Application.Detection.PackageName)) {
                                $detected = $true
                            }
                        }
                    } catch {
                        $detected = $false
                    }
                } else {
                    # Fallback to Get-AppxPackage if winget not available (PowerShell 5.1)
                    try {
                        $package = Get-AppxPackage -Name "*$($Application.Detection.PackageName)*" -ErrorAction SilentlyContinue
                        $detected = $null -ne $package
                    } catch {
                        $detected = $false
                    }
                }
            }
            default {
                $detected = Test-ApplicationByName -Name $appName
            }
        }
    }

    # Winget-based fallback detection if primary method failed
    if (-not $detected -and $Application.Sources -and $Application.Sources.Winget) {
        if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            try {
                $wingetId = $Application.Sources.Winget
                $wingetResult = & winget list --id $wingetId --accept-source-agreements 2>&1 | Out-String
                if ($wingetResult -match [regex]::Escape($wingetId) -and $wingetResult -notmatch "No installed package") {
                    $detected = $true
                }
            } catch {
                # Winget fallback detection failed silently - keep original detection result
                Write-Verbose "Winget fallback detection failed for $($Application.Name): $_"
            }
        }
    }

    return $detected
}

# === CACHE-BASED FAST DETECTION ===

<#
.SYNOPSIS
    Builds a cache of installed applications for fast batch detection.

.DESCRIPTION
    Queries registry, winget, and AppX packages once and returns a cache object
    that can be used with Test-ApplicationInstalledFast for rapid detection.

.OUTPUTS
    PSCustomObject containing:
    - RegistryApps: Hashtable of DisplayName -> PSObject (Version, Publisher, InstallLocation)
    - WingetOutput: Raw winget list output string
    - AppxPackages: Hashtable of PackageName prefix -> PSObject (Version, PackageFullName)

.EXAMPLE
    $cache = Get-InstalledApplicationsCache
    # Then use Test-ApplicationInstalledFast -Application $app -Cache $cache
#>
function Get-InstalledApplicationsCache {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    Write-Verbose "Building installed applications cache..."
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    # 1. Registry: Get all Win32 uninstall entries (one query)
    $registryApps = @{}
    $uninstallPaths = @(
        "$(Get-RegistryPath -PathKey 'UninstallX64')\*",
        "$(Get-RegistryPath -PathKey 'UninstallX86')\*",
        "$(Get-RegistryPath -PathKey 'UninstallCurrentUser')\*"
    )

    foreach ($path in $uninstallPaths) {
        try {
            $entries = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
            foreach ($entry in $entries) {
                if ($entry.DisplayName) {
                    $key = $entry.DisplayName.ToLowerInvariant()
                    if (-not $registryApps.ContainsKey($key)) {
                        $registryApps[$key] = [PSCustomObject]@{
                            DisplayName     = $entry.DisplayName
                            Version         = $entry.DisplayVersion
                            Publisher       = $entry.Publisher
                            InstallLocation = $entry.InstallLocation
                            UninstallString = $entry.UninstallString
                        }
                    }
                }
            }
        } catch {
            Write-Verbose "Error reading registry path $path : $_"
        }
    }

    Write-Verbose "Registry: Found $($registryApps.Count) apps"

    # 2. Winget: Get list output once (using WingetCache module)
    $wingetOutput = ""
    if (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
        try {
            $wingetOutput = Get-CachedWingetList
        } catch {
            Write-Verbose "Error running winget list: $_"
        }
    }

    Write-Verbose "Winget: Output from cache ($($wingetOutput.Length) chars)"

    # 3. AppX: Get all Store/MSIX packages (one query)
    $appxPackages = @{}
    try {
        $packages = Get-AppxPackage -ErrorAction SilentlyContinue
        foreach ($pkg in $packages) {
            # Use package name prefix for matching (e.g., "Microsoft.PowerToys" matches "Microsoft.PowerToys.SparseApp")
            $prefix = $pkg.Name -replace '\..*$', ''
            if (-not $appxPackages.ContainsKey($pkg.Name)) {
                $appxPackages[$pkg.Name] = [PSCustomObject]@{
                    Name            = $pkg.Name
                    Version         = $pkg.Version
                    PackageFullName = $pkg.PackageFullName
                    Publisher       = $pkg.Publisher
                }
            }
        }
    } catch {
        Write-Verbose "Error getting AppX packages: $_"
    }

    Write-Verbose "AppX: Found $($appxPackages.Count) packages"

    # 4. Pre-cache common command outputs (for Command detection method)
    $commandOutputs = @{}

    $commandCacheDefinitions = @(
        @{ Name = 'dotnet'; Commands = @(
            @{ Key = 'dotnet --list-runtimes'; Args = @('--list-runtimes') },
            @{ Key = 'dotnet --version'; Args = @('--version') }
        ) },
        @{ Name = 'java'; Commands = @(
            @{ Key = 'java -version'; Args = @('-version') }
        ) },
        @{ Name = 'python'; Commands = @(
            @{ Key = 'python --version'; Args = @('--version') }
        ) },
        @{ Name = 'node'; Commands = @(
            @{ Key = 'node --version'; Args = @('--version') }
        ) },
        @{ Name = 'git'; Commands = @(
            @{ Key = 'git --version'; Args = @('--version') }
        ) }
    )

    foreach ($definition in $commandCacheDefinitions) {
        $executable = [string]$definition.Name
        if (-not (Get-Command -Name $executable -ErrorAction SilentlyContinue)) {
            continue
        }

        foreach ($command in $definition.Commands) {
            try {
                $commandOutputs[$command.Key] = & $executable @($command.Args) 2>&1 | Out-String
            } catch {
                Write-Verbose "Failed to cache $executable info: $($_.Exception.Message)"
            }
        }
    }

    Write-Verbose "Commands: Cached $($commandOutputs.Count) command outputs"

    $stopwatch.Stop()
    Write-Verbose "Cache built in $($stopwatch.ElapsedMilliseconds)ms"

    return [PSCustomObject]@{
        RegistryApps    = $registryApps
        WingetOutput    = $wingetOutput
        AppxPackages    = $appxPackages
        CommandOutputs  = $commandOutputs
        CacheBuiltAt    = [DateTime]::Now
    }
}

<#
.SYNOPSIS
    Fast application detection using pre-built cache.

.DESCRIPTION
    Checks if an application is installed using the cached data from Get-InstalledApplicationsCache.
    This is much faster than Test-ApplicationInstalled for batch operations.

.PARAMETER Application
    The application object from the database.

.PARAMETER Cache
    The cache object from Get-InstalledApplicationsCache.

.OUTPUTS
    Boolean indicating if the application is installed.
#>
function Test-ApplicationInstalledFast {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter(Mandatory)]
        [PSCustomObject]$Cache
    )

    $appName = $Application.Name
    $detected = $false
    $version = $null

    # Method 1: Check by detection configuration
    if ($Application.Detection) {
        $method = $Application.Detection.Method

        switch ($method) {
            'Registry' {
                # Check if registry path exists (with null-safe property check)
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $detected = Test-Path -Path $Application.Detection.Path -ErrorAction SilentlyContinue
                    # Try to get version from registry using VersionKey if specified
                    if ($detected) {
                        try {
                            $regEntry = Get-ItemProperty -Path $Application.Detection.Path -ErrorAction SilentlyContinue
                            # Use VersionKey from app config if specified, otherwise fall back to common keys
                            $versionKey = if ($Application.Detection.PSObject.Properties['VersionKey']) {
                                $Application.Detection.VersionKey
                            } else {
                                $null
                            }
                            if ($versionKey -and $regEntry.PSObject.Properties[$versionKey]) {
                                $version = $regEntry.$versionKey
                            } elseif ($regEntry.DisplayVersion) {
                                $version = $regEntry.DisplayVersion
                            } elseif ($regEntry.Version) {
                                $version = $regEntry.Version
                            } elseif ($regEntry.CurrentVersion) {
                                $version = $regEntry.CurrentVersion
                            }
                        } catch {
                            Write-Verbose "Failed to get registry version for $($Application.Name): $($_.Exception.Message)"
                        }
                    }
                }
            }
            'File' {
                # Check if file exists (with null-safe property check)
                if ($Application.Detection.PSObject.Properties['Path'] -and $Application.Detection.Path) {
                    $expandedPath = Expand-DetectionPath -Path $Application.Detection.Path
                    $detected = Test-Path -Path $expandedPath -PathType Leaf -ErrorAction SilentlyContinue
                    # Try to get version from file
                    if ($detected) {
                        try {
                            $fileInfo = Get-Item -Path $expandedPath -ErrorAction SilentlyContinue
                            if ($fileInfo.VersionInfo.FileVersion) {
                                $version = $fileInfo.VersionInfo.FileVersion
                            }
                        } catch {
                            Write-Verbose "Failed to get file version for $($Application.Name): $($_.Exception.Message)"
                        }
                    }
                }
            }
            'StoreApp' {
                # Check in AppX cache by package name prefix
                $packageName = $Application.Detection.PackageName
                if ($packageName) {
                    foreach ($key in $Cache.AppxPackages.Keys) {
                        if ($key -like "$packageName*" -or $key -eq $packageName) {
                            $detected = $true
                            $version = $Cache.AppxPackages[$key].Version
                            break
                        }
                    }
                }
                # Also check winget output for Store ID
                if (-not $detected -and $Application.Sources -and $Application.Sources.Store -and $Cache.WingetOutput) {
                    $detected = $Cache.WingetOutput -match [regex]::Escape($Application.Sources.Store)
                }
            }
            'Command' {
                # Try to use cached command output first (much faster)
                try {
                    $fullCommand = $Application.Detection.Command
                    $commandParts = $fullCommand -split '\s+', 2
                    $executable = $commandParts[0]
                    $expectedPattern = if ($Application.Detection.PSObject.Properties['Arguments']) { $Application.Detection.Arguments } else { $null }
                    # Get custom VersionRegex from app config or use default
                    $versionRegex = if ($Application.Detection.PSObject.Properties['VersionRegex']) {
                        $Application.Detection.VersionRegex
                    } else {
                        '(\d+\.\d+[\.\d]*)'  # Default pattern
                    }

                    # Security: Only allow whitelisted executables for command detection
                    $exeBaseName = [System.IO.Path]::GetFileName($executable).ToLower()
                    if ($exeBaseName -notin (Get-DetectionAllowlist)) {
                        $detected = $false
                    } else {
                        # Check if this command output is cached
                        $output = $null
                        if ($Cache.CommandOutputs -and $Cache.CommandOutputs.ContainsKey($fullCommand)) {
                            $output = $Cache.CommandOutputs[$fullCommand]
                        }

                        if ($output) {
                            # Use cached output
                            if ($expectedPattern) {
                                $detected = $output -match [regex]::Escape($expectedPattern)
                                if ($detected -and $output -match $versionRegex) {
                                    $version = $matches[1]
                                }
                            } else {
                                $detected = $true
                                # Extract version even without expected pattern
                                if ($output -match $versionRegex) {
                                    $version = $matches[1]
                                }
                            }
                        } elseif (Get-Command -Name $executable -ErrorAction SilentlyContinue) {
                            # Fallback: execute command if not cached
                            $arguments = if ($commandParts.Count -gt 1) { $commandParts[1] } else { $null }

                            # Security: Sanitize arguments - block dangerous patterns
                            if ($arguments -and ($arguments -match '[;&|`$\(\)]|>>|<<')) {
                                Write-Status -Message (Get-LocalizedString -Key 'detect.security.dangerous_arguments' -Parameters @{ Executable = $executable }) -Level 'Warning'
                                $detected = $false
                            } else {
                                $output = if ($arguments) {
                                    & $executable $arguments 2>&1 | Out-String
                                } else {
                                    & $executable 2>&1 | Out-String
                                }
                                if ($expectedPattern) {
                                    $detected = $output -match [regex]::Escape($expectedPattern)
                                } else {
                                    $detected = $true
                                }
                            }
                            # Extract version using custom regex
                            if ($detected -and $output -match $versionRegex) {
                                $version = $matches[1]
                            }
                        }
                    }
                } catch {
                    $detected = $false
                }
            }
            'WindowsFeature' {
                $feature = Get-WindowsOptionalFeature -Online -FeatureName $Application.Detection.Feature -ErrorAction SilentlyContinue
                $detected = $feature -and $feature.State -eq 'Enabled'
            }
            'WindowsCapability' {
                $capability = Get-WindowsCapability -Online -Name "*$($Application.Detection.Capability)*" -ErrorAction SilentlyContinue
                $detected = $capability -and $capability.State -eq 'Installed'
            }
        }
    }

    # Method 2: Check by Winget ID in cached output (also extract version)
    if (-not $detected -and $Application.Sources -and $Application.Sources.Winget -and $Cache.WingetOutput) {
        $wingetId = $Application.Sources.Winget
        if ($Cache.WingetOutput -match [regex]::Escape($wingetId)) {
            $detected = $true
            # Try to extract version from winget output line
            $lines = $Cache.WingetOutput -split "`n"
            foreach ($line in $lines) {
                if ($line -match [regex]::Escape($wingetId)) {
                    # Parse version from the line (typically format: Name  Id  Version  Available  Source)
                    if ($line -match '(\d+\.[\d.]+)') {
                        $version = $matches[1]
                    }
                    break
                }
            }
        }
    }

    # Method 3: Check by app name in Registry cache
    if (-not $detected -and $Cache.RegistryApps) {
        $nameLower = $appName.ToLowerInvariant()
        # Exact match
        if ($Cache.RegistryApps.ContainsKey($nameLower)) {
            $detected = $true
            $version = $Cache.RegistryApps[$nameLower].Version
        }
        # Partial match
        if (-not $detected) {
            foreach ($key in $Cache.RegistryApps.Keys) {
                if ($key -like "*$nameLower*" -or $nameLower -like "*$key*") {
                    $detected = $true
                    $version = $Cache.RegistryApps[$key].Version
                    break
                }
            }
        }
    }

    return [PSCustomObject]@{
        IsInstalled = $detected
        Version     = $version
    }
}

<#
.SYNOPSIS
    Batch detection of multiple applications using optimized cache.

.DESCRIPTION
    Detects installation status and version for multiple applications in a single optimized operation.
    Returns a hashtable mapping AppId to status object with IsInstalled and Version.

.PARAMETER Applications
    Array of application objects from the database.

.OUTPUTS
    Hashtable mapping AppId -> PSCustomObject with IsInstalled (bool) and Version (string)
#>
function Get-ApplicationsInstallationStatus {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications
    )

    Write-Host (Get-LocalizedString -Key 'install.detection.scan_starting' -Parameters @{ Count = $Applications.Count }) -ForegroundColor Cyan

    # Build cache once
    $cache = Get-InstalledApplicationsCache

    # Check all apps using cache
    $results = @{}
    $installedCount = 0

    foreach ($app in $Applications) {
        $status = Test-ApplicationInstalledFast -Application $app -Cache $cache
        $results[$app.AppId] = @{
            IsInstalled = $status.IsInstalled
            Version     = $status.Version
        }
        if ($status.IsInstalled) { $installedCount++ }
    }

    Write-Host (Get-LocalizedString -Key 'install.detection.scan_complete' -Parameters @{ Installed = $installedCount; Total = $Applications.Count }) -ForegroundColor Green

    return $results
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    # Registry-first optimization
    'Get-RegistryInstalledApp',
    'Clear-RegistryAppsCache',
    # Helper functions
    'Test-RegistryKey',
    'Expand-DetectionPath',
    'Get-InstalledAppVersion',
    'Wait-ForOfficeInstallation',
    # Main detection functions
    'Test-ApplicationByName',
    'Test-ApplicationInstalled',
    # Cache-based fast detection
    'Get-InstalledApplicationsCache',
    'Test-ApplicationInstalledFast',
    'Get-ApplicationsInstallationStatus'
)
