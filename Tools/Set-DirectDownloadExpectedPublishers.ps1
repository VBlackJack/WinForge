<#
.SYNOPSIS
    Applies verified ExpectedPublisher values to direct-download apps.

.DESCRIPTION
    Performs targeted, idempotent edits to Apps\Database\applications.json.
    The script inserts or updates Sources.ExpectedPublisher immediately after
    Sources.DirectUrl for empirically verified direct-download installers and
    leaves known-unverified apps dormant.

    Dry-run is the default. Use -Apply to write changes. The implementation
    preserves the existing UTF-8 BOM setting, newline style, and JSON ordering;
    it does not perform a ConvertFrom-Json/ConvertTo-Json rewrite.

.PARAMETER DatabasePath
    Path to applications.json. Defaults to Apps\Database\applications.json.

.PARAMETER Apply
    Write the targeted changes. Without this switch, the script only reports
    planned changes.

.EXAMPLE
    .\Tools\Set-DirectDownloadExpectedPublishers.ps1

.EXAMPLE
    .\Tools\Set-DirectDownloadExpectedPublishers.ps1 -Apply
#>

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

[CmdletBinding()]
param(
    [Parameter()]
    [string]$DatabasePath,

    [Parameter()]
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Script:PublisherMappings = [ordered]@{
    GoogleChrome        = 'Google LLC'
    DirectX             = 'Microsoft Corporation'
    DotNetSDK           = 'Microsoft Corporation'
    Discord             = 'Discord Inc.'
    SignalDesktop       = 'Signal Messenger, LLC'
    Steam               = 'Valve Corp.'
    BattleNet           = 'Blizzard Entertainment, Inc.'
    EpicGamesLauncher   = 'Epic Games Inc.'
    Git                 = 'Johannes Schindelin'
    Roboform            = 'Siber Systems'
    Insomnia            = 'Kong Inc.'
    BalenaEtcher        = 'Balena Ltd'
    UltimakerCura       = 'Ultimaker B.V.'
    HWiNFO64            = 'REALiX, s.r.o.'
    Tabby               = 'Eugen Pankov Softwareentwicklung'
    FreeDownloadManager = 'Softdeluxe LLC'
    Parsec              = 'Unity Technologies SF'
}

$Script:DormantAppIds = @(
    'ABDownloadManager',
    'AIMP',
    'FileZilla'
)

function Resolve-DatabaseFilePath {
    param(
        [Parameter()]
        [string]$Path
    )

    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repositoryRoot = Split-Path -Parent $scriptDirectory

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $candidatePath = Join-Path $repositoryRoot 'Apps\Database\applications.json'
    }
    elseif ([System.IO.Path]::IsPathRooted($Path)) {
        $candidatePath = $Path
    }
    else {
        $candidatePath = Join-Path (Get-Location).ProviderPath $Path
    }

    if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
        throw "Database file not found: $candidatePath"
    }

    return (Resolve-Path -LiteralPath $candidatePath).ProviderPath
}

function Read-TextFilePreservingEncoding {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $startIndex = if ($hasUtf8Bom) { 3 } else { 0 }
    $contentLength = $bytes.Length - $startIndex
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $text = $utf8.GetString($bytes, $startIndex, $contentLength)

    return [PSCustomObject]@{
        Text       = $text
        HasUtf8Bom = $hasUtf8Bom
    }
}

function Split-TextLines {
    param(
        [Parameter(Mandatory)]
        [string]$Text
    )

    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $endsWithNewline = $Text.EndsWith($newline, [StringComparison]::Ordinal)
    $body = if ($endsWithNewline) {
        $Text.Substring(0, $Text.Length - $newline.Length)
    }
    else {
        $Text
    }

    $lines = if ($body.Length -eq 0) {
        @()
    }
    else {
        $body -split [Regex]::Escape($newline)
    }

    return [PSCustomObject]@{
        Lines           = $lines
        Newline         = $newline
        EndsWithNewline = $endsWithNewline
    }
}

function Join-TextLines {
    param(
        [Parameter(Mandatory)]
        [string[]]$Lines,

        [Parameter(Mandatory)]
        [string]$Newline,

        [Parameter(Mandatory)]
        [bool]$EndsWithNewline
    )

    $text = $Lines -join $Newline
    if ($EndsWithNewline) {
        $text += $Newline
    }

    return $text
}

