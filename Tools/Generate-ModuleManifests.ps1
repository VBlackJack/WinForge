#
# Generate module manifests for WinForge modules
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
    [string]$RootPath = (Split-Path $PSScriptRoot -Parent)
)

function ConvertTo-ManifestVersion {
    param([Parameter(Mandatory)][string]$DisplayVersion)

    $trimmed = $DisplayVersion.Trim()
    if ($trimmed -match '^(?<year>\d{4})(?<mmdd>\d{4})(?<sequence>\d{2})$') {
        $sequence = [int]$Matches.sequence
        if ($sequence -lt 1 -or $sequence -gt 99) {
            throw "Calendar version sequence must be between 01 and 99: $DisplayVersion"
        }

        return '1.0.{0}.{1}' -f $Matches.mmdd, $sequence
    }

    if ($trimmed -match '^\d+\.\d+\.\d+(?:\.\d+)?$') {
        return $trimmed
    }

    throw "Unsupported framework version format in Config/version.json: $DisplayVersion"
}

function Get-ManifestVersionInfo {
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $versionPath = Join-Path $RepositoryRoot 'Config\version.json'
    if (-not (Test-Path -Path $versionPath)) {
        throw "Version file not found: $versionPath"
    }

    try {
        $versionJson = Get-Content -Path $versionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $versionProperty = $versionJson.PSObject.Properties['Version']
        if (-not $versionProperty -or [string]::IsNullOrWhiteSpace([string]$versionProperty.Value)) {
            throw "Version property missing in $versionPath"
        }

        $displayVersion = [string]$versionProperty.Value
        return [PSCustomObject]@{
            DisplayVersion = $displayVersion
            ManifestVersion = ConvertTo-ManifestVersion -DisplayVersion $displayVersion
        }
    } catch {
        throw "Failed to resolve manifest version from $versionPath`: $($_.Exception.Message)"
    }
}

$versionInfo = Get-ManifestVersionInfo -RepositoryRoot $RootPath

$modules = @{
    # Core modules
    'Core\ApiEndpoints' = @{
        Description = 'REST API endpoint handlers for WinForge deployment server'
        Tags = @('WinForge', 'API', 'REST', 'Endpoints')
    }
    'Core\DirectoryConstants' = @{
        Description = 'Directory path constants for WinForge framework'
        Tags = @('WinForge', 'Core', 'Constants', 'Paths')
    }
    'Core\FeatureFlags' = @{
        Description = 'Feature flag management for WinForge runtime configuration'
        Tags = @('WinForge', 'Core', 'FeatureFlags', 'Configuration')
    }
    'Core\Localization' = @{
        Description = 'Internationalization (i18n) support for WinForge'
        Tags = @('WinForge', 'Core', 'i18n', 'Localization')
    }
    'Core\ModuleLoader' = @{
        Description = 'Dynamic module loading utilities for WinForge'
        Tags = @('WinForge', 'Core', 'ModuleLoader', 'Utility')
    }
    'Core\PluginManager' = @{
        Description = 'Plugin lifecycle management for WinForge extensibility'
        Tags = @('WinForge', 'Core', 'Plugin', 'Extensibility')
    }
    'Core\PluginSandbox' = @{
        Description = 'Sandboxed plugin execution with timeout enforcement'
        Tags = @('WinForge', 'Core', 'Plugin', 'Sandbox', 'Security')
    }
    'Core\RestApiServer' = @{
        Description = 'HTTP REST API server for WinForge remote management'
        Tags = @('WinForge', 'Core', 'REST', 'API', 'Server')
    }
    'Core\SecureStorage' = @{
        Description = 'DPAPI-based secure storage for sensitive configuration'
        Tags = @('WinForge', 'Core', 'Security', 'DPAPI', 'Encryption')
    }
    'Core\StructuredLogging' = @{
        Description = 'Structured JSON logging for WinForge operations'
        Tags = @('WinForge', 'Core', 'Logging', 'JSON', 'Structured')
    }
    'Core\TimeoutSettings' = @{
        Description = 'Timeout configuration management for WinForge operations'
        Tags = @('WinForge', 'Core', 'Timeout', 'Configuration')
    }
    'Core\WinForgeExceptions' = @{
        Description = 'Custom exception types for WinForge error handling'
        Tags = @('WinForge', 'Core', 'Exceptions', 'ErrorHandling')
    }
    # Feature modules
    'Modules\ApplicationDatabase' = @{
        Description = 'Application database management and queries'
        Tags = @('WinForge', 'Applications', 'Database')
    }
    'Modules\EnvironmentDetection' = @{
        Description = 'System environment detection and analysis'
        Tags = @('WinForge', 'Environment', 'Detection', 'System')
    }
    'Modules\InstallationOrchestrator' = @{
        Description = 'High-level installation orchestration and coordination'
        Tags = @('WinForge', 'Installation', 'Orchestrator')
    }
    'Modules\JsonSchemaValidation' = @{
        Description = 'JSON Schema validation for configuration files'
        Tags = @('WinForge', 'JSON', 'Schema', 'Validation')
    }
    'Modules\ParallelDetection' = @{
        Description = 'Parallel application detection using PowerShell 7+ features'
        Tags = @('WinForge', 'Detection', 'Parallel', 'Performance')
    }
    'Modules\Prerequisites' = @{
        Description = 'Prerequisites checking and installation'
        Tags = @('WinForge', 'Prerequisites', 'Dependencies')
    }
    'Modules\ProfileManager' = @{
        Description = 'Deployment profile loading and inheritance resolution'
        Tags = @('WinForge', 'Profiles', 'Inheritance')
    }
    'Modules\ScheduledDeployment' = @{
        Description = 'Scheduled deployment management and execution'
        Tags = @('WinForge', 'Scheduling', 'Deployment', 'Automation')
    }
    'Modules\StartMenuLayout' = @{
        Description = 'Windows Start menu layout management'
        Tags = @('WinForge', 'StartMenu', 'Layout', 'Windows11')
    }
    'Modules\StartMenuPinning' = @{
        Description = 'Application pinning to Windows Start menu and taskbar'
        Tags = @('WinForge', 'StartMenu', 'Pinning', 'Taskbar')
    }
    'Modules\StartupManager' = @{
        Description = 'Windows startup application management'
        Tags = @('WinForge', 'Startup', 'Autorun')
    }
    'Modules\StateManager' = @{
        Description = 'Deployment state persistence and recovery'
        Tags = @('WinForge', 'State', 'Persistence', 'Recovery')
    }
    'Modules\SystemConfig' = @{
        Description = 'Windows system configuration and optimization'
        Tags = @('WinForge', 'System', 'Configuration', 'Windows11')
    }
    'Modules\UpdateManager' = @{
        Description = 'Application update detection and management'
        Tags = @('WinForge', 'Updates', 'Management')
    }
    'Modules\UserProfileManager' = @{
        Description = 'User-specific profile and settings management'
        Tags = @('WinForge', 'UserProfile', 'Settings')
    }
    'Modules\WinForgeGUI' = @{
        Description = 'GUI launcher and integration utilities'
        Tags = @('WinForge', 'GUI', 'WPF', 'Integration')
    }
}

