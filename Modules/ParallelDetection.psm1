<#
.SYNOPSIS
    WinForge - Parallel Detection v3.7.2

.DESCRIPTION
    Lightweight detection module optimized for parallel job execution.
    Reduces memory footprint by avoiding string duplication of detection logic.

.NOTES
    Author: Julien Bombled
    v3.7.2
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

# === CONFIGURATION ===

# Import DetectionAllowlist: the shared command-detection allowlist (single source).
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
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

# Import DetectionRegistryGuard: the shared registry-path validation (single source).
$script:DetectionRegistryGuardPath = Join-Path $script:ModuleRoot 'DetectionRegistryGuard.psm1'
if (-not (Get-Command -Name Test-RegistryPathAllowed -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DetectionRegistryGuardPath) {
        Import-Module -Name $script:DetectionRegistryGuardPath -Force
    }
}

# Special-case detections: applications that require bespoke detection logic
# instead of (or before) the generic Detection config / winget fallback.
# Keyed by the application's Name property.
$script:SpecialCaseDetections = @{
    PowerToysAppName    = 'Microsoft PowerToys'
    PowerToysExePaths   = @(
        "${env:ProgramFiles}\PowerToys\PowerToys.exe",
        "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
        "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
    )
    PowerToysProcessName = 'PowerToys'
    QuickAssistAppName   = 'Microsoft Quick Assist'
    QuickAssistPackage   = 'MicrosoftCorporationII.QuickAssist'
}

# === DETECTION FUNCTIONS ===

function Test-AppInstalledParallel {
    <#
    .SYNOPSIS
        Fast application detection for parallel installation jobs.
    .DESCRIPTION
        Lightweight detection function optimized for parallel execution.
        Supports multiple detection methods: Registry, File, Command, WindowsFeature, etc.
    .PARAMETER App
        Application object with Detection configuration.
    .PARAMETER WingetListCache
        Optional pre-cached winget list output to avoid redundant CLI calls.
    .OUTPUTS
        Boolean indicating if application is installed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$App,

        [Parameter()]
        [string]$WingetListCache
    )

    $appName = $App.Name

    # === SPECIAL CASES ===

    # PowerToys detection
    if ($appName -eq $script:SpecialCaseDetections.PowerToysAppName) {
        foreach ($p in $script:SpecialCaseDetections.PowerToysExePaths) {
            if (Test-Path $p -ErrorAction SilentlyContinue) { return $true }
        }
        if (Get-Process -Name $script:SpecialCaseDetections.PowerToysProcessName -ErrorAction SilentlyContinue) { return $true }
    }

    # Quick Assist detection
    if ($appName -eq $script:SpecialCaseDetections.QuickAssistAppName) {
        try {
            $pkg = Get-AppxPackage -Name $script:SpecialCaseDetections.QuickAssistPackage -ErrorAction SilentlyContinue
            if ($pkg) { return $true }
        } catch {
            Write-Verbose "Quick Assist detection failed: $($_.Exception.Message)"
        }
    }

    # === NO DETECTION CONFIG - USE WINGET ===

    if (-not $App.Detection) {
        if ($WingetListCache) {
            if ($WingetListCache -match [regex]::Escape($appName)) { return $true }
        } elseif (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
            try {
                $result = Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList @('list', '--accept-source-agreements')
                $list = $result.Output
                if ($list -match [regex]::Escape($appName)) { return $true }
            } catch {
                Write-Verbose "Winget list detection failed for $appName : $($_.Exception.Message)"
            }
        }
        return $false
    }

    # === DETECTION METHOD DISPATCH ===

    switch ($App.Detection.Method) {
        'Registry' {
            return Test-RegistryDetection -Detection $App.Detection
        }
        'File' {
            return Test-FileDetection -Detection $App.Detection
        }
        'Command' {
            return Test-CommandDetection -Detection $App.Detection
        }
        'WindowsFeature' {
            return Test-WindowsFeatureDetection -Detection $App.Detection
        }
        'WindowsCapability' {
            return Test-WindowsCapabilityDetection -Detection $App.Detection
        }
        'StoreApp' {
            return Test-StoreAppDetection -App $App -WingetListCache $WingetListCache
        }
        default {
            # Fallback to winget list check
            if ($WingetListCache) {
                if ($WingetListCache -match [regex]::Escape($appName)) { return $true }
            } elseif (Get-Command -Name 'winget' -ErrorAction SilentlyContinue) {
                try {
                    $result = Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList @('list', '--accept-source-agreements')
                    $list = $result.Output
                    if ($list -match [regex]::Escape($appName)) { return $true }
                } catch {
                    Write-Verbose "Winget fallback detection failed for $appName : $($_.Exception.Message)"
                }
            }
            return $false
        }
    }
}

