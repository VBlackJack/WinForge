<#
.SYNOPSIS
    Win11Forge - API Endpoints Module

.DESCRIPTION
    Defines REST API endpoint handlers for Win11Forge:
    - Version information
    - Profile management
    - Application database
    - Deployment status and control
    - Cache statistics
    - Rollback operations

.NOTES
    Author: Julien Bombled
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

# Import Localization module for i18n
$script:LocalizationPath = Join-Path $script:ModuleRoot 'Localization.psm1'
if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationPath) {
        Import-Module -Name $script:LocalizationPath -Force
    }
}

$script:RestApiServerPath = Join-Path $script:ModuleRoot 'RestApiServer.psm1'
$script:VersionPath = Join-Path $script:RepositoryRoot 'Config\version.json'
$script:FrameworkVersion = 'unknown'
if (Test-Path -Path $script:VersionPath) {
    try {
        $script:FrameworkVersion = (Get-Content -Path $script:VersionPath -Raw | ConvertFrom-Json).Version
    } catch {
        Write-Verbose "Failed to read version.json at module load: $($_.Exception.Message)"
    }
}
$script:DatabasePath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
$script:ProfilesPath = Join-Path $script:RepositoryRoot 'Profiles'
$script:SchemasPath = Join-Path $script:RepositoryRoot 'Schemas'

# === VALIDATION CONSTANTS ===
$script:MAX_PROFILE_NAME_LENGTH = 100

# Import RestApiServer module
if (-not (Get-Command -Name Register-ApiEndpoint -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:RestApiServerPath) {
        Import-Module -Name $script:RestApiServerPath -Force
    }
}

# Import JsonSchemaValidation module for schema validation
$script:JsonSchemaValidationPath = Join-Path $script:RepositoryRoot 'Modules\JsonSchemaValidation.psm1'
if (-not (Get-Command -Name Test-JsonAgainstSchema -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:JsonSchemaValidationPath) {
        Import-Module -Name $script:JsonSchemaValidationPath -Force
    }
}

# === JSON VALIDATION HELPER ===

function Test-ApiRequestBody {
    <#
    .SYNOPSIS
        Validates an API request body against a JSON schema.
    .PARAMETER Body
        The parsed request body object.
    .PARAMETER SchemaName
        Name of the schema file (without path) to validate against.
    .OUTPUTS
        Hashtable with IsValid and Errors properties.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $Body,

        [Parameter(Mandatory)]
        [string]$SchemaName
    )

    $result = @{
        IsValid = $false
        Errors = @()
    }

    # Convert body to JSON string for schema validation
    try {
        if ($null -eq $Body) {
            $jsonString = '{}'
        } else {
            $jsonString = $Body | ConvertTo-Json -Depth 10 -Compress
        }

        $schemaPath = Join-Path $script:SchemasPath $SchemaName
        if (-not (Test-Path -Path $schemaPath)) {
            # Schema not found - skip validation but log warning
            Write-Verbose "Schema not found: $schemaPath"
            $result.IsValid = $true
            return $result
        }

        if (Get-Command -Name Test-JsonAgainstSchema -ErrorAction SilentlyContinue) {
            $schemaValidation = Test-JsonAgainstSchema -JsonContent $jsonString -SchemaPath $schemaPath
            if (-not $schemaValidation.IsValid) {
                $result.Errors = $schemaValidation.Errors
                return $result
            }
        }

        $result.IsValid = $true
    }
    catch {
        $result.Errors = @((Get-LocalizedString -Key 'api.endpoints.request_body_validation_failed' -Parameters @{ Error = $_.Exception.Message }))
    }

    return $result
}

