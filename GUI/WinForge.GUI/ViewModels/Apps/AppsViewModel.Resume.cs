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
using WinForge.GUI.Services.Coordinators;

namespace WinForge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Resumes an interrupted batch operation by re-running the coordinator on the
    /// applications that were not yet recorded as completed in the checkpoint.
    /// </summary>
    /// <remarks>
    /// The catalog is loaded if it has not been initialised yet. Apps that no longer
    /// exist in the catalog (because the user edited <c>applications.json</c> between
    /// the crash and the resume) are silently skipped - the resumed batch only
    /// replays items that are still resolvable.
    ///
    /// The new batch creates its own checkpoint via the coordinator; the caller is
    /// responsible for deleting the original checkpoint once the resume has been
    /// initiated, otherwise it would be re-offered on the next launch.
    /// </remarks>
    public async Task ResumeBatchAsync(BatchCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        if (_allApplications.Count == 0)
        {
            await InitializeAsync().ConfigureAwait(false);
        }

        IReadOnlyList<string> remainingIds = checkpoint.GetRemainingAppIds();
        if (remainingIds.Count == 0)
        {
            return;
        }

        Dictionary<string, ApplicationModel> byId = _allApplications.ToDictionary(a => a.AppId, StringComparer.OrdinalIgnoreCase);
        List<ApplicationModel> apps = new List<ApplicationModel>(remainingIds.Count);
        foreach (string id in remainingIds)
        {
            if (byId.TryGetValue(id, out ApplicationModel? app))
            {
                apps.Add(app);
            }
            else
            {
                _logger.LogDebug($"Resume skipping unknown app id '{id}' (not in catalog).");
            }
        }

        if (apps.Count == 0)
        {
            return;
        }

        switch (checkpoint.OperationKind)
        {
            case BatchOperationKind.Install:
                AppInstallationResult installResult = await _installationCoordinator.InstallAsync(
                    apps,
                    new AppInstallationOptions(ForceUpdate: checkpoint.Options.ForceUpdate),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                InstalledCount += installResult.InstalledCount;
                break;

            case BatchOperationKind.Update:
                await _updateCoordinator.UpdateAsync(
                    apps,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            case BatchOperationKind.Uninstall:
                await _uninstallCoordinator.UninstallAsync(
                    apps,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoint),
                    checkpoint.OperationKind,
                    "Unknown BatchOperationKind on resume.");
        }
    }
}
