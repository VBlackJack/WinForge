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

Export-ModuleMember -Function Test-DetectionArgumentDangerous, ConvertTo-DetectionArgumentArray
