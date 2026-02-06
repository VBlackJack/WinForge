<#
.SYNOPSIS
    Win11Forge - Installation Engine v3.6.8 (Modular Architecture)

.DESCRIPTION
    Core installation engine orchestration with multi-source support and parallel execution.

    This module has been split into submodules for maintainability:
    - ApplicationDetection.psm1: Detection and verification functions
    - InstallationMethods.psm1: Individual installation method implementations
    - InstallationOrchestrator.psm1: High-level orchestration and state management

    Features:
    - Winget (primary) with retry logic (sequential AND parallel)
    - Chocolatey (fallback) with retry logic (sequential AND parallel)
    - Microsoft Store (UWP apps)
    - Direct download + silent install with SHA256 validation (sequential AND parallel)
    - Application detection with special cases (PowerToys, Quick Assist)
    - Windows Features/Capabilities
    - Parallel installation (up to 5 apps simultaneously)
    - Rollback and deployment state management
    - Environment restriction checking

.NOTES
    Author: Julien Bombled
    v3.6.8

    Changelog v3.2.2 (Complete Modular Architecture):
    - ARCHITECTURE: InstallationOrchestrator.psm1 contains high-level coordination logic
    - ARCHITECTURE: State management (rollback, deployment resume) centralized in orchestrator
    - ARCHITECTURE: InstallationEngine.psm1 is now a thin wrapper that imports submodules
    - ARCHITECTURE: Clean separation of concerns across 4 modules

    Changelog v3.1.5 (Modular Architecture):
    - ARCHITECTURE: Split into 3 modules for maintainability (InstallationEngine, ApplicationDetection, InstallationMethods)
    - ARCHITECTURE: ApplicationDetection.psm1 contains all detection and verification functions
    - ARCHITECTURE: InstallationMethods.psm1 contains individual install method implementations
    - ARCHITECTURE: InstallationEngine.psm1 retains orchestration logic and state management

    Changelog v3.1.4 (Critical Security Fixes):
    - SECURITY: CRITICAL - Replaced cmd /c with Start-Process argument arrays in Invoke-Rollback
    - SECURITY: Added Test-ValidStateData for deployment state file validation
    - SECURITY: Added path traversal protection to parallel Test-AppInstalledParallel
    - SECURITY: Added executable whitelist for Command detection method
    - SECURITY: Added ValidateAppId in C# PowerShellBridge
    - CONFIG: Standardized parallel timeout to use $script:ParallelInstallTimeoutMs

    Changelog v3.1.3 (Security Hardening Update):
    - SECURITY: Added path traversal protection in Expand-DetectionPath
    - SECURITY: URL validation now blocks non-whitelisted domains by default
    - SECURITY: Trusted domains loaded from Config/download-sources.json
    - SECURITY: Full GUID used for temp directories (32 chars vs 8 chars)
    - SECURITY: Added -AllowUntrusted parameter for explicit override

    Changelog v3.0.0 (Parallel Reliability Update):
    - RELIABILITY: Added retry logic to parallel Winget (3 attempts with exponential backoff)
    - RELIABILITY: Added retry logic to parallel Chocolatey (3 attempts with exponential backoff)
    - SECURITY: Added SHA256 checksum validation for parallel DirectUrl downloads
    - REFACTORING: Created Invoke-InstallationMethodSequence helper for sequential installs
    - REFACTORING: Created Invoke-CustomInstallMethod helper for WindowsFeature/Capability
    - REFACTORING: Reduced Install-Application from 189 to ~70 lines
    - QUALITY: 458 Pester tests passing (100% pass rate)
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

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
# This module is a thin wrapper. The actual functionality is provided by nested modules
# defined in InstallationEngine.psd1:
# - ApplicationDetection.psm1
# - InstallationMethods.psm1
# - InstallationOrchestrator.psm1

$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent

# Import core modules for utilities
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
$script:FeatureFlagsPath = Join-Path $script:RepositoryRoot 'Core\FeatureFlags.psm1'
$script:DirectoryConstantsPath = Join-Path $script:RepositoryRoot 'Core\DirectoryConstants.psm1'
$script:EnvironmentDetectionPath = Join-Path $script:ModuleRoot 'EnvironmentDetection.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

if (-not (Get-Command -Name Test-FeatureEnabled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:FeatureFlagsPath) {
        Import-Module -Name $script:FeatureFlagsPath -Force
    }
}

if (-not (Get-Command -Name Get-Win11ForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

if (-not (Get-Command -Name Get-LocalizedString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

if (-not (Get-Command -Name Test-IsWindowsSandbox -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:EnvironmentDetectionPath) {
        Import-Module -Name $script:EnvironmentDetectionPath -Force
    }
}

# Import WingetCache for optimized winget list caching
$script:WingetCachePath = Join-Path $script:ModuleRoot 'WingetCache.psm1'
if (-not (Get-Command -Name Get-CachedWingetList -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:WingetCachePath) {
        Import-Module -Name $script:WingetCachePath -Force
    }
}

# Import sub-modules explicitly when loaded directly (not via manifest)
# This ensures functions are available whether importing .psm1 or .psd1
$script:ApplicationDetectionPath = Join-Path $script:ModuleRoot 'ApplicationDetection.psm1'
if (-not (Get-Command -Name Test-ApplicationInstalled -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:ApplicationDetectionPath) {
        Import-Module -Name $script:ApplicationDetectionPath -Force -Global
    }
}

$script:InstallationMethodsPath = Join-Path $script:ModuleRoot 'InstallationMethods.psm1'
if (-not (Get-Command -Name Install-ViaWinget -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:InstallationMethodsPath) {
        Import-Module -Name $script:InstallationMethodsPath -Force -Global
    }
}

$script:InstallationOrchestratorPath = Join-Path $script:ModuleRoot 'InstallationOrchestrator.psm1'
if (-not (Get-Command -Name Install-Application -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:InstallationOrchestratorPath) {
        Import-Module -Name $script:InstallationOrchestratorPath -Force -Global
    }
}

# === CONFIGURATION (shared across modules) ===
$maxParallelJobs = $null
if (Get-Command -Name Get-TimeoutSetting -ErrorAction SilentlyContinue) {
    $maxParallelJobs = Get-TimeoutSetting -Name 'MaxParallelJobs' -ErrorAction SilentlyContinue
}
$script:MaxParallelJobs = if ($null -ne $maxParallelJobs) { [int]$maxParallelJobs } else { 5 }

$defaultInstallTimeoutSeconds = $null
if (Get-Command -Name Get-TimeoutSetting -ErrorAction SilentlyContinue) {
    $defaultInstallTimeoutSeconds = Get-TimeoutSetting -Name 'DefaultInstallTimeoutSeconds' -ErrorAction SilentlyContinue
}
$script:DefaultInstallTimeoutSeconds = if ($null -ne $defaultInstallTimeoutSeconds) { [int]$defaultInstallTimeoutSeconds } else { 1800 }

$script:JobCheckInterval = 2
$script:OfficeInstallTimeoutSeconds = 2700
$script:ParallelInstallTimeoutMs = 600000

