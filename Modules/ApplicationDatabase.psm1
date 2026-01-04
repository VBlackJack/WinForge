# ApplicationDatabase.psm1
# Module for interacting with the centralized application database
# Author: Julien Bombled
# Version: 3.0.0
# Last Updated: 2025-10-06

$Script:DatabasePath = Join-Path $PSScriptRoot "..\Apps\Database\applications.json"
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
        Write-Verbose "Database file watcher already enabled"
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
            Write-Verbose "Database file changed - cache invalidated"
        } -SourceIdentifier 'Win11Forge.DatabaseWatcher' | Out-Null

        $Script:FileWatcherEnabled = $true
        Write-Verbose "Database file watcher enabled for: $Script:DatabasePath"
    } catch {
        Write-Warning "Could not enable database file watcher: $($_.Exception.Message)"
    }
}

function Disable-DatabaseFileWatcher {
    <#
    .SYNOPSIS
        Disables the database file watcher.
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
        Write-Verbose "Database file watcher disabled"
    } catch {
        Write-Warning "Could not disable database file watcher: $($_.Exception.Message)"
    }
}

function Test-DatabaseFileChanged {
    <#
    .SYNOPSIS
        Checks if the database file has been modified since last load.
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
    #>
    [CmdletBinding()]
    param()

    $Script:DatabaseCache = $null
    $Script:DatabaseLastModified = $null
    Write-Verbose "Database cache cleared"
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
            throw "Application database not found at: $Script:DatabasePath"
        }

        $jsonContent = Get-Content -Path $Script:DatabasePath -Raw -Encoding UTF8
        $Script:DatabaseCache = $jsonContent | ConvertFrom-Json
        $Script:DatabaseLastModified = (Get-Item $Script:DatabasePath).LastWriteTime

        Write-Verbose "Database loaded: $($Script:DatabaseCache.Applications.Count) applications"

        return $Script:DatabaseCache
    }
    catch {
        Write-Error "Failed to load application database: $_"
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
        Write-Warning "Application '$AppId' not found in database"
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
                throw "Priority must be a valid integer, got: $Priority ($($Priority.GetType().Name))"
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
                    throw "Required must be a valid boolean (true/false/0/1), got: $Required ($($Required.GetType().Name))"
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

    # Add optional fields if they exist
    if ($App.InstallMethod) {
        $profileApp | Add-Member -NotePropertyName "InstallMethod" -NotePropertyValue $App.InstallMethod
    }

    if ($App.InstallArguments) {
        $profileApp | Add-Member -NotePropertyName "InstallArguments" -NotePropertyValue $App.InstallArguments
    }

    if ($App.InstallationOptions) {
        $profileApp | Add-Member -NotePropertyName "InstallationOptions" -NotePropertyValue $App.InstallationOptions
    }

    if ($App.Notes) {
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
        Write-Error "Cannot validate: Database not loaded"
        return
    }

    $results = @()
    $totalApps = ($database.Applications.PSObject.Properties | Measure-Object).Count
    $current = 0

    foreach ($prop in $database.Applications.PSObject.Properties) {
        $current++
        $app = $prop.Value
        $appId = $prop.Name

        Write-Progress -Activity "Validating Application Sources" -Status "Testing $($app.Name) ($current/$totalApps)" -PercentComplete (($current / $totalApps) * 100)

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
                    $result.Errors += "Winget ID not found: $($app.Sources.Winget)"
                }
            }
            catch {
                $result.WingetValid = $false
                $result.Errors += "Winget test failed: $_"
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
                    $result.Errors += "Chocolatey package not found: $($app.Sources.Chocolatey)"
                }
            }
            catch {
                $result.ChocoValid = $false
                $result.Errors += "Chocolatey test failed: $_"
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
    Write-Verbose "Database cache cleared"
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
    'Get-DatabaseStatistics'
)

