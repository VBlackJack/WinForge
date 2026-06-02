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

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Whether the Pause command can execute.
    /// </summary>
    private bool CanPause => (IsInstalling || IsUninstalling) && !IsUpdating && !IsPaused;

    /// <summary>
    /// Pauses the current batch operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = true;
        _pauseGate.Pause();
        _deploymentStateService.SetPaused(true);
    }

    /// <summary>
    /// Whether the Resume command can execute.
    /// </summary>
    private bool CanResume => (IsInstalling || IsUninstalling) && !IsUpdating && IsPaused;

    /// <summary>
    /// Resumes the current batch operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        IsPaused = false;
        _pauseGate.Resume();
        _deploymentStateService.SetPaused(false);
    }

    /// <summary>
    /// Cancels the current batch operation.
    /// </summary>
    [RelayCommand]
    private async Task CancelBatchAsync()
    {
        bool confirmed = await _dialogService.ShowConfirmAsync(
            GetLocalizedString("Apps_CancelBatch_Title", "Cancel operation"),
            string.Format(
                CultureInfo.CurrentCulture,
                GetLocalizedString("Apps_CancelBatch_Message", "Cancel the current operation? {0} of {1} items have completed."),
                BatchProgressCurrent,
                BatchProgressTotal),
            Resources.Resources.Btn_CancelBatch,
            Resources.Resources.Common_Cancel);

        if (confirmed)
        {
            RequestBatchCancellation();
        }
    }

    private void RequestBatchCancellation()
    {
        StatusMessage = GetLocalizedString("Cancel_InProgress", "Cancelling operation...");
        _batchCancellationTokenSource?.Cancel();
        // Resume if paused to allow cancellation to propagate
        if (IsPaused)
        {
            IsPaused = false;
            _pauseGate.Resume();
        }
    }

    /// <summary>
    /// Closes the summary dialog.
    /// </summary>
    [RelayCommand]
    private void CloseSummaryDialog()
    {
        IsSummaryDialogOpen = false;
    }
}