function Write-TextFilePreservingEncoding {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Text,

        [Parameter(Mandatory)]
        [bool]$HasUtf8Bom
    )

    $encoding = [System.Text.UTF8Encoding]::new($HasUtf8Bom)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function ConvertTo-JsonStringLiteral {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    return ConvertTo-Json -InputObject $Value -Compress
}

function New-StringList {
    param(
        [Parameter(Mandatory)]
        [string[]]$Lines
    )

    $list = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $Lines) {
        $list.Add($line)
    }

    return ,$list
}

function Find-AppStartIndex {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [string]$AppId
    )

    $pattern = '^    "' + [Regex]::Escape($AppId) + '": \{$'
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match $pattern) {
            return $index
        }
    }

    return -1
}

function Find-AppEndIndex {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [int]$StartIndex
    )

    for ($index = $StartIndex + 1; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match '^    "[^"]+": \{$') {
            return $index - 1
        }
    }

    return $Lines.Count - 1
}

function Find-SourcesRange {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [int]$AppStartIndex,

        [Parameter(Mandatory)]
        [int]$AppEndIndex
    )

    $sourcesStart = -1
    for ($index = $AppStartIndex; $index -le $AppEndIndex; $index++) {
        if ($Lines[$index] -match '^      "Sources": \{$') {
            $sourcesStart = $index
            break
        }
    }

    if ($sourcesStart -lt 0) {
        return [PSCustomObject]@{ Start = -1; End = -1 }
    }

    for ($index = $sourcesStart + 1; $index -le $AppEndIndex; $index++) {
        if ($Lines[$index] -match '^      \},?$') {
            return [PSCustomObject]@{ Start = $sourcesStart; End = $index }
        }
    }

    return [PSCustomObject]@{ Start = $sourcesStart; End = -1 }
}

function Find-DirectUrlIndex {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [int]$SourcesStart,

        [Parameter(Mandatory)]
        [int]$SourcesEnd
    )

    for ($index = $SourcesStart + 1; $index -lt $SourcesEnd; $index++) {
        if ($Lines[$index] -match '^        "DirectUrl": ') {
            return $index
        }
    }

    return -1
}

function Find-ExpectedPublisherIndexes {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [int]$SourcesStart,

        [Parameter(Mandatory)]
        [int]$SourcesEnd
    )

    $indexes = [System.Collections.Generic.List[int]]::new()
    for ($index = $SourcesStart + 1; $index -lt $SourcesEnd; $index++) {
        if ($Lines[$index] -match '^        "ExpectedPublisher": ') {
            $indexes.Add($index)
        }
    }

    return ,$indexes
}

function Remove-TrailingComma {
    param(
        [Parameter(Mandatory)]
        [string]$Line
    )

    return $Line -replace ',$', ''
}

function Add-TrailingComma {
    param(
        [Parameter(Mandatory)]
        [string]$Line
    )

    if ($Line.EndsWith(',', [StringComparison]::Ordinal)) {
        return $Line
    }

    return "$Line,"
}

function Get-AppSourceContext {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [string]$AppId
    )

    $appStart = Find-AppStartIndex -Lines $Lines -AppId $AppId
    if ($appStart -lt 0) {
        throw "AppId not found in applications.json: $AppId"
    }

    $appEnd = Find-AppEndIndex -Lines $Lines -StartIndex $appStart
    $sourcesRange = Find-SourcesRange -Lines $Lines -AppStartIndex $appStart -AppEndIndex $appEnd
    if ($sourcesRange.Start -lt 0 -or $sourcesRange.End -lt 0) {
        throw "Sources block not found for AppId: $AppId"
    }

    $directUrlIndex = Find-DirectUrlIndex -Lines $Lines -SourcesStart $sourcesRange.Start -SourcesEnd $sourcesRange.End
    if ($directUrlIndex -lt 0) {
        throw "DirectUrl line not found for AppId: $AppId"
    }

    return [PSCustomObject]@{
        AppStart       = $appStart
        AppEnd         = $appEnd
        SourcesStart   = $sourcesRange.Start
        SourcesEnd     = $sourcesRange.End
        DirectUrlIndex = $directUrlIndex
    }
}

