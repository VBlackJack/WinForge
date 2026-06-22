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

using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.Resume;

namespace Win11Forge.GUI.Tests;

public sealed class BatchResumeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private DateTimeOffset _now = DateTimeOffset.Parse("2026-05-10T14:00:00Z").ToUniversalTime();

    public BatchResumeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Win11Forge.Tests.BatchResume", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; transient AV/indexer locks on Windows are tolerated.
        }
    }

    private BatchResumeService CreateService(TimeSpan? staleAfter = null)
    {
        return new BatchResumeService(_tempDir, staleAfter ?? TimeSpan.FromDays(14), () => _now);
    }

    private static IReadOnlyList<string> Plan(params string[] ids) => ids;

    [Fact]
    public async Task BeginBatchAsync_ShouldCreateFileWithCurrentSchemaAndInProgressState()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(
            BatchOperationKind.Install,
            Plan("App.A", "App.B"),
            new BatchOptions(ForceUpdate: false));

        string file = Path.Combine(_tempDir, $"batch-{batchId:D}.json");
        Assert.True(File.Exists(file));

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(loaded);
        Assert.Equal(BatchCheckpoint.CurrentSchemaVersion, loaded!.SchemaVersion);
        Assert.Equal(BatchOperationKind.Install, loaded.OperationKind);
        Assert.Equal(BatchState.InProgress, loaded.State);
        Assert.Equal(2, loaded.Plan.Count);
        Assert.Empty(loaded.Completed);
        Assert.False(loaded.Options.ForceUpdate);
    }

    [Fact]
    public async Task RoundTrip_PreservesPlanAndOptions()
    {
        BatchResumeService service = CreateService();
        IReadOnlyList<string> planIds = Plan("Foo.Bar", "Baz.Qux", "Acme.Widget");
        Guid batchId = await service.BeginBatchAsync(
            BatchOperationKind.Update,
            planIds,
            new BatchOptions(ForceUpdate: true));

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(loaded);
        Assert.Equal(planIds, loaded!.Plan);
        Assert.Equal(BatchOperationKind.Update, loaded.OperationKind);
        Assert.True(loaded.Options.ForceUpdate);
    }

    [Fact]
    public async Task AppendCompletedAsync_ShouldAppendItemAndAdvanceTimestamp()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(
            BatchOperationKind.Install,
            Plan("App.A", "App.B"),
            new BatchOptions(ForceUpdate: false));

        DateTimeOffset initialTimestamp = (await service.LoadCheckpointAsync(batchId))!.LastCheckpointAt;
        _now = _now.AddSeconds(5);
        await service.AppendCompletedAsync(batchId, "App.A", BatchItemOutcome.Installed);

        BatchCheckpoint? updated = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(updated);
        Assert.Single(updated!.Completed);
        Assert.Equal("App.A", updated.Completed[0].AppId);
        Assert.Equal(BatchItemOutcome.Installed, updated.Completed[0].Outcome);
        Assert.True(updated.LastCheckpointAt > initialTimestamp);
    }

    [Fact]
    public async Task AppendCompletedAsync_ShouldBeThreadSafeUnderConcurrency()
    {
        BatchResumeService service = CreateService();
        string[] planIds = Enumerable.Range(0, 50).Select(i => $"App.{i:D2}").ToArray();
        Guid batchId = await service.BeginBatchAsync(
            BatchOperationKind.Install,
            planIds,
            new BatchOptions(ForceUpdate: false));

        await Parallel.ForEachAsync(
            planIds,
            async (id, ct) => await service.AppendCompletedAsync(batchId, id, BatchItemOutcome.Installed, ct));

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(loaded);
        Assert.Equal(planIds.Length, loaded!.Completed.Count);
        Assert.Equal(planIds.OrderBy(x => x), loaded.Completed.Select(c => c.AppId).OrderBy(x => x));
    }

    [Fact]
    public async Task MarkBatchCompletedAsync_ShouldTransitionToCompleted()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(
            BatchOperationKind.Uninstall,
            Plan("App.A"),
            new BatchOptions(ForceUpdate: false));

        await service.MarkBatchCompletedAsync(batchId);

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(loaded);
        Assert.Equal(BatchState.Completed, loaded!.State);
    }

    [Fact]
    public async Task ListPendingAsync_ShouldReturnOnlyInProgress()
    {
        BatchResumeService service = CreateService();
        Guid pending = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Pending.App"), new BatchOptions(false));
        Guid completed = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Done.App"), new BatchOptions(false));
        await service.MarkBatchCompletedAsync(completed);

        IReadOnlyList<BatchCheckpoint> list = await service.ListPendingAsync();
        Assert.Single(list);
        Assert.Equal(pending, list[0].BatchId);
    }

    [Fact]
    public async Task ListPendingAsync_ShouldHideStaleCheckpoints()
    {
        BatchResumeService service = CreateService(staleAfter: TimeSpan.FromDays(7));
        Guid fresh = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Fresh.App"), new BatchOptions(false));

        // Make the next batch's LastCheckpointAt look ancient.
        _now = _now.AddDays(-30);
        Guid stale = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Stale.App"), new BatchOptions(false));
        _now = _now.AddDays(30);

        IReadOnlyList<BatchCheckpoint> list = await service.ListPendingAsync();
        Assert.Single(list);
        Assert.Equal(fresh, list[0].BatchId);
        Assert.NotEqual(stale, list[0].BatchId);
    }

    [Fact]
    public async Task ListPendingAsync_ShouldSkipSchemaMismatchedFiles()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("App.A"), new BatchOptions(false));

        // Corrupt the schema version in the persisted file.
        string path = Path.Combine(_tempDir, $"batch-{batchId:D}.json");
        string json = await File.ReadAllTextAsync(path);
        string bumped = json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999");
        await File.WriteAllTextAsync(path, bumped);

        IReadOnlyList<BatchCheckpoint> list = await service.ListPendingAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task LoadCheckpointAsync_ShouldReturnNullOnCorruptJson()
    {
        BatchResumeService service = CreateService();
        Guid batchId = Guid.NewGuid();
        string path = Path.Combine(_tempDir, $"batch-{batchId:D}.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadCheckpointAsync_ShouldReturnNullWhenMissing()
    {
        BatchResumeService service = CreateService();
        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(Guid.NewGuid());
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteCheckpointAsync_ShouldRemoveFile()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("App.A"), new BatchOptions(false));
        Assert.NotNull(await service.LoadCheckpointAsync(batchId));

        await service.DeleteCheckpointAsync(batchId);
        Assert.Null(await service.LoadCheckpointAsync(batchId));
    }

    [Fact]
    public async Task DeleteCheckpointAsync_ShouldNotThrowWhenFileMissing()
    {
        BatchResumeService service = CreateService();
        await service.DeleteCheckpointAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task PruneStaleAsync_ShouldRemoveOldCheckpoints()
    {
        BatchResumeService service = CreateService(staleAfter: TimeSpan.FromDays(7));

        _now = _now.AddDays(-30);
        Guid stale = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Old.App"), new BatchOptions(false));
        _now = _now.AddDays(30);
        Guid fresh = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("Fresh.App"), new BatchOptions(false));

        await service.PruneStaleAsync();

        Assert.Null(await service.LoadCheckpointAsync(stale));
        Assert.NotNull(await service.LoadCheckpointAsync(fresh));
    }

    [Fact]
    public async Task PruneStaleAsync_ShouldRemoveCorruptedFiles()
    {
        BatchResumeService service = CreateService();
        Guid corruptId = Guid.NewGuid();
        string path = Path.Combine(_tempDir, $"batch-{corruptId:D}.json");
        await File.WriteAllTextAsync(path, "{ broken");

        await service.PruneStaleAsync();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task PruneStaleAsync_OnEmptyDirectory_DoesNotThrow()
    {
        BatchResumeService service = CreateService();
        await service.PruneStaleAsync();
    }

    [Fact]
    public async Task BatchCheckpoint_GetRemainingAppIds_ShouldExcludeCompleted()
    {
        BatchResumeService service = CreateService();
        IReadOnlyList<string> planIds = Plan("App.A", "App.B", "App.C");
        Guid batchId = await service.BeginBatchAsync(BatchOperationKind.Install, planIds, new BatchOptions(false));

        await service.AppendCompletedAsync(batchId, "App.A", BatchItemOutcome.Installed);
        await service.AppendCompletedAsync(batchId, "App.C", BatchItemOutcome.Failed);

        BatchCheckpoint? loaded = await service.LoadCheckpointAsync(batchId);
        Assert.NotNull(loaded);
        IReadOnlyList<string> remaining = loaded!.GetRemainingAppIds();
        Assert.Single(remaining);
        Assert.Equal("App.B", remaining[0]);
    }

    [Fact]
    public async Task SerializedFile_UsesCurrentSchemaConstant()
    {
        BatchResumeService service = CreateService();
        Guid batchId = await service.BeginBatchAsync(BatchOperationKind.Install, Plan("App.A"), new BatchOptions(false));

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, $"batch-{batchId:D}.json"));
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal(BatchCheckpoint.CurrentSchemaVersion, doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }
}
