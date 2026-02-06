<#
.SYNOPSIS
    Win11Forge - Core Module

.DESCRIPTION
    Provides core functionality for the Win11Forge framework:
    - Logging and status messages
    - Error handling
    - Common utilities
    - Color-coded console output

.NOTES
    Author: Julien Bombled
    This module must be loaded before any other framework modules
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

# === MODULE VARIABLES ===
$script:LogFile = $null
$script:VerboseLogging = $false
$script:LoggingEnabled = $true
$script:StructuredLoggingEnabled = $false

# === STRUCTURED LOGGING IMPORT ===
$script:StructuredLoggingPath = Join-Path (Split-Path -Parent $PSCommandPath) 'StructuredLogging.psm1'
if (Test-Path -Path $script:StructuredLoggingPath) {
    try {
        Import-Module -Name $script:StructuredLoggingPath -Force -ErrorAction Stop
        $script:StructuredLoggingEnabled = $true
    } catch {
        $script:StructuredLoggingEnabled = $false
        Write-Warning "Failed to load StructuredLogging module: $($_.Exception.Message)"
    }
}

# === LOGGING FUNCTIONS ===

function Initialize-Logging {
    <#
    .SYNOPSIS
        Initializes logging system with file output.

    .PARAMETER LogPath
        Path to the log file

    .PARAMETER EnableVerbose
        Enable verbose logging
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$LogPath,

        [Parameter()]
        [switch]$EnableVerbose
    )

    $script:LogFile = $LogPath
    $script:VerboseLogging = $EnableVerbose.IsPresent

    # Ensure directory exists
    $logDirectory = Split-Path -Path $LogPath -Parent
    if (-not (Test-Path -Path $logDirectory)) {
        New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
    }

    # Read version from Config/version.json (single source of truth)
    $versionJsonPath = Join-Path (Split-Path (Split-Path -Parent $PSCommandPath) -Parent) 'Config\version.json'
    $frameworkVersion = 'unknown'
    if (Test-Path -Path $versionJsonPath) {
        try {
            $versionData = Get-Content -Path $versionJsonPath -Raw | ConvertFrom-Json
            $frameworkVersion = $versionData.Version
        } catch {
            Write-Verbose "Failed to read version.json: $($_.Exception.Message)"
        }
    }

    # Initialize log file with header
    $separator = '=' * 80
    $headerLine = Get-LocalizedString -Key 'log.header' -Parameters @{ Version = $frameworkVersion }
    $startedLine = Get-LocalizedString -Key 'log.started' -Parameters @{ DateTime = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') }
    $computerLine = Get-LocalizedString -Key 'log.computer' -Parameters @{ Name = $env:COMPUTERNAME }
    $userLine = Get-LocalizedString -Key 'log.user' -Parameters @{ Name = $env:USERNAME }
    $psLine = Get-LocalizedString -Key 'log.powershell_version' -Parameters @{ Version = "$($PSVersionTable.PSVersion)" }
    $osLine = Get-LocalizedString -Key 'log.os_version' -Parameters @{ OS = [System.Environment]::OSVersion.VersionString }
    $header = @"
$separator
$headerLine
$startedLine
$computerLine
$userLine
$psLine
$osLine
$separator

"@

    $header | Out-File -FilePath $script:LogFile -Encoding UTF8

    Write-Verbose "Logging initialized: $LogPath"
}

