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

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Application Editor dialog.
/// Handles add/edit operations with validation and dirty tracking.
/// </summary>
public partial class ApplicationEditorViewModel : ObservableObject
{
    private readonly IApplicationDatabaseService _databaseService;
    private readonly IPackageVerificationService _verificationService;
    private EditableApplicationModel? _originalApplication;
    private bool _isDirty;

    /// <summary>
    /// The application being edited.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    [NotifyPropertyChangedFor(nameof(CanEditAppId))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private EditableApplicationModel _application = new();

    /// <summary>
    /// Whether this is a new application (vs editing existing).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    [NotifyPropertyChangedFor(nameof(CanEditAppId))]
    private bool _isNewApplication = true;

    /// <summary>
    /// Available categories for selection.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    /// <summary>
    /// Selected category in the ComboBox.
    /// </summary>
    [ObservableProperty]
    private string? _selectedCategory;

    /// <summary>
    /// Whether a save operation is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    /// <summary>
    /// Validation error message to display.
    /// </summary>
    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// Whether the dialog result is success (saved).
    /// </summary>
    public bool DialogResult { get; private set; }

    /// <summary>
    /// Event raised when the dialog should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when user confirmation is needed (dirty discard).
    /// </summary>
    public event EventHandler<ConfirmDiscardEventArgs>? ConfirmDiscardRequested;

    /// <summary>
    /// Event raised when a new category input is needed.
    /// </summary>
    public event EventHandler<NewCategoryEventArgs>? NewCategoryRequested;

    /// <summary>
    /// Dialog title based on mode.
    /// </summary>
    public string DialogTitle => IsNewApplication
        ? Loc.AppDb_AddTitle
        : Loc.AppDb_EditTitle;

    /// <summary>
    /// Whether the App ID field can be edited (only for new apps).
    /// </summary>
    public bool CanEditAppId => IsNewApplication;

    /// <summary>
    /// Whether the form has unsaved changes.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>
    /// Special category value for adding a new category.
    /// </summary>
    public static string AddNewCategoryValue => "__ADD_NEW__";

    /// <summary>
    /// Whether the Winget source is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _wingetEnabled = true;

    /// <summary>
    /// Whether the Chocolatey source is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _chocolateyEnabled = true;

    /// <summary>
    /// Whether the Store source is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _storeEnabled = true;

    /// <summary>
    /// Whether the Direct Download source is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _directDownloadEnabled = true;

    /// <summary>
    /// Whether source verification is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isVerifying;

    /// <summary>
    /// Result message from verification.
    /// </summary>
    [ObservableProperty]
    private string _verificationResult = string.Empty;

    /// <summary>
    /// Whether verification was successful.
    /// </summary>
    [ObservableProperty]
    private bool _verificationSuccess;

    /// <summary>
    /// Initializes a new instance of ApplicationEditorViewModel.
    /// </summary>
    /// <param name="databaseService">Application database service.</param>
    /// <param name="verificationService">Package verification service.</param>
    public ApplicationEditorViewModel(
        IApplicationDatabaseService databaseService,
        IPackageVerificationService verificationService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
    }

    /// <summary>
    /// Initializes the editor for a new or existing application.
    /// </summary>
    /// <param name="application">The application to edit (or new empty model).</param>
    /// <param name="isNew">Whether this is a new application.</param>
    public async Task InitializeAsync(EditableApplicationModel application, bool isNew)
    {
        IsNewApplication = isNew;
        Application = application;
        _originalApplication = application.Clone();

        // Ensure sources and config objects exist
        Application.Sources ??= new ApplicationSourcesModel();
        Application.Sources.WingetConfig ??= new WingetSourceConfig();
        Application.Sources.ChocolateyConfig ??= new ChocolateySourceConfig();
        Application.Sources.DirectDownloadConfig ??= new DirectDownloadSourceConfig();

        // Initialize source enabled states based on content
        WingetEnabled = !string.IsNullOrWhiteSpace(Application.Sources.Winget);
        ChocolateyEnabled = !string.IsNullOrWhiteSpace(Application.Sources.Chocolatey);
        StoreEnabled = !string.IsNullOrWhiteSpace(Application.Sources.Store);
        DirectDownloadEnabled = !string.IsNullOrWhiteSpace(Application.Sources.DirectUrl);

        // For new apps, enable all sources by default
        if (isNew)
        {
            WingetEnabled = true;
            ChocolateyEnabled = true;
            StoreEnabled = true;
            DirectDownloadEnabled = true;
        }

        // Subscribe to property changes for dirty tracking
        Application.PropertyChanged += OnApplicationPropertyChanged;
        Application.Sources.PropertyChanged += OnApplicationPropertyChanged;
        Application.Sources.WingetConfig.PropertyChanged += OnApplicationPropertyChanged;
        Application.Sources.ChocolateyConfig.PropertyChanged += OnApplicationPropertyChanged;
        Application.Sources.DirectDownloadConfig.PropertyChanged += OnApplicationPropertyChanged;

        // Load categories
        await LoadCategoriesAsync();

        // Set selected category
        if (!string.IsNullOrEmpty(Application.Category) && Categories.Contains(Application.Category))
        {
            SelectedCategory = Application.Category;
        }

        IsDirty = false;
        ValidationMessage = string.Empty;
    }

