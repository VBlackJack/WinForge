<#
.SYNOPSIS
    Win11Forge GUI v3.6.8

.DESCRIPTION
    PowerShell graphical interface for Win11Forge deployment framework
    Provides interactive menus for profile selection, application browsing, and custom profile creation

.NOTES
    Author: Julien Bombled
    v3.6.8
    Requires: PowerShell 5.1+, Win11Forge v3.0.0+
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

# ============================================================================
# MODULE VARIABLES
# ============================================================================

$script:ModuleRoot = $PSScriptRoot
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:DatabaseLoaded = $false
$script:AppDatabase = $null

# Load framework version dynamically
$script:FrameworkVersion = try {
    $versionPath = Join-Path $script:RepositoryRoot 'Config\version.json'
    if (Test-Path $versionPath) {
        $versionData = Get-Content -Path $versionPath -Raw | ConvertFrom-Json
        $versionData.Version
    } else { '3.1.2' }
} catch { '3.1.2' }

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

        # Load Localization module for i18n support
        $locModule = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
        if (Test-Path $locModule) {
            Import-Module $locModule -Force -Global
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
            Write-Host (Get-LocalizedString -Key 'common.warning' -DefaultValue 'Warning') + ": Database file not found at $dbPath" -ForegroundColor Yellow
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
        Write-Host "$(Get-LocalizedString -Key 'common.error' -DefaultValue 'Error'): $_" -ForegroundColor Red
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
        Write-Host (Get-LocalizedString -Key 'gui.errors.db_not_loaded') -ForegroundColor Red
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
        [string]$Title = "Win11Forge v$script:FrameworkVersion"
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
    param()

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

        Write-Host "$(Get-LocalizedString -Key 'gui.menu.invalid_choice') $(Get-LocalizedString -Key 'gui.menu.press_back')" -ForegroundColor Red
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
        Show-Header -Title "Win11Forge v$script:FrameworkVersion - $(Get-LocalizedString -Key 'gui.menu.main_title' -DefaultValue 'Main Menu')"

        Write-Host "  1. $(Get-LocalizedString -Key 'gui.menu.deploy_profile')" -ForegroundColor White
        Write-Host "  2. $(Get-LocalizedString -Key 'gui.menu.browse_apps' -Parameters @{ Count = $script:AppDatabase.Count })" -ForegroundColor White
        Write-Host "  3. $(Get-LocalizedString -Key 'gui.menu.browse_profiles')" -ForegroundColor White
        Write-Host "  4. $(Get-LocalizedString -Key 'gui.menu.create_profile')" -ForegroundColor White
        Write-Host "  5. $(Get-LocalizedString -Key 'gui.menu.statistics')" -ForegroundColor White
        Write-Host "  6. $(Get-LocalizedString -Key 'gui.menu.validate_db')" -ForegroundColor White
        Write-Host "  7. $(Get-LocalizedString -Key 'gui.menu.add_app')" -ForegroundColor Cyan
        Write-Host "  0. $(Get-LocalizedString -Key 'gui.menu.exit')" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.menu.select_option') [0-7]" -ValidChoices @('0','1','2','3','4','5','6','7')

        switch ($choice) {
            '1' { Show-DeployProfileMenu }
            '2' { Show-ApplicationBrowser }
            '3' { Show-ProfileBrowser }
            '4' { Show-ProfileCreator }
            '5' { Show-DatabaseStatistics }
            '6' { Start-DatabaseValidation }
            '7' { Show-AddApplicationMenu }
            '0' {
                Write-Host "`n$(Get-LocalizedString -Key 'common.exit')!" -ForegroundColor Green
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

    Show-Header -Title (Get-LocalizedString -Key 'gui.deploy.title')

    # List available profiles
    $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
    $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

    Write-Host (Get-LocalizedString -Key 'gui.deploy.available_profiles') -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $profiles.Count; $i++) {
        $profile = $profiles[$i]
        $profileData = Get-Content $profile.FullName | ConvertFrom-Json

        $name = $profileData.Name
        $version = $profileData.Version
        $appCount = $profileData.Applications.Count

        Write-Host "  $($i + 1). $(Get-LocalizedString -Key 'gui.deploy.profile_info' -Parameters @{ Name = $name; Version = $version; Count = $appCount })" -ForegroundColor White
    }

    Write-Host "  0. $(Get-LocalizedString -Key 'gui.menu.back_to_main')" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$profiles.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.deploy.select_profile') [0-$($profiles.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedProfile = $profiles[$choice - 1]
    $profileName = $selectedProfile.BaseName

    # Deployment options
    Show-Header -Title "$(Get-LocalizedString -Key 'gui.deploy.title'): $profileName"

    Write-Host (Get-LocalizedString -Key 'gui.deploy.options_title') -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1. $(Get-LocalizedString -Key 'gui.deploy.mode_sequential')" -ForegroundColor White
    Write-Host "  2. $(Get-LocalizedString -Key 'gui.deploy.mode_parallel')" -ForegroundColor White
    Write-Host "  3. $(Get-LocalizedString -Key 'gui.deploy.mode_test')" -ForegroundColor White
    Write-Host "  0. $(Get-LocalizedString -Key 'common.cancel')" -ForegroundColor White

    Show-Footer

    $modeChoice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.deploy.select_mode') [0-3]" -ValidChoices @('0','1','2','3')

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
    Write-Host (Get-LocalizedString -Key 'gui.deploy.ready_to_deploy' -Parameters @{ Name = $profileName }) -ForegroundColor Yellow
    $modeName = if ($modeChoice -eq '2') { Get-LocalizedString -Key 'gui.deploy.mode_name_parallel' } elseif ($modeChoice -eq '3') { Get-LocalizedString -Key 'gui.deploy.mode_name_test' } else { Get-LocalizedString -Key 'gui.deploy.mode_name_sequential' }
    Write-Host (Get-LocalizedString -Key 'gui.deploy.mode_label' -Parameters @{ Mode = $modeName }) -ForegroundColor Gray
    Write-Host ""

    $confirm = Read-Host (Get-LocalizedString -Key 'gui.deploy.confirm_start')

    if ($confirm -eq 'Y' -or $confirm -eq 'y') {
        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.deploy.starting') -ForegroundColor Green
        Write-Host ""

        # Execute deployment script using & with splatting
        & $deployScript @deployParams
        $deployExitCode = $LASTEXITCODE

        Write-Host ""

        # Show deployment result summary
        if ($deployExitCode -eq 0) {
            Write-Host (Get-LocalizedString -Key 'gui.deploy.completed') -ForegroundColor Green
        } else {
            Write-Host (Get-LocalizedString -Key 'gui.deploy.completed_with_failures' -Parameters @{ Code = $deployExitCode }) -ForegroundColor Red
        }

        Write-Host ""
        Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
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
        Show-Header -Title (Get-LocalizedString -Key 'gui.apps.browse_title' -Parameters @{ Count = $script:AppDatabase.Count })

        Write-Host (Get-LocalizedString -Key 'gui.apps.browse_options') -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  1. $(Get-LocalizedString -Key 'gui.apps.view_all')" -ForegroundColor White
        Write-Host "  2. $(Get-LocalizedString -Key 'gui.apps.by_category')" -ForegroundColor White
        Write-Host "  3. $(Get-LocalizedString -Key 'gui.apps.by_tag')" -ForegroundColor White
        Write-Host "  4. $(Get-LocalizedString -Key 'gui.apps.search')" -ForegroundColor White
        Write-Host "  5. $(Get-LocalizedString -Key 'gui.apps.view_details')" -ForegroundColor White
        Write-Host "  0. $(Get-LocalizedString -Key 'gui.menu.back_to_main')" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.menu.select_option') [0-5]" -ValidChoices @('0','1','2','3','4','5')

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

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.all_apps_title')

    $apps = Get-DatabaseApps | Sort-Object -Property Name
    $pageSize = 20
    $currentPage = 0
    $totalPages = [math]::Ceiling($apps.Count / $pageSize)

    while ($true) {
        $start = $currentPage * $pageSize
        $end = [math]::Min($start + $pageSize, $apps.Count)
        $pageApps = $apps[$start..($end - 1)]

        Show-Header -Title "$(Get-LocalizedString -Key 'gui.apps.all_apps_title') ($(Get-LocalizedString -Key 'gui.apps.page_info' -Parameters @{ Current = ($currentPage + 1); Total = $totalPages }))"

        Write-Host ("{0,-5} {1,-30} {2,-15} {3}" -f (Get-LocalizedString -Key 'gui.apps.col_no'), (Get-LocalizedString -Key 'gui.apps.col_name'), (Get-LocalizedString -Key 'gui.apps.col_category'), (Get-LocalizedString -Key 'gui.apps.col_winget')) -ForegroundColor Yellow
        Write-Host ("-" * 70) -ForegroundColor DarkGray

        for ($i = 0; $i -lt $pageApps.Count; $i++) {
            $app = $pageApps[$i]
            $num = $start + $i + 1
            $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }

            Write-Host ("{0,-5} {1,-30} {2,-15} {3}" -f $num, $app.Name, $app.Category, $wingetId) -ForegroundColor White
        }

        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.apps.nav_help') -ForegroundColor Gray

        Show-Footer

        $nav = Read-Choice -Prompt (Get-LocalizedString -Key 'gui.apps.navigate') -ValidChoices @('N','n','P','p','Q','q')

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

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.category_title')

    $categories = Get-DatabaseCategories | Sort-Object

    Write-Host (Get-LocalizedString -Key 'gui.apps.available_categories') -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $categories.Count; $i++) {
        $category = $categories[$i]
        $count = (Get-DatabaseApps -Category $category).Count
        Write-Host "  $($i + 1). $(Get-LocalizedString -Key 'gui.apps.category_count' -Parameters @{ Name = $category; Count = $count })" -ForegroundColor White
    }

    Write-Host "  0. $(Get-LocalizedString -Key 'common.back')" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$categories.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.apps.select_category') [0-$($categories.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedCategory = $categories[$choice - 1]
    $apps = Get-DatabaseApps -Category $selectedCategory | Sort-Object -Property Name

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.category_header' -Parameters @{ Name = $selectedCategory; Count = $apps.Count })

    Write-Host ("{0,-30} {1}" -f (Get-LocalizedString -Key 'gui.apps.col_name'), (Get-LocalizedString -Key 'gui.apps.col_winget')) -ForegroundColor Yellow
    Write-Host ("-" * 70) -ForegroundColor DarkGray

    foreach ($app in $apps) {
        $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
        Write-Host ("{0,-30} {1}" -f $app.Name, $wingetId) -ForegroundColor White
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Show-ApplicationsByTag {
    <#
    .SYNOPSIS
        Browse applications by tag
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.tag_title')

    # Get all unique tags
    $allApps = Get-DatabaseApps
    $tags = $allApps | Where-Object { $_.Tags } | ForEach-Object { $_.Tags } | Select-Object -Unique | Sort-Object

    Write-Host (Get-LocalizedString -Key 'gui.apps.available_tags') -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $tags.Count; $i++) {
        $tag = $tags[$i]
        $count = (Get-DatabaseApps -Tag $tag).Count
        Write-Host "  $($i + 1). $(Get-LocalizedString -Key 'gui.apps.tag_count' -Parameters @{ Name = $tag; Count = $count })" -ForegroundColor White
    }

    Write-Host "  0. $(Get-LocalizedString -Key 'common.back')" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$tags.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.apps.select_tag') [0-$($tags.Count)]" -ValidChoices $validChoices

    if ($choice -eq '0') { return }

    $selectedTag = $tags[$choice - 1]
    $apps = Get-DatabaseApps -Tag $selectedTag | Sort-Object -Property Name

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.tag_header' -Parameters @{ Name = $selectedTag; Count = $apps.Count })

    Write-Host ("{0,-30} {1,-15} {2}" -f (Get-LocalizedString -Key 'gui.apps.col_name'), (Get-LocalizedString -Key 'gui.apps.col_category'), (Get-LocalizedString -Key 'gui.apps.col_winget')) -ForegroundColor Yellow
    Write-Host ("-" * 70) -ForegroundColor DarkGray

    foreach ($app in $apps) {
        $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
        Write-Host ("{0,-30} {1,-15} {2}" -f $app.Name, $app.Category, $wingetId) -ForegroundColor White
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Show-ApplicationSearch {
    <#
    .SYNOPSIS
        Search applications by name
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.search_title')

    Write-Host (Get-LocalizedString -Key 'gui.apps.search_prompt') -ForegroundColor Yellow
    $searchTerm = Read-Host (Get-LocalizedString -Key 'gui.apps.search_label')

    if ([string]::IsNullOrWhiteSpace($searchTerm)) { return }

    $results = Search-DatabaseApps -SearchTerm $searchTerm

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.search_results' -Parameters @{ Term = $searchTerm; Count = $results.Count })

    if ($results.Count -eq 0) {
        Write-Host (Get-LocalizedString -Key 'gui.apps.no_results' -Parameters @{ Term = $searchTerm }) -ForegroundColor Yellow
    }
    else {
        Write-Host ("{0,-30} {1,-15} {2}" -f (Get-LocalizedString -Key 'gui.apps.col_name'), (Get-LocalizedString -Key 'gui.apps.col_category'), (Get-LocalizedString -Key 'gui.apps.col_winget')) -ForegroundColor Yellow
        Write-Host ("-" * 70) -ForegroundColor DarkGray

        foreach ($app in $results) {
            $wingetId = if ($app.Sources -and $app.Sources.PSObject.Properties['Winget']) { $app.Sources.Winget } else { "-" }
            Write-Host ("{0,-30} {1,-15} {2}" -f $app.Name, $app.Category, $wingetId) -ForegroundColor White
        }
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Show-ApplicationDetails {
    <#
    .SYNOPSIS
        Display detailed information about an application
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.details_title')

    Write-Host (Get-LocalizedString -Key 'gui.apps.details_prompt') -ForegroundColor Yellow
    $input = Read-Host (Get-LocalizedString -Key 'gui.apps.details_input')

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
            Write-Host (Get-LocalizedString -Key 'gui.apps.multiple_found') -ForegroundColor Yellow
            Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
            return
        }
    }

    if (-not $app) {
        Write-Host (Get-LocalizedString -Key 'gui.apps.not_found' -Parameters @{ Input = $input }) -ForegroundColor Red
        Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
        return
    }

    Show-Header -Title (Get-LocalizedString -Key 'gui.apps.app_header' -Parameters @{ Name = $app.Name })

    Write-Host "$(Get-LocalizedString -Key 'gui.apps.label_appid')        " -NoNewline -ForegroundColor Yellow
    Write-Host $app.AppId -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.apps.label_name')         " -NoNewline -ForegroundColor Yellow
    Write-Host $app.Name -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.apps.label_category')     " -NoNewline -ForegroundColor Yellow
    Write-Host $app.Category -ForegroundColor White

    if ($app.Description) {
        Write-Host "$(Get-LocalizedString -Key 'gui.apps.label_description')  " -NoNewline -ForegroundColor Yellow
        Write-Host $app.Description -ForegroundColor White
    }

    Write-Host "`n$(Get-LocalizedString -Key 'gui.apps.label_sources')" -ForegroundColor Yellow
    if ($app.Sources -and $app.Sources.PSObject.Properties['Winget'] -and $app.Sources.Winget) {
        Write-Host "  $(Get-LocalizedString -Key 'gui.apps.label_winget')     " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Winget -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['Chocolatey'] -and $app.Sources.Chocolatey) {
        Write-Host "  $(Get-LocalizedString -Key 'gui.apps.label_chocolatey') " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Chocolatey -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['Store'] -and $app.Sources.Store) {
        Write-Host "  $(Get-LocalizedString -Key 'gui.apps.label_store')      " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.Store -ForegroundColor White
    }
    if ($app.Sources -and $app.Sources.PSObject.Properties['DirectUrl'] -and $app.Sources.DirectUrl) {
        Write-Host "  $(Get-LocalizedString -Key 'gui.apps.label_directurl')  " -NoNewline -ForegroundColor Gray
        Write-Host $app.Sources.DirectUrl -ForegroundColor White
    }

    if ($app.Tags -and $app.Tags.Count -gt 0) {
        Write-Host "`n$(Get-LocalizedString -Key 'gui.apps.label_tags')         " -NoNewline -ForegroundColor Yellow
        Write-Host ($app.Tags -join ', ') -ForegroundColor White
    }

    if ($app.Homepage) {
        Write-Host "$(Get-LocalizedString -Key 'gui.apps.label_homepage')     " -NoNewline -ForegroundColor Yellow
        Write-Host $app.Homepage -ForegroundColor White
    }

    if ($app.Verified) {
        Write-Host "`n$(Get-LocalizedString -Key 'gui.apps.label_verified')     " -NoNewline -ForegroundColor Yellow
        Write-Host (Get-LocalizedString -Key 'gui.apps.verified_yes' -Parameters @{ Date = $app.LastVerified }) -ForegroundColor Green
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

# ============================================================================
# PROFILE BROWSER
# ============================================================================

function Show-ProfileBrowser {
    <#
    .SYNOPSIS
        Browse available profiles
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.profiles.browse_title')

    $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
    $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

    Write-Host (Get-LocalizedString -Key 'gui.profiles.available') -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $profiles.Count; $i++) {
        $profile = $profiles[$i]
        $profileData = Get-Content $profile.FullName | ConvertFrom-Json

        Write-Host "  $($i + 1). $(Get-LocalizedString -Key 'gui.profiles.profile_info' -Parameters @{ Name = $profileData.Name; Version = $profileData.Version })" -ForegroundColor White
        Write-Host "      $(Get-LocalizedString -Key 'gui.profiles.apps_count' -Parameters @{ Count = $profileData.Applications.Count })" -ForegroundColor Gray
        Write-Host "      $(Get-LocalizedString -Key 'gui.profiles.desc_label' -Parameters @{ Description = $profileData.Description })" -ForegroundColor Gray
        Write-Host ""
    }

    Write-Host "  0. $(Get-LocalizedString -Key 'gui.menu.back_to_main')" -ForegroundColor White

    Show-Footer

    $validChoices = @('0') + (1..$profiles.Count | ForEach-Object { $_.ToString() })
    $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.profiles.select_view') [0-$($profiles.Count)]" -ValidChoices $validChoices

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

    Show-Header -Title (Get-LocalizedString -Key 'gui.profiles.details_title' -Parameters @{ Name = $profileData.Name })

    Write-Host "$(Get-LocalizedString -Key 'gui.profiles.label_name')         " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Name -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.profiles.label_version')      " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Version -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.profiles.label_description')  " -NoNewline -ForegroundColor Yellow
    Write-Host $profileData.Description -ForegroundColor White

    if ($profileData.Inherits -and $profileData.Inherits.Count -gt 0) {
        Write-Host "$(Get-LocalizedString -Key 'gui.profiles.label_inherits')     " -NoNewline -ForegroundColor Yellow
        Write-Host ($profileData.Inherits -join ', ') -ForegroundColor White
    }

    Write-Host "`n$(Get-LocalizedString -Key 'gui.profiles.label_applications') " -NoNewline -ForegroundColor Yellow
    Write-Host (Get-LocalizedString -Key 'gui.profiles.apps_summary' -Parameters @{ Count = $profileData.Applications.Count }) -ForegroundColor White
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
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

# ============================================================================
# PROFILE CREATOR
# ============================================================================

function Show-ProfileCreator {
    <#
    .SYNOPSIS
        Interactive profile creation wizard
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.creator.title')

    Write-Host (Get-LocalizedString -Key 'gui.creator.wizard_intro') -ForegroundColor Yellow
    Write-Host ""

    # Profile name
    Write-Host (Get-LocalizedString -Key 'gui.creator.enter_name') -ForegroundColor Yellow
    $profileName = Read-Choice -Prompt (Get-LocalizedString -Key 'gui.creator.name_prompt')

    # Profile description
    Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.enter_desc')" -ForegroundColor Yellow
    $profileDesc = Read-Choice -Prompt (Get-LocalizedString -Key 'gui.creator.desc_prompt')

    # Inheritance
    Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.inherit_prompt')" -ForegroundColor Yellow
    $inheritChoice = Read-Host (Get-LocalizedString -Key 'gui.creator.inherit_label')

    $inherits = @()
    if ($inheritChoice -eq 'Y' -or $inheritChoice -eq 'y') {
        $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
        $profiles = Get-ChildItem -Path $profilesPath -Filter '*.json' | Where-Object { $_.Name -notlike '*legacy*' }

        Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.available_inherit')" -ForegroundColor Yellow
        for ($i = 0; $i -lt $profiles.Count; $i++) {
            Write-Host "  $($i + 1). $($profiles[$i].BaseName)" -ForegroundColor White
        }

        $validChoices = 1..$profiles.Count | ForEach-Object { $_.ToString() }
        $inheritIdx = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.creator.select_inherit') [1-$($profiles.Count)]" -ValidChoices $validChoices

        $inherits = @($profiles[$inheritIdx - 1].BaseName)
    }

    # Application selection
    Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.select_apps_title')" -ForegroundColor Yellow
    Write-Host "  1. $(Get-LocalizedString -Key 'gui.creator.method_browse')" -ForegroundColor White
    Write-Host "  2. $(Get-LocalizedString -Key 'gui.creator.method_manual')" -ForegroundColor White

    $appChoice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.creator.method_prompt') [1-2]" -ValidChoices @('1','2')

    $selectedApps = @()

    if ($appChoice -eq '1') {
        $selectedApps = Select-ApplicationsFromDatabase
    }
    else {
        Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.enter_appids')" -ForegroundColor Yellow
        $appInput = Read-Host (Get-LocalizedString -Key 'gui.creator.appids_prompt')
        $selectedApps = $appInput -split ',' | ForEach-Object { $_.Trim() }
    }

    # Create profile object
    $newProfile = [PSCustomObject]@{
        Name = $profileName
        Description = $profileDesc
        Version = $script:FrameworkVersion
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
    Show-Header -Title (Get-LocalizedString -Key 'gui.creator.preview_title')

    Write-Host ($newProfile | ConvertTo-Json -Depth 10) -ForegroundColor Gray

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.creator.save_prompt') -ForegroundColor Yellow
    $saveChoice = Read-Host (Get-LocalizedString -Key 'gui.creator.save_label')

    if ($saveChoice -eq 'Y' -or $saveChoice -eq 'y') {
        $profilesPath = Join-Path $script:RepositoryRoot 'Profiles'
        $profileFile = Join-Path $profilesPath "$profileName.json"

        $newProfile | ConvertTo-Json -Depth 10 | Set-Content -Path $profileFile -Encoding UTF8

        Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.saved' -Parameters @{ Path = $profileFile })" -ForegroundColor Green
    }
    else {
        Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.discarded')" -ForegroundColor Yellow
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Select-ApplicationsFromDatabase {
    <#
    .SYNOPSIS
        Interactive application selection from database
    #>

    $selected = @()

    while ($true) {
        Show-Header -Title (Get-LocalizedString -Key 'gui.creator.select_title' -Parameters @{ Count = $selected.Count })

        Write-Host (Get-LocalizedString -Key 'gui.creator.options') -ForegroundColor Yellow
        Write-Host "  1. $(Get-LocalizedString -Key 'gui.creator.add_by_category')" -ForegroundColor White
        Write-Host "  2. $(Get-LocalizedString -Key 'gui.creator.add_by_tag')" -ForegroundColor White
        Write-Host "  3. $(Get-LocalizedString -Key 'gui.creator.add_by_search')" -ForegroundColor White
        Write-Host "  4. $(Get-LocalizedString -Key 'gui.creator.add_by_appid')" -ForegroundColor White
        Write-Host "  5. $(Get-LocalizedString -Key 'gui.creator.view_selected' -Parameters @{ Count = $selected.Count })" -ForegroundColor White
        Write-Host "  6. $(Get-LocalizedString -Key 'gui.creator.remove_selected')" -ForegroundColor White
        Write-Host "  0. $(Get-LocalizedString -Key 'gui.creator.done')" -ForegroundColor White

        Show-Footer

        $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.settings.option_prompt') [0-6]" -ValidChoices @('0','1','2','3','4','5','6')

        switch ($choice) {
            '1' {
                $categories = Get-DatabaseCategories | Sort-Object
                Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.categories_label')" -ForegroundColor Yellow
                Write-Host "  0. $(Get-LocalizedString -Key 'gui.creator.cancel')" -ForegroundColor Gray
                for ($i = 0; $i -lt $categories.Count; $i++) {
                    Write-Host "  $($i + 1). $($categories[$i])" -ForegroundColor White
                }
                $validChoices = 1..$categories.Count | ForEach-Object { $_.ToString() }
                $catChoice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.apps.select_category') [1-$($categories.Count)]" -ValidChoices $validChoices
                if ($catChoice -ne '0') {
                    $apps = Get-DatabaseApps -Category $categories[$catChoice - 1]
                    $selected += $apps | ForEach-Object { $_.AppId }
                }
            }
            '2' {
                $allApps = Get-DatabaseApps
                $tags = $allApps | Where-Object { $_.Tags } | ForEach-Object { $_.Tags } | Select-Object -Unique | Sort-Object
                Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.tags_label')" -ForegroundColor Yellow
                Write-Host "  0. $(Get-LocalizedString -Key 'gui.creator.cancel')" -ForegroundColor Gray
                for ($i = 0; $i -lt $tags.Count; $i++) {
                    Write-Host "  $($i + 1). $($tags[$i])" -ForegroundColor White
                }
                $validChoices = 1..$tags.Count | ForEach-Object { $_.ToString() }
                $tagChoice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.apps.select_tag') [1-$($tags.Count)]" -ValidChoices $validChoices
                if ($tagChoice -ne '0') {
                    $apps = Get-DatabaseApps -Tag $tags[$tagChoice - 1]
                    $selected += $apps | ForEach-Object { $_.AppId }
                }
            }
            '3' {
                Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.search_term')" -ForegroundColor Yellow
                $searchTerm = Read-Host (Get-LocalizedString -Key 'gui.apps.search_label')
                $apps = Search-DatabaseApps -SearchTerm $searchTerm
                $selected += $apps | ForEach-Object { $_.AppId }
            }
            '4' {
                Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.appid_label')" -ForegroundColor Yellow
                $appId = Read-Host (Get-LocalizedString -Key 'gui.creator.appid_label')
                if (Get-DatabaseAppById -AppId $appId) {
                    $selected += $appId
                }
                else {
                    Write-Host (Get-LocalizedString -Key 'gui.creator.invalid_appid' -Parameters @{ AppId = $appId }) -ForegroundColor Red
                }
            }
            '5' {
                Show-Header -Title (Get-LocalizedString -Key 'gui.creator.selected_apps_title' -Parameters @{ Count = $selected.Count })
                $selected | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
                Read-Host "`n$(Get-LocalizedString -Key 'gui.deploy.press_enter')"
            }
            '6' {
                Write-Host "`n$(Get-LocalizedString -Key 'gui.creator.appid_remove')" -ForegroundColor Yellow
                $removeId = Read-Host (Get-LocalizedString -Key 'gui.creator.appid_label')
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

    Show-Header -Title (Get-LocalizedString -Key 'gui.statistics.title')

    $stats = Get-DatabaseStats

    Write-Host "$(Get-LocalizedString -Key 'gui.statistics.total_apps')      " -NoNewline -ForegroundColor Yellow
    Write-Host $stats.TotalApplications -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.statistics.verified_apps')   " -NoNewline -ForegroundColor Yellow
    Write-Host (Get-LocalizedString -Key 'gui.statistics.verified_percent' -Parameters @{ Count = $stats.VerifiedApplications; Percent = [math]::Round($stats.VerificationRate) }) -ForegroundColor Green

    Write-Host "$(Get-LocalizedString -Key 'gui.statistics.categories')              " -NoNewline -ForegroundColor Yellow
    Write-Host $stats.TotalCategories -ForegroundColor White

    Write-Host "`n$(Get-LocalizedString -Key 'gui.statistics.sources_title')" -ForegroundColor Yellow
    Write-Host "  $(Get-LocalizedString -Key 'gui.statistics.label_winget')                " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithWinget -ForegroundColor White
    Write-Host "  $(Get-LocalizedString -Key 'gui.statistics.label_chocolatey')            " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithChocolatey -ForegroundColor White
    Write-Host "  $(Get-LocalizedString -Key 'gui.statistics.label_store')                 " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithStore -ForegroundColor White
    Write-Host "  $(Get-LocalizedString -Key 'gui.statistics.label_directurl')             " -NoNewline -ForegroundColor Gray
    Write-Host $stats.AppsWithDirectUrl -ForegroundColor White

    Write-Host "`n$(Get-LocalizedString -Key 'gui.statistics.top_categories')" -ForegroundColor Yellow
    $stats.CategoryBreakdown.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Host ("  {0,-20} {1}" -f $_.Key, $_.Value) -ForegroundColor White
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

# ============================================================================
# DATABASE VALIDATION
# ============================================================================

function Start-DatabaseValidation {
    <#
    .SYNOPSIS
        Run database validation
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.validation.title')

    Write-Host (Get-LocalizedString -Key 'gui.validation.intro') -ForegroundColor Yellow
    Write-Host (Get-LocalizedString -Key 'gui.validation.time_warning') -ForegroundColor Yellow
    Write-Host ""

    $confirm = Read-Host (Get-LocalizedString -Key 'gui.validation.confirm')

    if ($confirm -ne 'Y' -and $confirm -ne 'y') { return }

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.validation.validating') -ForegroundColor Green
    Write-Host ""

    $validationScript = Join-Path $script:RepositoryRoot 'Tools\Validate-AppDatabase.ps1'

    if (Test-Path $validationScript) {
        & $validationScript -ValidateWinget -ValidateChocolatey
    }
    else {
        Write-Host (Get-LocalizedString -Key 'gui.validation.script_not_found' -Parameters @{ Path = $validationScript }) -ForegroundColor Red
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

# ============================================================================
# SETTINGS MENU
# ============================================================================

function Show-SettingsMenu {
    <#
    .SYNOPSIS
        Display settings and options
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.settings.title')

    Write-Host "  1. $(Get-LocalizedString -Key 'gui.settings.view_info')" -ForegroundColor White
    Write-Host "  2. $(Get-LocalizedString -Key 'gui.settings.view_logs')" -ForegroundColor White
    Write-Host "  3. $(Get-LocalizedString -Key 'gui.settings.check_updates')" -ForegroundColor White
    Write-Host "  4. $(Get-LocalizedString -Key 'gui.settings.about')" -ForegroundColor White
    Write-Host "  0. $(Get-LocalizedString -Key 'gui.menu.back_to_main')" -ForegroundColor White

    Show-Footer

    $choice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.settings.option_prompt') [0-4]" -ValidChoices @('0','1','2','3','4')

    switch ($choice) {
        '1' { Show-FrameworkInfo }
        '2' { Show-LogsDirectory }
        '3' { Test-Updates }
        '4' { Show-About }
        '0' { return }
    }
}

function Show-FrameworkInfo {
    Show-Header -Title (Get-LocalizedString -Key 'gui.info.title')

    Write-Host "$(Get-LocalizedString -Key 'gui.info.version')      " -NoNewline -ForegroundColor Yellow
    Write-Host $script:FrameworkVersion -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.info.ps_version')      " -NoNewline -ForegroundColor Yellow
    Write-Host "$($PSVersionTable.PSVersion)" -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.info.repo_path')         " -NoNewline -ForegroundColor Yellow
    Write-Host $script:RepositoryRoot -ForegroundColor White

    Write-Host "$(Get-LocalizedString -Key 'gui.info.db_loaded')         " -NoNewline -ForegroundColor Yellow
    Write-Host $script:DatabaseLoaded -ForegroundColor White

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Show-LogsDirectory {
    $logsPath = Join-Path $script:RepositoryRoot 'Logs'

    Show-Header -Title (Get-LocalizedString -Key 'gui.logs.title')

    Write-Host (Get-LocalizedString -Key 'gui.logs.path' -Parameters @{ Path = $logsPath }) -ForegroundColor Yellow
    Write-Host ""

    if (Test-Path $logsPath) {
        $logs = Get-ChildItem -Path $logsPath -Filter '*.log' | Sort-Object -Property LastWriteTime -Descending

        if ($logs.Count -gt 0) {
            Write-Host (Get-LocalizedString -Key 'gui.logs.recent') -ForegroundColor Yellow
            $logs | Select-Object -First 10 | ForEach-Object {
                Write-Host "  $($_.Name) - $($_.LastWriteTime)" -ForegroundColor White
            }
        }
        else {
            Write-Host (Get-LocalizedString -Key 'gui.logs.no_logs') -ForegroundColor Gray
        }
    }
    else {
        Write-Host (Get-LocalizedString -Key 'gui.logs.no_directory') -ForegroundColor Gray
    }

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Test-Updates {
    Show-Header -Title (Get-LocalizedString -Key 'gui.updates.title')

    Write-Host (Get-LocalizedString -Key 'gui.updates.current_version' -Parameters @{ Version = $script:FrameworkVersion }) -ForegroundColor Yellow
    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.updates.no_mechanism') -ForegroundColor Gray

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

function Show-About {
    Show-Header -Title (Get-LocalizedString -Key 'gui.about.title')

    Write-Host "Win11Forge v$script:FrameworkVersion" -ForegroundColor Cyan
    Write-Host (Get-LocalizedString -Key 'gui.about.subtitle') -ForegroundColor White
    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.about.features_title') -ForegroundColor Yellow
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_apps' -Parameters @{ Count = $script:AppDatabase.Count })" -ForegroundColor White
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_profiles')" -ForegroundColor White
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_parallel')" -ForegroundColor White
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_gui')" -ForegroundColor White
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_database')" -ForegroundColor White
    Write-Host "  - $(Get-LocalizedString -Key 'gui.about.feature_custom')" -ForegroundColor White
    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.about.docs') -ForegroundColor Gray

    Write-Host ""
    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
}

# ============================================================================
# ADD APPLICATION MENU
# ============================================================================

function Show-AddApplicationMenu {
    <#
    .SYNOPSIS
        Interactive menu to add a new application to the database
    #>

    Show-Header -Title (Get-LocalizedString -Key 'gui.add_app.title')

    Write-Host (Get-LocalizedString -Key 'gui.add_app.wizard_intro') -ForegroundColor Yellow
    Write-Host ""

    # Step 1: Get application name
    Write-Host (Get-LocalizedString -Key 'gui.add_app.step_name') -ForegroundColor Cyan
    Write-Host "-------------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host ""
    $appName = Read-Host (Get-LocalizedString -Key 'gui.add_app.enter_name')

    if ([string]::IsNullOrWhiteSpace($appName)) {
        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.add_app.name_empty') -ForegroundColor Red
        Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
        return
    }

    Write-Host ""
    Write-Host (Get-LocalizedString -Key 'gui.add_app.searching' -Parameters @{ Name = $appName }) -ForegroundColor Yellow
    Write-Host ""

    # Step 2: Run search script
    $searchScript = Join-Path $script:RepositoryRoot 'Tools\Search-ApplicationSources.ps1'

    if (-not (Test-Path $searchScript)) {
        Write-Host (Get-LocalizedString -Key 'gui.add_app.search_script_missing' -Parameters @{ Path = $searchScript }) -ForegroundColor Red
        Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
        return
    }

    try {
        # Execute search
        & $searchScript -AppName $appName

        Write-Host ""
        Write-Host "-------------------------------------------------------------------" -ForegroundColor Cyan
        Write-Host ""

        # Step 3: Ask if user wants to add to database
        Write-Host (Get-LocalizedString -Key 'gui.add_app.add_to_db_prompt') -ForegroundColor Yellow -NoNewline
        Write-Host " (Y/N): " -ForegroundColor White -NoNewline
        $addToDb = Read-Host

        if ($addToDb -ne 'Y' -and $addToDb -ne 'y') {
            Write-Host ""
            Write-Host (Get-LocalizedString -Key 'gui.add_app.not_added') -ForegroundColor Gray
            Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
            return
        }

        # Step 4: Collect additional information
        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.add_app.step_info') -ForegroundColor Cyan
        Write-Host "-------------------------------------------------------------------" -ForegroundColor Cyan
        Write-Host ""

        $appId = $appName -replace '\s+', ''
        Write-Host "$(Get-LocalizedString -Key 'gui.add_app.generated_id') " -NoNewline -ForegroundColor Yellow
        Write-Host $appId -ForegroundColor White

        Write-Host ""
        $wingetId = Read-Host (Get-LocalizedString -Key 'gui.add_app.winget_id')
        $chocoId = Read-Host (Get-LocalizedString -Key 'gui.add_app.choco_id')
        $storeId = Read-Host (Get-LocalizedString -Key 'gui.add_app.store_id')
        $directUrl = Read-Host (Get-LocalizedString -Key 'gui.add_app.direct_url')

        Write-Host ""
        $category = Read-Host (Get-LocalizedString -Key 'gui.add_app.category_prompt')
        $description = Read-Host (Get-LocalizedString -Key 'gui.add_app.description_prompt')
        $homepage = Read-Host (Get-LocalizedString -Key 'gui.add_app.homepage_prompt')

        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.add_app.detection_title') -ForegroundColor Yellow
        Write-Host "  1. $(Get-LocalizedString -Key 'gui.add_app.detection_registry')"
        Write-Host "  2. $(Get-LocalizedString -Key 'gui.add_app.detection_file')"
        Write-Host "  3. $(Get-LocalizedString -Key 'gui.add_app.detection_command')"
        Write-Host "  4. $(Get-LocalizedString -Key 'gui.add_app.detection_storeapp')"
        $detectionChoice = Read-Choice -Prompt "$(Get-LocalizedString -Key 'gui.add_app.select_detection') [1-4]" -ValidChoices @('1','2','3','4')

        $detectionMethod = switch ($detectionChoice) {
            '1' { 'Registry' }
            '2' { 'File' }
            '3' { 'Command' }
            '4' { 'StoreApp' }
        }

        Write-Host ""
        $detectionPath = Read-Host (Get-LocalizedString -Key 'gui.add_app.detection_path')

        # Step 5: Generate JSON entry
        Write-Host ""
        Write-Host (Get-LocalizedString -Key 'gui.add_app.step_review') -ForegroundColor Cyan
        Write-Host "-------------------------------------------------------------------" -ForegroundColor Cyan
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

        Write-Host (Get-LocalizedString -Key 'gui.add_app.json_preview') -ForegroundColor Yellow
        Write-Host "-------------------------------------------------------------------" -ForegroundColor Gray
        $newApp | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Gray
        Write-Host "-------------------------------------------------------------------" -ForegroundColor Gray
        Write-Host ""

        Write-Host (Get-LocalizedString -Key 'gui.add_app.save_prompt') -ForegroundColor Yellow -NoNewline
        Write-Host " (Y/N): " -ForegroundColor White -NoNewline
        $confirmSave = Read-Host

        if ($confirmSave -ne 'Y' -and $confirmSave -ne 'y') {
            Write-Host ""
            Write-Host (Get-LocalizedString -Key 'gui.add_app.not_saved') -ForegroundColor Gray
            Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
            return
        }

        # Step 6: Add to database file
        $dbPath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'

        if (-not (Test-Path $dbPath)) {
            Write-Host ""
            Write-Host (Get-LocalizedString -Key 'gui.add_app.db_not_found' -Parameters @{ Path = $dbPath }) -ForegroundColor Red
            Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
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
        Write-Host (Get-LocalizedString -Key 'gui.add_app.success' -Parameters @{ Name = $appName }) -ForegroundColor Green
        Write-Host "   - $(Get-LocalizedString -Key 'gui.add_app.success_appid' -Parameters @{ AppId = $appId })" -ForegroundColor Gray
        Write-Host "   - $(Get-LocalizedString -Key 'gui.add_app.success_total' -Parameters @{ Count = $db.TotalApplications })" -ForegroundColor Gray
        Write-Host "   - $(Get-LocalizedString -Key 'gui.add_app.success_js')" -ForegroundColor Gray
        Write-Host ""

    }
    catch {
        Write-Host ""
        Write-Host "$(Get-LocalizedString -Key 'common.error'): $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
    }

    Read-Host (Get-LocalizedString -Key 'gui.deploy.press_enter')
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

