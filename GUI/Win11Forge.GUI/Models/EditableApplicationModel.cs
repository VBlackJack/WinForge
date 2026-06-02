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
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents an application entry for editing in the database.
/// This model maps directly to the applications.json structure.
/// </summary>
public partial class EditableApplicationModel : ObservableValidator, IValidatableObject
{
    /// <summary>
    /// Unique identifier for the application (e.g., "GoogleChrome").
    /// Used as the key in the applications.json dictionary.
    /// </summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Id_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9\.\-_]*$",
        ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Id_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(128, MinimumLength = 2, ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Id_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _appId = string.Empty;

    /// <summary>Display name of the application.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Name_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(256, MinimumLength = 1, ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Name_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _name = string.Empty;

    /// <summary>Application category (e.g., "Browser", "Utility", "Development").</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Category_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(64, MinimumLength = 1, ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Category_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _category = string.Empty;

    /// <summary>Description of the application.</summary>
    [ObservableProperty]
    [StringLength(1024, ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Description_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _description = string.Empty;

    /// <summary>Installation sources configuration.</summary>
    [ObservableProperty]
    private ApplicationSourcesModel _sources = new();

    /// <summary>Canonical silent-install arguments consumed by the PowerShell installer (all methods).</summary>
    [ObservableProperty]
    private string _installArguments = string.Empty;

    /// <summary>Detection configuration for checking if app is installed.</summary>
    [ObservableProperty]
    private ApplicationDetectionModel? _detection;

    /// <summary>Default installation priority (1-100, lower = higher priority).</summary>
    [ObservableProperty]
    [Range(1, 100, ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Priority_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    private int _defaultPriority = 50;

    /// <summary>Whether this application is required by default in profiles.</summary>
    [ObservableProperty]
    private bool _defaultRequired;

    /// <summary>Environment restrictions (e.g., "VM", "Physical").</summary>
    [ObservableProperty]
    private List<string> _environmentRestrictions = new();

    /// <summary>Tags for categorization and search.</summary>
    [ObservableProperty]
    private List<string> _tags = new();

    /// <summary>Date when the application was last verified.</summary>
    [ObservableProperty]
    private string _lastVerified = string.Empty;

    /// <summary>Whether the application configuration has been verified.</summary>
    [ObservableProperty]
    private bool _verified;

    /// <summary>Official homepage URL.</summary>
    [ObservableProperty]
    [ValidUrl(ErrorMessageResourceName = nameof(Resources.Resources.Validation_EditApp_Homepage_InvalidUrl), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _homepage = string.Empty;

    /// <summary>
    /// Validates the model as a whole (cross-property validation).
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // At least one installation source must be configured
        if (Sources is null || !Sources.HasAnySource())
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_EditApp_Sources_Required,
                new[] { nameof(Sources) });
        }
    }

    /// <summary>
    /// Creates a deep clone of this model for editing.
    /// </summary>
    public EditableApplicationModel Clone()
    {
        return new EditableApplicationModel
        {
            AppId = this.AppId,
            Name = this.Name,
            Category = this.Category,
            Description = this.Description,
            Sources = this.Sources.Clone(),
            InstallArguments = this.InstallArguments,
            Detection = this.Detection?.Clone(),
            DefaultPriority = this.DefaultPriority,
            DefaultRequired = this.DefaultRequired,
            EnvironmentRestrictions = new List<string>(this.EnvironmentRestrictions),
            Tags = new List<string>(this.Tags),
            LastVerified = this.LastVerified,
            Verified = this.Verified,
            Homepage = this.Homepage
        };
    }

    /// <summary>
    /// Validates all properties and returns whether the model is valid.
    /// </summary>
    public bool IsValid()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}

/// <summary>
/// Represents all available installation sources for an application.
/// </summary>
public partial class ApplicationSourcesModel : ObservableValidator
{
    /// <summary>Winget package identifier (e.g., "Google.Chrome").</summary>
    [ObservableProperty]
    [StringLength(256, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Sources_Winget_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string? _winget;

    /// <summary>Chocolatey package name (e.g., "googlechrome").</summary>
    [ObservableProperty]
    [StringLength(256, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Sources_Chocolatey_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string? _chocolatey;

    /// <summary>Microsoft Store app ID (e.g., "9NBLGGH4NNS1").</summary>
    [ObservableProperty]
    [RegularExpression(@"^[A-Za-z0-9]{12}$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Sources_Store_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string? _store;

    /// <summary>Direct download URL for the installer.</summary>
    [ObservableProperty]
    [ValidUrl(ErrorMessageResourceName = nameof(Resources.Resources.Validation_Sources_DirectUrl_InvalidUrl), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string? _directUrl;

    /// <summary>Expected Authenticode publisher subject for direct downloads.</summary>
    [ObservableProperty]
    private string? _expectedPublisher;

    /// <summary>Extended Winget configuration.</summary>
    [ObservableProperty]
    private WingetSourceConfig? _wingetConfig;

    /// <summary>Extended Chocolatey configuration.</summary>
    [ObservableProperty]
    private ChocolateySourceConfig? _chocolateyConfig;

    /// <summary>Extended direct download configuration.</summary>
    [ObservableProperty]
    private DirectDownloadSourceConfig? _directDownloadConfig;

    /// <summary>
    /// Checks if at least one installation source is configured.
    /// </summary>
    public bool HasAnySource()
    {
        return !string.IsNullOrWhiteSpace(Winget) ||
               !string.IsNullOrWhiteSpace(Chocolatey) ||
               !string.IsNullOrWhiteSpace(Store) ||
               !string.IsNullOrWhiteSpace(DirectUrl);
    }

    /// <summary>
    /// Gets a summary string of available sources.
    /// </summary>
    public string GetSourcesSummary()
    {
        List<string> sources = new List<string>();
        if (!string.IsNullOrWhiteSpace(Winget)) sources.Add("Winget");
        if (!string.IsNullOrWhiteSpace(Chocolatey)) sources.Add("Chocolatey");
        if (!string.IsNullOrWhiteSpace(Store)) sources.Add("Store");
        if (!string.IsNullOrWhiteSpace(DirectUrl)) sources.Add("DirectDownload");
        return sources.Count > 0 ? string.Join(", ", sources) : "None";
    }

    /// <summary>
    /// Creates a deep clone of this model.
    /// </summary>
    public ApplicationSourcesModel Clone()
    {
        return new ApplicationSourcesModel
        {
            Winget = this.Winget,
            Chocolatey = this.Chocolatey,
            Store = this.Store,
            DirectUrl = this.DirectUrl,
            ExpectedPublisher = this.ExpectedPublisher,
            WingetConfig = this.WingetConfig?.Clone(),
            ChocolateyConfig = this.ChocolateyConfig?.Clone(),
            DirectDownloadConfig = this.DirectDownloadConfig?.Clone()
        };
    }
}

/// <summary>
/// Extended configuration for Winget installation source.
/// </summary>
public partial class WingetSourceConfig : ObservableObject
{
    /// <summary>Specific version to install (empty = latest).</summary>
    [ObservableProperty]
    private string _version = string.Empty;

    /// <summary>Winget source repository (e.g., "winget", "msstore").</summary>
    [ObservableProperty]
    private string _source = string.Empty;

    /// <summary>Additional installation arguments.</summary>
    [ObservableProperty]
    private string _additionalArgs = string.Empty;

    /// <summary>
    /// Creates a deep clone of this model.
    /// </summary>
    public WingetSourceConfig Clone()
    {
        return new WingetSourceConfig
        {
            Version = this.Version,
            Source = this.Source,
            AdditionalArgs = this.AdditionalArgs
        };
    }
}

/// <summary>
/// Extended configuration for Chocolatey installation source.
/// </summary>
public partial class ChocolateySourceConfig : ObservableObject
{
    /// <summary>Specific version to install (empty = latest).</summary>
    [ObservableProperty]
    private string _version = string.Empty;

    /// <summary>Additional installation arguments.</summary>
    [ObservableProperty]
    private string _additionalArgs = string.Empty;

    /// <summary>
    /// Creates a deep clone of this model.
    /// </summary>
    public ChocolateySourceConfig Clone()
    {
        return new ChocolateySourceConfig
        {
            Version = this.Version,
            AdditionalArgs = this.AdditionalArgs
        };
    }
}

/// <summary>
/// Extended configuration for direct download installation source.
/// </summary>
public partial class DirectDownloadSourceConfig : ObservableValidator
{
    /// <summary>Type of installer (exe, msi, msix, zip).</summary>
    [ObservableProperty]
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_DirectDownload_InstallerType_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _installerType = "exe";

    /// <summary>Silent installation arguments.</summary>
    [ObservableProperty]
    private string _silentArgs = string.Empty;

    /// <summary>Expected file checksum (format: "sha256:hash").</summary>
    [ObservableProperty]
    [RegularExpression(@"^sha256:[a-fA-F0-9]{64}$",
        ErrorMessageResourceName = nameof(Resources.Resources.Validation_DirectDownload_Checksum_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    private string _checksum = string.Empty;

    /// <summary>Expected file name after download.</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>
    /// Creates a deep clone of this model.
    /// </summary>
    public DirectDownloadSourceConfig Clone()
    {
        return new DirectDownloadSourceConfig
        {
            InstallerType = this.InstallerType,
            SilentArgs = this.SilentArgs,
            Checksum = this.Checksum,
            FileName = this.FileName
        };
    }
}

/// <summary>
/// Detection configuration for checking if an application is installed.
/// </summary>
public partial class ApplicationDetectionModel : ObservableObject
{
    /// <summary>Detection method (Registry, File, Command, WindowsFeature).</summary>
    [ObservableProperty]
    private string _method = "Registry";

    /// <summary>Path for detection (registry path, file path, or command).</summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>Registry value name to check for version.</summary>
    [ObservableProperty]
    private string _versionKey = string.Empty;

    /// <summary>Minimum required version.</summary>
    [ObservableProperty]
    private string _minVersion = string.Empty;

    /// <summary>
    /// Creates a deep clone of this model.
    /// </summary>
    public ApplicationDetectionModel Clone()
    {
        return new ApplicationDetectionModel
        {
            Method = this.Method,
            Path = this.Path,
            VersionKey = this.VersionKey,
            MinVersion = this.MinVersion
        };
    }
}