function Test-JsonFileValid {
    <#
    .SYNOPSIS
        Validates a JSON file and optionally validates against a schema.
    .PARAMETER FilePath
        Path to the JSON file to validate.
    .PARAMETER SchemaName
        Name of the schema file (without path) to validate against.
    .OUTPUTS
        Hashtable with IsValid, Data, and ErrorMessage properties.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter()]
        [string]$SchemaName
    )

    $result = @{
        IsValid = $false
        Data = $null
        ErrorMessage = $null
    }

    try {
        if (-not (Test-Path -Path $FilePath)) {
            $result.ErrorMessage = (Get-LocalizedString -Key 'api.endpoints.file_not_found' -Parameters @{ FilePath = $FilePath })
            return $result
        }

        $jsonContent = Get-Content -Path $FilePath -Raw -ErrorAction Stop
        $result.Data = $jsonContent | ConvertFrom-Json -ErrorAction Stop

        # Optional schema validation
        if ($SchemaName -and (Get-Command -Name Test-JsonAgainstSchema -ErrorAction SilentlyContinue)) {
            $schemaPath = Join-Path $script:SchemasPath $SchemaName
            if (Test-Path -Path $schemaPath) {
                $schemaValidation = Test-JsonAgainstSchema -JsonContent $jsonContent -SchemaPath $schemaPath
                if (-not $schemaValidation.IsValid) {
                    $result.ErrorMessage = (Get-LocalizedString -Key 'api.endpoints.schema_validation_failed' -Parameters @{ Errors = ($schemaValidation.Errors -join '; ') })
                    return $result
                }
            }
        }

        $result.IsValid = $true
    }
    catch {
        $result.ErrorMessage = (Get-LocalizedString -Key 'api.endpoints.json_parsing_failed' -Parameters @{ Error = $_.Exception.Message })
    }

    return $result
}

# === DEPLOYMENT STATE ===
$script:DeploymentState = @{
    Status = 'Idle'
    CurrentProfile = $null
    Progress = 0
    StartTime = $null
    Applications = @()
    Errors = @()
}

# === ENDPOINT HANDLERS ===

function Get-VersionHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/version
    .DESCRIPTION
        Returns framework version information including the current version
        from Config/version.json, the API version, and a server timestamp.
    .PARAMETER Context
        The API request context containing headers, query parameters, and body.
    .OUTPUTS
        Hashtable with framework name, version, apiVersion, and timestamp.
    .EXAMPLE
        $result = Get-VersionHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    $versionInfo = @{
        framework = 'Win11Forge'
        version = $script:FrameworkVersion
        apiVersion = '1.0'
        timestamp = (Get-Date).ToString('o')
    }

    if (Test-Path $script:VersionPath) {
        try {
            $versionJson = Get-Content $script:VersionPath -Raw | ConvertFrom-Json
            $versionInfo.version = $versionJson.Version
            $versionInfo.lastUpdated = $versionJson.LastUpdated
        } catch {
            Write-Verbose "Failed to read version.json: $($_.Exception.Message)"
        }
    }

    return $versionInfo
}

function Get-ProfilesHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/profiles
    .DESCRIPTION
        Enumerates all deployment profiles from the Profiles/ directory.
        Returns each profile's name, description, application count, and file path.
    .PARAMETER Context
        The API request context containing headers, query parameters, and body.
    .OUTPUTS
        Hashtable with profiles array, count, and profilesDirectory path.
    .EXAMPLE
        $result = Get-ProfilesHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    $profiles = @()

    if (Test-Path $script:ProfilesPath) {
        $profileFiles = Get-ChildItem -Path $script:ProfilesPath -Filter '*.json' -ErrorAction SilentlyContinue

        foreach ($file in $profileFiles) {
            try {
                $profileData = Get-Content $file.FullName -Raw | ConvertFrom-Json
                $profiles += @{
                    id = $file.BaseName
                    name = if ($profileData.Name) { $profileData.Name } else { $file.BaseName }
                    description = if ($profileData.Description) { $profileData.Description } else { '' }
                    applicationCount = if ($profileData.Applications) { $profileData.Applications.Count } else { 0 }
                    filePath = $file.FullName
                }
            } catch {
                $profiles += @{
                    id = $file.BaseName
                    name = $file.BaseName
                    error = (Get-LocalizedString -Key 'api.endpoints.profile_parse_failed' -Parameters @{ Error = $_.Exception.Message })
                }
            }
        }
    }

    return @{
        profiles = $profiles
        count = $profiles.Count
        profilesDirectory = $script:ProfilesPath
    }
}

