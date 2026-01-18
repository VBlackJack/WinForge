<#
.SYNOPSIS
    Win11Forge Plugin Template - Main Module

.DESCRIPTION
    Template entry point for Win11Forge plugins.
    Rename this directory and customize for your plugin.

.NOTES
    Author: Your Name
    Version: 1.0.0
#>

#
# Copyright 2026 Your Name
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

Set-StrictMode -Version Latest

# === PLUGIN INITIALIZATION ===
$script:PluginRoot = Split-Path -Parent $PSCommandPath
$script:PluginName = 'plugin-template'

# === HOOK HANDLERS ===
# Naming convention: Invoke-{PluginName}{HookName} (without hyphens)
# Example: For plugin 'my-plugin' and hook 'pre-install' -> Invoke-mypluginpreinstall

function Invoke-plugintemplatepreinstall {
    <#
    .SYNOPSIS
        Pre-install hook handler.

    .PARAMETER Context
        Context hashtable with installation details.
    #>
    param([hashtable]$Context)

    # Your pre-install logic here
    Write-Verbose "[$script:PluginName] Pre-install hook called for: $($Context.AppName)"

    # Return $true to continue, $false to cancel
    return $true
}

function Invoke-plugintemplatepostinstall {
    <#
    .SYNOPSIS
        Post-install hook handler.

    .PARAMETER Context
        Context hashtable with installation details.
    #>
    param([hashtable]$Context)

    # Your post-install logic here
    Write-Verbose "[$script:PluginName] Post-install hook called for: $($Context.AppName)"

    return $true
}

# === CUSTOM INSTALLATION METHODS ===
# Naming convention: Install-{PluginName}{MethodName} (without hyphens)
# Example: For plugin 'my-plugin' and method 'CustomSource' -> Install-mypluginCustomSource

# function Install-plugintemplateCustomSource {
#     <#
#     .SYNOPSIS
#         Custom installation method handler.
#     #>
#     param(
#         [Parameter(Mandatory)]
#         [PSCustomObject]$App,
#
#         [Parameter()]
#         [hashtable]$Options
#     )
#
#     # Your custom installation logic here
#     return @{
#         Success = $true
#         Message = "Installed via custom method"
#     }
# }

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    'Invoke-plugintemplatepreinstall',
    'Invoke-plugintemplatepostinstall'
)
