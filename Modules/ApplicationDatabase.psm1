<#
.SYNOPSIS
    Win11Forge - Application Database v3.7.2

.DESCRIPTION
    Module for interacting with the centralized application database:
    - Database loading and caching
    - Application search and filtering
    - File watcher for automatic reload
    - Category and source management

.NOTES
    Author: Julien Bombled
    v3.7.2
    Last Updated: 2025-10-06
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

# Import Localization module for i18n support (optional - don't fail if not available)
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        try {
            Import-Module -Name $script:LocalizationModulePath -Force -ErrorAction SilentlyContinue
        } catch {
            Write-Verbose (t 'database.localization.import_failed' @{ Error = $PSItem.Exception.Message })
        }
    }
}

# Import DirectoryConstants for path management
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-Win11ForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

# Use $script:ModuleRoot for consistent path resolution
$Script:DatabasePath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
$Script:DatabaseCache = $null
$Script:DatabaseLastModified = $null
$Script:FileWatcher = $null
$Script:FileWatcherEnabled = $false

# === FILE WATCHER FUNCTIONS ===

function Enable-DatabaseFileWatcher {
    <#
    .SYNOPSIS
        Enables automatic database reload when the file changes.
    .DESCRIPTION
        Sets up a FileSystemWatcher to monitor the applications.json file
        and automatically invalidate the cache when changes are detected.
    #>
    [CmdletBinding()]
    param()

    if ($Script:FileWatcherEnabled) {
        Write-Verbose (t 'database.filewatcher.already_enabled')
        return
    }

    try {
        $databaseDir = Split-Path $Script:DatabasePath -Parent
        $databaseFile = Split-Path $Script:DatabasePath -Leaf

        $Script:FileWatcher = New-Object System.IO.FileSystemWatcher
        $Script:FileWatcher.Path = $databaseDir
        $Script:FileWatcher.Filter = $databaseFile
        $Script:FileWatcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite -bor [System.IO.NotifyFilters]::Size
        $Script:FileWatcher.EnableRaisingEvents = $true

        # Register event handler for file changes
        Register-ObjectEvent -InputObject $Script:FileWatcher -EventName Changed -Action {
            $Script:DatabaseCache = $null
            Write-Verbose (t 'database.filewatcher.cache_invalidated')
        } -SourceIdentifier 'Win11Forge.DatabaseWatcher' | Out-Null

        $Script:FileWatcherEnabled = $true
        Write-Verbose (t 'database.filewatcher.enabled' @{ Path = $Script:DatabasePath })
    } catch {
        Write-Warning (t 'database.filewatcher.enable_failed' @{ Error = $_.Exception.Message })
    }
}

function Disable-DatabaseFileWatcher {
    <#
    .SYNOPSIS
        Disables the database file watcher.
    .DESCRIPTION
        Unregisters the file system watcher event, disposes the watcher instance,
        and resets the watcher state. Silently handles cases where the watcher
        is already disabled or was never enabled.
    #>
    [CmdletBinding()]
    param()

    if (-not $Script:FileWatcherEnabled) {
        return
    }

    try {
        Unregister-Event -SourceIdentifier 'Win11Forge.DatabaseWatcher' -ErrorAction SilentlyContinue
        if ($Script:FileWatcher) {
            $Script:FileWatcher.EnableRaisingEvents = $false
            $Script:FileWatcher.Dispose()
            $Script:FileWatcher = $null
        }
        $Script:FileWatcherEnabled = $false
        Write-Verbose (t 'database.filewatcher.disabled')
    } catch {
        Write-Warning (t 'database.filewatcher.disable_failed' @{ Error = $_.Exception.Message })
    }
}

function Test-DatabaseFileChanged {
    <#
    .SYNOPSIS
        Checks if the database file has been modified since last load.
    .DESCRIPTION
        Compares the current LastWriteTime of the database JSON file against the
        timestamp recorded during the last load. Returns $false if the file does
        not exist or has never been loaded.
    .OUTPUTS
        Boolean indicating if file has changed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if (-not (Test-Path $Script:DatabasePath)) {
        return $false
    }

    $currentModified = (Get-Item $Script:DatabasePath).LastWriteTime
    if ($null -eq $Script:DatabaseLastModified) {
        return $false
    }

    return ($currentModified -gt $Script:DatabaseLastModified)
}

function Clear-DatabaseCache {
    <#
    .SYNOPSIS
        Clears the database cache, forcing a reload on next access.
    .DESCRIPTION
        Resets both the in-memory database cache and the last-modified timestamp,
        ensuring the next call to load the application database will re-read
        from disk rather than returning stale cached data.
    #>
    [CmdletBinding()]
    param()

    $Script:DatabaseCache = $null
    $Script:DatabaseLastModified = $null
    Write-Verbose (t 'database.cache.cleared')
}

<#
.SYNOPSIS
    Loads the application database from JSON file
.DESCRIPTION
    Reads and caches the centralized application database
.EXAMPLE
    $db = Get-ApplicationDatabase
