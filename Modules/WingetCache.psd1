#
# Module manifest for WingetCache
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
    RootModule = 'WingetCache.psm1'

    # Version number of this module
    ModuleVersion = '1.0.0623.1'

    # ID used to uniquely identify this module
    GUID = 'f4b8c1d5-7e6a-4f2b-9d3c-5e6f7a8b9c0d'

    # Author of this module
    Author = 'Julien Bombled'

    # Company or vendor of this module
    CompanyName = 'WinForge'

    # Copyright statement for this module
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Winget list caching module for WinForge. Provides TTL-based caching of winget list and search operations to improve performance.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Functions to export from this module
    FunctionsToExport = @(
        'Initialize-WingetCache',
        'Get-CachedWingetList',
        'Get-CachedWingetSearch',
        'Update-WingetListCache',
        'Clear-WingetCache',
        'Save-WingetCache',
        'Get-WingetCacheStatistics'
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
            Tags = @('WinForge', 'Winget', 'Cache', 'Performance')

            # A URL to the license for this module
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'

            # A URL to the main website for this project
            ProjectUri = 'https://github.com/VBlackJack/WinForge'

            # Release notes
            ReleaseNotes = 'TTL-based caching for winget list and search operations'
        }
    }
}
