<#
.SYNOPSIS
    Debug version matching patterns

.DESCRIPTION
    Tests version regex patterns against README.md to debug
    version detection issues.

.NOTES
    Author: Julien Bombled
    Version: 1.0.0
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

$readmeContent = Get-Content 'README.md' -Raw
$versionPattern = '(\d+\.\d+\.\d+)'

Write-Host "=== Testing version regex on README ===" -ForegroundColor Cyan

# Test 1: Line 35 pattern (what works)
if ($readmeContent -match "Win11Forge Framework v$versionPattern") {
    Write-Host "Pattern 1 (Win11Forge Framework v...): $($matches[1])" -ForegroundColor Green
}

# Test 2: Line 178 pattern (what's failing)
if ($readmeContent -match "v$versionPattern") {
    Write-Host "Pattern 2 (v...): $($matches[1])" -ForegroundColor Yellow
}

# Show what $uniqueVersions would be
$versions = @{}
$versions['README.md'] = '2.5.0'
$versions['Deploy'] = '2.5.0'
$versions['Module1'] = '2.5.0'

$uniqueVersions = $versions.Values | Select-Object -Unique
Write-Host "`nUnique versions array: $($uniqueVersions -join ', ')" -ForegroundColor Cyan
Write-Host "First unique version: $($uniqueVersions[0])" -ForegroundColor Cyan
