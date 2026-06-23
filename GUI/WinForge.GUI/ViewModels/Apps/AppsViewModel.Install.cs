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
            AppInstallationResult result = await _installationCoordinator.InstallAsync(
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
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.Apps_Error_InstallSingleFailed,
                app.Name,
                ex.Message);
        }
        finally
        {
            _deploymentStateService.EndDeployment();
            RefreshSelectionActionState();
        }
    }

    /// <summary>
    /// Whether the InstallSelected command can execute.
    /// </summary>
    private bool CanInstallSelected =>
        _allApplications.Any(app => app.IsSelected && IsPrimarySelectionActionable(app)) &&
        !IsInstalling &&
        !IsUninstalling;

    /// <summary>
    /// Display text for the primary selected-app action.
    /// </summary>
    public string SelectedPrimaryActionText
    {
        get
        {
            bool hasInstallableSelection = _allApplications.Any(app => app.IsSelected && IsInstallCandidate(app));
            bool hasUpdateSelection = _allApplications.Any(app => app.IsSelected && IsUpdateCandidate(app));

            if (hasInstallableSelection && hasUpdateSelection)
            {
                return Resources.Resources.Apps_InstallUpdateSelected;
            }

            if (hasUpdateSelection)
            {
                return Resources.Resources.Btn_UpdateSelected;
            }

            return Resources.Resources.Apps_InstallSelected;
        }
    }

    /// <summary>
    /// Applies the correct operation to all selected applications.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallSelected))]
    private async Task InstallSelectedAsync()
    {
        List<ApplicationModel> selectedApps = _allApplications.Where(a => a.IsSelected).ToList();
        if (selectedApps.Count == 0) return;

        List<ApplicationModel> installApps = selectedApps.Where(IsInstallCandidate).ToList();
        List<ApplicationModel> updateApps = selectedApps.Where(IsUpdateCandidate).ToList();
        int alreadyCurrentCount = selectedApps.Count(IsAlreadyCurrentCandidate);

        if (installApps.Count == 0 && updateApps.Count == 0) return;

        _lastOperationType = "install";
        IsInstalling = true;
        IsUpdating = updateApps.Count > 0;
        IsPaused = false;
        _pauseGate.Resume();
        _batchCancellationTokenSource = new CancellationTokenSource();

        ResetBatchProgress(selectedApps.Count);
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
            int completedOffset = 0;
            if (alreadyCurrentCount > 0)
            {
                completedOffset = alreadyCurrentCount;
                ApplyBatchProgress(new AppOperationProgress(completedOffset, selectedApps.Count, Current: null));
            }

            AppInstallationResult installResult = new(0, 0, 0, 0, 0, WasCancelled: false);
            if (installApps.Count > 0)
            {
                installResult = await _installationCoordinator.InstallAsync(
                    installApps,
                    new AppInstallationOptions(ForceUpdate: false),
                    CreateOffsetProgress(completedOffset, selectedApps.Count),
                    _batchCancellationTokenSource.Token);
                completedOffset += installApps.Count;
            }

            AppUpdateResult updateResult = new(0, 0, 0, 0, WasCancelled: false);
            if (updateApps.Count > 0)
            {
                updateResult = await _updateCoordinator.UpdateAsync(
                    updateApps,
                    CreateOffsetProgress(completedOffset, selectedApps.Count),
                    _batchCancellationTokenSource.Token);
            }

            CompleteBatchProgress(new AppOperationProgress(selectedApps.Count, selectedApps.Count, Current: null));
            InstalledCount += installResult.InstalledCount;
            UpdatesAvailableCount = Math.Max(0, UpdatesAvailableCount - updateResult.UpdatedCount);
            SuccessCount = installResult.InstalledCount + installResult.AlreadyInstalledCount + updateResult.UpdatedCount;
            FailedCount = installResult.FailedCount + updateResult.FailedCount;
            SkippedCount = alreadyCurrentCount + installResult.SkippedCount + updateResult.SkippedCount;

            // Determine final result
            if (installResult.WasCancelled || updateResult.WasCancelled)
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

            // Notify deployment state service
            _deploymentStateService.EndDeployment();
            RefreshSelectionActionState();
            ApplyFilter();
        }
    }

    private IProgress<AppOperationProgress> CreateOffsetProgress(int completedOffset, int total)
    {
        return new Progress<AppOperationProgress>(progress =>
        {
            ApplyBatchProgress(new AppOperationProgress(
                completedOffset + progress.Completed,
                total,
                progress.Current));
        });
    }

    private static bool IsPrimarySelectionActionable(ApplicationModel app)
    {
        return IsInstallCandidate(app) || IsUpdateCandidate(app);
    }

    private static bool IsInstallCandidate(ApplicationModel app)
    {
        return app.Status is ApplicationStatus.Pending or
            ApplicationStatus.Failed or
            ApplicationStatus.Skipped or
            ApplicationStatus.Uninstalled;
    }

    private static bool IsUpdateCandidate(ApplicationModel app)
    {
        return app.Status == ApplicationStatus.UpdateAvailable;
    }

    private static bool IsAlreadyCurrentCandidate(ApplicationModel app)
    {
        return app.Status is ApplicationStatus.Installed or ApplicationStatus.AlreadyInstalled;
    }

    private void ApplyBatchProgress(AppOperationProgress progress)
    {
        if (ShouldIgnoreBatchProgress(progress))
        {
            return;
        }

        ApplyBatchProgressCore(progress, updateDeploymentState: true);
    }

    private void ApplyBatchProgressCore(AppOperationProgress progress, bool updateDeploymentState)
    {
        _lastAppliedBatchProgressCompleted = progress.Completed;
        BatchProgressTotal = progress.Total;
        BatchProgressCurrent = progress.Completed;
        BatchProgressPercent = progress.Total > 0
            ? (double)progress.Completed / progress.Total * 100
            : 0;
        CurrentBatchAppName = progress.Current?.Name;

        // Update time estimate
        _progressEstimator.UpdateProgress(progress.Completed);
        EstimatedTimeRemaining = _progressEstimator.GetFormattedTimeRemaining();

        if (updateDeploymentState)
        {
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

    private void ResetBatchProgress(int total)
    {
        _batchProgressFinalized = false;
        _lastAppliedBatchProgressCompleted = 0;
        BatchProgressCurrent = 0;
        BatchProgressTotal = total;
        BatchProgressPercent = 0;
        CurrentBatchAppName = null;
    }

    private bool ShouldIgnoreBatchProgress(AppOperationProgress progress)
    {
        return _batchProgressFinalized ||
            progress.Completed < _lastAppliedBatchProgressCompleted;
    }

    private void CompleteBatchProgress(AppOperationProgress progress)
    {
        _batchProgressFinalized = true;
        ApplyBatchProgressCore(progress, updateDeploymentState: true);
    }

    private void CompleteUninstallProgress(AppOperationProgress progress)
    {
        _batchProgressFinalized = true;
        ApplyBatchProgressCore(progress, updateDeploymentState: false);
    }
}
