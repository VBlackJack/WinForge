#
# Module manifest for Win11ForgeExceptions
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
    RootModule = 'Win11ForgeExceptions.psm1'
    ModuleVersion = '1.0.0622.1'
    GUID = '2fd61c27-0cec-40f3-822e-89d81a3aaa0a'
    Author = 'Julien Bombled'
    CompanyName = 'Win11Forge'
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'
    Description = 'Custom exception types for Win11Forge error handling'
    PowerShellVersion = '5.1'
    RequiredModules = @()
    FunctionsToExport = '*'
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Win11Forge', 'Core', 'Exceptions', 'ErrorHandling')
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'
            ProjectUri = 'https://github.com/VBlackJack/WinForge'
            ReleaseNotes = 'Win11Forge v2026062201'
        }
    }
}
