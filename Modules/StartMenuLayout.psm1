<#
.SYNOPSIS
    WinForge - Start Menu Layout Manager v3.7.2

.DESCRIPTION
    Module for managing Windows 11 Start Menu layout using LayoutModification.json:
    - Generates LayoutModification.json from desktop shortcuts
    - Organizes shortcuts by category in folders
    - Deploys layout for current user or Default profile
    - Uses official Microsoft method (LayoutModification.json)

.NOTES
    Author: Julien Bombled
    v3.7.2
    Requires: PowerShell 5.1+, Windows 11, Administrator privileges
    Method: LayoutModification.json (official Microsoft method)
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
$script:DesktopPaths = @(
    [Environment]::GetFolderPath('Desktop'),
    [Environment]::GetFolderPath('CommonDesktopDirectory')
)

$script:StartMenuPaths = @{
    User = [Environment]::GetFolderPath('StartMenu')
    Common = [Environment]::GetFolderPath('CommonStartMenu')
}

$script:ProgramsFolder = Join-Path $script:StartMenuPaths.Common 'Programs'
# Windows system path - intentional direct env usage
$script:LayoutPath = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Shell\LayoutModification.json'
$script:DefaultLayoutPath = "$env:SystemDrive\Users\Default\AppData\Local\Microsoft\Windows\Shell\LayoutModification.json"

# === HELPER FUNCTIONS ===

<#
.SYNOPSIS
    Gets all desktop shortcuts
.DESCRIPTION
    Scans desktop folders and returns all .lnk files
.EXAMPLE
    $shortcuts = Get-DesktopShortcuts
#>
function Get-DesktopShortcuts {
    [CmdletBinding()]
    [OutputType([System.IO.FileInfo[]])]
    param()

    $shortcuts = @()

    foreach ($desktopPath in $script:DesktopPaths) {
        if (Test-Path $desktopPath) {
            $found = Get-ChildItem -Path $desktopPath -Filter "*.lnk" -ErrorAction SilentlyContinue
            if ($found) {
                $shortcuts += $found
            }
        }
    }

    return $shortcuts
}

<#
.SYNOPSIS
    Gets the category for an application by name
.DESCRIPTION
    Looks up the application in the database to find its category
.PARAMETER ApplicationName
    The name of the application or shortcut name
.EXAMPLE
    $category = Get-ApplicationCategory -ApplicationName "Google Chrome"
#>
function Get-ApplicationCategory {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApplicationName
    )

    # Import ApplicationDatabase if not already loaded
    if (-not (Get-Command -Name Get-ApplicationDatabase -ErrorAction SilentlyContinue)) {
        $dbModule = Join-Path $script:ModuleRoot 'ApplicationDatabase.psm1'
        if (Test-Path $dbModule) {
            Import-Module $dbModule -Force
        }
    }

    try {
        $database = Get-ApplicationDatabase
        if ($null -eq $database) {
            return 'Other'
        }

        # Try to find matching application in database
        foreach ($app in $database.Applications.PSObject.Properties) {
            $appData = $app.Value

            # Match by exact name or if shortcut name contains app name
            if ($appData.Name -eq $ApplicationName -or
                $ApplicationName -like "*$($appData.Name)*" -or
                $appData.Name -like "*$ApplicationName*") {

                if ($appData.Category) {
                    return $appData.Category
                }
            }
        }
    }
    catch {
        Write-Status -Message (t 'startmenu.layout.category.lookup.error' @{ ErrorMessage = $_.Exception.Message }) -Level 'Verbose'
    }

    # Default category mapping based on common keywords
    $categoryMappings = @{
        'Chrome|Firefox|Brave|Edge|Opera' = 'Browser'
        'VSCode|Visual Studio|Sublime|Notepad\+\+|Atom|MobaXterm' = 'Development'
        'Steam|Epic|Battle|Origin|Uplay|GOG' = 'Gaming'
        'VLC|Media Player|Spotify|iTunes|FastStone|Paint\.NET' = 'Media'
        '7-Zip|WinRAR|WinZip|ShareX|WizTree|Everything|IDM' = 'Utility'
        'Word|Excel|PowerPoint|Outlook|OneNote' = 'Office'
        'Photoshop|GIMP|Illustrator|Blender' = 'Graphics'
        'Discord|Teams|Slack|Zoom|Skype|Signal' = 'Communication'
        'Proton|Malwarebytes' = 'Security'
        'WinSCP|Advanced IP Scanner' = 'Network'
        'CrystalDiskInfo|Process Hacker' = 'Diagnostic'
        'pCloud|Drive' = 'CloudStorage'
        'MediaMonkey|Mp3tag' = 'Multimedia'
        'OBS' = 'Recording'
        'PDF-XChange|Adobe' = 'Productivity'
        'Recuva' = 'Recovery'
        'Winaero' = 'Configuration'
    }

    foreach ($pattern in $categoryMappings.Keys) {
        if ($ApplicationName -match $pattern) {
            return $categoryMappings[$pattern]
        }
    }

    return 'Other'
}

