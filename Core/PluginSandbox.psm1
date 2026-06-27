<#
.SYNOPSIS
    WinForge - Plugin Sandbox v3.7.2

.DESCRIPTION
    Provides sandboxed execution environment for WinForge plugins:
    - Process-level isolation for plugin execution
    - Timeout enforcement for runaway plugins
    - Resource limits and monitoring
    - Safe failure handling

.NOTES
    Author: Julien Bombled
    v3.7.2
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
$script:LocalizationPath = Join-Path $script:ModuleRoot 'Localization.psm1'
$script:TimeoutSettingsPath = Join-Path $script:ModuleRoot 'TimeoutSettings.psm1'
$script:PluginSandboxModulePath = $PSCommandPath

if (-not (Get-Command -Name Get-LogString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationPath) {
        Import-Module -Name $script:LocalizationPath -Force
    }
}

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

if (Test-Path -Path $script:TimeoutSettingsPath) {
    try {
        Import-Module -Name $script:TimeoutSettingsPath -Force -ErrorAction Stop
    } catch {
        Write-Warning (Get-LogString 'plugins.sandbox.timeout_module.load_failed' @{ Error = $_.Exception.Message })
    }
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

# === SECURITY: DANGEROUS COMMANDS LIST ===
$script:DangerousCommands = @(
    'Invoke-Expression', 'iex',
    'Add-Type',
    'New-Object',
    'Start-Process',
    'saps',
    '[System.Reflection',
    '[System.Runtime.InteropServices',
    'Set-ExecutionPolicy',
    'Remove-Item',
    'rm',
    'del',
    'erase',
    'rmdir',
    'rd',
    'Clear-Content',
    'Set-Content',
    'sc',
    'Out-File',
    '[scriptblock]::Create',
    'Invoke-Command',
    'Enter-PSSession',
    'New-PSSession',
    'Register-ScheduledTask',
    'Set-Service',
    'Stop-Service',
    'Restart-Service',
    'net.webclient',
    'downloadstring',
    'downloadfile',
    'Invoke-WebRequest',
    'iwr',
    'Invoke-RestMethod',
    'irm',
    'ConvertTo-SecureString',
    'Get-Credential'
)

# === SECURITY: PLUGIN COMMAND ALLOWLIST ===
$script:AllowedPluginCommands = @(
    'Compare-Object',
    'ConvertFrom-Json',
    'ConvertTo-Json',
    'Export-ModuleMember',
    'ForEach-Object',
    'Get-Date',
    'Get-LocalizedString',
    'Get-LogString',
    'Get-Random',
    'Join-Path',
    'Measure-Object',
    'Select-Object',
    'Set-StrictMode',
    'Sort-Object',
    'Split-Path',
    'Start-Sleep',
    'Test-Path',
    'Where-Object',
    'Write-Debug',
    'Write-Error',
    'Write-Information',
    'Write-Output',
    'Write-Progress',
    'Write-Section',
    'Write-Status',
    'Write-StatusProgress',
    'Write-Verbose',
    'Write-Warning'
)

$script:DangerousCommandSet = @{}
foreach ($dangerousCommand in $script:DangerousCommands) {
    $script:DangerousCommandSet[$dangerousCommand.ToLowerInvariant()] = $true
}

$script:AllowedPluginCommandSet = @{}
foreach ($allowedCommand in $script:AllowedPluginCommands) {
    $script:AllowedPluginCommandSet[$allowedCommand.ToLowerInvariant()] = $true
}

# === AST VALIDATION ===

function ConvertTo-PluginCommandName {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [AllowNull()]
        [string]$CommandName
    )

    if ([string]::IsNullOrWhiteSpace($CommandName)) {
        return ''
    }

    $normalized = $CommandName.Trim()
    if ($normalized.Contains('\')) {
        $normalized = ($normalized -split '\\')[-1]
    }

    return $normalized
}

function Test-PluginCommandAstSafe {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [System.Management.Automation.Language.CommandAst]$CommandAst,

        [Parameter()]
        [string[]]$DefinedFunctionNames = @()
    )

    $result = @{
        IsValid = $true
        Errors = @()
    }

    $commandName = $CommandAst.GetCommandName()
    if ([string]::IsNullOrWhiteSpace($commandName)) {
        $result.IsValid = $false
        $result.Errors += "Dynamic command invocation blocked: $($CommandAst.Extent.Text)"
        return $result
    }

    $normalizedCommandName = ConvertTo-PluginCommandName -CommandName $commandName
    $commandsToCheck = @($normalizedCommandName)

    try {
        $resolvedCommand = Get-Command -Name $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolvedCommand) {
            if ($resolvedCommand.CommandType -eq 'Application') {
                $result.IsValid = $false
                $result.Errors += "External command blocked: $commandName"
            }
            elseif ($resolvedCommand.CommandType -eq 'Alias' -and $resolvedCommand.Definition) {
                $commandsToCheck += (ConvertTo-PluginCommandName -CommandName $resolvedCommand.Definition)
            }
            elseif ($resolvedCommand.Name) {
                $commandsToCheck += (ConvertTo-PluginCommandName -CommandName $resolvedCommand.Name)
            }
        }
    } catch {
        $result.IsValid = $false
        $result.Errors += "Command resolution failed: $commandName"
    }

    if ($commandName -match '(^\.{0,2}[\\/]|[\\/]|\.((exe)|(cmd)|(bat)|(ps1)|(vbs)|(js))$)') {
        $result.IsValid = $false
        $result.Errors += "External command blocked: $commandName"
    }

    $hasDangerousCommand = $false
    foreach ($nameToCheck in ($commandsToCheck | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
        if ($script:DangerousCommandSet.ContainsKey($nameToCheck.ToLowerInvariant())) {
            $hasDangerousCommand = $true
            $result.IsValid = $false
            $result.Errors += (Get-LogString 'plugins.sandbox.validation.dangerous_command' @{ Command = $commandName })
        }
    }

    if (-not $hasDangerousCommand) {
        $definedFunctionSet = @{}
        foreach ($functionName in $DefinedFunctionNames) {
            if (-not [string]::IsNullOrWhiteSpace($functionName)) {
                $definedFunctionSet[(ConvertTo-PluginCommandName -CommandName $functionName).ToLowerInvariant()] = $true
            }
        }

        $isAllowed = $false
        foreach ($nameToCheck in ($commandsToCheck | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
            $lookup = $nameToCheck.ToLowerInvariant()
            if ($script:AllowedPluginCommandSet.ContainsKey($lookup) -or $definedFunctionSet.ContainsKey($lookup)) {
                $isAllowed = $true
                break
            }
        }

        if (-not $isAllowed) {
            $result.IsValid = $false
            $result.Errors += "Command not allowed by plugin sandbox allowlist: $commandName"
        }
    }

    return $result
}

function Test-ParsedPluginAstSafe {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [System.Management.Automation.Language.Ast]$Ast
    )

    $result = @{
        IsValid = $true
        Errors = @()
    }

    $definedFunctionNames = @(
        $Ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
        }, $true) | ForEach-Object { $_.Name }
    )

    $commandAsts = $Ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.CommandAst]
    }, $true)

    foreach ($cmdAst in $commandAsts) {
        $commandResult = Test-PluginCommandAstSafe -CommandAst $cmdAst -DefinedFunctionNames $definedFunctionNames
        if (-not $commandResult.IsValid) {
            $result.IsValid = $false
            $result.Errors += $commandResult.Errors
        }
    }

    $typeAsts = $Ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.TypeExpressionAst] -or
        $node -is [System.Management.Automation.Language.TypeConstraintAst]
    }, $true)

    foreach ($typeAst in $typeAsts) {
        $typeName = $typeAst.TypeName.FullName
        if ($typeName -match 'System\.Reflection|System\.Runtime\.InteropServices|System\.Net\.WebClient') {
            $result.IsValid = $false
            $result.Errors += (Get-LogString 'plugins.sandbox.validation.dangerous_type' @{ TypeName = $typeName })
        }
    }

    $memberAsts = $Ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -or
        $node -is [System.Management.Automation.Language.MemberExpressionAst]
    }, $true)

    foreach ($memberAst in $memberAsts) {
        $memberName = $memberAst.Member.Value
        if ($memberName -in @('Create', 'DownloadString', 'DownloadFile', 'Load', 'LoadFile', 'Invoke')) {
            $expressionText = $memberAst.Extent.Text
            if ($expressionText -match 'scriptblock|webclient|assembly|reflection') {
                $result.IsValid = $false
                $result.Errors += (Get-LogString 'plugins.sandbox.validation.dangerous_member' @{ Expression = $expressionText })
            }
        }
    }

    return $result
}

