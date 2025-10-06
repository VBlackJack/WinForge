<#
.SYNOPSIS
    Win11Forge GUI Module v1.0.0

.DESCRIPTION
    PowerShell graphical interface for Win11Forge deployment framework
    Provides interactive menus for profile selection, application browsing, and custom profile creation

.NOTES
    Author: Julien Bombled
    Version: 2.4.0
    Requires: PowerShell 5.1+, Win11Forge v2.4.0+
#>

Set-StrictMode -Version Latest

# ============================================================================
# MODULE VARIABLES
# ============================================================================

$script:ModuleRoot = $PSScriptRoot
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:DatabaseLoaded = $false
$script:AppDatabase = $null

# ============================================================================
# INITIALIZATION
# ============================================================================

function Initialize-GUIModules {
    <#
    .SYNOPSIS
        Initialize required modules for GUI
    #>
    try {
        # Load Core module
        $coreModule = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
        if (Test-Path $coreModule) {
            Import-Module $coreModule -Force -Global
        }

        # Load EnvironmentDetection module
        $envModule = Join-Path $script:RepositoryRoot 'Modules\EnvironmentDetection.psm1'
        if (Test-Path $envModule) {
            Import-Module $envModule -Force -Global
        }

        # Load Prerequisites module
        $prereqModule = Join-Path $script:RepositoryRoot 'Modules\Prerequisites.psm1'
        if (Test-Path $prereqModule) {
            Import-Module $prereqModule -Force -Global
        }

        # Load ApplicationDatabase module
        $dbModule = Join-Path $script:RepositoryRoot 'Modules\ApplicationDatabase.psm1'
        if (Test-Path $dbModule) {
            Import-Module $dbModule -Force -Global
        }

        # Load database directly from JSON file
        $dbPath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
        if (Test-Path $dbPath) {
            $dbContent = Get-Content $dbPath -Raw | ConvertFrom-Json
            $script:AppDatabase = @{}

            # Get the Applications node from the JSON structure
            $appsNode = $dbContent.Applications

            # Convert PSCustomObject to Hashtable
            foreach ($property in $appsNode.PSObject.Properties) {
                $script:AppDatabase[$property.Name] = $property.Value
            }

            $script:DatabaseLoaded = $true
            Write-Verbose "Database loaded: $($script:AppDatabase.Keys.Count) applications" -Verbose
        }
        else {
            Write-Host "Warning: Database file not found at $dbPath" -ForegroundColor Yellow
        }

        # Load ProfileManager module
        $profileModule = Join-Path $script:RepositoryRoot 'Modules\ProfileManager.psm1'
        if (Test-Path $profileModule) {
            Import-Module $profileModule -Force -Global
        }

        # Load InstallationEngine module
        $installModule = Join-Path $script:RepositoryRoot 'Modules\InstallationEngine.psm1'
        if (Test-Path $installModule) {
            Import-Module $installModule -Force -Global
        }

        # Load SystemConfig module
        $sysconfigModule = Join-Path $script:RepositoryRoot 'Modules\SystemConfig.psm1'
        if (Test-Path $sysconfigModule) {
            Import-Module $sysconfigModule -Force -Global
        }

        return $true
    }
    catch {
        Write-Host "Error loading modules: $_" -ForegroundColor Red
        return $false
    }
}

# ============================================================================
# DATABASE HELPER FUNCTIONS
# ============================================================================

function Get-DatabaseApps {
    <#
    .SYNOPSIS
        Get all apps from database as array
    #>
    param(
        [string]$Category,
        [string]$Tag
    )

    if (-not $script:AppDatabase) {
        Write-Host "Error: Database not loaded" -ForegroundColor Red
        return @()
    }

    $apps = @()
    foreach ($key in $script:AppDatabase.Keys) {
        $app = $script:AppDatabase[$key]

        # Create a new object with AppId added
        $appObj = [PSCustomObject]@{
            AppId = $key
            Name = if ($app.PSObject.Properties['Name']) { $app.Name } else { $key }
            Category = if ($app.PSObject.Properties['Category']) { $app.Category } else { 'Unknown' }
            Sources = if ($app.PSObject.Properties['Sources']) { $app.Sources } else { @{} }
            Tags = if ($app.PSObject.Properties['Tags']) { $app.Tags } else { @() }
            Description = if ($app.PSObject.Properties['Description']) { $app.Description } else { '' }
            Homepage = if ($app.PSObject.Properties['Homepage']) { $app.Homepage } else { '' }
            Verified = if ($app.PSObject.Properties['Verified']) { $app.Verified } else { $false }
            LastVerified = if ($app.PSObject.Properties['LastVerified']) { $app.LastVerified } else { '' }
        }

        # Filter by category if specified
        if ($Category -and $appObj.Category -ne $Category) {
            continue
        }

        # Filter by tag if specified
        if ($Tag -and ($appObj.Tags -notcontains $Tag)) {
            continue
        }

        $apps += $appObj
    }

    return $apps
}

function Get-DatabaseCategories {
    <#
    .SYNOPSIS
        Get all unique categories from database
    #>
    $categories = @()
    foreach ($key in $script:AppDatabase.Keys) {
        $app = $script:AppDatabase[$key]
        $category = if ($app.PSObject.Properties['Category']) { $app.Category } else { 'Unknown' }
        if ($category -notin $categories) {
            $categories += $category
        }
    }
    return $categories
}

function Get-DatabaseAppById {
    <#
    .SYNOPSIS
        Get app by ID from database
    #>
    param([string]$AppId)

    if ($script:AppDatabase.ContainsKey($AppId)) {
        $app = $script:AppDatabase[$AppId]
        return [PSCustomObject]@{
            AppId = $AppId
            Name = if ($app.PSObject.Properties['Name']) { $app.Name } else { $AppId }
            Category = if ($app.PSObject.Properties['Category']) { $app.Category } else { 'Unknown' }
            Sources = if ($app.PSObject.Properties['Sources']) { $app.Sources } else { @{} }
            Tags = if ($app.PSObject.Properties['Tags']) { $app.Tags } else { @() }
            Description = if ($app.PSObject.Properties['Description']) { $app.Description } else { '' }
            Homepage = if ($app.PSObject.Properties['Homepage']) { $app.Homepage } else { '' }
            Verified = if ($app.PSObject.Properties['Verified']) { $app.Verified } else { $false }
            LastVerified = if ($app.PSObject.Properties['LastVerified']) { $app.LastVerified } else { '' }
        }
    }
    return $null
}