<#
.SYNOPSIS
    Resolves a shortcut to get its target and AppID
.DESCRIPTION
    Gets the target executable from a .lnk file and attempts to determine its AppID
.PARAMETER ShortcutPath
    Full path to the .lnk file
.EXAMPLE
    $info = Get-ShortcutInfo -ShortcutPath "C:\Users\Public\Desktop\Chrome.lnk"
#>
function Get-ShortcutInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath
    )

    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($ShortcutPath)

        $info = @{
            Name = [System.IO.Path]::GetFileNameWithoutExtension($ShortcutPath)
            TargetPath = $shortcut.TargetPath
            Arguments = $shortcut.Arguments
            WorkingDirectory = $shortcut.WorkingDirectory
            IconLocation = $shortcut.IconLocation
            ShortcutPath = $ShortcutPath
        }

        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null

        return $info
    }
    catch {
        Write-Status -Message (t 'startmenu.layout.shortcut.read.error' @{ ShortcutPath = $ShortcutPath; ErrorMessage = $_.Exception.Message }) -Level 'Verbose'
        return $null
    }
}

<#
.SYNOPSIS
    Gets the AUMID for a packaged app
.DESCRIPTION
    Attempts to find the AUMID for UWP/Store apps
.PARAMETER AppName
    Name of the application
.EXAMPLE
    $aumid = Get-PackagedAppId -AppName "Terminal"
#>
function Get-PackagedAppId {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppName
    )

    try {
        # Get all installed packaged apps
        $apps = Get-StartApps | Where-Object { $_.Name -like "*$AppName*" }

        if ($apps -and $apps.Count -gt 0) {
            return $apps[0].AppID
        }
    }
    catch {
        Write-Status -Message (t 'startmenu.layout.packagedapp.find.error' @{ ErrorMessage = $_.Exception.Message }) -Level 'Verbose'
    }

    return $null
}

<#
.SYNOPSIS
    Copies shortcuts to Start Menu Programs folder
.DESCRIPTION
    Ensures shortcuts are available in the Start Menu Programs directory
.PARAMETER ShortcutPath
    Path to the shortcut
.PARAMETER Category
    Category folder name
.EXAMPLE
    $path = Copy-ShortcutToStartMenu -ShortcutPath "C:\Users\Public\Desktop\App.lnk" -Category "Utilities"
#>
function Copy-ShortcutToStartMenu {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,

        [Parameter(Mandatory = $true)]
        [string]$Category
    )

    try {
        # Create category folder in Start Menu
        $categoryFolder = Join-Path $script:ProgramsFolder $Category
        if (-not (Test-Path $categoryFolder)) {
            New-Item -Path $categoryFolder -ItemType Directory -Force | Out-Null
        }

        # Copy shortcut
        $shortcutName = [System.IO.Path]::GetFileName($ShortcutPath)
        $destinationPath = Join-Path $categoryFolder $shortcutName

        Copy-Item -Path $ShortcutPath -Destination $destinationPath -Force -ErrorAction Stop

        return $destinationPath
    }
    catch {
        Write-Status -Message (t 'startmenu.layout.shortcut.copy.error' @{ ErrorMessage = $_.Exception.Message }) -Level 'Error'
        return $null
    }
}

<#
.SYNOPSIS
    Generates LayoutModification.json from categorized shortcuts
.DESCRIPTION
    Creates the JSON structure for Windows 11 Start Menu layout with folders
.PARAMETER CategorizedShortcuts
    Hashtable of categories with their shortcuts
.EXAMPLE
    $json = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
