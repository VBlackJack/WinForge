<#
.SYNOPSIS
    WinForge - Structured Logging v3.7.2

.DESCRIPTION
    Provides JSON-based structured logging for WinForge:
    - Parallel JSON logs alongside text logs
    - Configurable retention and rotation
    - Session-based log grouping
    - Structured data capture for machine parsing
    - Export and query capabilities

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

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\logging-settings.json'
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
$script:LocalizationModulePath = Join-Path $script:ModuleRoot 'Localization.psm1'

if (-not (Get-Command -Name Get-WinForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    } else {
        throw [System.IO.FileNotFoundException]::new("DirectoryConstants module not found: $script:DirectoryConstantsPath")
    }
}

# Import Localization module for i18n support
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === LOGGING STATE ===
$script:LoggingState = @{
    Initialized = $false
    SessionId = $null
    StartTime = $null
    JsonLogPath = $null
    LogBuffer = @()
    BufferSize = 10
    Config = $null
    CurrentRequestId = $null
}

# === DEFAULT CONFIGURATION ===
$script:DefaultConfig = @{
    TextLogging = @{
        Enabled = $true
        RetentionDays = 7
    }
    JsonLogging = @{
        Enabled = $true
        RetentionDays = 30
        Directory = Get-WinForgeDirectory -DirectoryType 'JsonLogs'
        BufferSize = 10
        PrettyPrint = $false
        MaxFileSizeMB = 10  # Rotate log when file exceeds this size
    }
    Categories = @(
        'Installation',
        'Rollback',
        'Cache',
        'Configuration',
        'Detection',
        'System',
        'Plugin',
        'Api',
        'Update',
        'General'
    )
}

# === INITIALIZATION FUNCTIONS ===

function Initialize-StructuredLogging {
    <#
    .SYNOPSIS
        Initializes the structured logging system.

    .DESCRIPTION
        Sets up JSON logging with a new session ID, creates log directories,
        and performs retention cleanup of old logs.

    .PARAMETER SessionId
        Optional custom session ID. If not provided, a new GUID is generated.

    .PARAMETER ConfigOverride
        Optional hashtable to override default configuration.

    .EXAMPLE
        Initialize-StructuredLogging
        Initialize-StructuredLogging -SessionId "Deploy-2026-01-17"
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$SessionId,

        [Parameter()]
        [hashtable]$ConfigOverride
    )

    # Load configuration
    $script:LoggingState.Config = Get-LoggingConfig

    # Apply overrides
    if ($ConfigOverride) {
        foreach ($key in $ConfigOverride.Keys) {
            if ($script:LoggingState.Config.ContainsKey($key)) {
                $script:LoggingState.Config[$key] = $ConfigOverride[$key]
            }
        }
    }

    # Set session ID
    $script:LoggingState.SessionId = if ($PSBoundParameters.ContainsKey('SessionId')) {
        $SessionId
    } else {
        [guid]::NewGuid().ToString()
    }

    $script:LoggingState.StartTime = Get-Date

    # Create JSON log directory
    $jsonDir = $script:LoggingState.Config.JsonLogging.Directory
    if (-not (Test-Path -Path $jsonDir)) {
        try {
            New-Item -Path $jsonDir -ItemType Directory -Force | Out-Null
        } catch {
            Write-Warning (t 'core.logging.json_dir_create_failed' @{ Error = $_.Exception.Message })
        }
    }

    # Set JSON log file path
    $timestamp = $script:LoggingState.StartTime.ToString('yyyy-MM-dd_HH-mm-ss')
    $script:LoggingState.JsonLogPath = Join-Path $jsonDir "$timestamp`_$($script:LoggingState.SessionId).jsonl"

    # Initialize buffer
    $script:LoggingState.LogBuffer = @()
    $script:LoggingState.BufferSize = $script:LoggingState.Config.JsonLogging.BufferSize

    # Perform retention cleanup
    Invoke-LogRetentionCleanup

    # Mark as initialized
    $script:LoggingState.Initialized = $true

    # Write initialization log entry
    Write-StructuredLog -Level 'Info' -Category 'System' -Message (t 'core.logging.initialized') -Data @{
        SessionId = $script:LoggingState.SessionId
        JsonLogPath = $script:LoggingState.JsonLogPath
        ComputerName = $env:COMPUTERNAME
        UserName = $env:USERNAME
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    }

    Write-Verbose "Structured logging initialized: $($script:LoggingState.JsonLogPath)"
}

