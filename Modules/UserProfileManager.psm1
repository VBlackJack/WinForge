<#
.SYNOPSIS
    Win11Forge - User Profile Manager v3.6.8

.DESCRIPTION
    Manages persistent user profiles for Win11Forge:
    - Save custom application selections as profiles
    - Load and manage user-created profiles
    - Import/export profiles for sharing
    - Profile versioning and migration

.NOTES
    Author: Julien Bombled
    v3.6.8
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
$script:UserProfilesDir = Join-Path $env:LOCALAPPDATA 'Win11Forge\UserProfiles'
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\user-profiles-settings.json'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === CONFIGURATION ===
$script:ProfileSchema = @{
    Version = '1.0'
    RequiredFields = @('Name', 'Applications')
    OptionalFields = @('Description', 'Author', 'Tags', 'CreatedAt', 'ModifiedAt', 'Settings')
}

# === INITIALIZATION ===

function Initialize-UserProfileManager {
    <#
    .SYNOPSIS
        Initializes the user profile manager.

    .DESCRIPTION
        Creates the user profiles directory if it doesn't exist.

    .EXAMPLE
        Initialize-UserProfileManager
    #>
    [CmdletBinding()]
    param()

    if (-not (Test-Path $script:UserProfilesDir)) {
        try {
            New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
            Write-Verbose "Created user profiles directory: $script:UserProfilesDir"
        } catch {
            Write-Warning (Get-LocalizedString -Key 'userProfiles.error.createDirFailed' -Parameters @{ Error = $_.Exception.Message })
        }
    }
}

# === PROFILE MANAGEMENT ===

function Save-UserProfile {
    <#
    .SYNOPSIS
        Saves a custom user profile.

    .DESCRIPTION
        Creates or updates a user profile with the specified applications.

    .PARAMETER Name
        Name of the profile.

    .PARAMETER Applications
        Array of application IDs to include.

    .PARAMETER Description
        Optional description.

    .PARAMETER Author
        Optional author name.

    .PARAMETER Tags
        Optional tags for categorization.

    .PARAMETER Settings
        Optional hashtable of additional settings.

    .PARAMETER Overwrite
        Overwrite existing profile with same name.

    .OUTPUTS
        String containing the profile file path.

    .EXAMPLE
        Save-UserProfile -Name 'MyDevSetup' -Applications @('VSCode', 'Git', 'NodeJS')
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('^[a-zA-Z0-9_-]+$')]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Applications,

        [Parameter()]
        [string]$Description = '',

        [Parameter()]
        [string]$Author = $env:USERNAME,

        [Parameter()]
        [string[]]$Tags = @(),

        [Parameter()]
        [hashtable]$Settings = @{},

        [Parameter()]
        [switch]$Overwrite
    )

    Initialize-UserProfileManager

    $profilePath = Join-Path $script:UserProfilesDir "$Name.json"

    if ((Test-Path $profilePath) -and -not $Overwrite) {
        throw (Get-LocalizedString -Key 'userProfiles.error.alreadyExistsOverwrite' -Parameters @{ Name = $Name })
    }

    $existingProfile = $null
    if (Test-Path $profilePath) {
        try {
            $existingProfile = Get-Content $profilePath -Raw | ConvertFrom-Json
        } catch {
            Write-Verbose "Failed to read existing profile '$Name': $($_.Exception.Message)"
        }
    }

    $profile = [ordered]@{
        '$schema' = 'Win11Forge-UserProfile-v1.0'
        Name = $Name
        Description = $Description
        Author = $Author
        Tags = $Tags
        CreatedAt = if ($existingProfile -and $existingProfile.CreatedAt) {
            $existingProfile.CreatedAt
        } else {
            (Get-Date).ToString('o')
        }
        ModifiedAt = (Get-Date).ToString('o')
        Applications = $Applications
        Settings = $Settings
    }

    try {
        $profile | ConvertTo-Json -Depth 10 | Set-Content $profilePath -Encoding UTF8
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.saved' -Parameters @{ Name = $Name }) -Level 'Success' -Category 'Configuration'
        return $profilePath
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.error.saveFailed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error' -Category 'Configuration'
        throw
    }
}