function Get-ApplicationsHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/applications
    .DESCRIPTION
        Returns the application database with optional filtering by category
        or search term. Supports query parameters: ?category=Gaming&search=steam.
        Search terms are regex-escaped to prevent ReDoS attacks.
    .PARAMETER Context
        The API request context. Query parameters 'category' and 'search' are supported.
    .OUTPUTS
        Hashtable with applications array, count, categories breakdown, and databasePath.
    .EXAMPLE
        $result = Get-ApplicationsHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    $applications = @()
    $categories = @{}

    if (Test-Path $script:DatabasePath) {
        try {
            $db = Get-Content $script:DatabasePath -Raw | ConvertFrom-Json

            if ($db.Applications) {
                foreach ($prop in $db.Applications.PSObject.Properties) {
                    $app = $prop.Value

                    # Determine install method from Sources property
                    $installMethod = 'Unknown'
                    if ($app.PSObject.Properties['Sources'] -and $app.Sources) {
                        $sourceKeys = @($app.Sources.PSObject.Properties.Name)
                        if ($sourceKeys.Count -gt 0) {
                            $installMethod = $sourceKeys[0]
                        }
                    }

                    $appInfo = @{
                        id = $prop.Name
                        name = if ($app.PSObject.Properties['Name'] -and $app.Name) { $app.Name } else { $prop.Name }
                        category = if ($app.PSObject.Properties['Category'] -and $app.Category) { $app.Category } else { 'Uncategorized' }
                        installMethod = $installMethod
                        description = if ($app.PSObject.Properties['Description'] -and $app.Description) { $app.Description } else { '' }
                    }

                    $applications += $appInfo

                    # Track categories
                    $cat = $appInfo.category
                    if (-not $categories.ContainsKey($cat)) {
                        $categories[$cat] = 0
                    }
                    $categories[$cat]++
                }
            }
        } catch {
            return @{
                error = (Get-LocalizedString -Key 'api.endpoints.database_load_failed' -Parameters @{ Error = $_.Exception.Message })
            }
        }
    }

    # Apply query filters if provided
    $query = if ($Context -is [hashtable]) { $Context['Query'] } elseif ($Context.PSObject.Properties['Query']) { $Context.Query } else { $null }
    if ($query -and $query['category']) {
        $filterCategory = $query['category']
        $applications = $applications | Where-Object { $_.category -eq $filterCategory }
    }

    if ($query -and $query['search']) {
        # Escape regex special characters to prevent ReDoS attacks
        $searchTerm = [regex]::Escape($query['search'])
        $applications = $applications | Where-Object {
            $_.name -match $searchTerm -or $_.id -match $searchTerm
        }
    }

    # Ensure applications is always an array (filters can return $null)
    $applications = @($applications)

    return @{
        applications = $applications
        count = $applications.Count
        categories = $categories
        databasePath = $script:DatabasePath
    }
}

function Get-StatusHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/status
    .DESCRIPTION
        Returns the current deployment state including status, profile,
        progress percentage, uptime, processed applications count, and errors.
    .PARAMETER Context
        The API request context containing headers, query parameters, and body.
    .OUTPUTS
        Hashtable with status, currentProfile, progress, startTime, uptime,
        applicationsProcessed, errors, and timestamp.
    .EXAMPLE
        $result = Get-StatusHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    $uptime = $null
    if ($script:DeploymentState.StartTime) {
        $uptime = ((Get-Date) - $script:DeploymentState.StartTime).ToString()
    }

    return @{
        status = $script:DeploymentState.Status
        currentProfile = $script:DeploymentState.CurrentProfile
        progress = $script:DeploymentState.Progress
        startTime = if ($script:DeploymentState.StartTime) { $script:DeploymentState.StartTime.ToString('o') } else { $null }
        uptime = $uptime
        applicationsProcessed = $script:DeploymentState.Applications.Count
        errors = $script:DeploymentState.Errors
        timestamp = (Get-Date).ToString('o')
    }
}

