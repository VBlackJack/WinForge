<#
.SYNOPSIS
    Win11Forge - Prerequisites installation module (v2.1.2 FIXED)

.DESCRIPTION
    Handles installation of system prerequisites with environment refresh:
    - PowerShell 7
    - Chocolatey
    - Winget
    - .NET runtimes (.NET 6, 8, Framework 4.8.1)
    - Visual C++ redistributables (2015-2022)
    - Java runtime
    - Environment variable refresh

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
    Fixed: Write-Status empty strings handling
    Fixed: Chocolatey installation arguments
    All bugs from deployment test have been corrected
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

# Use centralized module loader for core dependencies
$script:ModuleLoaderPath = Join-Path $script:RepositoryRoot 'Core\ModuleLoader.psm1'
if (Test-Path -Path $script:ModuleLoaderPath) {
    Import-Module -Name $script:ModuleLoaderPath -Force
    $null = Initialize-Win11ForgeModule
} else {
    # Fallback: direct import if ModuleLoader not available
    $script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
    if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
        if (Test-Path -Path $script:CoreModulePath) {
            Import-Module -Name $script:CoreModulePath -Force
        } else {
            throw 'Core module is required before loading Prerequisites.psm1'
        }
    }
    $script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
    if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
        if (Test-Path -Path $script:LocalizationModulePath) {
            Import-Module -Name $script:LocalizationModulePath -Force
        }
    }
}

# === CONFIGURATION PATHS ===
$script:DownloadSourcesPath = Join-Path $script:RepositoryRoot 'Config\download-sources.json'

# === DOWNLOAD SOURCES CONFIGURATION ===
$script:DownloadSources = $null

function Get-DownloadSources {
    <#
    .SYNOPSIS
        Loads download sources configuration from JSON file.
    .OUTPUTS
        Hashtable containing download URLs and configuration
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($null -eq $script:DownloadSources) {
        if (Test-Path -Path $script:DownloadSourcesPath) {
            try {
                $content = Get-Content -Path $script:DownloadSourcesPath -Raw -Encoding UTF8
                # PS5.1 compatible: don't use -AsHashtable, dot notation works with PSCustomObject
                $script:DownloadSources = $content | ConvertFrom-Json -ErrorAction Stop
            }
            catch {
                Write-Warning "Failed to load download sources config: $($_.Exception.Message). Using fallback URLs."
                # Fallback to default values - UPDATE THESE VERSIONS PERIODICALLY
                $script:DownloadSources = @{
                    prerequisites = @{
                        chocolatey = @{
                            downloadUrl = 'https://community.chocolatey.org/api/v2/package/chocolatey'
                        }
                        powershell7 = @{
                            version = '7.5.0'
                            downloadUrl = 'https://github.com/PowerShell/PowerShell/releases/download/v7.5.0/PowerShell-7.5.0-win-x64.msi'
                        }
                    }
                }
            }
        }
        else {
            Write-Warning "Download sources config not found at: $script:DownloadSourcesPath. Using fallback URLs."
            # Fallback to default values - UPDATE THESE VERSIONS PERIODICALLY
            $script:DownloadSources = @{
                prerequisites = @{
                    chocolatey = @{
                        downloadUrl = 'https://community.chocolatey.org/api/v2/package/chocolatey'
                    }
                    powershell7 = @{
                        version = '7.5.0'
                        downloadUrl = 'https://github.com/PowerShell/PowerShell/releases/download/v7.5.0/PowerShell-7.5.0-win-x64.msi'
                    }
                }
            }
        }
    }

    return $script:DownloadSources
}

# === ENVIRONMENT REFRESH ===

