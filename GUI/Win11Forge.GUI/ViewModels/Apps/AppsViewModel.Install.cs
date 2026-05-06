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

using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.Coordinators;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Installs a single application.
    /// </summary>
    [RelayCommand]
    private async Task InstallAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        // Notify deployment state service for monitoring
        _deploymentStateService.StartDeployment([app]);
        _deploymentStateService.UpdateProgress(app.Name, 0, 1, Resources.Resources.Status_Installing);

        try
        {
            var result = await _installationCoordinator.InstallAsync(
                [app],
                new AppInstallationOptions(ForceUpdate: false));
            InstalledCount += result.InstalledCount;
            _deploymentStateService.UpdateProgress(app.Name, 1, 1, app.StatusMessage);
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _deploymentStateService.EndDeployment();
        }
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
        _pauseGate.Resume();
        _batchCancellationTokenSource = new CancellationTokenSource();

        BatchProgressCurrent = 0;
        BatchProgressTotal = selectedApps.Count;
        BatchProgressPercent = 0;
        CurrentBatchAppName = null;
        SuccessCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        EstimatedTimeRemaining = Resources.Resources.Progress_Calculating;

        // Start progress estimator
        _progressEstimator.Start(selectedApps.Count);

        // Notify deployment state service
        _deploymentStateService.StartDeployment(selectedApps);

        try
        {
            var result = await _installationCoordinator.InstallAsync(
                selectedApps,
                new AppInstallationOptions(ForceUpdate: true),
                new Progress<AppOperationProgress>(ApplyInstallProgress),
                _batchCancellationTokenSource.Token);

            ApplyInstallProgress(new AppOperationProgress(result.Total, result.Total, Current: null));
            InstalledCount += result.InstalledCount;
            SuccessCount = result.InstalledCount + result.AlreadyInstalledCount;
            FailedCount = result.FailedCount;
            SkippedCount = result.SkippedCount;

            // Determine final result
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
        }
        finally
        {
            IsInstalling = false;
            IsPaused = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;

            // Notify deployment state service
            _deploymentStateService.EndDeployment();
        }
    }

    private void ApplyInstallProgress(AppOperationProgress progress)
    {
        BatchProgressTotal = progress.Total;
        BatchProgressCurrent = progress.Completed;
        BatchProgressPercent = progress.Total > 0
            ? (double)progress.Completed / progress.Total * 100
            : 0;
        CurrentBatchAppName = progress.Current?.Name;

        // Update time estimate
        _progressEstimator.UpdateProgress(progress.Completed);
        EstimatedTimeRemaining = _progressEstimator.GetFormattedTimeRemaining();

        // Update shared deployment state with progress and time
        _deploymentStateService.UpdateProgress(
            progress.Current?.Name,
            progress.Completed,
            progress.Total,
            Resources.Resources.Progress_Deploying);
        _deploymentStateService.UpdateTime(
            _progressEstimator.GetFormattedElapsedTime(),
            EstimatedTimeRemaining);
    }
}