function Search-DatabaseApps {
    <#
    .SYNOPSIS
        Search apps in database
    #>
    param([string]$SearchTerm)

    $results = @()
    foreach ($key in $script:AppDatabase.Keys) {
        $app = $script:AppDatabase[$key]
        $name = if ($app.PSObject.Properties['Name']) { $app.Name } else { $key }

        if ($name -like "*$SearchTerm*" -or $key -like "*$SearchTerm*") {
            $results += [PSCustomObject]@{
                AppId = $key
                Name = $name
                Category = if ($app.PSObject.Properties['Category']) { $app.Category } else { 'Unknown' }
                Sources = if ($app.PSObject.Properties['Sources']) { $app.Sources } else { @{} }
                Tags = if ($app.PSObject.Properties['Tags']) { $app.Tags } else { @() }
                Description = if ($app.PSObject.Properties['Description']) { $app.Description } else { '' }
                Homepage = if ($app.PSObject.Properties['Homepage']) { $app.Homepage } else { '' }
                Verified = if ($app.PSObject.Properties['Verified']) { $app.Verified } else { $false }
                LastVerified = if ($app.PSObject.Properties['LastVerified']) { $app.LastVerified } else { '' }
            }
        }
    }
    return $results
}

function Get-DatabaseStats {
    <#
    .SYNOPSIS
        Get database statistics
    #>
    $total = $script:AppDatabase.Keys.Count
    $verified = 0
    $withWinget = 0
    $withChoco = 0
    $withStore = 0
    $withDirectUrl = 0
    $categoryBreakdown = @{}

    foreach ($key in $script:AppDatabase.Keys) {
        $app = $script:AppDatabase[$key]

        if ($app.PSObject.Properties['Verified'] -and $app.Verified) { $verified++ }
        if ($app.PSObject.Properties['Sources']) {
            if ($app.Sources.PSObject.Properties['Winget'] -and $app.Sources.Winget) { $withWinget++ }
            if ($app.Sources.PSObject.Properties['Chocolatey'] -and $app.Sources.Chocolatey) { $withChoco++ }
            if ($app.Sources.PSObject.Properties['Store'] -and $app.Sources.Store) { $withStore++ }
            if ($app.Sources.PSObject.Properties['DirectUrl'] -and $app.Sources.DirectUrl) { $withDirectUrl++ }
        }

        $category = if ($app.PSObject.Properties['Category']) { $app.Category } else { 'Unknown' }
        if ($categoryBreakdown.ContainsKey($category)) {
            $categoryBreakdown[$category]++
        } else {
            $categoryBreakdown[$category] = 1
        }
    }

    return [PSCustomObject]@{
        TotalApplications = $total
        VerifiedApplications = $verified
        VerificationRate = if ($total -gt 0) { ($verified / $total) * 100 } else { 0 }
        TotalCategories = $categoryBreakdown.Keys.Count
        AppsWithWinget = $withWinget
        AppsWithChocolatey = $withChoco
        AppsWithStore = $withStore
        AppsWithDirectUrl = $withDirectUrl
        CategoryBreakdown = $categoryBreakdown
    }
}

# ============================================================================
# DISPLAY UTILITIES
# ============================================================================

function Show-Header {
    <#
    .SYNOPSIS
        Display GUI header
    #>
    param(
        [string]$Title = "Win11Forge v2.4.0"
    )

    Clear-Host
    $width = 70
    Write-Host ("═" * $width) -ForegroundColor Cyan
    Write-Host ("  " + $Title.PadRight($width - 4)) -ForegroundColor Cyan
    Write-Host ("═" * $width) -ForegroundColor Cyan
    Write-Host ""
}

function Show-Footer {
    <#
    .SYNOPSIS
        Display GUI footer
    #>
    param(
        [string]$Message = "Enter your choice"
    )

    Write-Host ""
    Write-Host ("-" * 70) -ForegroundColor DarkGray
}

function Read-Choice {
    <#
    .SYNOPSIS
        Read user choice with validation
    #>
    param(
        [string]$Prompt = "Choice",
        [string[]]$ValidChoices,
        [switch]$AllowEmpty,
        [switch]$AllowCancel
    )

    while ($true) {
        Write-Host ""
        $choice = Read-Host $Prompt

        # Allow '0' to cancel/go back (unless it's a valid choice)
        if ($choice -eq '0' -and '0' -notin $ValidChoices) {
            return '0'
        }

        if ($AllowEmpty -and [string]::IsNullOrWhiteSpace($choice)) {
            return ""
        }

        if ($ValidChoices -and $choice -in $ValidChoices) {
            return $choice
        }

        if (-not $ValidChoices) {
            return $choice
        }

        Write-Host "Invalid choice. Please try again. (Press 0 to go back)" -ForegroundColor Red
    }
}

function Show-ProgressBar {
    <#
    .SYNOPSIS
        Display a simple progress bar
    #>
    param(
        [int]$Current,
        [int]$Total,
        [string]$Activity = "Processing"
    )

    $percent = [math]::Round(($Current / $Total) * 100)
    $barLength = 50
    $filledLength = [math]::Round(($Current / $Total) * $barLength)

    $bar = "█" * $filledLength + "░" * ($barLength - $filledLength)

    Write-Host -NoNewline "`r$Activity [$bar] $percent% ($Current/$Total)"

    if ($Current -eq $Total) {
        Write-Host ""
    }
}

# ============================================================================
# MAIN MENU
# ============================================================================