#>
function Get-ApplicationDatabase {
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$ForceReload
    )

    try {
        # Check if reload is needed (manual or file watcher triggered)
        if ($ForceReload) {
            $Script:DatabaseCache = $null
        }

        # Return cached version if available
        if ($null -ne $Script:DatabaseCache) {
            return $Script:DatabaseCache
        }

        if (-not (Test-Path $Script:DatabasePath)) {
            throw (New-ValidationException -Message (t 'database.load.not_found' @{ Path = $Script:DatabasePath }))
        }

        $jsonContent = Get-Content -Path $Script:DatabasePath -Raw -Encoding UTF8
        $Script:DatabaseCache = $jsonContent | ConvertFrom-Json
        $Script:DatabaseLastModified = (Get-Item $Script:DatabasePath).LastWriteTime

        # PS5.1 compatible: PSCustomObject doesn't have .Count, use Measure-Object
        $appCount = ($Script:DatabaseCache.Applications.PSObject.Properties | Measure-Object).Count
        Write-Verbose (t 'database.load.success' @{ Count = $appCount })

        return $Script:DatabaseCache
    }
    catch {
        Write-Error (t 'database.load.failed' @{ Error = $_ })
        return $null
    }
}

<#
.SYNOPSIS
    Gets an application by its ID from the database
.DESCRIPTION
    Retrieves application information from the centralized database
.PARAMETER AppId
    The unique identifier for the application (e.g., "GoogleChrome", "VSCode")
.EXAMPLE
    $app = Get-ApplicationById -AppId "GoogleChrome"
#>
function Get-ApplicationById {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppId
    )

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return $null
    }

    $app = $database.Applications.PSObject.Properties | Where-Object { $_.Name -eq $AppId } | Select-Object -ExpandProperty Value -First 1

    if ($null -eq $app) {
        Write-Warning (t 'database.application.not_found' @{ AppId = $AppId })
        return $null
    }

    # Add the AppId to the returned object
    $app | Add-Member -NotePropertyName "AppId" -NotePropertyValue $AppId -Force

    return $app
}

<#
.SYNOPSIS
    Gets all applications from the database
.DESCRIPTION
    Returns all applications in the database, optionally filtered by category or tag
.PARAMETER Category
    Filter by category (e.g., "Browser", "Development", "Gaming")
.PARAMETER Tag
    Filter by tag (e.g., "essential", "popular", "open-source")
.PARAMETER Verified
    Only return verified applications
.EXAMPLE
    $allApps = Get-AllApplications
.EXAMPLE
    $browsers = Get-AllApplications -Category "Browser"
.EXAMPLE
    $essentialApps = Get-AllApplications -Tag "essential"
#>
function Get-AllApplications {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Category,

        [Parameter(Mandatory = $false)]
        [string]$Tag,

        [Parameter(Mandatory = $false)]
        [switch]$Verified
    )

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return @()
    }

    $apps = @()

    foreach ($prop in $database.Applications.PSObject.Properties) {
        $app = $prop.Value
        $app | Add-Member -NotePropertyName "AppId" -NotePropertyValue $prop.Name -Force

        # Apply filters
        $include = $true

        if ($Category -and $app.Category -ne $Category) {
            $include = $false
        }

        if ($Tag -and $app.Tags -notcontains $Tag) {
            $include = $false
        }

        if ($Verified -and -not $app.Verified) {
            $include = $false
        }

        if ($include) {
            $apps += $app
        }
    }

    return $apps
}

<#
.SYNOPSIS
    Searches for applications by name
.DESCRIPTION
    Performs a case-insensitive search for applications matching the search term
.PARAMETER SearchTerm
    The search term to match against application names
.EXAMPLE
    $results = Search-Applications -SearchTerm "chrome"
#>
function Search-Applications {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SearchTerm
    )

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return @()
    }

    $results = @()

    foreach ($prop in $database.Applications.PSObject.Properties) {
        $app = $prop.Value

        if ($app.Name -like "*$SearchTerm*") {
            $app | Add-Member -NotePropertyName "AppId" -NotePropertyValue $prop.Name -Force
            $results += $app
        }
    }

    return $results
}

<#
.SYNOPSIS
    Converts a database application to profile format
.DESCRIPTION
    Converts an application from the database format to the profile JSON format
.PARAMETER App
    The application object from the database
.PARAMETER Priority
    The priority to assign to the application
.PARAMETER Required
    Whether the application is required
.EXAMPLE
    $profileApp = ConvertTo-ProfileApplication -App $dbApp -Priority 10 -Required $true
