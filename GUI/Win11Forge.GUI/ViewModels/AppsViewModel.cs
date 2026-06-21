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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.Services.PowerShell;

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
/// ViewModel for the Applications view.
/// Displays all applications with search and category filtering.
/// </summary>
public partial class AppsViewModel : ViewModelBase, IDisposable
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly IDeploymentStateService _deploymentStateService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IAppScanCoordinator _scanCoordinator;
    private readonly IAppInstallationCoordinator _installationCoordinator;
    private readonly IAppUpdateCoordinator _updateCoordinator;
    private readonly IAppUninstallCoordinator _uninstallCoordinator;
    private readonly IPauseGate _pauseGate;
    private readonly IDialogService _dialogService;
    private readonly IRepositoryPathService _pathService;
    private readonly IToastService? _toastService;
    private readonly ILoggingService _logger;
    private readonly ProgressEstimator _progressEstimator = new();
    private List<ApplicationModel> _allApplications = [];
    private CancellationTokenSource? _scanCancellationTokenSource;
    private CancellationTokenSource? _batchCancellationTokenSource;
    private bool _batchProgressFinalized;
    private int _lastAppliedBatchProgressCompleted;
    private bool _disposed;

    // External callbacks for Dashboard scan integration
    private Action<int, int>? _externalProgressCallback;
    private Action<int>? _externalCompletionCallback;

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
    /// Total number of applications being scanned (filtered count when filter active).
    /// </summary>
    [ObservableProperty]
    private int _scanTotalCount;

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
    [NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
    private int _selectedCount;

    /// <summary>
    /// Whether a batch installation is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowBatchPauseResume))]
    [NotifyPropertyChangedFor(nameof(ShowPauseButton))]
    [NotifyPropertyChangedFor(nameof(ShowResumeButton))]
    private bool _isInstalling;

    /// <summary>
    /// Debug: Service hash code to verify singleton.
    /// </summary>
    public int DebugServiceHashCode => _deploymentStateService.GetHashCode();

    /// <summary>
    /// Whether the log viewer dialog is open.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
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
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowBatchPauseResume))]
    [NotifyPropertyChangedFor(nameof(ShowPauseButton))]
    [NotifyPropertyChangedFor(nameof(ShowResumeButton))]
    private bool _isUninstalling;

    /// <summary>
    /// Whether a batch update is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowBatchPauseResume))]
    [NotifyPropertyChangedFor(nameof(ShowPauseButton))]
    [NotifyPropertyChangedFor(nameof(ShowResumeButton))]
    private bool _isUpdating;

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
    /// Name of the application currently being processed in batch operation.
    /// </summary>
    [ObservableProperty]
    private string? _currentBatchAppName;

    /// <summary>
    /// Whether batch operation is paused.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyPropertyChangedFor(nameof(ShowPauseButton))]
    [NotifyPropertyChangedFor(nameof(ShowResumeButton))]
    private bool _isPaused;

    /// <summary>
    /// Whether pause/resume controls should be visible for the active batch operation.
    /// </summary>
    public bool CanShowBatchPauseResume => (IsInstalling || IsUninstalling) && !IsUpdating;

    /// <summary>
    /// Whether the pause button should be visible.
    /// </summary>
    public bool ShowPauseButton => CanShowBatchPauseResume && !IsPaused;

    /// <summary>
    /// Whether the resume button should be visible.
    /// </summary>
    public bool ShowResumeButton => CanShowBatchPauseResume && IsPaused;

    /// <summary>
    /// Number of favorited applications.
    /// </summary>
    [ObservableProperty]
    private int _favoritesCount;

    /// <summary>
    /// Whether summary dialog is open.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    private bool _isSummaryDialogOpen;

    /// <summary>
    /// Whether the save profile dialog is open.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    private bool _isSaveProfileDialogOpen;

    /// <summary>
    /// Content for the save profile dialog.
    /// </summary>
    [ObservableProperty]
    private object? _saveProfileDialogContent;

    /// <summary>
    /// Whether any dialog is currently open.
    /// </summary>
    public bool IsDialogOpen => IsLogViewerOpen || IsSummaryDialogOpen || IsSaveProfileDialogOpen;

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
    /// Last non-error workflow status message.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    private static string GetLocalizedString(string resourceKey, string fallback)
    {
        return Resources.Resources.ResourceManager.GetString(resourceKey, Resources.Resources.Culture) ?? fallback;
    }

    private static string FormatLocalized(string resourceKey, string fallbackFormat, params object[] args)
    {
        string format = GetLocalizedString(resourceKey, fallbackFormat);
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }

    public AppsViewModel(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService,
        IDeploymentStateService deploymentStateService,
        IAppScanCoordinator scanCoordinator,
        IAppInstallationCoordinator installationCoordinator,
        IAppUpdateCoordinator updateCoordinator,
        IAppUninstallCoordinator uninstallCoordinator,
        IPauseGate pauseGate,
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null,
        IToastService? toastService = null,
        IRepositoryPathService? pathService = null,
        ILoggerFactory? loggerFactory = null)
    {
        _powerShellBridge = powerShellBridge;
        _settingsService = settingsService;
        _deploymentStateService = deploymentStateService;
        _scanCoordinator = scanCoordinator;
        _installationCoordinator = installationCoordinator;
        _updateCoordinator = updateCoordinator;
        _uninstallCoordinator = uninstallCoordinator;
        _pauseGate = pauseGate;
        _dialogService = dialogService ?? new DialogService();
        _fileDialogService = fileDialogService ?? new FileDialogService();
        _toastService = toastService;
        _pathService = pathService ?? new RepositoryPathService();
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<AppsViewModel>();

        // Subscribe to pause/resume/cancel requests from the monitoring view
        _deploymentStateService.PauseRequested += OnPauseRequested;
        _deploymentStateService.ResumeRequested += OnResumeRequested;
        _deploymentStateService.CancelRequested += OnCancelRequested;

        // Restore persisted view state from settings
        AppSettings settings = _settingsService.LoadSettings();

        // Restore persisted filter state from settings
        _searchText = settings.AppsLastSearchText ?? string.Empty;
        _selectedStatusFilter = (StatusFilterOption)Math.Clamp(settings.AppsLastStatusFilter, 0, 5);
        _showFavoritesColumn = settings.AppsShowFavoritesColumn;
        _showVersionColumn = settings.AppsShowVersionColumn;
        _showStatusColumn = settings.AppsShowStatusColumn;
        _showCategoryColumn = settings.AppsShowCategoryColumn;
        _showSourcesColumn = settings.AppsShowSourcesColumn;
        _showLogsColumn = settings.AppsShowLogsColumn;
        // Note: _selectedCategory is restored in InitializeAsync after categories are loaded

        // Register for filter messages from Dashboard
        WeakReferenceMessenger.Default.Register<ApplyFilterMessage>(this, async (r, m) =>
        {
            SelectedStatusFilter = m.Filter;

            // Trigger scan if requested (e.g., when coming from Dashboard "View Updates")
            if (m.TriggerScan && CanScan)
            {
                await ScanAsync();
            }
        });

        // Register for scan trigger messages from Dashboard
        WeakReferenceMessenger.Default.Register<TriggerScanMessage>(this, async (r, m) =>
        {
            // Store callbacks for progress reporting
            _externalProgressCallback = m.ProgressCallback;
            _externalCompletionCallback = m.CompletionCallback;

            try
            {
                // Initialize applications if not yet loaded
                if (_allApplications.Count == 0)
                {
                    await InitializeAsync();
                }

                if (CanScan)
                {
                    await ScanAsync();
                }
                else
                {
                    // Cannot scan - notify completion with 0
                    _externalCompletionCallback?.Invoke(0);
                }
            }
            finally
            {
                // Clear callbacks after scan
                _externalProgressCallback = null;
                _externalCompletionCallback = null;
            }
        });
    }

    private void OnPauseRequested(object? sender, EventArgs e)
    {
        if (CanPause)
        {
            Pause();
        }
    }

    private void OnResumeRequested(object? sender, EventArgs e)
    {
        if (CanResume)
        {
            Resume();
        }
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        RequestBatchCancellation();
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        _lastOperationType = "load";

        try
        {
            _allApplications = await _powerShellBridge.GetAllApplicationsAsync();
            TotalApplicationsCount = _allApplications.Count;

            // Sync to ObservableCollection for CollectionView
            _applicationsSource.Clear();
            foreach (ApplicationModel app in _allApplications)
            {
                _applicationsSource.Add(app);
            }

            // Build categories list
            List<string> categoryList = _allApplications
                .Select(a => a.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Insert "All Categories" at the beginning
            Categories = new ObservableCollection<string>(
                new[] { Resources.Resources.Apps_CategoryAll }.Concat(categoryList));

            // Restore persisted category filter, or default to "All Categories"
            AppSettings settings = _settingsService.LoadSettings();
            string persistedCategory = settings.AppsLastSelectedCategory;
            if (!string.IsNullOrEmpty(persistedCategory) && Categories.Contains(persistedCategory))
            {
                SelectedCategory = persistedCategory;
            }
            else
            {
                SelectedCategory = Categories[0];
            }

            // Initialize status filters (status filter is already restored in constructor)
            StatusFilters = new ObservableCollection<StatusFilterOption>(
                Enum.GetValues<StatusFilterOption>());

            // Load available profiles and pre-cache them
            List<string> profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            AvailableProfiles = new ObservableCollection<string>(profiles);
            await PreloadProfilesCacheAsync(profiles);

            // Apply initial filter
            ApplyFilter();

            // If persisted filter results in no apps visible but we have apps,
            // reset to "All" to avoid confusing empty state
            if (FilteredCount == 0 && TotalApplicationsCount > 0)
            {
                SelectedStatusFilter = StatusFilterOption.All;
                SelectedCategory = Categories[0]; // Reset to "All Categories"
                SearchText = string.Empty;
                ApplyFilter();
            }

            // Notify that Scan command can now execute (applications loaded)
            ScanCommand.NotifyCanExecuteChanged();
            _lastOperationType = string.Empty;
        }
        catch (PowerShellBridgeException ex)
        {
            ErrorMessage = FormatLocalized("Apps_Error_PowerShell", "PowerShell error: {0}", ex.Message);
            _logger.LogError("PowerShellBridgeException in InitializeAsync", ex);
        }
        catch (ApplicationDatabaseException ex)
        {
            ErrorMessage = FormatLocalized("Apps_Error_Database", "Database error: {0}", ex.Message);
            _logger.LogError("ApplicationDatabaseException in InitializeAsync", ex);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError("Unexpected exception in InitializeAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
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
    /// Updates installed and update counters based on current app states.
    /// </summary>
    private void UpdateCounters()
    {
        InstalledCount = _allApplications.Count(a =>
            a.Status == ApplicationStatus.Installed ||
            a.Status == ApplicationStatus.AlreadyInstalled ||
            a.Status == ApplicationStatus.UpdateAvailable);
        UpdatesAvailableCount = _allApplications.Count(a =>
            a.Status == ApplicationStatus.UpdateAvailable);
    }

    /// <summary>
    /// Releases all resources used by the AppsViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unregister from WeakReferenceMessenger
            WeakReferenceMessenger.Default.Unregister<ApplyFilterMessage>(this);
            WeakReferenceMessenger.Default.Unregister<TriggerScanMessage>(this);

            // Unsubscribe from events
            _deploymentStateService.PauseRequested -= OnPauseRequested;
            _deploymentStateService.ResumeRequested -= OnResumeRequested;
            _deploymentStateService.CancelRequested -= OnCancelRequested;

            // Cancel any ongoing operations
            _scanCancellationTokenSource?.Cancel();
            _scanCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource?.Cancel();
            _batchCancellationTokenSource?.Dispose();

            // Clear caches
            InvalidateCaches();
            _allApplications.Clear();
            _applicationsSource.Clear();
        }

        _disposed = true;
    }
}
