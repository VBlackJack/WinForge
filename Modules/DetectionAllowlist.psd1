#
# Module manifest for DetectionAllowlist
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
    RootModule = 'DetectionAllowlist.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0621.1'

    # ID used to uniquely identify this module
    GUID = 'ba65ebf7-4ff0-4c03-9e87-cff491276264'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'Win11Forge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Shared command-detection allowlist resolver. Single source of truth loaded from Config/detection-allowlist.json, read by every PowerShell detection path and aligned with the GUI detection probe.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Functions to export from this module
    FunctionsToExport = @(
        'Get-DetectionAllowlist'
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
            Tags = @('Win11Forge', 'Security', 'Detection', 'Allowlist')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = 'Single source for the command-detection allowlist, replacing four drifting in-module copies.'
        }
    }
}