function Update-EnvironmentPath {
    <#
    .SYNOPSIS
        Refreshes environment variables from registry without restarting session.

    .DESCRIPTION
        Reloads PATH and other environment variables from:
        - Machine-level (HKLM)
        - User-level (HKCU)
        - Session-level (Process)
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Verbose "Refreshing environment variables..."

        # Get Machine PATH
        $machinePath = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine')

        # Get User PATH
        $userPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')

        # Combine and set Process PATH (PS 5.1 compatible)
        $combinedPath = (@($machinePath, $userPath) | Where-Object { $_ }) -join ';'
        [System.Environment]::SetEnvironmentVariable('PATH', $combinedPath, 'Process')

        # Update $env:PATH
        $env:PATH = $combinedPath

        # Refresh other common environment variables
        $envVars = @(
            'ChocolateyInstall',
            'JAVA_HOME',
            'DOTNET_ROOT',
            'ProgramFiles',
            'ProgramFiles(x86)',
            'CommonProgramFiles',
            'PSModulePath'
        )

        foreach ($varName in $envVars) {
            $machineValue = [System.Environment]::GetEnvironmentVariable($varName, 'Machine')
            $userValue = [System.Environment]::GetEnvironmentVariable($varName, 'User')

            $value = if ($userValue) { $userValue } elseif ($machineValue) { $machineValue }

            if ($value) {
                [System.Environment]::SetEnvironmentVariable($varName, $value, 'Process')
                Set-Item -Path "env:$varName" -Value $value -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Verbose "Environment variables refreshed successfully"
        return $true
    } catch {
        Write-Verbose "Error refreshing environment: $($_.Exception.Message)"
        return $false
    }
}

function Invoke-EnvironmentRefresh {
    <#
    .SYNOPSIS
        Forces a complete environment refresh including PATH discovery.
    #>
    [CmdletBinding()]
    param()

    Update-EnvironmentPath | Out-Null

    # Refresh PowerShell's command cache
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('Path', 'User')

    # Force Get-Command cache refresh
    Get-Command -All | Out-Null

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.environment.refreshed') -Level 'Success'
}

# === HELPER FUNCTIONS ===

function Test-CommandAvailable {
    <#
    .SYNOPSIS
        Tests if a command is available in the current session.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Invoke-ExternalProcess {
    <#
    .SYNOPSIS
        Executes an external process with error handling.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [string[]]$ArgumentList,

        [switch]$RefreshEnvironment
    )

    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -NoNewWindow -PassThru

        if ($RefreshEnvironment) {
            Invoke-EnvironmentRefresh
        }

        if ($process.ExitCode -eq 0) {
            return $true
        }

        Write-Verbose "Process $FilePath exited with code $($process.ExitCode)"
        return $false
    } catch {
        Write-Verbose "Failed to start process $FilePath : $($_.Exception.Message)"
        return $false
    }
}

# === CHOCOLATEY INSTALLATION ===

