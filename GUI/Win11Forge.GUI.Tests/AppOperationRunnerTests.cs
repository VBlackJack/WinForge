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
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.Services.Coordinators.Internal;

namespace Win11Forge.GUI.Tests;

public class AppOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_ShouldRespectMaxParallelism()
    {
        var runner = new AppOperationRunner(maxParallelism: 2);
        var active = 0;
        var maxObserved = 0;

        await runner.RunAsync(
            Enumerable.Range(1, 8).ToList(),
            async (item, _) =>
            {
                var current = Interlocked.Increment(ref active);
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
        var runner = new AppOperationRunner(maxParallelism: 2);
        using var cts = new CancellationTokenSource();
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
        var runner = new AppOperationRunner(maxParallelism: 2);
        var progress = new RecordingProgress<AppOperationProgress>();
        var apps = new[]
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
        var runner = new AppOperationRunner(maxParallelism: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(
                new[] { 1, 2, 3 },
                Task<int> (item, _) => item == 2
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(item),
                _ => null));
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