function Show-MainMenu {
    <#
    .SYNOPSIS
        Display main menu and handle user selection
    #>

    while ($true) {
        Show-Header -Title "Win11Forge v2.4.0 - Main Menu"

        Write-Host "  1. Deploy Profile" -ForegroundColor White
        Write-Host "  2. Browse Applications Database ($($script:AppDatabase.Count) apps)" -ForegroundColor White
        Write-Host "  3. Browse Profiles" -ForegroundColor White
        Write-Host "  4. Create Custom Profile" -ForegroundColor White
        Write-Host "  5. Database Statistics" -ForegroundColor White
        Write-Host "  6. Validate Database" -ForegroundColor White
        Write-Host "  7. Add New Application" -ForegroundColor Cyan
        Write-Host "  0. Exit" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "Select option [0-7]" -ValidChoices @('0','1','2','3','4','5','6','7')

        switch ($choice) {
            '1' { Show-DeployProfileMenu }
            '2' { Show-ApplicationBrowser }
            '3' { Show-ProfileBrowser }
            '4' { Show-ProfileCreator }
            '5' { Show-DatabaseStatistics }
            '6' { Start-DatabaseValidation }
            '7' { Show-AddApplicationMenu }
            '0' {
                Write-Host "`nGoodbye!" -ForegroundColor Green
                return
            }
        }
    }
}

# ============================================================================
# DEPLOY PROFILE MENU
# ============================================================================

function Show-DeployProfileMenu {
    <#
    .SYNOPSIS
        Display profile deployment menu
    #>

    Show-Header -Title "Deploy Profile"

    # List available profiles
    $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
    $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

    Write-Host "Available Profiles:" -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $profiles.Count; $i++) {
        $profile = $profiles[$i]
        $profileData = Get-Content $profile.FullName | ConvertFrom-Json

        $name = $profileData.Name
        $version = $profileData.Version
        $appCount = $profileData.Applications.Count

        Write-Host "  $($i + 1). $name (v$version) - $appCount apps" -ForegroundColor White
    }

    Write-Host "  0. Back to Main Menu" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$profiles.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "Select profile to deploy [0-$($profiles.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedProfile = $profiles[$choice - 1]
    $profileName = $selectedProfile.BaseName

    # Deployment options
    Show-Header -Title "Deploy: $profileName"

    Write-Host "Deployment Options:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1. Standard Deployment (Sequential)" -ForegroundColor White
    Write-Host "  2. Parallel Deployment (Faster, requires PowerShell 7)" -ForegroundColor White
    Write-Host "  3. Test Mode (Dry run, no installation)" -ForegroundColor White
    Write-Host "  0. Cancel" -ForegroundColor White

    Show-Footer

    $modeChoice = Read-Choice -Prompt "Select deployment mode [0-3]" -ValidChoices @('0','1','2','3')

    if ($modeChoice -eq '0') { return }

    # Build deployment parameters
    $deployScript = Join-Path $script:RepositoryRoot 'Deploy-Win11Environment.ps1'

    # Prepare parameters
    $deployParams = @{
        ProfileName = $profileName
    }

    switch ($modeChoice) {
        '2' { $deployParams['Parallel'] = $true }
        '3' { $deployParams['TestMode'] = $true }
    }

    # Confirm deployment
    Write-Host ""
    Write-Host "Ready to deploy profile: $profileName" -ForegroundColor Yellow
    Write-Host "Mode: $(if ($modeChoice -eq '2') { 'Parallel' } elseif ($modeChoice -eq '3') { 'Test' } else { 'Sequential' })" -ForegroundColor Gray
    Write-Host ""

    $confirm = Read-Host "Start deployment? (Y/N)"

    if ($confirm -eq 'Y' -or $confirm -eq 'y') {
        Write-Host ""
        Write-Host "Starting deployment..." -ForegroundColor Green
        Write-Host ""

        # Execute deployment script using & with splatting
        & $deployScript @deployParams
        $deployExitCode = $LASTEXITCODE

        Write-Host ""

        # Show deployment result summary
        if ($deployExitCode -eq 0) {
            Write-Host "Deployment completed - Check log for details" -ForegroundColor Green
        } else {
            Write-Host "Deployment completed with failures - Exit code: $deployExitCode" -ForegroundColor Red
        }

        Write-Host ""
        Read-Host "Press Enter to continue"
    }
}

# ============================================================================
# APPLICATION BROWSER
# ============================================================================

function Show-ApplicationBrowser {
    <#
    .SYNOPSIS
        Browse applications in database
    #>

    while ($true) {
        Show-Header -Title "Application Browser ($($script:AppDatabase.Count) apps)"

        Write-Host "Browse Options:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  1. View All Applications" -ForegroundColor White
        Write-Host "  2. Browse by Category" -ForegroundColor White
        Write-Host "  3. Browse by Tag" -ForegroundColor White
        Write-Host "  4. Search Applications" -ForegroundColor White
        Write-Host "  5. View Application Details" -ForegroundColor White
        Write-Host "  0. Back to Main Menu" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "Select option [0-5]" -ValidChoices @('0','1','2','3','4','5')

        switch ($choice) {
            '1' { Show-AllApplications }
            '2' { Show-ApplicationsByCategory }
            '3' { Show-ApplicationsByTag }
            '4' { Show-ApplicationSearch }
            '5' { Show-ApplicationDetails }
            '0' { return }
        }
    }
}

