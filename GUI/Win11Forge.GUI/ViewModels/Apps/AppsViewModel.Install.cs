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

        await _installSemaphore.WaitAsync();

        // Notify deployment state service for monitoring
        _deploymentStateService.StartDeployment([app]);

        try
        {
            app.Status = ApplicationStatus.Installing;
            app.StatusMessage = Resources.Resources.Status_Installing;

            _deploymentStateService.UpdateProgress(app.Name, 0, 1, Resources.Resources.Status_Installing);

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
            _installSemaphore.Release();
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
        _pauseEvent.Set();
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

            // Notify deployment state service
            _deploymentStateService.EndDeployment();
        }
    }

    /// <summary>
    /// Installs a single application with semaphore-controlled concurrency.
    /// </summary>
    /// <remarks>
    /// Uses Interlocked operations for thread-safe counter updates, which require backing field access.
    /// </remarks>
#pragma warning disable MVVMTK0034 // Direct field reference required for Interlocked operations
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
            CurrentBatchAppName = app.Name;

            // Update shared deployment state
            _deploymentStateService.UpdateProgress(
                app.Name,
                _batchProgressCurrent,
                BatchProgressTotal,
                Resources.Resources.Status_Installing);

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

            // Update shared deployment state with progress and time
            _deploymentStateService.UpdateProgress(
                null,
                _batchProgressCurrent,
                BatchProgressTotal,
                Resources.Resources.Progress_Deploying);
            _deploymentStateService.UpdateTime(
                _progressEstimator.GetFormattedElapsedTime(),
                EstimatedTimeRemaining);
        }
    }
#pragma warning restore MVVMTK0034
}
