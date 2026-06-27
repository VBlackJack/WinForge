#
# Module manifest for DownloadValidation
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
    RootModule = 'DownloadValidation.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0627.1'

    # ID used to uniquely identify this module
    GUID = '3f29f7f7-c975-493a-96db-040b49cc2f9d'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'WinForge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Shared integrity-validation policy resolver for direct-download installers. Single source of the fail-closed decision used by the sequential and parallel installation paths.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Functions to export from this module
    FunctionsToExport = @(
        'Resolve-DirectDownloadValidationMode'
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
            Tags = @('WinForge', 'Security', 'Download', 'Validation')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = 'Extracted from the inline fail-closed gate so the sequential and parallel paths share one validation policy.'
        }
    }
}
