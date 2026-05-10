<#
.SYNOPSIS
  Verifies all packages.lock.json files under a path are RID-neutral.

.DESCRIPTION
  Parses each lock file as JSON and inspects the keys of the
  "dependencies" object. Any composite key shaped like "<tfm>/<rid>"
  (e.g. "net10.0-windows7.0/win-x64") is rejected, because such a key
  causes `dotnet restore --locked-mode` to fail with NU1004 when the
  csproj declares no <RuntimeIdentifier>.

  On detection, writes a GitHub Actions ::error annotation pointing to
  the file and the offending key, then exits 1. On a clean tree, exits
  0 with a one-line summary.

  Designed to run as a CI guard step before any solution-level
  `dotnet restore --locked-mode` invocation. See PR #98 (commit
  ea7af4f) for the prior incident and PR #99 for the publish-time fix.

.PARAMETER Path
  Directory to scan recursively for packages.lock.json files. Relative
  paths are resolved against the current working directory. Defaults
  to 'GUI'.

.EXAMPLE
  pwsh -NoProfile -File .\Tools\Test-PackagesLockRidNeutrality.ps1

.EXAMPLE
  pwsh -NoProfile -File .\Tools\Test-PackagesLockRidNeutrality.ps1 -Path GUI

.NOTES
  Author: Julien Bombled
  License: Apache 2.0
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

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = 'GUI'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Host "::error::Path '$Path' does not exist."
    exit 1
}

$lockFiles = @(Get-ChildItem -Path $Path -Filter 'packages.lock.json' -Recurse -File)
if ($lockFiles.Count -eq 0) {
    Write-Host "No packages.lock.json files found under '$Path'."
    exit 0
}

$violations = New-Object System.Collections.Generic.List[object]

foreach ($file in $lockFiles) {
    try {
        $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    } catch {
        Write-Host "::error file=$($file.FullName)::Failed to parse as JSON: $($_.Exception.Message)"
        $violations.Add([pscustomobject]@{ File = $file.FullName; Key = '<parse error>' })
        continue
    }

    $dependencies = $json.PSObject.Properties['dependencies']
    if ($null -eq $dependencies) {
        continue
    }

    foreach ($targetKey in $dependencies.Value.PSObject.Properties.Name) {
        if ($targetKey -match '/') {
            $violations.Add([pscustomobject]@{ File = $file.FullName; Key = $targetKey })
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($v in $violations) {
        Write-Host "::error file=$($v.File)::RID-specific target key '$($v.Key)' detected. Lock files under '$Path' must be RID-neutral. Run 'git restore' on this file or rebuild it with a RID-neutral 'dotnet restore' (no --runtime)."
    }
    exit 1
}

Write-Host "All $($lockFiles.Count) packages.lock.json file(s) under '$Path' are RID-neutral."
exit 0
