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
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Views;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Profile Editor view.
/// Handles creating new profiles and editing existing ones.
/// </summary>
public partial class ProfileEditorViewModel : ViewModelBase
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IProfileExportService _profileExportService;
    private string? _originalProfileName;

    /// <summary>
    /// Whether this is editing an existing profile (vs creating new).
    /// </summary>
    [ObservableProperty]
    private bool _isEditMode;

    /// <summary>
    /// The profile name.
    /// </summary>
    [ObservableProperty]
    private string _profileName = string.Empty;

    /// <summary>
    /// The profile description.
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Available parent profiles for inheritance.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableParents = [];

    /// <summary>
    /// Selected parent profile for inheritance.
    /// </summary>
    [ObservableProperty]
    private string? _selectedParent;

    /// <summary>
    /// Applications inherited from the parent profile (read-only).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ApplicationModel> _inheritedApplications = [];

    /// <summary>
    /// Applications added in this profile (editable).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ApplicationModel> _addedApplications = [];

    /// <summary>
    /// All available applications for adding.
    /// </summary>
    [ObservableProperty]
    private List<ApplicationModel> _allApplications = [];

    /// <summary>
    /// Whether inherited applications are being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingInherited;

    /// <summary>
    /// Whether a save operation is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isSaving;

    /// <summary>
    /// Success message after save operation.
    /// </summary>
    [ObservableProperty]
    private string? _successMessage;

    /// <summary>
    /// Title for the editor (New Profile / Edit Profile: X).
    /// </summary>
    public string EditorTitle => IsEditMode
        ? string.Format(Resources.Resources.Editor_Title_Edit, _originalProfileName)
        : Resources.Resources.Editor_Title_New;

    /// <summary>
    /// Initializes a new instance of ProfileEditorViewModel.
    /// </summary>
    public ProfileEditorViewModel(IPowerShellBridge powerShellBridge, IProfileExportService profileExportService)
    {
        _powerShellBridge = powerShellBridge;
        _profileExportService = profileExportService;
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Load available profiles for parent selection
            var profiles = await _powerShellBridge.GetAvailableProfilesAsync();

            // Add "None" option at the beginning
            AvailableParents = new ObservableCollection<string>(
                new[] { Resources.Resources.Editor_NoParent }.Concat(profiles));

            // Load all applications for the add dialog
            AllApplications = await _powerShellBridge.GetAllApplicationsAsync();
        }
        catch (Exception ex)
        {
            // Show full error for debugging
            ErrorMessage = ex.InnerException != null
                ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}\n\nStack: {ex.StackTrace}"
                : ex.ToString();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Initializes the editor for creating a new profile.
    /// </summary>
    public async Task InitializeNewProfileAsync()
    {
        IsEditMode = false;
        _originalProfileName = null;
        ProfileName = string.Empty;
        Description = string.Empty;
        SelectedParent = null;
        InheritedApplications.Clear();
        AddedApplications.Clear();

        await InitializeAsync();

        // Select "None" by default
        if (AvailableParents.Count > 0)
        {
            SelectedParent = AvailableParents[0];
        }

        OnPropertyChanged(nameof(EditorTitle));
    }

    /// <summary>
    /// Initializes the editor for editing an existing profile.
    /// </summary>
    public async Task InitializeEditProfileAsync(string profileName)
    {
        IsEditMode = true;
        _originalProfileName = profileName;

        await InitializeAsync();

        try
        {
            // Load the raw profile (without inheritance)
            var rawProfile = await _powerShellBridge.GetRawProfileAsync(profileName);

            ProfileName = rawProfile.Name;
            Description = rawProfile.Description;
            AddedApplications = rawProfile.Applications;

            // Set selected parent
            if (rawProfile.InheritedFrom.Count > 0)
            {
                var parentName = rawProfile.InheritedFrom[0];
                SelectedParent = AvailableParents.Contains(parentName)
                    ? parentName
                    : AvailableParents[0];
            }
            else
            {
                SelectedParent = AvailableParents.Count > 0 ? AvailableParents[0] : null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        OnPropertyChanged(nameof(EditorTitle));
    }

    /// <summary>
    /// Called when SelectedParent changes.
    /// Loads inherited applications from the parent profile.
    /// </summary>
    partial void OnSelectedParentChanged(string? value)
    {
        _ = LoadInheritedApplicationsAsync();
    }

    /// <summary>
    /// Loads applications from the selected parent profile.
    /// </summary>
    private async Task LoadInheritedApplicationsAsync()
    {
        if (string.IsNullOrEmpty(SelectedParent) ||
            SelectedParent == Resources.Resources.Editor_NoParent)
        {
            InheritedApplications.Clear();
            return;
        }

        IsLoadingInherited = true;

        try
        {
            var parentProfile = await _powerShellBridge.GetResolvedProfileAsync(SelectedParent);
            InheritedApplications = parentProfile.Applications;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(Resources.Resources.Editor_Error_LoadParent, ex.Message);
            InheritedApplications.Clear();
        }
        finally
        {
            IsLoadingInherited = false;
        }
    }

    /// <summary>
    /// Removes an application from the Added list.
    /// </summary>
    [RelayCommand]
    private void RemoveApplication(ApplicationModel? app)
    {
        if (app != null && AddedApplications.Contains(app))
        {
            AddedApplications.Remove(app);
        }
    }

    /// <summary>
    /// Shows the application picker dialog and adds the selected app.
    /// </summary>
    [RelayCommand]
    private async Task ShowAddApplicationDialogAsync()
    {
        // Filter out apps that are already in Inherited or Added lists
        var existingIds = new HashSet<string>(
            InheritedApplications.Select(a => a.AppId)
                .Concat(AddedApplications.Select(a => a.AppId)));

        var availableApps = AllApplications
            .Where(a => !existingIds.Contains(a.AppId))
            .ToList();

        if (availableApps.Count == 0)
        {
            ErrorMessage = Resources.Resources.Dialog_NoAppsAvailable;
            return;
        }

        // Create picker dialog with filtered apps
        var pickerViewModel = new ApplicationPickerViewModel(availableApps);
        var pickerDialog = new ApplicationPickerDialog
        {
            DataContext = pickerViewModel
        };

        // Show dialog and wait for result
        var result = await DialogHost.Show(pickerDialog, "RootDialog");

        // If user selected an app, add it
        if (result is ApplicationModel selectedApp)
        {
            AddApplicationInternal(selectedApp);
        }
    }

    /// <summary>
    /// Adds an application to the Added list internally.
    /// </summary>
    private void AddApplicationInternal(ApplicationModel app)
    {
        // Double-check not already added
        if (AddedApplications.Any(a => a.AppId == app.AppId))
        {
            return;
        }

        if (InheritedApplications.Any(a => a.AppId == app.AppId))
        {
            return;
        }

        // Create a copy for the Added list
        var newApp = new ApplicationModel
        {
            AppId = app.AppId,
            Name = app.Name,
            Category = app.Category,
            Description = app.Description,
            Priority = app.Priority,
            IsRequired = false,
            IsSelected = true
        };

        AddedApplications.Add(newApp);
    }

    /// <summary>
    /// Saves the profile to disk.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        // Validate profile name
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ErrorMessage = Resources.Resources.Editor_Error_NameRequired;
            return;
        }

        // Validate profile name contains valid characters
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        if (ProfileName.IndexOfAny(invalidChars) >= 0)
        {
            ErrorMessage = Resources.Resources.Editor_Error_InvalidName;
            return;
        }

        IsSaving = true;

        try
        {
            // Collect AppIds from Added applications
            var addedAppIds = AddedApplications
                .Select(a => a.AppId)
                .ToList();

            // Determine parent profile
            var parentProfile = SelectedParent == Resources.Resources.Editor_NoParent
                ? null
                : SelectedParent;

            // Save the profile
            await _powerShellBridge.SaveProfileAsync(
                ProfileName,
                Description,
                parentProfile,
                addedAppIds);

            // Show success message
            SuccessMessage = Resources.Resources.Msg_ProfileSaved;

            // Update original profile name for edit mode
            if (!IsEditMode)
            {
                _originalProfileName = ProfileName;
                IsEditMode = true;
                OnPropertyChanged(nameof(EditorTitle));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(Resources.Resources.Editor_Error_SaveFailed, ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Cancels editing and returns to previous view.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        // Clear any messages
        ErrorMessage = null;
        SuccessMessage = null;

        // Navigation will be handled by the MainWindow
    }

    /// <summary>
    /// Exports the current profile to a file.
    /// </summary>
    [RelayCommand]
    private async Task ExportProfileAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        // Validate profile name
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ErrorMessage = Resources.Resources.Editor_Error_NameRequired;
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = Resources.Resources.Editor_Btn_Export,
            Filter = Resources.Resources.Export_FileFilter,
            FileName = $"{ProfileName}.w11fp",
            DefaultExt = ".w11fp"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        IsSaving = true;

        try
        {
            // Build the deployment profile from current state
            var profile = BuildCurrentProfile();

            await _profileExportService.ExportToFileAsync(profile, saveDialog.FileName);

            SuccessMessage = Resources.Resources.Export_Success;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Imports a profile from a file.
    /// </summary>
    [RelayCommand]
    private async Task ImportProfileAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        var openDialog = new OpenFileDialog
        {
            Title = Resources.Resources.Editor_Btn_Import,
            Filter = Resources.Resources.Import_FileFilter,
            DefaultExt = ".w11fp"
        };

        if (openDialog.ShowDialog() != true)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var imported = await _profileExportService.ImportFromFileAsync(openDialog.FileName);

            if (imported == null)
            {
                ErrorMessage = Resources.Resources.Import_Error_Invalid;
                return;
            }

            var validation = _profileExportService.ValidateImport(imported);
            if (!validation.IsValid)
            {
                ErrorMessage = string.Format(Resources.Resources.Import_Error_Validation, validation.ErrorMessage);
                return;
            }

            // Apply imported data to the editor
            ProfileName = imported.Name;
            Description = imported.Description ?? string.Empty;

            // Set parent profile if specified
            if (!string.IsNullOrEmpty(imported.InheritsFrom) &&
                AvailableParents.Contains(imported.InheritsFrom))
            {
                SelectedParent = imported.InheritsFrom;
            }

            // Convert imported apps to ApplicationModels
            AddedApplications.Clear();
            foreach (var importedApp in imported.Applications)
            {
                // Try to find matching app in AllApplications
                var existingApp = AllApplications.FirstOrDefault(a =>
                    a.AppId == importedApp.WingetId ||
                    a.Name == importedApp.Name);

                if (existingApp != null)
                {
                    AddApplicationInternal(existingApp);
                }
            }

            SuccessMessage = Win11Forge.GUI.Resources.Resources.Import_Success;

            OnPropertyChanged(nameof(EditorTitle));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Builds a DeploymentProfileModel from the current editor state.
    /// </summary>
    private DeploymentProfileModel BuildCurrentProfile()
    {
        var parentProfile = SelectedParent == Resources.Resources.Editor_NoParent
            ? null
            : SelectedParent;

        return new DeploymentProfileModel
        {
            Name = ProfileName,
            Description = Description,
            InheritedFrom = parentProfile != null ? [parentProfile] : [],
            Applications = new System.Collections.ObjectModel.ObservableCollection<ApplicationModel>(AddedApplications)
        };
    }
}