#>
function New-LayoutModificationJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$CategorizedShortcuts
    )

    $pinnedList = @()

    foreach ($category in ($CategorizedShortcuts.Keys | Sort-Object)) {
        $shortcuts = $CategorizedShortcuts[$category]

        if ($shortcuts.Count -eq 0) {
            continue
        }

        $folderItems = @()

        foreach ($shortcutPath in $shortcuts) {
            if (-not (Test-Path $shortcutPath)) {
                continue
            }

            # Create desktopAppLink entry
            $item = @{
                desktopAppLink = $shortcutPath
            }

            $folderItems += $item
        }

        if ($folderItems.Count -gt 0) {
            # Create folder for this category
            $folder = @{
                folder = @{
                    displayName = $category
                    items = $folderItems
                }
            }

            $pinnedList += $folder
        }
    }

    $layout = @{
        pinnedList = $pinnedList
    }

    # Convert to JSON with proper formatting
    $json = $layout | ConvertTo-Json -Depth 10 -Compress:$false

    return $json
}

<#
.SYNOPSIS
    Deploys the LayoutModification.json file
.DESCRIPTION
    Copies the layout file to the appropriate location and restarts Start Menu
.PARAMETER JsonContent
    The JSON content to deploy
.PARAMETER ForDefaultProfile
    If specified, deploys to Default user profile for new users
.EXAMPLE
    Set-StartMenuLayout -JsonContent $json
#>
function Set-StartMenuLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$JsonContent,

        [Parameter()]
        [switch]$ForDefaultProfile
    )

    try {
        if ($ForDefaultProfile) {
            # Deploy to Default profile (requires admin)
            $targetPath = $script:DefaultLayoutPath
            $targetDir = Split-Path $targetPath -Parent

            if (-not (Test-Path $targetDir)) {
                New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
            }

            $JsonContent | Out-File -FilePath $targetPath -Encoding UTF8 -Force
            Write-Status -Message (t 'startmenu.layout.deploy.defaultprofile.success' @{ TargetPath = $targetPath }) -Level 'Success'
        }
        else {
            # Deploy to current user
            $targetPath = $script:LayoutPath
            $targetDir = Split-Path $targetPath -Parent

            if (-not (Test-Path $targetDir)) {
                New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
            }

            $JsonContent | Out-File -FilePath $targetPath -Encoding UTF8 -Force
            Write-Status -Message (t 'startmenu.layout.deploy.currentuser.success' @{ TargetPath = $targetPath }) -Level 'Success'

            # Restart Start Menu
            Write-Status -Message (t 'startmenu.layout.restart.starting') -Level 'Info'
            try {
                Stop-Process -Name StartMenuExperienceHost -Force -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 500
                Start-Process explorer.exe -ErrorAction SilentlyContinue
                Write-Status -Message (t 'startmenu.layout.restart.success') -Level 'Success'
            }
            catch {
                Write-Status -Message (t 'startmenu.layout.restart.failed') -Level 'Warning'
            }
        }

        return $true
    }
    catch {
        Write-Status -Message (t 'startmenu.layout.deploy.error' @{ ErrorMessage = $_.Exception.Message }) -Level 'Error'
        return $false
    }
}

<#
.SYNOPSIS
    Organizes desktop shortcuts into Start Menu layout by category
.DESCRIPTION
    Main function that:
    1. Scans desktop for shortcuts
    2. Categorizes shortcuts
    3. Copies shortcuts to Start Menu Programs folder
    4. Generates LayoutModification.json
    5. Deploys the layout
.PARAMETER ForDefaultProfile
    If specified, deploys layout for new user profiles (requires admin)
.PARAMETER ExcludePatterns
    Array of patterns to exclude
.EXAMPLE
    Invoke-StartMenuOrganization
.EXAMPLE
    Invoke-StartMenuOrganization -ForDefaultProfile