function Get-UserProfiles {
    <#
    .SYNOPSIS
        Lists all user profiles.

    .DESCRIPTION
        Returns information about all saved user profiles.

    .PARAMETER Name
        Optional filter by profile name (supports wildcards).

    .PARAMETER Tag
        Optional filter by tag.

    .OUTPUTS
        Array of profile information objects.

    .EXAMPLE
        Get-UserProfiles
        Get-UserProfiles -Name 'Dev*'
        Get-UserProfiles -Tag 'gaming'
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param(
        [Parameter()]
        [string]$Name = '*',

        [Parameter()]
        [string]$Tag
    )

    Initialize-UserProfileManager

    $profiles = @()
    $pattern = "$Name.json"

    $profileFiles = Get-ChildItem -Path $script:UserProfilesDir -Filter $pattern -ErrorAction SilentlyContinue

    foreach ($file in $profileFiles) {
        try {
            $profileData = Get-Content $file.FullName -Raw | ConvertFrom-Json

            # Apply tag filter
            if ($Tag -and $profileData.Tags) {
                if ($profileData.Tags -notcontains $Tag) {
                    continue
                }
            }

            $profiles += [PSCustomObject]@{
                Name = $profileData.Name
                Description = $profileData.Description
                Author = $profileData.Author
                Tags = $profileData.Tags
                ApplicationCount = if ($profileData.Applications) { $profileData.Applications.Count } else { 0 }
                Applications = $profileData.Applications
                CreatedAt = $profileData.CreatedAt
                ModifiedAt = $profileData.ModifiedAt
                FilePath = $file.FullName
                FileSize = $file.Length
            }
        } catch {
            Write-Verbose "Failed to parse profile: $($file.Name)"
        }
    }

    return $profiles | Sort-Object Name
}

function Get-UserProfile {
    <#
    .SYNOPSIS
        Gets a specific user profile.

    .PARAMETER Name
        Name of the profile.

    .OUTPUTS
        Profile object or null if not found.

    .EXAMPLE
        $profile = Get-UserProfile -Name 'MyDevSetup'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $profilePath = Join-Path $script:UserProfilesDir "$Name.json"

    if (-not (Test-Path $profilePath)) {
        Write-Warning (Get-LocalizedString -Key 'userProfiles.not_found' -Parameters @{ Name = $Name })
        return $null
    }

    try {
        return Get-Content $profilePath -Raw | ConvertFrom-Json
    } catch {
        Write-Warning (Get-LocalizedString -Key 'userProfiles.error.loadFailed' -Parameters @{ Error = $_.Exception.Message })
        return $null
    }
}

function Remove-UserProfile {
    <#
    .SYNOPSIS
        Removes a user profile.

    .PARAMETER Name
        Name of the profile to remove.

    .PARAMETER Confirm
        Requires confirmation.

    .EXAMPLE
        Remove-UserProfile -Name 'OldProfile' -Confirm
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter()]
        [switch]$Confirm
    )

    $profilePath = Join-Path $script:UserProfilesDir "$Name.json"

    if (-not (Test-Path $profilePath)) {
        Write-Warning (Get-LocalizedString -Key 'userProfiles.not_found' -Parameters @{ Name = $Name })
        return
    }

    if (-not $Confirm) {
        Write-Warning (Get-LocalizedString -Key 'userProfiles.error.confirmRequired')
        return
    }

    try {
        Remove-Item $profilePath -Force
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.removed' -Parameters @{ Name = $Name }) -Level 'Info' -Category 'Configuration'
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.error.removeFailed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error' -Category 'Configuration'
    }
}

# === IMPORT/EXPORT ===