function Get-LoggingConfig {
    <#
    .SYNOPSIS
        Loads and returns the logging configuration.

    .DESCRIPTION
        Reads the structured logging configuration from the JSON config file and returns
        a hashtable with text logging, JSON logging, and category settings. Falls back
        to default configuration values when the config file is missing or unreadable.

    .OUTPUTS
        Hashtable containing logging configuration.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if (Test-Path -Path $script:ConfigPath) {
        try {
            $json = Get-Content -Path $script:ConfigPath -Raw | ConvertFrom-Json

            return @{
                TextLogging = @{
                    Enabled = if ($null -ne $json.textLogging.enabled) { $json.textLogging.enabled } else { $script:DefaultConfig.TextLogging.Enabled }
                    RetentionDays = if ($null -ne $json.textLogging.retentionDays) { $json.textLogging.retentionDays } else { $script:DefaultConfig.TextLogging.RetentionDays }
                }
                JsonLogging = @{
                    Enabled = if ($null -ne $json.jsonLogging.enabled) { $json.jsonLogging.enabled } else { $script:DefaultConfig.JsonLogging.Enabled }
                    RetentionDays = if ($null -ne $json.jsonLogging.retentionDays) { $json.jsonLogging.retentionDays } else { $script:DefaultConfig.JsonLogging.RetentionDays }
                    Directory = if ($null -ne $json.jsonLogging.directory) { [Environment]::ExpandEnvironmentVariables($json.jsonLogging.directory) } else { $script:DefaultConfig.JsonLogging.Directory }
                    BufferSize = if ($null -ne $json.jsonLogging.bufferSize) { $json.jsonLogging.bufferSize } else { $script:DefaultConfig.JsonLogging.BufferSize }
                    PrettyPrint = if ($null -ne $json.jsonLogging.prettyPrint) { $json.jsonLogging.prettyPrint } else { $script:DefaultConfig.JsonLogging.PrettyPrint }
                }
                Categories = if ($null -ne $json.categories) { @($json.categories) } else { $script:DefaultConfig.Categories }
            }
        } catch {
            Write-Verbose "Failed to load logging config, using defaults: $($_.Exception.Message)"
            return $script:DefaultConfig
        }
    }

    return $script:DefaultConfig
}

# === LOGGING FUNCTIONS ===

function Write-StructuredLog {
    <#
    .SYNOPSIS
        Writes a structured log entry to JSON log file.

    .DESCRIPTION
        Creates a JSON log entry with timestamp, session ID, request ID, level, category,
        message, and optional structured data. Entries are buffered and flushed
        periodically for performance.

    .PARAMETER Level
        Log level: Info, Success, Warning, Error, Debug, Verbose

    .PARAMETER Category
        Log category for filtering/grouping.

    .PARAMETER Message
        The log message.

    .PARAMETER RequestId
        Optional request ID for correlating related log entries.
        If not specified, uses the current request ID from Set-LogRequestId.

    .PARAMETER Data
        Optional hashtable of structured data to include.

    .PARAMETER Exception
        Optional exception object to include.

    .EXAMPLE
        Write-StructuredLog -Level 'Info' -Category 'Installation' -Message 'Installing VSCode'
        Write-StructuredLog -Level 'Error' -Category 'Installation' -Message 'Install failed' -Data @{AppName='VSCode'; ErrorCode=1603}
        Write-StructuredLog -Level 'Info' -Category 'Installation' -Message 'Starting operation' -RequestId 'req-12345'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug', 'Verbose')]
        [string]$Level,

        [Parameter()]
        [string]$Category = 'General',

        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [string]$RequestId,

        [Parameter()]
        [hashtable]$Data,

        [Parameter()]
        [System.Exception]$Exception
    )

    # Check if logging is enabled and initialized
    if (-not $script:LoggingState.Initialized) {
        # Auto-initialize if needed
        Initialize-StructuredLogging
    }

    if (-not $script:LoggingState.Config.JsonLogging.Enabled) {
        return
    }

    # Determine request ID (parameter takes precedence over current)
    $effectiveRequestId = if ($RequestId) { $RequestId } else { $script:LoggingState.CurrentRequestId }

    # Build log entry
    $logEntry = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        sessionId = $script:LoggingState.SessionId
        level = $Level
        category = $Category
        message = $Message
    }

    # Add request ID if available
    if ($effectiveRequestId) {
        $logEntry.requestId = $effectiveRequestId
    }

    # Add structured data
    if ($Data) {
        $logEntry.data = $Data
    }

    # Add exception details
    if ($Exception) {
        $logEntry.exception = @{
            type = $Exception.GetType().FullName
            message = $Exception.Message
            stackTrace = $Exception.StackTrace
        }

        if ($Exception.InnerException) {
            $logEntry.exception.innerException = @{
                type = $Exception.InnerException.GetType().FullName
                message = $Exception.InnerException.Message
            }
        }
    }

    # Add to buffer
    $script:LoggingState.LogBuffer += $logEntry

    # Flush buffer if full
    if ($script:LoggingState.LogBuffer.Count -ge $script:LoggingState.BufferSize) {
        Clear-LogBuffer
    }
}

