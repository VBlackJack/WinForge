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

using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Called when ScannedCount changes - reports progress to external callbacks.
    /// </summary>
    partial void OnScannedCountChanged(int value)
    {
        _externalProgressCallback?.Invoke(value, ScanTotalCount);
    }

    /// <summary>
    /// Whether scan can be executed.
    /// </summary>
    private bool CanScan => !IsScanning && _allApplications.Count > 0;

    // Thread-safe counters for parallel scanning
    private int _scanInstalledCounter;
    private int _scanUpdatesCounter;

    /// <summary>
    /// Scans applications to check their installation status and available updates.
    /// When filters are active, only scans filtered applications for better performance.
    /// Uses SemaphoreSlim to limit concurrency to 12 parallel checks.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_allApplications.Count == 0)
        {
            // Notify external callback that scan completed (with 0 results)
            _externalCompletionCallback?.Invoke(0);
            return;
        }

        // Determine which apps to scan: filtered apps if any filter is active, otherwise all
        var hasActiveFilter = HasSearchFilter || HasCategoryFilter || HasStatusFilter;
        var appsToScan = hasActiveFilter
            ? FilteredApplications.Cast<ApplicationModel>().ToList()
            : _allApplications;

        if (appsToScan.Count == 0)
        {
            // Notify external callback that scan completed (with current count)
            _externalCompletionCallback?.Invoke(UpdatesAvailableCount);
            return;
        }

        IsScanning = true;
        ScannedCount = 0;
        ScanTotalCount = appsToScan.Count;
        _scanInstalledCounter = 0;
        _scanUpdatesCounter = 0;
        _scanCancellationTokenSource = new CancellationTokenSource();

        // Only reset counters if scanning all apps
        if (!hasActiveFilter)
        {
            InstalledCount = 0;
            UpdatesAvailableCount = 0;
        }

        // Reset statuses only for apps being scanned
        foreach (var app in appsToScan)
        {
            app.Status = ApplicationStatus.Pending;
            app.StatusMessage = Resources.Resources.Status_Checking;
            app.CurrentVersion = string.Empty;
            app.AvailableVersion = string.Empty;
        }

        try
        {
            // Use batch detection which internally uses fast registry-first detection
            var batchResults = await _powerShellBridge.GetBatchApplicationStatusAsync(appsToScan);
            if (batchResults != null)
            {
                System.Diagnostics.Debug.WriteLine($"Batch detection succeeded: {batchResults.Count} apps checked");
            }

            if (batchResults != null)
            {
                // Batch detection succeeded - apply results and check updates
                await ScanWithBatchResultsAsync(appsToScan, batchResults, _scanCancellationTokenSource.Token);
            }
            else
            {
                // Batch detection failed - fallback to per-app detection
                var tasks = appsToScan.Select(app => ScanApplicationAsync(
                    app,
                    _scanCancellationTokenSource.Token));

                await Task.WhenAll(tasks);
            }
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

            // Capture callback before it may be cleared by the TriggerScanMessage handler's finally block
            var completionCallback = _externalCompletionCallback;

            // Set the property values on UI thread to trigger notifications
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Recount all installed apps after partial scan
                if (hasActiveFilter)
                {
                    InstalledCount = _allApplications.Count(a =>
                        a.Status == ApplicationStatus.Installed ||
                        a.Status == ApplicationStatus.AlreadyInstalled);
                    UpdatesAvailableCount = _allApplications.Count(a =>
                        !string.IsNullOrEmpty(a.AvailableVersion) &&
                        a.AvailableVersion != a.CurrentVersion);
                }
                else
                {
                    InstalledCount = _scanInstalledCounter;
                    UpdatesAvailableCount = _scanUpdatesCounter;
                }
                CommandManager.InvalidateRequerySuggested();

                // Notify external callback (Dashboard) of completion
                completionCallback?.Invoke(UpdatesAvailableCount);
            });
        }
    }

    /// <summary>
    /// Applies batch detection results and checks for updates on installed apps.
    /// </summary>
    private async Task ScanWithBatchResultsAsync(
        List<ApplicationModel> apps,
        Dictionary<string, BatchAppStatus> batchResults,
        CancellationToken cancellationToken)
    {
        // First pass: apply batch results with versions (fast)
        var installedApps = new List<ApplicationModel>();

        foreach (var app in apps)
        {
            if (cancellationToken.IsCancellationRequested) return;

            if (batchResults.TryGetValue(app.AppId, out var batchStatus))
            {
                app.Status = batchStatus.Status;
                if (batchStatus.Status == ApplicationStatus.Installed ||
                    batchStatus.Status == ApplicationStatus.AlreadyInstalled)
                {
                    app.StatusMessage = Resources.Resources.Status_Installed;
                    // Apply version from batch if available
                    if (!string.IsNullOrEmpty(batchStatus.Version))
                    {
                        app.CurrentVersion = batchStatus.Version;
                    }
                    Interlocked.Increment(ref _scanInstalledCounter);
                    installedApps.Add(app);
                }
                else
                {
                    app.StatusMessage = Resources.Resources.Status_Missing;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Pending;
                app.StatusMessage = Resources.Resources.Status_Missing;
            }
            ScannedCount++;
        }

        // Second pass: check for available updates using batch method
        if (installedApps.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            await CheckBatchUpdatesAsync(installedApps, cancellationToken);
        }
    }

    /// <summary>
    /// Checks for available updates using batch detection for better performance.
    /// Falls back to individual checks if batch method is unavailable.
    /// </summary>
    private async Task CheckBatchUpdatesAsync(List<ApplicationModel> installedApps, CancellationToken cancellationToken)
    {
        try
        {
            // Try batch update detection first (single call, ~500ms total)
            if (_powerShellBridge is PowerShellBridgeFacade facade)
            {
                var batchUpdates = await facade.GetAvailableUpdatesAsync();

                if (batchUpdates.Count > 0)
                {
                    // Build lookup dictionary for fast matching
                    var updateLookup = batchUpdates.ToDictionary(
                        u => u.Id,
                        u => u,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var app in installedApps)
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        // Try direct AppId match
                        if (updateLookup.TryGetValue(app.AppId, out var updateInfo))
                        {
                            ApplyUpdateInfo(app, updateInfo);
                            continue;
                        }

                        // Try name match
                        var matchingUpdate = batchUpdates.FirstOrDefault(u =>
                            u.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase) ||
                            u.Id.Contains(app.Name, StringComparison.OrdinalIgnoreCase) ||
                            app.Name.Contains(u.Name, StringComparison.OrdinalIgnoreCase));

                        if (matchingUpdate != null)
                        {
                            ApplyUpdateInfo(app, matchingUpdate);
                        }
                    }

                    Debug.WriteLine($"Batch update check: {batchUpdates.Count} updates found, {_scanUpdatesCounter} matched");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Batch update detection failed, falling back to individual checks: {ex.Message}");
        }

        // Fallback: check updates individually for apps without version info
        var appsNeedingIndividualCheck = installedApps
            .Where(a => string.IsNullOrEmpty(a.CurrentVersion))
            .ToList();

        if (appsNeedingIndividualCheck.Count > 0)
        {
            var updateTasks = appsNeedingIndividualCheck.Select(app => CheckAppUpdateAsync(app, cancellationToken));
            await Task.WhenAll(updateTasks);
        }
    }

    /// <summary>
    /// Applies update information to an application.
    /// </summary>
    private void ApplyUpdateInfo(ApplicationModel app, UpdateInfo updateInfo)
    {
        // Check if there's actually an update available
        var currentVersion = !string.IsNullOrEmpty(app.CurrentVersion)
            ? app.CurrentVersion
            : updateInfo.CurrentVersion;

        if (!string.IsNullOrEmpty(updateInfo.NewVersion) &&
            !string.Equals(currentVersion, updateInfo.NewVersion, StringComparison.OrdinalIgnoreCase))
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.CurrentVersion = currentVersion;
            app.AvailableVersion = updateInfo.NewVersion;
            app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
            Interlocked.Increment(ref _scanUpdatesCounter);
        }
    }

    /// <summary>
    /// Checks for updates on a single installed application.
    /// </summary>
    private async Task CheckAppUpdateAsync(ApplicationModel app, CancellationToken cancellationToken)
    {
        await _scanSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested) return;

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
            }
        }
        finally
        {
            _scanSemaphore.Release();
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
    /// Scans a single application from context menu.
    /// </summary>
    [RelayCommand]
    private async Task ScanAppAsync(ApplicationModel? app)
    {
        if (app == null || IsScanning) return;

        app.Status = ApplicationStatus.Pending;
        app.StatusMessage = Resources.Resources.Status_Checking;
        app.CurrentVersion = string.Empty;
        app.AvailableVersion = string.Empty;

        try
        {
            var status = await _powerShellBridge.GetApplicationStatusAsync(app.AppId);

            if (status == ApplicationStatus.Installed || status == ApplicationStatus.AlreadyInstalled)
            {
                app.Status = status;

                // Check for updates
                var updateResult = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                if (updateResult.HasUpdate)
                {
                    app.Status = ApplicationStatus.UpdateAvailable;
                    app.CurrentVersion = updateResult.CurrentVersion;
                    app.AvailableVersion = updateResult.AvailableVersion;
                    app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
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

            // Update counters
            UpdateCounters();
        }
        catch (DetectionException ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = FormatLocalized("Apps_Error_DetectionFailed", "Detection failed: {0}", ex.Message);
            Debug.WriteLine($"DetectionException in ScanApplicationAsync for {ex.ApplicationId}: {ex}");
        }
        catch (PowerShellBridgeException ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = FormatLocalized("Apps_Error_PowerShell", "PowerShell error: {0}", ex.Message);
            Debug.WriteLine($"PowerShellBridgeException in ScanApplicationAsync: {ex}");
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = ex.Message;
            Debug.WriteLine($"Unexpected exception in ScanApplicationAsync: {ex}");
        }
    }

    /// <summary>
    /// Scans only the selected applications (IsSelected = true).
    /// </summary>
    [RelayCommand]
    private async Task ScanSelectedAsync()
    {
        var selectedApps = _allApplications.Where(a => a.IsSelected).ToList();

        if (selectedApps.Count == 0 || IsScanning) return;

        IsScanning = true;
        ScannedCount = 0;
        ScanTotalCount = selectedApps.Count;
        _scanInstalledCounter = 0;
        _scanUpdatesCounter = 0;
        _scanCancellationTokenSource = new CancellationTokenSource();

        // Reset statuses for selected apps
        foreach (var app in selectedApps)
        {
            app.Status = ApplicationStatus.Pending;
            app.StatusMessage = Resources.Resources.Status_Checking;
            app.CurrentVersion = string.Empty;
            app.AvailableVersion = string.Empty;
        }

        try
        {
            // Use batch detection which internally uses fast registry-first detection
            var batchResults = await _powerShellBridge.GetBatchApplicationStatusAsync(selectedApps);

            if (batchResults != null)
            {
                await ScanWithBatchResultsAsync(selectedApps, batchResults, _scanCancellationTokenSource.Token);
            }
            else
            {
                // Fallback to per-app detection
                var tasks = selectedApps.Select(app => ScanApplicationAsync(
                    app,
                    _scanCancellationTokenSource.Token));

                await Task.WhenAll(tasks);
            }
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

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdateCounters();
                CommandManager.InvalidateRequerySuggested();
            });
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
