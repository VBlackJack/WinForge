#
# Module manifest for InstallationEngine
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

@{
    # Script module or binary module file associated with this manifest
    RootModule = 'InstallationEngine.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0623.1'

    # ID used to uniquely identify this module
    GUID = 'e3a7b0c4-6d5f-4a1b-8c3e-4f5a6b7c8d9e'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'WinForge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Core installation engine orchestration for WinForge. Coordinates application installation via multiple sources (Winget, Chocolatey, Microsoft Store, Direct Download), manages rollback and deployment state, and supports parallel installation.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Nested modules to load
    NestedModules = @(
        'StateManager.psm1',
        'ApplicationDetection.psm1',
        'InstallationMethods.psm1',
        'InstallationOrchestrator.psm1'
    )

    # Functions to export from this module
    FunctionsToExport = @(
        # State Management (from StateManager)
        'Initialize-RollbackSession',
        'Save-RollbackState',
        'Add-RollbackEntry',
        'Get-RollbackState',
        'Get-RollbackEntries',
        'Clear-RollbackState',
        'Restore-RollbackState',
        'Test-ValidStateData',
        'Test-ValidRollbackEntry',
        'Initialize-DeploymentSession',
        'Save-DeploymentState',
        'Update-DeploymentProgress',
        'Get-DeploymentState',
        'Get-DeploymentProgress',
        'Clear-DeploymentState',
        'Test-DeploymentInProgress',
        'Get-ResumableDeployment',
        # Rollback & Deployment Orchestration (from InstallationOrchestrator)
        'Invoke-Rollback',
        'Test-IncompleteDeployment',
        'Resume-Deployment',
        # Detection functions (from ApplicationDetection)
        'Test-ApplicationInstalled',
        'Get-InstalledApplicationsCache',
        'Test-ApplicationInstalledFast',
        'Get-ApplicationsInstallationStatus',
        'Test-ApplicationByName',
        'Wait-ForOfficeInstallation',
        # Environment helper
        'Test-EnvironmentRestriction',
        # Installation methods (from InstallationMethods)
        'Install-ViaWinget',
        'Install-ViaChocolatey',
        'Install-ViaStore',
        'Install-ViaDirectDownload',
        'Install-WindowsFeature',
        'Install-WindowsCapability',
        # Orchestration helpers
        'Invoke-CustomInstallMethod',
        'Invoke-InstallationMethodSequence',
        'Get-ApplicationSources',
        'Invoke-ApplicationUpgrade',
        # Main installation functions
        'Install-Application',
        'Install-ApplicationsParallel'
    )

    # Cmdlets to export from this module
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess
    PrivateData = @{
        PSData = @{
            # Tags applied to this module
            Tags = @('WinForge', 'Installation', 'Deployment', 'Orchestration', 'Winget', 'Chocolatey')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = @'
v3.2.2: Complete Modular Architecture
- InstallationOrchestrator.psm1: High-level coordination logic (Install-Application, Install-ApplicationsParallel)
- State management (rollback, deployment resume) centralized in orchestrator
- InstallationEngine.psm1: Thin wrapper that imports and re-exports from sub-modules
- Clean separation of concerns across 4 modules

v3.1.5: Modular Architecture
- Split into 3 modules for maintainability
- ApplicationDetection.psm1: Detection and verification functions
- InstallationMethods.psm1: Individual installation method implementations
- InstallationEngine.psm1: Orchestration logic and state management

v3.1.4: Critical Security Fixes
- Replaced cmd /c with Start-Process argument arrays
- Added Test-ValidStateData for state file validation
- Added path traversal protection
- Added executable whitelist for Command detection
'@
        }
    }
}
