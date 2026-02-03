#
# Generate module manifests for Win11Forge modules
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

$modules = @{
    # Core modules
    'Core\ApiEndpoints' = @{
        Description = 'REST API endpoint handlers for Win11Forge deployment server'
        Tags = @('Win11Forge', 'API', 'REST', 'Endpoints')
    }
    'Core\DirectoryConstants' = @{
        Description = 'Directory path constants for Win11Forge framework'
        Tags = @('Win11Forge', 'Core', 'Constants', 'Paths')
    }
    'Core\FeatureFlags' = @{
        Description = 'Feature flag management for Win11Forge runtime configuration'
        Tags = @('Win11Forge', 'Core', 'FeatureFlags', 'Configuration')
    }
    'Core\Localization' = @{
        Description = 'Internationalization (i18n) support for Win11Forge'
        Tags = @('Win11Forge', 'Core', 'i18n', 'Localization')
    }
    'Core\ModuleLoader' = @{
        Description = 'Dynamic module loading utilities for Win11Forge'
        Tags = @('Win11Forge', 'Core', 'ModuleLoader', 'Utility')
    }
    'Core\PluginManager' = @{
        Description = 'Plugin lifecycle management for Win11Forge extensibility'
        Tags = @('Win11Forge', 'Core', 'Plugin', 'Extensibility')
    }
    'Core\PluginSandbox' = @{
        Description = 'Sandboxed plugin execution with timeout enforcement'
        Tags = @('Win11Forge', 'Core', 'Plugin', 'Sandbox', 'Security')
    }
    'Core\RestApiServer' = @{
        Description = 'HTTP REST API server for Win11Forge remote management'
        Tags = @('Win11Forge', 'Core', 'REST', 'API', 'Server')
    }
    'Core\SecureStorage' = @{
        Description = 'DPAPI-based secure storage for sensitive configuration'
        Tags = @('Win11Forge', 'Core', 'Security', 'DPAPI', 'Encryption')
    }
    'Core\StructuredLogging' = @{
        Description = 'Structured JSON logging for Win11Forge operations'
        Tags = @('Win11Forge', 'Core', 'Logging', 'JSON', 'Structured')
    }
    'Core\TimeoutSettings' = @{
        Description = 'Timeout configuration management for Win11Forge operations'
        Tags = @('Win11Forge', 'Core', 'Timeout', 'Configuration')
    }
    'Core\Win11ForgeExceptions' = @{
        Description = 'Custom exception types for Win11Forge error handling'
        Tags = @('Win11Forge', 'Core', 'Exceptions', 'ErrorHandling')
    }
    # Feature modules
    'Modules\ApplicationDatabase' = @{
        Description = 'Application database management and queries'
        Tags = @('Win11Forge', 'Applications', 'Database')
    }
    'Modules\EnvironmentDetection' = @{
        Description = 'System environment detection and analysis'
        Tags = @('Win11Forge', 'Environment', 'Detection', 'System')
    }
    'Modules\InstallationOrchestrator' = @{
        Description = 'High-level installation orchestration and coordination'
        Tags = @('Win11Forge', 'Installation', 'Orchestrator')
    }
    'Modules\JsonSchemaValidation' = @{
        Description = 'JSON Schema validation for configuration files'
        Tags = @('Win11Forge', 'JSON', 'Schema', 'Validation')
    }
    'Modules\ParallelDetection' = @{
        Description = 'Parallel application detection using PowerShell 7+ features'
        Tags = @('Win11Forge', 'Detection', 'Parallel', 'Performance')
    }
    'Modules\Prerequisites' = @{
        Description = 'Prerequisites checking and installation'
        Tags = @('Win11Forge', 'Prerequisites', 'Dependencies')
    }
    'Modules\ProfileManager' = @{
        Description = 'Deployment profile loading and inheritance resolution'
        Tags = @('Win11Forge', 'Profiles', 'Inheritance')
    }
    'Modules\ScheduledDeployment' = @{
        Description = 'Scheduled deployment management and execution'
        Tags = @('Win11Forge', 'Scheduling', 'Deployment', 'Automation')
    }
    'Modules\StartMenuLayout' = @{
        Description = 'Windows Start menu layout management'
        Tags = @('Win11Forge', 'StartMenu', 'Layout', 'Windows11')
    }
    'Modules\StartMenuPinning' = @{
        Description = 'Application pinning to Windows Start menu and taskbar'
        Tags = @('Win11Forge', 'StartMenu', 'Pinning', 'Taskbar')
    }
    'Modules\StartupManager' = @{
        Description = 'Windows startup application management'
        Tags = @('Win11Forge', 'Startup', 'Autorun')
    }
    'Modules\StateManager' = @{
        Description = 'Deployment state persistence and recovery'
        Tags = @('Win11Forge', 'State', 'Persistence', 'Recovery')
    }
    'Modules\SystemConfig' = @{
        Description = 'Windows system configuration and optimization'
        Tags = @('Win11Forge', 'System', 'Configuration', 'Windows11')
    }
    'Modules\UpdateManager' = @{
        Description = 'Application update detection and management'
        Tags = @('Win11Forge', 'Updates', 'Management')
    }
    'Modules\UserProfileManager' = @{
        Description = 'User-specific profile and settings management'
        Tags = @('Win11Forge', 'UserProfile', 'Settings')
    }
    'Modules\Win11ForgeGUI' = @{
        Description = 'GUI launcher and integration utilities'
        Tags = @('Win11Forge', 'GUI', 'WPF', 'Integration')
    }
}

function New-ModuleManifestContent {
    param(
        [string]$ModuleName,
        [string]$Description,
        [string[]]$Tags
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
    ModuleVersion = '3.5.2'
    GUID = '$guid'
    Author = 'Julien Bombled'
    CompanyName = 'Win11Forge'
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
            ProjectUri = 'https://github.com/JulienBombled/Win11Forge'
            ReleaseNotes = 'Win11Forge v3.5.2'
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
        $content = New-ModuleManifestContent -ModuleName $moduleName -Description $info.Description -Tags $info.Tags
        $content | Out-File -FilePath $psd1Path -Encoding UTF8 -NoNewline
        $created++
        Write-Host "[CREATED] $psd1Path" -ForegroundColor Green
    } else {
        Write-Host "[EXISTS] $psd1Path" -ForegroundColor Yellow
    }
}

Write-Host "`nCreated $created new manifests" -ForegroundColor Cyan