#>
function ConvertTo-ProfileApplication {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$App,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Priority = $null,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Required = $null
    )

    # Use defaults from database if not specified
    # Allow explicit Priority = 0 (highest priority), only use default if truly not specified
    # Use [object] type with $null default to avoid PowerShell auto-coercion (int→0, bool→$false)
    if ($null -eq $Priority) {
        $Priority = $App.DefaultPriority
    } else {
        # Validate Priority is numeric
        if ($Priority -isnot [int] -and $Priority -isnot [long]) {
            try {
                $Priority = [int]$Priority
            } catch {
                throw (New-ValidationException -Message (t 'database.validation.priority_invalid' @{ Value = $Priority; Type = $Priority.GetType().Name }))
            }
        }
    }

    if ($null -eq $Required) {
        $Required = $App.DefaultRequired
    } else {
        # Validate Required is boolean or convertible to boolean
        if ($Required -isnot [bool]) {
            # Support numeric boolean: 0 = false, non-zero = true (common in JSON)
            if ($Required -is [int] -or $Required -is [long] -or $Required -is [byte]) {
                $Required = [bool]$Required
            } else {
                # Try string parsing: "true", "false", "0", "1"
                try {
                    if ($Required -match '^\s*(0|1)\s*$') {
                        $Required = [bool][int]$Required
                    } else {
                        $Required = [bool]::Parse($Required)
                    }
                } catch {
                    throw (New-ValidationException -Message (t 'database.validation.required_invalid' @{ Value = $Required; Type = $Required.GetType().Name }))
                }
            }
        }
    }

    $profileApp = [PSCustomObject]@{
        AppId                    = $App.AppId
        Name                     = $App.Name
        Priority                 = $Priority
        Required                 = $Required
        Category                 = $App.Category
        Sources                  = $App.Sources
        Detection                = $App.Detection
        EnvironmentRestrictions  = $App.EnvironmentRestrictions
    }

    # Add optional fields if they exist (PS5.1 compatible: check property existence first)
    if ($null -ne $App.PSObject.Properties['InstallMethod'] -and $App.InstallMethod) {
        $profileApp | Add-Member -NotePropertyName "InstallMethod" -NotePropertyValue $App.InstallMethod
    }

    if ($null -ne $App.PSObject.Properties['InstallArguments'] -and $App.InstallArguments) {
        $profileApp | Add-Member -NotePropertyName "InstallArguments" -NotePropertyValue $App.InstallArguments
    }

    if ($null -ne $App.PSObject.Properties['InstallationOptions'] -and $App.InstallationOptions) {
        $profileApp | Add-Member -NotePropertyName "InstallationOptions" -NotePropertyValue $App.InstallationOptions
    }

    if ($null -ne $App.PSObject.Properties['Notes'] -and $App.Notes) {
        $profileApp | Add-Member -NotePropertyName "Notes" -NotePropertyValue $App.Notes
    }

    return $profileApp
}

<#
.SYNOPSIS
    Gets all available categories
.DESCRIPTION
    Returns all categories defined in the database
.EXAMPLE
    $categories = Get-ApplicationCategories
#>
function Get-ApplicationCategories {
    [CmdletBinding()]
    param()

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return @()
    }

    return $database.Categories.PSObject.Properties | ForEach-Object {
        $category = $_.Value
        $category | Add-Member -NotePropertyName "CategoryId" -NotePropertyValue $_.Name -Force
        $category
    }
}

<#
.SYNOPSIS
    Gets all available tags
.DESCRIPTION
    Returns all tags defined in the database
.EXAMPLE
    $tags = Get-ApplicationTags
#>
function Get-ApplicationTags {
    [CmdletBinding()]
    param()

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return @()
    }

    return $database.Tags.PSObject.Properties | ForEach-Object {
        [PSCustomObject]@{
            Tag         = $_.Name
            Description = $_.Value
        }
    }
}

<#
.SYNOPSIS
    Validates that all Winget IDs in the database are available
.DESCRIPTION
    Checks each application's Winget ID against the Winget repository
.PARAMETER UpdateDatabase
    If specified, updates the LastVerified and Verified fields in the database
.EXAMPLE
    $results = Test-ApplicationSources
#>
function Test-ApplicationSources {
    [CmdletBinding()]
    param()

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        Write-Error (t 'database.validate.not_loaded')
        return
    }

    $results = @()
    $totalApps = ($database.Applications.PSObject.Properties | Measure-Object).Count
    $current = 0

    foreach ($prop in $database.Applications.PSObject.Properties) {
        $current++
        $app = $prop.Value
        $appId = $prop.Name

        Write-Progress -Activity (t 'database.validate.progress_activity') -Status (t 'database.validate.progress_status' @{ AppName = $app.Name; Current = $current; Total = $totalApps }) -PercentComplete (($current / $totalApps) * 100)

        $result = [PSCustomObject]@{
            AppId       = $appId
            Name        = $app.Name
            WingetValid = $null
            ChocoValid  = $null
            StoreValid  = $null
            AllValid    = $false
            Errors      = @()
        }

        # Test Winget
        if ($app.Sources.Winget) {
            try {
                $wingetTest = winget search --id $app.Sources.Winget --exact 2>&1 | Out-String
                # Winget returns 0 even when ID not found, must check output content
                $result.WingetValid = ($LASTEXITCODE -eq 0 -and $wingetTest -match [regex]::Escape($app.Sources.Winget))
                if (-not $result.WingetValid) {
                    $result.Errors += (t 'database.validate.winget_not_found' @{ WingetId = $app.Sources.Winget })
                }
            }
            catch {
                $result.WingetValid = $false
                $result.Errors += (t 'database.validate.winget_test_failed' @{ Error = $_ })
            }
        }
        else {
            $result.WingetValid = $null
        }

        # Test Chocolatey
        if ($app.Sources.Chocolatey) {
            try {
                $chocoTest = choco search $app.Sources.Chocolatey --exact --limit-output 2>&1
                $result.ChocoValid = ($LASTEXITCODE -eq 0 -and $chocoTest)
                if (-not $result.ChocoValid) {
                    $result.Errors += (t 'database.validate.choco_not_found' @{ ChocoId = $app.Sources.Chocolatey })
                }
            }
            catch {
                $result.ChocoValid = $false
                $result.Errors += (t 'database.validate.choco_test_failed' @{ Error = $_ })
            }
        }
        else {
            $result.ChocoValid = $null
        }

        # Store validation would require different approach (not implemented here)
        $result.StoreValid = $null

        # Determine if all sources are valid
        $hasAnySource = ($result.WingetValid -eq $true) -or ($result.ChocoValid -eq $true) -or $app.Sources.DirectUrl
        $result.AllValid = $hasAnySource

        $results += $result
    }

    Write-Progress -Activity "Validating Application Sources" -Completed

    return $results
}

