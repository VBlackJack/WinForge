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

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates application update discovery and application update workflows.
/// </summary>
public interface IAppUpdateCoordinator
{
    /// <summary>
    /// Scans installed applications for available updates.
    /// </summary>
    Task<AppUpdateScanResult> ScanForUpdatesAsync(
        IReadOnlyCollection<ApplicationModel> installedApps,
        bool forceRefresh = false,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates applications sequentially in input order.
    /// </summary>
    Task<AppUpdateResult> UpdateAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
