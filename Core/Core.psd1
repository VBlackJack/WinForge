#
# Module manifest for Core
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
    RootModule = 'Core.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0622.1'

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'Win11Forge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Core functions for Win11Forge including logging, status output, command existence checking, and administrator privilege validation.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Functions to export from this module
    FunctionsToExport = @(
        'Write-Status',
        'Test-CommandExists',
        'Format-FileSize'
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
            Tags = @('Win11Forge', 'Core', 'Logging', 'Utility')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = 'Win11Forge v2026062201'
        }
    }
}
