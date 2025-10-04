<#
.SYNOPSIS
    Win11Forge - Start Menu Manager Module v1.0.0

.DESCRIPTION
    Module for managing Windows 11 Start Menu shortcuts and folders:
    - Detects desktop shortcuts
    - Pins shortcuts to Start Menu
    - Organizes shortcuts by category in folders
    - Creates category-based folder structure

.NOTES
    Version: 1.0.0
    Requires: PowerShell 5.1+, Administrator privileges
#>

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path (Split-Path $script:ModuleRoot -Parent) -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
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
        Write-Status -Message "Error looking up category: $($_.Exception.Message)" -Level 'Verbose'
    }

    # Default category mapping based on common keywords
    $categoryMappings = @{
        'Chrome|Firefox|Brave|Edge|Opera' = 'Browser'
        'VSCode|Visual Studio|Sublime|Notepad\+\+|Atom' = 'Development'
        'Steam|Epic|Battle|Origin|Uplay|GOG' = 'Gaming'
        'VLC|Media Player|Spotify|iTunes' = 'Media'
        '7-Zip|WinRAR|WinZip' = 'Utilities'
        'Word|Excel|PowerPoint|Outlook|OneNote' = 'Office'
        'Photoshop|GIMP|Illustrator|Blender' = 'Graphics'
        'Discord|Teams|Slack|Zoom|Skype' = 'Communication'
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
    Creates a folder in the Start Menu Programs directory
.DESCRIPTION
    Creates a category folder in Start Menu\Programs
.PARAMETER FolderName
    Name of the folder to create
.EXAMPLE
    New-StartMenuFolder -FolderName "Gaming"
#>
function New-StartMenuFolder {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FolderName
    )

    $folderPath = Join-Path $script:ProgramsFolder $FolderName

    if (-not (Test-Path $folderPath)) {
        try {
            New-Item -Path $folderPath -ItemType Directory -Force | Out-Null
            Write-Status -Message "Created Start Menu folder: $FolderName" -Level 'Success'
        }
        catch {
            Write-Status -Message "Failed to create folder $FolderName : $($_.Exception.Message)" -Level 'Error'
            return $null
        }
    }

    return $folderPath
}

<#
.SYNOPSIS
    Copies a shortcut to the Start Menu with category organization
.DESCRIPTION
    Copies a desktop shortcut to the appropriate category folder in Start Menu
.PARAMETER ShortcutPath
    Full path to the shortcut file
.PARAMETER Category
    Category name for organization
.EXAMPLE
    Copy-ShortcutToStartMenu -ShortcutPath "C:\Users\Public\Desktop\Chrome.lnk" -Category "Browser"
