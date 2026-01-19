<#
.SYNOPSIS
    Win11Forge - Update Manager Module v3.1.4

.DESCRIPTION
    Provides auto-update functionality for Win11Forge:
    - Check for new releases via GitHub API
    - Semantic version comparison
    - Download and apply updates
    - Backup and restore capabilities
    - Update scheduling

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

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:VersionPath = Join-Path $script:RepositoryRoot 'version.json'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module for i18n support
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === BATCH UPDATE CACHE ===
# Caches winget upgrade results to avoid multiple CLI calls
$script:BatchUpdateCache = @{}
$script:BatchUpdateCacheTime = $null
$script:BatchUpdateCacheMaxAgeMinutes = 10

# === CONFIGURATION ===
$script:UpdateConfig = @{
    GitHubOwner = 'owner'
    GitHubRepo = 'Win11Forge'
    ApiBaseUrl = 'https://api.github.com'
    AutoCheckEnabled = $true
    CheckIntervalHours = 24
    IncludePrerelease = $false
    BackupDirectory = Join-Path $env:LOCALAPPDATA 'Win11Forge\Backups'
    DownloadDirectory = Join-Path $env:LOCALAPPDATA 'Win11Forge\Updates'
    LastCheckFile = Join-Path $env:LOCALAPPDATA 'Win11Forge\last-update-check.json'
}

# === VERSION FUNCTIONS ===

function Get-CurrentVersion {
    <#
    .SYNOPSIS
        Gets the current installed version.

    .OUTPUTS
        String containing the version number.

    .EXAMPLE
        $version = Get-CurrentVersion
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if (Test-Path $script:VersionPath) {
        try {
            $versionJson = Get-Content $script:VersionPath -Raw | ConvertFrom-Json
            return $versionJson.Version
        } catch {
            Write-Verbose "Failed to read version file: $($_.Exception.Message)"
        }
    }

    return '0.0.0'
}

function Compare-SemanticVersions {
    <#
    .SYNOPSIS
        Compares two semantic version strings.

    .DESCRIPTION
        Compares versions in the format X.Y.Z (optionally with -prerelease suffix).
        Returns: -1 if Version1 < Version2, 0 if equal, 1 if Version1 > Version2

    .PARAMETER Version1
        First version to compare.

    .PARAMETER Version2
        Second version to compare.

    .OUTPUTS
        Integer: -1, 0, or 1

    .EXAMPLE
        Compare-SemanticVersions -Version1 '3.1.4' -Version2 '3.2.0'  # Returns -1
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [string]$Version1,

        [Parameter(Mandatory)]
        [string]$Version2
    )

    # Remove 'v' prefix if present
    $v1 = $Version1 -replace '^v', ''
    $v2 = $Version2 -replace '^v', ''

    # Extract prerelease suffix
    $v1Parts = $v1 -split '-', 2
    $v2Parts = $v2 -split '-', 2

    $v1Core = $v1Parts[0]
    $v2Core = $v2Parts[0]
    $v1Pre = if ($v1Parts.Count -gt 1) { $v1Parts[1] } else { $null }
    $v2Pre = if ($v2Parts.Count -gt 1) { $v2Parts[1] } else { $null }

    # Parse version numbers
    $v1Nums = $v1Core -split '\.' | ForEach-Object { [int]$_ }
    $v2Nums = $v2Core -split '\.' | ForEach-Object { [int]$_ }

    # Pad to 3 elements
    while ($v1Nums.Count -lt 3) { $v1Nums += 0 }
    while ($v2Nums.Count -lt 3) { $v2Nums += 0 }

    # Compare major, minor, patch
    for ($i = 0; $i -lt 3; $i++) {
        if ($v1Nums[$i] -gt $v2Nums[$i]) { return 1 }
        if ($v1Nums[$i] -lt $v2Nums[$i]) { return -1 }
    }

    # If core versions are equal, compare prerelease
    # No prerelease > with prerelease (e.g., 1.0.0 > 1.0.0-beta)
    if (-not $v1Pre -and $v2Pre) { return 1 }
    if ($v1Pre -and -not $v2Pre) { return -1 }
    if ($v1Pre -and $v2Pre) {
        return [string]::Compare($v1Pre, $v2Pre, $true)
    }

    return 0
}

