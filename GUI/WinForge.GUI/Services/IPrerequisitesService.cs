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

#nullable enable

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Interface for prerequisites management operations.
/// Separates prerequisites concerns from general PowerShell operations for better SRP and testability.
/// </summary>
public interface IPrerequisitesService
{
    /// <summary>
    /// Checks the installation status of all system prerequisites.
    /// </summary>
    /// <returns>PrerequisitesStatus containing status of each prerequisite</returns>
    Task<PrerequisitesStatus> CheckPrerequisitesAsync();

    /// <summary>
    /// Installs all missing prerequisites.
    /// </summary>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all prerequisites were installed successfully</returns>
    Task<bool> InstallPrerequisitesAsync(
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a specific prerequisite by name.
    /// </summary>
    /// <param name="prerequisiteName">Name of the prerequisite to install</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if installation succeeded</returns>
    Task<bool> InstallPrerequisiteAsync(
        string prerequisiteName,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system information including OS version, memory, etc.
    /// </summary>
    /// <returns>SystemInfoModel with system details</returns>
    Task<SystemInfoModel> GetSystemInfoAsync();

    /// <summary>
    /// Gets the Win11Forge version.
    /// </summary>
    /// <returns>Version string</returns>
    Task<string> GetWin11ForgeVersionAsync();

    /// <summary>
    /// Checks if PowerShell 7+ is available on the system.
    /// </summary>
    /// <returns>True if PowerShell 7 or higher is installed</returns>
    Task<bool> IsPowerShell7AvailableAsync();

    /// <summary>
    /// Checks if running with administrator privileges.
    /// </summary>
    /// <returns>True if running as administrator</returns>
    bool IsRunningAsAdministrator();
}

/// <summary>
/// Result of a prerequisite installation attempt.
/// </summary>
public record PrerequisiteInstallResult
{
    /// <summary>
    /// Name of the prerequisite.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the installation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if installation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Version installed (if available).
    /// </summary>
    public string? InstalledVersion { get; init; }
}
