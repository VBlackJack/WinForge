<#
.SYNOPSIS
    Reports Authenticode publishers for direct-download installers.

.DESCRIPTION
    Reads Apps\Database\applications.json, filters applications with a non-empty
    Sources.DirectUrl, downloads each installer to a temporary directory, and
    reports the Authenticode signature status and signer subject.

    This tool is output-only for the repository. It does not modify
    applications.json. Temporary downloads are removed by default.

.PARAMETER DatabasePath
    Path to applications.json. Defaults to Apps\Database\applications.json.

.PARAMETER AppIdFilter
    Optional AppId list to restrict the run, useful for smoke tests.

.PARAMETER TimeoutSeconds
    Per-download timeout in seconds. Defaults to 300.

.PARAMETER DownloadDirectory
    Optional directory for downloaded installers. When provided, downloads are
    kept for inspection and the directory is not cleaned up by this script.

.PARAMETER KeepDownloads
    Keep the temporary download directory when DownloadDirectory is not provided.

.EXAMPLE
    .\Tools\Get-DirectDownloadPublishers.ps1

.EXAMPLE
    .\Tools\Get-DirectDownloadPublishers.ps1 -AppIdFilter GoogleChrome,Git -Verbose
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
    [string[]]$AppIdFilter,

    [Parameter()]
    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 300,

    [Parameter()]
    [string]$DownloadDirectory,

    [Parameter()]
    [switch]$KeepDownloads
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Script:HttpUserAgent = 'WinForge-PublisherProbe/1.0'
$Script:TempDirectoryPrefix = 'WinForgePublisherProbe_'

