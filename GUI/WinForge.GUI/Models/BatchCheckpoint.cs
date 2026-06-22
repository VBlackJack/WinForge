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

namespace WinForge.GUI.Models;

/// <summary>
/// Identifies the kind of batch operation captured in a checkpoint.
/// </summary>
public enum BatchOperationKind
{
    Install,
    Update,
    Uninstall
}

/// <summary>
/// Lifecycle state of a batch checkpoint.
/// A file left in <see cref="InProgress"/> at startup signals an interrupted batch.
/// </summary>
public enum BatchState
{
    InProgress,
    Completed
}

/// <summary>
/// Outcome of a single application within a batch operation.
/// </summary>
public enum BatchItemOutcome
{
    Installed,
    AlreadyInstalled,
    Updated,
    Uninstalled,
    Failed,
    Skipped
}

/// <summary>
/// Snapshot of the options used to launch a batch operation.
/// Persisted with the checkpoint so a resumed batch reuses the original parameters.
/// </summary>
/// <param name="ForceUpdate">Whether the original batch requested forced updates.</param>
public sealed record BatchOptions(bool ForceUpdate);

/// <summary>
/// One application entry already processed during the batch.
/// </summary>
/// <param name="AppId">Application identifier matching <see cref="ApplicationModel.AppId"/>.</param>
/// <param name="Outcome">Final outcome reported by the coordinator.</param>
/// <param name="CompletedAt">UTC timestamp when the item finished.</param>
public sealed record BatchCompletedItem(
    string AppId,
    BatchItemOutcome Outcome,
    DateTimeOffset CompletedAt);

/// <summary>
/// Per-batch checkpoint persisted under
/// <c>%LocalAppData%\WinForge\state\batch-{BatchId}.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file is written atomically after each item completion. A process kill leaves the
/// file with <see cref="State"/> still set to <see cref="BatchState.InProgress"/>; the next
/// startup detects this and offers the user to resume, discard, or postpone the decision.
/// </para>
/// <para>
/// <see cref="SchemaVersion"/> is incremented on any breaking change. A reader that
/// encounters an unknown version logs a warning and skips the file (the user keeps the
/// ability to launch a fresh batch; only the resume safety net is lost for that file).
/// </para>
/// </remarks>
public sealed record BatchCheckpoint(
    int SchemaVersion,
    Guid BatchId,
    BatchOperationKind OperationKind,
    BatchState State,
    DateTimeOffset StartedAt,
    DateTimeOffset LastCheckpointAt,
    IReadOnlyList<string> Plan,
    IReadOnlyList<BatchCompletedItem> Completed,
    BatchOptions Options)
{
    /// <summary>
    /// Current schema version emitted by this build. Bump on breaking changes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Returns the AppIds in <see cref="Plan"/> that have not been recorded as completed yet.
    /// Order matches the original <see cref="Plan"/> ordering.
    /// </summary>
    public IReadOnlyList<string> GetRemainingAppIds()
    {
        if (Completed.Count == 0)
        {
            return Plan;
        }

        HashSet<string> done = new HashSet<string>(Completed.Select(c => c.AppId), StringComparer.OrdinalIgnoreCase);
        return Plan.Where(id => !done.Contains(id)).ToList();
    }
}