function Test-RegistryDetection {
    <#
    .SYNOPSIS
        Tests if an application is detected via registry path lookup.
    .DESCRIPTION
        Checks whether a registry path specified in the detection metadata exists on the system.
        Includes security validation to block path traversal patterns before accessing the registry.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    if (-not ($Detection.PSObject.Properties['Path'] -and $Detection.Path)) {
        return $false
    }

    $regPath = $Detection.Path

    # Security: Block path traversal
    if ($regPath -match '\.\.') { return $false }

    # Security: enforce the shared registry hive allowlist/blocklist (I5, same rule as the gold path)
    if (-not (Test-RegistryPathAllowed -Path $regPath)) {
        return $false
    }

    return Test-Path -Path $regPath -ErrorAction SilentlyContinue
}

function Test-FileDetection {
    <#
    .SYNOPSIS
        Tests if an application is detected via file path existence check.
    .DESCRIPTION
        Verifies whether a file exists at the path specified in the detection metadata, with support
        for environment variable expansion and wildcard paths. Enforces security checks including
        path traversal prevention and absolute path requirements.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    if (-not ($Detection.PSObject.Properties['Path'] -and $Detection.Path)) {
        return $false
    }

    $rawPath = $Detection.Path

    # Security: Block path traversal patterns
    if ($rawPath -match '\.\.' -or $rawPath -match '[\\/]\.\.[\\/]?' -or $rawPath -match '^\.\.') {
        return $false
    }

    $expandedPath = [Environment]::ExpandEnvironmentVariables($rawPath)

    # Validate expanded path
    if ($expandedPath -match '\.\.' -or $expandedPath -match '[\\/]\.\.[\\/]?' -or $expandedPath -match '^\.\.') {
        return $false
    }

    # Require absolute path
    if ($expandedPath -notmatch '^[A-Za-z]:[\\/]') {
        return $false
    }

    # Handle wildcard paths
    if ($expandedPath -match '\*') {
        return @(Get-ChildItem -Path $expandedPath -ErrorAction SilentlyContinue).Count -gt 0
    }

    return Test-Path -Path $expandedPath -PathType Leaf -ErrorAction SilentlyContinue
}