function Write-Status {
    <#
    .SYNOPSIS
        Writes a status message with color coding and logging.

    .PARAMETER Message
        The message to display

    .PARAMETER Level
        Message level: Info, Success, Warning, Error, Verbose

    .PARAMETER Category
        Log category for structured logging (e.g., Installation, Cache, System)

    .PARAMETER StructuredData
        Optional hashtable of structured data for JSON logging

    .PARAMETER NoNewline
        Don't add newline after message
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Verbose')]
        [string]$Level = 'Info',

        [Parameter()]
        [string]$Category = 'General',

        [Parameter()]
        [hashtable]$StructuredData,

        [Parameter()]
        [switch]$NoNewline
    )

    # Skip verbose messages if not in verbose mode
    if ($Level -eq 'Verbose' -and -not $script:VerboseLogging) {
        return
    }

    # Prepare log entry
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logEntry = "[$timestamp] [$Level] $Message"

    # Write to log file
    if ($script:LoggingEnabled -and $script:LogFile) {
        $logEntry | Out-File -FilePath $script:LogFile -Append -Encoding UTF8
    }

    # Write to structured log (JSON) if enabled
    if ($script:StructuredLoggingEnabled -and (Get-Command -Name 'Write-StructuredLog' -ErrorAction SilentlyContinue)) {
        $structuredLevel = if ($Level -eq 'Verbose') { 'Debug' } else { $Level }
        Write-StructuredLog -Level $structuredLevel -Category $Category -Message $Message -Data $StructuredData
    }

    # Determine console color
    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Verbose' { 'Gray' }
        default   { 'White' }
    }

    # Write to console
    if ($NoNewline) {
        Write-Host $Message -ForegroundColor $color -NoNewline
    } else {
        Write-Host $Message -ForegroundColor $color
    }
}

function Write-StatusProgress {
    <#
    .SYNOPSIS
        Writes a progress indicator with percentage.

    .PARAMETER Activity
        Activity description

    .PARAMETER Status
        Current status

    .PARAMETER PercentComplete
        Percentage complete (0-100)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Activity,

        [Parameter()]
        [string]$Status = '',

        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$PercentComplete = 0
    )

    Write-Progress -Activity $Activity -Status $Status -PercentComplete $PercentComplete
}

function Write-Section {
    <#
    .SYNOPSIS
        Writes a section header for better log organization.

    .PARAMETER Title
        Section title
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title
    )

    $separator = '=' * 80
    Write-Status -Message '' -Level 'Info'
    Write-Status -Message $separator -Level 'Info'
    Write-Status -Message "  $Title" -Level 'Info'
    Write-Status -Message $separator -Level 'Info'
    Write-Status -Message '' -Level 'Info'
}

# === ERROR HANDLING ===

function Invoke-SafeCommand {
    <#
    .SYNOPSIS
        Executes a script block with error handling and logging.

    .PARAMETER ScriptBlock
        Script block to execute

    .PARAMETER ErrorMessage
        Custom error message prefix

    .PARAMETER ContinueOnError
        Continue execution even if error occurs

    .OUTPUTS
        Returns $true if successful, $false if error
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock,

        [Parameter()]
        [string]$ErrorMessage = 'Operation failed',

        [Parameter()]
        [switch]$ContinueOnError
    )

    try {
        & $ScriptBlock
        return $true
    }
    catch {
        $errorDetail = $_.Exception.Message
        Write-Status -Message "$ErrorMessage : $errorDetail" -Level 'Error'

        if ($script:VerboseLogging) {
            Write-Status -Message "Stack trace: $($_.ScriptStackTrace)" -Level 'Verbose'
        }

        if (-not $ContinueOnError) {
            throw
        }

        return $false
    }
}

# === VALIDATION FUNCTIONS ===

function Test-Administrator {
    <#
    .SYNOPSIS
        Checks if current session has administrator privileges.

    .OUTPUTS
        [bool] True if running as administrator
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-InternetConnection {
    <#
    .SYNOPSIS
        Tests internet connectivity.

    .OUTPUTS
        [bool] True if internet is accessible
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        $connectivityTestHost = Get-NetworkDefault -SettingKey 'ConnectivityTestHost'
        $result = Test-Connection -ComputerName $connectivityTestHost -Count 1 -Quiet -ErrorAction Stop
        return $result
    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'core.internet_check_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Verbose'
        return $false
    }
}