function Clear-LogBuffer {
    <#
    .SYNOPSIS
        Flushes the log buffer to disk.

    .DESCRIPTION
        Writes all buffered log entries to the JSON log file.
    #>
    [CmdletBinding()]
    param()

    if ($script:LoggingState.LogBuffer.Count -eq 0) {
        return
    }

    if (-not $script:LoggingState.JsonLogPath) {
        return
    }

    try {
        $lines = @()
        foreach ($entry in $script:LoggingState.LogBuffer) {
            if ($script:LoggingState.Config.JsonLogging.PrettyPrint) {
                $lines += ($entry | ConvertTo-Json -Depth 10 -Compress:$false)
            } else {
                $lines += ($entry | ConvertTo-Json -Depth 10 -Compress)
            }
        }

        $lines | Add-Content -Path $script:LoggingState.JsonLogPath -Encoding UTF8 -ErrorAction Stop

        # Clear buffer
        $script:LoggingState.LogBuffer = @()

        # Check if log rotation is needed
        Invoke-LogRotationIfNeeded
    } catch {
        Write-Verbose "Failed to flush log buffer: $($_.Exception.Message)"
    }
}

function Invoke-LogRotationIfNeeded {
    <#
    .SYNOPSIS
        Rotates the log file if it exceeds the maximum size.

    .DESCRIPTION
        Checks the current log file size and rotates to a new file if it
        exceeds the configured MaxFileSizeMB threshold.
    #>
    [CmdletBinding()]
    param()

    if (-not $script:LoggingState.JsonLogPath) {
        return
    }

    if (-not (Test-Path -Path $script:LoggingState.JsonLogPath)) {
        return
    }

    # Get max file size from config (default 10 MB)
    $maxSizeMB = $script:LoggingState.Config.JsonLogging.MaxFileSizeMB
    if (-not $maxSizeMB) {
        $maxSizeMB = 10
    }
    $maxSizeBytes = $maxSizeMB * 1MB

    try {
        $fileInfo = Get-Item -Path $script:LoggingState.JsonLogPath -ErrorAction SilentlyContinue
        if ($fileInfo -and $fileInfo.Length -gt $maxSizeBytes) {
            # Log rotation needed
            Write-Verbose "Log file exceeded $maxSizeMB MB, rotating..."

            # Generate new log file path
            $jsonDir = $script:LoggingState.Config.JsonLogging.Directory
            $timestamp = (Get-Date).ToString('yyyy-MM-dd_HH-mm-ss')
            $newLogPath = Join-Path $jsonDir "$timestamp`_$($script:LoggingState.SessionId)_rotated.jsonl"

            # Write rotation notice to old file
            $rotationEntry = [ordered]@{
                timestamp = (Get-Date).ToString('o')
                sessionId = $script:LoggingState.SessionId
                level = 'Info'
                category = 'System'
                message = (t 'core.logging.log_rotated' @{ SizeMB = $maxSizeMB })
                data = @{
                    previousLogSize = $fileInfo.Length
                    newLogPath = $newLogPath
                }
            }
            ($rotationEntry | ConvertTo-Json -Depth 10 -Compress) | Add-Content -Path $script:LoggingState.JsonLogPath -Encoding UTF8 -ErrorAction Stop

            # Switch to new log file
            $script:LoggingState.JsonLogPath = $newLogPath

            # Write initialization entry to new file
            Write-StructuredLog -Level 'Info' -Category 'System' -Message (t 'core.logging.log_file_rotated') -Data @{
                reason = (t 'core.logging.reason_size_limit')
                previousLogSize = $fileInfo.Length
            }
        }
    } catch {
        Write-Verbose "Failed to check/perform log rotation: $($_.Exception.Message)"
    }
}

