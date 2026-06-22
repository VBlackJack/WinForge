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

using System.Text;
using WinForge.GUI.Models;
using WinForge.GUI.Resources;
using WinForge.GUI.Services.Coordinators.Internal;

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates application installation status scans and update detection.
/// </summary>
public sealed class AppScanCoordinator : IAppScanCoordinator
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IApplicationDetectionService _detectionService;
    private readonly IAppSettingsService _settingsService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILoggingService _logger;

    public AppScanCoordinator(
        IPowerShellBridge powerShellBridge,
        IApplicationDetectionService detectionService,
        IAppSettingsService settingsService,
        ILoggerFactory? loggerFactory = null)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _loggerFactory = loggerFactory ?? new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<AppScanCoordinator>();
    }

    /// <inheritdoc/>
    public async Task<AppScanResult> ScanAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);

        if (applications.Count == 0)
        {
            return new AppScanResult(0, 0, 0, WasCancelled: false);
        }

        List<ApplicationModel> apps = applications.ToList();

        try
        {
            Dictionary<string, BatchAppStatus>? batchResults = await _powerShellBridge.GetBatchApplicationStatusAsync(apps).ConfigureAwait(false);
            if (batchResults != null)
            {
                _logger.LogInfo($"Batch detection succeeded: {batchResults.Count} apps checked");
                return await ScanWithBatchResultsAsync(
                    apps,
                    batchResults,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            AppOperationRunner runner = CreateRunner();
            IReadOnlyList<AppScanItemResult> itemResults = await runner.RunAsync(
                apps,
                ScanApplicationAsync,
                app => app,
                progress,
                cancellationToken).ConfigureAwait(false);

            return BuildResult(apps.Count, itemResults, WasCancelled: false);
        }
        catch (OperationCanceledException)
        {
            return BuildResultFromCurrentState(apps, WasCancelled: true);
        }
    }

    private async Task<AppScanResult> ScanWithBatchResultsAsync(
        IReadOnlyList<ApplicationModel> apps,
        IReadOnlyDictionary<string, BatchAppStatus> batchResults,
        IProgress<AppOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<ApplicationModel> installedApps = new List<ApplicationModel>();
        int installedCount = 0;
        int completed = 0;

        foreach (ApplicationModel app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (batchResults.TryGetValue(app.AppId, out BatchAppStatus? batchStatus))
            {
                app.Status = batchStatus.Status;
                if (IsInstalledStatus(batchStatus.Status))
                {
                    app.StatusMessage = Resources.Resources.Status_Installed;
                    if (!string.IsNullOrEmpty(batchStatus.Version))
                    {
                        app.CurrentVersion = batchStatus.Version;
                    }

                    installedCount++;
                    installedApps.Add(app);
                }
                else
                {
                    app.StatusMessage = Resources.Resources.Status_Missing;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Pending;
                app.StatusMessage = Resources.Resources.Status_Missing;
            }

            completed++;
            progress?.Report(new AppOperationProgress(completed, apps.Count, app));
        }

        int updatesCount = installedApps.Count > 0
            ? await CheckBatchUpdatesAsync(installedApps, cancellationToken).ConfigureAwait(false)
            : 0;

        return new AppScanResult(apps.Count, installedCount, updatesCount, WasCancelled: false);
    }

    private async Task<int> CheckBatchUpdatesAsync(
        IReadOnlyList<ApplicationModel> installedApps,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<UpdateInfo> batchUpdates = await _detectionService.GetAvailableUpdatesAsync().ConfigureAwait(false);
            if (batchUpdates.Count > 0)
            {
                int matchedUpdates = ApplyBatchUpdates(installedApps, batchUpdates, cancellationToken);
                _logger.LogInfo($"Batch update check: {batchUpdates.Count} updates found, {matchedUpdates} matched");
                return matchedUpdates;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"Batch update detection failed, falling back to individual checks: {ex.Message}");
        }

        List<ApplicationModel> appsNeedingIndividualCheck = installedApps
            .Where(a => string.IsNullOrEmpty(a.CurrentVersion))
            .ToList();

        if (appsNeedingIndividualCheck.Count == 0)
        {
            return 0;
        }

        AppOperationRunner runner = CreateRunner();
        IReadOnlyList<AppScanItemResult> updateResults = await runner.RunAsync(
            appsNeedingIndividualCheck,
            CheckAppUpdateAsync,
            app => app,
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return updateResults.Count(result => result.HasUpdate);
    }

    private int ApplyBatchUpdates(
        IReadOnlyList<ApplicationModel> installedApps,
        IReadOnlyList<UpdateInfo> batchUpdates,
        CancellationToken cancellationToken)
    {
        Dictionary<string, UpdateInfo> updateLookup = batchUpdates.ToDictionary(
            u => u.Id,
            u => u,
            StringComparer.OrdinalIgnoreCase);
        int matchedUpdates = 0;

        foreach (ApplicationModel app in installedApps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (updateLookup.TryGetValue(app.AppId, out UpdateInfo? directUpdate))
            {
                if (ApplyUpdateInfo(app, directUpdate))
                {
                    matchedUpdates++;
                }

                continue;
            }

            UpdateInfo? matchingUpdate = batchUpdates.FirstOrDefault(u => IsUpdateMatch(app, u));

            if (matchingUpdate != null && ApplyUpdateInfo(app, matchingUpdate))
            {
                matchedUpdates++;
            }
        }

        return matchedUpdates;
    }

    private static bool IsUpdateMatch(ApplicationModel app, UpdateInfo update)
    {
        if (string.Equals(update.Name, app.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(update.Id, app.AppId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedAppId = NormalizePackageLookupKey(app.AppId);
        string normalizedAppName = NormalizePackageLookupKey(app.Name);

        foreach (string? candidate in new[] { update.Id, update.Name }.Where(static c => !string.IsNullOrWhiteSpace(c)))
        {
            string normalizedCandidate = NormalizePackageLookupKey(candidate);

            if (IsContainedPackageKey(normalizedAppId, normalizedCandidate) ||
                IsContainedPackageKey(normalizedAppName, normalizedCandidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsContainedPackageKey(string appKey, string candidateKey)
    {
        return appKey.Length >= 4 &&
               candidateKey.Length >= 4 &&
               (candidateKey.Contains(appKey, StringComparison.OrdinalIgnoreCase) ||
                appKey.Contains(candidateKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePackageLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static bool ApplyUpdateInfo(ApplicationModel app, UpdateInfo updateInfo)
    {
        string currentVersion = !string.IsNullOrEmpty(app.CurrentVersion)
            ? app.CurrentVersion
            : updateInfo.CurrentVersion;

        if (string.IsNullOrEmpty(updateInfo.NewVersion) ||
            string.Equals(currentVersion, updateInfo.NewVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        app.Status = ApplicationStatus.UpdateAvailable;
        app.CurrentVersion = currentVersion;
        app.AvailableVersion = updateInfo.NewVersion;
        app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
        return true;
    }

    private async Task<AppScanItemResult> CheckAppUpdateAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UpdateCheckResult updateResult = await _powerShellBridge.CheckApplicationUpdateAsync(app).ConfigureAwait(false);
        if (updateResult.HasUpdate)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.CurrentVersion = updateResult.CurrentVersion;
            app.AvailableVersion = updateResult.AvailableVersion;
            app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
            return new AppScanItemResult(IsInstalled: true, HasUpdate: true);
        }

        app.CurrentVersion = updateResult.CurrentVersion;
        return new AppScanItemResult(IsInstalled: true, HasUpdate: false);
    }

    private async Task<AppScanItemResult> ScanApplicationAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplicationStatus status = await _powerShellBridge.GetApplicationStatusAsync(app.AppId).ConfigureAwait(false);
        if (IsInstalledStatus(status))
        {
            app.Status = status;

            UpdateCheckResult updateResult = await _powerShellBridge.CheckApplicationUpdateAsync(app).ConfigureAwait(false);
            if (updateResult.HasUpdate)
            {
                app.Status = ApplicationStatus.UpdateAvailable;
                app.CurrentVersion = updateResult.CurrentVersion;
                app.AvailableVersion = updateResult.AvailableVersion;
                app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
                return new AppScanItemResult(IsInstalled: true, HasUpdate: true);
            }

            app.CurrentVersion = updateResult.CurrentVersion;
            app.StatusMessage = Resources.Resources.Status_Installed;
            return new AppScanItemResult(IsInstalled: true, HasUpdate: false);
        }

        app.Status = status;
        app.StatusMessage = Resources.Resources.Status_Missing;
        return new AppScanItemResult(IsInstalled: false, HasUpdate: false);
    }

    private AppOperationRunner CreateRunner()
    {
        int maxParallelScans = Math.Clamp(_settingsService.LoadSettings().MaxParallelScans, 1, 20);
        return new AppOperationRunner(maxParallelScans, _loggerFactory);
    }

    private static AppScanResult BuildResult(
        int total,
        IReadOnlyList<AppScanItemResult> itemResults,
        bool WasCancelled)
    {
        return new AppScanResult(
            total,
            itemResults.Count(result => result.IsInstalled),
            itemResults.Count(result => result.HasUpdate),
            WasCancelled);
    }

    private static AppScanResult BuildResultFromCurrentState(
        IReadOnlyList<ApplicationModel> apps,
        bool WasCancelled)
    {
        return new AppScanResult(
            apps.Count,
            apps.Count(app => IsInstalledStatus(app.Status) || app.Status == ApplicationStatus.UpdateAvailable),
            apps.Count(app => app.Status == ApplicationStatus.UpdateAvailable),
            WasCancelled);
    }

    private static bool IsInstalledStatus(ApplicationStatus status)
    {
        return status == ApplicationStatus.Installed ||
               status == ApplicationStatus.AlreadyInstalled;
    }

    private sealed record AppScanItemResult(bool IsInstalled, bool HasUpdate);
}
