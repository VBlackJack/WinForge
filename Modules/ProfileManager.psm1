<#
.SYNOPSIS
    Win11Forge - Profile Manager Version: 3.0.0

.DESCRIPTION
    Manages deployment profiles with JSON inheritance:
    - Profile loading and validation
    - Recursive inheritance resolution
    - Application merging and deduplication
    - System configuration merging
    - Integration with centralized Application Database

.NOTES
    Author: Julien Bombled
    Version: 3.0.0
    Supports multi-level inheritance (e.g., Personnel → Gaming → Office → Base)
    NEW: Supports Application Database references (AppId strings or objects)
#>

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:AppDatabaseModulePath = Join-Path $script:ModuleRoot 'ApplicationDatabase.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Application Database module
if (Test-Path -Path $script:AppDatabaseModulePath) {
    Import-Module -Name $script:AppDatabaseModulePath -Force
    $script:UseAppDatabase = $true
    Write-Verbose "Application Database module loaded"
} else {
    $script:UseAppDatabase = $false
    Write-Verbose "Application Database module not found - using legacy mode"
}

# === PROFILE CLASSES ===

class DeploymentProfile {
    [string]$Name
    [string]$Description
    [string]$Version
    [string[]]$Inherits
    [PSCustomObject[]]$Applications
    [hashtable]$SystemConfig
    [string]$ProfilePath

    DeploymentProfile() {
        $this.Applications = @()
        $this.Inherits = @()
        $this.SystemConfig = @{}
    }
}

# === PROFILE LOADING ===

function Get-ProfilePath {
    <#
    .SYNOPSIS
        Resolves profile path from name or file path.

    .PARAMETER ProfileName
        Profile name or full path to JSON file

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files

    .OUTPUTS
        [string] Resolved profile path
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfileName,

        [Parameter()]
        [string]$ProfilesDirectory
    )

    # If ProfileName is already a full path
    if (Test-Path -Path $ProfileName -PathType Leaf) {
        return $ProfileName
    }

    # Try to find in Profiles directory
    if (-not $ProfilesDirectory) {
        $ProfilesDirectory = Join-Path -Path $script:RepositoryRoot -ChildPath 'Profiles'
    }

    # Try with .json extension
    $profilePath = Join-Path -Path $ProfilesDirectory -ChildPath "$ProfileName.json"
    if (Test-Path -Path $profilePath) {
        return $profilePath
    }

    # Try without extension (maybe user provided it)
    $profilePath = Join-Path -Path $ProfilesDirectory -ChildPath $ProfileName
    if (Test-Path -Path $profilePath) {
        return $profilePath
    }

    throw "Profile not found: $ProfileName (searched in $ProfilesDirectory)"
}

