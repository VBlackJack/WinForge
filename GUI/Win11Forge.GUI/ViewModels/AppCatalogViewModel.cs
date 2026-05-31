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
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the App Catalog view.
/// Provides CRUD operations for managing application definitions.
/// </summary>
public partial class AppCatalogViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationDatabaseService _databaseService;
    private readonly IUndoService _undoService;
    private readonly IPackageVerificationService _verificationService;
    private readonly IApplicationEditorDialogService _applicationEditorDialogService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ICollectionView _applicationsView;
    private CancellationTokenSource? _searchDebounceTokenSource;
    private CancellationTokenSource? _verificationCts;
    private const int SearchDebounceMs = 300;
    private const int DuplicateAppIdMaxLength = 128;

    private static string GetLocalizedString(string resourceKey, string fallback)
    {
        return Loc.ResourceManager.GetString(resourceKey, Loc.Culture) ?? fallback;
    }

    private static string FormatLocalized(string resourceKey, string fallbackFormat, params object[] args)
    {
        var format = GetLocalizedString(resourceKey, fallbackFormat);
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }

    /// <summary>
    /// All applications loaded from the database.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<EditableApplicationModel> _applications = new();

    /// <summary>
    /// Currently selected application in the list.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifySelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
    private EditableApplicationModel? _selectedApplication;

    /// <summary>
    /// Search text for filtering applications.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Selected category filter.
    /// </summary>
    [ObservableProperty]
    private string? _selectedCategory;

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    /// <summary>
    /// Indicates if data is currently loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>
    /// Indicates if the last database load failed.
    /// </summary>
    [ObservableProperty]
    private bool _hasLoadError;

    /// <summary>
    /// Error message from the last failed database load.
    /// </summary>
    [ObservableProperty]
    private string? _loadErrorMessage;

    /// <summary>
    /// Indicates if verification is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyAllSourcesCommand))]
    private bool _isVerifying;

    /// <summary>
    /// Progress of batch verification (0-100).
    /// </summary>
    [ObservableProperty]
    private int _verificationProgress;

    /// <summary>
    /// Status message displayed to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Total count of applications in the database.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    private int _totalCount;

    /// <summary>
    /// Count of filtered applications.
    /// </summary>
    [ObservableProperty]
    private int _filteredCount;

    /// <summary>
    /// Whether undo is available.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    /// <summary>
    /// Whether redo is available.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private bool _canRedo;

    /// <summary>
    /// Description of the next undo action.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UndoButtonTooltip))]
    private string? _nextUndoDescription;

    /// <summary>
    /// Description of the next redo action.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RedoButtonTooltip))]
    private string? _nextRedoDescription;

    /// <summary>
    /// Localized tooltip for the Undo button.
    /// </summary>
    public string UndoButtonTooltip => NextUndoDescription != null
        ? string.Format(CultureInfo.CurrentCulture, Loc.AppCatalog_UndoTooltipFormat, NextUndoDescription)
        : Loc.AppCatalog_UndoTooltipDefault;

    /// <summary>
    /// Localized tooltip for the Redo button.
    /// </summary>
    public string RedoButtonTooltip => NextRedoDescription != null
        ? string.Format(CultureInfo.CurrentCulture, Loc.AppCatalog_RedoTooltipFormat, NextRedoDescription)
        : Loc.AppCatalog_RedoTooltipDefault;

    /// <summary>
    /// Message shown when the App Catalog is cleanly loaded but has no visible rows.
    /// </summary>
    public string EmptyStateMessage => TotalCount == 0
        ? GetLocalizedString("AppCatalog_EmptyDatabase", "No applications in database. Add one to get started.")
        : GetLocalizedString("AppCatalog_EmptyFilter", "No applications match the current filter. Try clearing it.");

    /// <summary>
    /// Initializes a new instance of AppCatalogViewModel.
    /// </summary>
    /// <param name="databaseService">Application database service.</param>
    /// <param name="undoService">Undo/redo service.</param>
    /// <param name="verificationService">Package verification service.</param>
    public AppCatalogViewModel(
        IApplicationDatabaseService databaseService,
        IUndoService undoService,
        IPackageVerificationService verificationService,
        IApplicationEditorDialogService? applicationEditorDialogService = null,
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
        _applicationEditorDialogService = applicationEditorDialogService ?? new ApplicationEditorDialogService();
        _dialogService = dialogService ?? new DialogService();
        _fileDialogService = fileDialogService ?? new FileDialogService();

        // Setup collection view for filtering
        _applicationsView = CollectionViewSource.GetDefaultView(Applications);
        _applicationsView.Filter = FilterApplication;
        _applicationsView.SortDescriptions.Add(new SortDescription(nameof(EditableApplicationModel.Name), ListSortDirection.Ascending));

        // Subscribe to database changes
        _databaseService.DatabaseChanged += OnDatabaseChanged;

        // Subscribe to undo service state changes
        _undoService.StateChanged += OnUndoStateChanged;
    }

    /// <summary>
    /// Gets the filtered view of applications.
    /// </summary>
    public ICollectionView AppCatalogView => _applicationsView;

    /// <summary>
    /// Command to load applications from the database.
    /// </summary>
    [RelayCommand]
    private async Task LoadApplicationsAsync()
    {
        IsLoading = true;
        HasLoadError = false;
        LoadErrorMessage = null;
        StatusMessage = Loc.AppCatalog_Loading;

        try
        {
            var apps = await _databaseService.LoadApplicationsAsync();
            var categories = await _databaseService.GetCategoriesAsync();

            Applications.Clear();
            foreach (var app in apps)
            {
                Applications.Add(app);
            }

            Categories.Clear();
            Categories.Add(Loc.Apps_CategoryAll);
            foreach (var category in categories.OrderBy(c => c))
            {
                Categories.Add(category);
            }

            SelectedCategory = Loc.Apps_CategoryAll;
            TotalCount = Applications.Count;
            UpdateFilteredCount();
            HasLoadError = false;
            LoadErrorMessage = null;
            StatusMessage = string.Format(Loc.AppCatalog_LoadedCount, TotalCount);

            // Notify commands that depend on Applications.Count
            VerifyAllSourcesCommand.NotifyCanExecuteChanged();
        }
        catch (ApplicationDatabaseException ex)
        {
            SetLoadError(ex);
            Debug.WriteLine($"ApplicationDatabaseException in LoadApplicationsAsync: {ex}");
        }
        catch (Exception ex)
        {
            SetLoadError(ex);
            Debug.WriteLine($"Unexpected exception in LoadApplicationsAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to add a new application.
    /// </summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        await OpenEditorForAddAsync();
    }

    /// <summary>
    /// Command to edit the selected application.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditAsync()
    {
        if (SelectedApplication == null) return;

        var original = SelectedApplication.Clone();
        await OpenEditorForEditAsync(SelectedApplication, original);
    }

    /// <summary>
    /// Command to delete the selected application.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedApplication == null) return;

        var confirmed = await ConfirmDeleteAsync(SelectedApplication);
        if (!confirmed) return;

        IsLoading = true;
        StatusMessage = Loc.AppCatalog_Deleting;

        try
        {
            var success = await DeleteApplicationWithUndoAsync(SelectedApplication);
            if (success)
            {
                Applications.Remove(SelectedApplication);
                SelectedApplication = null;
                TotalCount = Applications.Count;
                UpdateFilteredCount();
                StatusMessage = Loc.AppCatalog_Deleted;
            }
            else
            {
                StatusMessage = Loc.AppCatalog_DeleteError;
            }
        }
        catch (ApplicationDatabaseException ex)
        {
            StatusMessage = string.Format(Loc.AppCatalog_DeleteError, ex.Message);
            Debug.WriteLine($"ApplicationDatabaseException in DeleteSelectedApplicationAsync: {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.AppCatalog_DeleteError, ex.Message);
            Debug.WriteLine($"Unexpected exception in DeleteSelectedApplicationAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to duplicate the selected application.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private async Task DuplicateAsync()
    {
        if (SelectedApplication == null) return;

        var duplicate = CreateDuplicateApplication(SelectedApplication);
        await OpenEditorForAddAsync(duplicate);
    }

    /// <summary>
    /// Command to refresh the applications list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadApplicationsAsync();
    }

    /// <summary>
    /// Command to retry loading the applications database after a load failure.
    /// </summary>
    [RelayCommand]
    private async Task RetryLoadAsync()
    {
        await LoadApplicationsAsync();
    }

    /// <summary>
    /// Command to clear all filters.
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedCategory = Loc.Apps_CategoryAll;
    }

    /// <summary>
    /// Command to undo the last action.
    /// </summary>
    [RelayCommand(CanExecute = nameof(GetCanUndo))]
    private async Task UndoAsync()
    {
        var success = await _undoService.UndoAsync();
        if (success)
        {
            StatusMessage = string.Format(Loc.Undo_ActionUndone, _undoService.NextRedoDescription ?? Loc.Undo_Action);
        }
    }

    /// <summary>
    /// Command to redo the last undone action.
    /// </summary>
    [RelayCommand(CanExecute = nameof(GetCanRedo))]
    private async Task RedoAsync()
    {
        var success = await _undoService.RedoAsync();
        if (success)
        {
            StatusMessage = string.Format(Loc.Undo_ActionRedone, _undoService.NextUndoDescription ?? Loc.Undo_Action);
        }
    }

    /// <summary>
    /// Command to export selected applications.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedApplications))]
    private async Task ExportSelectedAsync()
    {
        var selectedIds = GetSelectedApplicationIds();
        await ExportApplicationsWithDialogAsync(selectedIds);
    }

    /// <summary>
    /// Command to export all applications.
    /// </summary>
    [RelayCommand]
    private async Task ExportAllAsync()
    {
        var allIds = Applications.Select(a => a.AppId).ToList();
        await ExportApplicationsWithDialogAsync(allIds);
    }

    /// <summary>
    /// Command to import applications.
    /// </summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        await ImportApplicationsWithDialogAsync();
    }

    /// <summary>
    /// Opens the editor for a new application and saves the result.
    /// </summary>
    public async Task OpenEditorForAddAsync(EditableApplicationModel? initialApplication = null)
    {
        try
        {
            var savedApplication = await _applicationEditorDialogService.ShowAddDialogAsync(initialApplication);
            if (savedApplication != null)
            {
                await SaveApplicationAsync(savedApplication, isNew: true);
            }
        }
        catch (Exception ex)
        {
            var message = string.Format(Loc.AppCatalog_EditorOpenError, ex.Message, ex.StackTrace);
            StatusMessage = message;
            Debug.WriteLine($"Application editor add failed: {ex}");
            await _dialogService.ShowErrorAsync(Loc.Common_Error, message);
        }
    }

    /// <summary>
    /// Opens the editor for an existing application and saves the result.
    /// </summary>
    public async Task OpenEditorForEditAsync(EditableApplicationModel application, EditableApplicationModel originalApplication)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(originalApplication);

        try
        {
            var savedApplication = await _applicationEditorDialogService.ShowEditDialogAsync(application);
            if (savedApplication != null)
            {
                await SaveApplicationAsync(savedApplication, isNew: false, originalApplication: originalApplication);
            }
        }
        catch (Exception ex)
        {
            var message = string.Format(Loc.AppCatalog_EditorOpenError, ex.Message, ex.StackTrace);
            StatusMessage = message;
            Debug.WriteLine($"Application editor edit failed: {ex}");
            await _dialogService.ShowErrorAsync(Loc.Common_Error, message);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(EditableApplicationModel application)
    {
        var message = string.Format(
            Loc.AppCatalog_DeleteConfirm,
            application.Name,
            application.AppId);

        return await _dialogService.ShowConfirmAsync(
            Loc.AppCatalog_DeleteTitle,
            message,
            Loc.Confirm_Delete_Btn,
            Loc.Common_Cancel);
    }

    private async Task ImportApplicationsWithDialogAsync()
    {
        try
        {
            var filePath = await _fileDialogService.ShowOpenAsync(new FileDialogOptions(
                Loc.AppCatalog_Import,
                FileDialogFilters.Json,
                DefaultExtension: FileDialogFilters.JsonDefaultExtension));

            if (filePath == null)
            {
                return;
            }

            var replaceExisting = await _dialogService.ShowYesNoCancelAsync(
                Loc.AppCatalog_Import,
                Loc.AppCatalog_ImportModeConfirm,
                Loc.AppCatalog_Import_Replace,
                Loc.AppCatalog_Import_Skip,
                Loc.Common_Cancel);

            if (!replaceExisting.HasValue)
            {
                return;
            }

            var mode = replaceExisting.Value ? ImportMode.Replace : ImportMode.Merge;
            await ImportApplicationsAsync(filePath, mode);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.AppCatalog_ImportError, ex.Message);
            Debug.WriteLine($"Import failed: {ex}");
            await _dialogService.ShowErrorAsync(Loc.Common_Error, StatusMessage);
        }
    }

    private async Task ExportApplicationsWithDialogAsync(IEnumerable<string> appIds)
    {
        var appIdList = appIds.ToList();
        if (appIdList.Count == 0)
        {
            await _dialogService.ShowInfoAsync(Loc.AppCatalog_Export, Loc.AppCatalog_NoExportSelection);
            return;
        }

        try
        {
            var filePath = await _fileDialogService.ShowSaveAsync(new FileDialogOptions(
                Loc.AppCatalog_Export,
                FileDialogFilters.Json,
                DefaultFileName: $"applications-export-{DateTime.Now:yyyyMMdd}",
                DefaultExtension: FileDialogFilters.JsonDefaultExtension));

            if (filePath != null)
            {
                await ExportApplicationsAsync(appIdList, filePath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.AppCatalog_ExportError, ex.Message);
            Debug.WriteLine($"Export failed: {ex}");
            await _dialogService.ShowErrorAsync(Loc.Common_Error, StatusMessage);
        }
    }

    /// <summary>
    /// Command to verify all application sources.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAllSourcesAsync()
    {
        _verificationCts?.Cancel();
        _verificationCts = new CancellationTokenSource();

        IsVerifying = true;
        VerificationProgress = 0;

        var apps = Applications.ToList();
        var total = apps.Count;
        var current = 0;
        var validCount = 0;
        var invalidCount = 0;
        var errorCount = 0;

        try
        {
            foreach (var app in apps)
            {
                if (_verificationCts.Token.IsCancellationRequested)
                {
                    StatusMessage = FormatLocalized(
                        "Verify_CancelledProgress",
                        "Verification cancelled ({0}/{1})",
                        current,
                        total);
                    break;
                }

                StatusMessage = FormatLocalized(
                    "Verify_ProgressItem",
                    "Verifying {0}/{1}: {2}...",
                    current + 1,
                    total,
                    app.Name);

                try
                {
                    var sources = new ApplicationSourcesForVerification(
                        app.Sources?.Winget,
                        app.Sources?.Chocolatey,
                        app.Sources?.Store,
                        app.Sources?.DirectUrl
                    );

                    var result = await _verificationService.VerifyAllSourcesAsync(sources, _verificationCts.Token);

                    app.Verified = result.HasValidSource;
                    if (result.HasValidSource) validCount++;
                    else invalidCount++;
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch
                {
                    // Continue on individual app errors
                    app.Verified = false;
                    errorCount++;
                }

                current++;
                VerificationProgress = (int)((current * 100.0) / total);
            }

            if (!_verificationCts.Token.IsCancellationRequested)
            {
                StatusMessage = errorCount > 0
                    ? FormatLocalized(
                        "Verify_CompleteSummaryWithErrors",
                        "Verification complete: {0} valid, {1} invalid, {2} errors",
                        validCount,
                        invalidCount,
                        errorCount)
                    : FormatLocalized(
                        "Verify_CompleteSummary",
                        "Verification complete: {0} valid, {1} invalid",
                        validCount,
                        invalidCount);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = FormatLocalized(
                "Verify_CancelledProgress",
                "Verification cancelled ({0}/{1})",
                current,
                total);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsVerifying = false;
            VerificationProgress = 0;
        }
    }

    /// <summary>
    /// Command to cancel ongoing verification.
    /// </summary>
    [RelayCommand]
    private void CancelVerification()
    {
        _verificationCts?.Cancel();
    }

    /// <summary>
    /// Command to verify the selected application sources.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanVerifySelected))]
    private async Task VerifySelectedAsync()
    {
        if (SelectedApplication == null) return;

        StatusMessage = string.Format(Loc.Verify_Verifying, SelectedApplication.Name);

        try
        {
            var sources = new ApplicationSourcesForVerification(
                SelectedApplication.Sources?.Winget,
                SelectedApplication.Sources?.Chocolatey,
                SelectedApplication.Sources?.Store,
                SelectedApplication.Sources?.DirectUrl
            );

            var result = await _verificationService.VerifyAllSourcesAsync(sources);

            SelectedApplication.Verified = result.HasValidSource;

            if (result.HasValidSource)
            {
                StatusMessage = string.Format(Loc.Verify_AllSourcesValid, result.ValidSourceCount, result.CheckedCount);
            }
            else
            {
                StatusMessage = Loc.Verify_NoValidSources;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.Verify_Error, ex.Message);
        }
    }

    /// <summary>
    /// Determines if selected verification can be performed.
    /// </summary>
    private bool CanVerifySelected() => SelectedApplication != null && !IsVerifying;

    /// <summary>
    /// Determines if the selected application can be duplicated.
    /// </summary>
    private bool CanDuplicate() => SelectedApplication != null;

    /// <summary>
    /// Determines if edit/delete operations can be performed.
    /// </summary>
    private bool CanEditOrDelete() => SelectedApplication != null;

    /// <summary>
    /// Determines if verification can be performed.
    /// </summary>
    private bool CanVerify() => !IsVerifying && Applications.Count > 0;

    /// <summary>
    /// Determines if there are selected applications for export.
    /// </summary>
    private bool HasSelectedApplications() => SelectedApplication != null;

    /// <summary>
    /// Determines if undo can be performed.
    /// </summary>
    private bool GetCanUndo() => CanUndo;

    /// <summary>
    /// Determines if redo can be performed.
    /// </summary>
    private bool GetCanRedo() => CanRedo;

    /// <summary>
    /// Called when search text changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        // Debounce search to avoid excessive filtering
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceMs, _searchDebounceTokenSource.Token);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _applicationsView.Refresh();
                    UpdateFilteredCount();
                });
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled, ignore
            }
        });
    }

    /// <summary>
    /// Called when selected category changes.
    /// </summary>
    partial void OnSelectedCategoryChanged(string? value)
    {
        _applicationsView.Refresh();
        UpdateFilteredCount();
    }

    /// <summary>
    /// Filter predicate for the applications collection view.
    /// </summary>
    private bool FilterApplication(object obj)
    {
        if (obj is not EditableApplicationModel app) return false;

        // Category filter
        if (!string.IsNullOrEmpty(SelectedCategory) &&
            SelectedCategory != Loc.Apps_CategoryAll &&
            !string.Equals(app.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            return app.AppId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   app.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (app.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   app.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    /// <summary>
    /// Updates the filtered count.
    /// </summary>
    private void UpdateFilteredCount()
    {
        FilteredCount = _applicationsView.Cast<object>().Count();
    }

    /// <summary>
    /// Handles database change events.
    /// </summary>
    private void OnDatabaseChanged(object? sender, DatabaseChangedEventArgs e)
    {
        // Reload on any database change
        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await LoadApplicationsAsync();
        });
    }

    private static EditableApplicationModel CreateDuplicateApplication(EditableApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var duplicate = application.Clone();
        duplicate.AppId = CreateDuplicateAppId(application.AppId);
        duplicate.Name = $"{application.Name} (Copy)";
        return duplicate;
    }

    private static string CreateDuplicateAppId(string appId)
    {
        var sourceAppId = string.IsNullOrWhiteSpace(appId) ? "App" : appId.Trim();
        var suffix = $"-copy-{Guid.NewGuid():N}";
        var maxBaseLength = Math.Max(1, DuplicateAppIdMaxLength - suffix.Length);

        if (sourceAppId.Length > maxBaseLength)
        {
            sourceAppId = sourceAppId[..maxBaseLength].TrimEnd('.', '-', '_');
        }

        if (string.IsNullOrEmpty(sourceAppId) || !char.IsLetterOrDigit(sourceAppId[0]))
        {
            sourceAppId = "App";
        }

        return $"{sourceAppId}{suffix}";
    }

    private void SetLoadError(Exception ex)
    {
        var message = FormatLocalized(
            "AppCatalog_LoadError",
            "Failed to load applications: {0}",
            ex.Message);

        HasLoadError = true;
        LoadErrorMessage = message;
        StatusMessage = message;
    }

    /// <summary>
    /// Handles undo service state changes.
    /// </summary>
    private void OnUndoStateChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CanUndo = _undoService.CanUndo;
            CanRedo = _undoService.CanRedo;
            NextUndoDescription = _undoService.NextUndoDescription;
            NextRedoDescription = _undoService.NextRedoDescription;
        });
    }

    /// <summary>
    /// Saves an application (called from editor dialog) with undo support.
    /// </summary>
    /// <param name="application">The application to save.</param>
    /// <param name="isNew">Whether this is a new application.</param>
    /// <param name="originalApplication">The original application before editing (for undo).</param>
    public async Task<bool> SaveApplicationAsync(EditableApplicationModel application, bool isNew, EditableApplicationModel? originalApplication = null)
    {
        var result = await _databaseService.SaveApplicationAsync(application, isNew);
        if (result.Success)
        {
            StatusMessage = isNew ? Loc.AppCatalog_Added : Loc.AppCatalog_Updated;

            // Record undoable action
            if (isNew)
            {
                // For new apps: undo = delete, redo = add back
                var appClone = application.Clone();
                var description = string.Format(Loc.Undo_AddApplication, application.Name);

                _undoService.RecordAction(new UndoableAction
                {
                    Description = description,
                    DescriptionKey = "Undo_AddApplication",
                    Category = "Application",
                    UndoAction = async () =>
                    {
                        await _databaseService.DeleteApplicationAsync(appClone.AppId);
                    },
                    DoAction = async () =>
                    {
                        await _databaseService.SaveApplicationAsync(appClone, true);
                    }
                });
            }
            else if (originalApplication != null)
            {
                // For edits: undo = restore original, redo = apply edit
                var originalClone = originalApplication.Clone();
                var editedClone = application.Clone();
                var description = string.Format(Loc.Undo_EditApplication, application.Name);

                _undoService.RecordAction(new UndoableAction
                {
                    Description = description,
                    DescriptionKey = "Undo_EditApplication",
                    Category = "Application",
                    UndoAction = async () =>
                    {
                        await _databaseService.SaveApplicationAsync(originalClone, false);
                    },
                    DoAction = async () =>
                    {
                        await _databaseService.SaveApplicationAsync(editedClone, false);
                    }
                });
            }
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? Loc.Apps_SaveFailed;
        }
        return result.Success;
    }

    /// <summary>
    /// Deletes an application with undo support.
    /// </summary>
    public async Task<bool> DeleteApplicationWithUndoAsync(EditableApplicationModel application)
    {
        var appClone = application.Clone();
        var success = await _databaseService.DeleteApplicationAsync(application.AppId);

        if (success)
        {
            var description = string.Format(Loc.Undo_DeleteApplication, application.Name);

            _undoService.RecordAction(new UndoableAction
            {
                Description = description,
                DescriptionKey = "Undo_DeleteApplication",
                Category = "Application",
                UndoAction = async () =>
                {
                    await _databaseService.SaveApplicationAsync(appClone, true);
                },
                DoAction = async () =>
                {
                    await _databaseService.DeleteApplicationAsync(appClone.AppId);
                }
            });
        }

        return success;
    }

    /// <summary>
    /// Imports applications from a file.
    /// </summary>
    /// <param name="filePath">Path to the import file.</param>
    /// <param name="mode">Import mode.</param>
    public async Task<ApplicationImportResult> ImportApplicationsAsync(string filePath, ImportMode mode)
    {
        var result = await _databaseService.ImportApplicationsAsync(filePath, mode);
        if (result.Success)
        {
            StatusMessage = string.Format(Loc.AppCatalog_ImportSuccess, result.AddedCount, result.UpdatedCount);
        }
        else
        {
            StatusMessage = string.Format(Loc.AppCatalog_ImportError, string.Join(", ", result.Errors));
        }
        return result;
    }

    /// <summary>
    /// Exports applications to a file.
    /// </summary>
    /// <param name="appIds">Application IDs to export.</param>
    /// <param name="filePath">Destination file path.</param>
    public async Task<bool> ExportApplicationsAsync(IEnumerable<string> appIds, string filePath)
    {
        var success = await _databaseService.ExportApplicationsAsync(appIds, filePath);
        if (success)
        {
            StatusMessage = string.Format(Loc.AppCatalog_ExportSuccess, appIds.Count());
        }
        else
        {
            StatusMessage = Loc.AppCatalog_ExportError;
        }
        return success;
    }

    /// <summary>
    /// Gets the IDs of selected applications.
    /// </summary>
    private List<string> GetSelectedApplicationIds()
    {
        // For now, return single selection; can be extended for multi-select
        if (SelectedApplication != null)
        {
            return new List<string> { SelectedApplication.AppId };
        }
        return new List<string>();
    }

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Releases all resources used by the AppCatalogViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from service events
            _databaseService.DatabaseChanged -= OnDatabaseChanged;
            _undoService.StateChanged -= OnUndoStateChanged;
        }

        _disposed = true;
    }

    #endregion
}
