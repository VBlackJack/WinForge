<#
.SYNOPSIS
    Win11Forge - Custom Exception Classes v3.1.4

.DESCRIPTION
    Defines custom exception classes for semantic error handling
    across the Win11Forge framework. Enables precise error
    categorization and handling.

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

# === BASE EXCEPTION CLASS ===

class Win11ForgeException : System.Exception {
    [string]$Category
    [datetime]$Timestamp
    [hashtable]$Context

    Win11ForgeException() : base() {
        $this.Category = 'General'
        $this.Timestamp = Get-Date
        $this.Context = @{}
    }

    Win11ForgeException([string]$message) : base($message) {
        $this.Category = 'General'
        $this.Timestamp = Get-Date
        $this.Context = @{}
    }

    Win11ForgeException([string]$message, [System.Exception]$innerException) : base($message, $innerException) {
        $this.Category = 'General'
        $this.Timestamp = Get-Date
        $this.Context = @{}
    }

    [string] ToLogString() {
        return "[{0}] [{1}] {2}" -f $this.Timestamp.ToString('o'), $this.Category, $this.Message
    }
}

# === INSTALLATION EXCEPTIONS ===

class InstallationException : Win11ForgeException {
    [string]$AppName
    [string]$AppId
    [string]$Method
    [int]$ExitCode

    InstallationException([string]$message, [string]$appName) : base($message) {
        $this.Category = 'Installation'
        $this.AppName = $appName
        $this.AppId = $null
        $this.Method = $null
        $this.ExitCode = -1
    }

    InstallationException([string]$message, [string]$appName, [string]$method) : base($message) {
        $this.Category = 'Installation'
        $this.AppName = $appName
        $this.AppId = $null
        $this.Method = $method
        $this.ExitCode = -1
    }

    InstallationException([string]$message, [string]$appName, [string]$method, [int]$exitCode) : base($message) {
        $this.Category = 'Installation'
        $this.AppName = $appName
        $this.AppId = $null
        $this.Method = $method
        $this.ExitCode = $exitCode
    }