# === REQUEST ID FUNCTIONS ===

function Set-LogRequestId {
    <#
    .SYNOPSIS
        Sets the current request ID for log correlation.

    .DESCRIPTION
        Sets a request ID that will be automatically included in all subsequent
        log entries until cleared. Use this to correlate logs for a specific
        operation like installing an application.

    .PARAMETER RequestId
        The request ID to set. If not provided, generates a new GUID.

    .OUTPUTS
        [string] The request ID that was set.

    .EXAMPLE
        $requestId = Set-LogRequestId
        # ... do work ...
        Clear-LogRequestId

    .EXAMPLE
        Set-LogRequestId -RequestId "install-vscode-12345"
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [string]$RequestId
    )

    if (-not $RequestId) {
        $RequestId = [System.Guid]::NewGuid().ToString('N').Substring(0, 12)
    }

    $script:LoggingState.CurrentRequestId = $RequestId
    return $RequestId
}

function Clear-LogRequestId {
    <#
    .SYNOPSIS
        Clears the current request ID.

    .DESCRIPTION
        Clears the request ID so subsequent log entries will not have a request ID
        unless one is explicitly provided.

    .EXAMPLE
        Clear-LogRequestId
    #>
    [CmdletBinding()]
    param()

    $script:LoggingState.CurrentRequestId = $null
}

function Get-LogRequestId {
    <#
    .SYNOPSIS
        Gets the current request ID.

    .DESCRIPTION
        Retrieves the request ID currently stored in the logging state. This ID is used
        to correlate all log entries belonging to the same logical request or operation.

    .OUTPUTS
        [string] The current request ID, or $null if not set.

    .EXAMPLE
        $currentId = Get-LogRequestId
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:LoggingState.CurrentRequestId
}

# === EXPORT FUNCTIONS ===

