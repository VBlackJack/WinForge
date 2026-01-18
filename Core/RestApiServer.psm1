<#
.SYNOPSIS
    Win11Forge - REST API Server Module v3.1.4

.DESCRIPTION
    Provides a local REST API server for Win11Forge:
    - HTTP listener on configurable port (default: 5170)
    - localhost only for security
    - JSON request/response handling
    - Endpoint registration and routing

.NOTES
    Author: Julien Bombled
    Version: 3.1.4
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
$script:CoreModulePath = Join-Path $script:ModuleRoot 'Core.psm1'
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\api-settings.json'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# === SERVER STATE ===
$script:ServerState = @{
    Listener = $null
    Running = $false
    Port = 5170
    Host = 'localhost'
    Endpoints = @{}
    RequestCount = 0
    StartTime = $null
    Config = $null
    RateLimitState = @{}  # IP -> { Count, WindowStart }
    ApiKeys = @{}         # key -> keyConfig
}

# === DEFAULT CONFIGURATION ===
$script:DefaultConfig = @{
    Port = 5170
    Host = 'localhost'
    RequestTimeoutMs = 30000
    MaxConcurrentRequests = 10
    EnableCors = $false
    LogRequests = $true
    RequireAuthentication = $true
    ApiKeyHeader = 'X-API-Key'
    RateLimitEnabled = $true
    MaxRequestsPerMinute = 60
    MaxRequestsPerHour = 1000
}

# === CONFIGURATION FUNCTIONS ===

function Get-ApiConfig {
    <#
    .SYNOPSIS
        Loads and returns the API configuration.

    .OUTPUTS
        Hashtable containing API configuration.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($null -eq $script:ServerState.Config) {
        if (Test-Path -Path $script:ConfigPath) {
            try {
                $json = Get-Content -Path $script:ConfigPath -Raw | ConvertFrom-Json
                $script:ServerState.Config = @{
                    Port = if ($null -ne $json.port) { $json.port } else { $script:DefaultConfig.Port }
                    Host = if ($null -ne $json.host) { $json.host } else { $script:DefaultConfig.Host }
                    RequestTimeoutMs = if ($null -ne $json.requestTimeoutMs) { $json.requestTimeoutMs } else { $script:DefaultConfig.RequestTimeoutMs }
                    MaxConcurrentRequests = if ($null -ne $json.maxConcurrentRequests) { $json.maxConcurrentRequests } else { $script:DefaultConfig.MaxConcurrentRequests }
                    EnableCors = if ($null -ne $json.enableCors) { $json.enableCors } else { $script:DefaultConfig.EnableCors }
                    LogRequests = if ($null -ne $json.logRequests) { $json.logRequests } else { $script:DefaultConfig.LogRequests }
                    RequireAuthentication = if ($null -ne $json.security.requireAuthentication) { $json.security.requireAuthentication } else { $script:DefaultConfig.RequireAuthentication }
                    ApiKeyHeader = if ($null -ne $json.security.apiKeyHeader) { $json.security.apiKeyHeader } else { $script:DefaultConfig.ApiKeyHeader }
                    RateLimitEnabled = if ($null -ne $json.security.rateLimiting.enabled) { $json.security.rateLimiting.enabled } else { $script:DefaultConfig.RateLimitEnabled }
                    MaxRequestsPerMinute = if ($null -ne $json.security.rateLimiting.maxRequestsPerMinute) { $json.security.rateLimiting.maxRequestsPerMinute } else { $script:DefaultConfig.MaxRequestsPerMinute }
                    MaxRequestsPerHour = if ($null -ne $json.security.rateLimiting.maxRequestsPerHour) { $json.security.rateLimiting.maxRequestsPerHour } else { $script:DefaultConfig.MaxRequestsPerHour }
                }

                # Load API keys
                if ($json.apiKeys.keys) {
                    foreach ($keyConfig in $json.apiKeys.keys) {
                        if ($keyConfig.enabled) {
                            $script:ServerState.ApiKeys[$keyConfig.key] = @{
                                Id = $keyConfig.id
                                Description = $keyConfig.description
                                Permissions = $keyConfig.permissions
                                CreatedAt = $keyConfig.createdAt
                                ExpiresAt = $keyConfig.expiresAt
                            }
                        }
                    }
                }

                # Load public endpoints
                $script:ServerState.PublicEndpoints = @()
                if ($json.apiKeys.publicEndpoints) {
                    $script:ServerState.PublicEndpoints = @($json.apiKeys.publicEndpoints)
                }
            } catch {
                Write-Verbose "Failed to load API config, using defaults: $($_.Exception.Message)"
                $script:ServerState.Config = $script:DefaultConfig.Clone()
            }
        } else {
            $script:ServerState.Config = $script:DefaultConfig.Clone()
        }
    }

    return $script:ServerState.Config
}