function Import-ProfileJson {
    <#
    .SYNOPSIS
        Loads a profile from JSON file.

    .PARAMETER Path
        Path to the profile JSON file

    .OUTPUTS
        [DeploymentProfile] Loaded profile object
    #>
    [CmdletBinding()]
    [OutputType([DeploymentProfile])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -Path $Path)) {
        throw "Profile file not found: $Path"
    }

    try {
        Write-Status -Message "Loading profile: $Path" -Level 'Verbose'

        $jsonContent = Get-Content -Path $Path -Raw -ErrorAction Stop
        $jsonObject = $jsonContent | ConvertFrom-Json -ErrorAction Stop

        $deploymentProfile = [DeploymentProfile]::new()
        $deploymentProfile.Name = $jsonObject.Name
        $deploymentProfile.Description = $jsonObject.Description

        # Use profile version if specified, otherwise use framework version
        if ($jsonObject.Version) {
            $deploymentProfile.Version = $jsonObject.Version
        } else {
            # Load framework version dynamically
            $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
            $versionPath = Join-Path $repoRoot 'Config\version.json'
            if (Test-Path $versionPath) {
                $versionData = Get-Content -Path $versionPath -Raw | ConvertFrom-Json
                $deploymentProfile.Version = $versionData.Version
            } else {
                $deploymentProfile.Version = 'Unknown'
            }
        }

        $deploymentProfile.ProfilePath = $Path

        # Handle inheritance
        if ($jsonObject.Inherits) {
            if ($jsonObject.Inherits -is [array]) {
                $deploymentProfile.Inherits = $jsonObject.Inherits
            } else {
                $deploymentProfile.Inherits = @($jsonObject.Inherits)
            }
        }

        # Load applications
        if ($jsonObject.Applications) {
            $deploymentProfile.Applications = $jsonObject.Applications
        }

        # Load system configuration
        if ($jsonObject.SystemConfig) {
            $deploymentProfile.SystemConfig = @{}
            $jsonObject.SystemConfig.PSObject.Properties | ForEach-Object {
                $deploymentProfile.SystemConfig[$_.Name] = $_.Value
            }
        }

        Write-Status -Message "Profile loaded: $($deploymentProfile.Name) v$($deploymentProfile.Version)" -Level 'Success'

        return $deploymentProfile

    } catch {
        Write-Status -Message "Failed to load profile: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

# === INHERITANCE RESOLUTION ===

function Resolve-ProfileInheritance {
    <#
    .SYNOPSIS
        Resolves profile inheritance recursively.

    .PARAMETER Profile
        Base profile to resolve

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files

    .PARAMETER Visited
        Internal: tracks visited profiles to prevent circular references

    .OUTPUTS
        [DeploymentProfile[]] Array of profiles in inheritance order (base first)
    #>
    [CmdletBinding()]
    [OutputType([DeploymentProfile[]])]
    param(
        [Parameter(Mandatory)]
        [DeploymentProfile]$InputProfile,

        [Parameter()]
        [string]$ProfilesDirectory,

        [Parameter()]
        [System.Collections.Generic.HashSet[string]]$Visited
    )

    if (-not $Visited) {
        $Visited = [System.Collections.Generic.HashSet[string]]::new()
    }

    # Check for circular reference
    if ($Visited.Contains($InputProfile.Name)) {
        Write-Status -Message "Circular inheritance detected: $($InputProfile.Name)" -Level 'Warning'
        return @()
    }

    [void]$Visited.Add($InputProfile.Name)

    $result = @()

    # Recursively load parent profiles
    if ($InputProfile.Inherits -and $InputProfile.Inherits.Count -gt 0) {
        foreach ($parentName in $InputProfile.Inherits) {
            try {
                $parentPath = Get-ProfilePath -ProfileName $parentName -ProfilesDirectory $ProfilesDirectory
                $parentProfile = Import-ProfileJson -Path $parentPath

                # Recursively resolve parent's inheritance
                $parentChain = Resolve-ProfileInheritance -InputProfile $parentProfile -ProfilesDirectory $ProfilesDirectory -Visited $Visited

                $result += $parentChain

            } catch {
                Write-Status -Message "Warning: Could not load parent profile '$parentName': $($_.Exception.Message)" -Level 'Warning'
            }
        }
    }

    # Add current profile
    $result += $InputProfile

    # Always return as array (prevents PowerShell from unwrapping single item)
    return , $result
}

function Resolve-ApplicationReference {
    <#
    .SYNOPSIS
        Resolves an application reference to a full application object.

    .DESCRIPTION
        Supports three formats:
        1. Full app object (legacy): { "Name": "Chrome", "Sources": {...} }
        2. AppId string (new): "GoogleChrome"
        3. AppId object (new with overrides): { "AppId": "GoogleChrome", "Priority": 10, "Overrides": {...} }

    .PARAMETER AppReference
        Application reference (string, object with Name, or object with AppId)

    .OUTPUTS
        [PSCustomObject] Full application object
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        $AppReference
    )

    # Case 1: String - AppId reference
    if ($AppReference -is [string]) {
        if ($script:UseAppDatabase) {
            $dbApp = Get-ApplicationById -AppId $AppReference
            if ($null -eq $dbApp) {
                Write-Status -Message "Warning: Application '$AppReference' not found in database" -Level 'Warning'
                return $null
            }
            return ConvertTo-ProfileApplication -App $dbApp
        } else {
            Write-Status -Message "Warning: AppId reference '$AppReference' found but database not available" -Level 'Warning'
            return $null
        }
    }

    # Case 2: Object with AppId property (new format with overrides)
    if ($AppReference.PSObject.Properties['AppId']) {
        if ($script:UseAppDatabase) {
            $dbApp = Get-ApplicationById -AppId $AppReference.AppId
            if ($null -eq $dbApp) {
                Write-Status -Message "Warning: Application '$($AppReference.AppId)' not found in database" -Level 'Warning'
                return $null
            }

            # Convert from database
            # Use $null -ne to handle explicit Priority = 0 (highest priority)
            $priority = if ($null -ne $AppReference.Priority) { $AppReference.Priority } else { $null }
            $required = if ($null -ne $AppReference.Required) { $AppReference.Required } else { $null }

            $app = ConvertTo-ProfileApplication -App $dbApp -Priority $priority -Required $required

            # Apply overrides if specified
            if ($AppReference.Overrides) {
                foreach ($prop in $AppReference.Overrides.PSObject.Properties) {
                    if ($app.PSObject.Properties[$prop.Name]) {
                        $app.($prop.Name) = $prop.Value
                    } else {
                        $app | Add-Member -NotePropertyName $prop.Name -NotePropertyValue $prop.Value -Force
                    }
                }
            }

            return $app
        } else {
            Write-Status -Message "Warning: AppId reference '$($AppReference.AppId)' found but database not available" -Level 'Warning'
            return $null
        }
    }

    # Case 3: Full application object (legacy format)
    if ($AppReference.PSObject.Properties['Name']) {
        return $AppReference
    }

    Write-Status -Message "Warning: Unknown application reference format" -Level 'Warning'
    return $null
}

