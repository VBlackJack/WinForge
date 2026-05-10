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

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services.Resume;

/// <summary>
/// File-backed implementation of <see cref="IBatchResumeService"/>.
/// </summary>
/// <remarks>
/// Persistence pattern is intentionally aligned with <see cref="AppSettingsService"/>:
/// root under <c>%LocalAppData%\Win11Forge\state\</c>, automatic fallback to the
/// system temp directory when LocalAppData is inaccessible, and shared
/// <see cref="JsonSerializerOptions"/> with camelCase naming.
///
/// Writes go through a temp file that is then renamed via
/// <see cref="File.Move(string, string, bool)"/>; this guarantees a reader either
/// observes the previous coherent file or the new coherent file, never a partial
/// half-written one.
///
/// Concurrency is bounded by a single per-instance <see cref="SemaphoreSlim"/>
/// covering all mutating operations. Reads are lock-free; the worst case is a
/// stale snapshot which is acceptable because the service is the source of truth
/// at process startup, not for live UI state.
/// </remarks>
public sealed class BatchResumeService : IBatchResumeService
{
    /// <summary>Default time-to-live before a checkpoint is considered stale.</summary>
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromDays(14);

    private const string FilePrefix = "batch-";
    private const string FileSuffix = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly string _stateDirectory;
    private readonly TimeSpan _staleAfter;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Production constructor used by DI. Resolves <c>%LocalAppData%\Win11Forge\state</c>
    /// with the same fallback chain as <see cref="AppSettingsService"/>.
    /// </summary>
    public BatchResumeService()
        : this(ResolveDefaultStateDirectory(), DefaultStaleAfter, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Test-friendly constructor accepting an arbitrary root directory, custom TTL,
    /// and a clock that can be advanced for stale-detection tests.
    /// </summary>
    public BatchResumeService(string stateDirectory, TimeSpan staleAfter, Func<DateTimeOffset> now)
    {
        _stateDirectory = stateDirectory ?? throw new ArgumentNullException(nameof(stateDirectory));
        _staleAfter = staleAfter;
        _now = now ?? throw new ArgumentNullException(nameof(now));

        EnsureStateDirectoryExists();
    }

    /// <inheritdoc/>
    public async Task<Guid> BeginBatchAsync(
        BatchOperationKind kind,
        IReadOnlyList<string> plan,
        BatchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);

        var batchId = Guid.NewGuid();
        var nowUtc = _now();
        var checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: batchId,
            OperationKind: kind,
            State: BatchState.InProgress,
            StartedAt: nowUtc,
            LastCheckpointAt: nowUtc,
            Plan: plan.ToArray(),
            Completed: Array.Empty<BatchCompletedItem>(),
            Options: options);

