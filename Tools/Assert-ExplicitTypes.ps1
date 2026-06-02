<#
.SYNOPSIS
  Fails when C# code uses implicit local variable declarations.

.DESCRIPTION
  Recursively scans C# source files under a path and reports local
  variable declarations, foreach declarations, and out declarations
  that use `var`. Generated files and build output directories are
  skipped. Anonymous-type assignments such as `var item = new { ... }`
  are allowed because C# requires `var` for those declarations.

.PARAMETER Path
  Directory to scan recursively for C# files. Relative paths are
  resolved against the current working directory. Defaults to 'GUI'.

.EXAMPLE
  pwsh -NoProfile -File .\Tools\Assert-ExplicitTypes.ps1

.EXAMPLE
  pwsh -NoProfile -File .\Tools\Assert-ExplicitTypes.ps1 -Path GUI

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
    Write-Host "Explicit type assertion failed: path '$Path' does not exist." -ForegroundColor Red
    exit 1
}

$files = @(
    Get-ChildItem -LiteralPath $Path -Filter '*.cs' -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and
            $_.Name -notlike '*.Designer.cs' -and
            $_.Name -notlike '*.g.cs'
        }
)

$implicitTypePatterns = @(
    '^\s*var\s+',
    '\bforeach\s*\(\s*var\s+',
    '\bout\s+var\s+'
)
$anonymousTypePattern = 'var\s+\w+\s*=\s*new\s*\{'
$violations = [System.Collections.Generic.List[string]]::new()

foreach ($file in $files) {
    $relativePath = Resolve-Path -LiteralPath $file.FullName -Relative
    if ($relativePath.StartsWith('.\')) {
        $relativePath = $relativePath.Substring(2)
    }

    $lines = @(Get-Content -LiteralPath $file.FullName)
    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        if ($line -match $anonymousTypePattern) {
            continue
        }

        foreach ($pattern in $implicitTypePatterns) {
            if ($line -match $pattern) {
                $lineNumber = $lineIndex + 1
                $violations.Add("${relativePath}:${lineNumber}: $($line.Trim())")
                break
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Explicit type assertion found $($violations.Count) var violation(s):" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host $violation -ForegroundColor Red
    }

    Write-Host "Use explicit types; see .editorconfig." -ForegroundColor Red
    exit 1
}

Write-Host "Explicit type assertion passed for '$Path'." -ForegroundColor Green
exit 0