function Merge-ProfileApplications {
    <#
    .SYNOPSIS
        Merges applications from multiple profiles with deduplication.

    .PARAMETER Profiles
        Array of profiles in inheritance order

    .OUTPUTS
        [PSCustomObject[]] Merged and deduplicated applications
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory)]
        [DeploymentProfile[]]$Profiles
    )

    $mergedApps = [System.Collections.Generic.Dictionary[string, PSCustomObject]]::new()

    foreach ($profileItem in $Profiles) {
        Write-Status -Message "Merging applications from: $($profileItem.Name)" -Level 'Verbose'

        foreach ($appRef in $profileItem.Applications) {
            # Resolve application reference (supports legacy and new formats)
            $app = Resolve-ApplicationReference -AppReference $appRef

            if ($null -eq $app) {
                continue
            }

            $appName = $app.Name

            # If app already exists, the later profile (child) overrides
            if ($mergedApps.ContainsKey($appName)) {
                Write-Status -Message "  Override: $appName (from $($profileItem.Name))" -Level 'Verbose'
            } else {
                Write-Status -Message "  Add: $appName" -Level 'Verbose'
            }

            $mergedApps[$appName] = $app
        }
    }

    return $mergedApps.Values
}


function ConvertTo-Hashtable {
    <#
    .SYNOPSIS
        Recursively converts PSCustomObject to Hashtable.

    .DESCRIPTION
        Converts nested PSCustomObject structures (from JSON) into proper Hashtables
        that can be used with functions expecting [hashtable] parameters.

    .OUTPUTS
        [hashtable] Converted hashtable
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        $InputObject
    )

    process {
        if ($null -eq $InputObject) {
            return $null
        }

        # Already a hashtable - recursively convert nested values
        if ($InputObject -is [hashtable]) {
            $output = @{}
            foreach ($key in $InputObject.Keys) {
                $output[$key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
            }
            return $output
        }

        # PSCustomObject - convert to hashtable
        if ($InputObject -is [PSCustomObject]) {
            $output = @{}
            foreach ($property in $InputObject.PSObject.Properties) {
                $output[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
            }
            return $output
        }

        # Array - recursively convert each element
        if ($InputObject -is [array]) {
            return @($InputObject | ForEach-Object { ConvertTo-Hashtable -InputObject $_ })
        }

        # Primitive type - return as-is
        return $InputObject
    }
}

function Merge-ProfileSystemConfig {
    <#
    .SYNOPSIS
        Merges system configuration from multiple profiles.

    .PARAMETER Profiles
        Array of profiles in inheritance order

    .OUTPUTS
        [hashtable] Merged system configuration
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [DeploymentProfile[]]$Profiles
    )

    $mergedConfig = @{}

    foreach ($profileItem in $Profiles) {
        if (-not $profileItem.SystemConfig -or $profileItem.SystemConfig.Count -eq 0) {
            continue
        }

        Write-Status -Message "Merging system config from: $($profileItem.Name)" -Level 'Verbose'

        foreach ($key in $profileItem.SystemConfig.Keys) {
            $value = $profileItem.SystemConfig[$key]

            # Deep merge for nested hashtables
            if ($mergedConfig.ContainsKey($key)) {
                if ($value -is [hashtable] -and $mergedConfig[$key] -is [hashtable]) {
                    # Merge nested properties
                    foreach ($nestedKey in $value.Keys) {
                        $mergedConfig[$key][$nestedKey] = $value[$nestedKey]
                    }
                } else {
                    # Override with new value
                    $mergedConfig[$key] = $value
                }
            } else {                # CRITICAL FIX: Recursively convert PSCustomObject to Hashtable
                $mergedConfig[$key] = ConvertTo-Hashtable -InputObject $value
            }
        }
    }

    return $mergedConfig
}

