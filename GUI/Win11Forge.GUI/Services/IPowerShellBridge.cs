/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Composite interface for PowerShell script execution bridge.
/// Provides unified access to Win11Forge PowerShell modules.
///
/// This interface is composed of focused interfaces following the Interface Segregation Principle (ISP):
/// - IVersionService: Version information retrieval
/// - IProfileManagementService: Deployment profile CRUD operations
/// - IApplicationManagementService: Application lifecycle management
/// - ISystemInfoService: System information retrieval
///
/// Consumers should depend on the smallest interface that meets their needs.
/// This composite interface exists for backward compatibility and convenience
/// when a class legitimately needs all capabilities.
/// </summary>
public interface IPowerShellBridge :
    IVersionService,
    IProfileManagementService,
    IApplicationManagementService,
    ISystemInfoService
{
    /// <summary>
    /// Checks the status of system prerequisites.
    /// </summary>
    /// <returns>Prerequisites status model</returns>
    Task<PrerequisitesStatus> CheckPrerequisitesAsync();

    /// <summary>
    /// Installs missing system prerequisites.
    /// </summary>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <returns>True if installation succeeded</returns>
    Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null);

    /// <summary>
    /// Executes a PowerShell script from the repository.
    /// </summary>
    /// <param name="relativePath">Relative path to script from repository root</param>
    /// <returns>Script output</returns>
    Task<string> ExecuteScriptAsync(string relativePath);

    /// <summary>
    /// Executes a PowerShell command/script inline.
    /// </summary>
    /// <param name="command">PowerShell command or script to execute</param>
    /// <returns>Command output</returns>
    Task<string> ExecuteCommandAsync(string command);
}

/// <summary>
/// Batch detection result for a single application.
/// </summary>
public record BatchAppStatus(ApplicationStatus Status, string? Version);
