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

using System.Collections.Concurrent;
using WinForge.GUI.Models;
using WinForge.GUI.Services.Coordinators;
using WinForge.GUI.Services.Coordinators.Internal;

namespace WinForge.GUI.Tests;

public class AppOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_ShouldRespectMaxParallelism()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 2);
        int active = 0;
        int maxObserved = 0;

        await runner.RunAsync(
            Enumerable.Range(1, 8).ToList(),
            async (item, _) =>
            {
                int current = Interlocked.Increment(ref active);
                maxObserved = Math.Max(maxObserved, current);
                await Task.Delay(25);
                Interlocked.Decrement(ref active);
                return item;
            },
            _ => null);

        Assert.True(maxObserved <= 2, $"Expected max parallelism <= 2, observed {maxObserved}.");
    }

    [Fact]
    public async Task RunAsync_ShouldPropagateCancellation()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 2);
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                new[] { 1, 2, 3 },
                (item, token) => Task.FromResult(item),
                _ => null,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RunAsync_ShouldReportProgressForEachCompletedItem()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 2);
        RecordingProgress<AppOperationProgress> progress = new RecordingProgress<AppOperationProgress>();
        ApplicationModel[] apps = new[]
        {
            new ApplicationModel { AppId = "App1", Name = "Application 1" },
            new ApplicationModel { AppId = "App2", Name = "Application 2" },
            new ApplicationModel { AppId = "App3", Name = "Application 3" }
        };

        await runner.RunAsync(
            apps,
            (app, _) => Task.FromResult(app.AppId),
            app => app,
            progress);

        Assert.Equal(3, progress.Reports.Count);
        Assert.Equal(new[] { 1, 2, 3 }, progress.Reports.Select(report => report.Completed));
        Assert.All(progress.Reports, report => Assert.Equal(3, report.Total));
        Assert.All(progress.Reports, report => Assert.NotNull(report.Current));
    }

    [Fact]
    public async Task RunAsync_ShouldPropagateOperationExceptions()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(
                new[] { 1, 2, 3 },
                Task<int> (item, _) => item == 2
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(item),
                _ => null));
    }

    [Fact]
    public async Task RunAsync_ShouldInvokeOnItemCompletedCallbackPerItem()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 4);
        ConcurrentBag<(int item, int result)> observed = new System.Collections.Concurrent.ConcurrentBag<(int item, int result)>();

        await runner.RunAsync(
            Enumerable.Range(1, 6).ToList(),
            (item, _) => Task.FromResult(item * 10),
            _ => null,
            progress: null,
            cancellationToken: default,
            onItemCompleted: (item, result, _) =>
            {
                observed.Add((item, result));
                return Task.CompletedTask;
            });

        Assert.Equal(6, observed.Count);
        Assert.Equal(
            Enumerable.Range(1, 6).Select(i => (i, i * 10)).OrderBy(x => x.i),
            observed.OrderBy(x => x.item));
    }

    [Fact]
    public async Task RunAsync_WhenOnItemCompletedThrows_ShouldNotAbortBatch()
    {
        AppOperationRunner runner = new AppOperationRunner(maxParallelism: 2);
        int processed = 0;

        IReadOnlyList<int> results = await runner.RunAsync(
            Enumerable.Range(1, 5).ToList(),
            (item, _) =>
            {
                Interlocked.Increment(ref processed);
                return Task.FromResult(item);
            },
            _ => null,
            progress: null,
            cancellationToken: default,
            onItemCompleted: (item, _, _) =>
            {
                if (item == 3)
                {
                    throw new InvalidOperationException("simulated checkpoint write failure");
                }
                return Task.CompletedTask;
            });

        Assert.Equal(5, processed);
        Assert.Equal(5, results.Count);
        Assert.Equal(Enumerable.Range(1, 5), results);
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = [];

        public void Report(T value)
        {
            Reports.Add(value);
        }
    }
}
