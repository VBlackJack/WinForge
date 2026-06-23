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

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinForge.GUI.Models;

/// <summary>
/// Represents an application in the deployment profile.
/// Inherits from ObservableValidator for validation support.
/// Note: [NotifyDataErrorInfo] is intentionally NOT used to prevent automatic red border UI in DataGrid.
/// </summary>
public partial class ApplicationModel : ObservableValidator
{
    /// <summary>Unique identifier for the application.</summary>
    [ObservableProperty]
    [Required(ErrorMessageResourceName = "Validation_AppId_Required", ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(256, MinimumLength = 1, ErrorMessageResourceName = "Validation_AppId_Length", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _appId = string.Empty;

    /// <summary>Display name of the application.</summary>
    [ObservableProperty]
    [Required(ErrorMessageResourceName = "Validation_AppName_Required", ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(512, MinimumLength = 1, ErrorMessageResourceName = "Validation_AppName_Length", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _name = string.Empty;

    /// <summary>Application category (e.g., Browser, Utility).</summary>
    [ObservableProperty]
    [StringLength(128, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _category = string.Empty;

    /// <summary>Description of the application.</summary>
    [ObservableProperty]
    [StringLength(2048, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _description = string.Empty;

    /// <summary>Installation priority (lower = installed first). 0 is reserved for system-critical apps.</summary>
    [ObservableProperty]
    [Range(0, 100, ErrorMessageResourceName = "Validation_RangeError", ErrorMessageResourceType = typeof(Resources.Resources))]
    private int _priority;

    /// <summary>Whether this application is required.</summary>
    [ObservableProperty]
    private bool _isRequired;

    /// <summary>Whether this application is a system prerequisite.</summary>
    [ObservableProperty]
    private bool _isPrerequisite;

    /// <summary>Whether this application is a required system prerequisite.</summary>
    public bool IsRequiredPrerequisite => IsRequired && IsPrerequisite;

    /// <summary>Current installation status.</summary>
    [ObservableProperty]
    private ApplicationStatus _status = ApplicationStatus.Pending;

    /// <summary>Error message if installation failed.</summary>
    [ObservableProperty]
    [StringLength(4096, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string? _errorMessage;

    /// <summary>Whether the application is selected for installation.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Installation log output for this application.</summary>
    [ObservableProperty]
    [StringLength(262144, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _logOutput = string.Empty;

    /// <summary>Installation progress (0-100).</summary>
    [ObservableProperty]
    [Range(0, 100, ErrorMessageResourceName = "Validation_RangeError", ErrorMessageResourceType = typeof(Resources.Resources))]
    private double _progressValue;

    /// <summary>Status message displayed during installation.</summary>
    [ObservableProperty]
    [StringLength(1024, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _statusMessage = string.Empty;

    /// <summary>Available installation sources (e.g., "Winget, Chocolatey").</summary>
    [ObservableProperty]
    [StringLength(256, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _sources = string.Empty;

    /// <summary>Whether this application requires manual installation.</summary>
    [ObservableProperty]
    private bool _manualInstallOnly;

    /// <summary>Official download URL for manual installation.</summary>
    [ObservableProperty]
    [StringLength(2048, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    [ValidUrl(ErrorMessageResourceName = "Validation_InvalidUrl", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _officialUrl = string.Empty;

    /// <summary>Installation notes or warnings.</summary>
    [ObservableProperty]
    [StringLength(2048, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _installNotes = string.Empty;

    /// <summary>Whether this application is marked as favorite.</summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>Current installed version of the application.</summary>
    [ObservableProperty]
    [StringLength(128, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _currentVersion = string.Empty;

    /// <summary>Available version for update (when update is available).</summary>
    [ObservableProperty]
    [StringLength(128, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _availableVersion = string.Empty;

    /// <summary>Profile tier this app belongs to (e.g., "Base", "Office", "Gaming").</summary>
    [ObservableProperty]
    [StringLength(64, ErrorMessageResourceName = "Validation_StringTooLong", ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _profileTier = string.Empty;

    partial void OnIsRequiredChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRequiredPrerequisite));
    }

    partial void OnIsPrerequisiteChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRequiredPrerequisite));
    }
}