        await WriteCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        return batchId;
    }

    /// <inheritdoc/>
    public async Task AppendCompletedAsync(
        Guid batchId,
        string appId,
        BatchItemOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(appId);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await ReadCheckpointAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                Debug.WriteLine($"[BatchResumeService] Cannot append to missing checkpoint {batchId}.");
                return;
            }

            var updated = existing with
            {
                LastCheckpointAt = _now(),
                Completed = existing.Completed
                    .Concat([new BatchCompletedItem(appId, outcome, _now())])
                    .ToArray()
            };

            await WriteCheckpointUnlockedAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task MarkBatchCompletedAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await ReadCheckpointAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                Debug.WriteLine($"[BatchResumeService] Cannot mark missing checkpoint {batchId} completed.");
                return;
            }

            var updated = existing with
            {
                State = BatchState.Completed,
                LastCheckpointAt = _now()
            };

            await WriteCheckpointUnlockedAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BatchCheckpoint>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_stateDirectory))
        {
            return Array.Empty<BatchCheckpoint>();
        }

        var pending = new List<BatchCheckpoint>();
        var threshold = _now() - _staleAfter;

        foreach (var path in Directory.EnumerateFiles(_stateDirectory, FilePrefix + "*" + FileSuffix))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var checkpoint = await ReadCheckpointFromPathAsync(path, cancellationToken).ConfigureAwait(false);
            if (checkpoint == null)
            {
                continue;
            }
            if (checkpoint.State != BatchState.InProgress)
            {
                continue;
            }
            if (checkpoint.LastCheckpointAt < threshold)
            {
                continue;
            }
            pending.Add(checkpoint);
        }

        return pending;
    }

    /// <inheritdoc/>
    public Task<BatchCheckpoint?> LoadCheckpointAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return ReadCheckpointAsync(batchId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteCheckpointAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCheckpointPath(batchId);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BatchResumeService] Failed to delete checkpoint {batchId}: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task PruneStaleAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_stateDirectory))
        {
            return;
        }

        var threshold = _now() - _staleAfter;

        foreach (var path in Directory.EnumerateFiles(_stateDirectory, FilePrefix + "*" + FileSuffix))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var checkpoint = await ReadCheckpointFromPathAsync(path, cancellationToken).ConfigureAwait(false);
                var shouldDelete = checkpoint == null || checkpoint.LastCheckpointAt < threshold;
                if (shouldDelete)
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BatchResumeService] Failed to prune {path}: {ex.Message}");
            }
        }
    }

    private async Task WriteCheckpointAsync(BatchCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteCheckpointUnlockedAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteCheckpointUnlockedAsync(BatchCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var finalPath = GetCheckpointPath(checkpoint.BatchId);
        var tempPath = finalPath + ".tmp";

        try
        {
            EnsureStateDirectoryExists();
            var json = JsonSerializer.Serialize(checkpoint, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BatchResumeService] Failed to write checkpoint {checkpoint.BatchId}: {ex.Message}");
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort temp cleanup; swallow to avoid masking the original failure.
            }
        }
    }

    private async Task<BatchCheckpoint?> ReadCheckpointAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var path = GetCheckpointPath(batchId);
        return await ReadCheckpointFromPathAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BatchCheckpoint?> ReadCheckpointFromPathAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var checkpoint = JsonSerializer.Deserialize<BatchCheckpoint>(json, JsonOptions);
            if (checkpoint == null)
            {
                return null;
            }
            if (checkpoint.SchemaVersion != BatchCheckpoint.CurrentSchemaVersion)
            {
                Debug.WriteLine(
                    $"[BatchResumeService] Skipping checkpoint with schema {checkpoint.SchemaVersion} " +
                    $"(expected {BatchCheckpoint.CurrentSchemaVersion}): {path}");
                return null;
            }
            return checkpoint;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[BatchResumeService] Corrupted checkpoint at {path}: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[BatchResumeService] IO error reading {path}: {ex.Message}");
            return null;
        }
    }

    private string GetCheckpointPath(Guid batchId)
    {
        return Path.Combine(_stateDirectory, FilePrefix + batchId.ToString("D") + FileSuffix);
    }

    private void EnsureStateDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_stateDirectory))
            {
                Directory.CreateDirectory(_stateDirectory);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BatchResumeService] Failed to create state directory '{_stateDirectory}': {ex.Message}");
        }
    }

    private static string ResolveDefaultStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        var win11ForgePath = Path.Combine(localAppData, "Win11Forge");
        try
        {
            if (!Directory.Exists(win11ForgePath))
            {
                Directory.CreateDirectory(win11ForgePath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BatchResumeService] Failed to create Win11Forge dir in AppData: {ex.Message}");
            win11ForgePath = Path.Combine(Path.GetTempPath(), "Win11Forge");
            try
            {
                if (!Directory.Exists(win11ForgePath))
                {
                    Directory.CreateDirectory(win11ForgePath);
                }
            }
            catch (Exception innerEx)
            {
                Debug.WriteLine($"[BatchResumeService] Failed to create fallback Win11Forge dir: {innerEx.Message}");
                win11ForgePath = Path.GetTempPath();
            }
        }

        return Path.Combine(win11ForgePath, "state");
    }
}
