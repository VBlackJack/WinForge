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

#nullable enable

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services.Resume;

/// <summary>
/// Persists per-batch checkpoints so an interrupted Install / Update / Uninstall
/// operation can be resumed after a process kill, BSOD, or forced reboot.
/// </summary>
/// <remarks>
/// Each checkpoint lives at <c>%LocalAppData%\Win11Forge\state\batch-{BatchId}.json</c>.
/// Files in <see cref="BatchState.InProgress"/> at startup signal an interrupted batch.
/// Files older than the configured TTL are silently removed at startup.
/// </remarks>
public interface IBatchResumeService
{
    /// <summary>
    /// Creates a new checkpoint in <see cref="BatchState.InProgress"/> and returns its identifier.
    /// </summary>
    Task<Guid> BeginBatchAsync(
        BatchOperationKind kind,
        IReadOnlyList<string> plan,
        BatchOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a single application's outcome and updates <see cref="BatchCheckpoint.LastCheckpointAt"/>.
    /// Safe to call concurrently from a parallel runner.
    /// </summary>
    Task AppendCompletedAsync(
        Guid batchId,
        string appId,
        BatchItemOutcome outcome,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the checkpoint to <see cref="BatchState.Completed"/>.
    /// Called when a batch ends gracefully (success or user cancellation).
    /// </summary>
    Task MarkBatchCompletedAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns checkpoints in <see cref="BatchState.InProgress"/> that are still within the TTL.
    /// Schema mismatches and corrupted files are skipped silently.
    /// Order is unspecified; callers that need the most recent should sort by
    /// <see cref="BatchCheckpoint.LastCheckpointAt"/> themselves.
    /// </summary>
    Task<IReadOnlyList<BatchCheckpoint>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a single checkpoint by id. Returns null when the file does not exist,
    /// the schema is unknown, or the JSON is corrupted.
    /// </summary>
    Task<BatchCheckpoint?> LoadCheckpointAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the checkpoint file for the given batch. No-op when the file is missing.
    /// </summary>
    Task DeleteCheckpointAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes checkpoints older than the configured TTL and any file with an unknown
    /// <see cref="BatchCheckpoint.SchemaVersion"/>. Designed to be invoked fire-and-forget at
    /// application startup. Failures are logged via <see cref="System.Diagnostics.Debug.WriteLine"/>
    /// and never thrown.
    /// </summary>
    Task PruneStaleAsync(CancellationToken cancellationToken = default);
}
