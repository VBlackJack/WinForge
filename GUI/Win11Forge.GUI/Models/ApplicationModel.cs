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

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents an application in the deployment profile.
/// Inherits from ObservableValidator to enable real-time validation with DataAnnotations.
/// </summary>
public partial class ApplicationModel : ObservableValidator, IValidatableObject
{
    /// <summary>Unique identifier for the application.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Application ID is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Application ID must be between 1 and 256 characters")]
    private string _appId = string.Empty;

    /// <summary>Display name of the application.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Application name is required")]
    [StringLength(512, MinimumLength = 1, ErrorMessage = "Application name must be between 1 and 512 characters")]
    private string _name = string.Empty;

    /// <summary>Application category (e.g., Browser, Utility).</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(128, ErrorMessage = "Category must not exceed 128 characters")]
    private string _category = string.Empty;

    /// <summary>Description of the application.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(2048, ErrorMessage = "Description must not exceed 2048 characters")]
    private string _description = string.Empty;

    /// <summary>Installation priority (lower = installed first).</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 100, ErrorMessage = "Priority must be between 1 and 100")]
    private int _priority;

    /// <summary>Whether this application is required.</summary>
    [ObservableProperty]
    private bool _isRequired;

    /// <summary>Current installation status.</summary>
    [ObservableProperty]
    private ApplicationStatus _status = ApplicationStatus.Pending;

    /// <summary>Error message if installation failed.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(4096, ErrorMessage = "Error message must not exceed 4096 characters")]
    private string? _errorMessage;

    /// <summary>Whether the application is selected for installation.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Installation log output for this application.</summary>
    [ObservableProperty]
    private string _logOutput = string.Empty;

    /// <summary>Installation progress (0-100).</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
    private double _progressValue;

    /// <summary>Status message displayed during installation.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(1024, ErrorMessage = "Status message must not exceed 1024 characters")]
    private string _statusMessage = string.Empty;

    /// <summary>Available installation sources (e.g., "Winget, Chocolatey").</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(256, ErrorMessage = "Sources must not exceed 256 characters")]
    private string _sources = string.Empty;

    /// <summary>Whether this application requires manual installation.</summary>
    [ObservableProperty]
    private bool _manualInstallOnly;

    /// <summary>Official download URL for manual installation.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(2048, ErrorMessage = "URL must not exceed 2048 characters")]
    private string _officialUrl = string.Empty;

    /// <summary>Installation notes or warnings.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(2048, ErrorMessage = "Install notes must not exceed 2048 characters")]
    private string _installNotes = string.Empty;

    /// <summary>Whether this application is marked as favorite.</summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>Current installed version of the application.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(128, ErrorMessage = "Version must not exceed 128 characters")]
    private string _currentVersion = string.Empty;

    /// <summary>Available version for update (when update is available).</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(128, ErrorMessage = "Version must not exceed 128 characters")]
    private string _availableVersion = string.Empty;

    /// <summary>Profile tier this app belongs to (e.g., "Base", "Office", "Gaming").</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(64, ErrorMessage = "Profile tier must not exceed 64 characters")]
    private string _profileTier = string.Empty;

    /// <summary>
    /// Validates the model with complex business rules.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Manual install applications must have an official URL
        if (ManualInstallOnly && string.IsNullOrWhiteSpace(OfficialUrl))
        {
            yield return new ValidationResult(
                "Manual install applications must have an official download URL",
                new[] { nameof(OfficialUrl) });
        }

        // Priority 0 is reserved for system-critical apps
        if (Priority == 0 && !IsRequired)
        {
            yield return new ValidationResult(
                "Priority 0 is reserved for required system-critical applications",
                new[] { nameof(Priority), nameof(IsRequired) });
        }
    }
}
