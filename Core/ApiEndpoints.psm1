<#
.SYNOPSIS
    Win11Forge - API Endpoints Module v3.1.4

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
    Version: 3.5.0
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
$script:RestApiServerPath = Join-Path $script:ModuleRoot 'RestApiServer.psm1'
$script:VersionPath = Join-Path $script:RepositoryRoot 'version.json'
$script:DatabasePath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
$script:ProfilesPath = Join-Path $script:RepositoryRoot 'Apps\Profiles'

# Import RestApiServer module
if (-not (Get-Command -Name Register-ApiEndpoint -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:RestApiServerPath) {
        Import-Module -Name $script:RestApiServerPath -Force
    }
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
    #>
    param($Context)

    $versionInfo = @{
        framework = 'Win11Forge'
        version = '3.1.4'
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
    #>
    param($Context)

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
                    error = "Failed to parse profile: $($_.Exception.Message)"
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
    #>
    param($Context)

    $applications = @()
    $categories = @{}

    if (Test-Path $script:DatabasePath) {
        try {
            $db = Get-Content $script:DatabasePath -Raw | ConvertFrom-Json

            if ($db.Applications) {
                foreach ($prop in $db.Applications.PSObject.Properties) {
                    $app = $prop.Value
                    $appInfo = @{
                        id = $prop.Name
                        name = if ($app.Name) { $app.Name } else { $prop.Name }
                        category = if ($app.Category) { $app.Category } else { 'Uncategorized' }
                        installMethod = if ($app.InstallMethod) { $app.InstallMethod } else { 'Unknown' }
                        description = if ($app.Description) { $app.Description } else { '' }
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
                error = "Failed to load application database: $($_.Exception.Message)"
            }
        }
    }

    # Apply query filters if provided
    $query = $Context.Query
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
    #>
    param($Context)

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
    #>
    param($Context)

    if ($script:DeploymentState.Status -eq 'Running') {
        return @{
            success = $false
            error = 'Deployment is already in progress'
            currentProfile = $script:DeploymentState.CurrentProfile
        }
    }

    $body = $Context.Body
    if (-not $body -or -not $body.profile) {
        return @{
            success = $false
            error = 'Profile name is required in request body'
        }
    }

    $profileName = $body.profile
    $testMode = if ($body.testMode) { $body.testMode } else { $false }

    # Verify profile exists
    $profilePath = Join-Path $script:ProfilesPath "$profileName.json"
    if (-not (Test-Path $profilePath)) {
        return @{
            success = $false
            error = "Profile not found: $profileName"
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
        message = "Deployment started for profile: $profileName"
        profile = $profileName
        testMode = $testMode
        startTime = $script:DeploymentState.StartTime.ToString('o')
    }
}

function Start-RollbackHandler {
    <#
    .SYNOPSIS
        Handler for POST /api/rollback
    #>
    param($Context)

    $body = $Context.Body
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
                error = 'No applications to rollback'
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
            message = 'Rollback summary retrieved. Set force=true to execute.'
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
        error = 'RollbackManager not available'
    }
}

function Get-CacheStatsHandler {
    <#
    .SYNOPSIS
        Handler for GET /api/cache/stats
    #>
    param($Context)

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

    Write-Verbose "Registered 7 default API endpoints"
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
    'Get-CacheStatsHandler'
)
