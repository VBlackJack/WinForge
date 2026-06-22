<#
.SYNOPSIS
    WinForge - Profile Manager v3.7.2

.DESCRIPTION
    Manages deployment profiles with JSON inheritance:
    - Profile loading and validation
    - Recursive inheritance resolution
    - Application merging and deduplication
    - System configuration merging
    - Integration with centralized Application Database

.NOTES
    Author: Julien Bombled
    v3.7.2
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

# Import JSON Schema Validation module for profile validation
$script:JsonSchemaValidationModulePath = Join-Path $script:ModuleRoot 'JsonSchemaValidation.psm1'
$script:UseSchemaValidation = $false
if (Test-Path -Path $script:JsonSchemaValidationModulePath) {
    try {
        Import-Module -Name $script:JsonSchemaValidationModulePath -Force -ErrorAction Stop
        $script:UseSchemaValidation = $true
        Write-Verbose "JSON Schema Validation module loaded"
    } catch {
        Write-Verbose "Failed to load JSON Schema Validation module: $($_.Exception.Message)"
    }
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
    .DESCRIPTION
        Saves a parsed DeploymentProfile object into the in-memory profile cache, keyed by its
        absolute file path, along with the file's last-write timestamp for staleness detection.
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
        # Cache update is non-critical, but log for debugging
        Write-Verbose "Profile cache update failed for '$absPath': $($_.Exception.Message)"
    }
}

function Clear-ProfileCache {
    <#
    .SYNOPSIS
        Clears the profile cache.
    .DESCRIPTION
        Removes all entries from the in-memory profile cache and their associated timestamps,
        forcing subsequent profile loads to re-read from disk.
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
    .DESCRIPTION
        Returns the framework version string from Config/version.json, caching the result in a
        script-scoped variable to avoid repeated file reads on subsequent calls.
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
        } catch {
            Write-Verbose "Failed to read version.json: $($_.Exception.Message)"
        }
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
    .DESCRIPTION
        Converts a profile name or file path into a validated absolute path to the profile JSON file.
        Applies security checks including path traversal prevention, name length limits, and safe
        character validation before resolving against the profiles directory.

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

    # Handle full paths: if it's already a valid file path, return it directly
    if ([System.IO.Path]::IsPathRooted($ProfileName) -and (Test-Path -Path $ProfileName -PathType Leaf)) {
        return $ProfileName
    }

    # Strip .json extension if present for name validation
    $nameToValidate = $ProfileName
    if ($nameToValidate.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) {
        $nameToValidate = $nameToValidate.Substring(0, $nameToValidate.Length - 5)
    }

    # Security: Validate profile name is safe (alphanumeric, dash, underscore only)
    # This prevents path traversal attacks
    if ($nameToValidate -notmatch '^[a-zA-Z0-9_-]+$') {
        throw (Get-LocalizedString -Key 'profile.validation.invalid_name' -Parameters @{ Name = $ProfileName })
    }

    # Security: Limit profile name length to prevent DoS
    if ($nameToValidate.Length -gt 64) {
        throw (Get-LocalizedString -Key 'profile.validation.name_too_long')
    }

    # Try to find in Profiles directory
    if (-not $ProfilesDirectory) {
        $ProfilesDirectory = Join-Path -Path $script:RepositoryRoot -ChildPath 'Profiles'
    }

    # Security: Get canonical base path for validation
    $canonicalBase = [System.IO.Path]::GetFullPath($ProfilesDirectory)

    # Try with .json extension
    $profilePath = Join-Path -Path $ProfilesDirectory -ChildPath "$nameToValidate.json"
    $canonicalPath = [System.IO.Path]::GetFullPath($profilePath)

    # Security: Verify resolved path stays within profiles directory
    if (-not $canonicalPath.StartsWith($canonicalBase, [StringComparison]::OrdinalIgnoreCase)) {
        throw (Get-LocalizedString -Key 'profile.validation.path_traversal' -Parameters @{ Name = $ProfileName })
    }

    if (Test-Path -Path $profilePath) {
        return $profilePath
    }

    throw (Get-LocalizedString -Key 'profile.not_found' -Parameters @{ Name = "$ProfileName ($ProfilesDirectory)" })
}