function Test-IsNewerVersion {
    <#
    .SYNOPSIS
        Compares two version strings and returns true if $Available is newer than $Current.
    .DESCRIPTION
        Uses [System.Version]::Parse() for proper semantic version comparison.
        Handles common edge cases like "v1.0" vs "1.0", missing patch versions, etc.
    .PARAMETER Current
        The currently installed version string.
    .PARAMETER Available
        The available/new version string to compare against.
    .OUTPUTS
        Boolean - true if Available > Current, false otherwise.
    .EXAMPLE
        Test-IsNewerVersion -Current "1.0" -Available "1.0.1"  # Returns $true
        Test-IsNewerVersion -Current "v2.0.0" -Available "1.9.9"  # Returns $false
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Current,

        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Available
    )

    # Handle null/empty cases
    if ([string]::IsNullOrWhiteSpace($Current)) { return $true }
    if ([string]::IsNullOrWhiteSpace($Available)) { return $false }

    # Clean version strings - remove common prefixes
    $cleanCurrent = $Current -replace '^[vV]', '' -replace '^version\s*', '' -replace '[^\d\.].*$', ''
    $cleanAvailable = $Available -replace '^[vV]', '' -replace '^version\s*', '' -replace '[^\d\.].*$', ''

    # Ensure we have at least major.minor.patch format
    $currentParts = $cleanCurrent -split '\.'
    $availableParts = $cleanAvailable -split '\.'

    # Pad to 4 parts (major.minor.patch.build) for System.Version compatibility
    while ($currentParts.Count -lt 2) { $currentParts += '0' }
    while ($availableParts.Count -lt 2) { $availableParts += '0' }

    # Limit to 4 parts max (System.Version constraint)
    if ($currentParts.Count -gt 4) { $currentParts = $currentParts[0..3] }
    if ($availableParts.Count -gt 4) { $availableParts = $availableParts[0..3] }

    $normalizedCurrent = $currentParts -join '.'
    $normalizedAvailable = $availableParts -join '.'

    try {
        $versionCurrent = [System.Version]::Parse($normalizedCurrent)
        $versionAvailable = [System.Version]::Parse($normalizedAvailable)

        return $versionAvailable -gt $versionCurrent
    } catch {
        # Fallback to string comparison if parsing fails
        if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
            Write-Verbose (Get-LocalizedString -Key 'optimization.version_compare_fallback' -Parameters @{ Current = $Current; Available = $Available })
        }
        return (Compare-SemanticVersions -Version1 $Available -Version2 $Current) -gt 0
    }
}