# === AUTHENTICATION FUNCTIONS ===

function Test-PublicEndpoint {
    <#
    .SYNOPSIS
        Tests if an endpoint is public (no authentication required).

    .PARAMETER Path
        The URL path to test.

    .OUTPUTS
        Boolean indicating if endpoint is public.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not $script:ServerState.PublicEndpoints) {
        return $false
    }

    return $script:ServerState.PublicEndpoints -contains $Path
}

function Test-ApiKeyValid {
    <#
    .SYNOPSIS
        Validates an API key.

    .PARAMETER ApiKey
        The API key to validate.

    .PARAMETER RequiredPermission
        Optional permission required for the operation.

    .OUTPUTS
        Hashtable with validation result and key info.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ApiKey,

        [Parameter()]
        [ValidateSet('read', 'write', 'deploy', 'admin')]
        [string]$RequiredPermission
    )

    $result = @{
        Valid = $false
        KeyId = $null
        Message = ''
        Permissions = @()
    }

    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        $result.Message = 'API key is required'
        return $result
    }

    if (-not $script:ServerState.ApiKeys.ContainsKey($ApiKey)) {
        $result.Message = 'Invalid API key'
        return $result
    }

    $keyConfig = $script:ServerState.ApiKeys[$ApiKey]

    # Check expiration
    if ($keyConfig.ExpiresAt) {
        $expirationDate = [datetime]::Parse($keyConfig.ExpiresAt)
        if ((Get-Date) -gt $expirationDate) {
            $result.Message = 'API key has expired'
            return $result
        }
    }

    # Check permission if required
    if ($RequiredPermission -and $keyConfig.Permissions -notcontains $RequiredPermission) {
        $result.Message = "API key does not have '$RequiredPermission' permission"
        return $result
    }

    $result.Valid = $true
    $result.KeyId = $keyConfig.Id
    $result.Permissions = $keyConfig.Permissions
    $result.Message = 'Valid'

    return $result
}

function Test-RateLimit {
    <#
    .SYNOPSIS
        Tests if a client has exceeded rate limits.

    .PARAMETER ClientIp
        The client IP address.

    .OUTPUTS
        Hashtable with rate limit status.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ClientIp
    )

    $config = Get-ApiConfig
    $result = @{
        Allowed = $true
        RetryAfterSeconds = 0
        Message = ''
        RequestsInMinute = 0
        RequestsInHour = 0
    }

    if (-not $config.RateLimitEnabled) {
        return $result
    }

    $now = Get-Date

    # Initialize state for new client
    if (-not $script:ServerState.RateLimitState.ContainsKey($ClientIp)) {
        $script:ServerState.RateLimitState[$ClientIp] = @{
            MinuteWindowStart = $now
            MinuteCount = 0
            HourWindowStart = $now
            HourCount = 0
        }
    }

    $state = $script:ServerState.RateLimitState[$ClientIp]

    # Reset minute window if needed
    if (($now - $state.MinuteWindowStart).TotalSeconds -ge 60) {
        $state.MinuteWindowStart = $now
        $state.MinuteCount = 0
    }

    # Reset hour window if needed
    if (($now - $state.HourWindowStart).TotalSeconds -ge 3600) {
        $state.HourWindowStart = $now
        $state.HourCount = 0
    }

    # Check minute limit
    if ($state.MinuteCount -ge $config.MaxRequestsPerMinute) {
        $result.Allowed = $false
        $result.RetryAfterSeconds = [int](60 - ($now - $state.MinuteWindowStart).TotalSeconds)
        $result.Message = "Rate limit exceeded: $($config.MaxRequestsPerMinute) requests per minute"
        $result.RequestsInMinute = $state.MinuteCount
        $result.RequestsInHour = $state.HourCount
        return $result
    }

    # Check hour limit
    if ($state.HourCount -ge $config.MaxRequestsPerHour) {
        $result.Allowed = $false
        $result.RetryAfterSeconds = [int](3600 - ($now - $state.HourWindowStart).TotalSeconds)
        $result.Message = "Rate limit exceeded: $($config.MaxRequestsPerHour) requests per hour"
        $result.RequestsInMinute = $state.MinuteCount
        $result.RequestsInHour = $state.HourCount
        return $result
    }

    # Increment counters
    $state.MinuteCount++
    $state.HourCount++

    $result.RequestsInMinute = $state.MinuteCount
    $result.RequestsInHour = $state.HourCount

    return $result
}