function Import-ProfileJson {
    <#
    .SYNOPSIS
        Loads a profile from JSON file with caching.
    .DESCRIPTION
        Reads a deployment profile JSON file, parses it into a DeploymentProfile object, and stores
        the result in the profile cache for faster subsequent loads. Uses the file's last-write
        timestamp to detect stale cache entries and reload automatically when the file changes.

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
        throw (Get-LocalizedString -Key 'profile.not_found' -Parameters @{ Name = $Path })
    }

    # Validate profile against schema before loading (security check)
    if ($script:UseSchemaValidation) {
        try {
            $validationResult = Test-DeploymentProfile -ProfilePath $Path
            if (-not $validationResult.IsValid) {
                $errorMessages = $validationResult.Errors -join '; '
                throw (Get-LocalizedString -Key 'profile.validation.schema_failed' -Parameters @{ Path = $Path; Errors = $errorMessages })
            }
            Write-Verbose "Profile schema validation passed: $Path"
        } catch {
            if ($_.Exception.Message -like '*schema validation failed*') {
                throw
            }
            # Log validation errors but continue if schema module has issues
            Write-Status -Message (Get-LocalizedString -Key 'profile.validation.schema_skipped' -Parameters @{ Error = $_.Exception.Message }) -Level 'Warning'
        }
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
        Write-Status -Message (Get-LocalizedString -Key 'profile.loading' -Parameters @{ Name = $Path }) -Level 'Verbose'

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

        Write-Status -Message (Get-LocalizedString -Key 'profile.loaded' -Parameters @{ Name = "$($deploymentProfile.Name) v$($deploymentProfile.Version)"; AppCount = $deploymentProfile.Applications.Count }) -Level 'Success'

        # Store in cache
        Set-CachedProfile -Path $Path -Profile $deploymentProfile

        return $deploymentProfile

    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'profile.load_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
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

    function New-InheritanceGraph {
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
                New-InheritanceGraph -Name $parent -Dir $Dir
            }
        } catch {
            $result.InheritanceGraph[$Name] = @{
                Inherits = @()
                Path = $null
                Error = $_.Exception.Message
            }
        }
    }

    function Find-Cycles {
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
                Find-Cycles -Name $parent -Path $Path
            }
        }

        [void]$recursionStack.Pop()
        [void]$Path.RemoveAt($Path.Count - 1)
    }

    # Build graph starting from target profile
    New-InheritanceGraph -Name $ProfileName -Dir $ProfilesDirectory

    # Detect cycles for all profiles in graph
    foreach ($name in $result.InheritanceGraph.Keys) {
        $visited.Clear()
        $recursionStack.Clear()
        $path = [System.Collections.Generic.List[string]]::new()
        Find-Cycles -Name $name -Path $path
    }

    # Remove duplicate cycles
    $result.Cycles = $result.Cycles | Select-Object -Unique

    return $result
}

function Resolve-ProfileInheritance {
    <#
    .SYNOPSIS
        Resolves profile inheritance recursively with cycle detection.
    .DESCRIPTION
        Walks the inheritance chain defined by each profile's 'inheritsFrom' property, loading
        parent profiles recursively and returning them in base-first order. Tracks visited ancestors
        to detect and report circular inheritance before it causes infinite recursion.

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

        $errorMsg = Get-LocalizedString -Key 'profile.inheritance.cycle_detected_compact' -Parameters @{ Cycle = $cycleString }
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
                if ($_.Exception -is [System.InvalidOperationException]) {
                    throw
                }
                Write-Status -Message (Get-LocalizedString -Key 'profile.inheritance.parent_load_failed' -Parameters @{ Parent = $parentName; Error = $_.Exception.Message }) -Level 'Warning'
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
                Write-Status -Message (Get-LocalizedString -Key 'profile.applications.not_found_in_db' -Parameters @{ AppId = $AppReference }) -Level 'Warning'
                return $null
            }
            return ConvertTo-ProfileApplication -App $dbApp
        } else {
            Write-Status -Message (Get-LocalizedString -Key 'profile.applications.db_not_available' -Parameters @{ AppId = $AppReference }) -Level 'Warning'
            return $null
        }
    }

    # Case 2: Object with AppId property (new format with overrides)
    if ($AppReference.PSObject.Properties['AppId']) {
        if ($script:UseAppDatabase) {
            $dbApp = Get-ApplicationById -AppId $AppReference.AppId
            if ($null -eq $dbApp) {
                Write-Status -Message (Get-LocalizedString -Key 'profile.applications.not_found_in_db' -Parameters @{ AppId = $AppReference.AppId }) -Level 'Warning'
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
            Write-Status -Message (Get-LocalizedString -Key 'profile.applications.db_not_available' -Parameters @{ AppId = $AppReference.AppId }) -Level 'Warning'
            return $null
        }
    }

    # Case 3: Full application object (legacy format)
    if ($AppReference.PSObject.Properties['Name']) {
        return $AppReference
    }

    Write-Status -Message (Get-LocalizedString -Key 'profile.applications.unknown_format') -Level 'Warning'
    return $null
}

