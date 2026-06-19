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

# Security: Whitelist of allowed registry path patterns
# Only registry paths matching these patterns are allowed for detection
$script:AllowedRegistryPatterns = @(
    '^HK(LM|CU):\\SOFTWARE(\\|$)',                        # Standard software keys (with or without trailing path)
    '^HK(LM|CU):\\SOFTWARE\\WOW6432Node(\\|$)',           # 32-bit software keys
    '^HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)',  # Uninstall keys
    '^HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)',
    '^HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall(\\|$)'
)

# Security: Blocked registry paths (sensitive hives)
$script:BlockedRegistryPatterns = @(
    '\\SAM\\',           # Security Account Manager
    '\\SECURITY\\',      # Security settings
    '\\SYSTEM\\',        # System configuration (except specific subkeys)
    '\\\.DEFAULT\\',     # Default user profile
    'RunOnce',           # Startup entries (potential persistence)
    'Run$'               # Startup entries
)

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
