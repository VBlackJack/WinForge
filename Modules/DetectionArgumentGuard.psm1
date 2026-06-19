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

Export-ModuleMember -Function Test-DetectionArgumentDangerous