# === MAIN PROFILE LOADING ===

function Get-DeploymentProfile {
    <#
    .SYNOPSIS
        Loads a deployment profile with full inheritance resolution.

    .PARAMETER ProfileName
        Profile name or path to JSON file

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files

    .OUTPUTS
        [hashtable] Complete deployment profile with merged applications and config
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfileName,

        [Parameter()]
        [string]$ProfilesDirectory
    )

    Write-Status -Message "Loading deployment profile: $ProfileName" -Level 'Info'

    try {
        # Get profile path
        $profilePath = Get-ProfilePath -ProfileName $ProfileName -ProfilesDirectory $ProfilesDirectory

        # Load base profile
        $baseProfile = Import-ProfileJson -Path $profilePath

        # Resolve inheritance
        Write-Status -Message "Resolving inheritance chain..." -Level 'Info'
        $profileChain = Resolve-ProfileInheritance -InputProfile $baseProfile -ProfilesDirectory $ProfilesDirectory

        Write-Status -Message "Inheritance chain: $($profileChain.Name -join ' -> ')" -Level 'Info'

        # Merge applications
        Write-Status -Message "Merging applications..." -Level 'Info'
        $mergedApplications = Merge-ProfileApplications -Profiles $profileChain

        # Merge system configuration
        Write-Status -Message "Merging system configuration..." -Level 'Info'
        $mergedConfig = Merge-ProfileSystemConfig -Profiles $profileChain

        # Create final profile
        $finalProfile = @{
            Name = $baseProfile.Name
            Description = $baseProfile.Description
            Version = $baseProfile.Version
            InheritanceChain = $profileChain.Name
            Applications = $mergedApplications
            SystemConfig = $mergedConfig
            ProfilePath = $profilePath
        }

        Write-Status -Message "Profile loaded successfully" -Level 'Success'
        Write-Status -Message "  Total applications: $($mergedApplications.Count)" -Level 'Info'
        Write-Status -Message "  Configuration sections: $($mergedConfig.Keys.Count)" -Level 'Info'

        return $finalProfile

    } catch {
        Write-Status -Message "Failed to load deployment profile: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

# === PROFILE VALIDATION ===

function Test-ProfileValid {
    <#
    .SYNOPSIS
        Validates a profile JSON structure.

    .PARAMETER ProfilePath
        Path to profile JSON file

    .OUTPUTS
        [hashtable] Validation result with errors
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfilePath
    )

    $result = @{
        Valid = $true
        Errors = @()
        Warnings = @()
    }

    if (-not (Test-Path -Path $ProfilePath)) {
        $result.Valid = $false
        $result.Errors += "Profile file not found: $ProfilePath"
        return $result
    }

    try {
        $deploymentProfile = Import-ProfileJson -Path $ProfilePath

        # Check required fields
        if ([string]::IsNullOrWhiteSpace($deploymentProfile.Name)) {
            $result.Valid = $false
            $result.Errors += "Profile name is missing"
        }

        if ([string]::IsNullOrWhiteSpace($deploymentProfile.Version)) {
            $result.Warnings += "Profile version is missing"
        }

        # Validate applications
        $appNames = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($app in $deploymentProfile.Applications) {
            # v2.3 Database Mode: Applications are simple strings (AppId)
            if ($app -is [string]) {
                if ([string]::IsNullOrWhiteSpace($app)) {
                    $result.Valid = $false
                    $result.Errors += "Empty AppId found in Applications array"
                    continue
                }

                # Check for duplicate AppIds
                if (-not $appNames.Add($app)) {
                    $result.Warnings += "Duplicate AppId: $app"
                }

                # Skip legacy validation checks for Database Mode
                continue
            }

            # v2.3 Database Mode: Applications with AppId override (object format)
            if ($app.PSObject.Properties['AppId']) {
                if ([string]::IsNullOrWhiteSpace($app.AppId)) {
                    $result.Valid = $false
                    $result.Errors += "Empty AppId found in override object"
                    continue
                }

                # Check for duplicate AppIds
                if (-not $appNames.Add($app.AppId)) {
                    $result.Warnings += "Duplicate AppId: $($app.AppId)"
                }

                # Skip legacy validation checks for Database Mode
                continue
            }

            # Legacy format: Applications are objects with Name, Sources, Priority
            if ([string]::IsNullOrWhiteSpace($app.Name)) {
                $result.Valid = $false
                $result.Errors += "Application with missing name found"
                continue
            }

            # Check for duplicates
            if (-not $appNames.Add($app.Name)) {
                $result.Warnings += "Duplicate application: $($app.Name)"
            }

            # Check for sources
            $hasSources = $false
            if ($app.Sources) {
                if ($app.Sources.Winget -or $app.Sources.Chocolatey -or $app.Sources.Store -or $app.Sources.DirectUrl) {
                    $hasSources = $true
                }
            }

            if (-not $hasSources -and -not $app.InstallMethod) {
                $result.Warnings += "Application '$($app.Name)' has no installation sources"
            }

            # Check priority
            if ($null -eq $app.Priority) {
                $result.Warnings += "Application '$($app.Name)' has no priority"
            }
        }

        return $result

    } catch {
        $result.Valid = $false
        $result.Errors += "JSON parsing error: $($_.Exception.Message)"
        return $result
    }
}

