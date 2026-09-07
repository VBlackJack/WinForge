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
<#
.SYNOPSIS
    Verifies required runtime schemas and bilingual entry documents in a release ZIP.
.DESCRIPTION
    Reads archive entries without extracting or executing the payload. Every schema
    shipped in the source must be present with identical bytes in the archive.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArchivePath,
    [Parameter()]
    [string]$SourceRoot = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
try {
    $required = @('README.md', 'README.fr.md', 'CHANGELOG.md', 'CHANGELOG.fr.md',
        'Deploy-Win11Environment.ps1', 'Modules/JsonSchemaValidation.psm1',
        'Modules/DeploymentPlanning.psm1', 'Docs/DEPLOYMENT_WORKBENCH.md', 'Docs/DEPLOYMENT_WORKBENCH.fr.md',
        'Tools/Deployment-Workbench.ps1', 'Tools/Test-WinGetConfiguration.ps1', 'Tools/Test-GuestAcceptance.ps1')
    $required += @(Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'Schemas') -Filter '*.schema.json' -File |
        ForEach-Object { 'Schemas/' + $_.Name })
    if (@($required | Where-Object { $_ -like 'Schemas/*' }).Count -eq 0) { throw 'Source schemas are missing.' }
    foreach ($relativePath in $required) {
        $entries = @($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $relativePath })
        if ($entries.Count -ne 1) { throw "Missing or duplicate release entry: $relativePath" }
        $reader = [System.IO.StreamReader]::new($entries[0].Open(), [System.Text.Encoding]::UTF8, $true)
        try { $actual = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $expected = [System.IO.File]::ReadAllText((Join-Path $SourceRoot $relativePath))
        if ($actual -cne $expected) { throw "Release entry differs from source: $relativePath" }
    }
    Write-Output "Release payload verified: $($required.Count) required entries."
} finally {
    $archive.Dispose()
}
