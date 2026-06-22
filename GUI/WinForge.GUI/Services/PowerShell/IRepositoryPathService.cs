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

namespace WinForge.GUI.Services.PowerShell;

/// <summary>
/// Service for resolving and managing the WinForge repository root path.
/// </summary>
public interface IRepositoryPathService
{
    /// <summary>
    /// Gets the repository root path where PowerShell scripts are located.
    /// </summary>
    string RepositoryRoot { get; }

    /// <summary>
    /// Gets the writable user data root path.
    /// </summary>
    string UserDataRoot { get; }

    /// <summary>
    /// Gets the directory where text and JSON logs are stored.
    /// </summary>
    string LogsDirectory { get; }

    /// <summary>
    /// Gets the full path to the application settings file.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Gets the full path to the deployment history file.
    /// </summary>
    string DeploymentHistoryFilePath { get; }

    /// <summary>
    /// Gets the writable user profiles directory.
    /// </summary>
    string UserProfilesDirectory { get; }

    /// <summary>
    /// Gets the packaged default profiles directory.
    /// </summary>
    string DefaultProfilesDirectory { get; }

    /// <summary>
    /// Gets the legacy install profiles directory.
    /// </summary>
    string LegacyInstallProfilesDirectory { get; }

    /// <summary>
    /// Gets whether user data resolution fell back from LocalAppData.
    /// </summary>
    bool IsUserDataFallbackActive { get; }

    /// <summary>
    /// Gets the repository root, throwing if not properly initialized.
    /// </summary>
    /// <returns>The validated repository root path.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the repository root is not initialized.</exception>
    string GetSafeRepositoryRoot();

    /// <summary>
    /// Gets the path to a file or directory relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The relative path from the repository root.</param>
    /// <returns>The full path.</returns>
    string GetPath(params string[] relativePath);

    /// <summary>
    /// Gets the path to a file or directory relative to the repository root with forward slashes.
    /// Useful for PowerShell script paths.
    /// </summary>
    /// <param name="relativePath">The relative path from the repository root.</param>
    /// <returns>The full path with forward slashes.</returns>
    string GetPathForPowerShell(params string[] relativePath);

    /// <summary>
    /// Gets the path to a file or directory relative to the writable user data root.
    /// </summary>
    /// <param name="relativePath">The relative path from the writable user data root.</param>
    /// <returns>The full path.</returns>
    string GetUserDataPath(params string[] relativePath);
}