function Export-UserProfile {
    <#
    .SYNOPSIS
        Exports a user profile to a file.

    .DESCRIPTION
        Exports a profile for sharing or backup purposes.

    .PARAMETER Name
        Name of the profile to export.

    .PARAMETER OutputPath
        Destination file path.

    .PARAMETER IncludeMetadata
        Include timestamp and author metadata.

    .OUTPUTS
        String containing the export path.

    .EXAMPLE
        Export-UserProfile -Name 'MyDevSetup' -OutputPath 'C:\Exports\MyDevSetup.json'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter()]
        [switch]$IncludeMetadata = $true
    )

    $profile = Get-UserProfile -Name $Name
    if (-not $profile) {
        throw (Get-LocalizedString -Key 'userProfiles.not_found' -Parameters @{ Name = $Name })
    }

    $exportData = [ordered]@{
        '$schema' = 'Win11Forge-UserProfile-v1.0'
        Name = $profile.Name
        Description = $profile.Description
        Applications = $profile.Applications
        Settings = $profile.Settings
    }

    if ($IncludeMetadata) {
        $exportData.Author = $profile.Author
        $exportData.Tags = $profile.Tags
        $exportData.ExportedAt = (Get-Date).ToString('o')
        $exportData.ExportedFrom = $env:COMPUTERNAME
    }

    $directory = Split-Path $OutputPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    try {
        $exportData | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.exported' -Parameters @{ Path = $OutputPath }) -Level 'Success' -Category 'Configuration'
        return $OutputPath
    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.error.exportFailed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error' -Category 'Configuration'
        throw
    }
}

function Import-UserProfile {
    <#
    .SYNOPSIS
        Imports a user profile from a file.

    .DESCRIPTION
        Imports an external profile file into the user profiles directory.

    .PARAMETER Path
        Path to the profile file to import.

    .PARAMETER NewName
        Optional new name for the imported profile.

    .PARAMETER Overwrite
        Overwrite existing profile with same name.

    .OUTPUTS
        String containing the imported profile path.

    .EXAMPLE
        Import-UserProfile -Path 'C:\Downloads\SharedProfile.json'
        Import-UserProfile -Path 'C:\Downloads\SharedProfile.json' -NewName 'MyNewProfile'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ })]
        [string]$Path,

        [Parameter()]
        [ValidatePattern('^[a-zA-Z0-9_-]+$')]
        [string]$NewName,

        [Parameter()]
        [switch]$Overwrite
    )

    Initialize-UserProfileManager

    try {
        $importData = Get-Content $Path -Raw | ConvertFrom-Json

        # Validate required fields
        if (-not $importData.Name -or -not $importData.Applications) {
            throw (Get-LocalizedString -Key 'userProfiles.error.invalidFormat')
        }

        $profileName = if ($NewName) { $NewName } else { $importData.Name }
        $destPath = Join-Path $script:UserProfilesDir "$profileName.json"

        if ((Test-Path $destPath) -and -not $Overwrite) {
            throw (Get-LocalizedString -Key 'userProfiles.error.alreadyExistsOverwrite' -Parameters @{ Name = $profileName })
        }

        # Create new profile with import data
        $profile = [ordered]@{
            '$schema' = 'Win11Forge-UserProfile-v1.0'
            Name = $profileName
            Description = if ($importData.Description) { $importData.Description } else { '' }
            Author = if ($importData.Author) { $importData.Author } else { 'Imported' }
            Tags = if ($importData.Tags) { @($importData.Tags) } else { @('imported') }
            CreatedAt = (Get-Date).ToString('o')
            ModifiedAt = (Get-Date).ToString('o')
            Applications = @($importData.Applications)
            Settings = if ($importData.Settings) { $importData.Settings } else { @{} }
            ImportedFrom = $Path
            ImportedAt = (Get-Date).ToString('o')
        }

        $profile | ConvertTo-Json -Depth 10 | Set-Content $destPath -Encoding UTF8

        Write-Status -Message (Get-LocalizedString -Key 'userProfiles.imported' -Parameters @{ Name = $profileName }) -Level 'Success' -Category 'Configuration'
        return $destPath
    } catch {
        Write-Status -Message "Failed to import profile: $($_.Exception.Message)" -Level 'Error' -Category 'Configuration'
        throw
    }
}

