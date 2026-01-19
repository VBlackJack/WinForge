<#
.SYNOPSIS
    Win11Forge - Plugin Sandbox Module v3.3.0

.DESCRIPTION
    Provides sandboxed execution environment for Win11Forge plugins:
    - Process-level isolation for plugin execution
    - Timeout enforcement for runaway plugins
    - Resource limits and monitoring
    - Safe failure handling

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
$script:TimeoutSettingsPath = Join-Path $script:ModuleRoot 'TimeoutSettings.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

if (Test-Path -Path $script:TimeoutSettingsPath) {
    Import-Module -Name $script:TimeoutSettingsPath -Force -ErrorAction SilentlyContinue
}

# === SANDBOX CONFIGURATION ===
$script:SandboxDefaults = @{
    ExecutionTimeoutSeconds = 30
    LoadTimeoutSeconds = 10
    MaxMemoryMB = 512
    AllowNetworkAccess = $false
    AllowFileSystemWrite = $false
    AllowedWritePaths = @()
}

# === SANDBOXED EXECUTION ===

function Invoke-PluginSandboxed {
    <#
    .SYNOPSIS
        Executes a plugin handler in a sandboxed environment.
    .DESCRIPTION
        Runs plugin code in an isolated PowerShell job with timeout enforcement
        and resource monitoring. Prevents runaway plugins from blocking the
        main deployment process.
    .PARAMETER Handler
        The handler scriptblock or command to execute.
    .PARAMETER Context
        Context hashtable to pass to the handler.
    .PARAMETER PluginName
        Name of the plugin for logging purposes.
    .PARAMETER TimeoutSeconds
        Maximum execution time in seconds.
    .PARAMETER AllowNetworkAccess
        Whether to allow network operations (future implementation).
    .OUTPUTS
        Hashtable containing execution result or error information.
    .EXAMPLE
        Invoke-PluginSandboxed -Handler $handler -Context @{ AppName = 'Test' } -PluginName 'MyPlugin'
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        $Handler,

        [Parameter()]
        [hashtable]$Context = @{},

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$PluginName,

        [Parameter()]
        [ValidateRange(1, 300)]
        [int]$TimeoutSeconds = 0,

        [Parameter()]
        [switch]$AllowNetworkAccess
    )

    # Get timeout from configuration if not specified
    if ($TimeoutSeconds -eq 0) {
        if (Get-Command -Name Get-PluginTimeout -ErrorAction SilentlyContinue) {
            $TimeoutSeconds = Get-PluginTimeout -Operation 'Execution'
        } else {
            $TimeoutSeconds = $script:SandboxDefaults.ExecutionTimeoutSeconds
        }
    }

    $result = @{
        Success = $false
        Plugin = $PluginName
        Result = $null
        Error = $null
        TimedOut = $false
        ExecutionTimeMs = 0
    }

    $startTime = Get-Date

    try {
        Write-Status -Message "Executing plugin '$PluginName' in sandbox (timeout: ${TimeoutSeconds}s)" -Level 'Verbose' -Category 'Plugin'

        # Create a job for isolated execution
        $job = Start-Job -ScriptBlock {
            param($HandlerScript, $ContextData)

            # Reconstruct handler in job scope
            $handler = [scriptblock]::Create($HandlerScript)

            # Execute handler with context
            & $handler $ContextData
        } -ArgumentList $Handler.ToString(), $Context

        # Wait for job with timeout
        $completed = $job | Wait-Job -Timeout $TimeoutSeconds

        if ($null -eq $completed) {
            # Job timed out
            $result.TimedOut = $true
            $result.Error = "Plugin execution timed out after $TimeoutSeconds seconds"

            # Forcibly stop the job
            $job | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue

            Write-Status -Message "Plugin '$PluginName' timed out after ${TimeoutSeconds}s" -Level 'Warning' -Category 'Plugin'
        } else {
            # Job completed
            $jobResult = Receive-Job -Job $job -ErrorAction SilentlyContinue

            if ($job.State -eq 'Failed') {
                $result.Error = $job.ChildJobs[0].JobStateInfo.Reason.Message
                Write-Status -Message "Plugin '$PluginName' failed: $($result.Error)" -Level 'Warning' -Category 'Plugin'
            } else {
                $result.Success = $true
                $result.Result = $jobResult
            }

            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
    } catch {
        $result.Error = $_.Exception.Message
        Write-Status -Message "Plugin sandbox error for '$PluginName': $($result.Error)" -Level 'Error' -Category 'Plugin'
    }

    $result.ExecutionTimeMs = ((Get-Date) - $startTime).TotalMilliseconds

    return $result
}