function Set-ExpectedPublisherLine {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [string]$AppId,

        [Parameter(Mandatory)]
        [string]$ExpectedPublisher
    )

    $context = Get-AppSourceContext -Lines $Lines -AppId $AppId
    $expectedLine = '        "ExpectedPublisher": ' + (ConvertTo-JsonStringLiteral -Value $ExpectedPublisher)
    $existingIndexes = Find-ExpectedPublisherIndexes -Lines $Lines -SourcesStart $context.SourcesStart -SourcesEnd $context.SourcesEnd

    $oldValue = $null
    if ($existingIndexes.Count -gt 0 -and $Lines[$existingIndexes[0]] -match '"ExpectedPublisher":\s*"([^"]*)"') {
        $oldValue = $matches[1]
    }

    for ($index = $existingIndexes.Count - 1; $index -ge 0; $index--) {
        $Lines.RemoveAt($existingIndexes[$index])
    }

    $context = Get-AppSourceContext -Lines $Lines -AppId $AppId
    $Lines[$context.DirectUrlIndex] = Add-TrailingComma -Line $Lines[$context.DirectUrlIndex]
    $Lines.Insert($context.DirectUrlIndex + 1, $expectedLine)

    $action = if ($null -eq $oldValue) {
        'Add'
    }
    elseif ([string]::Equals($oldValue, $ExpectedPublisher, [StringComparison]::Ordinal)) {
        'Keep'
    }
    else {
        'Update'
    }

    return [PSCustomObject]@{
        AppId             = $AppId
        Action            = $action
        ExpectedPublisher = $ExpectedPublisher
    }
}

function Remove-ExpectedPublisherLine {
    param(
        [Parameter(Mandatory)]
        [System.Collections.Generic.List[string]]$Lines,

        [Parameter(Mandatory)]
        [string]$AppId
    )

    $context = Get-AppSourceContext -Lines $Lines -AppId $AppId
    $existingIndexes = Find-ExpectedPublisherIndexes -Lines $Lines -SourcesStart $context.SourcesStart -SourcesEnd $context.SourcesEnd

    if ($existingIndexes.Count -eq 0) {
        return [PSCustomObject]@{
            AppId             = $AppId
            Action            = 'Skip'
            ExpectedPublisher = $null
        }
    }

    for ($index = $existingIndexes.Count - 1; $index -ge 0; $index--) {
        $Lines.RemoveAt($existingIndexes[$index])
    }

    $context = Get-AppSourceContext -Lines $Lines -AppId $AppId
    if ($context.DirectUrlIndex + 1 -eq $context.SourcesEnd) {
        $Lines[$context.DirectUrlIndex] = Remove-TrailingComma -Line $Lines[$context.DirectUrlIndex]
    }

    return [PSCustomObject]@{
        AppId             = $AppId
        Action            = 'Remove'
        ExpectedPublisher = $null
    }
}

$resolvedDatabasePath = Resolve-DatabaseFilePath -Path $DatabasePath
$fileContent = Read-TextFilePreservingEncoding -Path $resolvedDatabasePath
$splitContent = Split-TextLines -Text $fileContent.Text
$lineList = New-StringList -Lines $splitContent.Lines

$results = [System.Collections.Generic.List[object]]::new()

foreach ($mapping in $Script:PublisherMappings.GetEnumerator()) {
    $result = Set-ExpectedPublisherLine -Lines $lineList -AppId $mapping.Key -ExpectedPublisher $mapping.Value
    $results.Add($result)
}

foreach ($appId in $Script:DormantAppIds) {
    $result = Remove-ExpectedPublisherLine -Lines $lineList -AppId $appId
    $results.Add($result)
}

$newText = Join-TextLines -Lines $lineList.ToArray() -Newline $splitContent.Newline -EndsWithNewline $splitContent.EndsWithNewline
$null = $newText | ConvertFrom-Json

if ($Apply) {
    Write-TextFilePreservingEncoding -Path $resolvedDatabasePath -Text $newText -HasUtf8Bom $fileContent.HasUtf8Bom
}

$results | Sort-Object -Property AppId
