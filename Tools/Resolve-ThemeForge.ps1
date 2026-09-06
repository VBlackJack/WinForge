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
<#
.SYNOPSIS
    Checks out the exact ThemeForge revision used by CI.
.DESCRIPTION
    Creates a dedicated dependency checkout inside WinForge. Existing checkouts
    must be clean and already at the pinned revision; no existing work is reset.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dependency = (Get-Content -LiteralPath (Join-Path $root 'Config/build-dependencies.json') -Raw | ConvertFrom-Json).ThemeForge
if ($dependency.revision -notmatch '^[0-9a-f]{40}$') { throw 'Invalid dependency revision.' }
$destination = Join-Path $root 'ThemeForge'
if (Test-Path -LiteralPath $destination) {
    $current = git -C $destination rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Existing dependency directory is not a Git checkout.' }
    $status = git -C $destination status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0 -or $status -or $current -ne $dependency.revision) {
        throw 'Existing ThemeForge checkout differs from the pinned source. Preserve it and move it aside before retrying.'
    }
} else {
    git clone --no-checkout -- $dependency.repository $destination
    if ($LASTEXITCODE -ne 0) { throw 'ThemeForge clone failed.' }
    git -C $destination checkout --detach $dependency.revision
    if ($LASTEXITCODE -ne 0) { throw 'ThemeForge checkout failed.' }
}
Write-Output $destination