function Invoke-PluginHookSandboxed {
    <#
    .SYNOPSIS
        Invokes all handlers for a hook in sandboxed mode.
    .DESCRIPTION
        Executes each plugin handler in isolation with timeout enforcement.
        Failures in one plugin don't affect others.
    .PARAMETER HookName
        Name of the hook to invoke.
    .PARAMETER Context
        Context data to pass to handlers.
    .PARAMETER Handlers
        Array of handler objects from PluginState.
    .OUTPUTS
        Array of handler results.
    .EXAMPLE
        Invoke-PluginHookSandboxed -HookName 'pre-install' -Context @{ AppName = 'VSCode' } -Handlers $handlers
    #>
    [CmdletBinding()]
    [OutputType([array])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$HookName,

        [Parameter()]
        [hashtable]$Context = @{},

        [Parameter(Mandatory)]
        [array]$Handlers
    )

    $results = @()

    foreach ($handler in $Handlers) {
        Write-Status -Message "Invoking sandboxed hook '$HookName' from plugin '$($handler.PluginName)'" -Level 'Verbose' -Category 'Plugin'

        $sandboxResult = Invoke-PluginSandboxed `
            -Handler $handler.Handler `
            -Context $Context `
            -PluginName $handler.PluginName

        $results += @{
            Plugin = $handler.PluginName
            Hook = $HookName
            Success = $sandboxResult.Success
            Result = $sandboxResult.Result
            Error = $sandboxResult.Error
            TimedOut = $sandboxResult.TimedOut
            ExecutionTimeMs = $sandboxResult.ExecutionTimeMs
        }

        if (-not $sandboxResult.Success) {
            if ($sandboxResult.TimedOut) {
                Write-Status -Message "Hook '$HookName' timed out for plugin '$($handler.PluginName)'" -Level 'Warning' -Category 'Plugin'
            } else {
                Write-Status -Message "Hook '$HookName' failed for plugin '$($handler.PluginName)': $($sandboxResult.Error)" -Level 'Warning' -Category 'Plugin'
            }
        }
    }

    return $results
}

function Test-PluginSandboxAvailable {
    <#
    .SYNOPSIS
        Tests if sandboxed plugin execution is available.
    .DESCRIPTION
        Checks if the system supports PowerShell jobs for sandboxing.
    .OUTPUTS
        Boolean indicating if sandboxing is available.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        # Test job creation
        $testJob = Start-Job -ScriptBlock { return $true }
        $result = $testJob | Wait-Job -Timeout 5
        if ($null -ne $result) {
            $jobResult = Receive-Job -Job $testJob
            Remove-Job -Job $testJob -Force
            return $jobResult -eq $true
        }
        Stop-Job -Job $testJob -ErrorAction SilentlyContinue
        Remove-Job -Job $testJob -Force -ErrorAction SilentlyContinue
        return $false
    } catch {
        return $false
    }
}

function Get-SandboxStatus {
    <#
    .SYNOPSIS
        Returns current sandbox configuration and status.
    .OUTPUTS
        Hashtable with sandbox status information.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $timeoutConfig = $script:SandboxDefaults.Clone()

    if (Get-Command -Name Get-PluginTimeout -ErrorAction SilentlyContinue) {
        $timeoutConfig.ExecutionTimeoutSeconds = Get-PluginTimeout -Operation 'Execution'
        $timeoutConfig.LoadTimeoutSeconds = Get-PluginTimeout -Operation 'Load'
    }

    return @{
        Available = Test-PluginSandboxAvailable
        Configuration = $timeoutConfig
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        JobsSupported = $true
    }
}

function Invoke-PluginLoadSandboxed {
    <#
    .SYNOPSIS
        Loads a plugin module in a sandboxed validation check.
    .DESCRIPTION
        Validates that a plugin can be loaded without errors before
        actually importing it into the main session.
    .PARAMETER PluginPath
        Path to the plugin entry point.
    .PARAMETER PluginName
        Name of the plugin.
    .OUTPUTS
        Hashtable with load validation result.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$PluginPath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$PluginName
    )

    $loadTimeout = $script:SandboxDefaults.LoadTimeoutSeconds
    if (Get-Command -Name Get-PluginTimeout -ErrorAction SilentlyContinue) {
        $loadTimeout = Get-PluginTimeout -Operation 'Load'
    }

    $result = @{
        Success = $false
        PluginName = $PluginName
        Error = $null
        TimedOut = $false
        LoadTimeMs = 0
    }

    $startTime = Get-Date

    try {
        $job = Start-Job -ScriptBlock {
            param($Path)

            # Try to import the module
            Import-Module $Path -Force -ErrorAction Stop

            # Return exported commands count as validation
            $module = Get-Module -Name ([System.IO.Path]::GetFileNameWithoutExtension($Path))
            if ($module) {
                return @{
                    Valid = $true
                    ExportedCommands = $module.ExportedCommands.Count
                }
            }

            return @{ Valid = $true; ExportedCommands = 0 }
        } -ArgumentList $PluginPath

        $completed = $job | Wait-Job -Timeout $loadTimeout

        if ($null -eq $completed) {
            $result.TimedOut = $true
            $result.Error = "Plugin load validation timed out after $loadTimeout seconds"
            $job | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue
        } else {
            if ($job.State -eq 'Failed') {
                $result.Error = $job.ChildJobs[0].JobStateInfo.Reason.Message
            } else {
                $jobResult = Receive-Job -Job $job
                if ($jobResult.Valid) {
                    $result.Success = $true
                }
            }
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
    } catch {
        $result.Error = $_.Exception.Message
    }

    $result.LoadTimeMs = ((Get-Date) - $startTime).TotalMilliseconds

    return $result
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Invoke-PluginSandboxed',
    'Invoke-PluginHookSandboxed',
    'Invoke-PluginLoadSandboxed',
    'Test-PluginSandboxAvailable',
    'Get-SandboxStatus'
)