<#
.SYNOPSIS
    Refreshes the database cache
.DESCRIPTION
    Clears the cached database and reloads it from disk
.EXAMPLE
    Reset-DatabaseCache
#>
function Reset-DatabaseCache {
    [CmdletBinding()]
    param()

    $Script:DatabaseCache = $null
    Write-Verbose (t 'database.cache.cleared')
}

<#
.SYNOPSIS
    Gets database statistics
.DESCRIPTION
    Returns statistics about the application database
.EXAMPLE
    Get-DatabaseStatistics
#>
function Get-DatabaseStatistics {
    [CmdletBinding()]
    param()

    $database = Get-ApplicationDatabase
    if ($null -eq $database) {
        return $null
    }

    $stats = [PSCustomObject]@{
        DatabaseVersion    = $database.DatabaseVersion
        LastUpdated        = $database.LastUpdated
        TotalApplications  = $database.TotalApplications
        TotalCategories    = ($database.Categories.PSObject.Properties | Measure-Object).Count
        TotalTags          = ($database.Tags.PSObject.Properties | Measure-Object).Count
        VerifiedApps       = (Get-AllApplications -Verified | Measure-Object).Count
        AppsWithWinget     = (Get-AllApplications | Where-Object { $_.Sources.Winget } | Measure-Object).Count
        AppsWithChocolatey = (Get-AllApplications | Where-Object { $_.Sources.Chocolatey } | Measure-Object).Count
        AppsWithStore      = (Get-AllApplications | Where-Object { $_.Sources.Store } | Measure-Object).Count
        AppsWithDirectUrl  = (Get-AllApplications | Where-Object { $_.Sources.DirectUrl } | Measure-Object).Count
    }

    return $stats
}

<#
.SYNOPSIS
    Gets dependencies for an application
.DESCRIPTION
    Returns the list of application dependencies with their metadata
.PARAMETER AppId
    The application ID to get dependencies for
.PARAMETER DependencyType
    Filter by dependency type: Required, Optional, Recommended, or All (default)
.EXAMPLE
    $deps = Get-ApplicationDependencies -AppId 'VSCode'
    $reqDeps = Get-ApplicationDependencies -AppId 'VSCode' -DependencyType 'Required'
#>
function Get-ApplicationDependencies {
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory)]
        [string]$AppId,

        [Parameter()]
        [ValidateSet('Required', 'Optional', 'Recommended', 'All')]
        [string]$DependencyType = 'All'
    )

    $app = Get-ApplicationById -AppId $AppId
    if ($null -eq $app) {
        Write-Warning (t 'database.application.not_found_short' @{ AppId = $AppId })
        return @()
    }

    # Check if Dependencies property exists
    if ($null -eq $app.PSObject.Properties['Dependencies'] -or $null -eq $app.Dependencies) {
        return @()
    }

    $dependencies = @($app.Dependencies)

    # Filter by type if specified
    if ($DependencyType -ne 'All') {
        $dependencies = @($dependencies | Where-Object { $_.Type -eq $DependencyType.ToLower() })
    }

    # Resolve dependency details
    $resolvedDeps = foreach ($dep in $dependencies) {
        $depApp = Get-ApplicationById -AppId $dep.AppId
        [PSCustomObject]@{
            AppId       = $dep.AppId
            Name        = if ($depApp) { $depApp.Name } else { $dep.AppId }
            Type        = $dep.Type
            MinVersion  = $dep.MinVersion
            Reason      = $dep.Reason
            Resolved    = ($null -ne $depApp)
            Category    = if ($depApp) { $depApp.Category } else { $null }
        }
    }

    return $resolvedDeps
}

<#
.SYNOPSIS
    Resolves all dependencies for a list of applications
.DESCRIPTION
    Returns a sorted list of applications with dependencies resolved in correct installation order
.PARAMETER AppIds
    Array of application IDs to resolve
.PARAMETER IncludeOptional
    If specified, includes optional dependencies
.EXAMPLE
    $orderedApps = Resolve-ApplicationDependencies -AppIds @('VSCode', 'Docker')
#>
function Resolve-ApplicationDependencies {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string[]]$AppIds,

        [Parameter()]
        [switch]$IncludeOptional
    )

    $resolved = [System.Collections.Generic.HashSet[string]]::new()
    $result = [System.Collections.Generic.List[string]]::new()
    $visiting = [System.Collections.Generic.HashSet[string]]::new()

    function Resolve-Single {
        param([string]$Id)

        if ($resolved.Contains($Id)) {
            return
        }

        if ($visiting.Contains($Id)) {
            Write-Warning (t 'database.dependency.circular_detected' @{ AppId = $Id })
            return
        }

        [void]$visiting.Add($Id)

        # Get dependencies
        $depTypes = @('Required', 'Recommended')
        if ($IncludeOptional) {
            $depTypes += 'Optional'
        }

        $deps = Get-ApplicationDependencies -AppId $Id -DependencyType 'All' |
            Where-Object { $_.Type -in $depTypes.ToLower() }

        foreach ($dep in $deps) {
            if ($dep.Resolved) {
                Resolve-Single -Id $dep.AppId
            }
        }

        [void]$visiting.Remove($Id)
        [void]$resolved.Add($Id)
        [void]$result.Add($Id)
    }

    foreach ($appId in $AppIds) {
        Resolve-Single -Id $appId
    }

    return $result.ToArray()
}

