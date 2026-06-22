#
# Module manifest for StructuredLogging
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
    RootModule = 'StructuredLogging.psm1'
    ModuleVersion = '1.0.0622.1'
    GUID = 'bfc6d25e-9687-4bb8-a6c6-9244335d17e8'
    Author = 'Julien Bombled'
    CompanyName = 'WinForge'
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'
    Description = 'Structured JSON logging for WinForge operations'
    PowerShellVersion = '5.1'
    RequiredModules = @()
    FunctionsToExport = '*'
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('WinForge', 'Core', 'Logging', 'JSON', 'Structured')
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'
            ProjectUri = 'https://github.com/VBlackJack/WinForge'
            ReleaseNotes = 'WinForge v2026062201'
        }
    }
}
