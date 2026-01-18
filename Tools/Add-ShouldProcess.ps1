<#
.SYNOPSIS
    Helper script for adding ShouldProcess to functions

.DESCRIPTION
    Provides patterns and guidance for manually adding ShouldProcess
    support to remaining functions.

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

Write-Host "=== ShouldProcess Pattern for Set-* Functions ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Change CmdletBinding:" -ForegroundColor Yellow
Write-Host "   FROM: [CmdletBinding()]"
Write-Host "   TO:   [CmdletBinding(SupportsShouldProcess, ConfirmImpact='Medium')]"
Write-Host ""
Write-Host "2. Wrap main code in ShouldProcess:" -ForegroundColor Yellow
Write-Host '   if ($PSCmdlet.ShouldProcess("target", "action")) {'
Write-Host '       # existing code here'
Write-Host '   }'
Write-Host ""
Write-Host "3. For functions that don't return values:" -ForegroundColor Yellow
Write-Host '   if ($PSCmdlet.ShouldProcess("target", "action")) {'
Write-Host '       # existing code'
Write-Host '   } else {'
Write-Host '       Write-Verbose "Operation cancelled by user"'
Write-Host '   }'
Write-Host ""

Write-Host "=== Functions to Update (14 remaining) ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "SystemConfig.psm1 (6):" -ForegroundColor Yellow
Write-Host "  - Set-ExplorerConfiguration"
Write-Host "  - Set-TaskbarConfiguration"
Write-Host "  - Set-NetworkConfiguration"
Write-Host "  - Set-PrivacyConfiguration"
Write-Host "  - Set-PerformanceConfiguration"
Write-Host "  - Set-SecurityConfiguration"
Write-Host "  - Set-SystemConfiguration"
Write-Host ""
Write-Host "Prerequisites.psm1 (2):" -ForegroundColor Yellow
Write-Host "  - Update-EnvironmentPath"
Write-Host "  - Start-PrerequisitesInstallation"
Write-Host ""
Write-Host "StartMenuLayout.psm1 (2):" -ForegroundColor Yellow
Write-Host "  - New-LayoutModificationJson"
Write-Host "  - Set-StartMenuLayout"
Write-Host ""
Write-Host "Others (4):" -ForegroundColor Yellow
Write-Host "  - Start-ProcessWithTimeout (InstallationEngine.psm1)"
Write-Host "  - Start-DatabaseValidation (Win11ForgeGUI.psm1)"
Write-Host "  - Reset-DatabaseCache (ApplicationDatabase.psm1)"
Write-Host ""
Write-Host "NOTE: These require manual editing due to complex control flow" -ForegroundColor Red
