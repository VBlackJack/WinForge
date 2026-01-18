<#
.SYNOPSIS
    Win11Forge - Custom Exception Classes v3.1.4

.DESCRIPTION
    Defines custom exception classes for semantic error handling
    across the Win11Forge framework. Enables precise error
    categorization and handling.

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

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'New-InstallationException',
    'New-WingetException',
    'New-SecurityException',
    'New-ApiException',
    'Test-Win11ForgeException',
    'Get-ExceptionCategory'
)