# === PROFILE UTILITIES ===

function Get-ApplicationsByCategory {
    <#
    .SYNOPSIS
        Groups applications by category.

    .PARAMETER Applications
        Array of applications

    .OUTPUTS
        [hashtable] Applications grouped by category
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications
    )

    $grouped = @{}

    foreach ($app in $Applications) {
        $category = if ($app.Category) { $app.Category } else { 'Uncategorized' }

        if (-not $grouped.ContainsKey($category)) {
            $grouped[$category] = @()
        }

        $grouped[$category] += $app
    }

    return $grouped
}

function Get-RequiredApplications {
    <#
    .SYNOPSIS
        Filters only required applications.

    .PARAMETER Applications
        Array of applications

    .OUTPUTS
        [PSCustomObject[]] Required applications only
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications
    )

    return $Applications | Where-Object { $_.Required -eq $true }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Get-ProfilePath',
    'Import-ProfileJson',
    'Resolve-ProfileInheritance',
    'Resolve-ApplicationReference',
    'Merge-ProfileApplications',
    'Merge-ProfileSystemConfig',
    'Get-DeploymentProfile',
    'Test-ProfileValid',
    'Get-ApplicationsByCategory',
    'Get-RequiredApplications',
    'ConvertTo-Hashtable'
)

