<#
.SYNOPSIS
    Win11Forge - REST API Server Module v3.5.0

.DESCRIPTION
    Provides a local REST API server for Win11Forge:
    - HTTP listener on configurable port (default: 5170)
    - localhost only for security
    - JSON request/response handling
    - Endpoint registration and routing
    - Explicit resource cleanup for HttpContext and jobs

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
$script:CoreModulePath = Join-Path $script:ModuleRoot 'Core.psm1'
$script:SecureStoragePath = Join-Path $script:ModuleRoot 'SecureStorage.psm1'
$script:ConfigPath = Join-Path $script:RepositoryRoot 'Config\api-settings.json'

# Import Core module for logging
if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import SecureStorage module for DPAPI encryption
$script:UseSecureStorage = $false
if (Test-Path -Path $script:SecureStoragePath) {
    try {
        Import-Module -Name $script:SecureStoragePath -Force
        if (Get-Command -Name Test-SecureStorageAvailable -ErrorAction SilentlyContinue) {
            $script:UseSecureStorage = Test-SecureStorageAvailable
        }
    } catch {
        Write-Verbose "SecureStorage module not available: $($_.Exception.Message)"
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
    RateLimitState = @{}       # IP -> { MinuteCount, MinuteWindowStart, HourCount, HourWindowStart, LastAccess }
    ApiKeyRateLimitState = @{} # ApiKeyId -> { RequestCount, WindowStart, LastAccess }
    FailedAuthState = @{}      # IP -> { FailCount, FirstFailTime, BlockedUntil }
    ApiKeys = @{}              # key -> keyConfig
    LastCleanupTime = $null    # For periodic memory cleanup
}

# === DEFAULT CONFIGURATION ===
$script:DefaultConfig = @{
    Port = 5170
    Host = 'localhost'
    RequestTimeoutMs = 30000
    MaxConcurrentRequests = 10
    MaxRequestBodyBytes = 5242880  # 5MB maximum request body size
    EnableCors = $false
    LogRequests = $true
    RequireAuthentication = $true
    ApiKeyHeader = 'X-API-Key'
    CsrfEnabled = $true
    CsrfTokenHeader = 'X-CSRF-Token'
    CsrfTokenTtlMinutes = 60
    RateLimitEnabled = $true
    MaxRequestsPerMinute = 60
    MaxRequestsPerHour = 1000
    MaxFailedAuthPerHour = 10
    BlockDurationMinutes = 60
}

# CSRF Token State
$script:CsrfTokens = @{} # Token -> { CreatedAt, ApiKeyId }

# === CSRF FUNCTIONS ===

function New-CsrfToken {
    <#
    .SYNOPSIS
        Generates a new CSRF token for a given API key.
    .DESCRIPTION
        Creates a cryptographically secure CSRF token that is associated with
        a specific API key. Tokens expire after the configured TTL.
    .PARAMETER ApiKeyId
        The API key identifier to associate with this token.
    .OUTPUTS
        The generated CSRF token string.
    .EXAMPLE
        $token = New-CsrfToken -ApiKeyId 'gui-client'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$ApiKeyId
    )

    $config = Get-ApiConfig

    # Generate cryptographically secure token (256 bits)
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $token = [BitConverter]::ToString($bytes) -replace '-', ''
    $token = "csrf_$($token.ToLowerInvariant())"
    $rng.Dispose()

    # Store token with metadata
    $script:CsrfTokens[$token] = @{
        CreatedAt = Get-Date
        ApiKeyId = $ApiKeyId
        TtlMinutes = $config.CsrfTokenTtlMinutes
    }

    # Clean up expired tokens periodically
    Invoke-CsrfTokenCleanup

    Write-Verbose "Created CSRF token for API key: $ApiKeyId"
    return $token
}