<#
.SYNOPSIS
    Checks if dependencies are satisfied for an application
.DESCRIPTION
    Verifies that all required dependencies for an application are present in a given list
.PARAMETER AppId
    The application ID to check
.PARAMETER InstalledAppIds
    List of AppIds that are already installed or selected
.EXAMPLE
    $satisfied = Test-DependenciesSatisfied -AppId 'VSCode' -InstalledAppIds @('Git', 'NodeJS')
#>
function Test-DependenciesSatisfied {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$AppId,

        [Parameter(Mandatory)]
        [string[]]$InstalledAppIds
    )

    $deps = Get-ApplicationDependencies -AppId $AppId -DependencyType 'Required'

    $missing = @($deps | Where-Object { $_.AppId -notin $InstalledAppIds })

    return [PSCustomObject]@{
        AppId           = $AppId
        Satisfied       = ($missing.Count -eq 0)
        TotalRequired   = $deps.Count
        MissingCount    = $missing.Count
        MissingDeps     = $missing
    }
}

# === DATABASE MODIFICATION FUNCTIONS ===

<#
.SYNOPSIS
    Saves the application database to disk
.DESCRIPTION
    Writes the application database to the JSON file with optional backup
.PARAMETER Applications
    Hashtable of applications to save
.PARAMETER CreateBackup
    If specified, creates a timestamped backup before saving
.EXAMPLE
    Save-ApplicationDatabase -Applications $apps -CreateBackup
#>
function Save-ApplicationDatabase {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Applications,

        [Parameter()]
        [switch]$CreateBackup
    )

    try {
        # Create backup if requested
        $backupPath = $null
        if ($CreateBackup) {
            $backupPath = New-DatabaseBackup
        }

        # Load current database to preserve metadata
        $database = Get-ApplicationDatabase -ForceReload
        if ($null -eq $database) {
            # Create new database structure
            $database = [PSCustomObject]@{
                '$schema' = 'https://json-schema.org/draft-07/schema#'
                DatabaseVersion = '3.7.2'
                LastUpdated = (Get-Date).ToString('yyyy-MM-dd')
                TotalApplications = $Applications.Count
                Applications = [PSCustomObject]@{}
            }
        }

        # Update applications
        $appsObject = [PSCustomObject]@{}
        foreach ($key in $Applications.Keys | Sort-Object) {
            $appsObject | Add-Member -NotePropertyName $key -NotePropertyValue $Applications[$key]
        }

        # Update database metadata
        $database.Applications = $appsObject
        $database.TotalApplications = $Applications.Count
        $database.LastUpdated = (Get-Date).ToString('yyyy-MM-dd')

        # Convert to JSON with proper formatting
        $jsonContent = $database | ConvertTo-Json -Depth 10

        # Write to temp file first (atomic write)
        $tempPath = "$Script:DatabasePath.tmp"
        $jsonContent | Out-File -FilePath $tempPath -Encoding UTF8 -NoNewline

        # Replace original file
        Move-Item -Path $tempPath -Destination $Script:DatabasePath -Force

        # Clear cache
        Clear-DatabaseCache

        return [PSCustomObject]@{
            Success = $true
            BackupPath = $backupPath
            ApplicationCount = $Applications.Count
            Error = $null
        }
    }
    catch {
        # Clean up temp file if exists
        if (Test-Path "$Script:DatabasePath.tmp") {
            Remove-Item "$Script:DatabasePath.tmp" -Force -ErrorAction SilentlyContinue
        }

        return [PSCustomObject]@{
            Success = $false
            Error = $_.Exception.Message
            BackupPath = $backupPath
        }
    }
}

<#
.SYNOPSIS
    Creates a backup of the application database
.DESCRIPTION
    Creates a timestamped backup of the current database and manages backup rotation
.PARAMETER MaxBackups
    Maximum number of backups to keep (default: 10)
.OUTPUTS
    Path to the created backup file
.EXAMPLE
    $backupPath = New-DatabaseBackup
#>
function New-DatabaseBackup {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [int]$MaxBackups = 10
    )

    try {
        # Get backup directory
        $backupDir = Join-Path (Get-Win11ForgeDirectory -DirectoryType 'Backups') 'Database'
        if (-not (Test-Path $backupDir)) {
            New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
        }

        # Create backup filename with timestamp
        $timestamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
        $backupFileName = "applications-$timestamp.json"
        $backupPath = Join-Path $backupDir $backupFileName

        # Copy current database to backup
        if (Test-Path $Script:DatabasePath) {
            Copy-Item -Path $Script:DatabasePath -Destination $backupPath -Force
            Write-Verbose (t 'database.backup.created' @{ Path = $backupPath })
        }

        # Rotate old backups
        $backups = @(Get-ChildItem -Path $backupDir -Filter 'applications-*.json' |
            Sort-Object LastWriteTime -Descending)

        if ($backups.Count -gt $MaxBackups) {
            $toDelete = $backups | Select-Object -Skip $MaxBackups
            foreach ($old in $toDelete) {
                Remove-Item $old.FullName -Force
                Write-Verbose (t 'database.backup.removed_old' @{ FileName = $old.Name })
            }
        }

        return $backupPath
    }
    catch {
        Write-Warning (t 'database.backup.create_failed' @{ Error = $_.Exception.Message })
        return $null
    }
}

<#
.SYNOPSIS
    Gets all available database backups
.DESCRIPTION
    Returns a list of all available database backups with metadata
