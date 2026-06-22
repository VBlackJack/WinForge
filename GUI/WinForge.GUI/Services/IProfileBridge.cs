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
/// Interface for profile management operations.
/// Separates profile concerns from general PowerShell operations for better SRP and testability.
/// </summary>
public interface IProfileBridge
{
    /// <summary>
    /// Gets the list of available deployment profiles.
    /// </summary>
    /// <returns>List of profile names (without .json extension)</returns>
    Task<List<string>> GetAvailableProfilesAsync();

    /// <summary>
    /// Loads a deployment profile with full inheritance resolution.
    /// All inherited applications are merged into the result.
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
    /// Saves a deployment profile to disk.
    /// Creates or overwrites the profile JSON file.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <param name="description">Profile description</param>
    /// <param name="parentProfile">Parent profile name (null for no inheritance)</param>
    /// <param name="addedAppIds">List of application IDs added in this profile</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveProfileAsync(
        string profileName,
        string description,
        string? parentProfile,
        List<string> addedAppIds);

    /// <summary>
    /// Deletes a deployment profile from disk.
    /// </summary>
    /// <param name="profileName">Name of the profile to delete</param>
    /// <returns>True if deletion succeeded, false otherwise</returns>
    Task<bool> DeleteProfileAsync(string profileName);

    /// <summary>
    /// Checks if a profile exists.
    /// </summary>
    /// <param name="profileName">Name of the profile to check</param>
    /// <returns>True if the profile exists</returns>
    Task<bool> ProfileExistsAsync(string profileName);

    /// <summary>
    /// Gets the parent profiles for a given profile.
    /// Used for inheritance chain resolution.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <returns>List of parent profile names, or empty list if none</returns>
    Task<IReadOnlyList<string>> GetParentProfilesAsync(string profileName);

    /// <summary>
    /// Gets metadata about a profile without loading the full application list.
    /// </summary>
    /// <param name="profileName">Name of the profile</param>
    /// <returns>Profile metadata including name, description, parent count, app count</returns>
    Task<ProfileMetadata> GetProfileMetadataAsync(string profileName);

    /// <summary>
    /// Gets the profile directory path.
    /// </summary>
    string ProfilesDirectory { get; }
}

/// <summary>
/// Metadata about a deployment profile.
/// </summary>
public record ProfileMetadata
{
    /// <summary>
    /// Name of the profile.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Profile description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Number of parent profiles this profile inherits from.
    /// </summary>
    public int ParentCount { get; init; }

    /// <summary>
    /// Number of applications directly defined in this profile (not inherited).
    /// </summary>
    public int DirectAppCount { get; init; }

    /// <summary>
    /// Total number of applications including inherited ones.
    /// </summary>
    public int TotalAppCount { get; init; }

    /// <summary>
    /// Profile version if specified.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// File path of the profile.
    /// </summary>
    public required string FilePath { get; init; }
}