    /// <summary>
    /// Loads available categories from the database.
    /// </summary>
    private async Task LoadCategoriesAsync()
    {
        var existingCategories = await _databaseService.GetCategoriesAsync();

        Categories.Clear();
        foreach (var category in existingCategories.OrderBy(c => c))
        {
            Categories.Add(category);
        }

        // Add "Add new..." option
        Categories.Add(Loc.AppEditor_AddNewCategory);
    }

    /// <summary>
    /// Handles property changes on the application model.
    /// </summary>
    private void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        SaveCommand.NotifyCanExecuteChanged();

        // Clear validation on changes
        if (!string.IsNullOrEmpty(ValidationMessage))
        {
            ValidationMessage = string.Empty;
        }
    }

    /// <summary>
    /// Handles category selection changes.
    /// </summary>
    partial void OnSelectedCategoryChanged(string? value)
    {
        if (value == Loc.AppEditor_AddNewCategory)
        {
            // Request new category input
            var args = new NewCategoryEventArgs();
            NewCategoryRequested?.Invoke(this, args);

            if (!string.IsNullOrWhiteSpace(args.NewCategory))
            {
                // Add new category and select it
                var newCategory = args.NewCategory.Trim();
                var insertIndex = Categories.Count - 1; // Before "Add new..."

                // Find correct sorted position
                for (int i = 0; i < Categories.Count - 1; i++)
                {
                    if (string.Compare(Categories[i], newCategory, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        insertIndex = i;
                        break;
                    }
                }

                if (!Categories.Contains(newCategory))
                {
                    Categories.Insert(insertIndex, newCategory);
                }

                Application.Category = newCategory;
                SelectedCategory = newCategory;
            }
            else
            {
                // Cancelled - revert to previous value
                SelectedCategory = Application.Category;
            }
        }
        else if (value != null)
        {
            Application.Category = value;
        }
    }

    /// <summary>
    /// Determines if the save command can execute.
    /// </summary>
    private bool CanSave() => !IsSaving && Application != null;

    /// <summary>
    /// Verifies all configured sources.
    /// </summary>
    [RelayCommand]
    private async Task VerifySourcesAsync()
    {
        if (Application?.Sources == null) return;

        IsVerifying = true;
        VerificationResult = Loc.Verify_Verifying;
        VerificationSuccess = false;

        try
        {
            var sources = new ApplicationSourcesForVerification(
                WingetEnabled ? Application.Sources.Winget : null,
                ChocolateyEnabled ? Application.Sources.Chocolatey : null,
                StoreEnabled ? Application.Sources.Store : null,
                DirectDownloadEnabled ? Application.Sources.DirectUrl : null
            );

            var result = await _verificationService.VerifyAllSourcesAsync(sources);

            if (result.HasValidSource)
            {
                VerificationSuccess = true;
                VerificationResult = string.Format(
                    Loc.Verify_AllSourcesValid,
                    result.ValidSourceCount,
                    result.CheckedCount);
            }
            else
            {
                VerificationSuccess = false;
                VerificationResult = Loc.Verify_NoValidSources;
            }
        }
        catch (Exception ex)
        {
            VerificationSuccess = false;
            VerificationResult = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsVerifying = false;
        }
    }

    /// <summary>
    /// Saves the application.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Application == null) return;

        IsSaving = true;
        ValidationMessage = string.Empty;

        try
        {
            // Validate
            var validation = await _databaseService.ValidateApplicationAsync(Application, IsNewApplication);
            if (!validation.IsValid)
            {
                ValidationMessage = string.Join("\n", validation.Errors.Select(e => $"{e.Field}: {e.Message}"));
                return;
            }

            // Save
            var result = await _databaseService.SaveApplicationAsync(Application, IsNewApplication);
            if (result.Success)
            {
                DialogResult = true;
                IsDirty = false;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ValidationMessage = result.ErrorMessage ?? Loc.Apps_SaveFailed;
                if (result.ValidationErrors?.Any() == true)
                {
                    ValidationMessage += "\n" + string.Join("\n", result.ValidationErrors);
                }
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Cancels the edit operation.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsDirty)
        {
            var args = new ConfirmDiscardEventArgs();
            ConfirmDiscardRequested?.Invoke(this, args);

            if (!args.Discard)
            {
                return;
            }
        }

        DialogResult = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Cleans up event subscriptions.
    /// </summary>
    public void Cleanup()
    {
        if (Application != null)
        {
            Application.PropertyChanged -= OnApplicationPropertyChanged;
            if (Application.Sources != null)
            {
                Application.Sources.PropertyChanged -= OnApplicationPropertyChanged;
                if (Application.Sources.WingetConfig != null)
                {
                    Application.Sources.WingetConfig.PropertyChanged -= OnApplicationPropertyChanged;
                }
                if (Application.Sources.ChocolateyConfig != null)
                {
                    Application.Sources.ChocolateyConfig.PropertyChanged -= OnApplicationPropertyChanged;
                }
                if (Application.Sources.DirectDownloadConfig != null)
                {
                    Application.Sources.DirectDownloadConfig.PropertyChanged -= OnApplicationPropertyChanged;
                }
            }
        }
    }
}

/// <summary>
/// Event arguments for confirming discard of changes.
/// </summary>
public class ConfirmDiscardEventArgs : EventArgs
{
    /// <summary>
    /// Whether the user confirmed discarding changes.
    /// </summary>
    public bool Discard { get; set; }
}

/// <summary>
/// Event arguments for requesting a new category name.
/// </summary>
public class NewCategoryEventArgs : EventArgs
{
    /// <summary>
    /// The new category name entered by the user.
    /// </summary>
    public string? NewCategory { get; set; }
}