function Assert-Administrator {
    <#
    .SYNOPSIS
        Ensures script is running with administrator privileges or exits.
    #>
    [CmdletBinding()]
    param()

    if (-not (Test-Administrator)) {
        Write-Status -Message (Get-LocalizedString -Key 'core.admin_required') -Level 'Error'
        Write-Status -Message (Get-LocalizedString -Key 'core.admin_run_as') -Level 'Error'
        throw (Get-LocalizedString -Key 'core.admin_required')
    }
}

function Assert-InternetConnection {
    <#
    .SYNOPSIS
        Ensures internet connection is available or exits.
    #>
    [CmdletBinding()]
    param()

    Write-Status -Message (Get-LocalizedString -Key 'core.internet_checking') -Level 'Info'

    if (-not (Test-InternetConnection)) {
        Write-Status -Message (Get-LocalizedString -Key 'core.internet_no_connection') -Level 'Error'
        Write-Status -Message (Get-LocalizedString -Key 'core.internet_ensure_connection') -Level 'Error'
        throw (Get-LocalizedString -Key 'core.internet_no_connection')
    }

    Write-Status -Message (Get-LocalizedString -Key 'core.internet_verified') -Level 'Success'
}

# === UTILITY FUNCTIONS ===

function Get-StringHash {
    <#
    .SYNOPSIS
        Generates a hash from a string.

    .PARAMETER String
        String to hash

    .PARAMETER Algorithm
        Hash algorithm (MD5, SHA1, SHA256)

    .OUTPUTS
        [string] Hexadecimal hash
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$String,

        [Parameter()]
        [ValidateSet('MD5', 'SHA1', 'SHA256')]
        [string]$Algorithm = 'SHA256'
    )

    $stringAsStream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.StreamWriter]::new($stringAsStream)
    $writer.write($String)
    $writer.Flush()
    $stringAsStream.Position = 0

    $hash = Get-FileHash -InputStream $stringAsStream -Algorithm $Algorithm

    $writer.Dispose()
    $stringAsStream.Dispose()

    return $hash.Hash
}

function ConvertTo-SafeFileName {
    <#
    .SYNOPSIS
        Converts a string to a safe filename.

    .PARAMETER String
        String to convert

    .OUTPUTS
        [string] Safe filename
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$String
    )

    $invalidChars = [IO.Path]::GetInvalidFileNameChars()
    $safeName = $String

    foreach ($char in $invalidChars) {
        $safeName = $safeName.Replace($char, '_')
    }

    return $safeName
}

function Test-CommandExists {
    <#
    .SYNOPSIS
        Tests if a command exists in the current session.

    .PARAMETER Name
        Command name to test

    .OUTPUTS
        [bool] True if command exists
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Get-DownloadedFileName {
    <#
    .SYNOPSIS
        Extracts filename from URL or Content-Disposition header.

    .PARAMETER Url
        URL to parse

    .PARAMETER ContentDisposition
        Content-Disposition header value

    .OUTPUTS
        [string] Extracted filename
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [string]$ContentDisposition
    )

    # Try Content-Disposition header first
    if ($ContentDisposition -match 'filename[*]?=(?:UTF-8'')?["]?([^"]+)["]?') {
        return $matches[1]
    }

    # Fall back to URL parsing
    try {
        $uri = [System.Uri]$Url
        $filename = [System.IO.Path]::GetFileName($uri.LocalPath)

        if ([string]::IsNullOrWhiteSpace($filename)) {
            $filename = 'download.exe'
        }

        return $filename
    }
    catch {
        return 'download.exe'
    }
}

function Format-FileSize {
    <#
    .SYNOPSIS
        Formats a file size in bytes to human-readable format.

    .PARAMETER Bytes
        File size in bytes

    .OUTPUTS
        [string] Formatted file size
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [long]$Bytes
    )

    if ($Bytes -ge 1TB) {
        return '{0:N2} TB' -f ($Bytes / 1TB)
    }
    elseif ($Bytes -ge 1GB) {
        return '{0:N2} GB' -f ($Bytes / 1GB)
    }
    elseif ($Bytes -ge 1MB) {
        return '{0:N2} MB' -f ($Bytes / 1MB)
    }
    elseif ($Bytes -ge 1KB) {
        return '{0:N2} KB' -f ($Bytes / 1KB)
    }
    else {
        return '{0} bytes' -f $Bytes
    }
}

