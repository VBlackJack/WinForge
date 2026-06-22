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

using WinForge.GUI.Models;
using WinForge.GUI.Resources;
using WinForge.GUI.Services.Coordinators.Internal;
using WinForge.GUI.Services.Resume;

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates application uninstallation workflows outside the ViewModel.
/// </summary>
public sealed class AppUninstallCoordinator : IAppUninstallCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly IPauseGate _pauseGate;
    private readonly IBatchResumeService _resumeService;

    public AppUninstallCoordinator(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService,
        IPauseGate pauseGate,
        IBatchResumeService resumeService)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _pauseGate = pauseGate ?? throw new ArgumentNullException(nameof(pauseGate));
        _resumeService = resumeService ?? throw new ArgumentNullException(nameof(resumeService));
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

        List<ApplicationModel> apps = applications.ToList();
        AppOperationRunner runner = CreateRunner();

        Guid batchId = await _resumeService.BeginBatchAsync(
            BatchOperationKind.Uninstall,
            apps.Select(app => app.AppId).ToArray(),
            new BatchOptions(ForceUpdate: false),
            cancellationToken).ConfigureAwait(false);

        try
        {
            IReadOnlyList<AppUninstallItemResult> itemResults = await runner.RunAsync(
                apps,
                UninstallApplicationAsync,
                app => app,
                progress,
                cancellationToken,
                onItemCompleted: (app, result, token) =>
                    _resumeService.AppendCompletedAsync(batchId, app.AppId, ToOutcome(result.Status), token))
                .ConfigureAwait(false);

            // Mark with CancellationToken.None: once we reach this point the batch is
            // logically finished, the transition to Completed should not be cancellable.
            await _resumeService.MarkBatchCompletedAsync(batchId, CancellationToken.None).ConfigureAwait(false);
            return BuildResult(apps.Count, itemResults, cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Graceful user cancellation; mark complete so the user is not re-prompted.
            // A real crash would bypass this catch and leave InProgress in the file.
            await _resumeService.MarkBatchCompletedAsync(batchId, CancellationToken.None).ConfigureAwait(false);
            return BuildResultFromCurrentState(apps, WasCancelled: true);
        }
    }

    private static BatchItemOutcome ToOutcome(AppUninstallItemStatus status) => status switch
    {
        AppUninstallItemStatus.Uninstalled => BatchItemOutcome.Uninstalled,
        AppUninstallItemStatus.Failed => BatchItemOutcome.Failed,
        AppUninstallItemStatus.Skipped => BatchItemOutcome.Skipped,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

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

            InstallResult result = await _powerShellBridge.UninstallApplicationAsync(
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
        int maxParallelInstalls = Math.Clamp(_settingsService.LoadSettings().MaxParallelInstalls, 1, 10);
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
