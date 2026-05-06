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

namespace Win11Forge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates update discovery and update application workflows outside the ViewModel.
/// </summary>
public sealed class AppUpdateCoordinator : IAppUpdateCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;

    public AppUpdateCoordinator(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
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

            progress?.Report(new AppOperationProgress(completed, apps.Count, app));
        }

        return new AppUpdateResult(apps.Count, updatedCount, failedCount, skippedCount, wasCancelled);
    }

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
