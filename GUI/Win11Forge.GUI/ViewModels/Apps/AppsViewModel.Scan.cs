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
using Win11Forge.GUI.Services.Coordinators;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    partial void OnScannedCountChanged(int value)
    {
        _externalProgressCallback?.Invoke(value, ScanTotalCount);
    }
    private bool CanScan => !IsScanning && _allApplications.Count > 0;
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_allApplications.Count == 0)
        {
            _externalCompletionCallback?.Invoke(0);
            return;
        }
        var hasActiveFilter = HasSearchFilter || HasCategoryFilter || HasStatusFilter;
        var appsToScan = hasActiveFilter
            ? FilteredApplications.Cast<ApplicationModel>().ToList()
            : _allApplications;
        if (appsToScan.Count == 0)
        {
            _externalCompletionCallback?.Invoke(UpdatesAvailableCount);
            return;
        }
        AppScanResult? scanResult = null;
        var completionCallback = _externalCompletionCallback;
        _lastOperationType = "scan";
        IsScanning = true;
        ResetScanState(appsToScan, resetGlobalCounters: !hasActiveFilter);
        _scanCancellationTokenSource = new CancellationTokenSource();
        try
        {
            scanResult = await _scanCoordinator.ScanAsync(
                appsToScan,
                CreateScanProgress(),
                _scanCancellationTokenSource.Token);
            _lastOperationType = string.Empty;
        }
        finally
        {
            IsScanning = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
            await InvokeOnUiAsync(() =>
            {
                if (hasActiveFilter)
                {
                    RecountAfterFilteredScan();
                }
                else if (scanResult != null)
                {
                    InstalledCount = scanResult.InstalledCount;
                    UpdatesAvailableCount = scanResult.UpdatesAvailableCount;
                }

                CommandManager.InvalidateRequerySuggested();
                completionCallback?.Invoke(UpdatesAvailableCount);
            });
        }
    }
    [RelayCommand]
    private async Task ScanAppAsync(ApplicationModel? app)
    {
        if (app == null || IsScanning) return;
        PrepareAppForScan(app);
        try
        {
            await _scanCoordinator.ScanAsync([app]);
            UpdateCounters();
        }
        catch (DetectionException ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = FormatLocalized("Apps_Error_DetectionFailed", "Detection failed: {0}", ex.Message);
            Debug.WriteLine($"DetectionException in ScanAppAsync for {ex.ApplicationId}: {ex}");
        }
        catch (PowerShellBridgeException ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = FormatLocalized("Apps_Error_PowerShell", "PowerShell error: {0}", ex.Message);
            Debug.WriteLine($"PowerShellBridgeException in ScanAppAsync: {ex}");
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = ex.Message;
            Debug.WriteLine($"Unexpected exception in ScanAppAsync: {ex}");
        }
    }
    [RelayCommand]
    private async Task ScanSelectedAsync()
    {
        var selectedApps = _allApplications.Where(a => a.IsSelected).ToList();
        if (selectedApps.Count == 0 || IsScanning) return;
        IsScanning = true;
        ResetScanState(selectedApps, resetGlobalCounters: false);
        _scanCancellationTokenSource = new CancellationTokenSource();
        try
        {
            await _scanCoordinator.ScanAsync(
                selectedApps,
                CreateScanProgress(),
                _scanCancellationTokenSource.Token);
        }
        finally
        {
            IsScanning = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
            await InvokeOnUiAsync(() =>
            {
                UpdateCounters();
                CommandManager.InvalidateRequerySuggested();
            });
        }
    }
    [RelayCommand]
    private void CancelScan() => _scanCancellationTokenSource?.Cancel();

    private IProgress<AppOperationProgress> CreateScanProgress() =>
        new Progress<AppOperationProgress>(ApplyScanProgress);

    private void ApplyScanProgress(AppOperationProgress progress)
    {
        ScanTotalCount = progress.Total;
        ScannedCount = progress.Completed;
    }

    private void ResetScanState(IReadOnlyCollection<ApplicationModel> apps, bool resetGlobalCounters)
    {
        ApplyScanProgress(new AppOperationProgress(0, apps.Count, Current: null));
        if (resetGlobalCounters)
        {
            InstalledCount = 0;
            UpdatesAvailableCount = 0;
        }
        foreach (var app in apps) PrepareAppForScan(app);
    }

    private static void PrepareAppForScan(ApplicationModel app)
    {
        app.Status = ApplicationStatus.Pending;
        app.StatusMessage = Resources.Resources.Status_Checking;
        app.CurrentVersion = string.Empty;
        app.AvailableVersion = string.Empty;
    }

    private void RecountAfterFilteredScan()
    {
        InstalledCount = _allApplications.Count(a =>
            a.Status == ApplicationStatus.Installed ||
            a.Status == ApplicationStatus.AlreadyInstalled);
        UpdatesAvailableCount = _allApplications.Count(a =>
            !string.IsNullOrEmpty(a.AvailableVersion) &&
            a.AvailableVersion != a.CurrentVersion);
    }

    private static async Task InvokeOnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }
}