function Show-AllApplications {
    <#
    .SYNOPSIS
        Display all applications with pagination
    #>

    Show-Header -Title "All Applications"

    $apps = Get-DatabaseApps | Sort-Object -Property Name
    $pageSize = 20
    $currentPage = 0
    $totalPages = [math]::Ceiling($apps.Count / $pageSize)

    while ($true) {
        $start = $currentPage * $pageSize
        $end = [math]::Min($start + $pageSize, $apps.Count)
        $pageApps = $apps[$start..($end - 1)]

        Show-Header -Title "All Applications (Page $($currentPage + 1)/$totalPages)"

        Write-Host ("{0,-5} {1,-30} {2,-15} {3}" -f "No.", "Name", "Category", "Winget ID") -ForegroundColor Yellow
        Write-Host ("-" * 70) -ForegroundColor DarkGray

        for ($i = 0; $i -lt $pageApps.Count; $i++) {
            $app = $pageApps[$i]
            $num = $start + $i + 1
            $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }

            Write-Host ("{0,-5} {1,-30} {2,-15} {3}" -f $num, $app.Name, $app.Category, $wingetId) -ForegroundColor White
        }

        Write-Host ""
        Write-Host "N = Next Page | P = Previous Page | Q = Back" -ForegroundColor Gray

        Show-Footer

        $nav = Read-Choice -Prompt "Navigate" -ValidChoices @('N','n','P','p','Q','q')

        if ($nav -eq 'Q' -or $nav -eq 'q') { break }
        if ($nav -eq 'N' -or $nav -eq 'n') {
            if ($currentPage -lt $totalPages - 1) { $currentPage++ }
        }
        if ($nav -eq 'P' -or $nav -eq 'p') {
            if ($currentPage -gt 0) { $currentPage-- }
        }
    }
}

