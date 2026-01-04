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
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Application Manager view.
/// Displays all applications with search and category filtering.
/// </summary>
public partial class AppsViewModel : ViewModelBase
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly SemaphoreSlim _scanSemaphore = new(8);
    private readonly SemaphoreSlim _installSemaphore = new(1);
    private List<ApplicationModel> _allApplications = [];
    private CancellationTokenSource? _scanCancellationTokenSource;

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
    /// Total number of applications in database.
    /// </summary>
    [ObservableProperty]
    private int _totalApplicationsCount;

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
    private int _installedCount;

    /// <summary>
    /// Initializes a new instance of AppsViewModel.
    /// </summary>
    public AppsViewModel(IPowerShellBridge powerShellBridge)
    {
        _powerShellBridge = powerShellBridge;
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

            // Apply initial filter
            ApplyFilter();
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
    /// Applies search and category filters to the application list.
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

        var filteredList = filtered.ToList();
        FilteredApplications = new ObservableCollection<ApplicationModel>(filteredList);
        FilteredCount = filteredList.Count;
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
        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
    }

    /// <summary>
    /// Whether scan can be executed.
    /// </summary>
    private bool CanScan => !IsScanning && _allApplications.Count > 0;

    /// <summary>
    /// Scans all applications to check their installation status.
    /// Uses SemaphoreSlim to limit concurrency to 12 parallel checks.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_allApplications.Count == 0) return;

        IsScanning = true;
        ScannedCount = 0;
        InstalledCount = 0;
        _scanCancellationTokenSource = new CancellationTokenSource();

        // Reset all statuses to Checking
        foreach (var app in _allApplications)
        {
            app.Status = ApplicationStatus.Pending;
            app.StatusMessage = Resources.Resources.Status_Checking;
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
        }
    }

    /// <summary>
    /// Scans a single application with semaphore-controlled concurrency.
    /// </summary>
    private async Task ScanApplicationAsync(ApplicationModel app, CancellationToken cancellationToken)
    {
        await _scanSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            var status = await _powerShellBridge.GetApplicationStatusAsync(app.AppId);
            app.Status = status;

            if (status == ApplicationStatus.Installed)
            {
                app.StatusMessage = Resources.Resources.Status_Installed;
                InstalledCount++;
            }
            else
            {
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
    /// Cancels the current scan operation.
    /// </summary>
    [RelayCommand]
    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
    }
}