function Get-PropertyValueOrNull {
    param(
        [Parameter()]
        $InputObject,

        [Parameter(Mandatory)]
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

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

function Test-AppIdIncluded {
    param(
        [Parameter(Mandatory)]
        [string]$AppId,

        [Parameter()]
        [string[]]$Filter
    )

    if ($null -eq $Filter -or $Filter.Count -eq 0) {
        return $true
    }

    foreach ($filterItem in $Filter) {
        if ([string]::Equals($filterItem, $AppId, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-DirectDownloadApplications {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [string[]]$Filter
    )

    $databaseContent = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $database = $databaseContent | ConvertFrom-Json
    $applications = Get-PropertyValueOrNull -InputObject $database -PropertyName 'Applications'

    if ($null -eq $applications) {
        throw "Database does not contain an Applications object: $Path"
    }

    foreach ($applicationProperty in ($applications.PSObject.Properties | Sort-Object -Property Name)) {
        $appId = $applicationProperty.Name
        if (-not (Test-AppIdIncluded -AppId $appId -Filter $Filter)) {
            continue
        }

        $application = $applicationProperty.Value
        $sources = Get-PropertyValueOrNull -InputObject $application -PropertyName 'Sources'
        $directUrl = Get-PropertyValueOrNull -InputObject $sources -PropertyName 'DirectUrl'

        if ([string]::IsNullOrWhiteSpace([string]$directUrl)) {
            continue
        }

        [PSCustomObject]@{
            AppId = $appId
            Url   = [string]$directUrl
        }
    }
}

function ConvertTo-SafeFileName {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    $invalidCharacters = [Regex]::Escape((-join [System.IO.Path]::GetInvalidFileNameChars()))
    $safeValue = [Regex]::Replace($Value, "[$invalidCharacters]+", '_')
    $safeValue = $safeValue.Trim('_')

    if ([string]::IsNullOrWhiteSpace($safeValue)) {
        return 'download'
    }

    return $safeValue
}

function Get-DownloadFileExtension {
    param(
        [Parameter(Mandatory)]
        [string]$Url
    )

    $path = $Url
    try {
        $uri = [Uri]$Url
        $path = $uri.AbsolutePath
    }
    catch {
        $path = $Url
    }

    $match = [Regex]::Match($path, '\.(exe|msi|msix|zip)$', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) {
        return $match.Value.ToLowerInvariant()
    }

    return '.download'
}

function New-DownloadFilePath {
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [Parameter(Mandatory)]
        [string]$AppId,

        [Parameter(Mandatory)]
        [string]$Url
    )

    $safeAppId = ConvertTo-SafeFileName -Value $AppId
    $extension = Get-DownloadFileExtension -Url $Url
    return Join-Path $Directory "$safeAppId$extension"
}

function Save-InstallerFile {
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter(Mandatory)]
        [int]$Timeout
    )

    $ProgressPreference = 'SilentlyContinue'
    $headers = @{
        'User-Agent' = $Script:HttpUserAgent
    }

    Invoke-WebRequest -Uri $Url -OutFile $OutputPath -Headers $headers `
        -MaximumRedirection 10 -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop

    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Download did not create a file."
    }

    $downloadedFile = Get-Item -LiteralPath $OutputPath
    if ($downloadedFile.Length -le 0) {
        throw "Downloaded file is empty."
    }

    return $downloadedFile
}

function Get-FileLengthOrNull {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    return (Get-Item -LiteralPath $Path).Length
}

function Get-InstallerPublisherProbe {
    param(
        [Parameter(Mandatory)]
        [object]$Application,

        [Parameter(Mandatory)]
        [string]$Directory,

        [Parameter(Mandatory)]
        [int]$Timeout
    )

    $outputPath = New-DownloadFilePath -Directory $Directory -AppId $Application.AppId -Url $Application.Url
    Write-Verbose "Downloading $($Application.AppId) from $($Application.Url)"

    $downloadedFile = $null
    try {
        $downloadedFile = Save-InstallerFile -Url $Application.Url -OutputPath $outputPath -Timeout $Timeout
    }
    catch {
        $bytes = Get-FileLengthOrNull -Path $outputPath
        return [PSCustomObject]@{
            AppId         = $Application.AppId
            Url           = $Application.Url
            Status        = "DownloadFailed: $($_.Exception.Message)"
            Bytes         = $bytes
            SignerSubject = $null
        }
    }

    Write-Verbose "Reading Authenticode signature for $($Application.AppId): $outputPath"
    try {
        $signature = Get-AuthenticodeSignature -FilePath $outputPath
        $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { $null }

        return [PSCustomObject]@{
            AppId         = $Application.AppId
            Url           = $Application.Url
            Status        = $signature.Status.ToString()
            Bytes         = $downloadedFile.Length
            SignerSubject = $subject
        }
    }
    catch {
        return [PSCustomObject]@{
            AppId         = $Application.AppId
            Url           = $Application.Url
            Status        = "SignatureError: $($_.Exception.Message)"
            Bytes         = $downloadedFile.Length
            SignerSubject = $null
        }
    }
}

function New-ProbeDirectory {
    param(
        [Parameter()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $directoryName = "$Script:TempDirectoryPrefix$([guid]::NewGuid().ToString('N'))"
        $candidatePath = Join-Path ([System.IO.Path]::GetTempPath()) $directoryName
    }
    elseif ([System.IO.Path]::IsPathRooted($Path)) {
        $candidatePath = $Path
    }
    else {
        $candidatePath = Join-Path (Get-Location).ProviderPath $Path
    }

    New-Item -Path $candidatePath -ItemType Directory -Force | Out-Null
    return (Resolve-Path -LiteralPath $candidatePath).ProviderPath
}

function Remove-ProbeDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).ProviderPath
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $fullPath = [System.IO.Path]::GetFullPath($resolvedPath)
    $leafName = Split-Path -Leaf $fullPath

    if (-not $fullPath.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a non-temp probe directory: $fullPath"
    }

    if (-not $leafName.StartsWith($Script:TempDirectoryPrefix, [StringComparison]::Ordinal)) {
        throw "Refusing to remove an unexpected probe directory: $fullPath"
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

if (-not (Get-Command -Name Get-AuthenticodeSignature -ErrorAction SilentlyContinue)) {
    throw 'Get-AuthenticodeSignature is not available in this PowerShell session.'
}

$resolvedDatabasePath = Resolve-DatabaseFilePath -Path $DatabasePath
$downloadDirectoryWasProvided = -not [string]::IsNullOrWhiteSpace($DownloadDirectory)
$probeDirectory = New-ProbeDirectory -Path $DownloadDirectory

try {
    $applications = @(Get-DirectDownloadApplications -Path $resolvedDatabasePath -Filter $AppIdFilter)
    Write-Verbose "Found $($applications.Count) direct-download application(s)."
    Write-Verbose "Download directory: $probeDirectory"

    $results = [System.Collections.Generic.List[object]]::new()

    for ($index = 0; $index -lt $applications.Count; $index++) {
        $application = $applications[$index]
        $activityStatus = "$($index + 1)/$($applications.Count): $($application.AppId)"
        $percentComplete = if ($applications.Count -gt 0) {
            [int]((($index + 1) / $applications.Count) * 100)
        }
        else {
            100
        }

        Write-Progress -Activity 'Checking direct-download publishers' -Status $activityStatus -PercentComplete $percentComplete

        $result = Get-InstallerPublisherProbe -Application $application -Directory $probeDirectory -Timeout $TimeoutSeconds
        $results.Add($result)
    }

    Write-Progress -Activity 'Checking direct-download publishers' -Completed
    $results | Sort-Object -Property AppId
}
finally {
    if (-not $KeepDownloads -and -not $downloadDirectoryWasProvided) {
        Remove-ProbeDirectory -Path $probeDirectory
    }
    elseif ($KeepDownloads -or $downloadDirectoryWasProvided) {
        Write-Verbose "Downloads kept in: $probeDirectory"
    }
}
