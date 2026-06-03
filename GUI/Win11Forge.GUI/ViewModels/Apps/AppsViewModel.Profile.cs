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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Views;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    [ObservableProperty]
    private ObservableCollection<string> _availableProfiles = [];

    [ObservableProperty]
    private ObservableCollection<ProfileSelectorItem> _profileSelectorItems = [CreateCustomProfileSelectorItem()];

    /// <summary>
    /// Currently selected profile (null = no profile, manual selection).
    /// </summary>
    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private ProfileSelectorItem? _selectedProfileSelectorItem = CreateCustomProfileSelectorItem();

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
        RebuildProfileSelectorItems();
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

    /// <summary>
    /// Cache of resolved profile inheritance chains.
    /// Key = profile name, Value = parent-to-child profile chain used for tier badges.
    /// </summary>
    private Dictionary<string, List<string>> _profileInheritanceCache = [];

    private string? _lastAppliedProfile;
    private HashSet<string>? _lastAppliedProfileSelectionSnapshot;
    private bool _isRestoringProfile;

    /// <summary>
    /// Called when SelectedProfile changes.
    /// </summary>
    partial void OnSelectedProfileChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProfileApplied));
        SyncSelectedProfileSelectorItem(value);

        if (_isRestoringProfile)
        {
            return;
        }

        ApplyProfileSelectionAsync().SafeFireAndForget();
    }

    partial void OnSelectedProfileSelectorItemChanged(ProfileSelectorItem? value)
    {
        if (_isSyncingProfileSelector)
        {
            return;
        }

        SelectedProfile = value?.ProfileName;
    }

    private bool _isSyncingProfileSelector;

    private static ProfileSelectorItem CreateCustomProfileSelectorItem()
    {
        return new ProfileSelectorItem(Resources.Resources.Apps_CustomProfile, null);
    }

    private static ProfileSelectorItem CreateProfileSelectorItem(string profileName)
    {
        return new ProfileSelectorItem(profileName, profileName);
    }

    private void RebuildProfileSelectorItems()
    {
        ProfileSelectorItems = new ObservableCollection<ProfileSelectorItem>(
            new[] { CreateCustomProfileSelectorItem() }
                .Concat(AvailableProfiles.Select(CreateProfileSelectorItem)));
        SyncSelectedProfileSelectorItem(SelectedProfile);
    }

    private void AddProfileSelectorItem(string profileName)
    {
        if (ProfileSelectorItems.Any(
            item => string.Equals(item.ProfileName, profileName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ProfileSelectorItems.Add(CreateProfileSelectorItem(profileName));
    }

    private void SyncSelectedProfileSelectorItem(string? profileName)
    {
        ProfileSelectorItem? selectedItem = string.IsNullOrEmpty(profileName)
            ? ProfileSelectorItems.FirstOrDefault(item => item.IsCustom)
            : ProfileSelectorItems.FirstOrDefault(
                item => string.Equals(item.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));

        selectedItem ??= ProfileSelectorItems.FirstOrDefault(item => item.IsCustom);
        if (ReferenceEquals(SelectedProfileSelectorItem, selectedItem))
        {
            return;
        }

        _isSyncingProfileSelector = true;
        try
        {
            SelectedProfileSelectorItem = selectedItem;
        }
        finally
        {
            _isSyncingProfileSelector = false;
        }
    }

    /// <summary>
    /// Pre-loads all profiles into cache by reading JSON files directly.
    /// </summary>
    private async Task PreloadProfilesCacheAsync(IEnumerable<string> profileNames)
    {
        _resolvedProfileAppIdsCache.Clear();
        _rawProfileAppIdsCache.Clear();
        _profileInheritanceCache.Clear();

        IReadOnlyList<string> profileDirectories = GetProfileReadDirectories();
        if (profileDirectories.Count == 0)
        {
            return;
        }

        // Load all profiles from JSON files
        foreach (string profileName in profileNames)
        {
            try
            {
                List<string> rawAppIds = await ReadProfileAppIdsFromJsonAsync(profileDirectories, profileName);
                _rawProfileAppIdsCache[profileName] = rawAppIds;

                // Resolve inheritance to get all app IDs
                HashSet<string> resolvedAppIds = await ResolveProfileInheritanceAsync(profileDirectories, profileName);
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
    /// Gets the readable profile directory paths, with user profiles taking precedence.
    /// </summary>
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
    /// Gets the writable user profiles directory path.
    /// </summary>
    private string GetProfilesWriteDirectory()
    {
        string profilesDir = _pathService.UserProfilesDirectory;
        Directory.CreateDirectory(profilesDir);
        return profilesDir;
    }

    /// <summary>
    /// Reads app IDs directly from a profile JSON file (no inheritance).
    /// </summary>
    private static async Task<List<string>> ReadProfileAppIdsFromJsonAsync(
        IReadOnlyList<string> profileDirectories,
        string profileName)
    {
        string? profilePath = TryGetProfilePath(profileDirectories, profileName);
        if (profilePath == null)
        {
            return [];
        }

        string jsonContent = await File.ReadAllTextAsync(profilePath);
        using JsonDocument document = JsonDocument.Parse(jsonContent);
        JsonElement root = document.RootElement;

        List<string> appIds = new List<string>();

        if (root.TryGetProperty("Applications", out JsonElement appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement appElement in appsElement.EnumerateArray())
            {
                if (appElement.ValueKind == JsonValueKind.String)
                {
                    string? appId = appElement.GetString();
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
    private async Task<HashSet<string>> ResolveProfileInheritanceAsync(
        IReadOnlyList<string> profileDirectories,
        string profileName)
    {
        HashSet<string> allAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> profileChain = new List<string>();

        await ResolveProfileRecursiveAsync(profileDirectories, profileName, allAppIds, visited, profileChain);
        _profileInheritanceCache[profileName] = profileChain;

        return allAppIds;
    }

    /// <summary>
    /// Recursively resolves profile inheritance.
    /// </summary>
    private async Task ResolveProfileRecursiveAsync(
        IReadOnlyList<string> profileDirectories,
        string profileName,
        HashSet<string> allAppIds,
        HashSet<string> visited,
        List<string> profileChain)
    {
        if (visited.Contains(profileName))
        {
            return; // Avoid circular inheritance
        }
        visited.Add(profileName);

        string? profilePath = TryGetProfilePath(profileDirectories, profileName);
        if (profilePath == null)
        {
            return;
        }

        string jsonContent = await File.ReadAllTextAsync(profilePath);
        using JsonDocument document = JsonDocument.Parse(jsonContent);
        JsonElement root = document.RootElement;

        // First, resolve parent profiles
        if (root.TryGetProperty("Inherits", out JsonElement inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement parentElement in inheritsElement.EnumerateArray())
            {
                string? parentName = parentElement.GetString();
                if (!string.IsNullOrEmpty(parentName))
                {
                    await ResolveProfileRecursiveAsync(profileDirectories, parentName, allAppIds, visited, profileChain);
                }
            }
        }

        // Then add this profile's applications
        List<string> rawAppIds = new List<string>();
        if (root.TryGetProperty("Applications", out JsonElement appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement appElement in appsElement.EnumerateArray())
            {
                if (appElement.ValueKind == JsonValueKind.String)
                {
                    string? appId = appElement.GetString();
                    if (!string.IsNullOrEmpty(appId))
                    {
                        rawAppIds.Add(appId);
                        allAppIds.Add(appId);
                    }
                }
            }
        }

        _rawProfileAppIdsCache[profileName] = rawAppIds;
        profileChain.Add(profileName);
    }

    /// <summary>
    /// Applies the selected profile by checking apps in the list.
    /// </summary>
    private async Task ApplyProfileSelectionAsync()
    {
        string? selectedProfile = SelectedProfile;

        if (string.IsNullOrEmpty(selectedProfile))
        {
            _lastAppliedProfile = null;
            _lastAppliedProfileSelectionSnapshot = null;
            ClearProfileTiers();
            ApplyFilter();
            return;
        }

        try
        {
            bool mergeWithManualSelection = false;
            if (ShouldPromptBeforeApplyingProfile(selectedProfile))
            {
                int manualCount = _allApplications.Count(app => app.IsSelected);
                string applyMessage = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Resources.Profile_Apply_Message_HasManualSelection,
                    manualCount,
                    selectedProfile);

                bool? applyMode = await _dialogService.ShowYesNoCancelAsync(
                    Resources.Resources.Profile_Apply_Title,
                    applyMessage,
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

            ClearProfileTiers();

            HashSet<string> profileAppIds;

            // Try cache first
            if (_resolvedProfileAppIdsCache.TryGetValue(selectedProfile, out HashSet<string>? cachedIds) && cachedIds.Count > 0)
            {
                profileAppIds = cachedIds;
            }
            else
            {
                // Load on-demand from JSON
                IReadOnlyList<string> profileDirectories = GetProfileReadDirectories();
                if (profileDirectories.Count == 0)
                {
                    ErrorMessage = GetLocalizedString(
                        "Apps_Error_ProfilesDirectoryNotFound",
                        "Could not find Profiles directory");
                    return;
                }

                profileAppIds = await ResolveProfileInheritanceAsync(profileDirectories, selectedProfile);
                _resolvedProfileAppIdsCache[selectedProfile] = profileAppIds;

                List<string> rawAppIds = await ReadProfileAppIdsFromJsonAsync(profileDirectories, selectedProfile);
                _rawProfileAppIdsCache[selectedProfile] = rawAppIds;
            }

            // Build tier mapping using cached raw profiles
            BuildProfileTierMapping(selectedProfile);

            // Select apps from the profile
            foreach (ApplicationModel app in _allApplications)
            {
                if (profileAppIds.Contains(app.AppId))
                {
                    app.IsSelected = true;

                    // Assign tier badge
                    if (_profileAppTiers.TryGetValue(app.AppId, out string? tier))
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
            StoreLastAppliedProfileSelectionSnapshot();
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
            _logger.LogError("ProfileException in OnSelectedProfileChanged", ex);
        }
        catch (PowerShellBridgeException ex)
        {
            ErrorMessage = FormatLocalized(
                "Apps_Error_ProfileLoadPowerShellFailed",
                "Failed to load profile (PowerShell error): {0}",
                ex.Message);
            _logger.LogError("PowerShellBridgeException in OnSelectedProfileChanged", ex);
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatLocalized(
                "Apps_Error_ProfileLoadGeneric",
                "Failed to load profile: {0}",
                ex.Message);
            _logger.LogError("Unexpected exception in OnSelectedProfileChanged", ex);
        }
    }

    private bool ShouldPromptBeforeApplyingProfile(string selectedProfile)
    {
        if (string.IsNullOrEmpty(_lastAppliedProfile))
        {
            return _allApplications.Any(app => app.IsSelected);
        }

        if (string.Equals(selectedProfile, _lastAppliedProfile, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasSelectionChangedSinceLastAppliedProfile();
    }

    private void ClearProfileTiers()
    {
        foreach (ApplicationModel app in _allApplications)
        {
            app.ProfileTier = string.Empty;
        }

        _profileAppTiers.Clear();
    }

    private void StoreLastAppliedProfileSelectionSnapshot()
    {
        _lastAppliedProfileSelectionSnapshot = CaptureSelectedAppIds();
    }

    private HashSet<string> CaptureSelectedAppIds()
    {
        return _allApplications
            .Where(app => app.IsSelected)
            .Select(app => app.AppId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private bool HasSelectionChangedSinceLastAppliedProfile()
    {
        return _lastAppliedProfileSelectionSnapshot is not null &&
            !_lastAppliedProfileSelectionSnapshot.SetEquals(CaptureSelectedAppIds());
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
        List<string> hierarchy = _profileInheritanceCache.TryGetValue(profileName, out List<string>? cachedHierarchy) &&
            cachedHierarchy.Count > 0
                ? cachedHierarchy
                : new List<string> { profileName };

        // Build tier mapping (most specific wins, so iterate from base to specific)
        foreach (string tier in hierarchy)
        {
            if (_rawProfileAppIdsCache.TryGetValue(tier, out List<string>? appIds))
            {
                foreach (string appId in appIds)
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
        List<ApplicationModel> selectedApps = _allApplications.Where(a => a.IsSelected).ToList();

        if (selectedApps.Count == 0)
        {
            ErrorMessage = Resources.Resources.Dialog_SaveProfile_NoApps;
            return;
        }

        // Create dialog viewmodel
        SaveProfileDialogViewModel dialogViewModel = new SaveProfileDialogViewModel(
            SelectedProfile,
            AvailableProfiles,
            selectedApps.Count);

        SaveProfileDialog dialog = new Views.SaveProfileDialog
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
            string profilesDir = GetProfilesWriteDirectory();

            string profilePath = Path.Combine(
                profilesDir,
                $"{saveResult.ProfileName}{Win11ForgePathNames.JsonFileExtension}");

            // Build profile JSON
            Dictionary<string, object> profile = new Dictionary<string, object>
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
                _resolvedProfileAppIdsCache.TryGetValue(saveResult.ParentProfile, out HashSet<string>? parentAppIds))
            {
                string[] ownApps = selectedApps
                    .Where(a => !parentAppIds.Contains(a.AppId))
                    .Select(a => a.AppId)
                    .ToArray();
                profile["Applications"] = ownApps;
            }

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string jsonContent = JsonSerializer.Serialize(profile, jsonOptions);
            await File.WriteAllTextAsync(profilePath, jsonContent);

            // Update cache
            HashSet<string> appIds = selectedApps.Select(a => a.AppId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _resolvedProfileAppIdsCache[saveResult.ProfileName] = appIds;

            if (profile["Applications"] is string[] ownAppIds)
            {
                _rawProfileAppIdsCache[saveResult.ProfileName] = ownAppIds.ToList();
            }

            List<string> inheritanceChain = new List<string>();
            if (!string.IsNullOrEmpty(saveResult.ParentProfile))
            {
                if (_profileInheritanceCache.TryGetValue(saveResult.ParentProfile, out List<string>? parentChain))
                {
                    inheritanceChain.AddRange(parentChain);
                }
                else
                {
                    inheritanceChain.Add(saveResult.ParentProfile);
                }
            }
            inheritanceChain.Add(saveResult.ProfileName);
            _profileInheritanceCache[saveResult.ProfileName] = inheritanceChain;

            // Add to available profiles if new
            if (!AvailableProfiles.Contains(saveResult.ProfileName))
            {
                AvailableProfiles.Add(saveResult.ProfileName);
                AddProfileSelectorItem(saveResult.ProfileName);
                OnPropertyChanged(nameof(HasProfiles));
            }

            // Select the saved profile
            _lastAppliedProfile = saveResult.ProfileName;
            StoreLastAppliedProfileSelectionSnapshot();
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
        _profileInheritanceCache.Clear();
    }

    private static string? TryGetProfilePath(IReadOnlyList<string> profileDirectories, string profileName)
    {
        foreach (string profilesDir in profileDirectories)
        {
            string profilePath = Path.Combine(profilesDir, $"{profileName}{Win11ForgePathNames.JsonFileExtension}");
            string fullPath = Path.GetFullPath(profilePath);
            string fullProfilesDir = Path.GetFullPath(profilesDir);
            if (!fullProfilesDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullProfilesDir += Path.DirectorySeparatorChar;
            }

            if (!fullPath.StartsWith(fullProfilesDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