function Get-WingetUpdatesBatch {
    <#
    .SYNOPSIS
        Fetches all available winget updates in a single CLI call and caches the results.
    .DESCRIPTION
        Runs 'winget upgrade --include-unknown' once and parses the output to build
        a cache of all available updates. This is much more efficient than checking
        each application individually (~3s total vs ~2s per app).
    .PARAMETER Force
        Force cache refresh even if not expired.
    .OUTPUTS
        Hashtable mapping PackageId -> PSCustomObject with Name, CurrentVersion, AvailableVersion.
    .EXAMPLE
        $updates = Get-WingetUpdatesBatch
        if ($updates.ContainsKey('Microsoft.VisualStudioCode')) {
            Write-Host "VS Code update available: $($updates['Microsoft.VisualStudioCode'].AvailableVersion)"
        }
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$Force
    )

    # Check cache validity
    $cacheExpired = $script:BatchUpdateCacheTime -and `
        ((Get-Date) - $script:BatchUpdateCacheTime).TotalMinutes -gt $script:BatchUpdateCacheMaxAgeMinutes

    if (-not $Force -and $script:BatchUpdateCache.Count -gt 0 -and -not $cacheExpired) {
        if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
            Write-Verbose (Get-LocalizedString -Key 'optimization.batch_cache_hit' -Parameters @{ Count = $script:BatchUpdateCache.Count })
        }
        return $script:BatchUpdateCache
    }

    # Clear and rebuild cache
    $script:BatchUpdateCache = @{}
    $script:BatchUpdateCacheTime = Get-Date

    if (-not (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
        Write-Verbose "Winget not available, returning empty cache"
        return $script:BatchUpdateCache
    }

    if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
        Write-Status -Message (Get-LocalizedString -Key 'optimization.batch_cache_building') -Level 'Info'
    }

    try {
        # Run winget upgrade once to get all available updates
        $output = & winget upgrade --include-unknown --accept-source-agreements 2>&1 | Out-String

        # Parse the output - format is typically:
        # Name                            Id                           Version      Available      Source
        # ----------------------------------------------------------------------------------------------------------
        # Application Name                Publisher.AppName            1.0.0        1.1.0          winget

        $lines = $output -split "`n"
        $headerFound = $false
        $columnPositions = @{}

        foreach ($line in $lines) {
            # Skip empty lines
            if ([string]::IsNullOrWhiteSpace($line)) { continue }

            # Detect header line (contains "Name", "Id", "Version", "Available")
            if (-not $headerFound -and $line -match 'Name\s+Id\s+Version\s+Available') {
                $headerFound = $true
                # Parse column positions from header
                $columnPositions['Name'] = $line.IndexOf('Name')
                $columnPositions['Id'] = $line.IndexOf('Id')
                $columnPositions['Version'] = $line.IndexOf('Version')
                $columnPositions['Available'] = $line.IndexOf('Available')
                $columnPositions['Source'] = $line.IndexOf('Source')
                continue
            }

            # Skip separator line
            if ($line -match '^-+$' -or $line -match '^[-\s]+$') { continue }

            # Skip footer lines
            if ($line -match 'upgrades? available' -or $line -match 'winget upgrade') { continue }

            # Parse data lines (only if header was found and line is long enough)
            if ($headerFound -and $line.Length -gt 20) {
                try {
                    # Use column positions to extract data
                    $idStart = $columnPositions['Id']
                    $versionStart = $columnPositions['Version']
                    $availableStart = $columnPositions['Available']
                    $sourceStart = if ($columnPositions['Source'] -gt 0) { $columnPositions['Source'] } else { $line.Length }

                    if ($idStart -ge 0 -and $versionStart -gt $idStart -and $availableStart -gt $versionStart) {
                        $name = $line.Substring(0, $idStart).Trim()
                        $id = $line.Substring($idStart, $versionStart - $idStart).Trim()
                        $currentVersion = $line.Substring($versionStart, $availableStart - $versionStart).Trim()
                        $availableVersion = $line.Substring($availableStart, [Math]::Min($sourceStart - $availableStart, $line.Length - $availableStart)).Trim()

                        # Validate we have an ID and versions
                        if ($id -match '\.' -and $availableVersion -match '[\d\.]') {
                            $script:BatchUpdateCache[$id] = [PSCustomObject]@{
                                Name             = $name
                                PackageId        = $id
                                CurrentVersion   = $currentVersion
                                AvailableVersion = $availableVersion
                            }
                        }
                    }
                } catch {
                    # Line parsing failed - skip this line
                    Write-Verbose "Failed to parse line: $line"
                }
            }
        }

        if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
            Write-Status -Message (Get-LocalizedString -Key 'optimization.batch_cache_built' -Parameters @{ Count = $script:BatchUpdateCache.Count }) -Level 'Success'
        }
    } catch {
        Write-Status -Message "Failed to build batch update cache: $($_.Exception.Message)" -Level 'Warning'
    }

    return $script:BatchUpdateCache
}