# === PROFILE OPERATIONS ===

function Copy-UserProfile {
    <#
    .SYNOPSIS
        Creates a copy of an existing profile.

    .PARAMETER SourceName
        Name of the profile to copy.

    .PARAMETER DestinationName
        Name for the new profile.

    .OUTPUTS
        String containing the new profile path.

    .EXAMPLE
        Copy-UserProfile -SourceName 'Base' -DestinationName 'BaseCopy'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$SourceName,

        [Parameter(Mandatory)]
        [ValidatePattern('^[a-zA-Z0-9_-]+$')]
        [string]$DestinationName
    )

    $sourceProfile = Get-UserProfile -Name $SourceName
    if (-not $sourceProfile) {
        throw "Source profile not found: $SourceName"
    }

    return Save-UserProfile -Name $DestinationName `
        -Applications $sourceProfile.Applications `
        -Description "Copy of $SourceName" `
        -Tags @($sourceProfile.Tags) `
        -Settings $sourceProfile.Settings
}

function Merge-UserProfiles {
    <#
    .SYNOPSIS
        Merges multiple profiles into one.

    .PARAMETER Names
        Array of profile names to merge.

    .PARAMETER OutputName
        Name for the merged profile.

    .PARAMETER RemoveDuplicates
        Remove duplicate applications.

    .OUTPUTS
        String containing the merged profile path.

    .EXAMPLE
        Merge-UserProfiles -Names @('DevTools', 'Gaming') -OutputName 'Combined'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string[]]$Names,

        [Parameter(Mandatory)]
        [ValidatePattern('^[a-zA-Z0-9_-]+$')]
        [string]$OutputName,

        [Parameter()]
        [switch]$RemoveDuplicates = $true
    )

    $allApplications = @()
    $allTags = @()

    foreach ($name in $Names) {
        $profile = Get-UserProfile -Name $name
        if ($profile) {
            $allApplications += $profile.Applications
            if ($profile.Tags) {
                $allTags += $profile.Tags
            }
        } else {
            Write-Warning "Profile not found: $name"
        }
    }

    if ($RemoveDuplicates) {
        $allApplications = $allApplications | Select-Object -Unique
        $allTags = $allTags | Select-Object -Unique
    }

    return Save-UserProfile -Name $OutputName `
        -Applications $allApplications `
        -Description "Merged from: $($Names -join ', ')" `
        -Tags (@('merged') + $allTags) `
        -Overwrite
}

function Get-UserProfileStatistics {
    <#
    .SYNOPSIS
        Returns statistics about user profiles.

    .OUTPUTS
        PSCustomObject with profile statistics.

    .EXAMPLE
        Get-UserProfileStatistics
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $profiles = Get-UserProfiles

    $totalApps = 0
    $allTags = @()
    $appCounts = @{}

    foreach ($profile in $profiles) {
        $totalApps += $profile.ApplicationCount
        $allTags += $profile.Tags

        foreach ($app in $profile.Applications) {
            if (-not $appCounts.ContainsKey($app)) {
                $appCounts[$app] = 0
            }
            $appCounts[$app]++
        }
    }

    $topApps = $appCounts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10

    return [PSCustomObject]@{
        TotalProfiles = $profiles.Count
        TotalApplicationReferences = $totalApps
        UniqueApplications = $appCounts.Count
        UniqueTags = ($allTags | Select-Object -Unique).Count
        ProfilesDirectory = $script:UserProfilesDir
        DirectorySize = (Get-ChildItem $script:UserProfilesDir -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        TopApplications = $topApps | ForEach-Object { @{ Name = $_.Key; Count = $_.Value } }
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Initialize-UserProfileManager',
    'Save-UserProfile',
    'Get-UserProfiles',
    'Get-UserProfile',
    'Remove-UserProfile',
    'Export-UserProfile',
    'Import-UserProfile',
    'Copy-UserProfile',
    'Merge-UserProfiles',
    'Get-UserProfileStatistics'
)
