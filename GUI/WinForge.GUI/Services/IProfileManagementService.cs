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
/// Service for deployment profile management operations.
/// Part of the decomposed IPowerShellBridge (ISP compliance).
/// </summary>
public interface IProfileManagementService
{
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
}
