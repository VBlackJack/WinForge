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
        CurrentBatchAppName = null;
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
    /// <remarks>
    /// Uses Interlocked operations for thread-safe counter updates, which require backing field access.
    /// </remarks>
#pragma warning disable MVVMTK0034 // Direct field reference required for Interlocked operations
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
            CurrentBatchAppName = app.Name;

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
#pragma warning restore MVVMTK0034
}