    [string] ToLogString() {
        return "[{0}] [Installation] App={1} Method={2} ExitCode={3}: {4}" -f `
            $this.Timestamp.ToString('o'), $this.AppName, $this.Method, $this.ExitCode, $this.Message
    }
}

class WingetException : InstallationException {
    [string]$WingetOutput

    WingetException([string]$message, [string]$appName) : base($message, $appName, 'Winget') {
        $this.WingetOutput = $null
    }

    WingetException([string]$message, [string]$appName, [int]$exitCode, [string]$output) : base($message, $appName, 'Winget', $exitCode) {
        $this.WingetOutput = $output
    }
}

class ChocolateyException : InstallationException {
    [string]$ChocoOutput

    ChocolateyException([string]$message, [string]$appName) : base($message, $appName, 'Chocolatey') {
        $this.ChocoOutput = $null
    }

    ChocolateyException([string]$message, [string]$appName, [int]$exitCode, [string]$output) : base($message, $appName, 'Chocolatey', $exitCode) {
        $this.ChocoOutput = $output
    }
}

class DirectDownloadException : InstallationException {
    [string]$Url
    [string]$ExpectedChecksum
    [string]$ActualChecksum

    DirectDownloadException([string]$message, [string]$appName, [string]$url) : base($message, $appName, 'DirectDownload') {
        $this.Url = $url
        $this.ExpectedChecksum = $null
        $this.ActualChecksum = $null
    }

    DirectDownloadException([string]$message, [string]$appName, [string]$url, [string]$expected, [string]$actual) : base($message, $appName, 'DirectDownload') {
        $this.Url = $url
        $this.ExpectedChecksum = $expected
        $this.ActualChecksum = $actual
    }
}

class StoreException : InstallationException {
    [string]$StoreId

    StoreException([string]$message, [string]$appName, [string]$storeId) : base($message, $appName, 'Store') {
        $this.StoreId = $storeId
    }
}

# === DETECTION EXCEPTIONS ===

class DetectionException : Win11ForgeException {
    [string]$AppName
    [string]$DetectionMethod
    [string]$DetectionPath

    DetectionException([string]$message, [string]$appName) : base($message) {
        $this.Category = 'Detection'
        $this.AppName = $appName
        $this.DetectionMethod = $null
        $this.DetectionPath = $null
    }

    DetectionException([string]$message, [string]$appName, [string]$method, [string]$path) : base($message) {
        $this.Category = 'Detection'
        $this.AppName = $appName
        $this.DetectionMethod = $method
        $this.DetectionPath = $path
    }
}

class PathTraversalException : DetectionException {
    [string]$MaliciousPath

    PathTraversalException([string]$message, [string]$path) : base($message, $null) {
        $this.Category = 'Security'
        $this.MaliciousPath = $path
    }
}

# === CONFIGURATION EXCEPTIONS ===

class ConfigurationException : Win11ForgeException {
    [string]$ConfigFile
    [string]$ConfigKey

    ConfigurationException([string]$message) : base($message) {
        $this.Category = 'Configuration'
        $this.ConfigFile = $null
        $this.ConfigKey = $null
    }

    ConfigurationException([string]$message, [string]$configFile) : base($message) {
        $this.Category = 'Configuration'
        $this.ConfigFile = $configFile
        $this.ConfigKey = $null
    }

    ConfigurationException([string]$message, [string]$configFile, [string]$configKey) : base($message) {
        $this.Category = 'Configuration'
        $this.ConfigFile = $configFile
        $this.ConfigKey = $configKey
    }
}

class ProfileException : ConfigurationException {
    [string]$ProfileName
    [string]$ProfilePath

    ProfileException([string]$message, [string]$profileName) : base($message) {
        $this.Category = 'Profile'
        $this.ProfileName = $profileName
        $this.ProfilePath = $null
    }

    ProfileException([string]$message, [string]$profileName, [string]$profilePath) : base($message) {
        $this.Category = 'Profile'
        $this.ProfileName = $profileName
        $this.ProfilePath = $profilePath
    }
}

# === NETWORK EXCEPTIONS ===

class NetworkException : Win11ForgeException {
    [string]$Url
    [int]$StatusCode

    NetworkException([string]$message) : base($message) {
        $this.Category = 'Network'
        $this.Url = $null
        $this.StatusCode = 0
    }

    NetworkException([string]$message, [string]$url) : base($message) {
        $this.Category = 'Network'
        $this.Url = $url
        $this.StatusCode = 0
    }

    NetworkException([string]$message, [string]$url, [int]$statusCode) : base($message) {
        $this.Category = 'Network'
        $this.Url = $url
        $this.StatusCode = $statusCode
    }
}

class DownloadException : NetworkException {
    [long]$ExpectedSize
    [long]$ActualSize

    DownloadException([string]$message, [string]$url) : base($message, $url) {
        $this.ExpectedSize = 0
        $this.ActualSize = 0
    }
}

class UrlValidationException : NetworkException {
    [string]$Domain
    [bool]$IsWhitelisted

    UrlValidationException([string]$message, [string]$url, [string]$domain) : base($message, $url) {
        $this.Category = 'Security'
        $this.Domain = $domain
        $this.IsWhitelisted = $false
    }
}

# === SECURITY EXCEPTIONS ===

class SecurityException : Win11ForgeException {
    [string]$SecurityContext
    [string]$AttemptedAction

    SecurityException([string]$message) : base($message) {
        $this.Category = 'Security'
        $this.SecurityContext = $null
        $this.AttemptedAction = $null
    }

    SecurityException([string]$message, [string]$context, [string]$action) : base($message) {
        $this.Category = 'Security'
        $this.SecurityContext = $context
        $this.AttemptedAction = $action
    }
}

class CommandInjectionException : SecurityException {
    [string]$SuspiciousInput

    CommandInjectionException([string]$message, [string]$input) : base($message) {
        $this.Category = 'Security'
        $this.SecurityContext = 'CommandInjection'
        $this.SuspiciousInput = $input
    }
}

class AuthenticationException : SecurityException {
    [string]$ApiKey
    [string]$ClientIp

    AuthenticationException([string]$message) : base($message) {
        $this.Category = 'Authentication'
        $this.ApiKey = $null
        $this.ClientIp = $null
    }

    AuthenticationException([string]$message, [string]$clientIp) : base($message) {
        $this.Category = 'Authentication'
        $this.ApiKey = $null
        $this.ClientIp = $clientIp
    }
}

# === ROLLBACK EXCEPTIONS ===

class RollbackException : Win11ForgeException {
    [string]$SessionId
    [string[]]$FailedApps

    RollbackException([string]$message) : base($message) {
        $this.Category = 'Rollback'
        $this.SessionId = $null
        $this.FailedApps = @()
    }

    RollbackException([string]$message, [string]$sessionId) : base($message) {
        $this.Category = 'Rollback'
        $this.SessionId = $sessionId
        $this.FailedApps = @()
    }

    RollbackException([string]$message, [string]$sessionId, [string[]]$failedApps) : base($message) {
        $this.Category = 'Rollback'
        $this.SessionId = $sessionId
        $this.FailedApps = $failedApps
    }
}

# === API EXCEPTIONS ===

class ApiException : Win11ForgeException {
    [string]$Endpoint
    [string]$HttpMethod
    [int]$StatusCode

    ApiException([string]$message) : base($message) {
        $this.Category = 'API'
        $this.Endpoint = $null
        $this.HttpMethod = $null
        $this.StatusCode = 0
    }

    ApiException([string]$message, [string]$endpoint, [string]$method) : base($message) {
        $this.Category = 'API'
        $this.Endpoint = $endpoint
        $this.HttpMethod = $method
        $this.StatusCode = 0
    }

    ApiException([string]$message, [string]$endpoint, [string]$method, [int]$statusCode) : base($message) {
        $this.Category = 'API'
        $this.Endpoint = $endpoint
        $this.HttpMethod = $method
        $this.StatusCode = $statusCode
    }
}

class RateLimitException : ApiException {
    [int]$RetryAfterSeconds

    RateLimitException([string]$message, [string]$endpoint, [int]$retryAfter) : base($message, $endpoint, 'ANY', 429) {
        $this.RetryAfterSeconds = $retryAfter
    }
}

# === PLUGIN EXCEPTIONS ===

class PluginException : Win11ForgeException {
    [string]$PluginName
    [string]$PluginVersion
    [string]$HookName

    PluginException([string]$message, [string]$pluginName) : base($message) {
        $this.Category = 'Plugin'
        $this.PluginName = $pluginName
        $this.PluginVersion = $null
        $this.HookName = $null
    }

    PluginException([string]$message, [string]$pluginName, [string]$hookName) : base($message) {
        $this.Category = 'Plugin'
        $this.PluginName = $pluginName
        $this.PluginVersion = $null
        $this.HookName = $hookName
    }
}

# === HELPER FUNCTIONS ===

function New-InstallationException {
    <#
    .SYNOPSIS
        Creates a new InstallationException.
    #>
    [CmdletBinding()]
    [OutputType([InstallationException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter(Mandatory)]
        [string]$AppName,

        [string]$Method,

        [int]$ExitCode = -1
    )

    if ($Method) {
        return [InstallationException]::new($Message, $AppName, $Method, $ExitCode)
    }
    return [InstallationException]::new($Message, $AppName)
}

function New-WingetException {
    <#
    .SYNOPSIS
        Creates a new WingetException.
    #>
    [CmdletBinding()]
    [OutputType([WingetException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter(Mandatory)]
        [string]$AppName,

        [int]$ExitCode = -1,

        [string]$Output
    )

    if ($Output) {
        return [WingetException]::new($Message, $AppName, $ExitCode, $Output)
    }
    return [WingetException]::new($Message, $AppName)
}

function New-SecurityException {
    <#
    .SYNOPSIS
        Creates a new SecurityException.
    #>
    [CmdletBinding()]
    [OutputType([SecurityException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [string]$Context,

        [string]$Action
    )

    if ($Context -and $Action) {
        return [SecurityException]::new($Message, $Context, $Action)
    }
    return [SecurityException]::new($Message)
}

function New-ApiException {
    <#
    .SYNOPSIS
        Creates a new ApiException.
    #>
    [CmdletBinding()]
    [OutputType([ApiException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [string]$Endpoint,

        [string]$Method,

        [int]$StatusCode = 0
    )

    if ($Endpoint -and $Method) {
        return [ApiException]::new($Message, $Endpoint, $Method, $StatusCode)
    }
    return [ApiException]::new($Message)
}

function Test-Win11ForgeException {
    <#
    .SYNOPSIS
        Tests if an exception is a Win11Forge exception.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [System.Exception]$Exception
    )

    return $Exception -is [Win11ForgeException]
}

function Get-ExceptionCategory {
    <#
    .SYNOPSIS
        Gets the category of a Win11Forge exception.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [System.Exception]$Exception
    )

    if ($Exception -is [Win11ForgeException]) {
        return $Exception.Category
    }
    return 'Unknown'
}

# === STANDARDIZED ERROR CREATION ===

function New-Win11ForgeError {
    <#
    .SYNOPSIS
        Creates a standardized Win11Forge error with logging integration.
    .DESCRIPTION
        Central function for creating and logging errors across the framework.
        Automatically logs to structured logging if available and returns
        a properly formatted error record.
    .PARAMETER Message
        The error message.
    .PARAMETER Category
        Error category (Installation, Detection, Configuration, Network, Security, Plugin, API, Rollback, General).
    .PARAMETER ErrorCode
        Unique error code for programmatic handling.
    .PARAMETER Context
        Hashtable of additional context data.
    .PARAMETER InnerException
        The original exception that caused this error.
    .PARAMETER DoNotThrow
        If specified, returns the error object instead of throwing.
    .EXAMPLE
        New-Win11ForgeError -Message "Failed to install" -Category "Installation" -Context @{ AppName = "Firefox" }
    .EXAMPLE
        $err = New-Win11ForgeError -Message "Network timeout" -Category "Network" -ErrorCode "NET001" -DoNotThrow
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Installation', 'Detection', 'Configuration', 'Network', 'Security', 'Plugin', 'API', 'Rollback', 'General', 'Timeout', 'Validation')]
        [string]$Category = 'General',

        [Parameter()]
        [string]$ErrorCode,

        [Parameter()]
        [hashtable]$Context = @{},

        [Parameter()]
        [System.Exception]$InnerException,

        [Parameter()]
        [switch]$DoNotThrow
    )

    # Build error context
    $errorContext = @{
        Category = $Category
        ErrorCode = $ErrorCode
        Timestamp = Get-Date -Format 'o'
        Context = $Context
    }

    # Create appropriate exception type based on category
    $exception = switch ($Category) {
        'Installation' {
            $appName = if ($Context.AppName) { $Context.AppName } else { 'Unknown' }
            $method = if ($Context.Method) { $Context.Method } else { $null }
            if ($method) {
                [InstallationException]::new($Message, $appName, $method)
            } else {
                [InstallationException]::new($Message, $appName)
            }
        }
        'Detection' {
            $appName = if ($Context.AppName) { $Context.AppName } else { 'Unknown' }
            [DetectionException]::new($Message, $appName)
        }
        'Configuration' {
            $configFile = if ($Context.ConfigFile) { $Context.ConfigFile } else { $null }
            if ($configFile) {
                [ConfigurationException]::new($Message, $configFile)
            } else {
                [ConfigurationException]::new($Message)
            }
        }
        'Network' {
            $url = if ($Context.Url) { $Context.Url } else { $null }
            if ($url) {
                [NetworkException]::new($Message, $url)
            } else {
                [NetworkException]::new($Message)
            }
        }
        'Security' {
            $secContext = if ($Context.SecurityContext) { $Context.SecurityContext } else { $null }
            $action = if ($Context.Action) { $Context.Action } else { $null }
            if ($secContext -and $action) {
                [SecurityException]::new($Message, $secContext, $action)
            } else {
                [SecurityException]::new($Message)
            }
        }
        'Plugin' {
            $pluginName = if ($Context.PluginName) { $Context.PluginName } else { 'Unknown' }
            [PluginException]::new($Message, $pluginName)
        }
        'API' {
            $endpoint = if ($Context.Endpoint) { $Context.Endpoint } else { $null }
            $httpMethod = if ($Context.HttpMethod) { $Context.HttpMethod } else { $null }
            if ($endpoint -and $httpMethod) {
                [ApiException]::new($Message, $endpoint, $httpMethod)
            } else {
                [ApiException]::new($Message)
            }
        }
        'Rollback' {
            $sessionId = if ($Context.SessionId) { $Context.SessionId } else { $null }
            if ($sessionId) {
                [RollbackException]::new($Message, $sessionId)
            } else {
                [RollbackException]::new($Message)
            }
        }
        default {
            if ($InnerException) {
                [Win11ForgeException]::new($Message, $InnerException)
            } else {
                [Win11ForgeException]::new($Message)
            }
        }
    }

    # Add error code to context
    if ($ErrorCode) {
        $exception.Context['ErrorCode'] = $ErrorCode
    }

    # Log the error using Write-Status if available
    if (Get-Command -Name Write-Status -ErrorAction SilentlyContinue) {
        $logMessage = if ($ErrorCode) { "[$ErrorCode] $Message" } else { $Message }
        $structuredData = @{
            ErrorCode = $ErrorCode
            Category = $Category
            Context = $Context
        }
        Write-Status -Message $logMessage -Level 'Error' -Category $Category -StructuredData $structuredData
    }

    # Return or throw
    if ($DoNotThrow) {
        return $exception
    } else {
        throw $exception
    }
}

function Format-Win11ForgeError {
    <#
    .SYNOPSIS
        Formats a Win11Forge exception for display or logging.
    .PARAMETER Exception
        The exception to format.
    .PARAMETER IncludeStackTrace
        Include stack trace in output.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [System.Exception]$Exception,

        [Parameter()]
        [switch]$IncludeStackTrace
    )

    $output = New-Object System.Text.StringBuilder

    if ($Exception -is [Win11ForgeException]) {
        [void]$output.AppendLine("[$($Exception.Category)] $($Exception.Message)")
        [void]$output.AppendLine("Timestamp: $($Exception.Timestamp.ToString('o'))")

        if ($Exception.Context.Count -gt 0) {
            [void]$output.AppendLine("Context:")
            foreach ($key in $Exception.Context.Keys) {
                [void]$output.AppendLine("  $key : $($Exception.Context[$key])")
            }
        }
    } else {
        [void]$output.AppendLine("[General] $($Exception.Message)")
    }

    if ($IncludeStackTrace -and $Exception.StackTrace) {
        [void]$output.AppendLine("")
        [void]$output.AppendLine("Stack Trace:")
        [void]$output.AppendLine($Exception.StackTrace)
    }

    if ($Exception.InnerException) {
        [void]$output.AppendLine("")
        [void]$output.AppendLine("Inner Exception: $($Exception.InnerException.Message)")
    }

    return $output.ToString()
}

# === TIMEOUT EXCEPTION ===

class TimeoutException : Win11ForgeException {
    [string]$Operation
    [int]$TimeoutSeconds
    [int]$ElapsedSeconds

    TimeoutException([string]$message, [string]$operation, [int]$timeoutSeconds) : base($message) {
        $this.Category = 'Timeout'
        $this.Operation = $operation
        $this.TimeoutSeconds = $timeoutSeconds
        $this.ElapsedSeconds = $timeoutSeconds
    }

    [string] ToLogString() {
        return "[{0}] [Timeout] Operation={1} Timeout={2}s: {3}" -f `
            $this.Timestamp.ToString('o'), $this.Operation, $this.TimeoutSeconds, $this.Message
    }
}

