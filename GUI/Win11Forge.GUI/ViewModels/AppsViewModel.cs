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
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Resources;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// Status filter options for the application list.
/// </summary>
public enum StatusFilterOption
{
    All,
    Installed,
    NotInstalled,
    Selected,
    Favorites,
    HasUpdates
}

/// <summary>
/// ViewModel for the Application Manager view.
/// Displays all applications with search and category filtering.
/// </summary>
public partial class AppsViewModel : ViewModelBase
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly ProgressEstimator _progressEstimator = new();
    private SemaphoreSlim _scanSemaphore;
    private SemaphoreSlim _installSemaphore;
    private List<ApplicationModel> _allApplications = [];
    private CancellationTokenSource? _scanCancellationTokenSource;
    private CancellationTokenSource? _batchCancellationTokenSource;
    private readonly ManualResetEventSlim _pauseEvent = new(true);

    /// <summary>
    /// Filtered applications displayed in the view.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ApplicationModel> _filteredApplications = [];

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    /// <summary>
    /// Currently selected category filter.
    /// </summary>
    [ObservableProperty]
    private string _selectedCategory = string.Empty;

    /// <summary>
    /// Search text for filtering by name.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Available status filter options.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<StatusFilterOption> _statusFilters = [];

    /// <summary>
    /// Currently selected status filter.
    /// </summary>
    [ObservableProperty]
    private StatusFilterOption _selectedStatusFilter = StatusFilterOption.All;

    /// <summary>
    /// Total number of applications in database.
    /// </summary>
    [ObservableProperty]
    private int _totalApplicationsCount;

    /// <summary>
    /// Application names for search auto-complete.
    /// </summary>
    public IEnumerable<string> ApplicationNames => _allApplications.Select(a => a.Name);

    /// <summary>
    /// Number of applications after filtering.
    /// </summary>
    [ObservableProperty]
    private int _filteredCount;

    /// <summary>
    /// Whether a scan is currently in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ScanUpdatesCommand))]
    private bool _isScanning;

    /// <summary>
    /// Number of applications scanned so far.
    /// </summary>
    [ObservableProperty]
    private int _scannedCount;

    /// <summary>
    /// Number of installed applications found.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanUpdatesCommand))]
    private int _installedCount;

    /// <summary>
    /// Number of applications with updates available.
    /// </summary>
    [ObservableProperty]
    private int _updatesAvailableCount;

    /// <summary>
    /// Whether an update scan is currently in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanUpdatesCommand))]
    private bool _isScanningUpdates;

    /// <summary>
    /// Number of selected applications for batch installation.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
    private int _selectedCount;

    /// <summary>
    /// Whether a batch installation is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
    private bool _isInstalling;

    /// <summary>
    /// Whether the log viewer dialog is open.
    /// </summary>
    [ObservableProperty]
    private bool _isLogViewerOpen;

    /// <summary>
    /// The application whose logs are being viewed.
    /// </summary>
    [ObservableProperty]
    private ApplicationModel? _logViewerApplication;

    /// <summary>
    /// Whether a batch uninstallation is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
    private bool _isUninstalling;

    /// <summary>
    /// Current progress in batch operation (number of apps processed).
    /// </summary>
    [ObservableProperty]
    private int _batchProgressCurrent;

    /// <summary>
    /// Total number of apps in batch operation.
    /// </summary>
    [ObservableProperty]
    private int _batchProgressTotal;

    /// <summary>
    /// Batch progress percentage (0-100).
    /// </summary>
    [ObservableProperty]
    private double _batchProgressPercent;

    /// <summary>
    /// Whether batch operation is paused.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    private bool _isPaused;

    /// <summary>
    /// Number of favorited applications.
    /// </summary>
    [ObservableProperty]
    private int _favoritesCount;

    /// <summary>
    /// Whether summary dialog is open.
    /// </summary>
    [ObservableProperty]
    private bool _isSummaryDialogOpen;

    /// <summary>
    /// Result of the last deployment operation.
    /// </summary>
    [ObservableProperty]
    private DeploymentResult _lastDeploymentResult;

    /// <summary>
    /// Number of successful operations in last batch.
    /// </summary>
    [ObservableProperty]
    private int _successCount;

    /// <summary>
    /// Number of failed operations in last batch.
    /// </summary>
    [ObservableProperty]
    private int _failedCount;

    /// <summary>
    /// Number of skipped operations in last batch.
    /// </summary>
    [ObservableProperty]
    private int _skippedCount;

    /// <summary>
    /// Estimated time remaining for the current batch operation.
    /// </summary>
    [ObservableProperty]
    private string _estimatedTimeRemaining = string.Empty;

    /// <summary>
    /// Initializes a new instance of AppsViewModel.
    /// </summary>
    public AppsViewModel(IPowerShellBridge powerShellBridge, IAppSettingsService settingsService)
    {
        _powerShellBridge = powerShellBridge;
        _settingsService = settingsService;

        // Initialize semaphores with configured settings
        var settings = _settingsService.LoadSettings();
        var maxParallelInstalls = Math.Clamp(settings.MaxParallelInstalls, 1, 10);
        var maxParallelScans = Math.Clamp(settings.MaxParallelScans, 1, 20);
        _installSemaphore = new SemaphoreSlim(maxParallelInstalls);
        _scanSemaphore = new SemaphoreSlim(maxParallelScans);
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _allApplications = await _powerShellBridge.GetAllApplicationsAsync();
            TotalApplicationsCount = _allApplications.Count;

            // Build categories list
            var categoryList = _allApplications
                .Select(a => a.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Insert "All Categories" at the beginning
            Categories = new ObservableCollection<string>(
                new[] { Resources.Resources.Apps_CategoryAll }.Concat(categoryList));

            // Select "All Categories" by default
            SelectedCategory = Categories[0];

            // Initialize status filters
            StatusFilters = new ObservableCollection<StatusFilterOption>(
                Enum.GetValues<StatusFilterOption>());
            SelectedStatusFilter = StatusFilterOption.All;

            // Apply initial filter
            ApplyFilter();

            // Notify that Scan command can now execute (applications loaded)
            ScanCommand.NotifyCanExecuteChanged();
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
    /// Called when SearchText changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Called when SelectedCategory changes.
    /// </summary>
    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Called when SelectedStatusFilter changes.
    /// </summary>
    partial void OnSelectedStatusFilterChanged(StatusFilterOption value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Applies search, category, and status filters to the application list.
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = _allApplications.AsEnumerable();

        // Filter by search text (case-insensitive)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(a =>
                a.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                a.AppId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by category (if not "All Categories")
        if (!string.IsNullOrEmpty(SelectedCategory) &&
            SelectedCategory != Resources.Resources.Apps_CategoryAll)
        {
            filtered = filtered.Where(a =>
                a.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by installation status
        filtered = SelectedStatusFilter switch
        {
            StatusFilterOption.Installed => filtered.Where(a =>
                a.Status == ApplicationStatus.Installed ||
                a.Status == ApplicationStatus.AlreadyInstalled),
            StatusFilterOption.NotInstalled => filtered.Where(a =>
                a.Status != ApplicationStatus.Installed &&
                a.Status != ApplicationStatus.AlreadyInstalled),
            StatusFilterOption.Selected => filtered.Where(a => a.IsSelected),
            StatusFilterOption.Favorites => filtered.Where(a => a.IsFavorite),
            StatusFilterOption.HasUpdates => filtered.Where(a =>
                a.Status == ApplicationStatus.UpdateAvailable),
            _ => filtered // All
        };

        var filteredList = filtered.ToList();
        FilteredApplications = new ObservableCollection<ApplicationModel>(filteredList);
        FilteredCount = filteredList.Count;

        // Update selected count
        UpdateSelectedCount();
    }

    /// <summary>
    /// Updates the count of selected applications.
    /// </summary>
    public void UpdateSelectedCount()
    {
        SelectedCount = _allApplications.Count(a => a.IsSelected);
        FavoritesCount = _allApplications.Count(a => a.IsFavorite);
    }

    /// <summary>
    /// Refreshes the application list from the database.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Clear cache to force reload
        await InitializeAsync();
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = StatusFilterOption.All;
        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
    }

    /// <summary>
    /// Whether scan can be executed.
    /// </summary>
    private bool CanScan => !IsScanning && _allApplications.Count > 0;

    // Thread-safe counters for parallel scanning
    private int _scanInstalledCounter;
    private int _scanUpdatesCounter;

    /// <summary>
    /// Scans all applications to check their installation status and available updates.
    /// Uses SemaphoreSlim to limit concurrency to 12 parallel checks.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_allApplications.Count == 0) return;

        IsScanning = true;
        ScannedCount = 0;
        InstalledCount = 0;
        UpdatesAvailableCount = 0;
        _scanInstalledCounter = 0;
        _scanUpdatesCounter = 0;
        _scanCancellationTokenSource = new CancellationTokenSource();

        // Reset all statuses to Checking
        foreach (var app in _allApplications)
        {
            app.Status = ApplicationStatus.Pending;
            app.StatusMessage = Resources.Resources.Status_Checking;
            app.CurrentVersion = string.Empty;
            app.AvailableVersion = string.Empty;
        }

        try
        {
            var tasks = _allApplications.Select(app => ScanApplicationAsync(
                app,
                _scanCancellationTokenSource.Token));

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Scan was cancelled
        }
        finally
        {
            IsScanning = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;

            // Set the property values on UI thread to trigger notifications
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                InstalledCount = _scanInstalledCounter;
                UpdatesAvailableCount = _scanUpdatesCounter;
                CommandManager.InvalidateRequerySuggested();
            });
        }
    }

    /// <summary>
    /// Scans a single application with semaphore-controlled concurrency.
    /// Checks both installation status and available updates in one pass.
    /// </summary>
    private async Task ScanApplicationAsync(ApplicationModel app, CancellationToken cancellationToken)
    {
        await _scanSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            var status = await _powerShellBridge.GetApplicationStatusAsync(app.AppId);

            if (status == ApplicationStatus.Installed || status == ApplicationStatus.AlreadyInstalled)
            {
                // Set status first so CheckApplicationUpdateAsync can check it
                app.Status = status;
                Interlocked.Increment(ref _scanInstalledCounter);

                // Check for updates on installed apps
                var updateResult = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                if (updateResult.HasUpdate)
                {
                    app.Status = ApplicationStatus.UpdateAvailable;
                    app.CurrentVersion = updateResult.CurrentVersion;
                    app.AvailableVersion = updateResult.AvailableVersion;
                    app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
                    Interlocked.Increment(ref _scanUpdatesCounter);
                }
                else
                {
                    app.CurrentVersion = updateResult.CurrentVersion;
                    app.StatusMessage = Resources.Resources.Status_Installed;
                }
            }
            else
            {
                app.Status = status;
                app.StatusMessage = Resources.Resources.Status_Missing;
            }

            ScannedCount++;
        }
        finally
        {
            _scanSemaphore.Release();
        }
    }

    /// <summary>
    /// Installs a single application.
    /// </summary>
    [RelayCommand]
    private async Task InstallAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        await _installSemaphore.WaitAsync();

        try
        {
            app.Status = ApplicationStatus.Installing;
            app.StatusMessage = Resources.Resources.Status_Installing;

            var result = await _powerShellBridge.InstallApplicationAsync(
                app,
                isDryRun: false,
                forceUpdate: false,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = result.AlreadyInstalled
                    ? ApplicationStatus.AlreadyInstalled
                    : ApplicationStatus.Installed;
                app.StatusMessage = result.AlreadyInstalled
                    ? Resources.Resources.Status_AlreadyInstalled
                    : Resources.Resources.Status_Installed;

                if (!result.AlreadyInstalled)
                {
                    InstalledCount++;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _installSemaphore.Release();
        }
    }

    /// <summary>
    /// Uninstalls a single application.
    /// </summary>
    [RelayCommand]
    private async Task UninstallAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        await _installSemaphore.WaitAsync();

        try
        {
            app.Status = ApplicationStatus.Uninstalling;
            app.StatusMessage = Resources.Resources.Status_Uninstalling;

            var result = await _powerShellBridge.UninstallApplicationAsync(
                app,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = ApplicationStatus.Uninstalled;
                app.StatusMessage = Resources.Resources.Status_Uninstalled;

                if (InstalledCount > 0)
                {
                    InstalledCount--;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _installSemaphore.Release();
        }
    }

    /// <summary>
    /// Cancels the current scan operation.
    /// </summary>
    [RelayCommand]
    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Whether the ScanUpdates command can execute.
    /// Requires installed apps and no active scanning.
    /// </summary>
    private bool CanScanUpdates => !IsScanningUpdates && !IsScanning && InstalledCount > 0;

    /// <summary>
    /// Re-scans installed applications for available updates only.
    /// Faster than full scan as it skips installation detection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScanUpdates))]
    private async Task ScanUpdatesAsync()
    {
        IsScanningUpdates = true;
        var localUpdateCount = 0;

        try
        {
            // Get installed apps only (includes UpdateAvailable which are also installed)
            var installedApps = _allApplications
                .Where(a => a.Status == ApplicationStatus.Installed ||
                           a.Status == ApplicationStatus.AlreadyInstalled ||
                           a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();

            if (installedApps.Count == 0) return;

            // Check updates in parallel with semaphore
            var tasks = installedApps.Select(async app =>
            {
                await _scanSemaphore.WaitAsync();
                try
                {
                    app.StatusMessage = Resources.Resources.Common_Loading;
                    var result = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                    if (result.HasUpdate)
                    {
                        app.Status = ApplicationStatus.UpdateAvailable;
                        app.CurrentVersion = result.CurrentVersion;
                        app.AvailableVersion = result.AvailableVersion;
                        app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
                        Interlocked.Increment(ref localUpdateCount);
                    }
                    else
                    {
                        // No update - mark as installed
                        app.Status = ApplicationStatus.Installed;
                        app.CurrentVersion = result.CurrentVersion;
                        app.AvailableVersion = string.Empty;
                        app.StatusMessage = Resources.Resources.Status_Installed;
                    }
                }
                finally
                {
                    _scanSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Update count on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdatesAvailableCount = localUpdateCount;
            });

            // Apply filter to refresh view
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsScanningUpdates = false;
        }
    }

    /// <summary>
    /// Updates a single application.
    /// </summary>
    [RelayCommand]
    private async Task UpdateAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        await _installSemaphore.WaitAsync();

        try
        {
            app.Status = ApplicationStatus.Updating;
            app.StatusMessage = Resources.Resources.Status_Updating;

            var result = await _powerShellBridge.UpdateApplicationAsync(
                app,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                // Refresh version info after successful update
                var updateCheck = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                // Only update version if we got a valid one, otherwise keep the previous AvailableVersion as current
                if (!string.IsNullOrEmpty(updateCheck.CurrentVersion))
                {
                    app.CurrentVersion = updateCheck.CurrentVersion;
                }
                else if (!string.IsNullOrEmpty(app.AvailableVersion))
                {
                    // Use the AvailableVersion we were updating to as the new CurrentVersion
                    app.CurrentVersion = app.AvailableVersion;
                }

                app.AvailableVersion = string.Empty;
                app.Status = ApplicationStatus.Installed;
                app.StatusMessage = Resources.Resources.Status_Installed;

                if (UpdatesAvailableCount > 0)
                {
                    UpdatesAvailableCount--;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _installSemaphore.Release();
        }
    }

    /// <summary>
    /// Whether the UpdateSelected command can execute.
    /// </summary>
    private bool CanUpdateSelected => UpdatesAvailableCount > 0 && !IsInstalling && !IsUninstalling;

    /// <summary>
    /// Updates all applications with available updates.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
    private async Task UpdateSelectedAsync()
    {
        var appsWithUpdates = _allApplications
            .Where(a => a.Status == ApplicationStatus.UpdateAvailable && a.IsSelected)
            .ToList();

        if (appsWithUpdates.Count == 0)
        {
            // If none selected, update all with updates
            appsWithUpdates = _allApplications
                .Where(a => a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();
        }

        if (appsWithUpdates.Count == 0) return;

        IsInstalling = true;

        try
        {
            foreach (var app in appsWithUpdates)
            {
                await UpdateAppAsync(app);
            }
        }
        finally
        {
            IsInstalling = false;
            ApplyFilter();
        }
    }

    /// <summary>
    /// Selects all applications with updates available.
    /// </summary>
    [RelayCommand]
    private void SelectWithUpdates()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = app.Status == ApplicationStatus.UpdateAvailable;
        }

        SelectedCount = _allApplications.Count(a => a.IsSelected);
    }

    /// <summary>
    /// Whether the InstallSelected command can execute.
    /// </summary>
    private bool CanInstallSelected => SelectedCount > 0 && !IsInstalling && !IsUninstalling;

    /// <summary>
    /// Installs all selected applications with ForceUpdate enabled.
    /// Uses parallel execution with semaphore-controlled concurrency.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallSelected))]
    private async Task InstallSelectedAsync()
    {
        var selectedApps = _allApplications.Where(a => a.IsSelected).ToList();
        if (selectedApps.Count == 0) return;

        IsInstalling = true;
        IsPaused = false;
        _pauseEvent.Set();
        _batchCancellationTokenSource = new CancellationTokenSource();

        BatchProgressCurrent = 0;
        BatchProgressTotal = selectedApps.Count;
        BatchProgressPercent = 0;
        SuccessCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        EstimatedTimeRemaining = Resources.Resources.Progress_Calculating;

        // Start progress estimator
        _progressEstimator.Start(selectedApps.Count);

        try
        {
            // Create tasks for all apps to run in parallel (limited by semaphore)
            var tasks = selectedApps.Select(app => InstallSingleAppAsync(
                app, _batchCancellationTokenSource.Token));

            await Task.WhenAll(tasks);

            // Determine final result
            if (_batchCancellationTokenSource.Token.IsCancellationRequested)
            {
                LastDeploymentResult = DeploymentResult.Cancelled;
            }
            else if (FailedCount == 0)
            {
                LastDeploymentResult = DeploymentResult.Success;
            }
            else if (SuccessCount > 0)
            {
                LastDeploymentResult = DeploymentResult.PartialSuccess;
            }
            else
            {
                LastDeploymentResult = DeploymentResult.Failed;
            }

            IsSummaryDialogOpen = true;
        }
        finally
        {
            IsInstalling = false;
            IsPaused = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Installs a single application with semaphore-controlled concurrency.
    /// </summary>
    private async Task InstallSingleAppAsync(ApplicationModel app, CancellationToken cancellationToken)
    {
        // Check for cancellation before acquiring semaphore
        if (cancellationToken.IsCancellationRequested)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
            return;
        }

        // Wait if paused
        try
        {
            _pauseEvent.Wait(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
            return;
        }

        await _installSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                app.Status = ApplicationStatus.Skipped;
                app.StatusMessage = Resources.Resources.Status_Skipped;
                Interlocked.Increment(ref _skippedCount);
                OnPropertyChanged(nameof(SkippedCount));
                return;
            }

            app.Status = ApplicationStatus.Installing;
            app.StatusMessage = Resources.Resources.Status_Installing;

            var result = await _powerShellBridge.InstallApplicationAsync(
                app,
                isDryRun: false,
                forceUpdate: true,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = result.AlreadyInstalled
                    ? ApplicationStatus.AlreadyInstalled
                    : ApplicationStatus.Installed;
                app.StatusMessage = result.AlreadyInstalled
                    ? Resources.Resources.Status_AlreadyInstalled
                    : Resources.Resources.Status_Installed;

                Interlocked.Increment(ref _successCount);
                OnPropertyChanged(nameof(SuccessCount));

                if (!result.AlreadyInstalled)
                {
                    Interlocked.Increment(ref _installedCount);
                    OnPropertyChanged(nameof(InstalledCount));
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
                Interlocked.Increment(ref _failedCount);
                OnPropertyChanged(nameof(FailedCount));
            }
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            Interlocked.Increment(ref _failedCount);
            OnPropertyChanged(nameof(FailedCount));
        }
        finally
        {
            _installSemaphore.Release();
            Interlocked.Increment(ref _batchProgressCurrent);
            BatchProgressPercent = (double)_batchProgressCurrent / BatchProgressTotal * 100;
            OnPropertyChanged(nameof(BatchProgressCurrent));

            // Update time estimate
            _progressEstimator.UpdateProgress(_batchProgressCurrent);
            EstimatedTimeRemaining = _progressEstimator.GetFormattedTimeRemaining();
        }
    }

    /// <summary>
    /// Selects all visible (filtered) applications.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var app in FilteredApplications)
        {
            app.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Deselects all applications.
    /// </summary>
    [RelayCommand]
    private void SelectNone()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Selects all applications that are not installed.
    /// </summary>
    [RelayCommand]
    private void SelectNotInstalled()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = app.Status != ApplicationStatus.Installed &&
                             app.Status != ApplicationStatus.AlreadyInstalled;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Toggles the selection state of a specific application.
    /// </summary>
    [RelayCommand]
    private void ToggleSelection(ApplicationModel? app)
    {
        if (app == null) return;
        app.IsSelected = !app.IsSelected;
        UpdateSelectedCount();
    }

    /// <summary>
    /// Opens the log viewer for a specific application.
    /// </summary>
    [RelayCommand]
    private void ViewLogs(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.LogOutput)) return;
        LogViewerApplication = app;
        IsLogViewerOpen = true;
    }

    /// <summary>
    /// Closes the log viewer dialog.
    /// </summary>
    [RelayCommand]
    private void CloseLogViewer()
    {
        IsLogViewerOpen = false;
        LogViewerApplication = null;
    }

    /// <summary>
    /// Opens the official website for manual installation.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.OfficialUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app.OfficialUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if browser can't be opened
        }
    }

    /// <summary>
    /// Launches an installed application.
    /// </summary>
    [RelayCommand]
    private async Task LaunchAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        // Only launch installed apps
        if (app.Status != ApplicationStatus.Installed &&
            app.Status != ApplicationStatus.AlreadyInstalled &&
            app.Status != ApplicationStatus.UpdateAvailable)
        {
            return;
        }

        try
        {
            // Try to find and launch the application
            var launched = await _powerShellBridge.LaunchApplicationAsync(app);

            if (!launched)
            {
                // Fallback: try to open by name using shell
                ErrorMessage = string.Format(
                    Resources.Resources.Error_LaunchFailed,
                    app.Name);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Copies the application ID to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyAppId(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.AppId)) return;

        try
        {
            System.Windows.Clipboard.SetText(app.AppId);
        }
        catch
        {
            // Silently fail if clipboard is unavailable
        }
    }

    /// <summary>
    /// Toggles the favorite status of an application.
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite(ApplicationModel? app)
    {
        if (app == null) return;
        app.IsFavorite = !app.IsFavorite;
        FavoritesCount = _allApplications.Count(a => a.IsFavorite);

        // Refresh filter if currently viewing favorites
        if (SelectedStatusFilter == StatusFilterOption.Favorites)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// Whether the UninstallSelected command can execute.
    /// </summary>
    private bool CanUninstallSelected => SelectedCount > 0 && !IsUninstalling && !IsInstalling;

    /// <summary>
    /// Uninstalls all selected applications.
    /// Uses parallel execution with semaphore-controlled concurrency.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
    private async Task UninstallSelectedAsync()
    {
        var selectedApps = _allApplications
            .Where(a => a.IsSelected &&
                (a.Status == ApplicationStatus.Installed ||
                 a.Status == ApplicationStatus.AlreadyInstalled))
            .ToList();

        if (selectedApps.Count == 0) return;

        // Show confirmation dialog
        var confirmMessage = string.Format(Resources.Resources.Confirm_Uninstall_Message, selectedApps.Count);
        var result = System.Windows.MessageBox.Show(
            confirmMessage,
            Resources.Resources.Confirm_Uninstall_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsUninstalling = true;
        IsPaused = false;
        _pauseEvent.Set();
        _batchCancellationTokenSource = new CancellationTokenSource();

        BatchProgressCurrent = 0;
        BatchProgressTotal = selectedApps.Count;
        BatchProgressPercent = 0;
        SuccessCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        EstimatedTimeRemaining = Resources.Resources.Progress_Calculating;

        // Start progress estimator
        _progressEstimator.Start(selectedApps.Count);

        try
        {
            // Create tasks for all apps to run in parallel (limited by semaphore)
            var tasks = selectedApps.Select(app => UninstallSingleAppAsync(
                app, _batchCancellationTokenSource.Token));

            await Task.WhenAll(tasks);

            // Determine final result
            if (_batchCancellationTokenSource.Token.IsCancellationRequested)
            {
                LastDeploymentResult = DeploymentResult.Cancelled;
            }
            else if (FailedCount == 0)
            {
                LastDeploymentResult = DeploymentResult.Success;
            }
            else if (SuccessCount > 0)
            {
                LastDeploymentResult = DeploymentResult.PartialSuccess;
            }
            else
            {
                LastDeploymentResult = DeploymentResult.Failed;
            }

            IsSummaryDialogOpen = true;
        }
        finally
        {
            IsUninstalling = false;
            IsPaused = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Uninstalls a single application with semaphore-controlled concurrency.
    /// </summary>
    private async Task UninstallSingleAppAsync(ApplicationModel app, CancellationToken cancellationToken)
    {
        // Check for cancellation before acquiring semaphore
        if (cancellationToken.IsCancellationRequested)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
            return;
        }

        // Wait if paused
        try
        {
            _pauseEvent.Wait(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
            return;
        }

        await _installSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                app.Status = ApplicationStatus.Skipped;
                app.StatusMessage = Resources.Resources.Status_Skipped;
                Interlocked.Increment(ref _skippedCount);
                OnPropertyChanged(nameof(SkippedCount));
                return;
            }

            app.Status = ApplicationStatus.Uninstalling;
            app.StatusMessage = Resources.Resources.Status_Uninstalling;

            var result = await _powerShellBridge.UninstallApplicationAsync(
                app,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = ApplicationStatus.Uninstalled;
                app.StatusMessage = Resources.Resources.Status_Uninstalled;
                Interlocked.Increment(ref _successCount);
                OnPropertyChanged(nameof(SuccessCount));

                if (_installedCount > 0)
                {
                    Interlocked.Decrement(ref _installedCount);
                    OnPropertyChanged(nameof(InstalledCount));
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
                Interlocked.Increment(ref _failedCount);
                OnPropertyChanged(nameof(FailedCount));
            }
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            Interlocked.Increment(ref _skippedCount);
            OnPropertyChanged(nameof(SkippedCount));
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            Interlocked.Increment(ref _failedCount);
            OnPropertyChanged(nameof(FailedCount));
        }
        finally
        {
            _installSemaphore.Release();
            Interlocked.Increment(ref _batchProgressCurrent);
            BatchProgressPercent = (double)_batchProgressCurrent / BatchProgressTotal * 100;
            OnPropertyChanged(nameof(BatchProgressCurrent));

            // Update time estimate
            _progressEstimator.UpdateProgress(_batchProgressCurrent);
            EstimatedTimeRemaining = _progressEstimator.GetFormattedTimeRemaining();
        }
    }

    /// <summary>
    /// Whether the Pause command can execute.
    /// </summary>
    private bool CanPause => (IsInstalling || IsUninstalling) && !IsPaused;

    /// <summary>
    /// Pauses the current batch operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = true;
        _pauseEvent.Reset();
    }

    /// <summary>
    /// Whether the Resume command can execute.
    /// </summary>
    private bool CanResume => (IsInstalling || IsUninstalling) && IsPaused;

    /// <summary>
    /// Resumes the current batch operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        IsPaused = false;
        _pauseEvent.Set();
    }

    /// <summary>
    /// Cancels the current batch operation.
    /// </summary>
    [RelayCommand]
    private void CancelBatch()
    {
        _batchCancellationTokenSource?.Cancel();
        // Resume if paused to allow cancellation to propagate
        if (IsPaused)
        {
            IsPaused = false;
            _pauseEvent.Set();
        }
    }

    /// <summary>
    /// Closes the summary dialog.
    /// </summary>
    [RelayCommand]
    private void CloseSummaryDialog()
    {
        IsSummaryDialogOpen = false;
    }

    /// <summary>
    /// Exports the current selection to a JSON file.
    /// </summary>
    [RelayCommand]
    private void ExportSelection()
    {
        var selectedIds = _allApplications
            .Where(a => a.IsSelected)
            .Select(a => a.AppId)
            .ToList();

        if (selectedIds.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "win11forge-selection"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(selectedIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(dialog.FileName, json);
            }
            catch
            {
                // Silently fail
            }
        }
    }

    /// <summary>
    /// Imports a selection from a JSON file.
    /// </summary>
    [RelayCommand]
    private void ImportSelection()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var selectedIds = JsonSerializer.Deserialize<List<string>>(json);

                if (selectedIds != null)
                {
                    // Clear current selection
                    foreach (var app in _allApplications)
                    {
                        app.IsSelected = false;
                    }

                    // Apply imported selection
                    foreach (var appId in selectedIds)
                    {
                        var app = _allApplications.FirstOrDefault(a =>
                            a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (app != null)
                        {
                            app.IsSelected = true;
                        }
                    }

                    UpdateSelectedCount();
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }

    /// <summary>
    /// Exports favorites to a JSON file.
    /// </summary>
    [RelayCommand]
    private void ExportFavorites()
    {
        var favoriteIds = _allApplications
            .Where(a => a.IsFavorite)
            .Select(a => a.AppId)
            .ToList();

        if (favoriteIds.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "win11forge-favorites"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(favoriteIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(dialog.FileName, json);
            }
            catch
            {
                // Silently fail
            }
        }
    }

    /// <summary>
    /// Imports favorites from a JSON file.
    /// </summary>
    [RelayCommand]
    private void ImportFavorites()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(json);

                if (favoriteIds != null)
                {
                    // Clear current favorites
                    foreach (var app in _allApplications)
                    {
                        app.IsFavorite = false;
                    }

                    // Apply imported favorites
                    foreach (var appId in favoriteIds)
                    {
                        var app = _allApplications.FirstOrDefault(a =>
                            a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (app != null)
                        {
                            app.IsFavorite = true;
                        }
                    }

                    FavoritesCount = _allApplications.Count(a => a.IsFavorite);

                    // Refresh filter if viewing favorites
                    if (SelectedStatusFilter == StatusFilterOption.Favorites)
                    {
                        ApplyFilter();
                    }
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }

    /// <summary>
    /// Selects all favorite applications.
    /// </summary>
    [RelayCommand]
    private void SelectFavorites()
    {
        foreach (var app in _allApplications)
        {
            if (app.IsFavorite)
            {
                app.IsSelected = true;
            }
        }
        UpdateSelectedCount();
    }
}