#>
function Copy-ShortcutToStartMenu {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,

        [Parameter(Mandatory = $false)]
        [string]$Category = 'Other'
    )

    if (-not (Test-Path $ShortcutPath)) {
        Write-Status -Message "Shortcut not found: $ShortcutPath" -Level 'Warning'
        return $false
    }

    try {
        # Create category folder
        $categoryFolder = New-StartMenuFolder -FolderName $Category
        if ($null -eq $categoryFolder) {
            return $false
        }

        # Get shortcut name
        $shortcutName = [System.IO.Path]::GetFileName($ShortcutPath)
        $destinationPath = Join-Path $categoryFolder $shortcutName

        # Copy shortcut (overwrite if exists)
        Copy-Item -Path $ShortcutPath -Destination $destinationPath -Force -ErrorAction Stop

        Write-Status -Message "Copied to Start Menu: $shortcutName -> $Category" -Level 'Success'
        return $true
    }
    catch {
        Write-Status -Message "Failed to copy shortcut: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

<#
.SYNOPSIS
    Pins a shortcut to the Windows 11 Start Menu
.DESCRIPTION
    Uses Windows Shell to pin shortcuts to Start Menu (Windows 11 compatible)
.PARAMETER ShortcutPath
    Full path to the shortcut file
.PARAMETER TimeoutSeconds
    Timeout in seconds for the pin operation (default: 3)
.EXAMPLE
    Pin-ToStartMenu -ShortcutPath "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Gaming\Steam.lnk"
#>
function Pin-ToStartMenu {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,

        [Parameter()]
        [int]$TimeoutSeconds = 3
    )

    if (-not (Test-Path $ShortcutPath)) {
        Write-Status -Message "Cannot pin - shortcut not found: $ShortcutPath" -Level 'Warning'
        return $false
    }

    try {
        # Use a job with timeout to prevent hanging
        $pinJob = Start-Job -ScriptBlock {
            param($Path)

            try {
                $shell = New-Object -ComObject Shell.Application
                $folder = $shell.Namespace([System.IO.Path]::GetDirectoryName($Path))
                $item = $folder.ParseName([System.IO.Path]::GetFileName($Path))

                if ($null -eq $item) {
                    return $false
                }

                # Find "Pin to Start" verb (language-independent method)
                $pinVerb = $item.Verbs() | Where-Object {
                    $_.Name -like "*Start*" -or
                    $_.Name -like "*Épingler*" -or
                    $_.Name -like "*Ancrer*"
                } | Select-Object -First 1

                if ($null -ne $pinVerb) {
                    $pinVerb.DoIt()
                    Start-Sleep -Milliseconds 500  # Brief delay for operation to complete
                    return $true
                }

                return $false
            }
            catch {
                return $false
            }
            finally {
                if ($shell) {
                    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($shell) | Out-Null
                }
            }
        } -ArgumentList $ShortcutPath

        # Wait for job with timeout
        $completed = Wait-Job -Job $pinJob -Timeout $TimeoutSeconds

        if ($null -eq $completed) {
            # Job timed out
            Stop-Job -Job $pinJob
            Remove-Job -Job $pinJob -Force
            Write-Status -Message "Pin operation timed out for: $([System.IO.Path]::GetFileNameWithoutExtension($ShortcutPath))" -Level 'Verbose'
            return $false
        }

        # Get job result
        $result = Receive-Job -Job $pinJob
        Remove-Job -Job $pinJob -Force

        if ($result) {
            Write-Status -Message "Pinned to Start: $([System.IO.Path]::GetFileNameWithoutExtension($ShortcutPath))" -Level 'Success'
            return $true
        }
        else {
            Write-Status -Message "Could not pin: $([System.IO.Path]::GetFileNameWithoutExtension($ShortcutPath))" -Level 'Verbose'
            return $false
        }
    }
    catch {
        Write-Status -Message "Error pinning shortcut: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

<#
.SYNOPSIS
    Organizes all desktop shortcuts into Start Menu by category
.DESCRIPTION
    Main function that:
    1. Scans desktop for shortcuts
    2. Determines category for each shortcut
    3. Creates category folders in Start Menu
    4. Copies shortcuts to appropriate folders
    5. Optionally pins shortcuts to Start Menu
.PARAMETER PinToStart
    If specified, also pins shortcuts to Start Menu (may be slow)
.PARAMETER MaxPinAttempts
    Maximum number of shortcuts to attempt pinning (default: 10, to avoid long waits)
.PARAMETER ExcludePatterns
    Array of patterns to exclude (e.g., "*uninstall*", "*readme*")
.EXAMPLE
    Invoke-StartMenuOrganization
.EXAMPLE
    Invoke-StartMenuOrganization -PinToStart -MaxPinAttempts 5
.EXAMPLE
    Invoke-StartMenuOrganization -ExcludePatterns @("*uninstall*", "*help*")
#>
function Invoke-StartMenuOrganization {
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$PinToStart,

        [Parameter()]
        [int]$MaxPinAttempts = 10,

        [Parameter()]
        [string[]]$ExcludePatterns = @('*uninstall*', '*uninst*', '*remove*', '*help*', '*readme*')
    )

    Write-Status -Message "Starting Start Menu organization..." -Level 'Info'
    Write-Status -Message "Scanning desktop shortcuts..." -Level 'Info'

    $shortcuts = Get-DesktopShortcuts

    if ($shortcuts.Count -eq 0) {
        Write-Status -Message "No desktop shortcuts found" -Level 'Warning'
        return
    }

    Write-Status -Message "Found $($shortcuts.Count) desktop shortcuts" -Level 'Info'

    $stats = @{
        Total = $shortcuts.Count
        Copied = 0
        Pinned = 0
        Skipped = 0
        Failed = 0
        PinSkipped = 0
    }

    $categorizedShortcuts = @{}
    $pinCount = 0

    foreach ($shortcut in $shortcuts) {
        $shortcutName = $shortcut.Name

        # Check exclusion patterns
        $excluded = $false
        foreach ($pattern in $ExcludePatterns) {
            if ($shortcutName -like $pattern) {
                Write-Status -Message "Skipped (excluded): $shortcutName" -Level 'Verbose'
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

        # Track for summary
        if (-not $categorizedShortcuts.ContainsKey($category)) {
            $categorizedShortcuts[$category] = @()
        }
        $categorizedShortcuts[$category] += $appName

        # Copy to Start Menu
        if (Copy-ShortcutToStartMenu -ShortcutPath $shortcut.FullName -Category $category) {
            $stats.Copied++

            # Pin to Start if requested (with limit to avoid long waits)
            if ($PinToStart -and $pinCount -lt $MaxPinAttempts) {
                $destinationPath = Join-Path (Join-Path $script:ProgramsFolder $category) $shortcutName
                if (Pin-ToStartMenu -ShortcutPath $destinationPath -TimeoutSeconds 2) {
                    $stats.Pinned++
                }
                $pinCount++
            }
            elseif ($PinToStart -and $pinCount -ge $MaxPinAttempts) {
                $stats.PinSkipped++
            }
        }
        else {
            $stats.Failed++
        }
    }

    # Summary
    Write-Status -Message "`n=== Start Menu Organization Summary ===" -Level 'Info'
    Write-Status -Message "Total shortcuts processed: $($stats.Total)" -Level 'Info'
    Write-Status -Message "Successfully copied: $($stats.Copied)" -Level 'Success'

    if ($PinToStart) {
        Write-Status -Message "Successfully pinned: $($stats.Pinned)" -Level 'Success'
        if ($stats.PinSkipped -gt 0) {
            Write-Status -Message "Pin skipped (limit reached): $($stats.PinSkipped)" -Level 'Info'
        }
    }

    if ($stats.Skipped -gt 0) {
        Write-Status -Message "Skipped (excluded): $($stats.Skipped)" -Level 'Info'
    }

    if ($stats.Failed -gt 0) {
        Write-Status -Message "Failed: $($stats.Failed)" -Level 'Warning'
    }

    Write-Status -Message "`nCategories created:" -Level 'Info'
    foreach ($category in $categorizedShortcuts.Keys | Sort-Object) {
        $count = $categorizedShortcuts[$category].Count
        Write-Status -Message "  - $category ($count apps)" -Level 'Info'
        foreach ($app in $categorizedShortcuts[$category]) {
            Write-Status -Message "    * $app" -Level 'Verbose'
        }
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-DesktopShortcuts',
    'Get-ApplicationCategory',
    'New-StartMenuFolder',
    'Copy-ShortcutToStartMenu',
    'Pin-ToStartMenu',
    'Invoke-StartMenuOrganization'
)
