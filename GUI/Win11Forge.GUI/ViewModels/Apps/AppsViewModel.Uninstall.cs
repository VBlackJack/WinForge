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
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.Coordinators;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Uninstalls a single application.
    /// </summary>
    [RelayCommand]
    private async Task UninstallAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        try
        {
            var result = await _uninstallCoordinator.UninstallAsync([app]);
            InstalledCount = Math.Max(0, InstalledCount - result.UninstalledCount);
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.Apps_Error_UninstallSingleFailed,
                app.Name,
                ex.Message);
        }
    }

    /// <summary>
    /// Whether the UninstallSelected command can execute.
    /// </summary>
    private bool CanUninstallSelected => SelectedCount > 0 && !IsUninstalling && !IsInstalling;

    /// <summary>
    /// Uninstalls all selected applications.
    /// Uses parallel execution with coordinator-controlled concurrency.
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

        var confirmMessage = string.Format(Resources.Resources.Confirm_Uninstall_Message, selectedApps.Count);
        var confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.Confirm_Uninstall_Title,
            confirmMessage,
            Resources.Resources.Common_Yes,
            Resources.Resources.Common_No);
        if (!confirmed)
        {
            return;
        }

        _lastOperationType = "uninstall";
        IsUninstalling = true;
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

        _progressEstimator.Start(selectedApps.Count);

        try
        {
            var result = await _uninstallCoordinator.UninstallAsync(
                selectedApps,
                new Progress<AppOperationProgress>(ApplyUninstallProgress),
                _batchCancellationTokenSource.Token);

            ApplyUninstallProgress(new AppOperationProgress(result.Total, result.Total, Current: null));
            InstalledCount = Math.Max(0, InstalledCount - result.UninstalledCount);
            SuccessCount = result.UninstalledCount;
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
            IsUninstalling = false;
            IsPaused = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = null;
        }
    }

    private void ApplyUninstallProgress(AppOperationProgress progress)
    {
        BatchProgressTotal = progress.Total;
        BatchProgressCurrent = progress.Completed;
        BatchProgressPercent = progress.Total > 0
            ? (double)progress.Completed / progress.Total * 100
            : 0;
        CurrentBatchAppName = progress.Current?.Name;

        _progressEstimator.UpdateProgress(progress.Completed);
        EstimatedTimeRemaining = _progressEstimator.GetFormattedTimeRemaining();
    }
}
