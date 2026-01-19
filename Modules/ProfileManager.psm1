<#
.SYNOPSIS
    Win11Forge - Profile Manager Version: 3.5.0

.DESCRIPTION
    Manages deployment profiles with JSON inheritance:
    - Profile loading and validation
    - Recursive inheritance resolution
    - Application merging and deduplication
    - System configuration merging
    - Integration with centralized Application Database

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
    Supports multi-level inheritance (e.g., Personnel → Gaming → Office → Base)
    NEW: Supports Application Database references (AppId strings or objects)
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
$script:AppDatabaseModulePath = Join-Path $script:ModuleRoot 'ApplicationDatabase.psm1'

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

# Import Application Database module
if (Test-Path -Path $script:AppDatabaseModulePath) {
    Import-Module -Name $script:AppDatabaseModulePath -Force
    $script:UseAppDatabase = $true
    Write-Verbose "Application Database module loaded"
} else {
    $script:UseAppDatabase = $false
    Write-Verbose "Application Database module not found - using legacy mode"
}

# === PERFORMANCE OPTIMIZATION: PROFILE CACHING ===

# Profile cache (keyed by absolute file path)
$script:ProfileCache = @{}
$script:ProfileCacheLastModified = @{}

function script:Get-CachedProfile {
    <#
    .SYNOPSIS
        Gets a profile from cache if available and not modified.
    .DESCRIPTION
        Returns cached profile if file hasn't been modified since last load.
        Returns $null if cache miss or file modified.
    #>
    [CmdletBinding()]
    param([string]$Path)

    $absPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $script:ProfileCache.ContainsKey($absPath)) {
        return $null
    }

    # Check if file was modified
    try {
        $currentModified = (Get-Item $absPath -ErrorAction Stop).LastWriteTime
        $cachedModified = $script:ProfileCacheLastModified[$absPath]

        if ($currentModified -gt $cachedModified) {
            # File modified - invalidate cache
            $script:ProfileCache.Remove($absPath)
            $script:ProfileCacheLastModified.Remove($absPath)
            return $null
        }

        return $script:ProfileCache[$absPath]
    } catch {
        return $null
    }
}

function script:Set-CachedProfile {
    <#
    .SYNOPSIS
        Stores a profile in cache.
    #>
    [CmdletBinding()]
    param(
        [string]$Path,
        [DeploymentProfile]$Profile
    )

    $absPath = [System.IO.Path]::GetFullPath($Path)

    try {
        $script:ProfileCache[$absPath] = $Profile
        $script:ProfileCacheLastModified[$absPath] = (Get-Item $absPath -ErrorAction Stop).LastWriteTime
    } catch {
        # Silently fail if file not accessible
    }
}

function Clear-ProfileCache {
    <#
    .SYNOPSIS
        Clears the profile cache.
    #>
    [CmdletBinding()]
    param()

    $script:ProfileCache = @{}
    $script:ProfileCacheLastModified = @{}
    Write-Verbose "Profile cache cleared"
}

# === VERSION CACHING ===

$script:CachedFrameworkVersion = $null