function Test-CommandDetection {
    <#
    .SYNOPSIS
        Tests if an application is detected via command availability or execution.
    .DESCRIPTION
        Determines whether an application is present by verifying command availability and optionally
        running it to check output against an expected pattern. Only whitelisted executables are
        permitted for security.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    try {
        $parts = $Detection.Command -split '\s+', 2
        $exe = $parts[0]
        $cmdArgs = if ($parts.Count -gt 1) { $parts[1] } else { $null }

        # Security: Validate executable is whitelisted
        $exeBaseName = [System.IO.Path]::GetFileName($exe).ToLower()
        if ($exeBaseName -notin (Get-DetectionAllowlist)) {
            return $false
        }

        # Security: block dangerous argument patterns (command injection), via the
        # same shared guard the sequential gold path uses.
        if (Test-DetectionArgumentDangerous -Arguments $cmdArgs) {
            return $false
        }

        # Verify command exists
        if (-not (Get-Command -Name $exe -ErrorAction SilentlyContinue)) {
            return $false
        }

        # Check with expected pattern if provided
        $expectedPattern = if ($Detection.PSObject.Properties['Arguments']) { $Detection.Arguments } else { $null }

        # Security: split arguments into an array for safe execution (no shell
        # re-interpretation), matching the sequential gold path.
        $argArray = @(ConvertTo-DetectionArgumentArray -Arguments $cmdArgs)

        if ($expectedPattern) {
            $result = Invoke-NativeCommandUtf8 -FilePath $exe -ArgumentList $argArray
            $output = $result.Output
            return $output -match [regex]::Escape($expectedPattern)
        } else {
            $proc = if ($argArray.Count -gt 0) {
                Start-Process -FilePath $exe -ArgumentList $argArray -Wait -WindowStyle Hidden -PassThru -ErrorAction Stop
            } else {
                Start-Process -FilePath $exe -Wait -WindowStyle Hidden -PassThru -ErrorAction Stop
            }
            return $proc.ExitCode -eq 0
        }
    } catch {
        return $false
    }
}

function Test-WindowsFeatureDetection {
    <#
    .SYNOPSIS
        Tests if a Windows optional feature is enabled.
    .DESCRIPTION
        Queries Windows optional features to determine whether the specified feature is currently
        enabled on the system. Returns false if the feature is not found or not enabled.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    try {
        $feature = Get-WindowsOptionalFeature -Online -FeatureName $Detection.Feature -ErrorAction SilentlyContinue
        return $feature -and $feature.State -eq 'Enabled'
    } catch {
        return $false
    }
}

function Test-WindowsCapabilityDetection {
    <#
    .SYNOPSIS
        Tests if a Windows capability is installed.
    .DESCRIPTION
        Queries Windows capabilities to determine whether the specified capability is installed
        on the system. Uses wildcard matching on the capability name for flexible detection.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    try {
        # I6: filter and suppress errors on Get-WindowsCapability itself (the -Name
        # wildcard), not on a downstream Where-Object where -ErrorAction is a no-op.
        $capability = Get-WindowsCapability -Online -Name "*$($Detection.Capability)*" -ErrorAction SilentlyContinue
        return $capability -and $capability.State -eq 'Installed'
    } catch {
        return $false
    }
}

function Test-StoreAppDetection {
    <#
    .SYNOPSIS
        Tests if an application is detected via Microsoft Store or Winget listing.
    .DESCRIPTION
        Checks whether an application appears in the Winget list output, using either a provided
        cache string or by querying Winget directly. Supports matching by Winget ID, Chocolatey
        package name, or application display name.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [PSCustomObject]$App,
        [string]$WingetListCache
    )

    $list = $WingetListCache
    if (-not $list -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        try {
            $result = Invoke-NativeCommandUtf8 -FilePath 'winget' -ArgumentList @('list', '--accept-source-agreements')
            $list = $result.Output
        } catch {
            return $false
        }
    }

    if (-not $list) { return $false }

    # Check by Store ID
    $storeId = $null
    if ($App.PSObject.Properties['Sources'] -and $App.Sources -and $App.Sources.PSObject.Properties['Store']) {
        $storeId = $App.Sources.Store
    }

    if ($storeId -and $list -match [regex]::Escape($storeId)) {
        if ($list -notmatch "No installed package") {
            return $true
        }
    }

    # Check by PackageName
    if ($App.Detection.PackageName -and $list -match [regex]::Escape($App.Detection.PackageName)) {
        return $true
    }

    return $false
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Test-AppInstalledParallel',
    'Test-RegistryDetection',
    'Test-FileDetection',
    'Test-CommandDetection',
    'Test-WindowsFeatureDetection',
    'Test-WindowsCapabilityDetection',
    'Test-StoreAppDetection'
)