.OUTPUTS
    Array of backup objects with Path, Date, and Size properties
.EXAMPLE
    $backups = Get-DatabaseBackups
#>
function Get-DatabaseBackups {
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param()

    $backupDir = Join-Path (Get-Win11ForgeDirectory -DirectoryType 'Backups') 'Database'

    if (-not (Test-Path $backupDir)) {
        return @()
    }

    $backups = Get-ChildItem -Path $backupDir -Filter 'applications-*.json' |
        Sort-Object LastWriteTime -Descending |
        ForEach-Object {
            [PSCustomObject]@{
                Path = $_.FullName
                FileName = $_.Name
                Date = $_.LastWriteTime
                Size = $_.Length
                SizeFormatted = if ($_.Length -ge 1MB) {
                    "{0:N2} MB" -f ($_.Length / 1MB)
                } elseif ($_.Length -ge 1KB) {
                    "{0:N2} KB" -f ($_.Length / 1KB)
                } else {
                    "{0} bytes" -f $_.Length
                }
            }
        }

    return @($backups)
}

<#
.SYNOPSIS
    Restores the database from a backup
.DESCRIPTION
    Restores the application database from a specified backup file
.PARAMETER BackupPath
    Path to the backup file to restore
.PARAMETER CreateBackupFirst
    If specified, creates a backup of current state before restoring
.OUTPUTS
    Result object with Success and message
.EXAMPLE
    Restore-DatabaseFromBackup -BackupPath 'C:\...\applications-20260203-120000.json'
#>
function Restore-DatabaseFromBackup {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$BackupPath,

        [Parameter()]
        [switch]$CreateBackupFirst
    )

    try {
        # Validate backup file exists
        if (-not (Test-Path $BackupPath)) {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.backup.file_not_found' @{ Path = $BackupPath })
            }
        }

        # Validate backup file is valid JSON
        $backupContent = Get-Content -Path $BackupPath -Raw -Encoding UTF8
        try {
            $null = $backupContent | ConvertFrom-Json
        }
        catch {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.backup.invalid_format' @{ Error = $_.Exception.Message })
            }
        }

        # Create backup of current state if requested
        $currentBackup = $null
        if ($CreateBackupFirst -and (Test-Path $Script:DatabasePath)) {
            $currentBackup = New-DatabaseBackup
        }

        # Copy backup to database location
        Copy-Item -Path $BackupPath -Destination $Script:DatabasePath -Force

        # Clear cache to reload
        Clear-DatabaseCache

        return [PSCustomObject]@{
            Success = $true
            RestoredFrom = $BackupPath
            PreviousStateBackup = $currentBackup
            Message = (t 'database.backup.restore_success')
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
    Cleans up old database backups
.DESCRIPTION
    Removes old backups beyond the specified limit
.PARAMETER MaxBackups
    Maximum number of backups to keep (default: 10)
.OUTPUTS
    Number of backups deleted
.EXAMPLE
    Invoke-BackupRotation -MaxBackups 5
#>
function Invoke-BackupRotation {
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter()]
        [int]$MaxBackups = 10
    )

    $backupDir = Join-Path (Get-Win11ForgeDirectory -DirectoryType 'Backups') 'Database'

    if (-not (Test-Path $backupDir)) {
        return 0
    }

    $backups = @(Get-ChildItem -Path $backupDir -Filter 'applications-*.json' |
        Sort-Object LastWriteTime -Descending)

    $deletedCount = 0

    if ($backups.Count -gt $MaxBackups) {
        $toDelete = $backups | Select-Object -Skip $MaxBackups
        foreach ($old in $toDelete) {
            Remove-Item $old.FullName -Force
            $deletedCount++
            Write-Verbose (t 'database.backup.removed_old' @{ FileName = $old.Name })
        }
    }

    return $deletedCount
}

<#
.SYNOPSIS
    Validates an application configuration
.DESCRIPTION
    Validates an application object against required fields and format rules
.PARAMETER Application
    The application object to validate
.PARAMETER IsNew
    If true, validates that AppId doesn't already exist
.OUTPUTS
    Validation result object with IsValid and Errors properties
.EXAMPLE
    $result = Test-ApplicationConfiguration -Application $app -IsNew
