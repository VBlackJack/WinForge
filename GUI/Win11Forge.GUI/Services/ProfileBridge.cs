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

using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Implementation of profile management operations.
/// Wraps IPowerShellBridge for core operations and adds profile-specific logic.
/// </summary>
public class ProfileBridge : IProfileBridge
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IProfileValidationService _validationService;
    private readonly IRepositoryPathService _pathService;

    /// <summary>
    /// Initializes a new instance of ProfileBridge.
    /// </summary>
    /// <param name="powerShellBridge">PowerShell bridge for script execution</param>
    /// <param name="validationService">Profile validation service</param>
    public ProfileBridge(
        IPowerShellBridge powerShellBridge,
        IProfileValidationService validationService,
        IRepositoryPathService? pathService = null)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _pathService = pathService ?? new RepositoryPathService();
    }

    /// <inheritdoc/>
    public string ProfilesDirectory => _pathService.UserProfilesDirectory;

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableProfilesAsync()
    {
        return await _powerShellBridge.GetAvailableProfilesAsync();
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> LoadProfileAsync(string profileName)
    {
        ValidateProfileName(profileName);
        return await _powerShellBridge.LoadProfileAsync(profileName);
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetRawProfileAsync(string profileName)
    {
        ValidateProfileName(profileName);
        return await _powerShellBridge.GetRawProfileAsync(profileName);
    }

    /// <inheritdoc/>
    public async Task SaveProfileAsync(
        string profileName,
        string description,
        string? parentProfile,
        List<string> addedAppIds)
    {
        ValidateProfileName(profileName);

        if (parentProfile != null)
        {
            ValidateProfileName(parentProfile);
        }

        await _powerShellBridge.SaveProfileAsync(profileName, description, parentProfile, addedAppIds);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteProfileAsync(string profileName)
    {
        ValidateProfileName(profileName);

        var profilePath = GetWritableProfilePath(profileName);
        if (!File.Exists(profilePath))
        {
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                File.Delete(profilePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        });
    }

    /// <inheritdoc/>
    public Task<bool> ProfileExistsAsync(string profileName)
    {
        ValidateProfileName(profileName);
        return Task.FromResult(TryGetExistingProfilePath(profileName) != null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetParentProfilesAsync(string profileName)
    {
        ValidateProfileName(profileName);

        try
        {
            var rawProfile = await GetRawProfileAsync(profileName);
            return rawProfile.InheritedFrom?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public async Task<ProfileMetadata> GetProfileMetadataAsync(string profileName)
    {
        ValidateProfileName(profileName);

        var profilePath = TryGetExistingProfilePath(profileName);
        if (profilePath == null)
        {
            throw new FileNotFoundException(
                Win11Forge.GUI.Resources.Resources.Error_ProfileNotFound,
                profileName);
        }

        var rawProfile = await GetRawProfileAsync(profileName);

        int totalAppCount = rawProfile.Applications?.Count ?? 0;
        try
        {
            var resolvedProfile = await LoadProfileAsync(profileName);
            totalAppCount = resolvedProfile.Applications?.Count ?? 0;
        }
        catch
        {
            // Use raw count if resolution fails
        }

        return new ProfileMetadata
        {
            Name = rawProfile.Name,
            Description = rawProfile.Description,
            ParentCount = rawProfile.InheritedFrom?.Count ?? 0,
            DirectAppCount = rawProfile.Applications?.Count ?? 0,
            TotalAppCount = totalAppCount,
            Version = rawProfile.Version,
            FilePath = profilePath
        };
    }

    /// <summary>
    /// Gets the full path for a profile JSON file.
    /// Validates the path stays within the profiles directory.
    /// </summary>
    private string GetWritableProfilePath(string profileName)
    {
        var profilesDirectory = _pathService.UserProfilesDirectory;
        var profilePath = Path.Combine(
            profilesDirectory,
            $"{profileName}{Win11ForgePathNames.JsonFileExtension}");

        // Defense-in-depth: verify the resolved path stays within profiles directory
        return ValidatePathWithinDirectory(profilePath, profilesDirectory);
    }

    private string? TryGetExistingProfilePath(string profileName)
    {
        foreach (var profilesDirectory in GetProfileReadDirectories())
        {
            var profilePath = Path.Combine(
                profilesDirectory,
                $"{profileName}{Win11ForgePathNames.JsonFileExtension}");
            profilePath = ValidatePathWithinDirectory(profilePath, profilesDirectory);

            if (File.Exists(profilePath))
            {
                return profilePath;
            }
        }

        return null;
    }

    private IReadOnlyList<string> GetProfileReadDirectories()
    {
        return new[]
            {
                _pathService.UserProfilesDirectory,
                _pathService.DefaultProfilesDirectory
            }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Validates that a file path is within the expected directory to prevent path traversal.
    /// </summary>
    private static string ValidatePathWithinDirectory(string filePath, string expectedBaseDir)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullBaseDir = Path.GetFullPath(expectedBaseDir);

        // Ensure the base directory ends with a separator for proper prefix checking
        if (!fullBaseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            fullBaseDir += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path traversal detected: path is outside of allowed directory", nameof(filePath));
        }

        return fullPath;
    }

    /// <summary>
    /// Validates a profile name for security.
    /// Prevents path traversal attacks.
    /// </summary>
    private static void ValidateProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException(
                Win11Forge.GUI.Resources.Resources.Validation_ProfileNameRequired,
                nameof(profileName));
        }

        // Check for path traversal attempts
        if (profileName.Contains("..") ||
            profileName.Contains('/') ||
            profileName.Contains('\\'))
        {
            throw new ArgumentException(
                Win11Forge.GUI.Resources.Resources.Validation_ProfileNameInvalidChars,
                nameof(profileName));
        }

        // Check length
        const int maxLength = 100;
        if (profileName.Length > maxLength)
        {
            throw new ArgumentException(
                string.Format(
                    Win11Forge.GUI.Resources.Resources.Validation_ProfileNameTooLong,
                    maxLength),
                nameof(profileName));
        }

        // Check for invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (profileName.Any(c => invalidChars.Contains(c)))
        {
            throw new ArgumentException(
                Win11Forge.GUI.Resources.Resources.Validation_ProfileNameInvalidChars,
                nameof(profileName));
        }
    }
}
