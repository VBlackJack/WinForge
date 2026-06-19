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

# Registry-path validation policy, loaded from the shared Config file so the GUI
# probe (C#) and this module read the same single source. Resolved self-relative
# (module dir -> repo root -> Config) for runspace robustness.
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:RegistryPolicyPath = Join-Path $script:RepositoryRoot 'Config\detection-registry-policy.json'
$script:RegistryPolicyLoaded = $false
$script:AllowedRegistryPatterns = @()
$script:BlockedRegistryPatterns = @()

function Write-RegistryPolicyFailure {
    param([Parameter(Mandatory)][string]$Message)

    # A missing or corrupt policy fails closed (empty allowlist -> deny every path).
    # Surface it loudly so it is diagnosable, mirroring the allowlist loader.
    if (Get-Command -Name Write-Status -ErrorAction SilentlyContinue) {
        Write-Status -Message $Message -Level 'Error'
    } else {
        Write-Warning $Message
    }
}

function Initialize-RegistryPolicy {
    if ($script:RegistryPolicyLoaded) {
        return
    }

    # Fail closed on the ALLOWLIST: any load failure yields an empty allowedPatterns,
    # so no path matches and every registry path is denied. Never fail open on an
    # empty blocklist.
    $allowed = @()
    $blocked = @()
    try {
        if (-not (Test-Path -Path $script:RegistryPolicyPath)) {
            Write-RegistryPolicyFailure "Detection registry policy not found at '$script:RegistryPolicyPath'; registry detection is disabled (fail-closed)."
        } else {
            $data = Get-Content -Path $script:RegistryPolicyPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($data.PSObject.Properties['allowedPatterns'] -and $data.allowedPatterns) {
                $allowed = @($data.allowedPatterns | Where-Object { $_ })
            } else {
                Write-RegistryPolicyFailure "Detection registry policy '$script:RegistryPolicyPath' has no 'allowedPatterns'; registry detection is disabled (fail-closed)."
            }
            if ($data.PSObject.Properties['blockedPatterns'] -and $data.blockedPatterns) {
                $blocked = @($data.blockedPatterns | Where-Object { $_ })
            }
        }
    } catch {
        Write-RegistryPolicyFailure "Failed to load detection registry policy from '$script:RegistryPolicyPath': $($_.Exception.Message); registry detection is disabled (fail-closed)."
        $allowed = @()
        $blocked = @()
    }

    $script:AllowedRegistryPatterns = $allowed
    $script:BlockedRegistryPatterns = $blocked
    $script:RegistryPolicyLoaded = $true
}

function Test-RegistryPathAllowed {
    <#
    .SYNOPSIS
        Validates that a registry path is allowed for detection.

    .DESCRIPTION
        Single source of the registry-path validation rule shared by every detection
        path (the sequential gold standard and the parallel runspace), so the hive
        allowlist and the sensitive-hive blocklist cannot drift. Returns $false for
        sensitive or disallowed paths.

    .PARAMETER Path
        The registry path to validate.

    .OUTPUTS
        Boolean indicating if the path is allowed.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Initialize-RegistryPolicy

    # Normalize path separators
    $normalizedPath = $Path -replace '/', '\'

    # Check against blocked patterns first
    foreach ($blocked in $script:BlockedRegistryPatterns) {
        if ($normalizedPath -match $blocked) {
            Write-Verbose "Registry path blocked (sensitive): $Path"
            return $false
        }
    }

    # Check path length (prevent DoS via deep paths)
    if ($normalizedPath.Length -gt 512) {
        Write-Verbose "Registry path too long: $($normalizedPath.Length) chars"
        return $false
    }

    # Check against allowed patterns
    foreach ($allowed in $script:AllowedRegistryPatterns) {
        if ($normalizedPath -match $allowed) {
            return $true
        }
    }

    Write-Verbose "Registry path not in whitelist: $Path"
    return $false
}

Export-ModuleMember -Function Test-RegistryPathAllowed
