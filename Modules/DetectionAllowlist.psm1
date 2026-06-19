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

# Resolve the shared allowlist relative to this module so it loads identically in
# the parent session and inside a parallel runspace (Import-Module by path), with
# no transitive dependency that could silently fail to load in a job.
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:DetectionAllowlistPath = Join-Path $script:RepositoryRoot 'Config\detection-allowlist.json'
$script:DetectionAllowlistCache = @()
$script:DetectionAllowlistLoaded = $false

function Write-DetectionAllowlistFailure {
    param([Parameter(Mandatory)][string]$Message)

    # A missing or corrupt allowlist fails closed (deny-all), which silently turns
    # installed apps into "not installed". Surface it loudly so it is diagnosable,
    # mirroring the GUI loader. Falls back to Write-Warning where Write-Status is
    # not imported (e.g. a bare runspace).
    if (Get-Command -Name Write-Status -ErrorAction SilentlyContinue) {
        Write-Status -Message $Message -Level 'Error'
    } else {
        Write-Warning $Message
    }
}

function Get-DetectionAllowlist {
    <#
    .SYNOPSIS
        Returns the shared allowlist of executables permitted for Command detection.

    .DESCRIPTION
        Single source of truth for the PowerShell detection paths, loaded once from
        Config/detection-allowlist.json and cached. It is the same file the GUI
        detection probe reads, so the C# and PowerShell stacks cannot drift.

        Fails closed: any load failure (missing file, malformed JSON, empty list)
        yields an empty allowlist, which denies every Command detection. This is the
        safe side for security, but it makes installed apps look "not installed", so
        the failure is logged loudly.

    .PARAMETER Force
        Reload from disk, bypassing the cache.

    .OUTPUTS
        [string[]] The allowed executable base names (empty on load failure).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [switch]$Force
    )

    if ($script:DetectionAllowlistLoaded -and -not $Force) {
        return $script:DetectionAllowlistCache
    }

    $allowlist = @()
    try {
        if (-not (Test-Path -Path $script:DetectionAllowlistPath)) {
            Write-DetectionAllowlistFailure "Detection allowlist not found at '$script:DetectionAllowlistPath'; Command detection is disabled (fail-closed)."
        } else {
            $data = Get-Content -Path $script:DetectionAllowlistPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($data.PSObject.Properties['allowedExecutables'] -and $data.allowedExecutables) {
                $allowlist = @($data.allowedExecutables | Where-Object { $_ })
            } else {
                Write-DetectionAllowlistFailure "Detection allowlist '$script:DetectionAllowlistPath' has no 'allowedExecutables' entries; Command detection is disabled (fail-closed)."
            }
        }
    } catch {
        Write-DetectionAllowlistFailure "Failed to load detection allowlist from '$script:DetectionAllowlistPath': $($_.Exception.Message); Command detection is disabled (fail-closed)."
        $allowlist = @()
    }

    $script:DetectionAllowlistCache = $allowlist
    $script:DetectionAllowlistLoaded = $true
    return $allowlist
}

Export-ModuleMember -Function Get-DetectionAllowlist
