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

# Import DetectionAllowlist here rather than relying on the caller having imported it.
# A function defined in this module resolves commands against this module's scope and the
# global scope only: when a parallel runspace imports this module indirectly (through
# ParallelDetection), the allowlist module lands in that module's scope, not here, and the
# lookup below would fail closed and silently report every application as not installed.
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:DetectionAllowlistModulePath = Join-Path $script:ModuleRoot 'DetectionAllowlist.psm1'
if (-not (Get-Command -Name Get-DetectionArgumentAllowlist -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DetectionAllowlistModulePath) {
        Import-Module -Name $script:DetectionAllowlistModulePath -Force
    }
}

function Test-DetectionArgumentDangerous {
    <#
    .SYNOPSIS
        Returns whether command-detection arguments contain shell-injection patterns.

    .DESCRIPTION
        Single source of the argument-sanitization rule shared by every Command
        detection path (the sequential gold standard and the parallel runspace), so
        the guard cannot drift between them. Blocks shell metacharacters, command
        substitution, redirection, and control characters (including newlines), which
        could turn a detection probe into command injection.

    .PARAMETER Arguments
        The argument string parsed from a Detection.Command entry.

    .OUTPUTS
        [bool] $true when the arguments are dangerous and must not be executed;
        $false for empty arguments or a safe argument string.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [string]$Arguments
    )

    if ([string]::IsNullOrEmpty($Arguments)) {
        return $false
    }

    return [bool]($Arguments -match '[;&|`$\(\)\r\n]|>>|<<|[\x00-\x1f]')
}

function ConvertTo-DetectionArgumentArray {
    <#
    .SYNOPSIS
        Splits a command-detection argument string into an argument array.

    .DESCRIPTION
        Single source of the argument-splitting rule used by every Command detection
        path. Returning an array lets callers splat with @args / -ArgumentList so the
        shell never re-interprets the arguments (each token is a distinct argument).

    .PARAMETER Arguments
        The argument string parsed from a Detection.Command entry.

    .OUTPUTS
        [string[]] The arguments split on whitespace, or an empty array for empty input.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [string]$Arguments
    )

    # Callers wrap the result in @(...) so a single argument is treated as a one-element
    # array (PowerShell unwraps single-element arrays returned from a function).
    if ([string]::IsNullOrEmpty($Arguments)) {
        return @()
    }

    return @($Arguments -split '\s+' | Where-Object { $_ -ne '' })
}

function Test-DetectionArgumentAllowed {
    <#
    .SYNOPSIS
        Returns whether command-detection arguments are permitted to run.
    .DESCRIPTION
        Rejecting shell metacharacters is necessary but not sufficient. The executable
        allowlist permits interpreters (python, node, pwsh, ruby, perl, php) that accept
        code as an argument, and 'pwsh -Command Start-Process calc' contains no
        metacharacter at all. Detection only ever needs to ask a program for its version,
        so the argument side is an allowlist as well, configured in
        Config/detection-allowlist.json and shared with the GUI.

        An empty argument string is allowed: running the bare executable is how several
        programs report their presence.
    .PARAMETER Arguments
        The argument string parsed from a Detection.Command entry.
    .PARAMETER AllowedArguments
        The configured allowlist. Defaults to the shared configuration.
    .OUTPUTS
        [bool] $true when the arguments may be executed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [string]$Arguments,

        [Parameter()]
        [AllowNull()]
        [string[]]$AllowedArguments
    )

    if ([string]::IsNullOrWhiteSpace($Arguments)) {
        return $true
    }

    if (Test-DetectionArgumentDangerous -Arguments $Arguments) {
        return $false
    }

    if ($null -eq $AllowedArguments) {
        if (Get-Command -Name Get-DetectionArgumentAllowlist -ErrorAction SilentlyContinue) {
            $AllowedArguments = @(Get-DetectionArgumentAllowlist)
        } else {
            # Fail closed: without the allowlist there is no way to tell a version probe
            # from an interpreter invocation.
            return $false
        }
    }

    return ($Arguments.Trim() -in $AllowedArguments)
}

Export-ModuleMember -Function Test-DetectionArgumentDangerous, ConvertTo-DetectionArgumentArray, Test-DetectionArgumentAllowed
