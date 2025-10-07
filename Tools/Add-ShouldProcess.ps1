# Helper script to manually add ShouldProcess to remaining 14 functions
# This script provides the patterns to apply

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
