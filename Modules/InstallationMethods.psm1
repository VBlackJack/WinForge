<#
.SYNOPSIS
    Win11Forge - Installation Methods Module v3.1.4

.DESCRIPTION
    Individual installation method implementations:
    - Install-ViaWinget: Winget package installation with retry logic
    - Install-ViaChocolatey: Chocolatey package installation with retry logic
    - Install-ViaStore: Microsoft Store app installation
    - Install-ViaDirectDownload: Direct URL download and installation
    - Install-WindowsFeature: Windows Optional Feature enablement
    - Install-WindowsCapability: Windows Capability installation

.NOTES
    Author: Julien Bombled
    Version: 3.5.0

    Changelog v3.1.4:
    - Extracted from InstallationEngine.psm1 for modularity
    - Shared installation logic for sequential and parallel installations
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

# Import ApplicationDetection for version detection
$script:ApplicationDetectionPath = Join-Path $script:ModuleRoot 'ApplicationDetection.psm1'
if (-not (Get-Command -Name Get-InstalledAppVersion -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:ApplicationDetectionPath) {
        Import-Module -Name $script:ApplicationDetectionPath -Force
    }
}

# === TIMEOUT CONFIGURATION ===
# Import TimeoutSettings module for centralized timeout configuration
$script:TimeoutSettingsPath = Join-Path $script:RepositoryRoot 'Core\TimeoutSettings.psm1'
if (Test-Path -Path $script:TimeoutSettingsPath) {
    Import-Module -Name $script:TimeoutSettingsPath -Force -ErrorAction SilentlyContinue
}

# Helper function to get installation timeout (uses config or defaults)
function script:Get-ConfiguredInstallTimeout {
    param([string]$AppName = '')

    if (Get-Command -Name Get-InstallationTimeout -ErrorAction SilentlyContinue) {
        return Get-InstallationTimeout -AppName $AppName
    }

    # Fallback defaults if TimeoutSettings module not available
    if ($AppName -match 'Office|Microsoft 365|Word|Excel|PowerPoint|Outlook') {
        return 2700  # 45 minutes for Office
    }
    return 1800  # 30 minutes default
}

# === PERFORMANCE OPTIMIZATION: CACHED CONFIGURATION ===

# Cached download sources configuration (session-scoped)
$script:DownloadSourcesCache = $null
$script:DownloadSourcesCacheTime = $null

# Cached trusted domains as HashSet for O(1) lookup
$script:TrustedDomainsHashSet = $null

function script:Get-CachedDownloadSources {
    <#
    .SYNOPSIS
        Gets cached download sources configuration.
    .DESCRIPTION
        Loads Config/download-sources.json once and caches for session.
    #>
    [CmdletBinding()]
    param()

    if ($null -ne $script:DownloadSourcesCache) {
        return $script:DownloadSourcesCache
    }

    $configPath = Join-Path $script:RepositoryRoot 'Config\download-sources.json'
    if (Test-Path -Path $configPath -ErrorAction SilentlyContinue) {
        try {
            $script:DownloadSourcesCache = Get-Content -Path $configPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            $script:DownloadSourcesCacheTime = Get-Date
            Write-Verbose "Cached download-sources.json configuration"
        } catch {
            Write-Verbose "Could not load download sources config: $($_.Exception.Message)"
            $script:DownloadSourcesCache = $null
        }
    }

    return $script:DownloadSourcesCache
}

function script:Get-TrustedDomainsHashSet {
    <#
    .SYNOPSIS
        Gets trusted domains as HashSet for O(1) lookup performance.
    .DESCRIPTION
        Builds and caches a HashSet of trusted domains for fast validation.
    #>
    [CmdletBinding()]
    param()

    if ($null -ne $script:TrustedDomainsHashSet) {
        return $script:TrustedDomainsHashSet
    }

    # Load from config
    $config = Get-CachedDownloadSources
    $configTrustedDomains = @()
    if ($config -and $config.trustedDomains -and $config.trustedDomains.domains) {
        $configTrustedDomains = @($config.trustedDomains.domains)
    }

    # Fallback whitelist (common trusted CDNs and vendors)
    $fallbackDomains = @(
        # Microsoft ecosystem
        'microsoft.com', 'windows.com', 'windowsupdate.com', 'azure.com', 'azureedge.net',
        'office.com', 'visualstudio.com', 'visualstudio.microsoft.com',
        # Code hosting
        'github.com', 'githubusercontent.com', 'githubassets.com', 'gitlab.com',
        # CDNs
        'akamai.net', 'akamaized.net', 'cloudflare.com', 'cloudfront.net',
        'fastly.net', 'jsdelivr.net', 'amazonaws.com', 'steamstatic.com',
        # Common vendors
        'chocolatey.org', 'community.chocolatey.org'
    )

    # Merge and deduplicate
    $allDomains = @()
    if ($configTrustedDomains.Count -gt 0) {
        $allDomains += $configTrustedDomains
    }
    $allDomains += $fallbackDomains

    # Build HashSet with case-insensitive comparison for O(1) lookup
    $script:TrustedDomainsHashSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase
    )

    foreach ($domain in $allDomains) {
        $null = $script:TrustedDomainsHashSet.Add($domain.ToLowerInvariant())
    }

    Write-Verbose "Built trusted domains HashSet with $($script:TrustedDomainsHashSet.Count) entries"
    return $script:TrustedDomainsHashSet
}

function script:Test-TrustedDomain {
    <#
    .SYNOPSIS
        Fast O(1) trusted domain validation using HashSet.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Host
    )

    $trustedDomains = Get-TrustedDomainsHashSet
    $hostLower = $Host.ToLowerInvariant()

    # Exact match
    if ($trustedDomains.Contains($hostLower)) {
        return $true
    }

    # Subdomain match - check parent domains
    $parts = $hostLower.Split('.')
    for ($i = 1; $i -lt $parts.Count; $i++) {
        $parentDomain = ($parts[$i..($parts.Count - 1)]) -join '.'
        if ($trustedDomains.Contains($parentDomain)) {
            return $true
        }
    }

    return $false
}