function script:Get-CachedFrameworkVersion {
    <#
    .SYNOPSIS
        Gets cached framework version from version.json.
    #>
    if ($script:CachedFrameworkVersion) {
        return $script:CachedFrameworkVersion
    }

    $versionPath = Join-Path $script:RepositoryRoot 'Config\version.json'
    if (Test-Path $versionPath) {
        try {
            $versionData = Get-Content -Path $versionPath -Raw | ConvertFrom-Json
            $script:CachedFrameworkVersion = $versionData.Version
            return $script:CachedFrameworkVersion
        } catch { }
    }

    return 'Unknown'
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
        Loads a profile from JSON file with caching.

    .PARAMETER Path
        Path to the profile JSON file

    .PARAMETER NoCache
        If specified, bypasses cache and forces reload from disk.

    .OUTPUTS
        [DeploymentProfile] Loaded profile object
    #>
    [CmdletBinding()]
    [OutputType([DeploymentProfile])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$NoCache
    )

    if (-not (Test-Path -Path $Path)) {
        throw "Profile file not found: $Path"
    }

    # Check cache first (unless NoCache specified)
    if (-not $NoCache) {
        $cached = Get-CachedProfile -Path $Path
        if ($cached) {
            Write-Verbose "Profile loaded from cache: $Path"
            return $cached
        }
    }

    try {
        Write-Status -Message "Loading profile: $Path" -Level 'Verbose'

        $jsonContent = Get-Content -Path $Path -Raw -ErrorAction Stop
        $jsonObject = $jsonContent | ConvertFrom-Json -ErrorAction Stop

        $deploymentProfile = [DeploymentProfile]::new()
        $deploymentProfile.Name = $jsonObject.Name
        $deploymentProfile.Description = $jsonObject.Description

        # Use profile version if specified, otherwise use cached framework version
        if ($jsonObject.Version) {
            $deploymentProfile.Version = $jsonObject.Version
        } else {
            $deploymentProfile.Version = Get-CachedFrameworkVersion
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

        # Store in cache
        Set-CachedProfile -Path $Path -Profile $deploymentProfile

        return $deploymentProfile

    } catch {
        Write-Status -Message "Failed to load profile: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

# === INHERITANCE RESOLUTION ===

function Test-ProfileCycles {
    <#
    .SYNOPSIS
        Validates a profile for circular inheritance dependencies.

    .DESCRIPTION
        Performs a depth-first traversal of the inheritance graph to detect cycles.
        Returns detailed information about any cycles found.

    .PARAMETER ProfileName
        Name or path of the profile to validate

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files

    .OUTPUTS
        [hashtable] Validation result with:
          - HasCycles: [bool] Whether cycles were detected
          - Cycles: [string[]] Array of detected cycle paths (e.g., "A -> B -> A")
          - InheritanceGraph: [hashtable] Full inheritance relationships
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfileName,

        [Parameter()]
        [string]$ProfilesDirectory
    )

    $result = @{
        HasCycles = $false
        Cycles = @()
        InheritanceGraph = @{}
    }

    # Build inheritance graph
    $visited = [System.Collections.Generic.HashSet[string]]::new()
    $recursionStack = [System.Collections.Generic.Stack[string]]::new()

    function Build-InheritanceGraph {
        param([string]$Name, [string]$Dir)

        if ($result.InheritanceGraph.ContainsKey($Name)) {
            return
        }

        try {
            $path = Get-ProfilePath -ProfileName $Name -ProfilesDirectory $Dir -ErrorAction Stop
            $profile = Import-ProfileJson -Path $path -NoCache -ErrorAction Stop

            $result.InheritanceGraph[$Name] = @{
                Inherits = if ($profile.Inherits) { @($profile.Inherits) } else { @() }
                Path = $path
            }

            foreach ($parent in $result.InheritanceGraph[$Name].Inherits) {
                Build-InheritanceGraph -Name $parent -Dir $Dir
            }
        } catch {
            $result.InheritanceGraph[$Name] = @{
                Inherits = @()
                Path = $null
                Error = $_.Exception.Message
            }
        }
    }

    function Detect-Cycles {
        param([string]$Name, [System.Collections.Generic.List[string]]$Path)

        if ($recursionStack.Contains($Name)) {
            # Cycle detected - build cycle path
            $cycleStart = $Path.IndexOf($Name)
            $cyclePath = $Path.GetRange($cycleStart, $Path.Count - $cycleStart)
            $cyclePath.Add($Name)
            $result.HasCycles = $true
            $result.Cycles += ($cyclePath -join ' -> ')
            return
        }

        if ($visited.Contains($Name)) {
            return
        }

        [void]$visited.Add($Name)
        [void]$recursionStack.Push($Name)
        [void]$Path.Add($Name)

        if ($result.InheritanceGraph.ContainsKey($Name)) {
            foreach ($parent in $result.InheritanceGraph[$Name].Inherits) {
                Detect-Cycles -Name $parent -Path $Path
            }
        }

        [void]$recursionStack.Pop()
        [void]$Path.RemoveAt($Path.Count - 1)
    }

    # Build graph starting from target profile
    Build-InheritanceGraph -Name $ProfileName -Dir $ProfilesDirectory

    # Detect cycles for all profiles in graph
    foreach ($name in $result.InheritanceGraph.Keys) {
        $visited.Clear()
        $recursionStack.Clear()
        $path = [System.Collections.Generic.List[string]]::new()
        Detect-Cycles -Name $name -Path $path
    }

    # Remove duplicate cycles
    $result.Cycles = $result.Cycles | Select-Object -Unique

    return $result
}

function Resolve-ProfileInheritance {
    <#
    .SYNOPSIS
        Resolves profile inheritance recursively with cycle detection.

    .PARAMETER Profile
        Base profile to resolve

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files

    .PARAMETER AncestorPath
        Internal: tracks the full path of ancestors to detect and report cycles

    .OUTPUTS
        [DeploymentProfile[]] Array of profiles in inheritance order (base first)

    .NOTES
        Throws an exception if circular inheritance is detected.
    #>
    [CmdletBinding()]
    [OutputType([DeploymentProfile[]])]
    param(
        [Parameter(Mandatory)]
        [DeploymentProfile]$InputProfile,

        [Parameter()]
        [string]$ProfilesDirectory,

        [Parameter()]
        [System.Collections.Generic.HashSet[string]]$Visited,

        [Parameter()]
        [System.Collections.Generic.List[string]]$AncestorPath
    )

    if (-not $Visited) {
        $Visited = [System.Collections.Generic.HashSet[string]]::new()
    }

    if (-not $AncestorPath) {
        $AncestorPath = [System.Collections.Generic.List[string]]::new()
    }

    # Check for circular reference with full path reporting
    if ($AncestorPath.Contains($InputProfile.Name)) {
        $cycleStart = $AncestorPath.IndexOf($InputProfile.Name)
        $cyclePath = @($AncestorPath.GetRange($cycleStart, $AncestorPath.Count - $cycleStart))
        $cyclePath += $InputProfile.Name
        $cycleString = $cyclePath -join ' -> '

        $errorMsg = "Circular inheritance detected: $cycleString"
        Write-Status -Message $errorMsg -Level 'Error'
        throw [System.InvalidOperationException]::new($errorMsg)
    }

    # Add to ancestor path for this branch
    [void]$AncestorPath.Add($InputProfile.Name)

    # Check if already fully processed (for diamond inheritance)
    if ($Visited.Contains($InputProfile.Name)) {
        [void]$AncestorPath.RemoveAt($AncestorPath.Count - 1)
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
                $parentChain = Resolve-ProfileInheritance -InputProfile $parentProfile -ProfilesDirectory $ProfilesDirectory -Visited $Visited -AncestorPath $AncestorPath

                $result += $parentChain

            } catch {
                # Re-throw cycle detection errors
                if ($_.Exception -is [System.InvalidOperationException] -and $_.Exception.Message -like "*Circular inheritance*") {
                    throw
                }
                Write-Status -Message "Warning: Could not load parent profile '$parentName': $($_.Exception.Message)" -Level 'Warning'
            }
        }
    }

    # Remove from ancestor path after processing children
    [void]$AncestorPath.RemoveAt($AncestorPath.Count - 1)

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
        $mergedApplications = @(Merge-ProfileApplications -Profiles $profileChain)

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
        Write-Status -Message "  Configuration sections: $(if ($mergedConfig) { $mergedConfig.Keys.Count } else { 0 })" -Level 'Info'

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

            # PS5.1 compatible: check if property exists before accessing
            $hasInstallMethod = $null -ne $app.PSObject.Properties['InstallMethod'] -and $app.InstallMethod
            if (-not $hasSources -and -not $hasInstallMethod) {
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
    'Test-ProfileCycles',
    'Get-ApplicationsByCategory',
    'Get-RequiredApplications',
    'ConvertTo-Hashtable',
    'Clear-ProfileCache'
)

