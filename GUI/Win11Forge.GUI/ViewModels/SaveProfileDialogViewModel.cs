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
using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// Result of the save profile dialog.
/// </summary>
public class SaveProfileResult
{
    /// <summary>
    /// Whether to overwrite the existing profile.
    /// </summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// The profile name (for new profiles or rename).
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// The parent profile for inheritance (null = no parent).
    /// </summary>
    public string? ParentProfile { get; set; }

    /// <summary>
    /// The profile description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the Save Profile dialog.
/// </summary>
public partial class SaveProfileDialogViewModel : ObservableObject
{
    /// <summary>
    /// Whether a profile is currently selected (can overwrite).
    /// </summary>
    [ObservableProperty]
    private bool _hasExistingProfile;

    /// <summary>
    /// The name of the currently selected profile.
    /// </summary>
    [ObservableProperty]
    private string _existingProfileName = string.Empty;

    /// <summary>
    /// Whether to overwrite the existing profile.
    /// </summary>
    [ObservableProperty]
    private bool _overwriteExisting;

    /// <summary>
    /// The new profile name (when creating new).
    /// </summary>
    [ObservableProperty]
    private string _newProfileName = string.Empty;

    /// <summary>
    /// Available parent profiles for inheritance.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableParents = [];

    /// <summary>
    /// Selected parent profile.
    /// </summary>
    [ObservableProperty]
    private string? _selectedParent;

    /// <summary>
    /// Profile description.
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Number of selected applications.
    /// </summary>
    [ObservableProperty]
    private int _selectedAppsCount;

    /// <summary>
    /// Whether the save button should be enabled.
    /// </summary>
    public bool CanSave => OverwriteExisting || !string.IsNullOrWhiteSpace(NewProfileName);

    /// <summary>
    /// Initializes a new instance of SaveProfileDialogViewModel.
    /// </summary>
    public SaveProfileDialogViewModel(
        string? currentProfile,
        IEnumerable<string> availableProfiles,
        int selectedAppsCount)
    {
        HasExistingProfile = !string.IsNullOrEmpty(currentProfile);
        ExistingProfileName = currentProfile ?? string.Empty;
        OverwriteExisting = HasExistingProfile;
        SelectedAppsCount = selectedAppsCount;

        // Build parent list with "None" option
        var parents = new List<string> { Resources.Resources.Editor_NoParent };
        parents.AddRange(availableProfiles.Where(p => p != currentProfile));
        AvailableParents = new ObservableCollection<string>(parents);
        SelectedParent = parents[0];
    }

    partial void OnOverwriteExistingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnNewProfileNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Builds the result from the current dialog state.
    /// </summary>
    public SaveProfileResult GetResult()
    {
        return new SaveProfileResult
        {
            OverwriteExisting = OverwriteExisting,
            ProfileName = OverwriteExisting ? ExistingProfileName : NewProfileName.Trim(),
            ParentProfile = SelectedParent == Resources.Resources.Editor_NoParent ? null : SelectedParent,
            Description = Description
        };
    }
}