function Test-CsrfToken {
    <#
    .SYNOPSIS
        Validates a CSRF token.
    .DESCRIPTION
        Checks if the provided CSRF token is valid, not expired, and
        optionally matches the expected API key.
    .PARAMETER Token
        The CSRF token to validate.
    .PARAMETER ApiKeyId
        Optional API key ID to verify token ownership.
    .OUTPUTS
        Hashtable with validation result.
    .EXAMPLE
        $result = Test-CsrfToken -Token $token -ApiKeyId 'gui-client'
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Token,

        [Parameter()]
        [string]$ApiKeyId
    )

    $result = @{
        Valid = $false
        Message = ''
        Expired = $false
    }

    # Check if CSRF is enabled
    $config = Get-ApiConfig
    if (-not $config.CsrfEnabled) {
        $result.Valid = $true
        $result.Message = 'CSRF validation disabled'
        return $result
    }

    if ([string]::IsNullOrWhiteSpace($Token)) {
        $result.Message = 'CSRF token is required'
        return $result
    }

    if (-not $script:CsrfTokens.ContainsKey($Token)) {
        $result.Message = 'Invalid CSRF token'
        return $result
    }

    $tokenData = $script:CsrfTokens[$Token]
    $now = Get-Date

    # Check expiration
    $expirationTime = $tokenData.CreatedAt.AddMinutes($tokenData.TtlMinutes)
    if ($now -gt $expirationTime) {
        $result.Message = 'CSRF token has expired'
        $result.Expired = $true
        # Remove expired token
        $script:CsrfTokens.Remove($Token)
        return $result
    }

    # Check API key ownership if specified
    if ($ApiKeyId -and $tokenData.ApiKeyId -ne $ApiKeyId) {
        $result.Message = 'CSRF token does not belong to this API key'
        return $result
    }

    $result.Valid = $true
    $result.Message = 'Valid'

    return $result
}

function Invoke-CsrfTokenCleanup {
    <#
    .SYNOPSIS
        Removes expired CSRF tokens from memory.
    .DESCRIPTION
        Cleans up tokens that have exceeded their TTL to prevent memory leaks.
        Called automatically during token generation.
    #>
    [CmdletBinding()]
    param()

    $now = Get-Date
    $expiredTokens = @()

    foreach ($token in $script:CsrfTokens.Keys) {
        $data = $script:CsrfTokens[$token]
        $expirationTime = $data.CreatedAt.AddMinutes($data.TtlMinutes)
        if ($now -gt $expirationTime) {
            $expiredTokens += $token
        }
    }

    foreach ($token in $expiredTokens) {
        $script:CsrfTokens.Remove($token)
    }

    if ($expiredTokens.Count -gt 0) {
        Write-Verbose "CSRF cleanup: removed $($expiredTokens.Count) expired tokens"
    }
}

function Get-CsrfTokenStatus {
    <#
    .SYNOPSIS
        Returns status of all active CSRF tokens.
    .OUTPUTS
        Array of CSRF token status objects.
    .EXAMPLE
        Get-CsrfTokenStatus
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $now = Get-Date
    $status = @()

    foreach ($token in $script:CsrfTokens.Keys) {
        $data = $script:CsrfTokens[$token]
        $expirationTime = $data.CreatedAt.AddMinutes($data.TtlMinutes)
        $remainingSeconds = [int]($expirationTime - $now).TotalSeconds

        $status += [PSCustomObject]@{
            TokenPrefix = $token.Substring(0, [Math]::Min(15, $token.Length)) + '...'
            ApiKeyId = $data.ApiKeyId
            CreatedAt = $data.CreatedAt
            ExpiresAt = $expirationTime
            RemainingSeconds = [Math]::Max(0, $remainingSeconds)
            Expired = $remainingSeconds -le 0
        }
    }

    return $status
}

function Clear-CsrfTokens {
    <#
    .SYNOPSIS
        Clears all CSRF tokens from memory.
    .DESCRIPTION
        Removes all CSRF tokens. Use with caution as this will invalidate
        all active sessions.
    .EXAMPLE
        Clear-CsrfTokens
    #>
    [CmdletBinding()]
    param()

    $count = $script:CsrfTokens.Count
    $script:CsrfTokens = @{}
    Write-Verbose "Cleared $count CSRF tokens"
}

# === SECURITY FUNCTIONS ===