#>
function Test-ApplicationConfiguration {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter()]
        [switch]$IsNew
    )

    $errors = @()

    # Validate AppId
    if ([string]::IsNullOrWhiteSpace($Application.AppId)) {
        $errors += [PSCustomObject]@{ Field = 'AppId'; Message = (t 'database.validation.appid_required') }
    }
    elseif ($Application.AppId -notmatch '^[A-Za-z][A-Za-z0-9\.\-_]*$') {
        $errors += [PSCustomObject]@{ Field = 'AppId'; Message = (t 'database.validation.appid_format') }
    }
    elseif ($IsNew) {
        # Check uniqueness for new applications
        $existing = Get-ApplicationById -AppId $Application.AppId
        if ($null -ne $existing) {
            $errors += [PSCustomObject]@{ Field = 'AppId'; Message = (t 'database.validation.appid_exists' @{ AppId = $Application.AppId }) }
        }
    }

    # Validate Name
    if ([string]::IsNullOrWhiteSpace($Application.Name)) {
        $errors += [PSCustomObject]@{ Field = 'Name'; Message = (t 'database.validation.name_required') }
    }

    # Validate Category
    if ([string]::IsNullOrWhiteSpace($Application.Category)) {
        $errors += [PSCustomObject]@{ Field = 'Category'; Message = (t 'database.validation.category_required') }
    }

    # Validate Sources
    $hasSources = $false
    if ($Application.Sources) {
        if (-not [string]::IsNullOrWhiteSpace($Application.Sources.Winget)) { $hasSources = $true }
        if (-not [string]::IsNullOrWhiteSpace($Application.Sources.Chocolatey)) { $hasSources = $true }
        if (-not [string]::IsNullOrWhiteSpace($Application.Sources.Store)) { $hasSources = $true }
        if (-not [string]::IsNullOrWhiteSpace($Application.Sources.DirectUrl)) { $hasSources = $true }
    }

    if (-not $hasSources) {
        $errors += [PSCustomObject]@{ Field = 'Sources'; Message = (t 'database.validation.source_required') }
    }

    # Validate DirectUrl format if provided
    if ($Application.Sources -and -not [string]::IsNullOrWhiteSpace($Application.Sources.DirectUrl)) {
        if ($Application.Sources.DirectUrl -notmatch '^https?://') {
            $errors += [PSCustomObject]@{ Field = 'Sources.DirectUrl'; Message = (t 'database.validation.directurl_format') }
        }
    }

    # Validate Homepage format if provided
    if (-not [string]::IsNullOrWhiteSpace($Application.Homepage)) {
        if ($Application.Homepage -notmatch '^https?://') {
            $errors += [PSCustomObject]@{ Field = 'Homepage'; Message = (t 'database.validation.homepage_format') }
        }
    }

    # Validate Priority
    if ($Application.DefaultPriority -and ($Application.DefaultPriority -lt 1 -or $Application.DefaultPriority -gt 100)) {
        $errors += [PSCustomObject]@{ Field = 'DefaultPriority'; Message = (t 'database.validation.priority_range') }
    }

    return [PSCustomObject]@{
        IsValid = ($errors.Count -eq 0)
        Errors = $errors
    }
}

<#
.SYNOPSIS
    Adds or updates an application in the database
.DESCRIPTION
    Adds a new application or updates an existing one in the database
.PARAMETER Application
    The application object to save
.PARAMETER Force
    If specified, skips validation
.EXAMPLE
    Set-Application -Application $app
#>
function Set-Application {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter()]
        [switch]$Force
    )

    $existing = $null

    # Validate unless Force
    if (-not $Force) {
        $existing = Get-ApplicationById -AppId $Application.AppId
        $validation = Test-ApplicationConfiguration -Application $Application -IsNew:($null -eq $existing)

        if (-not $validation.IsValid) {
            return [PSCustomObject]@{
                Success = $false
                Errors = $validation.Errors
            }
        }
    }

    try {
        # Load current database
        $database = Get-ApplicationDatabase -ForceReload
        if ($null -eq $database) {
            return [PSCustomObject]@{
                Success = $false
                Errors = @([PSCustomObject]@{ Field = 'Database'; Message = (t 'database.load.failed_short') })
            }
        }

        # Convert applications to hashtable for manipulation
        $apps = @{}
        foreach ($prop in $database.Applications.PSObject.Properties) {
            $apps[$prop.Name] = $prop.Value
        }

        # Prepare application object (remove AppId from properties as it's the key)
        $appToSave = [PSCustomObject]@{
            Name = $Application.Name
            Category = $Application.Category
            Description = if ($Application.Description) { $Application.Description } else { '' }
            Sources = $Application.Sources
            Detection = $Application.Detection
            DefaultPriority = if ($Application.DefaultPriority) { $Application.DefaultPriority } else { 50 }
            DefaultRequired = if ($null -ne $Application.DefaultRequired) { $Application.DefaultRequired } else { $false }
            EnvironmentRestrictions = if ($Application.EnvironmentRestrictions) { $Application.EnvironmentRestrictions } else { @() }
            Tags = if ($Application.Tags) { $Application.Tags } else { @() }
            LastVerified = (Get-Date).ToString('yyyy-MM-dd')
            Verified = $false
            Homepage = if ($Application.Homepage) { $Application.Homepage } else { '' }
        }

        # Add or update
        $apps[$Application.AppId] = $appToSave

        # Save database
        $result = Save-ApplicationDatabase -Applications $apps -CreateBackup
        $saveError = if ($result.PSObject.Properties['Error']) { $result.Error } else { $null }

        return [PSCustomObject]@{
            Success = $result.Success
            IsNew = ($null -eq $existing)
            BackupPath = $result.BackupPath
            Errors = if ($saveError) { @([PSCustomObject]@{ Field = 'Save'; Message = $saveError }) } else { @() }
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Errors = @([PSCustomObject]@{ Field = 'Exception'; Message = $_.Exception.Message })
        }
    }
}

<#
.SYNOPSIS
    Removes an application from the database
.DESCRIPTION
    Deletes an application from the database by its ID
.PARAMETER AppId
    The application ID to remove
.EXAMPLE
    Remove-Application -AppId 'MyApp'