function Start-DeploymentHandler {
    <#
    .SYNOPSIS
        Handler for POST /api/deploy
    .DESCRIPTION
        Initiates a deployment for the specified profile. Validates the request
        body against the deployment schema, checks for path traversal attacks,
        and verifies the profile exists before starting. Returns an error if a
        deployment is already in progress.
    .PARAMETER Context
        The API request context. Body must contain 'profile' (required) and
        optionally 'testMode' (boolean).
    .OUTPUTS
        Hashtable with success, message, profile, testMode, and startTime.
    .EXAMPLE
        $result = Start-DeploymentHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    if ($script:DeploymentState.Status -eq 'Running') {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.deployment_already_running')
            currentProfile = $script:DeploymentState.CurrentProfile
        }
    }

    $body = $Context.Body

    # Validate request body against schema
    $validationResult = Test-ApiRequestBody -Body $body -SchemaName 'api-deploy-request.schema.json'
    if (-not $validationResult.IsValid) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.request_validation_failed')
            validationErrors = $validationResult.Errors
        }
    }

    if (-not $body -or -not $body.profile) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.profile_name_required')
        }
    }

    $profileName = $body.profile
    $testMode = if ($body.testMode) { $body.testMode } else { $false }

    # Security: Validate profile name doesn't contain path traversal attempts
    if ($profileName -match '\.\.|[/\\]') {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.profile_name_invalid_chars')
        }
    }

    # Security: Validate profile name length
    if ($profileName.Length -gt $script:MAX_PROFILE_NAME_LENGTH) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.profile_name_too_long' -Parameters @{ MaxLength = $script:MAX_PROFILE_NAME_LENGTH })
        }
    }

    # Verify profile exists with path traversal protection
    $profilePath = Join-Path $script:ProfilesPath "$profileName.json"
    $canonicalPath = [System.IO.Path]::GetFullPath($profilePath)
    $canonicalBase = [System.IO.Path]::GetFullPath($script:ProfilesPath)

    # Security: Verify resolved path stays within profiles directory
    if (-not $canonicalPath.StartsWith($canonicalBase, [StringComparison]::OrdinalIgnoreCase)) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.security_invalid_profile_path')
        }
    }

    if (-not (Test-Path $profilePath)) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.profile_not_found' -Parameters @{ ProfileName = $profileName })
        }
    }

    # Initialize deployment state
    $script:DeploymentState.Status = 'Starting'
    $script:DeploymentState.CurrentProfile = $profileName
    $script:DeploymentState.Progress = 0
    $script:DeploymentState.StartTime = Get-Date
    $script:DeploymentState.Applications = @()
    $script:DeploymentState.Errors = @()

    # Note: Actual deployment would be triggered here
    # This is a stub that would integrate with InstallationEngine

    return @{
        success = $true
        message = (Get-LocalizedString -Key 'api.endpoints.deployment_started' -Parameters @{ ProfileName = $profileName })
        profile = $profileName
        testMode = $testMode
        startTime = $script:DeploymentState.StartTime.ToString('o')
    }
}