function Get-SanitizedErrorMessage {
    <#
    .SYNOPSIS
        Sanitizes error messages before sending to API clients.
    .DESCRIPTION
        Removes sensitive information from exception messages to prevent
        information disclosure attacks. Internal details are logged server-side.
    .PARAMETER Exception
        The exception object to sanitize.
    .PARAMETER ErrorCode
        Optional error code for categorization.
    .OUTPUTS
        Hashtable with sanitized error response.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [System.Exception]$Exception,

        [Parameter()]
        [string]$ErrorCode = 'INTERNAL_ERROR'
    )

    # Log full exception server-side for debugging
    if (Get-Command -Name Write-Status -ErrorAction SilentlyContinue) {
        Write-Status -Message "API Error [$ErrorCode]: $($Exception.Message)" -Level 'Error' -Category 'Api'
    }

    # Map exception types to safe messages
    $safeMessage = switch -Regex ($Exception.GetType().Name) {
        'ArgumentException|ArgumentNullException' { 'Invalid request parameters' }
        'UnauthorizedAccessException' { 'Access denied' }
        'FileNotFoundException|DirectoryNotFoundException' { 'Resource not found' }
        'TimeoutException' { 'Operation timed out' }
        'InvalidOperationException' { 'Invalid operation' }
        'JsonException|JsonReaderException' { 'Invalid JSON format' }
        default { 'An internal error occurred' }
    }

    return @{
        error = $safeMessage
        code = $ErrorCode
        timestamp = (Get-Date).ToString('o')
    }
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

                # Load API keys - prefer secure storage over plain config
                if ($script:UseSecureStorage) {
                    try {
                        $secureKeys = Get-SecureApiKeysForAuth
                        if ($secureKeys -and $secureKeys.Count -gt 0) {
                            $script:ServerState.ApiKeys = $secureKeys
                            Write-Verbose "Loaded $($secureKeys.Count) API keys from secure storage (DPAPI)"
                        } else {
                            # Fall back to config file if no secure keys exist
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
                                        Write-Verbose "Warning: Using unencrypted API key from config. Consider using Save-SecureApiKey for encryption."
                                    }
                                }
                            }
                        }
                    } catch {
                        Write-Verbose "Failed to load secure keys, falling back to config: $($_.Exception.Message)"
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
                    }
                } else {
                    # Secure storage not available - SECURITY: Refuse to load plaintext API keys
                    # This prevents accidental exposure of credentials in production
                    if ($json.apiKeys.keys) {
                        $enabledKeys = @($json.apiKeys.keys | Where-Object { $_.enabled })
                        if ($enabledKeys.Count -gt 0) {
                            Write-Status -Message "SECURITY: DPAPI secure storage not available. Refusing to load plaintext API keys." -Level 'Error' -Category 'Api'
                            Write-Status -Message "Use Save-SecureApiKey cmdlet to store API keys securely, or disable authentication." -Level 'Warning' -Category 'Api'
                            # Do not load plaintext keys - this is a security requirement
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
        Tests if a client has exceeded rate limits using sliding window algorithm.
        Provides smoother rate limiting than fixed window by tracking actual request timestamps.

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

    # Periodic cleanup of stale entries (every 10 minutes)
    Invoke-RateLimitCleanup

    # Initialize state for new client with sliding window timestamp array
    if (-not $script:ServerState.RateLimitState.ContainsKey($ClientIp)) {
        $script:ServerState.RateLimitState[$ClientIp] = @{
            RequestTimestamps = [System.Collections.Generic.List[datetime]]::new()
            LastAccess = $now
        }
    }

    $state = $script:ServerState.RateLimitState[$ClientIp]
    $state.LastAccess = $now

    # Sliding window: Remove timestamps older than 1 hour
    $oneHourAgo = $now.AddHours(-1)
    $oneMinuteAgo = $now.AddMinutes(-1)

    # Prune old timestamps (older than 1 hour)
    $validTimestamps = [System.Collections.Generic.List[datetime]]::new()
    foreach ($ts in $state.RequestTimestamps) {
        if ($ts -gt $oneHourAgo) {
            $validTimestamps.Add($ts)
        }
    }
    $state.RequestTimestamps = $validTimestamps

    # Count requests in sliding windows
    $requestsInMinute = @($state.RequestTimestamps | Where-Object { $_ -gt $oneMinuteAgo }).Count
    $requestsInHour = $state.RequestTimestamps.Count

    # Check minute limit
    if ($requestsInMinute -ge $config.MaxRequestsPerMinute) {
        # Calculate retry-after based on oldest request in minute window
        $oldestInMinute = $state.RequestTimestamps | Where-Object { $_ -gt $oneMinuteAgo } | Sort-Object | Select-Object -First 1
        $result.Allowed = $false
        $result.RetryAfterSeconds = [int][Math]::Ceiling(60 - ($now - $oldestInMinute).TotalSeconds)
        if ($result.RetryAfterSeconds -lt 1) { $result.RetryAfterSeconds = 1 }
        $result.Message = "Rate limit exceeded: $($config.MaxRequestsPerMinute) requests per minute"
        $result.RequestsInMinute = $requestsInMinute
        $result.RequestsInHour = $requestsInHour
        return $result
    }

    # Check hour limit
    if ($requestsInHour -ge $config.MaxRequestsPerHour) {
        # Calculate retry-after based on oldest request in hour window
        $oldestInHour = $state.RequestTimestamps | Sort-Object | Select-Object -First 1
        $result.Allowed = $false
        $result.RetryAfterSeconds = [int][Math]::Ceiling(3600 - ($now - $oldestInHour).TotalSeconds)
        if ($result.RetryAfterSeconds -lt 1) { $result.RetryAfterSeconds = 1 }
        $result.Message = "Rate limit exceeded: $($config.MaxRequestsPerHour) requests per hour"
        $result.RequestsInMinute = $requestsInMinute
        $result.RequestsInHour = $requestsInHour
        return $result
    }

    # Add current request timestamp
    $state.RequestTimestamps.Add($now)

    $result.RequestsInMinute = $requestsInMinute + 1
    $result.RequestsInHour = $requestsInHour + 1

    return $result
}

function Test-ApiKeyRateLimit {
    <#
    .SYNOPSIS
        Tests rate limit for a specific API key using sliding window algorithm.
    .PARAMETER ApiKeyId
        The API key identifier.
    .OUTPUTS
        Hashtable with rate limit status.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ApiKeyId
    )

    $config = Get-ApiConfig
    $result = @{
        Allowed = $true
        RequestCount = 0
        Message = ''
        RetryAfterSeconds = 0
    }

    if (-not $config.RateLimitEnabled) {
        return $result
    }

    $now = Get-Date

    # Initialize state for new API key with sliding window
    if (-not $script:ServerState.ApiKeyRateLimitState.ContainsKey($ApiKeyId)) {
        $script:ServerState.ApiKeyRateLimitState[$ApiKeyId] = @{
            RequestTimestamps = [System.Collections.Generic.List[datetime]]::new()
            LastAccess = $now
        }
    }

    $state = $script:ServerState.ApiKeyRateLimitState[$ApiKeyId]
    $state.LastAccess = $now

    # Sliding window: Remove timestamps older than 1 hour
    $oneHourAgo = $now.AddHours(-1)
    $validTimestamps = [System.Collections.Generic.List[datetime]]::new()
    foreach ($ts in $state.RequestTimestamps) {
        if ($ts -gt $oneHourAgo) {
            $validTimestamps.Add($ts)
        }
    }
    $state.RequestTimestamps = $validTimestamps

    $requestCount = $state.RequestTimestamps.Count

    # Check limit (use MaxRequestsPerHour for API keys)
    if ($requestCount -ge $config.MaxRequestsPerHour) {
        $oldestRequest = $state.RequestTimestamps | Sort-Object | Select-Object -First 1
        $result.Allowed = $false
        $result.Message = "API key rate limit exceeded"
        $result.RequestCount = $requestCount
        $result.RetryAfterSeconds = [int][Math]::Ceiling(3600 - ($now - $oldestRequest).TotalSeconds)
        if ($result.RetryAfterSeconds -lt 1) { $result.RetryAfterSeconds = 1 }
        return $result
    }

    # Add current request timestamp
    $state.RequestTimestamps.Add($now)
    $result.RequestCount = $requestCount + 1

    return $result
}

function Test-FailedAuthBlock {
    <#
    .SYNOPSIS
        Tests if a client IP is blocked due to failed authentications.
    .PARAMETER ClientIp
        The client IP address.
    .OUTPUTS
        Hashtable with block status.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ClientIp
    )

    $config = Get-ApiConfig
    $result = @{
        Blocked = $false
        RetryAfterSeconds = 0
        FailCount = 0
    }

    $now = Get-Date

    if (-not $script:ServerState.FailedAuthState.ContainsKey($ClientIp)) {
        return $result
    }

    $state = $script:ServerState.FailedAuthState[$ClientIp]

    # Check if currently blocked
    if ($state.BlockedUntil -and $now -lt $state.BlockedUntil) {
        $result.Blocked = $true
        $result.RetryAfterSeconds = [int]($state.BlockedUntil - $now).TotalSeconds
        $result.FailCount = $state.FailCount
        return $result
    }

    # Reset if block expired
    if ($state.BlockedUntil -and $now -ge $state.BlockedUntil) {
        $script:ServerState.FailedAuthState.Remove($ClientIp)
    }

    return $result
}