function Get-ApplicationUpdateStatus {
    <#
    .SYNOPSIS
        Checks if an application has an update available using the batch cache.
    .DESCRIPTION
        Uses the batch update cache to efficiently check update status without
        making additional CLI calls. Falls back to direct winget query if needed.
    .PARAMETER WingetId
        The Winget package ID to check.
    .PARAMETER CurrentVersion
        Optional current version for comparison (uses cache version if not provided).
    .OUTPUTS
        PSCustomObject with HasUpdate, CurrentVersion, AvailableVersion, or $null if not found.
    .EXAMPLE
        $status = Get-ApplicationUpdateStatus -WingetId 'Microsoft.VisualStudioCode'
        if ($status.HasUpdate) {
            Write-Host "Update available: $($status.AvailableVersion)"
        }
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$WingetId,

        [Parameter()]
        [string]$CurrentVersion
    )

    # Ensure batch cache is populated
    $cache = Get-WingetUpdatesBatch

    # Check if app is in the update cache
    if ($cache.ContainsKey($WingetId)) {
        $cacheEntry = $cache[$WingetId]

        # Use provided current version or cache version
        $installedVersion = if ($CurrentVersion) { $CurrentVersion } else { $cacheEntry.CurrentVersion }

        return [PSCustomObject]@{
            HasUpdate        = Test-IsNewerVersion -Current $installedVersion -Available $cacheEntry.AvailableVersion
            PackageId        = $WingetId
            Name             = $cacheEntry.Name
            CurrentVersion   = $installedVersion
            AvailableVersion = $cacheEntry.AvailableVersion
            Source           = 'BatchCache'
        }
    }

    # App not in update cache - no update available (or not installed via winget)
    return [PSCustomObject]@{
        HasUpdate        = $false
        PackageId        = $WingetId
        Name             = $null
        CurrentVersion   = $CurrentVersion
        AvailableVersion = $null
        Source           = 'BatchCache'
    }
}

function Clear-BatchUpdateCache {
    <#
    .SYNOPSIS
        Clears the batch update cache to force a refresh.
    #>
    [CmdletBinding()]
    param()

    $script:BatchUpdateCache = @{}
    $script:BatchUpdateCacheTime = $null

    if (Get-Command -Name 'Get-LocalizedString' -ErrorAction SilentlyContinue) {
        Write-Verbose (Get-LocalizedString -Key 'optimization.batch_cache_cleared')
    }
}

# === UPDATE CHECK FUNCTIONS ===

function Get-LatestReleaseInfo {
    <#
    .SYNOPSIS
        Fetches the latest release information from GitHub.

    .PARAMETER IncludePrerelease
        Include prerelease versions.

    .OUTPUTS
        PSCustomObject with release information.

    .EXAMPLE
        $release = Get-LatestReleaseInfo
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$IncludePrerelease
    )

    $apiUrl = "$($script:UpdateConfig.ApiBaseUrl)/repos/$($script:UpdateConfig.GitHubOwner)/$($script:UpdateConfig.GitHubRepo)/releases"

    if ($IncludePrerelease) {
        $apiUrl += "?per_page=10"
    } else {
        $apiUrl += "/latest"
    }

    try {
        $headers = @{
            'Accept' = 'application/vnd.github+json'
            'User-Agent' = 'Win11Forge-UpdateManager'
        }

        $response = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method GET -TimeoutSec 30

        if ($IncludePrerelease) {
            # Get latest from array (including prereleases)
            $release = $response | Select-Object -First 1
        } else {
            $release = $response
        }

        if (-not $release) {
            return $null
        }

        return [PSCustomObject]@{
            TagName = $release.tag_name
            Version = ($release.tag_name -replace '^v', '')
            Name = $release.name
            Body = $release.body
            PublishedAt = $release.published_at
            HtmlUrl = $release.html_url
            IsPrerelease = $release.prerelease
            Assets = $release.assets | ForEach-Object {
                @{
                    Name = $_.name
                    Size = $_.size
                    DownloadUrl = $_.browser_download_url
                    ContentType = $_.content_type
                }
            }
        }
    } catch {
        Write-Status -Message "Failed to fetch release info: $($_.Exception.Message)" -Level 'Error' -Category 'Update'
        return $null
    }
}

function Test-UpdateAvailable {
    <#
    .SYNOPSIS
        Checks if an update is available.

    .PARAMETER IncludePrerelease
        Check for prerelease versions.

    .OUTPUTS
        PSCustomObject with update availability information.

    .EXAMPLE
        $updateInfo = Test-UpdateAvailable
        if ($updateInfo.UpdateAvailable) { Write-Host "Update available!" }
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$IncludePrerelease
    )

    $currentVersion = Get-CurrentVersion
    $latestRelease = Get-LatestReleaseInfo -IncludePrerelease:$IncludePrerelease

    if (-not $latestRelease) {
        return [PSCustomObject]@{
            UpdateAvailable = $false
            Error = 'Failed to fetch latest release information'
            CurrentVersion = $currentVersion
            LatestVersion = $null
            CheckedAt = Get-Date
        }
    }

    $comparison = Compare-SemanticVersions -Version1 $latestRelease.Version -Version2 $currentVersion

    $result = [PSCustomObject]@{
        UpdateAvailable = ($comparison -gt 0)
        CurrentVersion = $currentVersion
        LatestVersion = $latestRelease.Version
        ReleaseName = $latestRelease.Name
        ReleaseNotes = $latestRelease.Body
        ReleaseUrl = $latestRelease.HtmlUrl
        PublishedAt = $latestRelease.PublishedAt
        IsPrerelease = $latestRelease.IsPrerelease
        Assets = $latestRelease.Assets
        CheckedAt = Get-Date
    }

    # Save last check timestamp
    Save-LastCheckTime

    return $result
}