function Start-RollbackHandler {
    <#
    .SYNOPSIS
        Handler for POST /api/rollback
    .DESCRIPTION
        Handles rollback requests. Without force=true, returns a rollback summary
        showing which applications can be rolled back. With force=true, executes
        the rollback via RollbackManager. Validates the request body against
        the rollback schema.
    .PARAMETER Context
        The API request context. Body optionally contains 'force' (boolean).
    .OUTPUTS
        Hashtable with success, message/error, and rollback summary or execution results.
    .EXAMPLE
        $result = Start-RollbackHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    $body = $Context.Body

    # Validate request body against schema
    $validationResult = Test-ApiRequestBody -Body $body -SchemaName 'api-rollback-request.schema.json'
    if (-not $validationResult.IsValid) {
        return @{
            success = $false
            error = (Get-LocalizedString -Key 'api.endpoints.request_validation_failed')
            validationErrors = $validationResult.Errors
        }
    }

    $force = if ($body -and $body.force) { $body.force } else { $false }

    # Import RollbackManager if available
    $rollbackManagerPath = Join-Path $script:RepositoryRoot 'Modules\RollbackManager.psm1'
    if (Test-Path $rollbackManagerPath) {
        Import-Module $rollbackManagerPath -Force -ErrorAction SilentlyContinue
    }

    if (Get-Command -Name 'Get-RollbackSummary' -ErrorAction SilentlyContinue) {
        $summary = Get-RollbackSummary

        if ($summary.TotalApps -eq 0) {
            return @{
                success = $false
                error = (Get-LocalizedString -Key 'api.endpoints.no_apps_to_rollback')
            }
        }

        if ($force -and (Get-Command -Name 'Invoke-RollbackWithConfirmation' -ErrorAction SilentlyContinue)) {
            try {
                $result = Invoke-RollbackWithConfirmation -Force
                return @{
                    success = $result.Success
                    message = $result.Message
                    appsRolledBack = $result.AppsRolledBack
                    errors = $result.Errors
                }
            } catch {
                return @{
                    success = $false
                    error = $_.Exception.Message
                }
            }
        }

        return @{
            success = $true
            message = (Get-LocalizedString -Key 'api.endpoints.rollback_summary_retrieved')
            summary = @{
                sessionId = $summary.SessionId
                totalApps = $summary.TotalApps
                rollbackableCount = $summary.RollbackableCount
                applications = $summary.Applications
            }
        }
    }

    return @{
        success = $false
        error = (Get-LocalizedString -Key 'api.endpoints.rollback_manager_unavailable')
    }
}

function Get-CacheStatsHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/cache/stats
    .DESCRIPTION
        Returns Winget cache statistics including hit/miss rates, cache age,
        search cache entries, and last warmup timestamp. Requires WingetCache
        module to be available.
    .PARAMETER Context
        The API request context containing headers, query parameters, and body.
    .OUTPUTS
        Hashtable with winget cache statistics and timestamp.
    .EXAMPLE
        $result = Get-CacheStatsHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    # Import WingetCache if available
    $wingetCachePath = Join-Path $script:RepositoryRoot 'Modules\WingetCache.psm1'
    if (Test-Path $wingetCachePath) {
        Import-Module $wingetCachePath -Force -ErrorAction SilentlyContinue
    }

    $stats = @{
        winget = $null
        timestamp = (Get-Date).ToString('o')
    }

    if (Get-Command -Name 'Get-WingetCacheStatistics' -ErrorAction SilentlyContinue) {
        $wingetStats = Get-WingetCacheStatistics
        $stats.winget = @{
            listCacheValid = $wingetStats.ListCacheValid
            listCacheAgeMinutes = $wingetStats.ListCacheAgeMinutes
            listHits = $wingetStats.ListHits
            listMisses = $wingetStats.ListMisses
            listHitRate = $wingetStats.ListHitRate
            searchCacheEntries = $wingetStats.SearchCacheEntries
            searchHits = $wingetStats.SearchHits
            searchMisses = $wingetStats.SearchMisses
            searchHitRate = $wingetStats.SearchHitRate
            lastWarmup = if ($wingetStats.LastWarmup) { $wingetStats.LastWarmup.ToString('o') } else { $null }
        }
    }

    return $stats
}

function Get-CsrfTokenHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/csrf-token
    .DESCRIPTION
        Generates and returns a new CSRF token for the authenticated API key.
        This token must be included in the X-CSRF-Token header for all
        state-changing requests (POST, PUT, DELETE).
    .PARAMETER Context
        The API request context. Must include a valid API key in headers.
    .OUTPUTS
        Hashtable with csrfToken, expiresInMinutes, headerName, and timestamp.
    .EXAMPLE
        $result = Get-CsrfTokenHandler -Context $requestContext
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Context
    )

    # Get API key from headers to associate token
    $config = Get-ApiConfig
    $apiKey = $Context.Headers[$config.ApiKeyHeader]

    if (-not $apiKey) {
        return @{
            error = (Get-LocalizedString -Key 'api.endpoints.csrf_api_key_required')
            code = 'UNAUTHORIZED'
        }
    }

    # Validate API key and get its ID
    $authResult = Test-ApiKeyValid -ApiKey $apiKey
    if (-not $authResult.Valid) {
        return @{
            error = $authResult.Message
            code = 'UNAUTHORIZED'
        }
    }

    # Generate new CSRF token
    $csrfToken = New-CsrfToken -ApiKeyId $authResult.KeyId

    return @{
        csrfToken = $csrfToken
        expiresInMinutes = $config.CsrfTokenTtlMinutes
        headerName = $config.CsrfTokenHeader
        timestamp = (Get-Date).ToString('o')
    }
}