# === SECURITY FUNCTIONS ===

function Test-SafeExtractPath {
    <#
    .SYNOPSIS
        Validates extracted file paths to prevent Zip Slip attacks.
    .DESCRIPTION
        Ensures that extracted file paths don't escape the target directory
        through path traversal attacks (e.g., ../../../etc/passwd).
    .PARAMETER ExtractedPath
        The full path of an extracted file.
    .PARAMETER TargetDirectory
        The intended extraction directory.
    .OUTPUTS
        Boolean indicating if the path is safe.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$ExtractedPath,

        [Parameter(Mandatory)]
        [string]$TargetDirectory
    )

    try {
        # Resolve both paths to their absolute canonical forms
        $canonicalTarget = [System.IO.Path]::GetFullPath($TargetDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
        $canonicalExtracted = [System.IO.Path]::GetFullPath($ExtractedPath)

        # Verify the extracted path starts with the target directory
        if (-not $canonicalExtracted.StartsWith($canonicalTarget + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Status -Message "Security: Blocked path traversal attempt: $ExtractedPath" -Level 'Warning'
            return $false
        }

        # Additional check for symbolic links (Windows)
        if ([System.IO.File]::Exists($ExtractedPath)) {
            $fileInfo = [System.IO.FileInfo]::new($ExtractedPath)
            if ($fileInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                Write-Status -Message "Security: Blocked symbolic link in archive: $ExtractedPath" -Level 'Warning'
                return $false
            }
        }

        return $true
    } catch {
        Write-Status -Message "Security: Path validation error: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

function Expand-ArchiveSafe {
    <#
    .SYNOPSIS
        Safely extracts a ZIP archive with Zip Slip protection.
    .DESCRIPTION
        Extracts files while validating each extracted path to prevent
        path traversal attacks. Blocks dangerous file types by default.
    .PARAMETER Path
        Path to the ZIP archive.
    .PARAMETER DestinationPath
        Target extraction directory.
    .PARAMETER AllowDangerousExtensions
        If specified, allows extraction of potentially dangerous file types.
        WARNING: Use only for trusted archives.
    .OUTPUTS
        Boolean indicating successful extraction.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$DestinationPath,

        [Parameter()]
        [switch]$AllowDangerousExtensions
    )

    # Security: List of extensions that can execute code
    $dangerousExtensions = @('.ps1', '.psm1', '.psd1', '.bat', '.cmd', '.vbs', '.js', '.dll', '.exe', '.msi', '.scr', '.hta')

    try {
        # Create destination if not exists
        if (-not (Test-Path $DestinationPath)) {
            New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
        }

        $canonicalDest = [System.IO.Path]::GetFullPath($DestinationPath)

        # Use .NET ZipFile for entry-by-entry validation
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop

        $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
        try {
            # Security: PRE-EXTRACTION VALIDATION - Check ALL entries before extracting ANY
            $entriesToExtract = @()

            foreach ($entry in $archive.Entries) {
                # Skip directories
                if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) {
                    continue
                }

                # Security: Check for symlink/reparse point indicators in external attributes
                # Unix symlinks have mode 0xA000 in upper 16 bits of ExternalAttributes
                $unixMode = ($entry.ExternalAttributes -shr 16) -band 0xFFFF
                $isSymlink = ($unixMode -band 0xA000) -eq 0xA000
                if ($isSymlink) {
                    Write-Status -Message "Security: Symlink detected in archive (blocked): $($entry.FullName)" -Level 'Error'
                    return $false
                }

                # Calculate intended extraction path
                $entryPath = Join-Path $canonicalDest $entry.FullName
                $entryFullPath = [System.IO.Path]::GetFullPath($entryPath)

                # Validate path doesn't escape destination (Zip Slip prevention)
                if (-not $entryFullPath.StartsWith($canonicalDest + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                    Write-Status -Message "Security: Zip Slip attack detected in entry: $($entry.FullName)" -Level 'Error'
                    return $false
                }

                # Check for dangerous file extensions
                $extension = [System.IO.Path]::GetExtension($entry.FullName).ToLower()
                if ($extension -in $dangerousExtensions) {
                    if (-not $AllowDangerousExtensions) {
                        Write-Status -Message "Security: Blocked dangerous file type in archive: $($entry.FullName) (use -AllowDangerousExtensions to override)" -Level 'Error'
                        return $false
                    }
                    Write-Status -Message "Security: Warning - extracting dangerous file type: $($entry.FullName)" -Level 'Warning'
                }

                # Entry passed all security checks - add to extraction list
                $entriesToExtract += @{ Entry = $entry; DestPath = $entryFullPath }
            }

            # All entries validated - proceed with extraction
            foreach ($item in $entriesToExtract) {
                $entryDir = [System.IO.Path]::GetDirectoryName($item.DestPath)
                if (-not (Test-Path $entryDir)) {
                    New-Item -Path $entryDir -ItemType Directory -Force | Out-Null
                }
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($item.Entry, $item.DestPath, $true)

                # Post-extraction: Verify no symlink was created (defense in depth)
                if (Test-Path $item.DestPath) {
                    $fileAttribs = (Get-Item $item.DestPath -Force).Attributes
                    if ($fileAttribs -band [System.IO.FileAttributes]::ReparsePoint) {
                        Remove-Item $item.DestPath -Force -ErrorAction SilentlyContinue
                        Write-Status -Message "Security: Removed symlink created during extraction: $($item.DestPath)" -Level 'Warning'
                    }
                }
            }
            return $true
        } finally {
            $archive.Dispose()
        }
    } catch {
        Write-Status -Message "Archive extraction failed: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

# === HELPER FUNCTIONS ===

function Test-InternalAddress {
    <#
    .SYNOPSIS
        Tests if a host resolves to an internal/private IP address.
    .DESCRIPTION
        Detects localhost, private IP ranges (RFC1918), and link-local addresses
        to prevent SSRF attacks targeting internal services.
    .PARAMETER Host
        The hostname or IP address to check.
    .OUTPUTS
        Boolean indicating if address is internal.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Host
    )

    # Block localhost variations
    $localhostPatterns = @('localhost', 'localhost.localdomain', '127.0.0.1', '::1', '0.0.0.0')
    if ($Host.ToLower() -in $localhostPatterns -or $Host.ToLower().EndsWith('.local')) {
        return $true
    }

    # Try to resolve the hostname to IP addresses
    try {
        $addresses = [System.Net.Dns]::GetHostAddresses($Host)
    } catch {
        # If resolution fails, allow (will fail at download anyway)
        return $false
    }

    foreach ($addr in $addresses) {
        $bytes = $addr.GetAddressBytes()

        if ($addr.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) {
            # IPv4 checks
            $first = $bytes[0]
            $second = $bytes[1]

            # 127.0.0.0/8 - Loopback
            if ($first -eq 127) { return $true }

            # 10.0.0.0/8 - Private Class A
            if ($first -eq 10) { return $true }

            # 172.16.0.0/12 - Private Class B (172.16.x.x - 172.31.x.x)
            if ($first -eq 172 -and $second -ge 16 -and $second -le 31) { return $true }

            # 192.168.0.0/16 - Private Class C
            if ($first -eq 192 -and $second -eq 168) { return $true }

            # 169.254.0.0/16 - Link-local
            if ($first -eq 169 -and $second -eq 254) { return $true }

            # 0.0.0.0/8 - This network
            if ($first -eq 0) { return $true }
        }
        elseif ($addr.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) {
            # IPv6 checks
            if ($addr.IsIPv6LinkLocal) { return $true }
            if ($addr.IsIPv6SiteLocal) { return $true }

            # ::1 - Loopback
            if ($bytes.Length -eq 16 -and
                $bytes[0..14] -join '' -eq ([byte[]]::new(15) -join '') -and
                $bytes[15] -eq 1) {
                return $true
            }

            # fc00::/7 - Unique local address
            if (($bytes[0] -band 0xFE) -eq 0xFC) { return $true }
        }
    }

    return $false
}

function Test-ValidDownloadUrl {
    <#
    .SYNOPSIS
        Validates download URL for security and format
    .DESCRIPTION
        Validates URL structure, blocks internal/private IP addresses (SSRF protection),
        and checks against trusted domain whitelist.
        Loads trusted domains from Config/download-sources.json if available.
    .PARAMETER Url
        The URL to validate.
    .PARAMETER AllowUntrusted
        If specified, allows non-whitelisted domains (logs warning but returns true).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [switch]$AllowUntrusted
    )

    # Check URL format (must be HTTP/HTTPS)
    if (-not ($Url -match '^https?://')) {
        Write-Status -Message "Invalid URL protocol (must be HTTP/HTTPS): $Url" -Level 'Verbose'
        return $false
    }

    # Validate URI structure
    try {
        $uri = [System.Uri]$Url
        if ($uri.Scheme -notin @('http', 'https')) {
            Write-Status -Message "Invalid URL scheme: $($uri.Scheme)" -Level 'Verbose'
            return $false
        }
    } catch {
        Write-Status -Message "Malformed URL: $Url" -Level 'Verbose'
        return $false
    }

    # SSRF Protection: Block internal/private IP addresses
    if (Test-InternalAddress -Host $uri.Host) {
        Write-Status -Message "Security: Download blocked - internal/private address detected: $($uri.Host)" -Level 'Error'
        return $false
    }

    # Use cached HashSet for O(1) domain validation
    $domainMatched = Test-TrustedDomain -Host $uri.Host

    if (-not $domainMatched) {
        if ($AllowUntrusted) {
            Write-Status -Message "Downloading from non-whitelisted domain (allowed by caller): $($uri.Host)" -Level 'Warning'
            return $true
        } else {
            Write-Status -Message "Download blocked - untrusted domain: $($uri.Host). Add to Config\download-sources.json trustedDomains to allow." -Level 'Warning'
            return $false
        }
    }

    return $true
}

function Start-ProcessWithTimeout {
    <#
    .SYNOPSIS
        Starts a process with timeout protection
    #>
    [CmdletBinding()]
    [OutputType([System.Diagnostics.Process])]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$ArgumentList,

        [Parameter()]
        [int]$TimeoutSeconds = 600,

        [Parameter()]
        [switch]$NoNewWindow,

        [Parameter()]
        [switch]$PassThru
    )

    try {
        $processParams = @{
            FilePath = $FilePath
            NoNewWindow = $NoNewWindow.IsPresent
            PassThru = $true
        }

        if ($ArgumentList) {
            $processParams['ArgumentList'] = $ArgumentList
        }

        $process = Start-Process @processParams

        # Wait for process with timeout
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Write-Status -Message "Process timed out after $TimeoutSeconds seconds - attempting graceful termination" -Level 'Warning'

            # Graceful termination: Try CloseMainWindow first
            $gracefulClosed = $false
            try {
                if ($process.CloseMainWindow()) {
                    # Wait up to 5 seconds for graceful close
                    $gracefulClosed = $process.WaitForExit(5000)
                    if ($gracefulClosed) {
                        Write-Status -Message "Process terminated gracefully via CloseMainWindow" -Level 'Info'
                    }
                }
            } catch {
                Write-Status -Message "CloseMainWindow failed: $($_.Exception.Message)" -Level 'Verbose'
            }

            # Forceful termination if graceful failed
            if (-not $gracefulClosed -and -not $process.HasExited) {
                Write-Status -Message "Graceful termination failed - forcing process kill" -Level 'Warning'
                try {
                    $process.Kill()
                    # Wait for process to actually terminate
                    $process.WaitForExit(3000)
                } catch {
                    Write-Status -Message "Process kill failed: $($_.Exception.Message)" -Level 'Error'
                }

                # Kill child processes to prevent orphans
                try {
                    Get-CimInstance -ClassName Win32_Process |
                        Where-Object { $_.ParentProcessId -eq $process.Id } |
                        ForEach-Object {
                            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                        }
                } catch {
                    Write-Status -Message "Child process cleanup failed: $($_.Exception.Message)" -Level 'Verbose'
                }
            }

            throw "Process execution timed out after $TimeoutSeconds seconds"
        }

        # Refresh process object to ensure ExitCode is available
        # This is required because Start-Process -PassThru may not update ExitCode after WaitForExit
        $process.Refresh()

        # Ensure we have a valid exit code (fallback to -1 if null)
        $exitCode = if ($null -ne $process.ExitCode) { $process.ExitCode } else { -1 }

        if ($PassThru) {
            # Create a wrapper object with guaranteed ExitCode
            return [PSCustomObject]@{
                ExitCode = $exitCode
                Id = $process.Id
                HasExited = $process.HasExited
            }
        }

        return $exitCode
    } catch {
        Write-Status -Message "Process execution failed: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

function Invoke-FileDownloadWithProgress {
    <#
    .SYNOPSIS
        Downloads file with progress reporting, streaming, and optional checksum validation
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Url,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$OutputPath,

        [Parameter()]
        [ValidateRange(1, 7200)]
        [int]$TimeoutSeconds = 1800,  # 30 minutes for large files

        [Parameter()]
        [string]$ExpectedSHA256 = $null  # Optional SHA256 checksum for validation
    )

    # Security: Validate URL format before any processing
    # Block command injection characters that could be dangerous in URLs passed to external tools
    $dangerousChars = @(';', '&', '|', '`', '$', '(', ')', '<', '>', '"', "'", "`n", "`r", [char]0)
    foreach ($char in $dangerousChars) {
        if ($Url.Contains($char)) {
            Write-Status -Message "Security: URL contains potentially dangerous character: $char" -Level 'Error'
            return $false
        }
    }

    # Validate URL structure
    try {
        $uri = [System.Uri]$Url
        if ($uri.Scheme -notin @('http', 'https')) {
            Write-Status -Message "Security: Invalid URL scheme (must be http or https): $($uri.Scheme)" -Level 'Error'
            return $false
        }
    } catch {
        Write-Status -Message "Security: Malformed URL: $Url" -Level 'Error'
        return $false
    }

    # Ensure TLS 1.2 is enabled (required by many modern servers)
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $downloadSuccess = $false

    # Method 1: Try Invoke-WebRequest (modern, secure, better redirect handling)
    try {
        Write-Output "[INFO] Attempting download via Invoke-WebRequest..."
        Write-Status -Message "Attempting download via Invoke-WebRequest..." -Level 'Verbose'

        # Disable progress bar for faster download
        $ProgressPreference = 'SilentlyContinue'

        # Use browser-like headers to avoid blocks
        $headers = @{
            'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
            'Accept' = 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8'
            'Accept-Language' = 'en-US,en;q=0.5'
        }

        Invoke-WebRequest -Uri $Url -OutFile $OutputPath -Headers $headers -UseBasicParsing -ErrorAction Stop

        if ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
            $downloadSuccess = $true
            Write-Output "[SUCCESS] Download completed"
            Write-Status -Message "Invoke-WebRequest download succeeded" -Level 'Verbose'
        }
    } catch {
        Write-Output "[WARNING] Invoke-WebRequest download failed, trying fallback method..."
        Write-Status -Message "Invoke-WebRequest download failed: $($_.Exception.Message)" -Level 'Verbose'
        # Clean up any partial file
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
    }

    # Method 2: Fallback to Invoke-WebRequest (better redirect handling)
    if (-not $downloadSuccess) {
        # Clean up any partial file from previous attempt
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            Write-Output "[INFO] Attempting download via Invoke-WebRequest..."
            Write-Status -Message "Attempting download via Invoke-WebRequest (handles redirects)..." -Level 'Verbose'
            $ProgressPreference = 'SilentlyContinue'  # Disable progress bar for speed
            $headers = @{
                "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                "Accept" = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"
            }
            $response = Invoke-WebRequest -Uri $Url -OutFile $OutputPath -UseBasicParsing -TimeoutSec $TimeoutSeconds -Headers $headers -ErrorAction Stop -PassThru

            # Security: Explicitly validate HTTP status code (defensive check)
            if ($response.StatusCode -ge 400) {
                Write-Output "[WARNING] HTTP status code indicates failure: $($response.StatusCode)"
                Write-Status -Message "HTTP error status: $($response.StatusCode)" -Level 'Warning'
                if (Test-Path -Path $OutputPath) {
                    Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
                }
            }
            elseif ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                $downloadSuccess = $true
                Write-Output "[SUCCESS] Download completed (HTTP $($response.StatusCode))"
            }
            else {
                Write-Output "[WARNING] Download completed but file is empty or missing"
            }
        } catch {
            Write-Output "[WARNING] Invoke-WebRequest failed, trying curl..."
            Write-Status -Message "Invoke-WebRequest download failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Method 3: Try curl.exe (built into Windows 10/11, handles redirects well)
    if (-not $downloadSuccess) {
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            $curlPath = [System.IO.Path]::Combine($env:SystemRoot, 'System32', 'curl.exe')
            if (Test-Path $curlPath) {
                Write-Output "[INFO] Attempting download via curl.exe..."
                Write-Status -Message "Attempting download via curl.exe..." -Level 'Verbose'
                # Security: Use Start-Process with ArgumentList array to prevent command injection
                # Each argument is passed separately, preventing shell interpretation
                $curlArgs = @(
                    '-L'                          # Follow redirects
                    '-o', $OutputPath             # Output file
                    '-A', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0'  # User agent
                    '--connect-timeout', '30'     # Connection timeout
                    '--max-time', $TimeoutSeconds.ToString()  # Max total time
                    '--fail'                      # Fail on HTTP errors
                    '--silent'                    # Silent mode
                    '--show-error'                # But show errors
                    $Url                          # The URL (safely passed as single argument)
                )
                $curlProcess = Start-Process -FilePath $curlPath -ArgumentList $curlArgs -NoNewWindow -Wait -PassThru
                if ($curlProcess.ExitCode -eq 0 -and (Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                    $downloadSuccess = $true
                    Write-Output "[SUCCESS] Download completed via curl"
                } else {
                    Write-Output "[WARNING] curl exit code: $($curlProcess.ExitCode)"
                }
            }
        } catch {
            Write-Output "[WARNING] curl.exe download failed: $($_.Exception.Message)"
            Write-Status -Message "curl.exe download failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    # Method 4: Last resort - Start-BitsTransfer (BITS service)
    if (-not $downloadSuccess) {
        if (Test-Path -Path $OutputPath) {
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            Write-Output "[INFO] Attempting download via BITS transfer..."
            Write-Status -Message "Attempting download via BITS transfer..." -Level 'Verbose'
            Start-BitsTransfer -Source $Url -Destination $OutputPath -ErrorAction Stop
            if ((Test-Path -Path $OutputPath) -and (Get-Item -Path $OutputPath).Length -gt 0) {
                $downloadSuccess = $true
                Write-Output "[SUCCESS] Download completed via BITS"
            }
        } catch {
            Write-Output "[ERROR] BITS transfer failed"
            Write-Status -Message "BITS transfer failed: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    if (-not $downloadSuccess) {
        Write-Output "[ERROR] Download failed: All methods exhausted"
        Write-Status -Message "Download failed: All methods exhausted" -Level 'Error'
        return $false
    }

    # Verify file exists and has content
    if (-not (Test-Path -Path $OutputPath)) {
        Write-Status -Message "Download failed: File not found after download" -Level 'Error'
        return $false
    }

    $fileSize = (Get-Item -Path $OutputPath).Length
    if ($fileSize -eq 0) {
        Write-Status -Message "Download failed: Downloaded file is empty" -Level 'Error'
        Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
        return $false
    }

    Write-Status -Message "Download completed ($fileSize bytes)" -Level 'Verbose'

    # SHA256 checksum validation
    if ($ExpectedSHA256) {
        Write-Status -Message "Validating SHA256 checksum..." -Level 'Verbose'
        $fileHash = (Get-FileHash -Path $OutputPath -Algorithm SHA256).Hash

        if ($fileHash -ne $ExpectedSHA256) {
            Write-Status -Message "Checksum validation FAILED! Expected: $ExpectedSHA256, Got: $fileHash" -Level 'Error'
            Write-Status -Message "Removing potentially corrupted file..." -Level 'Warning'
            Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
            return $false
        }

        Write-Status -Message "Checksum validation passed (SHA256: $fileHash)" -Level 'Success'
    }

    return $true
}

function Format-FileSize {
    <#
    .SYNOPSIS
        Formats bytes into human-readable file size.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [long]$Bytes
    )

    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    } elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    } elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    } else {
        return "$Bytes bytes"
    }
}