function Show-ApplicationsByCategory {
    <#
    .SYNOPSIS
        Browse applications by category
    #>

    Show-Header -Title "Browse by Category"

    $categories = Get-DatabaseCategories | Sort-Object

    Write-Host "Available Categories:" -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $categories.Count; $i++) {
        $category = $categories[$i]
        $count = (Get-DatabaseApps -Category $category).Count
        Write-Host "  $($i + 1). $category ($count apps)" -ForegroundColor White
    }

    Write-Host "  0. Back" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$categories.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "Select category [0-$($categories.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedCategory = $categories[$choice - 1]
    $apps = Get-DatabaseApps -Category $selectedCategory | Sort-Object -Property Name

    Show-Header -Title "Category: $selectedCategory ($($apps.Count) apps)"

    Write-Host ("{0,-30} {1}" -f "Name", "Winget ID") -ForegroundColor Yellow
    Write-Host ("-" * 70) -ForegroundColor DarkGray

    foreach ($app in $apps) {
        $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
        Write-Host ("{0,-30} {1}" -f $app.Name, $wingetId) -ForegroundColor White
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Show-ApplicationsByTag {
    <#
    .SYNOPSIS
        Browse applications by tag
    #>

    Show-Header -Title "Browse by Tag"

    # Get all unique tags
    $allApps = Get-DatabaseApps
    $tags = $allApps | Where-Object { $_.Tags } | ForEach-Object { $_.Tags } | Select-Object -Unique | Sort-Object

    Write-Host "Available Tags:" -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $tags.Count; $i++) {
        $tag = $tags[$i]
        $count = (Get-DatabaseApps -Tag $tag).Count
        Write-Host "  $($i + 1). $tag ($count apps)" -ForegroundColor White
    }

    Write-Host "  0. Back" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$tags.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "Select tag [0-$($tags.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedTag = $tags[$choice - 1]
    $apps = Get-DatabaseApps -Tag $selectedTag | Sort-Object -Property Name

    Show-Header -Title "Tag: $selectedTag ($($apps.Count) apps)"

    Write-Host ("{0,-30} {1,-15} {2}" -f "Name", "Category", "Winget ID") -ForegroundColor Yellow
    Write-Host ("-" * 70) -ForegroundColor DarkGray

    foreach ($app in $apps) {
        $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
        Write-Host ("{0,-30} {1,-15} {2}" -f $app.Name, $app.Category, $wingetId) -ForegroundColor White
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Show-ApplicationSearch {
    <#
    .SYNOPSIS
        Search applications by name
    #>

    Show-Header -Title "Search Applications"

    Write-Host "Enter search term (or press Enter to cancel):" -ForegroundColor Yellow
    $searchTerm = Read-Host "Search"

    if ([string]::IsNullOrWhiteSpace($searchTerm)) { return }

    $results = Search-DatabaseApps -SearchTerm $searchTerm

    Show-Header -Title "Search Results: '$searchTerm' ($($results.Count) found)"

    if ($results.Count -eq 0) {
        Write-Host "No applications found matching '$searchTerm'" -ForegroundColor Yellow
    }
    else {
        Write-Host ("{0,-30} {1,-15} {2}" -f "Name", "Category", "Winget ID") -ForegroundColor Yellow
        Write-Host ("-" * 70) -ForegroundColor DarkGray

        foreach ($app in $results) {
            $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
            Write-Host ("{0,-30} {1,-15} {2}" -f $app.Name, $app.Category, $wingetId) -ForegroundColor White
        }
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Show-ApplicationDetails {
    <#
    .SYNOPSIS
        Display detailed information about an application
    #>

    Show-Header -Title "Application Details"

    Write-Host "Enter Application ID or search term:" -ForegroundColor Yellow
    $input = Read-Host "AppId/Search"

    if ([string]::IsNullOrWhiteSpace($input)) { return }

    # Try to get by ID first
    $app = Get-DatabaseAppById -AppId $input

    # If not found, try search
    if (-not $app) {
        $results = Search-DatabaseApps -SearchTerm $input
        if ($results.Count -eq 1) {
            $app = $results[0]
        }
        elseif ($results.Count -gt 1) {
            Write-Host "Multiple applications found. Please be more specific." -ForegroundColor Yellow
            Read-Host "Press Enter to continue"
            return
        }
    }

    if (-not $app) {
        Write-Host "Application not found: $input" -ForegroundColor Red
        Read-Host "Press Enter to continue"
        return
    }

    Show-Header -Title "Application: $($app.Name)"

    Write-Host "AppId:        " -NoNewline -ForegroundColor Yellow
    Write-Host $app.AppId -ForegroundColor White

    Write-Host "Name:         " -NoNewline -ForegroundColor Yellow
    Write-Host $app.Name -ForegroundColor White

    Write-Host "Category:     " -NoNewline -ForegroundColor Yellow
    Write-Host $app.Category -ForegroundColor White

    if ($app.Description) {
        Write-Host "Description:  " -NoNewline -ForegroundColor Yellow
        Write-Host $app.Description -ForegroundColor White
    }

    Write-Host "`nSources:" -ForegroundColor Yellow
    if ($app.Sources -and $app.Sources.PSObject.Properties['Winget'] -and $app.Sources.Winget) {
        Write-Host "  Winget:     " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Winget -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['Chocolatey'] -and $app.Sources.Chocolatey) {
        Write-Host "  Chocolatey: " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Chocolatey -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['Store'] -and $app.Sources.Store) {
        Write-Host "  Store:      " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Store -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['DirectUrl'] -and $app.Sources.DirectUrl) {
        Write-Host "  DirectUrl:  " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.DirectUrl -ForegroundColor White
    }

    if ($app.Tags -and $app.Tags.Count -gt 0) {
        Write-Host "`nTags:         " -NoNewline -ForegroundColor Yellow
        Write-Host ($app.Tags -join ', ') -ForegroundColor White
    }

    if ($app.Homepage) {
        Write-Host "Homepage:     " -NoNewline -ForegroundColor Yellow
        Write-Host $app.Homepage -ForegroundColor White
    }

    if ($app.Verified) {
        Write-Host "`nVerified:     " -NoNewline -ForegroundColor Yellow
        Write-Host "Yes ($($app.LastVerified))" -ForegroundColor Green
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

# ============================================================================
# PROFILE BROWSER
# ============================================================================

function Show-ProfileBrowser {
    <#
    .SYNOPSIS
        Browse available profiles
    #>

    Show-Header -Title "Profile Browser"

    $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
    $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

    Write-Host "Available Profiles:" -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $profiles.Count; $i++) {
        $profile = $profiles[$i]
        $profileData = Get-Content $profile.FullName | ConvertFrom-Json

        Write-Host "  $($i + 1). $($profileData.Name) (v$($profileData.Version))" -ForegroundColor White
        Write-Host "      Apps: $($profileData.Applications.Count)" -ForegroundColor Gray
        Write-Host "      Desc: $($profileData.Description)" -ForegroundColor Gray
        Write-Host ""
    }

    Write-Host "  0. Back to Main Menu" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$profiles.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "Select profile to view [0-$($profiles.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedProfile = $profiles[$choice - 1]
    Show-ProfileDetails -ProfilePath $selectedProfile.FullName
}

function Show-ProfileDetails {
    <#
    .SYNOPSIS
        Display detailed profile information
    #>
    param(
        [string]$ProfilePath
    )

    $profileData = Get-Content $ProfilePath | ConvertFrom-Json

    Show-Header -Title "Profile: $($profileData.Name)"

    Write-Host "Name:         " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Name -ForegroundColor White

    Write-Host "Version:      " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Version -ForegroundColor White

    Write-Host "Description:  " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Description -ForegroundColor White

    if ($profileData.Inherits -and $profileData.Inherits.Count -gt 0) {
        Write-Host "Inherits:     " -NoNewline -ForegroundColor Yellow
        Write-Host ($profileData.Inherits -join ', ') -ForegroundColor White
    }

    Write-Host "`nApplications: " -NoNewline -ForegroundColor Yellow
    Write-Host "$($profileData.Applications.Count) apps" -ForegroundColor White
    Write-Host ""

    foreach ($app in $profileData.Applications) {
        if ($app -is [string]) {
            Write-Host "  - $app" -ForegroundColor Gray
        }
        else {
            $appId = if ($app.AppId) { $app.AppId } else { $app.Name }
            Write-Host "  - $appId" -ForegroundColor Gray
        }
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

# ============================================================================
# PROFILE CREATOR
# ============================================================================

function Show-ProfileCreator {
    <#
    .SYNOPSIS
        Interactive profile creation wizard
    #>

    Show-Header -Title "Create Custom Profile"

    Write-Host "This wizard will help you create a custom deployment profile." -ForegroundColor Yellow
    Write-Host ""

    # Profile name
    Write-Host "Enter profile name:" -ForegroundColor Yellow
    $profileName = Read-Choice -Prompt "Name"

    # Profile description
    Write-Host "`nEnter profile description:" -ForegroundColor Yellow
    $profileDesc = Read-Choice -Prompt "Description"

    # Inheritance
    Write-Host "`nInherit from existing profile? (Y/N)" -ForegroundColor Yellow
    $inheritChoice = Read-Host "Inherit"

    $inherits = @()
    if ($inheritChoice -eq 'Y' -or $inheritChoice -eq 'y') {
        $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
        $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

        Write-Host "`nAvailable profiles to inherit from:" -ForegroundColor Yellow
        for ($i = 0; $i -lt $profiles.Count; $i++) {
            Write-Host "  $($i + 1). $($profiles[$i].BaseName)" -ForegroundColor White
        }

        $validChoices = 1..$profiles.Count | ForEach-Object { $_.ToString() }
        $inheritIdx = Read-Choice -Prompt "Select profile [1-$($profiles.Count)]" -ValidChoices $validChoices

        $inherits = @($profiles[$inheritIdx - 1].BaseName)
    }

    # Application selection
    Write-Host "`nSelect applications to include:" -ForegroundColor Yellow
    Write-Host "  1. Browse and select from database" -ForegroundColor White
    Write-Host "  2. Enter AppIds manually" -ForegroundColor White

    $appChoice = Read-Choice -Prompt "Method [1-2]" -ValidChoices @('1','2')

    $selectedApps = @()

    if ($appChoice -eq '1') {
        $selectedApps = Select-ApplicationsFromDatabase
    }
    else {
        Write-Host "`nEnter AppIds separated by commas:" -ForegroundColor Yellow
        $appInput = Read-Host "AppIds"
        $selectedApps = $appInput -split ',' | ForEach-Object { $_.Trim() }
    }

    # Create profile object
    $newProfile = [PSCustomObject]@{
        Name = $profileName
        Description = $profileDesc
        Version = "2.4.0"
        Inherits = $inherits
        Applications = $selectedApps
        SystemConfig = @{
            WindowsUpdate = @{
                DisableDriverUpdates = $false
                PauseUpdates = $false
            }
            Privacy = @{
                DisableTelemetry = $true
                DisableAdvertisingID = $true
            }
            Performance = @{
                DisableHibernation = $false
                OptimizeStartup = $true
            }
        }
    }

    # Preview
    Show-Header -Title "Profile Preview"

    Write-Host ($newProfile | ConvertTo-Json -Depth 10) -ForegroundColor Gray

    Write-Host ""
    Write-Host "Save this profile? (Y/N)" -ForegroundColor Yellow
    $saveChoice = Read-Host "Save"

    if ($saveChoice -eq 'Y' -or $saveChoice -eq 'y') {
        $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
        $profileFile = Join-Path $profilesPath "$profileName.json"

        $newProfile | ConvertTo-Json -Depth 10 | Set-Content -Path $profileFile -Encoding UTF8

        Write-Host "`nProfile saved: $profileFile" -ForegroundColor Green
    }
    else {
        Write-Host "`nProfile discarded." -ForegroundColor Yellow
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Select-ApplicationsFromDatabase {
    <#
    .SYNOPSIS
        Interactive application selection from database
    #>

    $selected = @()

    while ($true) {
        Show-Header -Title "Select Applications ($($selected.Count) selected)"

        Write-Host "Options:" -ForegroundColor Yellow
        Write-Host "  1. Add by Category" -ForegroundColor White
        Write-Host "  2. Add by Tag" -ForegroundColor White
        Write-Host "  3. Add by Search" -ForegroundColor White
        Write-Host "  4. Add by AppId" -ForegroundColor White
        Write-Host "  5. View Selected ($($selected.Count))" -ForegroundColor White
        Write-Host "  6. Remove Selected" -ForegroundColor White
        Write-Host "  0. Done" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "Option [0-6]" -ValidChoices @('0','1','2','3','4','5','6')

        switch ($choice) {
            '1' {
                $categories = Get-DatabaseCategories | Sort-Object
                Write-Host "`nCategories:" -ForegroundColor Yellow
                Write-Host "  0. Cancel" -ForegroundColor Gray
                for ($i = 0; $i -lt $categories.Count; $i++) {
                    Write-Host "  $($i + 1). $($categories[$i])" -ForegroundColor White
                }
                $validChoices = 1..$categories.Count | ForEach-Object { $_.ToString() }
                $catChoice = Read-Choice -Prompt "Category [1-$($categories.Count)] or 0 to cancel" -ValidChoices $validChoices
                if ($catChoice -ne '0') {
                    $apps = Get-DatabaseApps -Category $categories[$catChoice - 1]
                    $selected += $apps | ForEach-Object { $_.AppId }
                }
            }
            '2' {
                $allApps = Get-DatabaseApps
                $tags = $allApps | Where-Object { $_.Tags } | ForEach-Object { $_.Tags } | Select-Object -Unique | Sort-Object
                Write-Host "`nTags:" -ForegroundColor Yellow
                Write-Host "  0. Cancel" -ForegroundColor Gray
                for ($i = 0; $i -lt $tags.Count; $i++) {
                    Write-Host "  $($i + 1). $($tags[$i])" -ForegroundColor White
                }
                $validChoices = 1..$tags.Count | ForEach-Object { $_.ToString() }
                $tagChoice = Read-Choice -Prompt "Tag [1-$($tags.Count)] or 0 to cancel" -ValidChoices $validChoices
                if ($tagChoice -ne '0') {
                    $apps = Get-DatabaseApps -Tag $tags[$tagChoice - 1]
                    $selected += $apps | ForEach-Object { $_.AppId }
                }
            }
            '3' {
                Write-Host "`nSearch term:" -ForegroundColor Yellow
                $searchTerm = Read-Host "Search"
                $apps = Search-DatabaseApps -SearchTerm $searchTerm
                $selected += $apps | ForEach-Object { $_.AppId }
            }
            '4' {
                Write-Host "`nAppId:" -ForegroundColor Yellow
                $appId = Read-Host "AppId"
                if (Get-DatabaseAppById -AppId $appId) {
                    $selected += $appId
                }
                else {
                    Write-Host "Invalid AppId: $appId" -ForegroundColor Red
                }
            }
            '5' {
                Show-Header -Title "Selected Applications ($($selected.Count))"
                $selected | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
                Read-Host "`nPress Enter to continue"
            }
            '6' {
                Write-Host "`nAppId to remove:" -ForegroundColor Yellow
                $removeId = Read-Host "AppId"
                $selected = $selected | Where-Object { $_ -ne $removeId }
            }
            '0' { break }
        }

        # Remove duplicates
        $selected = $selected | Select-Object -Unique
    }

    return $selected
}

# ============================================================================
# DATABASE STATISTICS
# ============================================================================

function Show-DatabaseStatistics {
    <#
    .SYNOPSIS
        Display database statistics
    #>

    Show-Header -Title "Database Statistics"

    $stats = Get-DatabaseStats

    Write-Host "Total Applications:      " -NoNewline -ForegroundColor Yellow
    Write-Host $stats.TotalApplications -ForegroundColor White

    Write-Host "Verified Applications:   " -NoNewline -ForegroundColor Yellow
    Write-Host "$($stats.VerifiedApplications) ($([math]::Round($stats.VerificationRate))%)" -ForegroundColor Green

    Write-Host "Categories:              " -NoNewline -ForegroundColor Yellow
    Write-Host $stats.TotalCategories -ForegroundColor White

    Write-Host "`nSources:" -ForegroundColor Yellow
    Write-Host "  Winget:                " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithWinget -ForegroundColor White
    Write-Host "  Chocolatey:            " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithChocolatey -ForegroundColor White
    Write-Host "  Store:                 " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithStore -ForegroundColor White
    Write-Host "  DirectUrl:             " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithDirectUrl -ForegroundColor White

    Write-Host "`nTop 5 Categories:" -ForegroundColor Yellow
    $stats.CategoryBreakdown.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Host ("  {0,-20} {1}" -f $_.Key, $_.Value) -ForegroundColor White
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

# ============================================================================
# DATABASE VALIDATION
# ============================================================================

function Start-DatabaseValidation {
    <#
    .SYNOPSIS
        Run database validation
    #>

    Show-Header -Title "Database Validation"

    Write-Host "This will validate all application sources in the database." -ForegroundColor Yellow
    Write-Host "This process may take several minutes." -ForegroundColor Yellow
    Write-Host ""

    $confirm = Read-Host "Continue? (Y/N)"

    if ($confirm -ne 'Y' -and $confirm -ne 'y') { return }

    Write-Host ""
    Write-Host "Validating database..." -ForegroundColor Green
    Write-Host ""

    $validationScript = Join-Path $script:RepositoryRoot 'Tools\Validate-AppDatabase.ps1'

    if (Test-Path $validationScript) {
        & $validationScript -ValidateWinget -ValidateChocolatey
    }
    else {
        Write-Host "Validation script not found: $validationScript" -ForegroundColor Red
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

# ============================================================================
# SETTINGS MENU
# ============================================================================

function Show-SettingsMenu {
    <#
    .SYNOPSIS
        Display settings and options
    #>

    Show-Header -Title "Settings & Options"

    Write-Host "  1. View Framework Information" -ForegroundColor White
    Write-Host "  2. View Logs Directory" -ForegroundColor White
    Write-Host "  3. Check for Updates" -ForegroundColor White
    Write-Host "  4. About Win11Forge" -ForegroundColor White
    Write-Host "  0. Back to Main Menu" -ForegroundColor White

    Show-Footer

    $choice = Read-Choice -Prompt "Option [0-4]" -ValidChoices @('0','1','2','3','4')

    switch ($choice) {
        '1' { Show-FrameworkInfo }
        '2' { Show-LogsDirectory }
        '3' { Check-Updates }
        '4' { Show-About }
        '0' { return }
    }
}

function Show-FrameworkInfo {
    Show-Header -Title "Framework Information"

    Write-Host "Win11Forge Version:      " -NoNewline -ForegroundColor Yellow
    Write-Host "2.4.0" -ForegroundColor White

    Write-Host "PowerShell Version:      " -NoNewline -ForegroundColor Yellow
    Write-Host "$($PSVersionTable.PSVersion)" -ForegroundColor White

    Write-Host "Repository Path:         " -NoNewline -ForegroundColor Yellow
    Write-Host $script:RepositoryRoot -ForegroundColor White

    Write-Host "Database Loaded:         " -NoNewline -ForegroundColor Yellow
    Write-Host $script:DatabaseLoaded -ForegroundColor White

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Show-LogsDirectory {
    $logsPath = Join-Path $script:RepositoryRoot 'Logs'

    Show-Header -Title "Logs Directory"

    Write-Host "Logs Path: $logsPath" -ForegroundColor Yellow
    Write-Host ""

    if (Test-Path $logsPath) {
        $logs = Get-ChildItem -Path $logsPath -Filter '*.log' | Sort-Object -Property LastWriteTime -Descending

        if ($logs.Count -gt 0) {
            Write-Host "Recent Logs:" -ForegroundColor Yellow
            $logs | Select-Object -First 10 | ForEach-Object {
                Write-Host "  $($_.Name) - $($_.LastWriteTime)" -ForegroundColor White
            }
        }
        else {
            Write-Host "No logs found." -ForegroundColor Gray
        }
    }
    else {
        Write-Host "Logs directory does not exist yet." -ForegroundColor Gray
    }

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Check-Updates {
    Show-Header -Title "Check for Updates"

    Write-Host "Current Version: 2.4.0" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "No update mechanism configured (requires Git repository)." -ForegroundColor Gray

    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Show-About {
    Show-Header -Title "About Win11Forge"

    Write-Host "Win11Forge v2.4.0" -ForegroundColor Cyan
    Write-Host "Windows 11 Deployment Framework with Centralized Database" -ForegroundColor White
    Write-Host ""
    Write-Host "Features:" -ForegroundColor Yellow
    Write-Host "  - 66 pre-configured applications" -ForegroundColor White
    Write-Host "  - Profile-based deployment" -ForegroundColor White
    Write-Host "  - Parallel installation support" -ForegroundColor White
    Write-Host "  - Interactive GUI" -ForegroundColor White
    Write-Host "  - Centralized application database" -ForegroundColor White
    Write-Host "  - Custom profile creation" -ForegroundColor White
    Write-Host ""
    Write-Host "Documentation: See Apps/README.md and Apps/QUICK_START.md" -ForegroundColor Gray

    Write-Host ""
    Read-Host "Press Enter to continue"
}

# ============================================================================
# ADD APPLICATION MENU
# ============================================================================

function Show-AddApplicationMenu {
    <#
    .SYNOPSIS
        Interactive menu to add a new application to the database
    #>

    Show-Header -Title "Add New Application"

    Write-Host "This wizard will help you add a new application to the database." -ForegroundColor Yellow
    Write-Host ""

    # Step 1: Get application name
    Write-Host "Step 1: Application Name" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    $appName = Read-Host "Enter application name (e.g., 'Discord', 'Spotify')"

    if ([string]::IsNullOrWhiteSpace($appName)) {
        Write-Host ""
        Write-Host "❌ Application name cannot be empty" -ForegroundColor Red
        Read-Host "Press Enter to continue"
        return
    }

    Write-Host ""
    Write-Host "Searching for '$appName' across all sources..." -ForegroundColor Yellow
    Write-Host ""

    # Step 2: Run search script
    $searchScript = Join-Path $script:RepositoryRoot 'Tools\Search-ApplicationSources.ps1'

    if (-not (Test-Path $searchScript)) {
        Write-Host "❌ Search script not found at: $searchScript" -ForegroundColor Red
        Read-Host "Press Enter to continue"
        return
    }

    try {
        # Execute search
        & $searchScript -AppName $appName

        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""

        # Step 3: Ask if user wants to add to database
        Write-Host "Do you want to add this application to the database? " -ForegroundColor Yellow -NoNewline
        Write-Host "(Y/N): " -ForegroundColor White -NoNewline
        $addToDb = Read-Host

        if ($addToDb -ne 'Y' -and $addToDb -ne 'y') {
            Write-Host ""
            Write-Host "ℹ️  Application not added" -ForegroundColor Gray
            Read-Host "Press Enter to continue"
            return
        }

        # Step 4: Collect additional information
        Write-Host ""
        Write-Host "Step 2: Additional Information" -ForegroundColor Cyan
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""

        $appId = $appName -replace '\s+', ''
        Write-Host "Generated App ID: " -NoNewline -ForegroundColor Yellow
        Write-Host $appId -ForegroundColor White

        Write-Host ""
        $wingetId = Read-Host "Winget ID (leave empty if none)"
        $chocoId = Read-Host "Chocolatey ID (leave empty if none)"
        $storeId = Read-Host "Microsoft Store ID (leave empty if none)"
        $directUrl = Read-Host "Direct Download URL (leave empty if none)"

        Write-Host ""
        $category = Read-Host "Category (e.g., Browser, Development, Gaming)"
        $description = Read-Host "Description"
        $homepage = Read-Host "Homepage URL"

        Write-Host ""
        Write-Host "Detection Method:" -ForegroundColor Yellow
        Write-Host "  1. Registry"
        Write-Host "  2. File"
        Write-Host "  3. Command"
        Write-Host "  4. StoreApp"
        $detectionChoice = Read-Choice -Prompt "Select detection method [1-4]" -ValidChoices @('1','2','3','4')

        $detectionMethod = switch ($detectionChoice) {
            '1' { 'Registry' }
            '2' { 'File' }
            '3' { 'Command' }
            '4' { 'StoreApp' }
        }

        Write-Host ""
        $detectionPath = Read-Host "Detection path (e.g., 'HKLM:\SOFTWARE\AppName' or 'C:\Program Files\App\app.exe')"

        # Step 5: Generate JSON entry
        Write-Host ""
        Write-Host "Step 3: Review & Save" -ForegroundColor Cyan
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""

        $newApp = [PSCustomObject]@{
            Name                     = $appName
            Category                 = if ($category) { $category } else { "Other" }
            Description              = if ($description) { $description } else { "No description provided" }
            Sources                  = [PSCustomObject]@{
                Winget      = if ($wingetId) { $wingetId } else { $null }
                Chocolatey  = if ($chocoId) { $chocoId } else { $null }
                Store       = if ($storeId) { $storeId } else { $null }
                DirectUrl   = if ($directUrl) { $directUrl } else { $null }
            }
            Detection                = [PSCustomObject]@{
                Method = $detectionMethod
                Path   = $detectionPath
            }
            DefaultPriority          = 99
            DefaultRequired          = $false
            EnvironmentRestrictions  = @()
            Tags                     = @("user-added")
            LastVerified             = (Get-Date -Format "yyyy-MM-dd")
            Verified                 = $false
            Homepage                 = if ($homepage) { $homepage } else { $null }
        }

        Write-Host "Application JSON Preview:" -ForegroundColor Yellow
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
        $newApp | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Gray
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
        Write-Host ""

        Write-Host "Save this application to database? " -ForegroundColor Yellow -NoNewline
        Write-Host "(Y/N): " -ForegroundColor White -NoNewline
        $confirmSave = Read-Host

        if ($confirmSave -ne 'Y' -and $confirmSave -ne 'y') {
            Write-Host ""
            Write-Host "ℹ️  Application not saved" -ForegroundColor Gray
            Read-Host "Press Enter to continue"
            return
        }

        # Step 6: Add to database file
        $dbPath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'

        if (-not (Test-Path $dbPath)) {
            Write-Host ""
            Write-Host "❌ Database file not found: $dbPath" -ForegroundColor Red
            Read-Host "Press Enter to continue"
            return
        }

        # Load current database
        $db = Get-Content $dbPath -Raw | ConvertFrom-Json

        # Add new application
        $db.Applications | Add-Member -MemberType NoteProperty -Name $appId -Value $newApp -Force

        # Update metadata
        $db.TotalApplications = ($db.Applications.PSObject.Properties | Measure-Object).Count
        $db.LastUpdated = Get-Date -Format "yyyy-MM-dd"

        # Save database
        $db | ConvertTo-Json -Depth 10 | Set-Content $dbPath -Encoding UTF8

        # Reload database in GUI
        $script:AppDatabase[$appId] = $newApp

        # Update applications-data.js for ProfileCreator.html
        $jsPath = Join-Path $script:RepositoryRoot 'Tools\applications-data.js'
        $appsJson = $db.Applications | ConvertTo-Json -Depth 10 -Compress
        "const WIN11FORGE_APPS = $appsJson;" | Set-Content -Path $jsPath -Encoding UTF8

        Write-Host ""
        Write-Host "✅ Application '$appName' added successfully!" -ForegroundColor Green
        Write-Host "   - App ID: $appId" -ForegroundColor Gray
        Write-Host "   - Total apps in database: $($db.TotalApplications)" -ForegroundColor Gray
        Write-Host "   - applications-data.js updated" -ForegroundColor Gray
        Write-Host ""

    }
    catch {
        Write-Host ""
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
    }

    Read-Host "Press Enter to continue"
}

# ============================================================================
# MODULE EXPORT
# ============================================================================

Export-ModuleMember -Function @(
    'Initialize-GUIModules',
    'Show-MainMenu',
    'Show-Header',
    'Show-Footer',
    'Read-Choice'
)

