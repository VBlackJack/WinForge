#
# Module manifest for SecureStorage
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
    RootModule = 'SecureStorage.psm1'
    ModuleVersion = '3.6.8'
    GUID = '9f5e4876-1718-42ac-8f5f-884fa833bf3d'
    Author = 'Julien Bombled'
    CompanyName = 'Win11Forge'
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'
    Description = 'DPAPI-based secure storage for sensitive configuration'
    PowerShellVersion = '5.1'
    RequiredModules = @()
    FunctionsToExport = '*'
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Win11Forge', 'Core', 'Security', 'DPAPI', 'Encryption')
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'
            ProjectUri = 'https://github.com/JulienBombled/Win11Forge'
            ReleaseNotes = 'Win11Forge v3.6.8'
        }
    }
}