# === PACKAGE MANAGER INSTALLATION METHODS ===

function Install-ViaWinget {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter()]
        [switch]$Silent,

        [Parameter()]
        [int]$MaxRetries = 3,

        [Parameter()]
        [int]$RetryDelaySeconds = 2
    )

    if (-not (Test-CommandExists -Name 'winget')) {
        Write-Status -Message "Winget not available" -Level 'Verbose'
        return $false
    }

    # Silent installation by default (unless explicitly set to $false)
    $isSilent = -not $PSBoundParameters.ContainsKey('Silent') -or $Silent.IsPresent

    $arguments = @(
        'install',
        '--id', $PackageId,
        '--accept-package-agreements',
        '--accept-source-agreements'
    )

    if ($isSilent) {
        $arguments += '--silent'
    }

    # Retry logic with exponential backoff
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            if ($attempt -eq 1) {
                Write-Output "[INFO] Installing via Winget: $PackageId"
                Write-Status -Message "Installing via Winget: $PackageId" -Level 'Info'
            } else {
                Write-Output "[INFO] Retry $attempt/$MaxRetries for Winget: $PackageId"
                Write-Status -Message "Retry $attempt/$MaxRetries for Winget: $PackageId" -Level 'Info'
            }

            # Execute winget and capture output to detect "already installed" patterns
            $wingetOutput = & winget @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" patterns in output - treat as success
            # Winget may say "already installed", "No available upgrade", "No newer package versions"
            if ($wingetOutput -match 'already installed' -or
                $wingetOutput -match 'No available upgrade' -or
                $wingetOutput -match 'No newer package versions' -or
                $wingetOutput -match 'Successfully installed') {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Exit code 0 = success
            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Exit code -1978334974 = APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_ALREADY_INSTALLED
            if ($exitCode -eq -1978334974) {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Already installed (Winget)$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Already installed (Winget)$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Check for transient network errors (exit codes that might benefit from retry)
            $transientErrors = @(-1978335189, -1978335212)  # Common Winget network errors
            if ($transientErrors -contains $exitCode) {
                if ($attempt -lt $MaxRetries) {
                    $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                    Write-Output "[WARNING] Transient error detected (exit code: $exitCode), retrying in $delay seconds..."
                    Write-Status -Message "Transient error detected (exit code: $exitCode), retrying in $delay seconds..." -Level 'Warning'
                    Start-Sleep -Seconds $delay
                    continue
                }
                # Last attempt with transient error - don't verify (could detect old install), just fail
                Write-Output "[WARNING] Winget installation failed after $MaxRetries attempts (exit code: $exitCode)"
                Write-Status -Message "Winget installation failed after $MaxRetries attempts (exit code: $exitCode)" -Level 'Warning'
                return $false
            }

            # Post-install verification: Check if package is actually installed despite non-zero exit code
            # This handles cases where winget returns unexpected exit codes but installation succeeded
            Write-Output "[INFO] Verifying installation..."
            $verifyResult = & winget list --id $PackageId --accept-source-agreements 2>&1 | Out-String
            if ($verifyResult -match [regex]::Escape($PackageId) -and $verifyResult -notmatch "No installed package") {
                $version = Get-InstalledAppVersion -WingetId $PackageId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Winget$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Winget$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            Write-Output "[WARNING] Winget installation failed (exit code: $exitCode)"
            Write-Status -Message "Winget installation failed (exit code: $exitCode)" -Level 'Warning'
            return $false

        } catch {
            if ($attempt -lt $MaxRetries) {
                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Winget error: $($_.Exception.Message), retrying in $delay seconds..."
                Write-Status -Message "Winget error: $($_.Exception.Message), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            } else {
                Write-Output "[ERROR] Winget installation error after $MaxRetries attempts: $($_.Exception.Message)"
                Write-Status -Message "Winget installation error after $MaxRetries attempts: $($_.Exception.Message)" -Level 'Verbose'
                return $false
            }
        }
    }

    return $false
}

