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

using Win11Forge.GUI.Models;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services.Coordinators.Internal;
using Win11Forge.GUI.Services.Resume;

namespace Win11Forge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates update discovery and update application workflows outside the ViewModel.
/// </summary>
public sealed class AppUpdateCoordinator : IAppUpdateCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly IBatchResumeService _resumeService;

    public AppUpdateCoordinator(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService,
        IBatchResumeService resumeService)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _resumeService = resumeService ?? throw new ArgumentNullException(nameof(resumeService));
    }

    /// <inheritdoc/>
    public async Task<AppUpdateScanResult> ScanForUpdatesAsync(
        IReadOnlyCollection<ApplicationModel> installedApps,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installedApps);

        if (installedApps.Count == 0)
        {
            return new AppUpdateScanResult(0, 0, WasCancelled: false);
        }

        var apps = installedApps.ToList();
        var maxParallelScans = Math.Clamp(_settingsService.LoadSettings().MaxParallelScans, 1, 20);
        var runner = new AppOperationRunner(maxParallelScans);

        try
        {
            var itemResults = await runner.RunAsync(
                apps,
                CheckApplicationUpdateAsync,
                app => app,
                progress,
                cancellationToken).ConfigureAwait(false);

            return new AppUpdateScanResult(
                apps.Count,
                itemResults.Count(result => result.HasUpdate),
                cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            return new AppUpdateScanResult(
                apps.Count,
                apps.Count(app => app.Status == ApplicationStatus.UpdateAvailable),
                WasCancelled: true);
        }
    }

    /// <inheritdoc/>
    public async Task<AppUpdateResult> UpdateAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);

        if (applications.Count == 0)
        {
            return new AppUpdateResult(0, 0, 0, 0, WasCancelled: false);
        }

        var apps = applications.ToList();
        var updatedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var completed = 0;
        var wasCancelled = false;

        var batchId = await _resumeService.BeginBatchAsync(
            BatchOperationKind.Update,
            apps.Select(app => app.AppId).ToArray(),
            new BatchOptions(ForceUpdate: false),
            cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var app in apps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    skippedCount += apps.Count - completed;
                    break;
                }

                var itemResult = await UpdateApplicationAsync(app, cancellationToken).ConfigureAwait(false);
                completed++;

                switch (itemResult.Status)
                {
                    case AppUpdateItemStatus.Updated:
                        updatedCount++;
                        break;
                    case AppUpdateItemStatus.Failed:
                        failedCount++;
                        break;
                    case AppUpdateItemStatus.Skipped:
                        skippedCount++;
                        wasCancelled = true;
                        break;
                }

                // Checkpoint inline since UpdateAsync is sequential and does not go through
                // AppOperationRunner. The runner-based callback path is reserved for the
                // parallel Install / Uninstall coordinators.
                try
                {
                    await _resumeService.AppendCompletedAsync(
                        batchId,
                        app.AppId,
                        ToOutcome(itemResult.Status),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AppUpdateCoordinator] Checkpoint append failed for {app.AppId}: {ex.Message}");
                }

                progress?.Report(new AppOperationProgress(completed, apps.Count, app));
            }
        }
        catch (OperationCanceledException)
        {
            // UpdateApplicationAsync absorbs OperationCanceledException by design, so
            // this catch only fires when AppendCompletedAsync observes the same token
            // mid-batch. Preserve the original UpdateAsync contract that does not
            // throw on cancel; the wasCancelled signal is propagated through the
            // returned result instead.
            wasCancelled = true;
            skippedCount += apps.Count - completed;
        }

        // Mark with CancellationToken.None so the Completed transition is never lost
        // due to a late cancellation; a real crash bypasses this line entirely and
        // leaves the file in BatchState.InProgress, which is exactly the signal we
        // want for the next-startup resume prompt.
        await _resumeService.MarkBatchCompletedAsync(batchId, CancellationToken.None).ConfigureAwait(false);

        return new AppUpdateResult(apps.Count, updatedCount, failedCount, skippedCount, wasCancelled);
    }

    private static BatchItemOutcome ToOutcome(AppUpdateItemStatus status) => status switch
    {
        AppUpdateItemStatus.Updated => BatchItemOutcome.Updated,
        AppUpdateItemStatus.Failed => BatchItemOutcome.Failed,
        AppUpdateItemStatus.Skipped => BatchItemOutcome.Skipped,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private async Task<AppUpdateScanItemResult> CheckApplicationUpdateAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        app.StatusMessage = Resources.Resources.Common_Loading;
        var result = await _powerShellBridge.CheckApplicationUpdateAsync(app).ConfigureAwait(false);

        if (result.HasUpdate)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.CurrentVersion = result.CurrentVersion;
            app.AvailableVersion = result.AvailableVersion;
            app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
            return new AppUpdateScanItemResult(HasUpdate: true);
        }

        app.Status = ApplicationStatus.Installed;
        app.CurrentVersion = result.CurrentVersion;
        app.AvailableVersion = string.Empty;
        app.StatusMessage = Resources.Resources.Status_Installed;
        return new AppUpdateScanItemResult(HasUpdate: false);
    }

    private async Task<AppUpdateItemResult> UpdateApplicationAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            app.Status = ApplicationStatus.Updating;
            app.StatusMessage = Resources.Resources.Status_Updating;

            var result = await _powerShellBridge.UpdateApplicationAsync(
                app,
                progress => app.StatusMessage = progress).ConfigureAwait(false);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                await RefreshUpdatedApplicationAsync(app).ConfigureAwait(false);
                return AppUpdateItemResult.Updated();
            }

            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = result.Message;
            return AppUpdateItemResult.Failed();
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            return AppUpdateItemResult.Skipped();
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            return AppUpdateItemResult.Failed();
        }
    }

    private async Task RefreshUpdatedApplicationAsync(ApplicationModel app)
    {
        var updateCheck = await _powerShellBridge.CheckApplicationUpdateAsync(app).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(updateCheck.CurrentVersion))
        {
            app.CurrentVersion = updateCheck.CurrentVersion;
        }
        else if (!string.IsNullOrEmpty(app.AvailableVersion))
        {
            app.CurrentVersion = app.AvailableVersion;
        }

        app.AvailableVersion = string.Empty;
        app.Status = ApplicationStatus.Installed;
        app.StatusMessage = Resources.Resources.Status_Installed;
    }

    private sealed record AppUpdateScanItemResult(bool HasUpdate);

    private enum AppUpdateItemStatus
    {
        Updated,
        Failed,
        Skipped
    }

    private sealed record AppUpdateItemResult(AppUpdateItemStatus Status)
    {
        public static AppUpdateItemResult Updated() => new(AppUpdateItemStatus.Updated);
        public static AppUpdateItemResult Failed() => new(AppUpdateItemStatus.Failed);
        public static AppUpdateItemResult Skipped() => new(AppUpdateItemStatus.Skipped);
    }
}
