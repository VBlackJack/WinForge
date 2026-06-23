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

namespace WinForge.GUI.Services;

/// <summary>
/// Publishes update scan results to long-lived consumers without depending on view lifecycle callbacks.
/// </summary>
public interface IUpdateScanStateService
{
    /// <summary>
    /// Raised after an update scan refreshes application statuses.
    /// </summary>
    event EventHandler<UpdateScanCompletedEventArgs>? UpdateScanCompleted;

    /// <summary>
    /// Publishes refreshed application statuses.
    /// </summary>
    void PublishUpdateScanCompleted(
        IReadOnlyCollection<ApplicationModel> applications,
        int updatesAvailableCount);
}

/// <summary>
/// Event data for an update scan completion.
/// </summary>
public sealed class UpdateScanCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of UpdateScanCompletedEventArgs.
    /// </summary>
    public UpdateScanCompletedEventArgs(
        IReadOnlyCollection<ApplicationModel> applications,
        int updatesAvailableCount)
    {
        Applications = applications;
        UpdatesAvailableCount = updatesAvailableCount;
    }

    /// <summary>
    /// Applications whose update status was refreshed by the scan.
    /// </summary>
    public IReadOnlyCollection<ApplicationModel> Applications { get; }

    /// <summary>
    /// Number of applications with updates available after the scan.
    /// </summary>
    public int UpdatesAvailableCount { get; }
}
