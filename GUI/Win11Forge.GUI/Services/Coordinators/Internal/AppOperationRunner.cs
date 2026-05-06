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

namespace Win11Forge.GUI.Services.Coordinators.Internal;

/// <summary>
/// Runs bounded parallel application operations and reports generic completion progress.
/// </summary>
internal sealed class AppOperationRunner
{
    private readonly int _maxParallelism;

    public AppOperationRunner(int maxParallelism)
    {
        if (maxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelism), "Parallelism must be at least 1.");
        }

        _maxParallelism = maxParallelism;
    }

    public async Task<IReadOnlyList<TResult>> RunAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, CancellationToken, Task<TResult>> operation,
        Func<TItem, ApplicationModel?> currentSelector,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(currentSelector);

        if (items.Count == 0)
        {
            return [];
        }

        using var semaphore = new SemaphoreSlim(_maxParallelism);
        var results = new TResult[items.Count];
        var completed = 0;

        var tasks = items.Select(async (item, index) =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[index] = await operation(item, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                var currentCompleted = Interlocked.Increment(ref completed);
                progress?.Report(new AppOperationProgress(
                    currentCompleted,
                    items.Count,
                    currentSelector(item)));
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }
}