function Get-ElapsedTime {
    <#
    .SYNOPSIS
        Calculates elapsed time from a start time.

    .PARAMETER StartTime
        Start time

    .OUTPUTS
        [string] Formatted elapsed time
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [datetime]$StartTime
    )

    $elapsed = (Get-Date) - $StartTime
    return $elapsed.ToString('hh\:mm\:ss')
}

# === CONFIRMATION FUNCTIONS ===

function Confirm-Action {
    <#
    .SYNOPSIS
        Prompts user for confirmation.

    .PARAMETER Message
        Confirmation message

    .PARAMETER DefaultYes
        Default to Yes if user just presses Enter

    .OUTPUTS
        [bool] True if user confirms
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [switch]$DefaultYes
    )

    $prompt = if ($DefaultYes) { '[Y/n]' } else { '[y/N]' }
    Write-Host "$Message $prompt " -NoNewline -ForegroundColor Yellow

    $response = Read-Host

    if ([string]::IsNullOrWhiteSpace($response)) {
        return $DefaultYes.IsPresent
    }

    return $response -match '^[Yy]'
}

# === CLEANUP FUNCTIONS ===

function Clear-TemporaryFiles {
    <#
    .SYNOPSIS
        Cleans up temporary files in specified directory.

    .PARAMETER Path
        Directory path to clean

    .PARAMETER OlderThanDays
        Only remove files older than specified days
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [int]$OlderThanDays = 1
    )

    if (-not (Test-Path -Path $Path)) {
        Write-Status -Message "Path not found: $Path" -Level 'Verbose'
        return
    }

    try {
        $cutoffDate = (Get-Date).AddDays(-$OlderThanDays)
        $files = Get-ChildItem -Path $Path -File -Recurse -ErrorAction SilentlyContinue |
                 Where-Object { $_.LastWriteTime -lt $cutoffDate }

        $count = 0
        foreach ($file in $files) {
            try {
                Remove-Item -Path $file.FullName -Force -ErrorAction Stop
                $count++
            }
            catch {
                Write-Status -Message "Could not remove: $($file.Name)" -Level 'Verbose'
            }
        }

        if ($count -gt 0) {
            Write-Status -Message (Get-LocalizedString -Key 'core.cleaned_temp_files' -Parameters @{ Count = $count }) -Level 'Info'
        }
    }
    catch {
        Write-Status -Message "Error cleaning temporary files: $($_.Exception.Message)" -Level 'Verbose'
    }
}

# === LOCALIZATION INTEGRATION ===

$script:LocalizationModulePath = Join-Path -Path $PSScriptRoot -ChildPath 'Localization.psm1'
if (Test-Path -Path $script:LocalizationModulePath) {
    try {
        Import-Module -Name $script:LocalizationModulePath -Force -ErrorAction Stop
    } catch {
        Write-Warning "Failed to load Localization module: $($_.Exception.Message)"
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    # Logging
    'Initialize-Logging',
    'Write-Status',
    'Write-StatusProgress',
    'Write-Section',

    # Error handling
    'Invoke-SafeCommand',

    # Validation
    'Test-Administrator',
    'Test-InternetConnection',
    'Assert-Administrator',
    'Assert-InternetConnection',

    # Utilities
    'Get-StringHash',
    'ConvertTo-SafeFileName',
    'Test-CommandExists',
    'Get-DownloadedFileName',
    'Format-FileSize',
    'Get-ElapsedTime',

    # Confirmation
    'Confirm-Action',

    # Cleanup
    'Clear-TemporaryFiles'
)