#>
function Remove-Application {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$AppId
    )

    try {
        # Verify application exists
        $existing = Get-ApplicationById -AppId $AppId
        if ($null -eq $existing) {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.application.not_found' @{ AppId = $AppId })
            }
        }

        # Load current database
        $database = Get-ApplicationDatabase -ForceReload
        if ($null -eq $database) {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.load.failed_short')
            }
        }

        # Convert to hashtable and remove
        $apps = @{}
        foreach ($prop in $database.Applications.PSObject.Properties) {
            if ($prop.Name -ne $AppId) {
                $apps[$prop.Name] = $prop.Value
            }
        }

        # Save database
        $result = Save-ApplicationDatabase -Applications $apps -CreateBackup
        $saveError = if ($result.PSObject.Properties['Error']) { $result.Error } else { $null }

        return [PSCustomObject]@{
            Success = $result.Success
            RemovedAppId = $AppId
            BackupPath = $result.BackupPath
            Error = $saveError
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
    Imports applications from a JSON file
.DESCRIPTION
    Imports applications from an external JSON file into the database
.PARAMETER Path
    Path to the JSON file to import
.PARAMETER Mode
    Import mode: Merge (skip duplicates), Replace (overwrite duplicates), or ReplaceAll
.EXAMPLE
    Import-ApplicationsFromFile -Path 'C:\apps.json' -Mode Merge
#>
function Import-ApplicationsFromFile {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('Merge', 'Replace', 'ReplaceAll')]
        [string]$Mode = 'Merge'
    )

    try {
        if (-not (Test-Path $Path)) {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.import.file_not_found' @{ Path = $Path })
            }
        }

        # Read import file
        $importContent = Get-Content -Path $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $importApps = $importContent.Applications

        if ($null -eq $importApps) {
            return [PSCustomObject]@{
                Success = $false
                Error = (t 'database.import.no_applications')
            }
        }

        # Load current database
        $database = Get-ApplicationDatabase -ForceReload
        $apps = @{}

        if ($Mode -ne 'ReplaceAll' -and $null -ne $database) {
            foreach ($prop in $database.Applications.PSObject.Properties) {
                $apps[$prop.Name] = $prop.Value
            }
        }

        # Process imports
        $added = 0
        $updated = 0
        $skipped = 0
        $errors = @()

        foreach ($prop in $importApps.PSObject.Properties) {
            $appId = $prop.Name
            $app = $prop.Value

            # Validate
            $app | Add-Member -NotePropertyName 'AppId' -NotePropertyValue $appId -Force
            $validation = Test-ApplicationConfiguration -Application $app

            if (-not $validation.IsValid) {
                $errors += "Invalid app '$appId': $($validation.Errors.Message -join ', ')"
                $skipped++
                continue
            }

            $exists = $apps.ContainsKey($appId)

            if ($exists) {
                if ($Mode -eq 'Merge') {
                    $skipped++
                }
                else {
                    $apps[$appId] = $prop.Value
                    $updated++
                }
            }
            else {
                $apps[$appId] = $prop.Value
                $added++
            }
        }

        # Save database
        $result = Save-ApplicationDatabase -Applications $apps -CreateBackup

        return [PSCustomObject]@{
            Success = $result.Success
            Added = $added
            Updated = $updated
            Skipped = $skipped
            Errors = $errors
            BackupPath = $result.BackupPath
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
    Exports applications to a JSON file
.DESCRIPTION
    Exports selected applications to a JSON file
.PARAMETER AppIds
    Array of application IDs to export
.PARAMETER Path
    Destination file path
.EXAMPLE
    Export-ApplicationsToFile -AppIds @('Chrome', 'Firefox') -Path 'C:\export.json'
#>
function Export-ApplicationsToFile {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string[]]$AppIds,

        [Parameter(Mandatory)]
        [string]$Path
    )

    try {
        $database = Get-ApplicationDatabase
        if ($null -eq $database) {
            Write-Error (t 'database.load.failed_short')
            return $false
        }

        # Build export object
        $exportApps = [PSCustomObject]@{}

        foreach ($appId in $AppIds) {
            $prop = $database.Applications.PSObject.Properties | Where-Object { $_.Name -eq $appId }
            if ($prop) {
                $exportApps | Add-Member -NotePropertyName $prop.Name -NotePropertyValue $prop.Value
            }
        }

        $export = [PSCustomObject]@{
            ExportDate = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
            ExportedFrom = 'Win11Forge'
            ApplicationCount = ($exportApps.PSObject.Properties | Measure-Object).Count
            Applications = $exportApps
        }

        $export | ConvertTo-Json -Depth 10 | Out-File -FilePath $Path -Encoding UTF8

        return $true
    }
    catch {
        Write-Error (t 'database.export.failed' @{ Error = $_.Exception.Message })
        return $false
    }
}

# Export module members
Export-ModuleMember -Function @(
    'Get-ApplicationDatabase',
    'Get-ApplicationById',
    'Get-AllApplications',
    'Search-Applications',
    'ConvertTo-ProfileApplication',
    'Get-ApplicationCategories',
    'Get-ApplicationTags',
    'Test-ApplicationSources',
    'Reset-DatabaseCache',
    'Get-DatabaseStatistics',
    # Dependency management
    'Get-ApplicationDependencies',
    'Resolve-ApplicationDependencies',
    'Test-DependenciesSatisfied',
    # Database modification
    'Save-ApplicationDatabase',
    'New-DatabaseBackup',
    'Test-ApplicationConfiguration',
    'Set-Application',
    'Remove-Application',
    'Import-ApplicationsFromFile',
    'Export-ApplicationsToFile',
    # Backup management
    'Get-DatabaseBackups',
    'Restore-DatabaseFromBackup',
    'Invoke-BackupRotation',
    # File watcher
    'Enable-DatabaseFileWatcher',
    'Disable-DatabaseFileWatcher',
    'Clear-DatabaseCache'
)