#>
function Invoke-StartMenuOrganization {
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$ForDefaultProfile,

        [Parameter()]
        [string[]]$ExcludePatterns = @('*uninstall*', '*uninst*', '*remove*', '*help*', '*readme*')
    )

    Write-Status -Message (t 'startmenu.layout.organization.starting') -Level 'Info'
    Write-Status -Message (t 'startmenu.layout.shortcuts.scanning') -Level 'Info'

    $shortcuts = Get-DesktopShortcuts

    # Handle $null, single object, or array
    if (-not $shortcuts) {
        Write-Status -Message (t 'startmenu.layout.shortcuts.notfound') -Level 'Warning'
        return
    }

    # Ensure it's an array
    $shortcutArray = @($shortcuts)

    if ($shortcutArray.Count -eq 0) {
        Write-Status -Message (t 'startmenu.layout.shortcuts.notfound') -Level 'Warning'
        return
    }

    Write-Status -Message (t 'startmenu.layout.shortcuts.found' @{ Count = $shortcutArray.Count }) -Level 'Info'

    $stats = @{
        Total = $shortcutArray.Count
        Copied = 0
        Skipped = 0
        Failed = 0
    }

    $categorizedShortcuts = @{}

    foreach ($shortcut in $shortcutArray) {
        $shortcutName = $shortcut.Name

        # Check exclusion patterns
        $excluded = $false
        foreach ($pattern in $ExcludePatterns) {
            if ($shortcutName -like $pattern) {
                Write-Status -Message (t 'startmenu.layout.shortcut.skipped' @{ ShortcutName = $shortcutName }) -Level 'Verbose'
                $stats.Skipped++
                $excluded = $true
                break
            }
        }

        if ($excluded) {
            continue
        }

        # Get category
        $appName = [System.IO.Path]::GetFileNameWithoutExtension($shortcutName)
        $category = Get-ApplicationCategory -ApplicationName $appName

        # Initialize category if needed
        if (-not $categorizedShortcuts.ContainsKey($category)) {
            $categorizedShortcuts[$category] = @()
        }

        # Copy to Start Menu Programs folder
        $destinationPath = Copy-ShortcutToStartMenu -ShortcutPath $shortcut.FullName -Category $category

        if ($destinationPath) {
            $categorizedShortcuts[$category] += $destinationPath
            $stats.Copied++
            Write-Status -Message (t 'startmenu.layout.shortcut.copied' @{ ShortcutName = $shortcutName; Category = $category }) -Level 'Verbose'
        }
        else {
            $stats.Failed++
        }
    }

    # Generate LayoutModification.json
    Write-Status -Message (t 'startmenu.layout.json.generating') -Level 'Info'
    $jsonContent = New-LayoutModificationJson -CategorizedShortcuts $categorizedShortcuts

    # Deploy layout
    Write-Status -Message (t 'startmenu.layout.deploy.starting') -Level 'Info'
    $deployed = Set-StartMenuLayout -JsonContent $jsonContent -ForDefaultProfile:$ForDefaultProfile

    # Summary
    Write-Status -Message (t 'startmenu.layout.summary.header') -Level 'Info'
    Write-Status -Message (t 'startmenu.layout.summary.total' @{ Total = $stats.Total }) -Level 'Info'
    Write-Status -Message (t 'startmenu.layout.summary.copied' @{ Copied = $stats.Copied }) -Level 'Success'

    if ($stats.Skipped -gt 0) {
        Write-Status -Message (t 'startmenu.layout.summary.skipped' @{ Skipped = $stats.Skipped }) -Level 'Info'
    }

    if ($stats.Failed -gt 0) {
        Write-Status -Message (t 'startmenu.layout.summary.failed' @{ Failed = $stats.Failed }) -Level 'Warning'
    }

    Write-Status -Message (t 'startmenu.layout.summary.categories.header') -Level 'Info'
    foreach ($category in $categorizedShortcuts.Keys | Sort-Object) {
        $count = $categorizedShortcuts[$category].Count
        Write-Status -Message (t 'startmenu.layout.summary.categories.item' @{ Category = $category; Count = $count }) -Level 'Info'
    }

    if ($deployed) {
        Write-Status -Message (t 'startmenu.layout.deploy.success') -Level 'Success'
        if ($ForDefaultProfile) {
            Write-Status -Message (t 'startmenu.layout.deploy.defaultprofile.info') -Level 'Info'
        }
        else {
            Write-Status -Message (t 'startmenu.layout.deploy.currentuser.info') -Level 'Info'
        }
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-DesktopShortcuts',
    'Get-ApplicationCategory',
    'Get-ShortcutInfo',
    'Get-PackagedAppId',
    'Copy-ShortcutToStartMenu',
    'New-LayoutModificationJson',
    'Set-StartMenuLayout',
    'Invoke-StartMenuOrganization'
)
