#
# Module manifest for ApplicationDetection
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
    RootModule = 'ApplicationDetection.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0622.1'

    # ID used to uniquely identify this module
    GUID = 'c1e5f8a2-4b3d-4e9f-8a1c-2d3e4f5a6b7c'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'WinForge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Application detection and installation verification functions for WinForge. Includes detection methods for Registry, File, Command, WindowsFeature, WindowsCapability, and StoreApp applications.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Functions to export from this module
    FunctionsToExport = @(
        'Test-RegistryKey',
        'Expand-DetectionPath',
        'Get-InstalledAppVersion',
        'Wait-ForOfficeInstallation',
        'Test-ApplicationByName',
        'Test-ApplicationInstalled',
        'Get-InstalledApplicationsCache',
        'Test-ApplicationInstalledFast',
        'Get-ApplicationsInstallationStatus'
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
            Tags = @('WinForge', 'Detection', 'Application', 'Winget', 'Chocolatey')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = 'Extracted from InstallationEngine.psm1 for modularity (v3.1.4)'
        }
    }
}
