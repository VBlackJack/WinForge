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

Export-ModuleMember -Function Resolve-DirectDownloadValidationMode
