<#
.SYNOPSIS
    Win11Forge - Start Menu Pinning v3.6.8

.DESCRIPTION
    Module for managing Windows 11 Start Menu pinned items using start2.bin/start.bin method:
    - Captures current user's Start Menu layout (start2.bin)
    - Deploys layout to Default profile for new users
    - Works reliably on Windows 11 22H2+ (unlike deprecated LayoutModification.json)

.NOTES
    Author: Julien Bombled
    v3.6.8
    Requires: PowerShell 5.1+, Windows 11, Administrator privileges
    Method: start2.bin binary file copy (most reliable method as of 2024-2025)

    Context: LayoutModification.json is broken/deprecated in Windows 11 since:
    - June 2025 cumulative update
    - Windows 11 24H2
    - Export-StartLayout cmdlet is deprecated

    This module uses the working alternative: copying start2.bin/start.bin
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
$script:ModuleRoot = $PSScriptRoot
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

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

# === CONSTANTS ===

# Start Menu data paths (current user)
$script:StartMenuDataPaths = @{
    # Windows 11 22H2+ uses start2.bin
    Start2 = Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin'
    # Windows 11 21H2 uses start.bin
    Start1 = Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start.bin'
}

# Default profile paths (for new users)
$script:DefaultProfilePaths = @{
    Start2 = "$env:SystemDrive\Users\Default\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin"
    Start1 = "$env:SystemDrive\Users\Default\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start.bin"
}

# Backup directory
$script:BackupDirectory = Join-Path $script:RepositoryRoot 'Backups\StartMenuLayouts'

# === HELPER FUNCTIONS ===

<#
.SYNOPSIS
    Detects which Start Menu binary file is in use
.DESCRIPTION
    Windows 11 versions use different files:
    - 21H2: start.bin
    - 22H2+: start2.bin
.EXAMPLE
    $file = Get-StartMenuBinaryType
    # Returns: "Start2" or "Start1"
#>
function Get-StartMenuBinaryType {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    # Check start2.bin first (22H2+)
    if (Test-Path $script:StartMenuDataPaths.Start2) {
        return 'Start2'
    }

    # Fallback to start.bin (21H2)
    if (Test-Path $script:StartMenuDataPaths.Start1) {
        return 'Start1'
    }

    # None found
    return $null
}

<#
.SYNOPSIS
    Gets the path to the current user's Start Menu binary file
.DESCRIPTION
    Returns the full path to start2.bin or start.bin depending on Windows version
.EXAMPLE
    $path = Get-CurrentUserStartMenuBinary
#>
function Get-CurrentUserStartMenuBinary {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $binaryType = Get-StartMenuBinaryType

    if (-not $binaryType) {
        return $null
    }

    return $script:StartMenuDataPaths[$binaryType]
}

<#
.SYNOPSIS
    Gets the path to the Default profile's Start Menu binary file
.DESCRIPTION
    Returns the target path for deploying to Default user profile
.EXAMPLE
    $path = Get-DefaultProfileStartMenuBinary
#>
function Get-DefaultProfileStartMenuBinary {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $binaryType = Get-StartMenuBinaryType

    if (-not $binaryType) {
        # Default to Start2 for newer Windows 11
        $binaryType = 'Start2'
    }

    return $script:DefaultProfilePaths[$binaryType]
}

<#
.SYNOPSIS
    Captures the current user's Start Menu layout
.DESCRIPTION
    Copies start2.bin/start.bin to a backup location for later deployment
.PARAMETER BackupName
    Optional name for the backup (default: timestamp)
.EXAMPLE
    Backup-StartMenuLayout
.EXAMPLE
    Backup-StartMenuLayout -BackupName "ProductionLayout"
#>
function Backup-StartMenuLayout {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$BackupName
    )

    Write-Status -Message (Get-LocalizedString -Key 'startmenu.capturing') -Level 'Info'

    # Find current user's binary
    $sourcePath = Get-CurrentUserStartMenuBinary

    if (-not $sourcePath) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.binary_not_found') -Level 'Error'
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.incompatible_version') -Level 'Error'
        return $false
    }

    if (-not (Test-Path $sourcePath)) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.binary_not_at_path' -Parameters @{ Path = $sourcePath }) -Level 'Error'
        return $false
    }

    # Create backup directory
    if (-not (Test-Path $script:BackupDirectory)) {
        New-Item -Path $script:BackupDirectory -ItemType Directory -Force | Out-Null
    }

    # Generate backup filename
    if (-not $BackupName) {
        $BackupName = Get-Date -Format 'yyyyMMdd_HHmmss'
    }

    $binaryType = Get-StartMenuBinaryType
    $backupFileName = "${BackupName}_$binaryType.bin"
    $backupPath = Join-Path $script:BackupDirectory $backupFileName

    # Copy binary
    try {
        Copy-Item -Path $sourcePath -Destination $backupPath -Force -ErrorAction Stop

        $fileSize = (Get-Item $backupPath).Length
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.captured_success' -Parameters @{ FileName = $backupFileName; Size = $fileSize }) -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.backup_location' -Parameters @{ Path = $backupPath }) -Level 'Info'

        return $backupPath
    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.backup_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        return $false
    }
}

