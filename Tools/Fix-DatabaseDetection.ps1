<#
.SYNOPSIS
    Add Detection configuration to applications in database

.DESCRIPTION
    Updates the application database to add proper Detection
    configuration for applications like Proton apps.

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

$dbPath = 'Apps/Database/applications.json'
$db = Get-Content $dbPath -Raw | ConvertFrom-Json

# Define Detection for Proton apps (Winget installs to user AppData)
$protonApps = @{
    'ProtonDrive' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Drive'
        Value = 'DisplayName'
    }
    'ProtonMailBridge' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Mail Bridge'
        Value = 'DisplayName'
    }
    'ProtonPass' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Pass'
        Value = 'DisplayName'
    }
}

$fixed = 0

foreach ($appId in $protonApps.Keys) {
    if ($db.Applications.$appId) {
        $detection = $protonApps[$appId]

        # Add Detection if missing
        if (-not $db.Applications.$appId.Detection) {
            $db.Applications.$appId | Add-Member -NotePropertyName 'Detection' -NotePropertyValue ([PSCustomObject]$detection) -Force
            Write-Host "Added Detection to $appId" -ForegroundColor Green
            $fixed++
        } else {
            Write-Host "$appId already has Detection" -ForegroundColor Yellow
        }
    } else {
        Write-Host "$appId not found in database" -ForegroundColor Red
    }
}

# Save updated database
$db | ConvertTo-Json -Depth 10 | Set-Content $dbPath -Encoding UTF8

Write-Host "`nFixed $fixed app(s) in database" -ForegroundColor Cyan
Write-Host "Database saved to: $dbPath" -ForegroundColor Cyan