function Add-FailedAuthAttempt {
    <#
    .SYNOPSIS
        Records a failed authentication attempt.
    .PARAMETER ClientIp
        The client IP address.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ClientIp
    )

    $config = Get-ApiConfig
    $now = Get-Date
    $maxFailed = if ($config.MaxFailedAuthPerHour) { $config.MaxFailedAuthPerHour } else { 10 }
    $blockMinutes = if ($config.BlockDurationMinutes) { $config.BlockDurationMinutes } else { 60 }

    if (-not $script:ServerState.FailedAuthState.ContainsKey($ClientIp)) {
        $script:ServerState.FailedAuthState[$ClientIp] = @{
            FailCount = 0
            FirstFailTime = $now
            BlockedUntil = $null
            LastAccess = $now
        }
    }

    $state = $script:ServerState.FailedAuthState[$ClientIp]
    $state.LastAccess = $now

    # Reset if hour passed since first fail
    if (($now - $state.FirstFailTime).TotalSeconds -ge 3600) {
        $state.FirstFailTime = $now
        $state.FailCount = 0
    }

    $state.FailCount++

    # Block if exceeded limit
    if ($state.FailCount -ge $maxFailed) {
        $state.BlockedUntil = $now.AddMinutes($blockMinutes)
        Write-Status -Message "IP $ClientIp blocked for $blockMinutes minutes due to $($state.FailCount) failed auth attempts" -Level 'Warning' -Category 'Api'
    }
}