<#
.SYNOPSIS
    Deploys a Start Menu layout to the Default profile
.DESCRIPTION
    Copies start2.bin/start.bin to the Default user profile so new users get this layout
.PARAMETER SourcePath
    Path to the binary file to deploy (from backup or current user)
.PARAMETER UseCurrentUser
    If specified, uses the current user's layout directly
.EXAMPLE
    Deploy-StartMenuLayoutToDefault -UseCurrentUser
.EXAMPLE
    Deploy-StartMenuLayoutToDefault -SourcePath "C:\Backups\layout.bin"
#>
function Deploy-StartMenuLayoutToDefault {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$SourcePath,

        [Parameter()]
        [switch]$UseCurrentUser
    )

    Write-Status -Message (Get-LocalizedString -Key 'startmenu.deploying_default') -Level 'Info'

    # Determine source
    if ($UseCurrentUser) {
        $SourcePath = Get-CurrentUserStartMenuBinary

        if (-not $SourcePath) {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.current_binary_not_found') -Level 'Error'
            return $false
        }

        Write-Status -Message (Get-LocalizedString -Key 'startmenu.using_current_layout' -Parameters @{ Path = $SourcePath }) -Level 'Info'
    }

    # Validate source
    if (-not $SourcePath -or -not (Test-Path $SourcePath)) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.source_not_found' -Parameters @{ Path = $SourcePath }) -Level 'Error'
        return $false
    }

    # Get target path
    $targetPath = Get-DefaultProfileStartMenuBinary
    $targetDir = Split-Path $targetPath -Parent

    Write-Status -Message (Get-LocalizedString -Key 'startmenu.target_location' -Parameters @{ Path = $targetPath }) -Level 'Info'

    # Create target directory if needed
    if (-not (Test-Path $targetDir)) {
        try {
            New-Item -Path $targetDir -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.created_target_dir' -Parameters @{ Path = $targetDir }) -Level 'Success'
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.create_dir_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
            return $false
        }
    }

    # Backup existing Default profile layout if it exists
    if (Test-Path $targetPath) {
        $backupName = "DefaultProfile_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        $backupPath = Join-Path $script:BackupDirectory "$backupName.bin"

        try {
            if (-not (Test-Path $script:BackupDirectory)) {
                New-Item -Path $script:BackupDirectory -ItemType Directory -Force | Out-Null
            }

            Copy-Item -Path $targetPath -Destination $backupPath -Force -ErrorAction Stop
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.existing_backed_up' -Parameters @{ Path = $backupPath }) -Level 'Info'
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.backup_existing_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning'
        }
    }

    # Deploy layout
    try {
        Copy-Item -Path $SourcePath -Destination $targetPath -Force -ErrorAction Stop

        $fileSize = (Get-Item $targetPath).Length
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.deployed_success' -Parameters @{ Size = $fileSize }) -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.new_users_inherit') -Level 'Success'

        return $true
    }
    catch {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.deploy_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        return $false
    }
}

<#
.SYNOPSIS
    Lists all backed up Start Menu layouts
.DESCRIPTION
    Shows all captured layouts in the backup directory
.EXAMPLE
    Get-BackedUpLayouts
#>
function Get-BackedUpLayouts {
    [CmdletBinding()]
    param()

    if (-not (Test-Path $script:BackupDirectory)) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.no_backups_dir') -Level 'Info'
        return @()
    }

    $backups = Get-ChildItem -Path $script:BackupDirectory -Filter "*.bin" -ErrorAction SilentlyContinue

    if (-not $backups -or $backups.Count -eq 0) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.no_backups_found' -Parameters @{ Path = $script:BackupDirectory }) -Level 'Info'
        return @()
    }

    Write-Status -Message (Get-LocalizedString -Key 'startmenu.found_backups' -Parameters @{ Count = $backups.Count }) -Level 'Info'

    foreach ($backup in $backups) {
        $size = [math]::Round($backup.Length / 1KB, 2)
        Write-Status -Message "  - $($backup.Name) ($size KB) - Modified: $($backup.LastWriteTime)" -Level 'Info'
    }

    return $backups
}

