<#
.SYNOPSIS
    Checks for issues in the application database

.DESCRIPTION
    Examines problematic applications in the database to
    identify configuration issues with sources and detection.

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

Import-Module ./Modules/ApplicationDatabase.psm1 -Force

$problematicApps = @('WindowsSandbox', 'ProtonDrive', 'ProtonMailBridge', 'ProtonPass')

foreach ($appId in $problematicApps) {
    $app = Get-ApplicationById -AppId $appId
    if ($app) {
        Write-Host "`n=== $appId ===" -ForegroundColor Cyan
        Write-Host "Name: $($app.Name)"
        Write-Host "Category: $($app.Category)"

        $sources = @()
        if ($app.Sources.Winget) { $sources += "Winget: $($app.Sources.Winget)" }
        if ($app.Sources.Chocolatey) { $sources += "Chocolatey: $($app.Sources.Chocolatey)" }
        if ($app.Sources.Store) { $sources += "Store: $($app.Sources.Store)" }
        if ($app.Sources.DirectUrl) { $sources += "DirectUrl: $($app.Sources.DirectUrl)" }

        if ($sources.Count -eq 0) {
            Write-Host "Sources: NONE" -ForegroundColor Red
        } else {
            Write-Host "Sources:" -ForegroundColor Green
            $sources | ForEach-Object { Write-Host "  $_" }
        }

        if ($app.Detection) {
            Write-Host "Detection: Present" -ForegroundColor Green
        } else {
            Write-Host "Detection: MISSING" -ForegroundColor Red
        }
    } else {
        Write-Host "`n=== $appId ===" -ForegroundColor Red
        Write-Host "NOT FOUND IN DATABASE" -ForegroundColor Red
    }
}