function Install-ViaChocolatey {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageName,

        [Parameter()]
        [int]$MaxRetries = 3,

        [Parameter()]
        [int]$RetryDelaySeconds = 2
    )

    if (-not (Test-CommandExists -Name 'choco')) {
        Write-Status -Message "Chocolatey not available" -Level 'Verbose'
        return $false
    }

    $arguments = @(
        'install', $PackageName,
        '-y',
        '--no-progress',
        '--ignore-checksums'
    )

    # Retry logic with exponential backoff
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            if ($attempt -eq 1) {
                Write-Output "[INFO] Installing via Chocolatey: $PackageName"
                Write-Status -Message "Installing via Chocolatey: $PackageName" -Level 'Info'
            } else {
                Write-Output "[INFO] Retry $attempt/$MaxRetries for Chocolatey: $PackageName"
                Write-Status -Message "Retry $attempt/$MaxRetries for Chocolatey: $PackageName" -Level 'Info'
            }

            # Execute choco and capture output to detect "already installed"
            $chocoOutput = & choco @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" pattern in output - treat as success
            if ($chocoOutput -match 'already installed' -or $chocoOutput -match 'has been installed') {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            # Check for transient network errors (but NOT if already installed)
            $transientErrors = @(1641, 3010, -1)  # Common Chocolatey transient errors (reboot required, network timeout)
            if ($transientErrors -contains $exitCode -and $attempt -lt $MaxRetries) {
                # Before retrying, check if package is already installed
                Write-Output "[INFO] Verifying installation..."
                $chocoList = & choco list --local-only --exact $PackageName 2>&1 | Out-String
                if ($chocoList -match $PackageName -and $chocoList -notmatch "0 packages installed") {
                    $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                    $versionInfo = if ($version) { " v$version" } else { "" }
                    Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo (verified)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                    Write-Status -Message "Installed successfully via Chocolatey$versionInfo (verified)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                    return $true
                }

                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Transient error detected (exit code: $exitCode), retrying in $delay seconds..."
                Write-Status -Message "Transient error detected (exit code: $exitCode), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            }

            # Post-install verification: Check if package is actually installed despite non-zero exit code
            # Chocolatey may return non-zero codes even when installation succeeded
            Write-Output "[INFO] Verifying installation..."
            $chocoList = & choco list --local-only --exact $PackageName 2>&1 | Out-String
            if ($chocoList -match $PackageName -and $chocoList -notmatch "0 packages installed") {
                $version = Get-InstalledAppVersion -ChocolateyId $PackageName
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Chocolatey$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })"
                Write-Status -Message "Installed successfully via Chocolatey$versionInfo (verified post-install)$(if ($attempt -gt 1) { " (attempt $attempt)" })" -Level 'Success'
                return $true
            }

            Write-Output "[WARNING] Chocolatey installation failed (exit code: $exitCode)"
            Write-Status -Message "Chocolatey installation failed (exit code: $exitCode)" -Level 'Verbose'
            return $false

        } catch {
            if ($attempt -lt $MaxRetries) {
                $delay = $RetryDelaySeconds * [Math]::Pow(2, $attempt - 1)  # Exponential backoff
                Write-Output "[WARNING] Chocolatey error: $($_.Exception.Message), retrying in $delay seconds..."
                Write-Status -Message "Chocolatey error: $($_.Exception.Message), retrying in $delay seconds..." -Level 'Warning'
                Start-Sleep -Seconds $delay
                continue
            } else {
                Write-Output "[ERROR] Chocolatey installation error after $MaxRetries attempts: $($_.Exception.Message)"
                Write-Status -Message "Chocolatey installation error after $MaxRetries attempts: $($_.Exception.Message)" -Level 'Verbose'
                return $false
            }
        }
    }

    return $false
}

