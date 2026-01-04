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
/// Interface for PowerShell script execution bridge.
/// Provides methods to interact with Win11Forge PowerShell modules.
/// </summary>
public interface IPowerShellBridge
{
    /// <summary>
    /// Gets the repository root path where PowerShell scripts are located.
    /// </summary>
    string RepositoryRoot { get; }

    /// <summary>
    /// Gets the Win11Forge version from Config/version.json.
    /// </summary>
    /// <returns>Version string (e.g., "2.6.0")</returns>
    Task<string> GetWin11ForgeVersionAsync();

    /// <summary>
    /// Gets the list of available deployment profiles.
    /// </summary>
    /// <returns>List of profile names (without .json extension)</returns>
    Task<List<string>> GetAvailableProfilesAsync();

    /// <summary>
    /// Loads a deployment profile with full inheritance resolution.
    /// </summary>
    /// <param name="profileName">Name of the profile to load</param>
    /// <returns>Deployment profile model with merged applications</returns>
    Task<DeploymentProfileModel> LoadProfileAsync(string profileName);

    /// <summary>
    /// Installs a single application.
    /// </summary>
    /// <param name="app">Application model to install</param>
    /// <param name="isDryRun">If true, simulates installation without making changes</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <returns>Installation result</returns>
    Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        Action<string>? progressCallback = null);

    /// <summary>
    /// Gets all applications from the database.
    /// </summary>
    /// <returns>List of all applications</returns>
    Task<List<ApplicationModel>> GetAllApplicationsAsync();

    /// <summary>
    /// Checks if an application is installed on the system.
    /// </summary>
    /// <param name="appId">Application ID to check</param>
    /// <returns>ApplicationStatus indicating installed state</returns>
    Task<ApplicationStatus> GetApplicationStatusAsync(string appId);

    /// <summary>
    /// Gets a raw profile without inheritance resolution.
    /// Used for editing to see what's defined in this specific profile.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <returns>Profile with only its own applications (not inherited)</returns>
    Task<DeploymentProfileModel> GetRawProfileAsync(string profileName);

    /// <summary>
    /// Gets a resolved profile with full inheritance.
    /// Alias for LoadProfileAsync for clarity.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <returns>Profile with all inherited applications merged</returns>
    Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName);

    /// <summary>
    /// Saves a deployment profile to disk.
    /// Creates or overwrites the profile JSON file.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <param name="description">Profile description</param>
    /// <param name="parentProfile">Parent profile name (null for no inheritance)</param>
    /// <param name="addedAppIds">List of application IDs added in this profile</param>
    Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds);

    /// <summary>
    /// Gets system information for the Dashboard display.
    /// </summary>
    /// <returns>System information model</returns>
    Task<SystemInfoModel> GetSystemInfoAsync();

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
}