function Export-LogsToJson {
    <#
    .SYNOPSIS
        Exports log entries to a single JSON file.

    .DESCRIPTION
        Reads JSONL log files and exports them as a properly formatted
        JSON array for external tools.

    .PARAMETER OutputPath
        Path for the output JSON file.

    .PARAMETER StartDate
        Optional filter for log entries after this date.

    .PARAMETER EndDate
        Optional filter for log entries before this date.

    .PARAMETER Categories
        Optional array of categories to include.

    .PARAMETER Levels
        Optional array of levels to include.

    .EXAMPLE
        Export-LogsToJson -OutputPath "C:\Logs\export.json"
        Export-LogsToJson -OutputPath "C:\Logs\errors.json" -Levels @('Error', 'Warning')
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter()]
        [datetime]$StartDate,

        [Parameter()]
        [datetime]$EndDate,

        [Parameter()]
        [string[]]$Categories,

        [Parameter()]
        [string[]]$Levels
    )

    # Ensure buffer is flushed
    Clear-LogBuffer

    $jsonDir = $script:LoggingState.Config.JsonLogging.Directory
    if (-not (Test-Path -Path $jsonDir)) {
        Write-Warning (t 'core.logging.json_dir_not_found' @{ Path = $jsonDir })
        return
    }

    $allEntries = @()
    $jsonlFiles = Get-ChildItem -Path $jsonDir -Filter '*.jsonl' -ErrorAction SilentlyContinue

    foreach ($file in $jsonlFiles) {
        $lines = Get-Content -Path $file.FullName -ErrorAction SilentlyContinue
        foreach ($line in $lines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }

            try {
                $entry = $line | ConvertFrom-Json
                $include = $true

                # Apply filters
                if ($StartDate -and $entry.timestamp) {
                    $entryTime = if ($entry.timestamp -is [datetime]) { $entry.timestamp } else { [datetimeoffset]::Parse($entry.timestamp).LocalDateTime }
                    if ($entryTime -lt $StartDate) { $include = $false }
                }

                if ($EndDate -and $entry.timestamp) {
                    $entryTime = if ($entry.timestamp -is [datetime]) { $entry.timestamp } else { [datetimeoffset]::Parse($entry.timestamp).LocalDateTime }
                    if ($entryTime -gt $EndDate) { $include = $false }
                }

                if ($Categories -and $entry.category) {
                    if ($entry.category -notin $Categories) { $include = $false }
                }

                if ($Levels -and $entry.level) {
                    if ($entry.level -notin $Levels) { $include = $false }
                }

                if ($include) {
                    $allEntries += $entry
                }
            } catch {
                Write-Verbose "Failed to parse log line: $line"
            }
        }
    }

    # Sort by timestamp
    $allEntries = $allEntries | Sort-Object { if ($_.timestamp -is [datetime]) { $_.timestamp } else { [datetimeoffset]::Parse($_.timestamp).LocalDateTime } }

    # Export
    $directory = Split-Path -Path $OutputPath -Parent
    if (-not (Test-Path -Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    $allEntries | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8

    $exportCount = @($allEntries).Count
    Write-Verbose "Exported $exportCount log entries to $OutputPath"
    return $exportCount
}

function Get-StructuredLogs {
    <#
    .SYNOPSIS
        Retrieves structured log entries.

    .DESCRIPTION
        Reads and returns log entries from the current session or all sessions.

    .PARAMETER CurrentSessionOnly
        Only return entries from the current session.

    .PARAMETER Category
        Filter by category.

    .PARAMETER Level
        Filter by level.

    .PARAMETER Last
        Return only the last N entries.

    .OUTPUTS
        Array of log entry objects.

    .EXAMPLE
        Get-StructuredLogs -CurrentSessionOnly
        Get-StructuredLogs -Category 'Error' -Last 10
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$CurrentSessionOnly,

        [Parameter()]
        [string]$Category,

        [Parameter()]
        [string]$Level,

        [Parameter()]
        [int]$Last
    )

    # Flush buffer first
    Clear-LogBuffer

    $entries = @()

    if ($CurrentSessionOnly -and $script:LoggingState.JsonLogPath -and (Test-Path $script:LoggingState.JsonLogPath)) {
        $lines = Get-Content -Path $script:LoggingState.JsonLogPath -ErrorAction SilentlyContinue
        foreach ($line in $lines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $entries += ($line | ConvertFrom-Json)
            } catch {
                # Skip malformed JSON lines (common in log files)
                Write-Verbose "Skipping malformed JSON log line: $($_.Exception.Message)"
            }
        }
    } else {
        $jsonDir = $script:LoggingState.Config.JsonLogging.Directory
        if (Test-Path -Path $jsonDir) {
            $jsonlFiles = Get-ChildItem -Path $jsonDir -Filter '*.jsonl' | Sort-Object LastWriteTime -Descending
            foreach ($file in $jsonlFiles) {
                $lines = Get-Content -Path $file.FullName -ErrorAction SilentlyContinue
                foreach ($line in $lines) {
                    if ([string]::IsNullOrWhiteSpace($line)) { continue }
                    try {
                        $entries += ($line | ConvertFrom-Json)
                    } catch {
                        # Skip malformed JSON lines (common in log files)
                        Write-Verbose "Skipping malformed JSON log line in $($file.Name): $($_.Exception.Message)"
                    }
                }
            }
        }
    }

    # Apply filters
    if ($Category) {
        $entries = $entries | Where-Object { $_.category -eq $Category }
    }

    if ($Level) {
        $entries = $entries | Where-Object { $_.level -eq $Level }
    }

    # Sort and limit
    $entries = $entries | Sort-Object { [datetime]::Parse($_.timestamp) } -Descending

    if ($Last -gt 0) {
        $entries = $entries | Select-Object -First $Last
    }

    return $entries
}

# === MAINTENANCE FUNCTIONS ===

