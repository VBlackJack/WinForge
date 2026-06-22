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

namespace Win11Forge.GUI.Services.Coordinators;

/// <summary>
/// Coordinates bounded parallel installation of applications.
/// </summary>
public interface IAppInstallationCoordinator
{
    /// <summary>
    /// Installs the supplied applications and returns final aggregate counters.
    /// </summary>
    /// <param name="applications">Applications to install.</param>
    /// <param name="options">Installation options.</param>
    /// <param name="progress">Optional per-application completion progress.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Final installation counters.</returns>
    Task<AppInstallationResult> InstallAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        AppInstallationOptions options,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
