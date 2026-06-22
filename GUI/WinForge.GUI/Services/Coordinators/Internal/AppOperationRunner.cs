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
using WinForge.GUI.Services;

namespace WinForge.GUI.Services.Coordinators.Internal;

/// <summary>
/// Runs bounded parallel application operations and reports generic completion progress.
/// </summary>
internal sealed class AppOperationRunner
{
    private readonly int _maxParallelism;
    private readonly ILoggingService _logger;

    public AppOperationRunner(int maxParallelism, ILoggerFactory? loggerFactory = null)
    {
        if (maxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelism), "Parallelism must be at least 1.");
        }

        _maxParallelism = maxParallelism;
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<AppOperationRunner>();
    }

    public async Task<IReadOnlyList<TResult>> RunAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, CancellationToken, Task<TResult>> operation,
        Func<TItem, ApplicationModel?> currentSelector,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Func<TItem, TResult, CancellationToken, Task>? onItemCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(currentSelector);

        if (items.Count == 0)
        {
            return [];
        }

        using SemaphoreSlim semaphore = new SemaphoreSlim(_maxParallelism);
        TResult[] results = new TResult[items.Count];
        int completed = 0;

        IEnumerable<Task> tasks = items.Select(async (item, index) =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            TResult? result = default;
            bool operationCompleted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await operation(item, cancellationToken).ConfigureAwait(false);
                results[index] = result;
                operationCompleted = true;
            }
            finally
            {
                int currentCompleted = Interlocked.Increment(ref completed);

                // Per-item completion hook. Kept generic: the runner has no resume-specific
                // logic beyond invoking the callback and isolating its failures so that a
                // checkpoint write error does not abort the rest of the batch.
                if (operationCompleted && onItemCompleted != null && result is not null)
                {
                    try
                    {
                        await onItemCompleted(item, result, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation propagates through the outer awaits; the callback
                        // simply observed the same token.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[AppOperationRunner] onItemCompleted callback failed: {ex.Message}");
                    }
                }

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
