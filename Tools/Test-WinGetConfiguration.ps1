# Copyright 2026 Julien Bombled
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software distributed
# under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the License for the
# specific language governing permissions and limitations under the License.

<#
.SYNOPSIS
    Runs the limited WinGet desired-state test prototype against a reviewed configuration.
.DESCRIPTION
    Uses only 'configure test', never 'configure' apply. Configuration resources
    can be downloaded and their Test implementations executed by WinGet.
    Review the configuration and resource publishers before invoking this tool.
#>
[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$ConfigurationPath, [Parameter(Mandatory)][string]$ReportPath)
$ErrorActionPreference='Stop'
$configuration=(Resolve-Path -LiteralPath $ConfigurationPath -ErrorAction Stop).ProviderPath
$winget=(Get-Command winget -ErrorAction Stop).Source
if (-not $PSCmdlet.ShouldProcess($configuration,'Run WinGet configuration Test resources')) { return }
Import-Module (Join-Path (Split-Path $PSScriptRoot -Parent) 'Core/Core.psm1') -ErrorAction Stop
$native=Invoke-NativeCommandUtf8 -FilePath $winget -ArgumentList @('configure','test','-f',$configuration,'--accept-configuration-agreements','--disable-interactivity') -TimeoutSeconds 600
$output=$native.Output
$code=$native.ExitCode
@{Configuration=$configuration;SHA256=(Get-FileHash -LiteralPath $configuration -Algorithm SHA256).Hash;At=[datetime]::UtcNow.ToString('o');ExitCode=$code;Output=($output -join [Environment]::NewLine)} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
# Preserve native failure instead of converting a failed resource test into success.
exit $code
