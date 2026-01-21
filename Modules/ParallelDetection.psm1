<#
.SYNOPSIS
    Win11Forge - Parallel Detection Module v3.4.0

.DESCRIPTION
    Lightweight detection module optimized for parallel job execution.
    Reduces memory footprint by avoiding string duplication of detection logic.

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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

# Security: Whitelist of allowed executables for Command detection
$script:AllowedDetectionExecutables = @(
    'java', 'java.exe', 'javac', 'javac.exe',
    'dotnet', 'dotnet.exe',
    'python', 'python.exe', 'python3', 'python3.exe',
    'node', 'node.exe', 'npm', 'npm.cmd',
    'git', 'git.exe',
    'docker', 'docker.exe',
    'rustc', 'rustc.exe', 'cargo', 'cargo.exe',
    'go', 'go.exe',
    'ruby', 'ruby.exe',
    'php', 'php.exe',
    'perl', 'perl.exe'
)

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
    if ($appName -eq 'Microsoft PowerToys') {
        $paths = @(
            "${env:ProgramFiles}\PowerToys\PowerToys.exe",
            "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
            "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
        )
        foreach ($p in $paths) {
            if (Test-Path $p -ErrorAction SilentlyContinue) { return $true }
        }
        if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) { return $true }
    }

    # Quick Assist detection
    if ($appName -eq 'Microsoft Quick Assist') {
        try {
            $pkg = Get-AppxPackage -Name "MicrosoftCorporationII.QuickAssist" -ErrorAction SilentlyContinue
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
                $list = & winget list --accept-source-agreements 2>&1 | Out-String
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
                    $list = & winget list --accept-source-agreements 2>&1 | Out-String
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
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    if (-not ($Detection.PSObject.Properties['Path'] -and $Detection.Path)) {
        return $false
    }

    $regPath = $Detection.Path

    # Security: Block path traversal
    if ($regPath -match '\.\.') { return $false }

    return Test-Path -Path $regPath -ErrorAction SilentlyContinue
}

function Test-FileDetection {
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
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    try {
        $parts = $Detection.Command -split '\s+', 2
        $exe = $parts[0]
        $cmdArgs = if ($parts.Count -gt 1) { $parts[1] } else { $null }

        # Security: Validate executable is whitelisted
        $exeBaseName = [System.IO.Path]::GetFileName($exe).ToLower()
        if ($exeBaseName -notin $script:AllowedDetectionExecutables) {
            return $false
        }

        # Verify command exists
        if (-not (Get-Command -Name $exe -ErrorAction SilentlyContinue)) {
            return $false
        }

        # Check with expected pattern if provided
        $expectedPattern = if ($Detection.PSObject.Properties['Arguments']) { $Detection.Arguments } else { $null }

        if ($expectedPattern) {
            $output = if ($cmdArgs) {
                & $exe $cmdArgs 2>&1 | Out-String
            } else {
                & $exe 2>&1 | Out-String
            }
            return $output -match [regex]::Escape($expectedPattern)
        } else {
            $proc = if ($cmdArgs) {
                Start-Process -FilePath $exe -ArgumentList $cmdArgs -Wait -NoNewWindow -PassThru -ErrorAction Stop
            } else {
                Start-Process -FilePath $exe -Wait -NoNewWindow -PassThru -ErrorAction Stop
            }
            return $proc.ExitCode -eq 0
        }
    } catch {
        return $false
    }
}

function Test-WindowsFeatureDetection {
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
    [CmdletBinding()]
    [OutputType([bool])]
    param([PSCustomObject]$Detection)

    try {
        $capability = Get-WindowsCapability -Online | Where-Object {
            $_.Name -like "*$($Detection.Capability)*"
        } -ErrorAction SilentlyContinue
        return $capability -and $capability.State -eq 'Installed'
    } catch {
        return $false
    }
}

function Test-StoreAppDetection {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [PSCustomObject]$App,
        [string]$WingetListCache
    )

    $list = $WingetListCache
    if (-not $list -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        try {
            $list = & winget list --accept-source-agreements 2>&1 | Out-String
        } catch {
            return $false
        }
    }

    if (-not $list) { return $false }

    # Check by Store ID
    if ($App.Sources.Store -and $list -match [regex]::Escape($App.Sources.Store)) {
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