function Invoke-LogRetentionCleanup {
    <#
    .SYNOPSIS
        Removes old log files based on retention settings.

    .DESCRIPTION
        Deletes JSON log files older than the configured retention period.
        Optionally compresses logs before deletion based on age.

    .PARAMETER CompressOlderThanDays
        Compress logs older than this many days (default: 7).
        Set to 0 to disable compression.

    .EXAMPLE
        Invoke-LogRetentionCleanup
        Invoke-LogRetentionCleanup -CompressOlderThanDays 3
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateRange(0, 365)]
        [int]$CompressOlderThanDays = 7
    )

    $config = $script:LoggingState.Config
    if (-not $config) {
        $config = Get-LoggingConfig
    }

    $jsonDir = $config.JsonLogging.Directory
    $retentionDays = $config.JsonLogging.RetentionDays

    if (-not (Test-Path -Path $jsonDir)) {
        return
    }

    $now = Get-Date
    $cutoffDate = $now.AddDays(-$retentionDays)
    $compressCutoff = $now.AddDays(-$CompressOlderThanDays)

    $removedCount = 0
    $compressedCount = 0

    # Get all log files (both .jsonl and .jsonl.zip)
    $logFiles = Get-ChildItem -Path $jsonDir -Filter '*.jsonl' -ErrorAction SilentlyContinue

    foreach ($file in $logFiles) {
        try {
            # Delete files older than retention period
            if ($file.LastWriteTime -lt $cutoffDate) {
                Remove-Item -Path $file.FullName -Force
                $removedCount++
                # Also remove associated zip if exists
                $zipPath = "$($file.FullName).zip"
                if (Test-Path $zipPath) {
                    Remove-Item -Path $zipPath -Force
                }
                continue
            }

            # Compress files older than compression threshold
            if ($CompressOlderThanDays -gt 0 -and $file.LastWriteTime -lt $compressCutoff) {
                $zipPath = "$($file.FullName).zip"
                if (-not (Test-Path $zipPath)) {
                    try {
                        Compress-Archive -Path $file.FullName -DestinationPath $zipPath -CompressionLevel Optimal -Force
                        # Remove original after successful compression
                        Remove-Item -Path $file.FullName -Force
                        $compressedCount++
                    } catch {
                        Write-Verbose "Failed to compress log file: $($file.Name) - $($_.Exception.Message)"
                    }
                }
            }
        } catch {
            Write-Verbose "Failed to process log file: $($file.Name)"
        }
    }

    # Also cleanup old zip files beyond retention
    $zipFiles = Get-ChildItem -Path $jsonDir -Filter '*.jsonl.zip' -ErrorAction SilentlyContinue
    foreach ($zipFile in $zipFiles) {
        if ($zipFile.LastWriteTime -lt $cutoffDate) {
            try {
                Remove-Item -Path $zipFile.FullName -Force
                $removedCount++
            } catch {
                Write-Verbose "Failed to remove old zip file: $($zipFile.Name)"
            }
        }
    }

    if ($removedCount -gt 0 -or $compressedCount -gt 0) {
        Write-Verbose "Log cleanup: removed $removedCount files, compressed $compressedCount files"
    }
}

function Get-ArchivedLogs {
    <#
    .SYNOPSIS
        Lists archived (compressed) log files.

    .DESCRIPTION
        Scans the JSON logging directory for compressed (.jsonl.zip) log archives and
        returns metadata about each file, including name, path, size, and timestamps,
        sorted by most recent modification first.

    .OUTPUTS
        Array of archived log file information.

    .EXAMPLE
        Get-ArchivedLogs
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $config = $script:LoggingState.Config
    if (-not $config) {
        $config = Get-LoggingConfig
    }

    $jsonDir = $config.JsonLogging.Directory

    if (-not (Test-Path -Path $jsonDir)) {
        return @()
    }

    $archives = Get-ChildItem -Path $jsonDir -Filter '*.jsonl.zip' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        ForEach-Object {
            [PSCustomObject]@{
                Name = $_.Name
                Path = $_.FullName
                SizeKB = [math]::Round($_.Length / 1KB, 2)
                CreatedAt = $_.CreationTime
                ModifiedAt = $_.LastWriteTime
            }
        }

    return @($archives)
}