# === ENDPOINT REGISTRATION ===

function Register-DefaultEndpoints {
    <#
    .SYNOPSIS
        Registers all default API endpoints.

    .DESCRIPTION
        Sets up the standard Win11Forge API endpoints.

    .EXAMPLE
        Register-DefaultEndpoints
    #>
    [CmdletBinding()]
    param()

    # Ensure RestApiServer is loaded
    if (-not (Get-Command -Name Register-ApiEndpoint -ErrorAction SilentlyContinue)) {
        if (Test-Path -Path $script:RestApiServerPath) {
            Import-Module -Name $script:RestApiServerPath -Force
        }
    }

    # Register endpoints
    Register-ApiEndpoint -Path '/api/version' -Method 'GET' -Handler ${function:Get-VersionHandler} -Description 'Get framework version information'
    Register-ApiEndpoint -Path '/api/profiles' -Method 'GET' -Handler ${function:Get-ProfilesHandler} -Description 'List available deployment profiles'
    Register-ApiEndpoint -Path '/api/applications' -Method 'GET' -Handler ${function:Get-ApplicationsHandler} -Description 'Get application database'
    Register-ApiEndpoint -Path '/api/status' -Method 'GET' -Handler ${function:Get-StatusHandler} -Description 'Get current deployment status'
    Register-ApiEndpoint -Path '/api/deploy' -Method 'POST' -Handler ${function:Start-DeploymentHandler} -Description 'Start a deployment'
    Register-ApiEndpoint -Path '/api/rollback' -Method 'POST' -Handler ${function:Start-RollbackHandler} -Description 'Trigger rollback operation'
    Register-ApiEndpoint -Path '/api/cache/stats' -Method 'GET' -Handler ${function:Get-CacheStatsHandler} -Description 'Get cache statistics'
    Register-ApiEndpoint -Path '/api/csrf-token' -Method 'GET' -Handler ${function:Get-CsrfTokenHandler} -Description 'Get CSRF token for state-changing requests'

    Write-Verbose (Get-LocalizedString -Key 'api.endpoints.default_endpoints_registered' -Parameters @{ Count = 8 })
}

function Update-DeploymentState {
    <#
    .SYNOPSIS
        Updates the deployment state (called by InstallationEngine).

    .PARAMETER Status
        New status value.

    .PARAMETER Progress
        Progress percentage (0-100).

    .PARAMETER AppName
        Application being processed.

    .PARAMETER Error
        Error message if applicable.

    .EXAMPLE
        Update-DeploymentState -Status 'Running' -Progress 50 -AppName 'VSCode'
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateSet('Idle', 'Starting', 'Running', 'Completed', 'Failed', 'RollingBack')]
        [string]$Status,

        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Progress,

        [Parameter()]
        [string]$AppName,

        [Parameter()]
        [string]$Error
    )

    if ($PSBoundParameters.ContainsKey('Status')) {
        $script:DeploymentState.Status = $Status
    }

    if ($PSBoundParameters.ContainsKey('Progress')) {
        $script:DeploymentState.Progress = $Progress
    }

    if ($AppName) {
        $script:DeploymentState.Applications += @{
            name = $AppName
            timestamp = (Get-Date).ToString('o')
        }
    }

    if ($Error) {
        $script:DeploymentState.Errors += @{
            message = $Error
            timestamp = (Get-Date).ToString('o')
        }
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Register-DefaultEndpoints',
    'Update-DeploymentState',
    'Get-VersionHandler',
    'Get-ProfilesHandler',
    'Get-ApplicationsHandler',
    'Get-StatusHandler',
    'Start-DeploymentHandler',
    'Start-RollbackHandler',
    'Get-CacheStatsHandler',
    'Get-CsrfTokenHandler'
)