function Get-RequiredPermission {
    <#
    .SYNOPSIS
        Determines required permission based on HTTP method and path.

    .PARAMETER Method
        The HTTP method.

    .PARAMETER Path
        The URL path.

    .OUTPUTS
        Required permission string.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Path
    )

    # Deploy/rollback operations require deploy permission
    if ($Path -match '/api/(deploy|rollback)') {
        return 'deploy'
    }

    # POST/PUT/DELETE require write permission
    if ($Method -in @('POST', 'PUT', 'DELETE')) {
        return 'write'
    }

    # GET requests require read permission
    return 'read'
}

# === ENDPOINT REGISTRATION ===

function Register-ApiEndpoint {
    <#
    .SYNOPSIS
        Registers an API endpoint handler.

    .DESCRIPTION
        Maps a URL path and HTTP method to a handler script block.

    .PARAMETER Path
        The URL path (e.g., '/api/version').

    .PARAMETER Method
        HTTP method: GET, POST, PUT, DELETE

    .PARAMETER Handler
        Script block that handles the request. Receives request context as parameter.

    .PARAMETER Description
        Optional description for documentation.

    .EXAMPLE
        Register-ApiEndpoint -Path '/api/version' -Method 'GET' -Handler { return @{ version = '3.1.4' } }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE')]
        [string]$Method = 'GET',

        [Parameter(Mandatory)]
        [scriptblock]$Handler,

        [Parameter()]
        [string]$Description = ''
    )

    $key = "$Method`:$Path"
    $script:ServerState.Endpoints[$key] = @{
        Path = $Path
        Method = $Method
        Handler = $Handler
        Description = $Description
        RegisteredAt = Get-Date
    }

    Write-Verbose "Registered endpoint: $Method $Path"
}

function Unregister-ApiEndpoint {
    <#
    .SYNOPSIS
        Removes a registered API endpoint.

    .PARAMETER Path
        The URL path.

    .PARAMETER Method
        HTTP method.

    .EXAMPLE
        Unregister-ApiEndpoint -Path '/api/custom' -Method 'GET'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE')]
        [string]$Method = 'GET'
    )

    $key = "$Method`:$Path"
    if ($script:ServerState.Endpoints.ContainsKey($key)) {
        $script:ServerState.Endpoints.Remove($key)
        Write-Verbose "Unregistered endpoint: $Method $Path"
    }
}

function Get-RegisteredEndpoints {
    <#
    .SYNOPSIS
        Returns all registered endpoints.

    .OUTPUTS
        Array of endpoint information objects.
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $endpoints = @()
    foreach ($key in $script:ServerState.Endpoints.Keys) {
        $ep = $script:ServerState.Endpoints[$key]
        $endpoints += [PSCustomObject]@{
            Path = $ep.Path
            Method = $ep.Method
            Description = $ep.Description
            RegisteredAt = $ep.RegisteredAt
        }
    }

    return $endpoints
}

# === SERVER FUNCTIONS ===

function Start-ApiServer {
    <#
    .SYNOPSIS
        Starts the REST API server.

    .DESCRIPTION
        Starts an HTTP listener on the configured port and begins
        processing incoming requests.

    .PARAMETER Port
        Optional port override.

    .PARAMETER Async
        If specified, runs the server in a background job.

    .EXAMPLE
        Start-ApiServer
        Start-ApiServer -Port 8080 -Async
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateRange(1024, 65535)]
        [int]$Port,

        [Parameter()]
        [switch]$Async
    )

    if ($script:ServerState.Running) {
        Write-Status -Message "API server is already running" -Level 'Warning' -Category 'Api'
        return
    }

    $config = Get-ApiConfig

    if ($PSBoundParameters.ContainsKey('Port')) {
        $config.Port = $Port
    }

    $script:ServerState.Port = $config.Port
    $script:ServerState.Host = $config.Host

    $prefix = "http://$($config.Host):$($config.Port)/"

    try {
        $script:ServerState.Listener = [System.Net.HttpListener]::new()
        $script:ServerState.Listener.Prefixes.Add($prefix)
        $script:ServerState.Listener.Start()
        $script:ServerState.Running = $true
        $script:ServerState.StartTime = Get-Date
        $script:ServerState.RequestCount = 0

        Write-Status -Message "API server started on $prefix" -Level 'Success' -Category 'Api' -StructuredData @{
            Port = $config.Port
            Host = $config.Host
        }

        if ($Async) {
            # Run in background
            $serverScript = {
                param($Listener, $Endpoints, $Config)
                while ($Listener.IsListening) {
                    try {
                        $context = $Listener.GetContext()
                        # Handle request (simplified for async)
                        $response = $context.Response
                        $response.StatusCode = 200
                        $response.Close()
                    } catch {
                        break
                    }
                }
            }
            Start-Job -ScriptBlock $serverScript -ArgumentList $script:ServerState.Listener, $script:ServerState.Endpoints, $config
        } else {
            # Run synchronously (blocking)
            Invoke-ApiServerLoop
        }
    } catch {
        $script:ServerState.Running = $false
        Write-Status -Message "Failed to start API server: $($_.Exception.Message)" -Level 'Error' -Category 'Api'
        throw
    }
}

