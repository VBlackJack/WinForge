<#
.SYNOPSIS
    Fix module version numbers (v2)

.DESCRIPTION
    Updated version of module version fixer with improved
    pattern matching.

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

$targetVersion = '2.5.0'
$modules = @(
    'Modules/StartMenuLayout.psm1',
    'Modules/StartupManager.psm1',
    'Modules/Prerequisites.psm1',
    'Modules/StartMenuPinning.psm1',
    'Modules/SystemConfig.psm1',
    'Modules/EnvironmentDetection.psm1',
    'Modules/ProfileManager.psm1',
    'Modules/ApplicationDatabase.psm1'
)

$fixed = 0

foreach ($modulePath in $modules) {
    if (Test-Path $modulePath) {
        $content = Get-Content $modulePath -Raw
        $originalContent = $content

        # Fix malformed versions like $12.5.0
        $content = $content -replace '\$1\d+\.\d+\.\d+', "Version: $targetVersion"

        # Fix version patterns
        $content = $content -replace '(Module v)\d+\.\d+\.\d+', "`${1}$targetVersion"
        $content = $content -replace '(Version:\s*)\d+\.\d+\.\d+', "`${1}$targetVersion"
        $content = $content -replace 'v\d+\.\d+\.\d+(?=\s*\r?\n)', "v$targetVersion"

        if ($content -ne $originalContent) {
            Set-Content -Path $modulePath -Value $content -NoNewline
            $fixed++
            Write-Host "Fixed: $modulePath" -ForegroundColor Green
        } else {
            Write-Host "Already correct: $modulePath" -ForegroundColor Gray
        }
    }
}

Write-Host "`nFixed $fixed module(s) to version $targetVersion" -ForegroundColor Cyan