<#
.SYNOPSIS
    Complete workflow to capture and deploy Start Menu layout
.DESCRIPTION
    Main function that:
    1. Captures current user's Start Menu layout
    2. Backs it up
    3. Deploys to Default profile for new users
    4. Optionally applies to current user
.PARAMETER BackupName
    Optional name for the backup
.PARAMETER SkipBackup
    Skip backup step and deploy directly
.PARAMETER ApplyToCurrentUser
    Also apply the layout to the current user (requires restart of Start Menu)
.EXAMPLE
    Invoke-StartMenuPinning
.EXAMPLE
    Invoke-StartMenuPinning -BackupName "GamingProfile"
.EXAMPLE
    Invoke-StartMenuPinning -ApplyToCurrentUser
#>
function Invoke-StartMenuPinning {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$BackupName,

        [Parameter()]
        [switch]$SkipBackup,

        [Parameter()]
        [switch]$ApplyToCurrentUser
    )

    Write-Status -Message (Get-LocalizedString -Key 'startmenu.bin_method_title') -Level 'Info'
    Write-Host ""

    # Step 1: Backup current layout (optional)
    $backupPath = $null

    if (-not $SkipBackup) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.step1_backup') -Level 'Info'
        $backupPath = Backup-StartMenuLayout -BackupName $BackupName

        if (-not $backupPath) {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.backup_abort') -Level 'Error'
            return $false
        }

        Write-Host ""
    }

    # Step 2: Deploy to Default profile
    Write-Status -Message (Get-LocalizedString -Key 'startmenu.step2_deploy') -Level 'Info'

    if ($backupPath) {
        $deployed = Deploy-StartMenuLayoutToDefault -SourcePath $backupPath
    }
    else {
        $deployed = Deploy-StartMenuLayoutToDefault -UseCurrentUser
    }

    Write-Host ""

    # Step 3: Apply to current user (optional)
    if ($ApplyToCurrentUser -and $deployed) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.step3_apply') -Level 'Info'

        $sourcePath = if ($backupPath) { $backupPath } else { Get-DefaultProfileStartMenuBinary }
        $targetPath = Get-CurrentUserStartMenuBinary

        try {
            # Backup current user's layout first
            $currentBackup = Join-Path $script:BackupDirectory "CurrentUser_BeforeApply_$(Get-Date -Format 'yyyyMMdd_HHmmss').bin"
            if (Test-Path $targetPath) {
                Copy-Item -Path $targetPath -Destination $currentBackup -Force -ErrorAction Stop
                Write-Status -Message (Get-LocalizedString -Key 'startmenu.current_backed_up' -Parameters @{ Path = $currentBackup }) -Level 'Info'
            }

            # Apply new layout
            Copy-Item -Path $sourcePath -Destination $targetPath -Force -ErrorAction Stop
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.applied_current') -Level 'Success'

            # Restart Start Menu
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.restarting') -Level 'Info'
            try {
                Stop-Process -Name StartMenuExperienceHost -Force -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 500
                Write-Status -Message (Get-LocalizedString -Key 'startmenu.restart_success') -Level 'Success'
            }
            catch {
                Write-Status -Message (Get-LocalizedString -Key 'startmenu.restart_manual') -Level 'Warning'
            }
        }
        catch {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.apply_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        }

        Write-Host ""
    }

    # Summary
    if ($deployed) {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.summary_title') -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.summary_captured') -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.summary_deployed') -Level 'Success'

        if ($ApplyToCurrentUser) {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.summary_applied') -Level 'Success'
        }

        Write-Host ""
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.new_users_inherit') -Level 'Info'

        if (-not $ApplyToCurrentUser) {
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.current_unchanged') -Level 'Info'
        }

        if ($backupPath) {
            Write-Host ""
            Write-Status -Message (Get-LocalizedString -Key 'startmenu.backup_saved' -Parameters @{ Path = $backupPath }) -Level 'Info'
        }

        return $true
    }
    else {
        Write-Status -Message (Get-LocalizedString -Key 'startmenu.deployment_failed') -Level 'Error'
        return $false
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-StartMenuBinaryType',
    'Get-CurrentUserStartMenuBinary',
    'Get-DefaultProfileStartMenuBinary',
    'Backup-StartMenuLayout',
    'Deploy-StartMenuLayoutToDefault',
    'Get-BackedUpLayouts',
    'Invoke-StartMenuPinning'
)