function Save-LastCheckTime {
    <#
    .SYNOPSIS
        Saves the last update check timestamp.
    #>
    [CmdletBinding()]
    param()

    $checkData = @{
        LastCheck = (Get-Date).ToString('o')
    }

    $directory = Split-Path $script:UpdateConfig.LastCheckFile -Parent
    if (-not (Test-Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    $checkData | ConvertTo-Json | Set-Content $script:UpdateConfig.LastCheckFile -Encoding UTF8
}

function Test-ShouldCheckForUpdates {
    <#
    .SYNOPSIS
        Determines if an update check should be performed.

    .OUTPUTS
        Boolean indicating if check should be performed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if (-not $script:UpdateConfig.AutoCheckEnabled) {
        return $false
    }

    if (-not (Test-Path $script:UpdateConfig.LastCheckFile)) {
        return $true
    }

    try {
        $lastCheck = Get-Content $script:UpdateConfig.LastCheckFile -Raw | ConvertFrom-Json
        $lastCheckTime = [datetime]::Parse($lastCheck.LastCheck)
        $hoursSinceCheck = ((Get-Date) - $lastCheckTime).TotalHours

        return ($hoursSinceCheck -ge $script:UpdateConfig.CheckIntervalHours)
    } catch {
        return $true
    }
}

# === UPDATE DOWNLOAD AND INSTALL ===

function Invoke-DownloadUpdate {
    <#
    .SYNOPSIS
        Downloads an update package.

    .PARAMETER ReleaseInfo
        Release information from Get-LatestReleaseInfo or Test-UpdateAvailable.

    .PARAMETER AssetName
        Name of the asset to download (default: first .zip file).

    .OUTPUTS
        String containing the downloaded file path.

    .EXAMPLE
        $downloadPath = Invoke-DownloadUpdate -ReleaseInfo $updateInfo
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$ReleaseInfo,

        [Parameter()]
        [string]$AssetName
    )

    if (-not $ReleaseInfo.Assets -or $ReleaseInfo.Assets.Count -eq 0) {
        Write-Status -Message "No downloadable assets found in release" -Level 'Error' -Category 'Update'
        return $null
    }

    # Find asset to download
    $asset = $null
    if ($AssetName) {
        $asset = $ReleaseInfo.Assets | Where-Object { $_.Name -eq $AssetName } | Select-Object -First 1
    } else {
        # Default: first .zip file
        $asset = $ReleaseInfo.Assets | Where-Object { $_.Name -match '\.zip$' } | Select-Object -First 1
    }

    if (-not $asset) {
        Write-Status -Message "No suitable asset found for download" -Level 'Error' -Category 'Update'
        return $null
    }

    # Ensure download directory exists
    if (-not (Test-Path $script:UpdateConfig.DownloadDirectory)) {
        New-Item -Path $script:UpdateConfig.DownloadDirectory -ItemType Directory -Force | Out-Null
    }

    $downloadPath = Join-Path $script:UpdateConfig.DownloadDirectory $asset.Name

    Write-Status -Message "Downloading update: $($asset.Name)" -Level 'Info' -Category 'Update' -StructuredData @{
        AssetName = $asset.Name
        Size = $asset.Size
        DownloadUrl = $asset.DownloadUrl
    }

    try {
        Invoke-WebRequest -Uri $asset.DownloadUrl -OutFile $downloadPath -UseBasicParsing

        Write-Status -Message "Download completed: $downloadPath" -Level 'Success' -Category 'Update'
        return $downloadPath
    } catch {
        Write-Status -Message "Download failed: $($_.Exception.Message)" -Level 'Error' -Category 'Update'
        return $null
    }
}

# === BACKUP AND RESTORE ===

function Backup-CurrentVersion {
    <#
    .SYNOPSIS
        Creates a backup of the current installation.

    .PARAMETER BackupName
        Optional name for the backup (default: version-timestamp).

    .OUTPUTS
        String containing the backup path.

    .EXAMPLE
        $backupPath = Backup-CurrentVersion
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [string]$BackupName
    )

    $currentVersion = Get-CurrentVersion
    $timestamp = (Get-Date).ToString('yyyy-MM-dd_HH-mm-ss')

    if (-not $BackupName) {
        $BackupName = "v$currentVersion`_$timestamp"
    }

    # Ensure backup directory exists
    if (-not (Test-Path $script:UpdateConfig.BackupDirectory)) {
        New-Item -Path $script:UpdateConfig.BackupDirectory -ItemType Directory -Force | Out-Null
    }

    $backupPath = Join-Path $script:UpdateConfig.BackupDirectory "$BackupName.zip"

    Write-Status -Message "Creating backup: $BackupName" -Level 'Info' -Category 'Update'

    try {
        # Compress the current installation
        Compress-Archive -Path $script:RepositoryRoot -DestinationPath $backupPath -Force

        Write-Status -Message "Backup created: $backupPath" -Level 'Success' -Category 'Update'
        return $backupPath
    } catch {
        Write-Status -Message "Backup failed: $($_.Exception.Message)" -Level 'Error' -Category 'Update'
        return $null
    }
}

