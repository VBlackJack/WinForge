<#
.SYNOPSIS
    Launch Startup Manager HTML Tool

.DESCRIPTION
    Opens the Startup Manager HTML interface in default browser

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

$htmlPath = Join-Path $PSScriptRoot "StartupManager.html"

if (-not (Test-Path $htmlPath)) {
    Write-Host "Error: StartupManager.html not found at $htmlPath" -ForegroundColor Red
    exit 1
}

Write-Host "Opening Startup Manager in browser..." -ForegroundColor Cyan
Start-Process $htmlPath

Write-Host ""
Write-Host "Startup Manager is now open in your browser." -ForegroundColor Green
Write-Host "Use the interface to select applications to disable from startup." -ForegroundColor Yellow
Write-Host "When done, download the config and copy to:" -ForegroundColor Yellow
Write-Host "  C:\sys\Win11Forge\Config\startup-blacklist.json" -ForegroundColor White
