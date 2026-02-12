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
using System.Text.RegularExpressions;
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
    private readonly IPackageSearchService _packageSearchService;
    private EditableApplicationModel? _originalApplication;
    private bool _isDirty;
    private const int PackageSearchLimit = 20;

    [GeneratedRegex(@"[^A-Za-z0-9\.\-_]")]
    private static partial Regex AppIdSanitizerRegex();

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
    /// Winget package search query.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchWingetPackagesCommand))]
    private string _wingetSearchQuery = string.Empty;

    /// <summary>
    /// Chocolatey package search query.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchChocolateyPackagesCommand))]
    private string _chocolateySearchQuery = string.Empty;

    /// <summary>
    /// Microsoft Store package search query.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchStorePackagesCommand))]
    private string _storeSearchQuery = string.Empty;

    /// <summary>
    /// Winget search results.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PackageSearchResult> _wingetSearchResults = new();

    /// <summary>
    /// Chocolatey search results.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PackageSearchResult> _chocolateySearchResults = new();

    /// <summary>
    /// Store search results.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PackageSearchResult> _storeSearchResults = new();

    /// <summary>
    /// Currently selected Winget search result.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyWingetSearchResultCommand))]
    private PackageSearchResult? _selectedWingetSearchResult;

    /// <summary>
    /// Currently selected Chocolatey search result.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyChocolateySearchResultCommand))]
    private PackageSearchResult? _selectedChocolateySearchResult;

    /// <summary>
    /// Currently selected Store search result.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyStoreSearchResultCommand))]
    private PackageSearchResult? _selectedStoreSearchResult;

    /// <summary>
    /// Winget search status message.
    /// </summary>
    [ObservableProperty]
    private string _wingetSearchStatus = string.Empty;

    /// <summary>
    /// Chocolatey search status message.
    /// </summary>
    [ObservableProperty]
    private string _chocolateySearchStatus = string.Empty;

    /// <summary>
    /// Store search status message.
    /// </summary>
    [ObservableProperty]
    private string _storeSearchStatus = string.Empty;

    /// <summary>
    /// Whether Winget package search is running.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchWingetPackagesCommand))]
    private bool _isWingetSearching;

    /// <summary>
    /// Whether Chocolatey package search is running.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchChocolateyPackagesCommand))]
    private bool _isChocolateySearching;

    /// <summary>
    /// Whether Store package search is running.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchStorePackagesCommand))]
    private bool _isStoreSearching;

    /// <summary>
    /// Initializes a new instance of ApplicationEditorViewModel.
    /// </summary>
    /// <param name="databaseService">Application database service.</param>
    /// <param name="verificationService">Package verification service.</param>
    /// <param name="packageSearchService">Package search service.</param>
    public ApplicationEditorViewModel(
        IApplicationDatabaseService databaseService,
        IPackageVerificationService verificationService,
        IPackageSearchService packageSearchService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
        _packageSearchService = packageSearchService ?? throw new ArgumentNullException(nameof(packageSearchService));
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

        ResetSearchState();

        IsDirty = false;
        ValidationMessage = string.Empty;
    }

    /// <summary>
    /// Resets package discovery state for a fresh editor session.
    /// </summary>
    private void ResetSearchState()
    {
        WingetSearchQuery = string.Empty;
        ChocolateySearchQuery = string.Empty;
        StoreSearchQuery = string.Empty;

        WingetSearchStatus = string.Empty;
        ChocolateySearchStatus = string.Empty;
        StoreSearchStatus = string.Empty;

        WingetSearchResults.Clear();
        ChocolateySearchResults.Clear();
        StoreSearchResults.Clear();

        SelectedWingetSearchResult = null;
        SelectedChocolateySearchResult = null;
        SelectedStoreSearchResult = null;
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
    /// Determines if Winget package search can execute.
    /// </summary>
    private bool CanSearchWingetPackages() =>
        !IsWingetSearching && !string.IsNullOrWhiteSpace(WingetSearchQuery);

    /// <summary>
    /// Determines if Chocolatey package search can execute.
    /// </summary>
    private bool CanSearchChocolateyPackages() =>
        !IsChocolateySearching && !string.IsNullOrWhiteSpace(ChocolateySearchQuery);

    /// <summary>
    /// Determines if Store package search can execute.
    /// </summary>
    private bool CanSearchStorePackages() =>
        !IsStoreSearching && !string.IsNullOrWhiteSpace(StoreSearchQuery);

    /// <summary>
    /// Searches Winget packages by query.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSearchWingetPackages))]
    private async Task SearchWingetPackagesAsync()
    {
        var query = WingetSearchQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            WingetSearchStatus = string.Empty;
            WingetSearchResults.Clear();
            SelectedWingetSearchResult = null;
            return;
        }

        if (!_packageSearchService.IsWingetAvailable)
        {
            WingetSearchStatus = Loc.Verify_WingetUnavailable;
            WingetSearchResults.Clear();
            SelectedWingetSearchResult = null;
            return;
        }

        IsWingetSearching = true;
        WingetSearchStatus = Loc.AppEditor_Searching;

        try
        {
            var results = await _packageSearchService.SearchWingetAsync(query, PackageSearchLimit);
            SetSearchResults(WingetSearchResults, results);

            SelectedWingetSearchResult = WingetSearchResults.FirstOrDefault();
            WingetSearchStatus = results.Count == 0
                ? Loc.AppEditor_NoSearchResults
                : string.Format(Loc.AppEditor_SearchResultsCount, results.Count);
        }
        catch (Exception ex)
        {
            WingetSearchStatus = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsWingetSearching = false;
        }
    }

    /// <summary>
    /// Searches Chocolatey packages by query.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSearchChocolateyPackages))]
    private async Task SearchChocolateyPackagesAsync()
    {
        var query = ChocolateySearchQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ChocolateySearchStatus = string.Empty;
            ChocolateySearchResults.Clear();
            SelectedChocolateySearchResult = null;
            return;
        }

        if (!_packageSearchService.IsChocolateyAvailable)
        {
            ChocolateySearchStatus = Loc.Verify_ChocoUnavailable;
            ChocolateySearchResults.Clear();
            SelectedChocolateySearchResult = null;
            return;
        }

        IsChocolateySearching = true;
        ChocolateySearchStatus = Loc.AppEditor_Searching;

        try
        {
            var results = await _packageSearchService.SearchChocolateyAsync(query, PackageSearchLimit);
            SetSearchResults(ChocolateySearchResults, results);

            SelectedChocolateySearchResult = ChocolateySearchResults.FirstOrDefault();
            ChocolateySearchStatus = results.Count == 0
                ? Loc.AppEditor_NoSearchResults
                : string.Format(Loc.AppEditor_SearchResultsCount, results.Count);
        }
        catch (Exception ex)
        {
            ChocolateySearchStatus = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsChocolateySearching = false;
        }
    }

    /// <summary>
    /// Searches Microsoft Store packages by query.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSearchStorePackages))]
    private async Task SearchStorePackagesAsync()
    {
        var query = StoreSearchQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            StoreSearchStatus = string.Empty;
            StoreSearchResults.Clear();
            SelectedStoreSearchResult = null;
            return;
        }

        if (!_packageSearchService.IsWingetAvailable)
        {
            StoreSearchStatus = Loc.Verify_WingetUnavailable;
            StoreSearchResults.Clear();
            SelectedStoreSearchResult = null;
            return;
        }

        IsStoreSearching = true;
        StoreSearchStatus = Loc.AppEditor_Searching;

        try
        {
            var results = await _packageSearchService.SearchStoreAsync(query, PackageSearchLimit);
            SetSearchResults(StoreSearchResults, results);

            SelectedStoreSearchResult = StoreSearchResults.FirstOrDefault();
            StoreSearchStatus = results.Count == 0
                ? Loc.AppEditor_NoSearchResults
                : string.Format(Loc.AppEditor_SearchResultsCount, results.Count);
        }
        catch (Exception ex)
        {
            StoreSearchStatus = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsStoreSearching = false;
        }
    }

    /// <summary>
    /// Determines if Winget search result can be applied.
    /// </summary>
    private bool CanApplyWingetSearchResult() => SelectedWingetSearchResult != null;

    /// <summary>
    /// Applies selected Winget search result to the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyWingetSearchResult))]
    private void ApplyWingetSearchResult()
    {
        if (SelectedWingetSearchResult == null || Application?.Sources == null) return;

        Application.Sources.Winget = SelectedWingetSearchResult.PackageId;
        Application.Sources.WingetConfig ??= new WingetSourceConfig();

        if (string.IsNullOrWhiteSpace(Application.Sources.WingetConfig.Source))
        {
            Application.Sources.WingetConfig.Source = "winget";
        }

        WingetEnabled = true;
        PrefillMetadataFromSearchResult(SelectedWingetSearchResult);
        WingetSearchStatus = string.Format(Loc.AppEditor_SearchApplied, SelectedWingetSearchResult.PackageId);
    }

    /// <summary>
    /// Determines if Chocolatey search result can be applied.
    /// </summary>
    private bool CanApplyChocolateySearchResult() => SelectedChocolateySearchResult != null;

    /// <summary>
    /// Applies selected Chocolatey search result to the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyChocolateySearchResult))]
    private void ApplyChocolateySearchResult()
    {
        if (SelectedChocolateySearchResult == null || Application?.Sources == null) return;

        Application.Sources.Chocolatey = SelectedChocolateySearchResult.PackageId;
        Application.Sources.ChocolateyConfig ??= new ChocolateySourceConfig();

        ChocolateyEnabled = true;
        PrefillMetadataFromSearchResult(SelectedChocolateySearchResult);
        ChocolateySearchStatus = string.Format(Loc.AppEditor_SearchApplied, SelectedChocolateySearchResult.PackageId);
    }

    /// <summary>
    /// Determines if Store search result can be applied.
    /// </summary>
    private bool CanApplyStoreSearchResult() => SelectedStoreSearchResult != null;

    /// <summary>
    /// Applies selected Store search result to the editor.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyStoreSearchResult))]
    private void ApplyStoreSearchResult()
    {
        if (SelectedStoreSearchResult == null || Application?.Sources == null) return;

        Application.Sources.Store = SelectedStoreSearchResult.PackageId;
        Application.Sources.WingetConfig ??= new WingetSourceConfig();

        if (string.IsNullOrWhiteSpace(Application.Sources.WingetConfig.Source))
        {
            Application.Sources.WingetConfig.Source = "msstore";
        }

        StoreEnabled = true;
        PrefillMetadataFromSearchResult(SelectedStoreSearchResult);
        StoreSearchStatus = string.Format(Loc.AppEditor_SearchApplied, SelectedStoreSearchResult.PackageId);
    }

    /// <summary>
    /// Replaces search collection content while preserving object reference for bindings.
    /// </summary>
    private static void SetSearchResults(
        ObservableCollection<PackageSearchResult> target,
        IReadOnlyList<PackageSearchResult> results)
    {
        target.Clear();
        foreach (var result in results)
        {
            target.Add(result);
        }
    }

    /// <summary>
    /// Prefills app metadata from a package search result when fields are still empty.
    /// </summary>
    private void PrefillMetadataFromSearchResult(PackageSearchResult result)
    {
        if (Application == null) return;

        if (string.IsNullOrWhiteSpace(Application.Name) && !string.IsNullOrWhiteSpace(result.DisplayName))
        {
            Application.Name = result.DisplayName.Trim();
        }

        if (IsNewApplication && string.IsNullOrWhiteSpace(Application.AppId))
        {
            var candidate = BuildAppIdCandidate(result);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                Application.AppId = candidate;
            }
        }
    }

    /// <summary>
    /// Builds a valid AppId candidate from a selected package.
    /// </summary>
    private static string BuildAppIdCandidate(PackageSearchResult result)
    {
        var seed = !string.IsNullOrWhiteSpace(result.PackageId)
            ? result.PackageId
            : result.DisplayName;

        var normalized = AppIdSanitizerRegex().Replace(seed ?? string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = AppIdSanitizerRegex().Replace(result.DisplayName ?? string.Empty, string.Empty);
        }

        if (normalized.Length > 64)
        {
            normalized = normalized[..64];
        }

        return normalized;
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