function Restore-PreviousVersion {
    <#
    .SYNOPSIS
        Restores a previous version from backup.

    .PARAMETER BackupPath
        Path to the backup file.

    .EXAMPLE
        Restore-PreviousVersion -BackupPath "C:\Backups\v3.1.3.zip"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ })]
        [string]$BackupPath
    )

    Write-Status -Message "Restoring from backup: $BackupPath" -Level 'Info' -Category 'Update'

    try {
        # Extract backup to a temp directory first
        $tempDir = Join-Path $env:TEMP "Win11Forge_Restore_$(Get-Date -Format 'yyyyMMddHHmmss')"
        Expand-Archive -Path $BackupPath -DestinationPath $tempDir -Force

        # Copy files back
        Copy-Item -Path "$tempDir\*" -Destination $script:RepositoryRoot -Recurse -Force

        # Cleanup temp directory
        Remove-Item -Path $tempDir -Recurse -Force

        Write-Status -Message "Restore completed successfully" -Level 'Success' -Category 'Update'
    } catch {
        Write-Status -Message "Restore failed: $($_.Exception.Message)" -Level 'Error' -Category 'Update'
        throw
    }
}

function Get-AvailableBackups {
    <#
    .SYNOPSIS
        Lists available backups.

    .OUTPUTS
        Array of backup information objects.

    .EXAMPLE
        Get-AvailableBackups | Format-Table
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $backups = @()

    if (Test-Path $script:UpdateConfig.BackupDirectory) {
        $backupFiles = Get-ChildItem -Path $script:UpdateConfig.BackupDirectory -Filter '*.zip'

        foreach ($file in $backupFiles) {
            $backups += [PSCustomObject]@{
                Name = $file.BaseName
                Path = $file.FullName
                Size = $file.Length
                SizeMB = [math]::Round($file.Length / 1MB, 2)
                Created = $file.CreationTime
            }
        }
    }

    return $backups | Sort-Object Created -Descending
}

# === UPDATE INSTALLATION ===

