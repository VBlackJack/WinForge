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

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Models;
using WinForge.GUI.Services.Coordinators;

namespace WinForge.GUI.ViewModels;

public partial class AppsViewModel
{
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
        _lastOperationType = "scanupdates";
        IsScanningUpdates = true;

        try
        {
            // Get installed apps only (includes UpdateAvailable which are also installed)
            List<ApplicationModel> installedApps = _allApplications
                .Where(a => a.Status == ApplicationStatus.Installed ||
                           a.Status == ApplicationStatus.AlreadyInstalled ||
                           a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();

            if (installedApps.Count == 0)
            {
                _lastOperationType = string.Empty;
                return;
            }

            AppUpdateScanResult result = await _updateCoordinator.ScanForUpdatesAsync(installedApps, forceRefresh: true);
            UpdatesAvailableCount = result.UpdatesAvailableCount;

            // Apply filter to refresh view
            ApplyFilter();
            RefreshSelectionActionState();
            _lastOperationType = string.Empty;
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

        try
        {
            AppUpdateResult result = await _updateCoordinator.UpdateAsync([app]);
            if (result.UpdatedCount > 0 && UpdatesAvailableCount > 0)
            {
                UpdatesAvailableCount--;
            }

            RefreshSelectionActionState();
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.Apps_Error_UpdateSingleFailed,
                app.Name,
                ex.Message);
        }
    }

    /// <summary>
    /// Whether the UpdateSelected command can execute.
    /// </summary>
    private bool CanUpdateSelected =>
        _allApplications.Any(app => app.Status == ApplicationStatus.UpdateAvailable) &&
        !IsInstalling &&
        !IsUninstalling;

    /// <summary>
    /// Updates all applications with available updates.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
    private async Task UpdateSelectedAsync()
    {
        List<ApplicationModel> appsWithUpdates = _allApplications
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

        _lastOperationType = "update";
        IsInstalling = true;
        IsUpdating = true;
        IsPaused = false;
        _pauseGate.Resume();
        _batchCancellationTokenSource = new CancellationTokenSource();

        ResetBatchProgress(appsWithUpdates.Count);
        SuccessCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        EstimatedTimeRemaining = Resources.Resources.Progress_Calculating;

        _progressEstimator.Start(appsWithUpdates.Count);
        _deploymentStateService.StartDeployment(appsWithUpdates);

        try
        {
            AppUpdateResult result = await _updateCoordinator.UpdateAsync(
                appsWithUpdates,
                new Progress<AppOperationProgress>(ApplyBatchProgress),
                _batchCancellationTokenSource.Token);

            CompleteBatchProgress(new AppOperationProgress(result.Total, result.Total, Current: null));

            if (result.UpdatedCount > 0)
            {
                UpdatesAvailableCount = Math.Max(0, UpdatesAvailableCount - result.UpdatedCount);
            }

            SuccessCount = result.UpdatedCount;
            FailedCount = result.FailedCount;
            SkippedCount = result.SkippedCount;

            if (result.WasCancelled)
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
            _lastOperationType = string.Empty;
        }
        finally
        {
            IsUpdating = false;
            IsInstalling = false;
            IsPaused = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;

            _deploymentStateService.EndDeployment();
            RefreshSelectionActionState();
            ApplyFilter();
        }
    }
}