function Invoke-RateLimitCleanup {
    <#
    .SYNOPSIS
        Cleans up stale rate limit entries to prevent memory leaks.
    .DESCRIPTION
        Removes entries that haven't been accessed in the last hour.
        Called periodically (every 10 minutes).
    #>
    [CmdletBinding()]
    param()

    $now = Get-Date
    $staleThresholdMinutes = 60

    # Only run cleanup every 10 minutes
    if ($script:ServerState.LastCleanupTime -and
        ($now - $script:ServerState.LastCleanupTime).TotalMinutes -lt 10) {
        return
    }

    $script:ServerState.LastCleanupTime = $now
    $cleanedCount = 0

    # Clean IP rate limit state
    $staleIps = @($script:ServerState.RateLimitState.Keys | Where-Object {
        $state = $script:ServerState.RateLimitState[$_]
        $state.LastAccess -and ($now - $state.LastAccess).TotalMinutes -gt $staleThresholdMinutes
    })

    foreach ($ip in $staleIps) {
        $script:ServerState.RateLimitState.Remove($ip)
        $cleanedCount++
    }

    # Clean API key rate limit state
    $staleKeys = @($script:ServerState.ApiKeyRateLimitState.Keys | Where-Object {
        $state = $script:ServerState.ApiKeyRateLimitState[$_]
        $state.LastAccess -and ($now - $state.LastAccess).TotalMinutes -gt $staleThresholdMinutes
    })

    foreach ($key in $staleKeys) {
        $script:ServerState.ApiKeyRateLimitState.Remove($key)
        $cleanedCount++
    }

    # Clean expired failed auth blocks
    $expiredBlocks = @($script:ServerState.FailedAuthState.Keys | Where-Object {
        $state = $script:ServerState.FailedAuthState[$_]
        ($state.BlockedUntil -and $now -ge $state.BlockedUntil) -or
        ($state.LastAccess -and ($now - $state.LastAccess).TotalMinutes -gt $staleThresholdMinutes)
    })

    foreach ($ip in $expiredBlocks) {
        $script:ServerState.FailedAuthState.Remove($ip)
        $cleanedCount++
    }

    if ($cleanedCount -gt 0) {
        Write-Status -Message "Rate limit cleanup: removed $cleanedCount stale entries" -Level 'Verbose' -Category 'Api'
    }
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
            # Run in background with concurrent request handling
            $serverJob = Start-Job -ScriptBlock {
                param($Port, $Host, $EndpointsJson, $ConfigJson, $ApiKeysJson, $PublicEndpointsJson)

                # Recreate server state in job
                $listener = [System.Net.HttpListener]::new()
                $prefix = "http://${Host}:${Port}/"
                $listener.Prefixes.Add($prefix)
                $listener.Start()

                $endpoints = $EndpointsJson | ConvertFrom-Json -AsHashtable
                $config = $ConfigJson | ConvertFrom-Json -AsHashtable
                $apiKeys = $ApiKeysJson | ConvertFrom-Json -AsHashtable
                $publicEndpoints = $PublicEndpointsJson | ConvertFrom-Json

                $rateLimitState = @{}

                while ($listener.IsListening) {
                    try {
                        $context = $listener.GetContext()
                        $request = $context.Request
                        $response = $context.Response

                        $method = $request.HttpMethod
                        $path = $request.Url.LocalPath
                        $clientIp = $request.RemoteEndPoint.Address.ToString()

                        # Rate limiting
                        $now = Get-Date
                        if (-not $rateLimitState.ContainsKey($clientIp)) {
                            $rateLimitState[$clientIp] = @{ MinuteStart = $now; MinuteCount = 0; HourStart = $now; HourCount = 0 }
                        }
                        $state = $rateLimitState[$clientIp]
                        if (($now - $state.MinuteStart).TotalSeconds -ge 60) { $state.MinuteStart = $now; $state.MinuteCount = 0 }
                        if (($now - $state.HourStart).TotalSeconds -ge 3600) { $state.HourStart = $now; $state.HourCount = 0 }

                        if ($state.MinuteCount -ge $config.MaxRequestsPerMinute -or $state.HourCount -ge $config.MaxRequestsPerHour) {
                            $response.StatusCode = 429
                            $response.Headers.Add('Retry-After', '60')
                            $buffer = [System.Text.Encoding]::UTF8.GetBytes('{"error":"Rate limit exceeded"}')
                            $response.ContentLength64 = $buffer.Length
                            $response.OutputStream.Write($buffer, 0, $buffer.Length)
                            $response.Close()
                            continue
                        }
                        $state.MinuteCount++
                        $state.HourCount++

                        # Authentication (skip for public endpoints)
                        if ($config.RequireAuthentication -and $path -notin $publicEndpoints) {
                            $apiKey = $request.Headers[$config.ApiKeyHeader]
                            if (-not $apiKey -or -not $apiKeys.ContainsKey($apiKey)) {
                                $response.StatusCode = 401
                                $buffer = [System.Text.Encoding]::UTF8.GetBytes('{"error":"Unauthorized"}')
                                $response.ContentLength64 = $buffer.Length
                                $response.OutputStream.Write($buffer, 0, $buffer.Length)
                                $response.Close()
                                continue
                            }
                        }

                        # Find handler
                        $key = "${method}:${path}"
                        $reader = $null
                        if ($endpoints.ContainsKey($key)) {
                            $handler = [scriptblock]::Create($endpoints[$key].Handler)
                            try {
                                $requestBody = $null
                                if ($request.HasEntityBody) {
                                    # Security: Validate request body size to prevent DoS
                                    $maxBodySize = $script:ServerState.Config.MaxRequestBodyBytes
                                    if ($request.ContentLength64 -gt $maxBodySize) {
                                        throw "Request body too large (max: $maxBodySize bytes)"
                                    }
                                    $reader = [System.IO.StreamReader]::new($request.InputStream)
                                    $requestBody = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction SilentlyContinue
                                }
                                $requestContext = @{ Method = $method; Path = $path; Query = $request.QueryString; Body = $requestBody }
                                $result = & $handler $requestContext
                                $jsonResponse = $result | ConvertTo-Json -Depth 10 -Compress
                                $response.StatusCode = 200
                            } catch {
                                # Sanitize error message - do not expose internal details
                                $jsonResponse = @{
                                    error = 'An internal error occurred'
                                    code = 'INTERNAL_ERROR'
                                    timestamp = (Get-Date).ToString('o')
                                } | ConvertTo-Json
                                $response.StatusCode = 500
                            } finally {
                                # Explicit StreamReader cleanup
                                if ($reader) { $reader.Dispose(); $reader = $null }
                            }
                        } else {
                            # Do not expose method/path details
                            $jsonResponse = @{
                                error = 'Endpoint not found'
                                code = 'NOT_FOUND'
                                timestamp = (Get-Date).ToString('o')
                            } | ConvertTo-Json
                            $response.StatusCode = 404
                        }

                        $response.ContentType = 'application/json'
                        $buffer = [System.Text.Encoding]::UTF8.GetBytes($jsonResponse)
                        $response.ContentLength64 = $buffer.Length
                        $response.OutputStream.Write($buffer, 0, $buffer.Length)
                        # Explicit response cleanup with error logging
                        try {
                            $response.OutputStream.Flush()
                            $response.OutputStream.Close()
                        } catch {
                            # Log but don't throw - response cleanup is best-effort
                            Write-Verbose "Response cleanup warning: $($_.Exception.Message)"
                        }
                        $response.Close()
                    } catch [System.Net.HttpListenerException] {
                        break
                    } catch {
                        # Log error but continue
                    }
                }

                $listener.Stop()
                $listener.Close()
            } -ArgumentList @(
                $config.Port,
                $config.Host,
                ($script:ServerState.Endpoints | ConvertTo-Json -Depth 10 -Compress),
                ($config | ConvertTo-Json -Depth 5 -Compress),
                ($script:ServerState.ApiKeys | ConvertTo-Json -Depth 5 -Compress),
                ($script:ServerState.PublicEndpoints | ConvertTo-Json -Compress)
            )

            # Store job reference for later management
            $script:ServerState.BackgroundJob = $serverJob
            Write-Status -Message "API server started in background (Job ID: $($serverJob.Id))" -Level 'Info' -Category 'Api'
            return $serverJob
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

    .DESCRIPTION
        Stops the HTTP listener and cleans up all resources including
        background jobs and rate limit state.

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
        # Stop and clean up background job if running
        if ($script:ServerState.BackgroundJob) {
            $job = $script:ServerState.BackgroundJob
            if ($job.State -eq 'Running') {
                Stop-Job -Job $job -ErrorAction SilentlyContinue
            }
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
            $script:ServerState.BackgroundJob = $null
        }

        # Stop and dispose listener
        if ($script:ServerState.Listener) {
            $script:ServerState.Listener.Stop()
            $script:ServerState.Listener.Close()
            $script:ServerState.Listener = $null
        }

        $script:ServerState.Running = $false

        # Clear rate limit state to free memory
        $script:ServerState.RateLimitState = @{}

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

                # CSRF validation for state-changing methods (POST, PUT, DELETE)
                if ($config.CsrfEnabled -and $method -in @('POST', 'PUT', 'DELETE')) {
                    # Skip CSRF for token generation endpoint
                    if ($path -ne '/api/csrf-token') {
                        $csrfToken = $request.Headers[$config.CsrfTokenHeader]
                        $csrfResult = Test-CsrfToken -Token $csrfToken -ApiKeyId $authResult.KeyId

                        if (-not $csrfResult.Valid) {
                            $response.StatusCode = 403
                            $errorResponse = @{
                                error = $csrfResult.Message
                                code = 'CSRF_VALIDATION_FAILED'
                            } | ConvertTo-Json
                            $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorResponse)
                            $response.ContentLength64 = $buffer.Length
                            $response.ContentType = 'application/json'
                            $response.OutputStream.Write($buffer, 0, $buffer.Length)
                            $response.Close()

                            if ($config.LogRequests) {
                                Write-Status -Message "CSRF validation failed for $path from ${clientIp}: $($csrfResult.Message)" -Level 'Warning' -Category 'Api'
                            }
                            continue
                        }
                    }
                }
            }

            # Find handler
            $key = "$method`:$path"
            $handler = $null

            if ($script:ServerState.Endpoints.ContainsKey($key)) {
                $handler = $script:ServerState.Endpoints[$key].Handler
            }

            if ($handler) {
                $reader = $null
                try {
                    # Build request context
                    $requestBody = $null
                    if ($request.HasEntityBody) {
                        # Security: Validate request body size to prevent DoS
                        $maxBodySize = $script:ServerState.Config.MaxRequestBodyBytes
                        if ($request.ContentLength64 -gt $maxBodySize) {
                            throw "Request body too large (max: $maxBodySize bytes)"
                        }
                        $reader = [System.IO.StreamReader]::new($request.InputStream)
                        $requestBody = $reader.ReadToEnd()

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
                    # Sanitize error message to prevent information disclosure
                    $sanitizedError = Get-SanitizedErrorMessage -Exception $_.Exception -ErrorCode 'HANDLER_ERROR'
                    $errorResponse = $sanitizedError | ConvertTo-Json
                    $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorResponse)
                    $response.ContentLength64 = $buffer.Length
                    $response.OutputStream.Write($buffer, 0, $buffer.Length)
                } finally {
                    # Explicit resource cleanup
                    if ($reader) {
                        $reader.Dispose()
                        $reader = $null
                    }
                }
            } else {
                # 404 Not Found - Do not expose method/path details
                $response.StatusCode = 404
                $notFoundResponse = @{
                    error = 'Endpoint not found'
                    code = 'NOT_FOUND'
                    timestamp = (Get-Date).ToString('o')
                } | ConvertTo-Json
                $buffer = [System.Text.Encoding]::UTF8.GetBytes($notFoundResponse)
                $response.ContentLength64 = $buffer.Length
                $response.OutputStream.Write($buffer, 0, $buffer.Length)
            }

            # Explicit HttpContext response cleanup with error logging
            try {
                $response.OutputStream.Flush()
                $response.OutputStream.Close()
            } catch {
                # Log but don't throw - response cleanup is best-effort
                Write-Verbose "Response cleanup warning: $($_.Exception.Message)"
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

    # Generate a secure random key with full entropy (256 bits)
    # Use hex encoding to preserve full entropy (no character filtering needed)
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    # Convert to hex string (64 chars) - preserves full 256-bit entropy
    $apiKey = [BitConverter]::ToString($bytes) -replace '-', ''
    $apiKey = "w11f_$($apiKey.ToLowerInvariant())"
    $rng.Dispose()

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
        Returns rate limit status for all tracked clients (sliding window).

    .OUTPUTS
        Array of rate limit status objects.

    .EXAMPLE
        Get-RateLimitStatus
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param()

    $now = Get-Date
    $oneMinuteAgo = $now.AddMinutes(-1)
    $oneHourAgo = $now.AddHours(-1)

    $status = @()
    foreach ($ip in $script:ServerState.RateLimitState.Keys) {
        $state = $script:ServerState.RateLimitState[$ip]
        $timestamps = $state.RequestTimestamps
        $requestsInMinute = @($timestamps | Where-Object { $_ -gt $oneMinuteAgo }).Count
        $requestsInHour = @($timestamps | Where-Object { $_ -gt $oneHourAgo }).Count

        $status += [PSCustomObject]@{
            ClientIp = $ip
            RequestsInMinute = $requestsInMinute
            RequestsInHour = $requestsInHour
            LastAccess = $state.LastAccess
            Algorithm = 'SlidingWindow'
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
    # CSRF Protection
    'New-CsrfToken',
    'Test-CsrfToken',
    'Invoke-CsrfTokenCleanup',
    'Get-CsrfTokenStatus',
    'Clear-CsrfTokens',
    # Rate Limiting
    'Test-RateLimit',
    'Test-ApiKeyRateLimit',
    'Test-FailedAuthBlock',
    'Add-FailedAuthAttempt',
    'Invoke-RateLimitCleanup',
    'Get-RateLimitStatus',
    'Clear-RateLimitState',
    # Security
    'Get-SanitizedErrorMessage'
)