class ValidationException : Win11ForgeException {
    [string]$ParameterName
    [string]$ProvidedValue
    [string]$ExpectedFormat

    ValidationException([string]$message, [string]$parameterName) : base($message) {
        $this.Category = 'Validation'
        $this.ParameterName = $parameterName
        $this.ProvidedValue = $null
        $this.ExpectedFormat = $null
    }

    ValidationException([string]$message, [string]$parameterName, [string]$providedValue, [string]$expectedFormat) : base($message) {
        $this.Category = 'Validation'
        $this.ParameterName = $parameterName
        $this.ProvidedValue = $providedValue
        $this.ExpectedFormat = $expectedFormat
    }
}

function New-TimeoutException {
    <#
    .SYNOPSIS
        Creates a new TimeoutException.
    #>
    [CmdletBinding()]
    [OutputType([TimeoutException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter(Mandatory)]
        [string]$Operation,

        [Parameter(Mandatory)]
        [int]$TimeoutSeconds
    )

    return [TimeoutException]::new($Message, $Operation, $TimeoutSeconds)
}

function New-ValidationException {
    <#
    .SYNOPSIS
        Creates a new ValidationException.
    #>
    [CmdletBinding()]
    [OutputType([ValidationException])]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter(Mandatory)]
        [string]$ParameterName,

        [string]$ProvidedValue,

        [string]$ExpectedFormat
    )

    if ($ProvidedValue -and $ExpectedFormat) {
        return [ValidationException]::new($Message, $ParameterName, $ProvidedValue, $ExpectedFormat)
    }
    return [ValidationException]::new($Message, $ParameterName)
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'New-InstallationException',
    'New-WingetException',
    'New-SecurityException',
    'New-ApiException',
    'New-TimeoutException',
    'New-ValidationException',
    'New-Win11ForgeError',
    'Format-Win11ForgeError',
    'Test-Win11ForgeException',
    'Get-ExceptionCategory'
)