function Expand-ArchivedLog {
    <#
    .SYNOPSIS
        Extracts an archived log file for viewing.

    .DESCRIPTION
        Decompresses a previously archived JSONL log zip file so its contents can be
        inspected or processed. Extracts to the same directory as the archive by default,
        or to a specified output path.

    .PARAMETER ArchivePath
        Path to the .zip archive.

    .PARAMETER OutputPath
        Optional output path. Defaults to same directory.

    .OUTPUTS
        Path to the extracted file.

    .EXAMPLE
        Expand-ArchivedLog -ArchivePath "C:\Logs\2026-01-15.jsonl.zip"
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string]$ArchivePath,

        [Parameter()]
        [string]$OutputPath
    )

    if (-not $OutputPath) {
        $OutputPath = Split-Path -Parent $ArchivePath
    }

    try {
        Expand-Archive -Path $ArchivePath -DestinationPath $OutputPath -Force
        $extractedFile = Join-Path $OutputPath ([System.IO.Path]::GetFileNameWithoutExtension($ArchivePath))
        return $extractedFile
    } catch {
        Write-Error (t 'core.logging.expand_archive_failed' @{ Error = $_.Exception.Message })
        return $null
    }
}

function Close-StructuredLogging {
    <#
    .SYNOPSIS
        Closes the structured logging session.

    .DESCRIPTION
        Flushes all buffered entries and writes a session close entry.

    .EXAMPLE
        Close-StructuredLogging
    #>
    [CmdletBinding()]
    param()

    if (-not $script:LoggingState.Initialized) {
        return
    }

    # Write close entry
    Write-StructuredLog -Level 'Info' -Category 'System' -Message (t 'core.logging.session_closed') -Data @{
        SessionId = $script:LoggingState.SessionId
        Duration = ((Get-Date) - $script:LoggingState.StartTime).ToString()
    }

    # Final flush
    Clear-LogBuffer

    # Reset state
    $script:LoggingState.Initialized = $false
    $script:LoggingState.SessionId = $null

    Write-Verbose "Structured logging session closed"
}

function Get-LoggingStatistics {
    <#
    .SYNOPSIS
        Returns statistics about the logging system.

    .DESCRIPTION
        Gathers and returns aggregate statistics about the structured logging subsystem,
        including total log file count, combined size on disk, oldest and newest log
        timestamps, and current buffer usage.

    .OUTPUTS
        PSCustomObject with logging statistics.

    .EXAMPLE
        Get-LoggingStatistics
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $config = $script:LoggingState.Config
    if (-not $config) {
        $config = Get-LoggingConfig
    }

    $jsonDir = $config.JsonLogging.Directory
    $totalSize = 0
    $fileCount = 0
    $oldestLog = $null
    $newestLog = $null

    if (Test-Path -Path $jsonDir) {
        $files = Get-ChildItem -Path $jsonDir -Filter '*.jsonl'
        $fileCount = $files.Count

        foreach ($file in $files) {
            $totalSize += $file.Length
        }

        if ($files.Count -gt 0) {
            $sortedFiles = $files | Sort-Object LastWriteTime
            $oldestLog = $sortedFiles[0].LastWriteTime
            $newestLog = $sortedFiles[-1].LastWriteTime
        }
    }

    return [PSCustomObject]@{
        Initialized = $script:LoggingState.Initialized
        CurrentSessionId = $script:LoggingState.SessionId
        SessionStartTime = $script:LoggingState.StartTime
        CurrentLogFile = $script:LoggingState.JsonLogPath
        BufferedEntries = $script:LoggingState.LogBuffer.Count
        BufferSize = $script:LoggingState.BufferSize
        JsonLoggingEnabled = $config.JsonLogging.Enabled
        RetentionDays = $config.JsonLogging.RetentionDays
        LogDirectory = $jsonDir
        TotalLogFiles = $fileCount
        TotalSizeBytes = $totalSize
        TotalSizeMB = [math]::Round($totalSize / 1MB, 2)
        OldestLogDate = $oldestLog
        NewestLogDate = $newestLog
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-StructuredLogging',
    'Get-LoggingConfig',
    'Write-StructuredLog',
    'Clear-LogBuffer',
    'Set-LogRequestId',
    'Clear-LogRequestId',
    'Get-LogRequestId',
    'Export-LogsToJson',
    'Get-StructuredLogs',
    'Invoke-LogRetentionCleanup',
    'Get-ArchivedLogs',
    'Expand-ArchivedLog',
    'Close-StructuredLogging',
    'Get-LoggingStatistics'
)
