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
/// Long-lived update scan state publisher.
/// </summary>
public sealed class UpdateScanStateService : IUpdateScanStateService
{
    /// <inheritdoc/>
    public event EventHandler<UpdateScanCompletedEventArgs>? UpdateScanCompleted;

    /// <inheritdoc/>
    public void PublishUpdateScanCompleted(
        IReadOnlyCollection<ApplicationModel> applications,
        int updatesAvailableCount)
    {
        ArgumentNullException.ThrowIfNull(applications);
        UpdateScanCompleted?.Invoke(
            this,
            new UpdateScanCompletedEventArgs(applications, updatesAvailableCount));
    }
}
