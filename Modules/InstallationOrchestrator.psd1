#
# Module manifest for InstallationOrchestrator
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
    RootModule = 'InstallationOrchestrator.psm1'
    ModuleVersion = '3.6.8'
    GUID = 'a205fabe-d842-42c1-8989-4b9ec5383a53'
    Author = 'Julien Bombled'
    CompanyName = 'Win11Forge'
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'
    Description = 'High-level installation orchestration and coordination'
    PowerShellVersion = '5.1'
    RequiredModules = @()
    FunctionsToExport = '*'
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Win11Forge', 'Installation', 'Orchestrator')
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'
            ProjectUri = 'https://github.com/JulienBombled/Win11Forge'
            ReleaseNotes = 'Win11Forge v3.6.7'
        }
    }
}