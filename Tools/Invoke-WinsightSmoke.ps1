<#
.SYNOPSIS
    Runs an opt-in Winsight smoke test against the Win11Forge WPF GUI.

.DESCRIPTION
    Builds Winsight and Win11Forge if needed, launches Win11Forge.GUI.dll through
    dotnet, drives it through the Winsight MCP stdio server, and captures PNG
    screenshots for the dashboard, settings, and app catalog surfaces.

.PARAMETER WinsightRoot
    Path to the Winsight repository. Defaults to WINSIGHT_ROOT, then ..\winsight.

.PARAMETER Configuration
    Build configuration for Winsight and Win11Forge.

.PARAMETER ArtifactDirectory
    Directory where screenshots are written.

.PARAMETER SkipBuild
    Reuse existing build outputs.

.EXAMPLE
    .\Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
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
    [string]$WinsightRoot,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ArtifactDirectory,

    [switch]$SkipBuild,

    [int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RequestId = 1
$script:McpProcess = $null

function Resolve-WinsightRoot {
    param(
        [string]$RequestedRoot,
        [string]$RepositoryRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    if (-not [string]::IsNullOrWhiteSpace($env:WINSIGHT_ROOT)) {
        return (Resolve-Path -LiteralPath $env:WINSIGHT_ROOT).Path
    }

    $fallback = Join-Path (Split-Path -Parent $RepositoryRoot) 'winsight'
    return (Resolve-Path -LiteralPath $fallback).Path
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    Write-Host "Running: $FilePath $($ArgumentList -join ' ')" -ForegroundColor Cyan
    $process = Start-Process -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -NoNewWindow `
        -Wait `
        -PassThru

    if ($process.ExitCode -ne 0) {
        throw "$FilePath exited with code $($process.ExitCode)."
    }
}

function Start-McpServer {
    param(
        [Parameter(Mandatory)]
        [string]$McpAssemblyPath
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = 'dotnet'
    $psi.Arguments = "`"$McpAssemblyPath`""
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $false
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $script:McpProcess = [System.Diagnostics.Process]::Start($psi)
    if ($null -eq $script:McpProcess) {
        throw "Failed to start Winsight MCP server."
    }

    $null = Send-McpRequest -Method 'initialize' -Params ([ordered]@{
        protocolVersion = '2024-11-05'
        capabilities = [ordered]@{}
        clientInfo = [ordered]@{
            name = 'win11forge-winsight-smoke'
            version = '0.1'
        }
    })

    Send-McpNotification -Method 'notifications/initialized'
}

function Send-McpNotification {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [object]$Params
    )

    $payload = [ordered]@{
        jsonrpc = '2.0'
        method = $Method
    }

    if ($null -ne $Params) {
        $payload.params = $Params
    }

    $json = $payload | ConvertTo-Json -Depth 20 -Compress
    $script:McpProcess.StandardInput.WriteLine($json)
    $script:McpProcess.StandardInput.Flush()
}

function Send-McpRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [object]$Params
    )

    if ($null -eq $script:McpProcess -or $script:McpProcess.HasExited) {
        throw "Winsight MCP server is not running."
    }

    $id = $script:RequestId
    $script:RequestId++

    $payload = [ordered]@{
        jsonrpc = '2.0'
        id = $id
        method = $Method
    }

    if ($null -ne $Params) {
        $payload.params = $Params
    }

    $json = $payload | ConvertTo-Json -Depth 20 -Compress
    $script:McpProcess.StandardInput.WriteLine($json)
    $script:McpProcess.StandardInput.Flush()

    $response = Read-McpResponse -RequestId $id
    if ($response.PSObject.Properties['error']) {
        throw "MCP request '$Method' failed: $($response.error | ConvertTo-Json -Depth 10 -Compress)"
    }

    return $response
}

function Read-McpResponse {
    param(
        [Parameter(Mandatory)]
        [int]$RequestId
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::Now -lt $deadline) {
        $task = $script:McpProcess.StandardOutput.ReadLineAsync()
        $remaining = $deadline - [DateTimeOffset]::Now
        if ($remaining.TotalMilliseconds -le 0) {
            break
        }

        if (-not $task.Wait([TimeSpan]::FromMilliseconds([Math]::Max(1, $remaining.TotalMilliseconds)))) {
            break
        }

        $line = $task.Result
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $response = $line | ConvertFrom-Json
        if ($response.PSObject.Properties['id'] -and [int]$response.id -eq $RequestId) {
            return $response
        }
    }

    throw "Timed out waiting for MCP response id $RequestId."
}

function Invoke-WinsightTool {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [hashtable]$Arguments = @{}
    )

    $response = Send-McpRequest -Method 'tools/call' -Params ([ordered]@{
        name = $Name
        arguments = $Arguments
    })

    $content = @($response.result.content)
    if ($content.Count -eq 0 -or [string]::IsNullOrWhiteSpace($content[0].text)) {
        throw "Winsight tool '$Name' returned no text payload."
    }

    return $content[0].text | ConvertFrom-Json
}

function Invoke-WinsightOperation {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [hashtable]$Arguments = @{}
    )

    $result = Invoke-WinsightTool -Name $Name -Arguments $Arguments
    if (-not $result.success) {
        throw "Winsight tool '$Name' failed: $($result.error)"
    }

    return $result
}

function Find-UiNode {
    param(
        [Parameter(Mandatory)]
        [object]$Node,

        [Parameter(Mandatory)]
        [string]$AutomationId
    )

    if ($Node.PSObject.Properties['automationId'] -and $Node.automationId -eq $AutomationId) {
        return $Node
    }

    if ($Node.PSObject.Properties['children']) {
        foreach ($child in @($Node.children)) {
            $match = Find-UiNode -Node $child -AutomationId $AutomationId
            if ($null -ne $match) {
                return $match
            }
        }
    }

    return $null
}

function Get-UiAutomationIds {
    param(
        [Parameter(Mandatory)]
        [object]$Node
    )

    $ids = [System.Collections.Generic.List[string]]::new()
    if ($Node.PSObject.Properties['automationId'] -and -not [string]::IsNullOrWhiteSpace($Node.automationId)) {
        $ids.Add([string]$Node.automationId)
    }

    if ($Node.PSObject.Properties['children']) {
        foreach ($child in @($Node.children)) {
            foreach ($id in Get-UiAutomationIds -Node $child) {
                $ids.Add($id)
            }
        }
    }

    return $ids
}

function Wait-WinsightWindow {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::Now -lt $deadline) {
        $windows = @(Invoke-WinsightTool -Name 'list_windows')
        $window = $windows | Where-Object {
            $_.processId -eq $ProcessId -or $_.title -like '*Win11Forge*'
        } | Select-Object -First 1

        if ($null -ne $window) {
            return $window
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for the Win11Forge window."
}

function Wait-WinsightElement {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId,

        [Parameter(Mandatory)]
        [string]$AutomationId
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    $lastTree = $null
    while ([DateTimeOffset]::Now -lt $deadline) {
        $tree = Invoke-WinsightTool -Name 'inspect_ui_tree' -Arguments @{
            processId = $ProcessId
            maxDepth = 10
        }
        $lastTree = $tree

        $node = Find-UiNode -Node $tree -AutomationId $AutomationId
        if ($null -ne $node) {
            return $node
        }

        Start-Sleep -Milliseconds 250
    }

    $knownIds = if ($null -ne $lastTree) {
        (Get-UiAutomationIds -Node $lastTree | Select-Object -First 40) -join ', '
    } else {
        '<none>'
    }

    throw "Timed out waiting for AutomationId '$AutomationId'. First visible AutomationIds: $knownIds"
}

function Click-WinsightElement {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId,

        [Parameter(Mandatory)]
        [string]$AutomationId
    )

    $null = Invoke-WinsightOperation -Name 'click_element' -Arguments @{
        processId = $ProcessId
        automationId = $AutomationId
    }
}

function Capture-WinsightScreenshot {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$OutputDirectory
    )

    $path = Join-Path $OutputDirectory "$Name.png"
    $result = Invoke-WinsightOperation -Name 'capture_screenshot' -Arguments @{
        processId = $ProcessId
        outputPath = $path
    }

    if (-not (Test-Path -LiteralPath $result.data.path)) {
        throw "Screenshot was not written: $($result.data.path)"
    }

    $file = Get-Item -LiteralPath $result.data.path
    if ($file.Length -lt 4096) {
        throw "Screenshot appears empty: $($result.data.path)"
    }

    Write-Host "Captured: $($result.data.path)" -ForegroundColor Green
    return $result.data.path
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$winsightRepo = Resolve-WinsightRoot -RequestedRoot $WinsightRoot -RepositoryRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repoRoot 'TestResults\winsight'
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
New-Item -Path $ArtifactDirectory -ItemType Directory -Force | Out-Null

$winsightMcpProject = Join-Path $winsightRepo 'src\Winsight.Mcp\Winsight.Mcp.csproj'
$winsightMcpAssembly = Join-Path $winsightRepo "src\Winsight.Mcp\bin\$Configuration\net10.0-windows\winsight-mcp.dll"
$guiProject = Join-Path $repoRoot 'GUI\Win11Forge.GUI\Win11Forge.GUI.csproj'
$guiAssembly = Join-Path $repoRoot "GUI\Win11Forge.GUI\bin\$Configuration\net10.0-windows\Win11Forge.GUI.dll"

if (-not $SkipBuild) {
    Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('build', $winsightMcpProject, '-c', $Configuration) -WorkingDirectory $winsightRepo
    Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('build', $guiProject, '-c', $Configuration) -WorkingDirectory $repoRoot
}

if (-not (Test-Path -LiteralPath $winsightMcpAssembly)) {
    throw "Winsight MCP assembly not found: $winsightMcpAssembly"
}

if (-not (Test-Path -LiteralPath $guiAssembly)) {
    throw "Win11Forge GUI assembly not found: $guiAssembly"
}

$appProcess = $null
try {
    Start-McpServer -McpAssemblyPath $winsightMcpAssembly

    $appProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList @("`"$guiAssembly`"") `
        -WorkingDirectory (Split-Path -Parent $guiAssembly) `
        -PassThru

    $window = Wait-WinsightWindow -ProcessId $appProcess.Id
    $processId = [int]$window.processId
    Write-Host "Win11Forge window found: pid=$processId title='$($window.title)'" -ForegroundColor Green

    $null = Wait-WinsightElement -ProcessId $processId -AutomationId 'NavDashboard'
    Click-WinsightElement -ProcessId $processId -AutomationId 'NavDashboard'
    $null = Wait-WinsightElement -ProcessId $processId -AutomationId 'PageDashboard'
    $null = Capture-WinsightScreenshot -ProcessId $processId -Name '01-dashboard' -OutputDirectory $ArtifactDirectory

    Click-WinsightElement -ProcessId $processId -AutomationId 'NavSettings'
    $null = Wait-WinsightElement -ProcessId $processId -AutomationId 'ThemePicker'
    $null = Capture-WinsightScreenshot -ProcessId $processId -Name '02-settings' -OutputDirectory $ArtifactDirectory

    Click-WinsightElement -ProcessId $processId -AutomationId 'NavAppCatalog'
    $null = Wait-WinsightElement -ProcessId $processId -AutomationId 'PageAppCatalog'
    $null = Capture-WinsightScreenshot -ProcessId $processId -Name '03-app-catalog' -OutputDirectory $ArtifactDirectory

    Write-Host "Winsight smoke completed successfully." -ForegroundColor Green
}
finally {
    if ($null -ne $appProcess) {
        try {
            if (-not $appProcess.HasExited) {
                $null = $appProcess.CloseMainWindow()
                if (-not $appProcess.WaitForExit(3000)) {
                    $appProcess.Kill($true)
                }
            }
        }
        finally {
            $appProcess.Dispose()
        }
    }

    if ($null -ne $script:McpProcess) {
        try {
            if (-not $script:McpProcess.HasExited) {
                $script:McpProcess.StandardInput.Close()
                if (-not $script:McpProcess.WaitForExit(3000)) {
                    $script:McpProcess.Kill($true)
                }
            }
        }
        finally {
            $script:McpProcess.Dispose()
        }
    }
}