function Install-ViaStore {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$ProductId
    )

    # Check for Windows Sandbox - Store is unavailable
    if (Test-IsWindowsSandbox) {
        Write-Output "[WARNING] Skipping Store install for $ProductId - Windows Store is unavailable in Sandbox"
        Write-Status -Message "Skipping Store install for $ProductId - Windows Store is unavailable in Sandbox" -Level 'Warning'
        return $false
    }

    try {
        Write-Output "[INFO] Installing via Microsoft Store: $ProductId"
        Write-Status -Message "Installing via Microsoft Store: $ProductId" -Level 'Info'

        if (Test-CommandExists -Name 'winget') {
            $arguments = @(
                'install',
                '--id', $ProductId,
                '--source', 'msstore',
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            # Execute winget and capture output to detect "already installed" patterns
            $storeOutput = & winget @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE

            # Check for "already installed" patterns in output - treat as success
            if ($storeOutput -match 'already installed' -or
                $storeOutput -match 'No available upgrade' -or
                $storeOutput -match 'No newer package versions' -or
                $storeOutput -match 'Successfully installed') {
                $version = Get-InstalledAppVersion -WingetId $ProductId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Microsoft Store$versionInfo"
                Write-Status -Message "Installed successfully via Microsoft Store$versionInfo" -Level 'Success'
                return $true
            }

            if ($exitCode -eq 0) {
                $version = Get-InstalledAppVersion -WingetId $ProductId
                $versionInfo = if ($version) { " v$version" } else { "" }
                Write-Output "[SUCCESS] Installed successfully via Microsoft Store$versionInfo"
                Write-Status -Message "Installed successfully via Microsoft Store$versionInfo" -Level 'Success'
                return $true
            }
        }

        Start-Process "ms-windows-store://pdp/?ProductId=$ProductId"
        Write-Output "[WARNING] Store opened - please complete installation manually"
        Write-Status -Message "Store opened - please complete installation manually" -Level 'Warning'
        return $false

    } catch {
        Write-Output "[ERROR] Store installation error: $($_.Exception.Message)"
        Write-Status -Message "Store installation error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

# === DIRECT DOWNLOAD HELPER FUNCTIONS ===

function Install-MsiPackage {
    <#
    .SYNOPSIS
        Installs an MSI package silently.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath
    )

    Write-Output "[INFO] Installing MSI package..."
    $arguments = @('/i', "`"$InstallerPath`"", '/qn', '/norestart')
    $process = Start-ProcessWithTimeout -FilePath 'msiexec.exe' -ArgumentList $arguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
    if ($process.ExitCode -eq 0) {
        Write-Output "[SUCCESS] MSI package installed successfully"
    } else {
        Write-Output "[WARNING] MSI installer returned exit code: $($process.ExitCode)"
    }
    return ($process.ExitCode -eq 0)
}

function Install-ExePackage {
    <#
    .SYNOPSIS
        Installs an EXE package with custom or auto-detected silent switches.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath,

        [Parameter()]
        [string]$CustomArguments = $null
    )

    if ($CustomArguments) {
        Write-Output "[INFO] Running installer with custom arguments: $CustomArguments"
        Write-Status -Message "Using custom install arguments: $CustomArguments" -Level 'Verbose'
        try {
            $process = Start-ProcessWithTimeout -FilePath $InstallerPath -ArgumentList $CustomArguments -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            if ($process.ExitCode -eq 0) {
                Write-Output "[SUCCESS] Installation completed"
            } else {
                Write-Output "[WARNING] Installer returned exit code: $($process.ExitCode)"
            }
            return ($process.ExitCode -eq 0)
        } catch {
            Write-Output "[ERROR] EXE installation failed: $($_.Exception.Message)"
            Write-Status -Message "EXE installation with custom args failed: $($_.Exception.Message)" -Level 'Verbose'
            return $false
        }
    }

    Write-Output "[INFO] Trying silent installation switches..."
    # Try common silent switches
    $silentSwitches = @('/S', '/SILENT', '/VERYSILENT', '/quiet', '/qn')
    foreach ($switch in $silentSwitches) {
        try {
            $process = Start-ProcessWithTimeout -FilePath $InstallerPath -ArgumentList $switch -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            if ($process.ExitCode -eq 0) {
                Write-Output "[SUCCESS] Installation completed with switch: $switch"
                return $true
            }
        } catch {
            continue
        }
    }

    Write-Output "[WARNING] No silent switch worked for this installer"
    return $false
}

function Install-ZipPackage {
    <#
    .SYNOPSIS
        Extracts and installs a ZIP package (installer or portable).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath,

        [Parameter()]
        [string]$TempDir,

        [Parameter()]
        [string]$CustomArguments = $null,

        [Parameter()]
        [string]$DetectionPath = $null
    )

    Write-Status -Message "Extracting ZIP archive (with Zip Slip protection)" -Level 'Info'
    $extractPath = Join-Path $TempDir "extracted"

    # Use safe extraction with path traversal validation
    $extractResult = Expand-ArchiveSafe -Path $InstallerPath -DestinationPath $extractPath
    if (-not $extractResult) {
        Write-Status -Message "ZIP extraction failed security validation" -Level 'Error'
        return $false
    }

    # Check if ZIP contains an installer (setup.exe/install.exe)
    $setupExe = Get-ChildItem -Path $extractPath -Filter *.exe -Recurse |
        Where-Object { $_.Name -match 'setup|install' } |
        Select-Object -First 1

    if ($setupExe) {
        Write-Status -Message "Executing installer from archive: $($setupExe.Name)" -Level 'Info'
        try {
            $args = if ($CustomArguments) { $CustomArguments } else { '/S' }
            $process = Start-ProcessWithTimeout -FilePath $setupExe.FullName -ArgumentList $args -NoNewWindow -PassThru -TimeoutSeconds $script:DefaultInstallTimeoutSeconds
            return ($process.ExitCode -eq 0)
        } catch {
            Write-Status -Message "ZIP installer execution failed: $($_.Exception.Message)" -Level 'Verbose'
            return $false
        }
    }

    # ZIP contains portable tools - deploy to destination
    Write-Status -Message "No installer found - deploying portable tools" -Level 'Info'

    $destinationPath = $null
    if ($DetectionPath) {
        $destinationPath = Split-Path $DetectionPath -Parent
        Write-Status -Message "Using detection path: $DetectionPath" -Level 'Verbose'
    }

    if (-not $destinationPath) {
        $destinationPath = Join-Path ${env:ProgramFiles} ([System.IO.Path]::GetFileNameWithoutExtension($InstallerPath))
    }

    Write-Status -Message "Deploying to: $destinationPath" -Level 'Info'

    if (-not (Test-Path $destinationPath)) {
        New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
    }

    Copy-Item -Path "$extractPath\*" -Destination $destinationPath -Recurse -Force
    Write-Status -Message "Deployment completed successfully" -Level 'Success'
    return $true
}

function Install-ViaDirectDownload {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [ValidateSet('exe', 'msi', 'zip', 'auto')]
        [string]$InstallerType = 'auto',

        [Parameter()]
        [string]$CustomArguments = $null,

        [Parameter()]
        [string]$DetectionPath = $null,

        [Parameter()]
        [string]$ExpectedSHA256 = $null  # Optional SHA256 checksum
    )

    try {
        # Validate URL before download
        if (-not (Test-ValidDownloadUrl -Url $Url)) {
            Write-Output "[ERROR] Invalid or insecure URL: $Url"
            Write-Status -Message "Invalid or insecure URL: $Url" -Level 'Error'
            return $false
        }

        Write-Output "[INFO] Downloading from: $Url"
        Write-Status -Message "Downloading from: $Url" -Level 'Info'

        $tempDir = Join-Path -Path $env:TEMP -ChildPath "Win11Forge_$([guid]::NewGuid().ToString('N'))"
        New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

        $filename = [System.IO.Path]::GetFileName($Url)
        if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.[a-z]{3,4}$') {
            $filename = "installer_$([guid]::NewGuid().ToString('N')).exe"
        }

        $installerPath = Join-Path -Path $tempDir -ChildPath $filename

        # Use streaming download with optional checksum validation
        $downloadParams = @{
            Url = $Url
            OutputPath = $installerPath
        }

        if ($ExpectedSHA256) {
            $downloadParams['ExpectedSHA256'] = $ExpectedSHA256
            Write-Output "[INFO] Checksum validation enabled (SHA256)"
            Write-Status -Message "Checksum validation enabled (SHA256)" -Level 'Info'
        }

        $downloadSuccess = Invoke-FileDownloadWithProgress @downloadParams

        if (-not $downloadSuccess -or -not (Test-Path -Path $installerPath)) {
            Write-Output "[ERROR] Download failed: File not found or checksum mismatch"
            Write-Status -Message "Download failed: File not found or checksum mismatch" -Level 'Error'
            return $false
        }

        $fileSize = Format-FileSize -Bytes (Get-Item $installerPath).Length
        Write-Output "[INFO] Downloaded: $fileSize"
        Write-Status -Message "Downloaded: $fileSize" -Level 'Info'

        if ($InstallerType -eq 'auto') {
            $InstallerType = switch -Regex ($filename) {
                '\.msi$' { 'msi' }
                '\.zip$' { 'zip' }
                default  { 'exe' }
            }
        }

        Write-Output "[INFO] Running $InstallerType installer..."

        # Install using appropriate method (delegated to helper functions)
        $installed = switch ($InstallerType) {
            'msi' { Install-MsiPackage -InstallerPath $installerPath }
            'exe' { Install-ExePackage -InstallerPath $installerPath -CustomArguments $CustomArguments }
            'zip' { Install-ZipPackage -InstallerPath $installerPath -TempDir $tempDir -CustomArguments $CustomArguments -DetectionPath $DetectionPath }
            default { $false }
        }

        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

        if ($installed) {
            Write-Output "[SUCCESS] Installed successfully via direct download"
            Write-Status -Message "Installed successfully via direct download" -Level 'Success'
        } else {
            Write-Output "[WARNING] Direct installation failed"
            Write-Status -Message "Direct installation failed" -Level 'Verbose'
        }

        return $installed

    } catch {
        Write-Output "[ERROR] Direct download error: $($_.Exception.Message)"
        Write-Status -Message "Direct download error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

# === WINDOWS FEATURE/CAPABILITY INSTALLATION ===

function Install-WindowsFeature {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName
    )

    try {
        Write-Status -Message "Enabling Windows feature: $FeatureName" -Level 'Info'

        $feature = Get-WindowsOptionalFeature -Online -FeatureName $FeatureName -ErrorAction Stop

        if ($feature.State -eq 'Enabled') {
            Write-Status -Message "Feature already enabled" -Level 'Success'
            return $true
        }

        Enable-WindowsOptionalFeature -Online -FeatureName $FeatureName -NoRestart -ErrorAction Stop | Out-Null

        Write-Status -Message "Feature enabled successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to enable feature: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

function Install-WindowsCapability {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName
    )

    try {
        Write-Status -Message "Installing Windows capability: $CapabilityName" -Level 'Info'

        $capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$CapabilityName*" }

        if ($null -eq $capabilities -or $capabilities.Count -eq 0) {
            Write-Status -Message "Capability not found: $CapabilityName" -Level 'Error'
            return $false
        }

        $capability = if ($capabilities -is [array]) { $capabilities[0] } else { $capabilities }

        if ($capability.State -eq 'Installed') {
            Write-Status -Message "Capability already installed" -Level 'Success'
            return $true
        }

        Write-Status -Message "Installing capability: $($capability.Name)" -Level 'Verbose'
        Add-WindowsCapability -Online -Name $capability.Name -ErrorAction Stop | Out-Null

        Write-Status -Message "Capability installed successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to install capability: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

# === ORCHESTRATION HELPERS ===

function Invoke-CustomInstallMethod {
    <#
    .SYNOPSIS
        Handles custom installation methods (WindowsFeature, WindowsCapability).

    .PARAMETER Application
        The application object with InstallMethod property.

    .OUTPUTS
        [hashtable] Installation result
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $false
        Method = $null
        Message = ''
    }

    $installMethod = $Application.InstallMethod

    switch ($installMethod) {
        'WindowsFeature' {
            $installed = Install-WindowsFeature -FeatureName $Application.Detection.Feature
            $result.Method = 'WindowsFeature'
            if ($installed) {
                $result.Success = $true
                $result.Message = "Installed via WindowsFeature"
            } else {
                $result.Message = "Failed to install via WindowsFeature"
            }
        }
        'WindowsCapability' {
            $installed = Install-WindowsCapability -CapabilityName $Application.Detection.Capability
            $result.Method = 'WindowsCapability'
            if ($installed) {
                $result.Success = $true
                $result.Message = "Installed via WindowsCapability"
            } else {
                $result.Message = "Failed to install via WindowsCapability"
            }
        }
        default {
            $result.Message = "Unknown install method: $installMethod"
        }
    }

    return $result
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    # Helper functions
    'Test-ValidDownloadUrl',
    'Start-ProcessWithTimeout',
    'Invoke-FileDownloadWithProgress',
    'Format-FileSize',
    # Package manager methods
    'Install-ViaWinget',
    'Install-ViaChocolatey',
    'Install-ViaStore',
    # Direct download methods
    'Install-MsiPackage',
    'Install-ExePackage',
    'Install-ZipPackage',
    'Install-ViaDirectDownload',
    # Windows feature/capability
    'Install-WindowsFeature',
    'Install-WindowsCapability',
    # Orchestration helpers
    'Invoke-CustomInstallMethod'
)
