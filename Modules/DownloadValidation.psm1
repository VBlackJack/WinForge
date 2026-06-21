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

# Explicit opt-out token. When it appears in the checksum field it means
# "this field is not enforced", not "opt out of every control".
$script:SkipValidationToken = 'SKIP_VALIDATION'

function Resolve-DirectDownloadValidationMode {
    <#
    .SYNOPSIS
        Resolves the integrity-validation policy for a direct-download installer.

    .DESCRIPTION
        Single source of the fail-closed validation decision, shared by the
        sequential (Install-ViaDirectDownload) and parallel installation paths so
        the two cannot drift. The function decides policy only; the post-download
        mechanics still apply a publisher signature and/or a checksum independently
        based on which fields are present.

        Returns one of:
          'Validated'      - at least one enforceable control is present (a real
                             publisher, or a real SHA256 hash). A real publisher is
                             never downgraded by a SKIP_VALIDATION token in the
                             checksum field: the token only marks that field as not
                             enforced, so publisher + SKIP_VALIDATION resolves to
                             'Validated' (the publisher still applies).
          'SkipValidation' - no enforceable control, but an explicit SKIP_VALIDATION
                             opt-out is present.
          'None'           - no control and no opt-out; the caller must refuse before
                             downloading anything.

    .PARAMETER ExpectedSHA256
        The expected SHA256 checksum, or the SKIP_VALIDATION opt-out token, or empty.

    .PARAMETER ExpectedPublisher
        The expected Authenticode publisher substring, or empty.

    .OUTPUTS
        [string] One of 'Validated', 'SkipValidation', 'None'.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$ExpectedSHA256,
        [string]$ExpectedPublisher
    )

    $hasPublisher = -not [string]::IsNullOrWhiteSpace($ExpectedPublisher)
    $hasRealChecksum = $ExpectedSHA256 -and ($ExpectedSHA256 -ne $script:SkipValidationToken)
    $hasSkipToken = ($ExpectedSHA256 -eq $script:SkipValidationToken)

    if ($hasPublisher -or $hasRealChecksum) {
        return 'Validated'
    }
    if ($hasSkipToken) {
        return 'SkipValidation'
    }
    return 'None'
}

function Get-ExpectedChecksum {
    <#
    .SYNOPSIS
        Returns the canonical expected SHA256 checksum from a Sources object.

    .DESCRIPTION
        Single source of the checksum accessor shared by the sequential and parallel
        installation paths. Only the canonical ExpectedSHA256 field is honoured: the
        Sources schema is additionalProperties:false and defines no other checksum
        field, so there is no legacy fallback to read.

    .PARAMETER Sources
        The application Sources object.

    .OUTPUTS
        [string] The ExpectedSHA256 value (which may be the SKIP_VALIDATION token), or
        $null when no checksum field is present.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [PSCustomObject]$Sources
    )

    if ($null -ne $Sources -and $Sources.PSObject.Properties['ExpectedSHA256'] -and $Sources.ExpectedSHA256) {
        return $Sources.ExpectedSHA256
    }
    return $null
}

function Assert-FileChecksum {
    <#
    .SYNOPSIS
        Verifies that a file satisfies an expected SHA256 checksum requirement.

    .DESCRIPTION
        Single source of the post-download checksum enforcement shared by the sequential
        and parallel installation paths. The comparison is case-insensitive (Get-FileHash
        returns upper-case hex while catalogue checksums may be either case), matching the
        former inline '-ne' compares so the move is behaviour-preserving.

        Returns $true when there is nothing to enforce (an empty value or the
        SKIP_VALIDATION opt-out token) so callers can gate unconditionally; otherwise
        returns whether the file's SHA256 matches the expected value.

    .PARAMETER Path
        Path to the downloaded file.

    .PARAMETER ExpectedSHA256
        The expected checksum, the SKIP_VALIDATION token, or empty.

    .OUTPUTS
        [bool] $true if the requirement is satisfied; $false on a real mismatch.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [string]$ExpectedSHA256
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedSHA256) -or $ExpectedSHA256 -eq $script:SkipValidationToken) {
        return $true
    }

    $actual = (Get-FileHash -Path $Path -Algorithm SHA256).Hash
    return $actual -eq $ExpectedSHA256
}

function Test-DirectDownloadChecksumGate {
    <#
    .SYNOPSIS
        Returns the post-download checksum gate verdict for a direct-download installer.

    .DESCRIPTION
        Wraps Assert-FileChecksum with the metadata the parallel installer needs to
        preserve fail-closed cleanup and logging without duplicating the checksum policy.

        Empty checksums and the SKIP_VALIDATION opt-out token are not enforced and return
        Proceed = $true. Real checksums are enforced through Assert-FileChecksum, with the
        actual hash included for caller diagnostics.

    .PARAMETER Path
        Path to the downloaded file.

    .PARAMETER ExpectedSHA256
        The expected checksum, the SKIP_VALIDATION token, or empty.

    .OUTPUTS
        [PSCustomObject] with Enforced, Proceed, and ActualHash properties.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [string]$ExpectedSHA256
    )

    $isEnforced = -not [string]::IsNullOrWhiteSpace($ExpectedSHA256) -and $ExpectedSHA256 -ne $script:SkipValidationToken
    if (-not $isEnforced) {
        return [PSCustomObject]@{
            Enforced   = $false
            Proceed    = $true
            ActualHash = $null
        }
    }

    $proceed = Assert-FileChecksum -Path $Path -ExpectedSHA256 $ExpectedSHA256
    $actual = (Get-FileHash -Path $Path -Algorithm SHA256).Hash

    return [PSCustomObject]@{
        Enforced   = $true
        Proceed    = $proceed
        ActualHash = $actual
    }
}

Export-ModuleMember -Function Resolve-DirectDownloadValidationMode, Get-ExpectedChecksum, Assert-FileChecksum, Test-DirectDownloadChecksumGate