function Install-Chocolatey {
    <#
    .SYNOPSIS
        Installs Chocolatey package manager.
    .DESCRIPTION
        Uses manual installation method to avoid dependency on Microsoft.PowerShell.Archive
        module which may not be available on fresh Windows 11 installations.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $chocolateyAvailable = Test-CommandAvailable -Name 'choco'

    if ($chocolateyAvailable -and -not $Force) {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.already_installed') -Level 'Info'
        return $true
    }

    try {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.installing') -Level 'Info'

        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072

        # Load .NET compression assembly for ZIP extraction
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop

        # Define installation paths
        $chocoInstallPath = $env:ChocolateyInstall
        if (-not $chocoInstallPath) {
            $chocoInstallPath = "$env:ProgramData\chocolatey"
        }
        $chocoTempPath = Join-Path $env:TEMP "chocolatey-install-$(Get-Date -Format 'yyyyMMddHHmmss')"

        # Create temp directory
        New-Item -ItemType Directory -Path $chocoTempPath -Force | Out-Null

        try {
            # Download latest Chocolatey nupkg
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.downloading') -Level 'Info'
            $chocoNupkg = Join-Path $chocoTempPath 'chocolatey.nupkg'
            $webClient = New-Object System.Net.WebClient
            $sources = Get-DownloadSources
            $chocoUrl = $sources.prerequisites.chocolatey.downloadUrl
            $webClient.DownloadFile($chocoUrl, $chocoNupkg)
            $webClient.Dispose()

            # Extract nupkg (it's just a ZIP file)
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.extracting') -Level 'Info'
            $chocoExtractPath = Join-Path $chocoTempPath 'chocolatey'
            [System.IO.Compression.ZipFile]::ExtractToDirectory($chocoNupkg, $chocoExtractPath)

            # Find and run chocolateyInstall.ps1
            $installScript = Join-Path $chocoExtractPath 'tools\chocolateyInstall.ps1'
            if (-not (Test-Path $installScript)) {
                throw "chocolateyInstall.ps1 not found in package"
            }

            # Set environment variable for Chocolatey install path
            $env:ChocolateyInstall = $chocoInstallPath

            # Create Chocolatey directory structure
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.setup_dirs') -Level 'Info'
            $chocoLibPath = Join-Path $chocoInstallPath 'lib'
            $chocoBinPath = Join-Path $chocoInstallPath 'bin'
            New-Item -ItemType Directory -Path $chocoLibPath -Force | Out-Null
            New-Item -ItemType Directory -Path $chocoBinPath -Force | Out-Null

            # Copy chocolatey package to lib folder
            $chocoLibChoco = Join-Path $chocoLibPath 'chocolatey'
            if (Test-Path $chocoLibChoco) {
                Remove-Item $chocoLibChoco -Recurse -Force
            }
            Copy-Item $chocoExtractPath -Destination $chocoLibChoco -Recurse

            # Run the chocolatey install script
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.running_setup') -Level 'Info'
            & $installScript

            # Add Chocolatey to PATH if not already there
            $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
            if ($machinePath -notlike "*$chocoBinPath*") {
                [Environment]::SetEnvironmentVariable('Path', "$machinePath;$chocoBinPath", 'Machine')
            }

            # Update current session PATH
            $env:Path = "$env:Path;$chocoBinPath"

        } finally {
            # Clean up temp directory
            if (Test-Path $chocoTempPath) {
                Remove-Item $chocoTempPath -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        # Refresh environment after installation
        Invoke-EnvironmentRefresh

    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.install_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        throw "Chocolatey installation failed: $($_.Exception.Message)"
    }

    if (Test-CommandAvailable -Name 'choco') {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.installed_success') -Level 'Success'
        return $true
    }

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.chocolatey.install_failed_unknown') -Level 'Error'
    return $false
}

# === POWERSHELL 7 INSTALLATION ===

function Install-PowerShell7 {
    <#
    .SYNOPSIS
        Installs or upgrades to PowerShell 7.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $currentVersion = $PSVersionTable.PSVersion

    if ($currentVersion.Major -ge 7 -and -not $Force) {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.already_installed' -Parameters @{ Version = $currentVersion }) -Level 'Info'
        return $true
    }

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.installing') -Level 'Info'

    # Try Winget first
    if (Test-CommandAvailable -Name 'winget') {
        try {
            $arguments = @('install', '--id', 'Microsoft.PowerShell', '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }

            if (Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment) {
                Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.completed') -Level 'Success'
                Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.restart_required') -Level 'Warning'
                return $true
            }
        } catch {
            Write-Verbose "Winget installation failed: $($_.Exception.Message)"
        }
    }

    # Try Chocolatey as fallback
    if (Test-CommandAvailable -Name 'choco') {
        try {
            $arguments = @('install', 'powershell-core', '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }

            if (Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment) {
                Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.completed') -Level 'Success'
                Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.restart_required') -Level 'Warning'
                return $true
            }
        } catch {
            Write-Verbose "Chocolatey installation failed: $($_.Exception.Message)"
        }
    }

    # Direct download as last resort
    try {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.direct_download') -Level 'Info'

        $sources = Get-DownloadSources
        $downloadUrl = $sources.prerequisites.powershell7.downloadUrl
        $expectedHash = $sources.prerequisites.powershell7.sha256
        $fileName = [System.IO.Path]::GetFileName($downloadUrl)
        $tempPath = Join-Path -Path $env:TEMP -ChildPath $fileName

        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing

        # SHA256 checksum validation
        if ($expectedHash -and $expectedHash -ne 'SKIP_VALIDATION') {
            Write-Status -Message 'Validating SHA256 checksum...' -Level 'Info'
            $actualHash = (Get-FileHash -Path $tempPath -Algorithm SHA256).Hash
            if ($actualHash -ne $expectedHash) {
                Write-Status -Message "SHA256 verification failed! Expected: $expectedHash, Got: $actualHash" -Level 'Error'
                Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
                throw "SHA256 checksum mismatch - download may be corrupted or tampered"
            }
            Write-Status -Message 'SHA256 checksum verified successfully' -Level 'Success'
        }

        $arguments = @('/i', "`"$tempPath`"", '/qn', '/norestart', 'ADD_PATH=1', 'ENABLE_MU=1')
        if (Invoke-ExternalProcess -FilePath 'msiexec.exe' -ArgumentList $arguments -RefreshEnvironment) {
            Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.completed') -Level 'Success'
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.restart_required') -Level 'Warning'
            return $true
        }

        Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Verbose "Direct download failed: $($_.Exception.Message)"
    }

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.powershell.install_failed_unknown') -Level 'Error'
    return $false
}

# === .NET RUNTIME INSTALLATION ===

function Install-DotNetRuntime {
    <#
    .SYNOPSIS
        Installs .NET runtimes (Framework 4.8.1, .NET 6, .NET 8).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $runtimes = @(
        @{
            Name = '.NET Framework 4.8.1'
            WingetId = 'Microsoft.DotNet.Framework.DeveloperPack_4'
            ChocolateyId = 'dotnetfx'
            Type = 'Framework'
        },
        @{
            Name = '.NET 6 Runtime'
            WingetId = 'Microsoft.DotNet.Runtime.6'
            ChocolateyId = 'dotnet-6.0-runtime'
            Type = 'Core'
        },
        @{
            Name = '.NET 6 Desktop Runtime'
            WingetId = 'Microsoft.DotNet.DesktopRuntime.6'
            ChocolateyId = 'dotnet-6.0-desktopruntime'
            Type = 'Core'
        },
        @{
            Name = '.NET 8 Runtime'
            WingetId = 'Microsoft.DotNet.Runtime.8'
            ChocolateyId = 'dotnet-8.0-runtime'
            Type = 'Core'
        },
        @{
            Name = '.NET 8 Desktop Runtime'
            WingetId = 'Microsoft.DotNet.DesktopRuntime.8'
            ChocolateyId = 'dotnet-8.0-desktopruntime'
            Type = 'Core'
        },
        @{
            Name = '.NET 9 Runtime'
            WingetId = 'Microsoft.DotNet.Runtime.9'
            ChocolateyId = 'dotnet-9.0-runtime'
            Type = 'Core'
        },
        @{
            Name = '.NET 9 Desktop Runtime'
            WingetId = 'Microsoft.DotNet.DesktopRuntime.9'
            ChocolateyId = 'dotnet-9.0-desktopruntime'
            Type = 'Core'
        }
    )

    $allSucceeded = $true

    foreach ($runtime in $runtimes) {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.dotnet.installing' -Parameters @{ Name = $runtime.Name }) -Level 'Info'
        $installed = $false

        # Try Winget first
        if (Test-CommandAvailable -Name 'winget') {
            $arguments = @('install', '--id', $runtime.WingetId, '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }

            $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment
        }

        # Try Chocolatey as fallback
        if ((-not $installed) -and (Test-CommandAvailable -Name 'choco')) {
            $arguments = @('install', $runtime.ChocolateyId, '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }

            $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment
        }

        if ($installed) {
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.dotnet.installed_success' -Parameters @{ Name = $runtime.Name }) -Level 'Success'
        } else {
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.dotnet.install_failed' -Parameters @{ Name = $runtime.Name }) -Level 'Warning'
            $allSucceeded = $false
        }
    }

    # Final environment refresh after all runtimes
    Invoke-EnvironmentRefresh

    return $allSucceeded
}

# === VISUAL C++ REDISTRIBUTABLES ===

function Install-VCRedist {
    <#
    .SYNOPSIS
        Installs Visual C++ redistributables (2015-2022, all architectures).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $vcRedists = @(
        @{
            Name = 'Visual C++ 2015-2022 x64'
            ChocolateyId = 'vcredist2015'
            WingetId = 'Microsoft.VCRedist.2015+.x64'
        },
        @{
            Name = 'Visual C++ 2015-2022 x86'
            ChocolateyId = 'vcredist2015'
            WingetId = 'Microsoft.VCRedist.2015+.x86'
        }
    )

    $allSucceeded = $true

    foreach ($vcRedist in $vcRedists) {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.vcredist.installing' -Parameters @{ Name = $vcRedist.Name }) -Level 'Info'
        $installed = $false

        # Try Chocolatey first (more reliable for VCRedist)
        if (Test-CommandAvailable -Name 'choco') {
            # FIXED: Removed malformed --params argument
            $arguments = @('install', $vcRedist.ChocolateyId, '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }

            $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments
        }

        # Try Winget as fallback
        if ((-not $installed) -and (Test-CommandAvailable -Name 'winget')) {
            $arguments = @('install', '--id', $vcRedist.WingetId, '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }

            $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments
        }

        if ($installed) {
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.vcredist.installed_success' -Parameters @{ Name = $vcRedist.Name }) -Level 'Success'
        } else {
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.vcredist.install_failed' -Parameters @{ Name = $vcRedist.Name }) -Level 'Warning'
            $allSucceeded = $false
        }
    }

    return $allSucceeded
}

# === JAVA RUNTIME INSTALLATION ===

function Install-JavaRuntime {
    <#
    .SYNOPSIS
        Installs Java runtime environment (Temurin JRE 21).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    # Check if Java is already installed
    try {
        & java -version 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0 -and -not $Force) {
            Write-Status -Message (Get-LocalizedString -Key 'prerequisites.java.already_installed') -Level 'Info'
            $javaVersion = & java -version 2>&1 | Select-Object -First 1
            Write-Verbose "Detected: $javaVersion"
            return $true
        }
    } catch {
        # Silently ignore Java detection errors - continue with installation
        Write-Verbose "Java detection failed, continuing with installation: $($_.Exception.Message)"
    }

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.java.installing') -Level 'Info'
    $installed = $false

    # Try Winget first
    if (Test-CommandAvailable -Name 'winget') {
        $arguments = @('install', '--id', 'EclipseAdoptium.Temurin.21.JRE', '--silent', '--accept-package-agreements', '--accept-source-agreements')
        if ($Force) { $arguments += '--force' }

        $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment
    }

    # Try Chocolatey as fallback
    if ((-not $installed) -and (Test-CommandAvailable -Name 'choco')) {
        $arguments = @('install', 'temurin21jre', '-y', '--no-progress')
        if ($Force) { $arguments += '--force' }

        $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment
    }

    if ($installed) {
        # Refresh environment to get JAVA_HOME
        Invoke-EnvironmentRefresh

        # Explicitly add JAVA_HOME\bin to PATH if JAVA_HOME is set
        $javaHome = [System.Environment]::GetEnvironmentVariable('JAVA_HOME', 'Machine')
        if (-not $javaHome) {
            $javaHome = [System.Environment]::GetEnvironmentVariable('JAVA_HOME', 'User')
        }

        if ($javaHome -and (Test-Path $javaHome)) {
            $javaBinPath = Join-Path $javaHome 'bin'
            if ((Test-Path $javaBinPath) -and ($env:PATH -notlike "*$javaBinPath*")) {
                $env:PATH = "$javaBinPath;$env:PATH"
                [System.Environment]::SetEnvironmentVariable('PATH', $env:PATH, 'Process')
                Write-Verbose "Added Java bin to PATH: $javaBinPath"
            }
            # Also set JAVA_HOME in process environment
            $env:JAVA_HOME = $javaHome
        }

        # Verify installation
        try {
            & java -version 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Status -Message (Get-LocalizedString -Key 'prerequisites.java.installed_verified') -Level 'Success'
                return $true
            }
        } catch {
            Write-Verbose "Java verification failed"
        }
    }

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.java.install_failed') -Level 'Warning'
    return $false
}

# === PREREQUISITES TEST ===

function Test-Prerequisites {
    <#
    .SYNOPSIS
        Tests if prerequisites are installed and returns detailed status.

    .OUTPUTS
        [System.Collections.Specialized.OrderedDictionary] Dictionary of prerequisites status
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param()

    # Refresh environment before testing
    Invoke-EnvironmentRefresh

    $results = [ordered]@{
        Chocolatey = @{
            Installed = Test-CommandAvailable -Name 'choco'
            Version = if (Test-CommandAvailable -Name 'choco') {
                (& choco --version 2>&1 | Select-Object -First 1)
            } else {
                'Not installed'
            }
        }
        PowerShell7 = @{
            Installed = ($PSVersionTable.PSVersion.Major -ge 7)
            Version = $PSVersionTable.PSVersion.ToString()
        }
        Winget = @{
            Installed = Test-CommandAvailable -Name 'winget'
            Version = if (Test-CommandAvailable -Name 'winget') {
                try { (& winget --version 2>&1) } catch { 'Unknown' }
            } else {
                'Not installed'
            }
        }
        DotNet = @{
            Installed = $false
            Version = 'Not found'
        }
        DotNetFramework = @{
            Installed = $false
            Version = 'Not found'
        }
        Java = @{
            Installed = $false
            Version = 'Not found'
        }
        VCRedist = @{
            Installed = $false
            Version = 'Unknown'
        }
    }

    # Check .NET Core
    try {
        $dotnetInfo = & dotnet --list-runtimes 2>&1 | Out-String
        $results['DotNet'].Installed = $true
        $results['DotNet'].Version = $dotnetInfo.Trim()
    } catch {
        Write-Verbose ".NET Core not detected"
    }

    # Check .NET Framework
    try {
        $fxVersion = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop
        if ($fxVersion.Release) {
            $results['DotNetFramework'].Installed = $true
            $results['DotNetFramework'].Version = "4.8.1 (Release: $($fxVersion.Release))"
        }
    } catch {
        Write-Verbose ".NET Framework version not detected"
    }

    # Check Java
    try {
        $javaVersion = & java -version 2>&1 | Select-Object -First 1
        if ($LASTEXITCODE -eq 0) {
            $results['Java'].Installed = $true
            $results['Java'].Version = $javaVersion
        }
    } catch {
        Write-Verbose "Java not detected"
    }

    # Check VC++ Redist
    $vcRedistKeys = @(
        'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
    )

    foreach ($key in $vcRedistKeys) {
        if (Test-Path $key) {
            $vcInfo = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            if ($vcInfo) {
                $results['VCRedist'].Installed = $true
                $results['VCRedist'].Version = "2015-2022 ($($vcInfo.Version))"
                break
            }
        }
    }

    return $results
}

# === MAIN INSTALLATION WORKFLOW ===

function Start-PrerequisitesInstallation {
    <#
    .SYNOPSIS
        Installs all system prerequisites with automatic environment refresh.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.starting') -Level 'Info'

    try {
        # Install package managers first
        Install-Chocolatey -Force:$Force | Out-Null
        Invoke-EnvironmentRefresh

        # Install PowerShell 7
        Install-PowerShell7 -Force:$Force | Out-Null

        # Install runtimes
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.installing_runtimes') -Level 'Info'
        Install-DotNetRuntime -Force:$Force | Out-Null
        Install-VCRedist -Force:$Force | Out-Null
        Install-JavaRuntime -Force:$Force | Out-Null

        # Final environment refresh
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.final_refresh') -Level 'Info'
        Invoke-EnvironmentRefresh

        # Test and report results
        $results = Test-Prerequisites

        Write-Host ""
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.summary_title') -Level 'Info'

        foreach ($key in $results.Keys) {
            $status = if ($results[$key].Installed) { '[OK]' } else { '[MISSING]' }
            $statusLevel = if ($results[$key].Installed) { 'Success' } else { 'Warning' }
            Write-Status -Message "$status $key : $($results[$key].Version)" -Level $statusLevel
        }

        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.completed') -Level 'Success'

        return $results
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'prerequisites.workflow.failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        throw
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Install-Chocolatey',
    'Install-PowerShell7',
    'Install-DotNetRuntime',
    'Install-VCRedist',
    'Install-JavaRuntime',
    'Test-Prerequisites',
    'Start-PrerequisitesInstallation',
    'Update-EnvironmentPath',
    'Invoke-EnvironmentRefresh'
)