function Merge-ProfileApplications {
    <#
    .SYNOPSIS
        Merges applications from multiple profiles with deduplication.
    .DESCRIPTION
        Iterates through profiles in inheritance order and combines their application lists into a
        single deduplicated collection. When the same application appears in multiple profiles, the
        child profile's definition takes precedence over the parent.

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
    .DESCRIPTION
        Combines system configuration hashtables from profiles in inheritance order, with child
        profile settings overriding parent values. Produces a single merged configuration hashtable
        ready for use by the system configuration module.

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
    .DESCRIPTION
        Serves as the primary entry point for loading a deployment profile. Resolves the profile
        path, loads the JSON file, walks the full inheritance chain, and returns a merged result
        containing the combined applications list and system configuration.

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

    Write-Status -Message (Get-LocalizedString -Key 'profile.loading' -Parameters @{ Name = $ProfileName }) -Level 'Info'

    try {
        # Get profile path
        $profilePath = Get-ProfilePath -ProfileName $ProfileName -ProfilesDirectory $ProfilesDirectory

        # Security: Runtime cycle detection before loading
        Write-Status -Message (Get-LocalizedString -Key 'profile.inheritance.checking_cycles') -Level 'Verbose'
        $cycleCheck = Test-ProfileCycles -ProfileName $ProfileName -ProfilesDirectory $ProfilesDirectory
        if ($cycleCheck.HasCycles) {
            $cycleDetails = $cycleCheck.Cycles -join '; '
            throw [System.InvalidOperationException]::new(
                (Get-LocalizedString -Key 'profile.inheritance.cycle_detected' -Parameters @{ ProfileName = $ProfileName; Details = $cycleDetails })
            )
        }

        # Load base profile
        $baseProfile = Import-ProfileJson -Path $profilePath

        # Resolve inheritance
        Write-Status -Message (Get-LocalizedString -Key 'profile.inheritance.resolving_chain') -Level 'Info'
        $profileChain = Resolve-ProfileInheritance -InputProfile $baseProfile -ProfilesDirectory $ProfilesDirectory

        Write-Status -Message (Get-LocalizedString -Key 'profile.inheritance.chain' -Parameters @{ Chain = ($profileChain.Name -join ' -> ') }) -Level 'Info'

        # Merge applications
        Write-Status -Message (Get-LocalizedString -Key 'profile.merge.applications') -Level 'Info'
        $mergedApplications = @(Merge-ProfileApplications -Profiles $profileChain)

        # Merge system configuration
        Write-Status -Message (Get-LocalizedString -Key 'profile.merge.system_configuration') -Level 'Info'
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

        Write-Status -Message (Get-LocalizedString -Key 'profile.loaded_successfully') -Level 'Success'
        Write-Status -Message (Get-LocalizedString -Key 'profile.summary.total_applications' -Parameters @{ Count = $mergedApplications.Count }) -Level 'Info'
        $configSectionCount = if ($mergedConfig) { $mergedConfig.Keys.Count } else { 0 }
        Write-Status -Message (Get-LocalizedString -Key 'profile.summary.configuration_sections' -Parameters @{ Count = $configSectionCount }) -Level 'Info'

        return $finalProfile

    } catch {
        Write-Status -Message (Get-LocalizedString -Key 'profile.load_failed' -Parameters @{ Error = $_.Exception.Message }) -Level 'Error'
        throw
    }
}

# === PROFILE VALIDATION ===

