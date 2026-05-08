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
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    [ObservableProperty]
    private ObservableCollection<string> _availableProfiles = [];

    /// <summary>
    /// Currently selected profile (null = no profile, manual selection).
    /// </summary>
    [ObservableProperty]
    private string? _selectedProfile;

    /// <summary>
    /// Indicates whether a profile is currently applied.
    /// </summary>
    public bool HasProfileApplied => !string.IsNullOrEmpty(SelectedProfile);

    /// <summary>
    /// Indicates whether there are any profiles available.
    /// </summary>
    public bool HasProfiles => AvailableProfiles.Count > 0;

    /// <summary>
    /// Notifies dependent properties when AvailableProfiles changes.
    /// </summary>
    partial void OnAvailableProfilesChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(HasProfiles));
    }

    /// <summary>
    /// Cache of resolved profile app IDs with their tier.
    /// </summary>
    private Dictionary<string, string> _profileAppTiers = [];

    /// <summary>
    /// Cache of resolved profile app IDs (with inheritance).
    /// Key = profile name, Value = set of AppIds included in that profile.
    /// </summary>
    private Dictionary<string, HashSet<string>> _resolvedProfileAppIdsCache = [];

    /// <summary>
    /// Cache of raw profile app IDs (without inheritance, for tier mapping).
    /// Key = profile name, Value = list of AppIds defined directly in that profile.
    /// </summary>
    private Dictionary<string, List<string>> _rawProfileAppIdsCache = [];

    private string? _lastAppliedProfile;
    private bool _isRestoringProfile;

    /// <summary>
    /// Called when SelectedProfile changes.
    /// </summary>
    partial void OnSelectedProfileChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProfileApplied));
        if (_isRestoringProfile)
        {
            return;
        }

        _ = ApplyProfileSelectionAsync();
    }

    /// <summary>
    /// Pre-loads all profiles into cache by reading JSON files directly.
    /// </summary>
    private async Task PreloadProfilesCacheAsync(IEnumerable<string> profileNames)
    {
        _resolvedProfileAppIdsCache.Clear();
        _rawProfileAppIdsCache.Clear();

        // Get profiles directory path
        var profilesDir = GetProfilesDirectory();
        if (string.IsNullOrEmpty(profilesDir) || !Directory.Exists(profilesDir))
        {
            return;
        }

        // Load all profiles from JSON files
        foreach (var profileName in profileNames)
        {
            try
            {
                var rawAppIds = await ReadProfileAppIdsFromJsonAsync(profilesDir, profileName);
                _rawProfileAppIdsCache[profileName] = rawAppIds;

                // Resolve inheritance to get all app IDs
                var resolvedAppIds = await ResolveProfileInheritanceAsync(profilesDir, profileName);
                _resolvedProfileAppIdsCache[profileName] = resolvedAppIds;
            }
            catch
            {
                _rawProfileAppIdsCache[profileName] = [];
                _resolvedProfileAppIdsCache[profileName] = [];
            }
        }
    }

    /// <summary>
    /// Gets the Profiles directory path.
    /// </summary>
    private static string? GetProfilesDirectory()
    {
        // Try multiple locations relative to executable
        var exePath = AppDomain.CurrentDomain.BaseDirectory;

        // GUI\bin\Release\net8.0-windows → go up to repo root
        var current = new DirectoryInfo(exePath);

        for (int i = 0; i < 6 && current != null; i++)
        {
            var profilesPath = Path.Combine(current.FullName, "Profiles");
            if (Directory.Exists(profilesPath))
            {
                return profilesPath;
            }
            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Reads app IDs directly from a profile JSON file (no inheritance).
    /// </summary>
    private static async Task<List<string>> ReadProfileAppIdsFromJsonAsync(string profilesDir, string profileName)
    {
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");
        if (!File.Exists(profilePath))
        {
            return [];
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var appIds = new List<string>();

        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                if (appElement.ValueKind == JsonValueKind.String)
                {
                    var appId = appElement.GetString();
                    if (!string.IsNullOrEmpty(appId))
                    {
                        appIds.Add(appId);
                    }
                }
            }
        }

        return appIds;
    }

    /// <summary>
    /// Resolves profile inheritance and returns all app IDs.
    /// </summary>
    private async Task<HashSet<string>> ResolveProfileInheritanceAsync(string profilesDir, string profileName)
    {
        var allAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ResolveProfileRecursiveAsync(profilesDir, profileName, allAppIds, visited);

        return allAppIds;
    }

    /// <summary>
    /// Recursively resolves profile inheritance.
    /// </summary>
    private async Task ResolveProfileRecursiveAsync(
        string profilesDir,
        string profileName,
        HashSet<string> allAppIds,
        HashSet<string> visited)
    {
        if (visited.Contains(profileName))
        {
            return; // Avoid circular inheritance
        }
        visited.Add(profileName);

        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");
        if (!File.Exists(profilePath))
        {
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        // First, resolve parent profiles
        if (root.TryGetProperty("Inherits", out var inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parentElement in inheritsElement.EnumerateArray())
            {
                var parentName = parentElement.GetString();
                if (!string.IsNullOrEmpty(parentName))
                {
                    await ResolveProfileRecursiveAsync(profilesDir, parentName, allAppIds, visited);
                }
            }
        }

        // Then add this profile's applications
        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                if (appElement.ValueKind == JsonValueKind.String)
                {
                    var appId = appElement.GetString();
                    if (!string.IsNullOrEmpty(appId))
                    {
                        allAppIds.Add(appId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies the selected profile by checking apps in the list.
    /// </summary>
    private async Task ApplyProfileSelectionAsync()
    {
        var selectedProfile = SelectedProfile;

        // Clear all profile tiers first
        foreach (var app in _allApplications)
        {
            app.ProfileTier = string.Empty;
        }
        _profileAppTiers.Clear();

        if (string.IsNullOrEmpty(selectedProfile))
        {
            _lastAppliedProfile = null;
            ApplyFilter();
            return;
        }

        try
        {
            var mergeWithManualSelection = false;
            if (ShouldPromptBeforeApplyingProfile())
            {
                var applyMode = await _dialogService.ShowYesNoCancelAsync(
                    Resources.Resources.Profile_Apply_Title,
                    Resources.Resources.Profile_Apply_Message,
                    Resources.Resources.Profile_Apply_Replace,
                    Resources.Resources.Profile_Apply_Merge,
                    Resources.Resources.Common_Cancel);

                if (applyMode is null)
                {
                    RestoreSelectedProfile(_lastAppliedProfile);
                    return;
                }

                mergeWithManualSelection = applyMode == false;
            }

            HashSet<string> profileAppIds;

            // Try cache first
            if (_resolvedProfileAppIdsCache.TryGetValue(selectedProfile, out var cachedIds) && cachedIds.Count > 0)
            {
                profileAppIds = cachedIds;
            }
            else
            {
                // Load on-demand from JSON
                var profilesDir = GetProfilesDirectory();
                if (string.IsNullOrEmpty(profilesDir))
                {
                    ErrorMessage = GetLocalizedString(
                        "Apps_Error_ProfilesDirectoryNotFound",
                        "Could not find Profiles directory");
                    return;
                }

                profileAppIds = await ResolveProfileInheritanceAsync(profilesDir, selectedProfile);
                _resolvedProfileAppIdsCache[selectedProfile] = profileAppIds;

                var rawAppIds = await ReadProfileAppIdsFromJsonAsync(profilesDir, selectedProfile);
                _rawProfileAppIdsCache[selectedProfile] = rawAppIds;
            }

            // Build tier mapping using cached raw profiles
            BuildProfileTierMapping(selectedProfile);

            // Select apps from the profile
            foreach (var app in _allApplications)
            {
                if (profileAppIds.Contains(app.AppId))
                {
                    app.IsSelected = true;

                    // Assign tier badge
                    if (_profileAppTiers.TryGetValue(app.AppId, out var tier))
                    {
                        app.ProfileTier = tier;
                    }
                }
                else if (!mergeWithManualSelection)
                {
                    app.IsSelected = false;
                }
            }

            _lastAppliedProfile = selectedProfile;
            ApplyFilter();
            UpdateSelectedCount();
        }
        catch (ProfileException ex)
        {
            ErrorMessage = FormatLocalized(
                "Apps_Error_ProfileLoadFailed",
                "Failed to load profile '{0}': {1}",
                ex.ProfileName ?? string.Empty,
                ex.Message);
            Debug.WriteLine($"ProfileException in OnSelectedProfileChanged: {ex}");
        }
        catch (PowerShellBridgeException ex)
        {
            ErrorMessage = FormatLocalized(
                "Apps_Error_ProfileLoadPowerShellFailed",
                "Failed to load profile (PowerShell error): {0}",
                ex.Message);
            Debug.WriteLine($"PowerShellBridgeException in OnSelectedProfileChanged: {ex}");
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatLocalized(
                "Apps_Error_ProfileLoadGeneric",
                "Failed to load profile: {0}",
                ex.Message);
            Debug.WriteLine($"Unexpected exception in OnSelectedProfileChanged: {ex}");
        }
    }

    private bool ShouldPromptBeforeApplyingProfile()
    {
        return string.IsNullOrEmpty(_lastAppliedProfile) &&
            _allApplications.Any(app => app.IsSelected);
    }

    private void RestoreSelectedProfile(string? profileName)
    {
        _isRestoringProfile = true;
        try
        {
            SelectedProfile = profileName;
        }
        finally
        {
            _isRestoringProfile = false;
        }
    }

    /// <summary>
    /// Builds a mapping of app IDs to their originating profile tier.
    /// Uses the raw profile cache (apps defined directly in each profile).
    /// </summary>
    private void BuildProfileTierMapping(string profileName)
    {
        // Define the profile hierarchy (from base to most specific)
        var profileHierarchy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Personnel", ["Base", "Office", "Gaming", "Personnel"] },
            { "Gaming", ["Base", "Office", "Gaming"] },
            { "Office", ["Base", "Office"] },
            { "Base", ["Base"] }
        };

        if (!profileHierarchy.TryGetValue(profileName, out var hierarchy))
        {
            hierarchy = [profileName];
        }

        // Build tier mapping (most specific wins, so iterate from base to specific)
        foreach (var tier in hierarchy)
        {
            if (_rawProfileAppIdsCache.TryGetValue(tier, out var appIds))
            {
                foreach (var appId in appIds)
                {
                    // Overwrite so most specific tier wins
                    _profileAppTiers[appId] = tier;
                }
            }
        }
    }

    /// <summary>
    /// Clears the current profile selection.
    /// </summary>
    [RelayCommand]
    private void ClearProfile()
    {
        SelectedProfile = null;
    }

    /// <summary>
    /// Shows the save profile dialog and saves the current selection.
    /// </summary>
    [RelayCommand]
    private async Task ShowSaveProfileDialogAsync()
    {
        var selectedApps = _allApplications.Where(a => a.IsSelected).ToList();

        if (selectedApps.Count == 0)
        {
            ErrorMessage = Resources.Resources.Dialog_SaveProfile_NoApps;
            return;
        }

        // Create dialog viewmodel
        var dialogViewModel = new SaveProfileDialogViewModel(
            SelectedProfile,
            AvailableProfiles,
            selectedApps.Count);

        var dialog = new Views.SaveProfileDialog
        {
            DataContext = dialogViewModel
        };

        // Show save profile dialog using ViewModel's save profile state
        SaveProfileDialogContent = dialog;
        IsSaveProfileDialogOpen = true;

        // Wait for the dialog to complete (set by view's dialog closing handler)
        while (IsSaveProfileDialogOpen)
        {
            await Task.Delay(100);
        }

        if (dialog.DataContext is SaveProfileDialogViewModel vm)
        {
            await SaveProfileAsync(vm.GetResult(), selectedApps);
        }
    }

    /// <summary>
    /// Saves the current selection as a profile.
    /// </summary>
    private async Task SaveProfileAsync(SaveProfileResult saveResult, List<Models.ApplicationModel> selectedApps)
    {
        try
        {
            var profilesDir = GetProfilesDirectory();
            if (string.IsNullOrEmpty(profilesDir))
            {
                ErrorMessage = GetLocalizedString(
                    "Apps_Error_ProfilesDirectoryNotFound",
                    "Could not find Profiles directory");
                return;
            }

            var profilePath = Path.Combine(profilesDir, $"{saveResult.ProfileName}.json");

            // Build profile JSON
            var profile = new Dictionary<string, object>
            {
                ["Name"] = saveResult.ProfileName,
                ["Description"] = saveResult.Description,
                ["Version"] = "3.2.0",
                ["Inherits"] = saveResult.ParentProfile != null
                    ? new[] { saveResult.ParentProfile }
                    : Array.Empty<string>(),
                ["Applications"] = selectedApps.Select(a => a.AppId).ToArray()
            };

            // If inheriting, remove apps that are already in the parent
            if (!string.IsNullOrEmpty(saveResult.ParentProfile) &&
                _resolvedProfileAppIdsCache.TryGetValue(saveResult.ParentProfile, out var parentAppIds))
            {
                var ownApps = selectedApps
                    .Where(a => !parentAppIds.Contains(a.AppId))
                    .Select(a => a.AppId)
                    .ToArray();
                profile["Applications"] = ownApps;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(profile, jsonOptions);
            await File.WriteAllTextAsync(profilePath, jsonContent);

            // Update cache
            var appIds = selectedApps.Select(a => a.AppId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _resolvedProfileAppIdsCache[saveResult.ProfileName] = appIds;

            if (profile["Applications"] is string[] ownAppIds)
            {
                _rawProfileAppIdsCache[saveResult.ProfileName] = ownAppIds.ToList();
            }

            // Add to available profiles if new
            if (!AvailableProfiles.Contains(saveResult.ProfileName))
            {
                AvailableProfiles.Add(saveResult.ProfileName);
            }

            // Select the saved profile
            _lastAppliedProfile = saveResult.ProfileName;
            SelectedProfile = saveResult.ProfileName;

            // Clear any error message to indicate success
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(Resources.Resources.Dialog_SaveProfile_Error, ex.Message);
        }
    }

    /// <summary>
    /// Invalidates all cached data, forcing a refresh on next access.
    /// </summary>
    public void InvalidateCaches()
    {
        _resolvedProfileAppIdsCache.Clear();
        _rawProfileAppIdsCache.Clear();
    }
}
