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

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Services.Implementations;

/// <summary>
/// Implementation of IProfileManagementService for deployment profile operations.
/// </summary>
public class ProfileManagementServiceImpl : IProfileManagementService
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly IApplicationCacheService _cacheService;
    private readonly IVersionService _versionService;

    /// <summary>
    /// Initializes a new instance of the ProfileManagementServiceImpl.
    /// </summary>
    public ProfileManagementServiceImpl(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        IApplicationCacheService cacheService,
        IVersionService versionService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableProfilesAsync()
    {
        return await Task.Run(() =>
        {
            var profiles = GetProfileReadDirectories()
                .SelectMany(directory => Directory.GetFiles(directory, $"*{Win11ForgePathNames.JsonFileExtension}"))
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            return profiles;
        });
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> LoadProfileAsync(string profileName)
    {
        // Validate profile name to prevent path traversal
        profileName = PowerShellValidation.ValidateProfileName(profileName);

        // Ensure applications database is loaded
        await _cacheService.EnsureApplicationsCacheAsync();

        // Load profile directly from JSON (no PowerShell needed)
        return await LoadProfileFromJsonAsync(profileName, new List<string>());
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetRawProfileAsync(string profileName)
    {
        // Validate profile name to prevent path traversal
        profileName = PowerShellValidation.ValidateProfileName(profileName);

        await _cacheService.EnsureApplicationsCacheAsync();

        var profilePath = ResolveProfilePath(profileName);

        // Load raw profile directly from JSON (no inheritance resolution)
        if (string.IsNullOrEmpty(profilePath))
        {
            throw new FileNotFoundException($"Profile not found: {profileName}");
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var profile = new DeploymentProfileModel
        {
            Name = JsonHelper.GetJsonString(root, "Name") ?? profileName,
            Description = JsonHelper.GetJsonString(root, "Description") ?? string.Empty,
            Version = JsonHelper.GetJsonString(root, "Version") ?? "1.0.0"
        };

        // Get parent profile (Inherits property) but don't resolve it
        if (root.TryGetProperty("Inherits", out var inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parentName in inheritsElement.EnumerateArray())
            {
                var parentNameStr = parentName.GetString();
                if (!string.IsNullOrEmpty(parentNameStr))
                {
                    profile.InheritedFrom.Add(parentNameStr);
                }
            }
        }

        // Get applications defined in this profile only (no inheritance)
        var appIds = new List<string>();
        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                string? appId = null;

                if (appElement.ValueKind == JsonValueKind.String)
                {
                    appId = appElement.GetString();
                }
                else if (appElement.ValueKind == JsonValueKind.Object)
                {
                    appId = JsonHelper.GetJsonString(appElement, "AppId");
                }

                if (!string.IsNullOrEmpty(appId))
                {
                    appIds.Add(appId);
                }
            }
        }

        // Convert app IDs to ApplicationModels
        profile.Applications = new ObservableCollection<ApplicationModel>(
            appIds.Select(appId => CreateApplicationModel(appId))
                  .OrderBy(a => a.Priority)
        );

        return profile;
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName)
    {
        return await LoadProfileAsync(profileName);
    }

    /// <inheritdoc/>
    public async Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds)
    {
        // Validate profile names to prevent path traversal
        profileName = PowerShellValidation.ValidateProfileName(profileName);
        if (!string.IsNullOrEmpty(parentProfile))
        {
            parentProfile = PowerShellValidation.ValidateProfileName(parentProfile);
        }

        var profilesDir = _pathService.UserProfilesDirectory;
        var profilePath = Path.Combine(
            profilesDir,
            $"{profileName}{Win11ForgePathNames.JsonFileExtension}");

        // Defense-in-depth: verify the resolved path stays within profiles directory
        profilePath = PowerShellValidation.ValidatePathWithinDirectory(profilePath, profilesDir);

        // Ensure profiles directory exists
        if (!Directory.Exists(profilesDir))
        {
            Directory.CreateDirectory(profilesDir);
        }

        // Get current version for the profile
        var version = await _versionService.GetWin11ForgeVersionAsync();
        if (version == "Unknown" || version == "Error")
        {
            version = "2.6.0";
        }

        await Task.Run(() =>
        {
            // Build the profile JSON structure
            var profileObj = new Dictionary<string, object>
            {
                ["Name"] = profileName,
                ["Description"] = description,
                ["Version"] = version
            };

            // Add inheritance if parent is specified
            if (!string.IsNullOrEmpty(parentProfile) &&
                parentProfile != Loc.Editor_NoParent)
            {
                profileObj["Inherits"] = new[] { parentProfile };
            }

            // Add applications as string array of AppIds (database mode)
            if (addedAppIds.Count > 0)
            {
                profileObj["Applications"] = addedAppIds.ToArray();
            }
            else
            {
                profileObj["Applications"] = Array.Empty<string>();
            }

            // Serialize with indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase
            };

            var jsonContent = JsonSerializer.Serialize(profileObj, options);

            // Write to file
            File.WriteAllText(profilePath, jsonContent);
        });
    }

    /// <summary>
    /// Loads a profile from JSON file with inheritance support.
    /// </summary>
    private async Task<DeploymentProfileModel> LoadProfileFromJsonAsync(
        string profileName,
        List<string> inheritanceChain)
    {
        var profilePath = ResolveProfilePath(profileName);
        if (string.IsNullOrEmpty(profilePath))
        {
            throw new FileNotFoundException($"Profile not found: {profileName}");
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var profile = new DeploymentProfileModel
        {
            Name = JsonHelper.GetJsonString(root, "Name") ?? profileName,
            Description = JsonHelper.GetJsonString(root, "Description") ?? string.Empty,
            Version = JsonHelper.GetJsonString(root, "Version") ?? "1.0.0"
        };

        // Track inheritance chain
        inheritanceChain.Add(profileName);

        // Handle inheritance
        var allAppIds = new List<string>();

        if (root.TryGetProperty("Inherits", out var inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parentName in inheritsElement.EnumerateArray())
            {
                var parentNameStr = parentName.GetString();
                if (!string.IsNullOrEmpty(parentNameStr) && !inheritanceChain.Contains(parentNameStr))
                {
                    profile.InheritedFrom.Add(parentNameStr);

                    // Load parent profile and merge apps
                    var parentProfile = await LoadProfileFromJsonAsync(parentNameStr, inheritanceChain);
                    foreach (var app in parentProfile.Applications)
                    {
                        if (!allAppIds.Contains(app.AppId))
                        {
                            allAppIds.Add(app.AppId);
                        }
                    }
                }
            }
        }

        // Add this profile's applications
        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                string? appId = null;

                if (appElement.ValueKind == JsonValueKind.String)
                {
                    appId = appElement.GetString();
                }
                else if (appElement.ValueKind == JsonValueKind.Object)
                {
                    appId = JsonHelper.GetJsonString(appElement, "AppId");
                }

                if (!string.IsNullOrEmpty(appId) && !allAppIds.Contains(appId))
                {
                    allAppIds.Add(appId);
                }
            }
        }

        // Convert app IDs to ApplicationModels
        profile.Applications = new ObservableCollection<ApplicationModel>(
            allAppIds.Select(appId => CreateApplicationModel(appId))
                     .OrderBy(a => a.Priority)
        );

        return profile;
    }

    /// <summary>
    /// Creates an ApplicationModel from an app ID using the applications cache.
    /// </summary>
    private ApplicationModel CreateApplicationModel(string appId)
    {
        var app = new ApplicationModel
        {
            AppId = appId,
            Name = appId,
            Category = Loc.Common_Unknown,
            Priority = 50,
            IsRequired = false,
            Status = ApplicationStatus.Pending,
            IsSelected = true
        };

        // Enrich from applications database
        if (_cacheService.TryGetApplicationData(appId, out var appData))
        {
            app.Name = JsonHelper.GetJsonString(appData, "Name") ?? appId;
            app.Category = JsonHelper.GetJsonString(appData, "Category") ?? "Unknown";
            app.Description = JsonHelper.GetJsonString(appData, "Description") ?? string.Empty;

            if (appData.TryGetProperty("DefaultPriority", out var priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            if (appData.TryGetProperty("DefaultRequired", out var requiredProp) &&
                requiredProp.ValueKind == JsonValueKind.True)
            {
                app.IsRequired = true;
            }
        }

        return app;
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

    private string? ResolveProfilePath(string profileName)
    {
        foreach (var profilesDir in GetProfileReadDirectories())
        {
            var profilePath = Path.Combine(
                profilesDir,
                $"{profileName}{Win11ForgePathNames.JsonFileExtension}");

            profilePath = PowerShellValidation.ValidatePathWithinDirectory(profilePath, profilesDir);
            if (File.Exists(profilePath))
            {
                return profilePath;
            }
        }

        return null;
    }
}