function Test-ProfileValid {
    <#
    .SYNOPSIS
        Validates a profile JSON structure.
    .DESCRIPTION
        Performs structural validation on a profile JSON file, checking for required fields, valid
        application references, and correct inheritance declarations. Returns a result hashtable
        containing a validity flag along with any errors and warnings found.

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
    .DESCRIPTION
        Organizes an array of application objects into a hashtable keyed by their Category property.
        Applications without a category are placed into an 'Uncategorized' group.

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
    .DESCRIPTION
        Returns only the applications from the input array whose Required property is set to true,
        excluding optional applications from the deployment list.

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

# === APPID CROSS-REFERENCE VALIDATION ===

function Test-ProfileAppIds {
    <#
    .SYNOPSIS
        Validates that all AppIds in a profile exist in the applications database.

    .DESCRIPTION
        Cross-references AppIds from a profile against the applications.json database
        to ensure all referenced applications are valid and installable.

    .PARAMETER ProfilePath
        Path to profile JSON file to validate.

    .PARAMETER ApplicationsDbPath
        Path to applications.json database. Defaults to Apps/Database/applications.json.

    .OUTPUTS
        [hashtable] Validation result with:
          - Valid: [bool] Whether all AppIds are valid
          - InvalidAppIds: [string[]] List of AppIds not found in database
          - ValidAppIds: [string[]] List of valid AppIds
          - TotalAppIds: [int] Total number of AppIds in profile

    .EXAMPLE
        Test-ProfileAppIds -ProfilePath "Profiles/Gaming.json"
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfilePath,

        [Parameter()]
        [string]$ApplicationsDbPath
    )

    $result = @{
        Valid = $true
        InvalidAppIds = @()
        ValidAppIds = @()
        TotalAppIds = 0
        Errors = @()
    }

    # Determine applications database path
    if ([string]::IsNullOrWhiteSpace($ApplicationsDbPath)) {
        $ApplicationsDbPath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
    }

    # Load applications database
    if (-not (Test-Path -Path $ApplicationsDbPath)) {
        $result.Valid = $false
        $result.Errors += "Applications database not found: $ApplicationsDbPath"
        return $result
    }

    try {
        $dbContent = Get-Content -Path $ApplicationsDbPath -Raw -Encoding UTF8
        $database = $dbContent | ConvertFrom-Json -ErrorAction Stop

        # Build HashSet of valid AppIds for O(1) lookup
        $validAppIdSet = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase
        )

        if ($database.Applications) {
            foreach ($prop in $database.Applications.PSObject.Properties) {
                $null = $validAppIdSet.Add($prop.Name)
            }
        }

        Write-Verbose "Loaded $($validAppIdSet.Count) valid AppIds from database"

    } catch {
        $result.Valid = $false
        $result.Errors += "Failed to parse applications database: $($_.Exception.Message)"
        return $result
    }

    # Load profile
    if (-not (Test-Path -Path $ProfilePath)) {
        $result.Valid = $false
        $result.Errors += "Profile not found: $ProfilePath"
        return $result
    }

    try {
        $profile = Import-ProfileJson -Path $ProfilePath

        # Extract AppIds from profile
        $profileAppIds = @()
        foreach ($app in $profile.Applications) {
            if ($app -is [string]) {
                $profileAppIds += $app
            } elseif ($app.PSObject.Properties['AppId']) {
                $profileAppIds += $app.AppId
            }
        }

        $result.TotalAppIds = $profileAppIds.Count

        # Validate each AppId
        foreach ($appId in $profileAppIds) {
            if ($validAppIdSet.Contains($appId)) {
                $result.ValidAppIds += $appId
            } else {
                $result.InvalidAppIds += $appId
                $result.Valid = $false
            }
        }

        if ($result.InvalidAppIds.Count -gt 0) {
            Write-Status -Message (Get-LocalizedString -Key 'profile.validation.invalid_appids' -Parameters @{ Count = $result.InvalidAppIds.Count; AppIds = ($result.InvalidAppIds -join ', ') }) -Level 'Warning'
        } else {
            Write-Status -Message (Get-LocalizedString -Key 'profile.validation.all_appids_valid' -Parameters @{ Count = $result.TotalAppIds }) -Level 'Success'
        }

    } catch {
        $result.Valid = $false
        $result.Errors += (Get-LocalizedString -Key 'profile.validation.failed' -Parameters @{ Error = $_.Exception.Message })
    }

    return $result
}

function Test-AllProfilesAppIds {
    <#
    .SYNOPSIS
        Validates AppIds in all profiles in the Profiles directory.

    .DESCRIPTION
        Iterates through all profile JSON files and validates their AppIds
        against the applications database.

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files.

    .OUTPUTS
        [hashtable] Summary of validation results for all profiles.

    .EXAMPLE
        Test-AllProfilesAppIds
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [string]$ProfilesDirectory
    )

    if ([string]::IsNullOrWhiteSpace($ProfilesDirectory)) {
        $ProfilesDirectory = Join-Path $script:RepositoryRoot 'Profiles'
    }

    $summary = @{
        TotalProfiles = 0
        ValidProfiles = 0
        InvalidProfiles = 0
        AllInvalidAppIds = @()
        Results = @{}
    }

    $profileFiles = Get-ChildItem -Path $ProfilesDirectory -Filter '*.json' -File

    foreach ($file in $profileFiles) {
        $summary.TotalProfiles++
        $profileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)

        Write-Status -Message (Get-LocalizedString -Key 'profile.validation.validating_profile' -Parameters @{ Name = $profileName }) -Level 'Info'
        $result = Test-ProfileAppIds -ProfilePath $file.FullName

        $summary.Results[$profileName] = $result

        if ($result.Valid) {
            $summary.ValidProfiles++
        } else {
            $summary.InvalidProfiles++
            $summary.AllInvalidAppIds += $result.InvalidAppIds
        }
    }

    # Deduplicate invalid AppIds
    $summary.AllInvalidAppIds = $summary.AllInvalidAppIds | Select-Object -Unique

    Write-Status -Message (Get-LocalizedString -Key 'profile.validation.complete' -Parameters @{ Valid = $summary.ValidProfiles; Total = $summary.TotalProfiles }) -Level $(if ($summary.InvalidProfiles -eq 0) { 'Success' } else { 'Warning' })

    return $summary
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
    'Test-ProfileAppIds',
    'Test-AllProfilesAppIds',
    'Get-ApplicationsByCategory',
    'Get-RequiredApplications',
    'ConvertTo-Hashtable',
    'Clear-ProfileCache'
)