function New-ModuleManifestContent {
    param(
        [string]$ModuleName,
        [string]$Description,
        [string[]]$Tags,
        [string]$ManifestVersion,
        [string]$DisplayVersion
    )

    $guid = [guid]::NewGuid().ToString()
    $tagsStr = ($Tags | ForEach-Object { "'$_'" }) -join ', '

    return @"
#
# Module manifest for $ModuleName
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
    RootModule = '$ModuleName.psm1'
    ModuleVersion = '$ManifestVersion'
    GUID = '$guid'
    Author = 'Julien Bombled'
    CompanyName = 'WinForge'
    Copyright = '(c) 2026 Julien Bombled. All rights reserved.'
    Description = '$Description'
    PowerShellVersion = '5.1'
    RequiredModules = @()
    FunctionsToExport = '*'
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @($tagsStr)
            LicenseUri = 'https://www.apache.org/licenses/LICENSE-2.0'
            ProjectUri = 'https://github.com/VBlackJack/WinForge'
            ReleaseNotes = 'WinForge v$DisplayVersion'
        }
    }
}
"@
}

$created = 0
foreach ($module in $modules.GetEnumerator()) {
    $relativePath = $module.Key
    $info = $module.Value
    $moduleName = Split-Path $relativePath -Leaf
    $fullPath = Join-Path $RootPath $relativePath
    $psd1Path = "$fullPath.psd1"

    if (-not (Test-Path $psd1Path)) {
        $content = New-ModuleManifestContent -ModuleName $moduleName -Description $info.Description -Tags $info.Tags -ManifestVersion $versionInfo.ManifestVersion -DisplayVersion $versionInfo.DisplayVersion
        $content | Out-File -FilePath $psd1Path -Encoding UTF8 -NoNewline
        $created++
        Write-Host "[CREATED] $psd1Path" -ForegroundColor Green
    } else {
        Write-Host "[EXISTS] $psd1Path" -ForegroundColor Yellow
    }
}

Write-Host "`nCreated $created new manifests" -ForegroundColor Cyan