function Test-ScriptblockSafe {
    <#
    .SYNOPSIS
        Validates a scriptblock string using AST analysis.
    .DESCRIPTION
        Parses the scriptblock and checks for dangerous commands or patterns
        that could compromise system security.
    .PARAMETER ScriptText
        The scriptblock text to validate.
    .OUTPUTS
        [hashtable] With IsValid and Errors properties.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$ScriptText
    )

    $result = @{
        IsValid = $true
        Errors = @()
    }

    try {
        # Parse the script using AST
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseInput(
            $ScriptText,
            [ref]$tokens,
            [ref]$errors
        )

        # Check for parse errors
        if ($errors.Count -gt 0) {
            $result.IsValid = $false
            $result.Errors += (Get-LogString 'plugins.sandbox.validation.parse_errors' @{ Error = $errors[0].Message })
            return $result
        }

        $astValidation = Test-ParsedPluginAstSafe -Ast $ast
        if (-not $astValidation.IsValid) {
            $result.IsValid = $false
            $result.Errors += $astValidation.Errors
        }

    } catch {
        $result.IsValid = $false
        $result.Errors += (Get-LogString 'plugins.sandbox.validation.ast_error' @{ Error = $_.Exception.Message })
    }

    return $result
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

        # Security: Validate the actual handler body rather than a FunctionInfo
        # display name, then pass only text into the isolated job.
        if ($Handler -is [scriptblock]) {
            $handlerText = $Handler.ToString()
        } elseif ($Handler -is [System.Management.Automation.CommandInfo] -and $null -ne $Handler.ScriptBlock) {
            $handlerText = $Handler.ScriptBlock.ToString()
        } elseif ($null -ne $Handler.PSObject.Properties['ScriptBlock'] -and $null -ne $Handler.ScriptBlock) {
            $handlerText = $Handler.ScriptBlock.ToString()
        } else {
            $handlerText = [string]$Handler
        }

        $validation = Test-ScriptblockSafe -ScriptText $handlerText
        if (-not $validation.IsValid) {
            $result.Error = (Get-LogString 'plugins.sandbox.security.handler_blocked' @{ Errors = ($validation.Errors -join '; ') })
            Write-Status -Message (Get-LogString 'plugins.sandbox.security.plugin_blocked' @{ Name = $PluginName; Error = $result.Error }) -Level 'Error' -Category 'Plugin'
            $result.ExecutionTimeMs = ((Get-Date) - $startTime).TotalMilliseconds
            return $result
        }

        # Create a job for isolated execution
        # Security: Import this module inside the job and re-use the same AST
        # validator there to prevent TOCTOU drift between parent and job scope.
        $sandboxModulePath = $script:PluginSandboxModulePath
        $job = Start-Job -ScriptBlock {
            param($HandlerText, $ContextData, $SandboxModulePath)

            # Security: Re-validate handler inside job scope before execution (TOCTOU prevention)
            Import-Module -Name $SandboxModulePath -Force -ErrorAction Stop
            $validation = Test-ScriptblockSafe -ScriptText $HandlerText
            if (-not $validation.IsValid) {
                throw "Security: Handler blocked in job context: $($validation.Errors -join '; ')"
            }

            # Create handler before switching to constrained language
            $handler = [scriptblock]::Create($HandlerText)

            # Security: Enforce Constrained Language Mode to prevent dangerous type usage
            $ExecutionContext.SessionState.LanguageMode = 'ConstrainedLanguage'

            # Execute handler with context
            & $handler $ContextData
        } -ArgumentList $handlerText, $Context, $sandboxModulePath

        # Wait for job with timeout
        $completed = $job | Wait-Job -Timeout $TimeoutSeconds

        if ($null -eq $completed) {
            # Job timed out
            $result.TimedOut = $true
            $result.Error = (Get-LogString 'plugins.sandbox.execution.timed_out' @{ Timeout = $TimeoutSeconds })

            # Forcibly stop the job
            $job | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue

            Write-Status -Message "Plugin '$PluginName' timed out after ${TimeoutSeconds}s" -Level 'Warning' -Category 'Plugin'
        } else {
            # Job completed - receive output
            # Safety: Use -Wait to ensure all output is received, with -ErrorAction
            # to prevent exceptions if output stream has issues
            try {
                # Receive-Job should be instant for completed jobs
                # Add -Wait to ensure all streams are fully read
                $jobResult = Receive-Job -Job $job -Wait -ErrorAction Stop

                if ($job.State -eq 'Failed') {
                    $result.Error = $job.ChildJobs[0].JobStateInfo.Reason.Message
                    Write-Status -Message (Get-LogString 'plugins.sandbox.execution.failed' @{ Name = $PluginName; Error = $result.Error }) -Level 'Warning' -Category 'Plugin'
                } else {
                    $result.Success = $true
                    $result.Result = $jobResult
                }
            } catch {
                $result.Error = (Get-LogString 'plugins.sandbox.execution.output_error' @{ Error = $_.Exception.Message })
                Write-Status -Message (Get-LogString 'plugins.sandbox.execution.plugin_output_error' @{ Name = $PluginName; Error = $result.Error }) -Level 'Warning' -Category 'Plugin'
            } finally {
                # Always clean up job
                Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
            }
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
        Write-Status -Message (Get-LogString 'plugins.sandbox.hook.invoking' @{ Hook = $HookName; Name = $handler.PluginName }) -Level 'Verbose' -Category 'Plugin'

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
                Write-Status -Message (Get-LogString 'plugins.sandbox.hook.timed_out' @{ Hook = $HookName; Name = $handler.PluginName }) -Level 'Warning' -Category 'Plugin'
            } else {
                Write-Status -Message (Get-LogString 'plugins.sandbox.hook.failed' @{ Hook = $HookName; Name = $handler.PluginName; Error = $sandboxResult.Error }) -Level 'Warning' -Category 'Plugin'
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

    .DESCRIPTION
        Retrieves the current sandbox availability, timeout configuration, PowerShell
        version, and job support status. Merges defaults with any overrides from the
        timeout settings module when available.

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
        # Security: Read and validate the full module file content via AST before loading
        $moduleContent = Get-Content -Path $PluginPath -Raw -ErrorAction Stop
        $preValidation = Test-ScriptblockSafe -ScriptText $moduleContent
        if (-not $preValidation.IsValid) {
            $result.Error = (Get-LogString 'plugins.sandbox.security.module_blocked' @{ Errors = ($preValidation.Errors -join '; ') })
            Write-Status -Message (Get-LogString 'plugins.sandbox.security.load_blocked' @{ Name = $PluginName; Error = $result.Error }) -Level 'Error' -Category 'Plugin'
            $result.LoadTimeMs = ((Get-Date) - $startTime).TotalMilliseconds
            return $result
        }

        $sandboxModulePath = $script:PluginSandboxModulePath
        $moduleName = [System.IO.Path]::GetFileNameWithoutExtension($PluginPath)
        $job = Start-Job -ScriptBlock {
            param($Path, $ModuleContent, $ModuleName, $SandboxModulePath)

            # Security: Re-validate the module content AST inside job scope (TOCTOU prevention)
            Import-Module -Name $SandboxModulePath -Force -ErrorAction Stop
            $validation = Test-ScriptblockSafe -ScriptText $ModuleContent
            if (-not $validation.IsValid) {
                throw "Security: Module blocked in job context: $($validation.Errors -join '; ')"
            }

            # Security: Enforce Constrained Language Mode for module execution.
            $ExecutionContext.SessionState.LanguageMode = 'ConstrainedLanguage'

            # Try to import the module
            Import-Module $Path -Force -ErrorAction Stop

            # Return exported commands count as validation
            $module = Get-Module -Name $ModuleName
            if ($module) {
                return @{
                    Valid = $true
                    ExportedCommands = $module.ExportedCommands.Count
                }
            }

            return @{ Valid = $true; ExportedCommands = 0 }
        } -ArgumentList $PluginPath, $moduleContent, $moduleName, $sandboxModulePath

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
    'Get-SandboxStatus',
    'Test-ScriptblockSafe'
)
