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
    /// exist in the catalog require a frozen installation definition. Missing
    /// definitions stop recovery so no unresolved action is silently discarded.
    ///
    /// The new batch creates its own checkpoint via the coordinator; the caller is
    /// responsible for deleting the original checkpoint after the resumed operation
    /// returns successfully. Persistence errors leave the original available for retry.
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
            if (checkpoint.OperationKind == BatchOperationKind.Install &&
                checkpoint.Options.Definitions?.TryGetValue(id, out System.Text.Json.JsonElement definition) == true)
            {
                apps.Add(new ApplicationModel
                {
                    AppId = id,
                    Name = definition.GetProperty("Name").GetString() ?? id,
                    FrozenDefinitionJson = definition.GetRawText()
                });
                continue;
            }
            if (byId.TryGetValue(id, out ApplicationModel? app))
            {
                apps.Add(app);
            }
            else
            {
                throw new InvalidOperationException(PreflightText("MissingDefinition") + " " + id);
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
