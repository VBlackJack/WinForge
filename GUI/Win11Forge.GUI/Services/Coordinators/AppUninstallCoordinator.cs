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
/// Coordinates application uninstallation workflows outside the ViewModel.
/// </summary>
public sealed class AppUninstallCoordinator : IAppUninstallCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly IPauseGate _pauseGate;

    public AppUninstallCoordinator(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService,
        IPauseGate pauseGate)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _pauseGate = pauseGate ?? throw new ArgumentNullException(nameof(pauseGate));
    }

    /// <inheritdoc/>
    public async Task<AppUninstallResult> UninstallAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);

        if (applications.Count == 0)
        {
            return new AppUninstallResult(0, 0, 0, 0, WasCancelled: false);
        }

        var apps = applications.ToList();
        var runner = CreateRunner();

        try
        {
            var itemResults = await runner.RunAsync(
                apps,
                UninstallApplicationAsync,
                app => app,
                progress,
                cancellationToken).ConfigureAwait(false);

            return BuildResult(apps.Count, itemResults, cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            return BuildResultFromCurrentState(apps, WasCancelled: true);
        }
    }

    private async Task<AppUninstallItemResult> UninstallApplicationAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            app.Status = ApplicationStatus.Uninstalling;
            app.StatusMessage = Resources.Resources.Status_Uninstalling;

            var result = await _powerShellBridge.UninstallApplicationAsync(
                app,
                progress => app.StatusMessage = progress).ConfigureAwait(false);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = ApplicationStatus.Uninstalled;
                app.StatusMessage = Resources.Resources.Status_Uninstalled;
                return AppUninstallItemResult.Uninstalled();
            }

            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = result.Message;
            return AppUninstallItemResult.Failed();
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            return AppUninstallItemResult.Skipped();
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            return AppUninstallItemResult.Failed();
        }
    }

    private AppOperationRunner CreateRunner()
    {
        var maxParallelInstalls = Math.Clamp(_settingsService.LoadSettings().MaxParallelInstalls, 1, 10);
        return new AppOperationRunner(maxParallelInstalls);
    }

    private static AppUninstallResult BuildResult(
        int total,
        IReadOnlyList<AppUninstallItemResult> itemResults,
        bool WasCancelled)
    {
        return new AppUninstallResult(
            total,
            itemResults.Count(result => result.Status == AppUninstallItemStatus.Uninstalled),
            itemResults.Count(result => result.Status == AppUninstallItemStatus.Failed),
            itemResults.Count(result => result.Status == AppUninstallItemStatus.Skipped),
            WasCancelled);
    }

    private static AppUninstallResult BuildResultFromCurrentState(
        IReadOnlyList<ApplicationModel> apps,
        bool WasCancelled)
    {
        return new AppUninstallResult(
            apps.Count,
            apps.Count(app => app.Status == ApplicationStatus.Uninstalled),
            apps.Count(app => app.Status == ApplicationStatus.Failed),
            apps.Count(app => app.Status == ApplicationStatus.Skipped),
            WasCancelled);
    }

    private enum AppUninstallItemStatus
    {
        Uninstalled,
        Failed,
        Skipped
    }

    private sealed record AppUninstallItemResult(AppUninstallItemStatus Status)
    {
        public static AppUninstallItemResult Uninstalled() => new(AppUninstallItemStatus.Uninstalled);
        public static AppUninstallItemResult Failed() => new(AppUninstallItemStatus.Failed);
        public static AppUninstallItemResult Skipped() => new(AppUninstallItemStatus.Skipped);
    }
}