function Stop-ApiServer {
    <#
    .SYNOPSIS
        Stops the REST API server.

    .EXAMPLE
        Stop-ApiServer
    #>
    [CmdletBinding()]
    param()

    if (-not $script:ServerState.Running) {
        Write-Status -Message "API server is not running" -Level 'Warning' -Category 'Api'
        return
    }

    try {
        $script:ServerState.Listener.Stop()
        $script:ServerState.Listener.Close()
        $script:ServerState.Running = $false

        $uptime = if ($script:ServerState.StartTime) {
            ((Get-Date) - $script:ServerState.StartTime).ToString()
        } else {
            'N/A'
        }

        Write-Status -Message "API server stopped" -Level 'Info' -Category 'Api' -StructuredData @{
            Uptime = $uptime
            TotalRequests = $script:ServerState.RequestCount
        }
    } catch {
        Write-Status -Message "Error stopping API server: $($_.Exception.Message)" -Level 'Error' -Category 'Api'
    }
}

function Invoke-ApiServerLoop {
    <#
    .SYNOPSIS
        Main server loop for processing requests.
    #>
    [CmdletBinding()]
    param()

    $config = Get-ApiConfig

    while ($script:ServerState.Listener.IsListening) {
        try {
            $context = $script:ServerState.Listener.GetContext()
            $script:ServerState.RequestCount++

            $request = $context.Request
            $response = $context.Response

            $method = $request.HttpMethod
            $path = $request.Url.LocalPath

            $clientIp = $request.RemoteEndPoint.Address.ToString()

            if ($config.LogRequests) {
                Write-Status -Message "API Request: $method $path from $clientIp" -Level 'Verbose' -Category 'Api'
            }

            # Check rate limit first
            $rateLimitResult = Test-RateLimit -ClientIp $clientIp
            if (-not $rateLimitResult.Allowed) {
                $response.StatusCode = 429
                $response.Headers.Add('Retry-After', $rateLimitResult.RetryAfterSeconds.ToString())
                $errorResponse = @{
                    error = $rateLimitResult.Message
                    retryAfter = $rateLimitResult.RetryAfterSeconds
                } | ConvertTo-Json
                $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorResponse)
                $response.ContentLength64 = $buffer.Length
                $response.ContentType = 'application/json'
                $response.OutputStream.Write($buffer, 0, $buffer.Length)
                $response.Close()
                continue
            }

            # Check authentication (skip for public endpoints)
            if ($config.RequireAuthentication -and -not (Test-PublicEndpoint -Path $path)) {
                $apiKey = $request.Headers[$config.ApiKeyHeader]
                $requiredPermission = Get-RequiredPermission -Method $method -Path $path
                $authResult = Test-ApiKeyValid -ApiKey $apiKey -RequiredPermission $requiredPermission

                if (-not $authResult.Valid) {
                    $response.StatusCode = 401
                    $errorResponse = @{
                        error = $authResult.Message
                        code = 'UNAUTHORIZED'
                    } | ConvertTo-Json
                    $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorResponse)
                    $response.ContentLength64 = $buffer.Length
                    $response.ContentType = 'application/json'
                    $response.OutputStream.Write($buffer, 0, $buffer.Length)
                    $response.Close()

                    if ($config.LogRequests) {
                        Write-Status -Message "API Auth failed: $($authResult.Message) for $path from $clientIp" -Level 'Warning' -Category 'Api'
                    }
                    continue
                }
            }

            # Find handler
            $key = "$method`:$path"
            $handler = $null

            if ($script:ServerState.Endpoints.ContainsKey($key)) {
                $handler = $script:ServerState.Endpoints[$key].Handler
            }

            if ($handler) {
                try {
                    # Build request context
                    $requestBody = $null
                    if ($request.HasEntityBody) {
                        $reader = [System.IO.StreamReader]::new($request.InputStream)
                        $requestBody = $reader.ReadToEnd()
                        $reader.Close()

                        if ($request.ContentType -match 'application/json') {
                            $requestBody = $requestBody | ConvertFrom-Json
                        }
                    }

                    $requestContext = @{
                        Method = $method
                        Path = $path
                        Query = $request.QueryString
                        Body = $requestBody
                        Headers = $request.Headers
                    }

                    # Execute handler
                    $result = & $handler $requestContext

                    # Send response
                    $response.StatusCode = 200
                    $response.ContentType = 'application/json'

                    $jsonResponse = $result | ConvertTo-Json -Depth 10 -Compress
                    $buffer = [System.Text.Encoding]::UTF8.GetBytes($jsonResponse)
                    $response.ContentLength64 = $buffer.Length
                    $response.OutputStream.Write($buffer, 0, $buffer.Length)
                } catch {
                    $response.StatusCode = 500
                    $errorResponse = @{ error = $_.Exception.Message } | ConvertTo-Json
                    $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorResponse)
                    $response.ContentLength64 = $buffer.Length
                    $response.OutputStream.Write($buffer, 0, $buffer.Length)
                }
            } else {
                # 404 Not Found
                $response.StatusCode = 404
                $notFoundResponse = @{ error = "Endpoint not found: $method $path" } | ConvertTo-Json
                $buffer = [System.Text.Encoding]::UTF8.GetBytes($notFoundResponse)
                $response.ContentLength64 = $buffer.Length
                $response.OutputStream.Write($buffer, 0, $buffer.Length)
            }

            $response.Close()
        } catch [System.Net.HttpListenerException] {
            # Listener was closed
            break
        } catch {
            Write-Status -Message "Request processing error: $($_.Exception.Message)" -Level 'Error' -Category 'Api'
        }
    }
}