function Install-Update {
    <#
    .SYNOPSIS
        Installs a downloaded update.

    .PARAMETER UpdatePath
        Path to the downloaded update file.

    .PARAMETER CreateBackup
        Create a backup before updating (default: true).

    .EXAMPLE
        Install-Update -UpdatePath "C:\Updates\Win11Forge-3.2.0.zip"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ })]
        [string]$UpdatePath,

        [Parameter()]
        [switch]$CreateBackup = $true
    )

    Write-Status -Message "Installing update from: $UpdatePath" -Level 'Info' -Category 'Update'

    # Create backup first
    if ($CreateBackup) {
        $backupPath = Backup-CurrentVersion
        if (-not $backupPath) {
            Write-Status -Message "Backup failed, aborting update" -Level 'Error' -Category 'Update'
            return $false
        }
    }

    try {
        # Extract update to temp directory
        $tempDir = Join-Path $env:TEMP "Win11Forge_Update_$(Get-Date -Format 'yyyyMMddHHmmss')"
        Expand-Archive -Path $UpdatePath -DestinationPath $tempDir -Force

        # Copy new files
        Copy-Item -Path "$tempDir\*" -Destination $script:RepositoryRoot -Recurse -Force

        # Cleanup
        Remove-Item -Path $tempDir -Recurse -Force

        Write-Status -Message "Update installed successfully" -Level 'Success' -Category 'Update'
        return $true
    } catch {
        Write-Status -Message "Update failed: $($_.Exception.Message)" -Level 'Error' -Category 'Update'

        # Attempt restore if backup was created
        if ($CreateBackup -and $backupPath) {
            Write-Status -Message "Attempting to restore from backup..." -Level 'Warning' -Category 'Update'
            try {
                Restore-PreviousVersion -BackupPath $backupPath
            } catch {
                Write-Status -Message "Restore also failed. Manual intervention required." -Level 'Error' -Category 'Update'
            }
        }

        return $false
    }
}

# === CONFIGURATION FUNCTIONS ===

function Set-UpdateConfiguration {
    <#
    .SYNOPSIS
        Updates the update manager configuration.

    .PARAMETER GitHubOwner
        GitHub repository owner.

    .PARAMETER GitHubRepo
        GitHub repository name.

    .PARAMETER AutoCheckEnabled
        Enable automatic update checks.

    .PARAMETER CheckIntervalHours
        Hours between automatic checks.

    .PARAMETER IncludePrerelease
        Include prerelease versions.

    .EXAMPLE
        Set-UpdateConfiguration -AutoCheckEnabled $true -CheckIntervalHours 12
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$GitHubOwner,

        [Parameter()]
        [string]$GitHubRepo,

        [Parameter()]
        [bool]$AutoCheckEnabled,

        [Parameter()]
        [ValidateRange(1, 168)]
        [int]$CheckIntervalHours,

        [Parameter()]
        [bool]$IncludePrerelease
    )

    if ($PSBoundParameters.ContainsKey('GitHubOwner')) {
        $script:UpdateConfig.GitHubOwner = $GitHubOwner
    }

    if ($PSBoundParameters.ContainsKey('GitHubRepo')) {
        $script:UpdateConfig.GitHubRepo = $GitHubRepo
    }

    if ($PSBoundParameters.ContainsKey('AutoCheckEnabled')) {
        $script:UpdateConfig.AutoCheckEnabled = $AutoCheckEnabled
    }

    if ($PSBoundParameters.ContainsKey('CheckIntervalHours')) {
        $script:UpdateConfig.CheckIntervalHours = $CheckIntervalHours
    }

    if ($PSBoundParameters.ContainsKey('IncludePrerelease')) {
        $script:UpdateConfig.IncludePrerelease = $IncludePrerelease
    }

    Write-Verbose "Update configuration updated"
}

function Get-UpdateConfiguration {
    <#
    .SYNOPSIS
        Returns the current update configuration.

    .OUTPUTS
        Hashtable with configuration values.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    return $script:UpdateConfig.Clone()
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    # Version functions
    'Get-CurrentVersion',
    'Compare-SemanticVersions',
    'Test-IsNewerVersion',
    # Batch update cache (optimization)
    'Get-WingetUpdatesBatch',
    'Get-ApplicationUpdateStatus',
    'Clear-BatchUpdateCache',
    # Update check functions
    'Get-LatestReleaseInfo',
    'Test-UpdateAvailable',
    'Test-ShouldCheckForUpdates',
    # Download and install
    'Invoke-DownloadUpdate',
    'Backup-CurrentVersion',
    'Restore-PreviousVersion',
    'Get-AvailableBackups',
    'Install-Update',
    # Configuration
    'Set-UpdateConfiguration',
    'Get-UpdateConfiguration'
)
