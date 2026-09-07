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
using WinForge.GUI.Services.PowerShell;
using System.IO;
using System.Text.Json;

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates application installation workflows outside the ViewModel.
/// </summary>
public sealed class AppInstallationCoordinator : IAppInstallationCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IAppSettingsService _settingsService;
    private readonly IPauseGate _pauseGate;
    private readonly IBatchResumeService _resumeService;
    private readonly IRepositoryPathService? _pathService;

    public AppInstallationCoordinator(
        IPowerShellBridge powerShellBridge,
        IAppSettingsService settingsService,
        IPauseGate pauseGate,
        IBatchResumeService resumeService,
        IRepositoryPathService? pathService = null)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _pauseGate = pauseGate ?? throw new ArgumentNullException(nameof(pauseGate));
        _resumeService = resumeService ?? throw new ArgumentNullException(nameof(resumeService));
        _pathService = pathService;
    }

    /// <inheritdoc/>
    public async Task<AppInstallationResult> InstallAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        AppInstallationOptions options,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(options);

        if (applications.Count == 0)
        {
            return new AppInstallationResult(0, 0, 0, 0, 0, WasCancelled: false);
        }

        List<ApplicationModel> apps = applications.ToList();
        AppOperationRunner runner = CreateRunner();

        Dictionary<string, JsonElement> definitions = new(StringComparer.OrdinalIgnoreCase);
        if (_pathService is not null)
        {
            string catalogText = await File.ReadAllTextAsync(
                _pathService.GetPath("Apps", "Database", "applications.json"), cancellationToken).ConfigureAwait(false);
            using JsonDocument catalog = JsonDocument.Parse(catalogText);
            foreach (ApplicationModel app in apps)
            {
                using JsonDocument definition = JsonDocument.Parse(app.FrozenDefinitionJson ??
                    catalog.RootElement.GetProperty("Applications").GetProperty(app.AppId).GetRawText());
                definitions.Add(app.AppId, definition.RootElement.Clone());
            }
        }

        Guid batchId = await _resumeService.BeginBatchAsync(
            BatchOperationKind.Install,
            apps.Select(app => app.AppId).ToArray(),
            new BatchOptions(options.ForceUpdate) { Definitions = definitions },
            cancellationToken).ConfigureAwait(false);

        try
        {
            IReadOnlyList<AppInstallationItemResult> itemResults = await runner.RunAsync(
                apps,
                (app, token) => InstallFrozenApplicationAsync(app, options, definitions, token),
                app => app,
                progress,
                cancellationToken,
                onItemCompleted: async (app, result, token) =>
                {
                    await _resumeService.AppendCompletedAsync(batchId, app.AppId, ToOutcome(result.Status), token).ConfigureAwait(false);
                    if (result.Observation is not null)
                    {
                        await _resumeService.RecordObservationAsync(batchId, app.AppId, result.Observation, token).ConfigureAwait(false);
                    }
                })
                .ConfigureAwait(false);

            if (itemResults.Any(result => result.Status == AppInstallationItemStatus.Installed))
            {
                await InvalidateUpdateCacheBestEffortAsync().ConfigureAwait(false);
            }

            // Mark with CancellationToken.None: once we reach this point the batch is
            // logically finished, the transition to Completed should not be cancellable.
            await _resumeService.MarkBatchCompletedAsync(batchId, CancellationToken.None).ConfigureAwait(false);
            return BuildResult(apps.Count, itemResults, cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Graceful user cancellation: the batch is considered complete from the
            // resume service's perspective so the user is not re-prompted at next
            // launch. A real crash would bypass this catch and leave the file in
            // BatchState.InProgress, which is exactly the signal we want.
            await _resumeService.MarkBatchCompletedAsync(batchId, CancellationToken.None).ConfigureAwait(false);
            return BuildResultFromCurrentState(apps, WasCancelled: true);
        }
    }

    private static BatchItemOutcome ToOutcome(AppInstallationItemStatus status) => status switch
    {
        AppInstallationItemStatus.Installed => BatchItemOutcome.Installed,
        AppInstallationItemStatus.AlreadyInstalled => BatchItemOutcome.AlreadyInstalled,
        AppInstallationItemStatus.Failed => BatchItemOutcome.Failed,
        AppInstallationItemStatus.Skipped => BatchItemOutcome.Skipped,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private async Task<AppInstallationItemResult> InstallFrozenApplicationAsync(
        ApplicationModel app, AppInstallationOptions options,
        IReadOnlyDictionary<string, JsonElement> definitions, CancellationToken cancellationToken)
    {
        string? previous = app.FrozenDefinitionJson;
        if (definitions.TryGetValue(app.AppId, out JsonElement definition))
        {
            app.FrozenDefinitionJson = definition.GetRawText();
        }
        try { return await InstallApplicationAsync(app, options, cancellationToken).ConfigureAwait(false); }
        finally { app.FrozenDefinitionJson = previous; }
    }

    private async Task<AppInstallationItemResult> InstallApplicationAsync(
        ApplicationModel app,
        AppInstallationOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            app.Status = ApplicationStatus.Installing;
            app.StatusMessage = Resources.Resources.Status_Installing;

            InstallResult result = await _powerShellBridge.InstallApplicationAsync(
                app,
                isDryRun: false,
                forceUpdate: options.ForceUpdate,
                progressCallback: progress => app.StatusMessage = progress).ConfigureAwait(false);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = result.AlreadyInstalled
                    ? ApplicationStatus.AlreadyInstalled
                    : ApplicationStatus.Installed;
                app.StatusMessage = result.AlreadyInstalled
                    ? Resources.Resources.Status_AlreadyInstalled
                    : Resources.Resources.Status_Installed;

                if (result.InstalledVersion is not null) { app.CurrentVersion = result.InstalledVersion; }
                return new AppInstallationItemResult(result.AlreadyInstalled
                    ? AppInstallationItemStatus.AlreadyInstalled : AppInstallationItemStatus.Installed)
                {
                    Observation = new BatchObservation(result.InstalledVersion, result.Method, result.Message)
                };
            }

            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = result.Message;
            return AppInstallationItemResult.Failed() with { Observation = new BatchObservation(null, result.Method, result.Message) };
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
            return AppInstallationItemResult.Skipped();
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
            return AppInstallationItemResult.Failed();
        }
    }

    private AppOperationRunner CreateRunner()
    {
        int maxParallelInstalls = Math.Clamp(_settingsService.LoadSettings().MaxParallelInstalls, 1, 10);
        return new AppOperationRunner(maxParallelInstalls);
    }

    private async Task InvalidateUpdateCacheBestEffortAsync()
    {
        try
        {
            Task? invalidation = _powerShellBridge.InvalidateUpdateCacheAsync();
            if (invalidation is not null)
            {
                await invalidation.ConfigureAwait(false);
            }
        }
        catch
        {
            // Cache invalidation is a freshness optimization; the install result remains authoritative.
        }
    }

    private static AppInstallationResult BuildResult(
        int total,
        IReadOnlyList<AppInstallationItemResult> itemResults,
        bool WasCancelled)
    {
        return new AppInstallationResult(
            total,
            itemResults.Count(result => result.Status == AppInstallationItemStatus.Installed),
            itemResults.Count(result => result.Status == AppInstallationItemStatus.AlreadyInstalled),
            itemResults.Count(result => result.Status == AppInstallationItemStatus.Failed),
            itemResults.Count(result => result.Status == AppInstallationItemStatus.Skipped),
            WasCancelled);
    }

    private static AppInstallationResult BuildResultFromCurrentState(
        IReadOnlyList<ApplicationModel> apps,
        bool WasCancelled)
    {
        return new AppInstallationResult(
            apps.Count,
            apps.Count(app => app.Status == ApplicationStatus.Installed),
            apps.Count(app => app.Status == ApplicationStatus.AlreadyInstalled),
            apps.Count(app => app.Status == ApplicationStatus.Failed),
            apps.Count(app => app.Status == ApplicationStatus.Skipped),
            WasCancelled);
    }

    private enum AppInstallationItemStatus
    {
        Installed,
        AlreadyInstalled,
        Failed,
        Skipped
    }

    private sealed record AppInstallationItemResult(AppInstallationItemStatus Status)
    {
        public BatchObservation? Observation { get; init; }
        public static AppInstallationItemResult Installed() => new(AppInstallationItemStatus.Installed);
        public static AppInstallationItemResult AlreadyInstalled() => new(AppInstallationItemStatus.AlreadyInstalled);
        public static AppInstallationItemResult Failed() => new(AppInstallationItemStatus.Failed);
        public static AppInstallationItemResult Skipped() => new(AppInstallationItemStatus.Skipped);
    }
}