# === STATUS FUNCTIONS ===

function Get-ApiServerStatus {
    <#
    .SYNOPSIS
        Returns the current API server status.

    .OUTPUTS
        PSCustomObject with server status information.

    .EXAMPLE
        Get-ApiServerStatus
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $uptime = $null
    if ($script:ServerState.Running -and $script:ServerState.StartTime) {
        $uptime = ((Get-Date) - $script:ServerState.StartTime).ToString()
    }

    return [PSCustomObject]@{
        Running = $script:ServerState.Running
        Host = $script:ServerState.Host
        Port = $script:ServerState.Port
        Url = "http://$($script:ServerState.Host):$($script:ServerState.Port)/"
        StartTime = $script:ServerState.StartTime
        Uptime = $uptime
        RequestCount = $script:ServerState.RequestCount
        EndpointCount = $script:ServerState.Endpoints.Count
    }
}

function Test-ApiServerRunning {
    <#
    .SYNOPSIS
        Tests if the API server is running and responding.

    .OUTPUTS
        Boolean indicating if server is responsive.

    .EXAMPLE
        if (Test-ApiServerRunning) { Write-Host "Server is up" }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if (-not $script:ServerState.Running) {
        return $false
    }

    try {
        $url = "http://$($script:ServerState.Host):$($script:ServerState.Port)/api/version"
        $response = Invoke-RestMethod -Uri $url -Method GET -TimeoutSec 5 -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

# === API KEY MANAGEMENT ===

function New-ApiKey {
    <#
    .SYNOPSIS
        Creates a new API key.

    .PARAMETER Id
        Unique identifier for the key.

    .PARAMETER Description
        Description of the key's purpose.

    .PARAMETER Permissions
        Array of permissions: read, write, deploy, admin.

    .PARAMETER ExpiresInDays
        Optional expiration in days.

    .OUTPUTS
        The generated API key string.

    .EXAMPLE
        $key = New-ApiKey -Id 'automation' -Description 'CI/CD automation' -Permissions @('read', 'deploy')
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Id,

        [Parameter()]
        [string]$Description = '',

        [Parameter()]
        [ValidateSet('read', 'write', 'deploy', 'admin')]
        [string[]]$Permissions = @('read'),

        [Parameter()]
        [int]$ExpiresInDays
    )

    # Generate a secure random key
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $apiKey = [Convert]::ToBase64String($bytes) -replace '[+/=]', ''
    $apiKey = "w11f_$($apiKey.Substring(0, 40))"

    $expiresAt = $null
    if ($ExpiresInDays -gt 0) {
        $expiresAt = (Get-Date).AddDays($ExpiresInDays).ToString('yyyy-MM-dd')
    }

    $script:ServerState.ApiKeys[$apiKey] = @{
        Id = $Id
        Description = $Description
        Permissions = $Permissions
        CreatedAt = (Get-Date).ToString('yyyy-MM-dd')
        ExpiresAt = $expiresAt
    }

    Write-Verbose "Created API key: $Id"
    return $apiKey
}

function Remove-ApiKey {
    <#
    .SYNOPSIS
        Removes an API key by its ID or key value.

    .PARAMETER Id
        The ID of the key to remove.

    .PARAMETER Key
        The actual key string to remove.

    .EXAMPLE
        Remove-ApiKey -Id 'automation'
    #>
    [CmdletBinding()]
    param(
        [Parameter(ParameterSetName = 'ById')]
        [string]$Id,

        [Parameter(ParameterSetName = 'ByKey')]
        [string]$Key
    )

    if ($Key) {
        if ($script:ServerState.ApiKeys.ContainsKey($Key)) {
            $script:ServerState.ApiKeys.Remove($Key)
            Write-Verbose "Removed API key"
        }
    }
    elseif ($Id) {
        $keyToRemove = $null
        foreach ($k in $script:ServerState.ApiKeys.Keys) {
            if ($script:ServerState.ApiKeys[$k].Id -eq $Id) {
                $keyToRemove = $k
                break
            }
        }
        if ($keyToRemove) {
            $script:ServerState.ApiKeys.Remove($keyToRemove)
            Write-Verbose "Removed API key: $Id"
        }
    }
}

function Get-ApiKeys {
    <#
    .SYNOPSIS
        Returns all registered API keys (without exposing actual key values).

    .OUTPUTS
        Array of API key information objects.

    .EXAMPLE
        Get-ApiKeys
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $keys = @()
    foreach ($key in $script:ServerState.ApiKeys.Keys) {
        $config = $script:ServerState.ApiKeys[$key]
        $keys += [PSCustomObject]@{
            Id = $config.Id
            Description = $config.Description
            Permissions = $config.Permissions
            CreatedAt = $config.CreatedAt
            ExpiresAt = $config.ExpiresAt
            KeyPrefix = $key.Substring(0, [Math]::Min(10, $key.Length)) + '...'
        }
    }

    return $keys
}

function Get-RateLimitStatus {
    <#
    .SYNOPSIS
        Returns rate limit status for all tracked clients.

    .OUTPUTS
        Array of rate limit status objects.

    .EXAMPLE
        Get-RateLimitStatus
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $status = @()
    foreach ($ip in $script:ServerState.RateLimitState.Keys) {
        $state = $script:ServerState.RateLimitState[$ip]
        $status += [PSCustomObject]@{
            ClientIp = $ip
            RequestsInMinute = $state.MinuteCount
            RequestsInHour = $state.HourCount
            MinuteWindowStart = $state.MinuteWindowStart
            HourWindowStart = $state.HourWindowStart
        }
    }

    return $status
}

function Clear-RateLimitState {
    <#
    .SYNOPSIS
        Clears all rate limit tracking state.

    .EXAMPLE
        Clear-RateLimitState
    #>
    [CmdletBinding()]
    param()

    $script:ServerState.RateLimitState = @{}
    Write-Verbose "Rate limit state cleared"
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    # Configuration
    'Get-ApiConfig',
    # Endpoint Management
    'Register-ApiEndpoint',
    'Unregister-ApiEndpoint',
    'Get-RegisteredEndpoints',
    # Server Control
    'Start-ApiServer',
    'Stop-ApiServer',
    'Get-ApiServerStatus',
    'Test-ApiServerRunning',
    # Authentication
    'Test-ApiKeyValid',
    'Test-PublicEndpoint',
    'New-ApiKey',
    'Remove-ApiKey',
    'Get-ApiKeys',
    # Rate Limiting
    'Test-RateLimit',
    'Get-RateLimitStatus',
    'Clear-RateLimitState'
)
