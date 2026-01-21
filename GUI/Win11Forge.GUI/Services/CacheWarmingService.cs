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

using System.Diagnostics;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Background service that pre-warms the application detection cache on startup
/// and periodically refreshes it in the background.
///
/// This eliminates the initial delay when users first browse applications.
/// </summary>
public class CacheWarmingService : IDisposable
{
    private readonly IApplicationDetectionService _detectionService;
    private readonly CancellationTokenSource _cts = new();
    private Task? _warmingTask;
    private bool _disposed;

    /// <summary>
    /// Interval between background cache refreshes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Event raised when cache warming starts.
    /// </summary>
    public event EventHandler? WarmingStarted;

    /// <summary>
    /// Event raised when cache warming completes.
    /// </summary>
    public event EventHandler<CacheWarmingCompletedEventArgs>? WarmingCompleted;

    public CacheWarmingService(IApplicationDetectionService detectionService)
    {
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
    }

    /// <summary>
    /// Starts the cache warming process.
    /// Call this during application startup.
    /// </summary>
    public void Start()
    {
        if (_warmingTask != null) return;

        _warmingTask = Task.Run(async () =>
        {
            try
            {
                await WarmCacheLoopAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Cache warming service stopped.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cache warming service error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Stops the cache warming service.
    /// </summary>
    public void Stop()
    {
        _cts.Cancel();
    }

    /// <summary>
    /// Main loop that warms the cache initially and then periodically refreshes it.
    /// </summary>
    private async Task WarmCacheLoopAsync(CancellationToken cancellationToken)
    {
        // Initial warm-up
        Debug.WriteLine("Starting initial cache warming...");
        WarmingStarted?.Invoke(this, EventArgs.Empty);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _detectionService.WarmCacheAsync();
            stopwatch.Stop();

            var stats = _detectionService.GetCacheStatistics();
            Debug.WriteLine($"Initial cache warming completed: {stats.PackageCount} packages in {stopwatch.ElapsedMilliseconds}ms");

            WarmingCompleted?.Invoke(this, new CacheWarmingCompletedEventArgs
            {
                Success = true,
                PackageCount = stats.PackageCount,
                Duration = stopwatch.Elapsed,
                IsInitialWarming = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initial cache warming failed: {ex.Message}");

            WarmingCompleted?.Invoke(this, new CacheWarmingCompletedEventArgs
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                IsInitialWarming = true
            });
        }

        // Periodic refresh loop
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                Debug.WriteLine("Starting periodic cache refresh...");
                stopwatch.Restart();

                // Force refresh to get latest data
                await _detectionService.GetInstalledPackagesAsync(forceRefresh: true);
                stopwatch.Stop();

                var stats = _detectionService.GetCacheStatistics();
                Debug.WriteLine($"Periodic cache refresh completed: {stats.PackageCount} packages in {stopwatch.ElapsedMilliseconds}ms");

                WarmingCompleted?.Invoke(this, new CacheWarmingCompletedEventArgs
                {
                    Success = true,
                    PackageCount = stats.PackageCount,
                    Duration = stopwatch.Elapsed,
                    IsInitialWarming = false
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Periodic cache refresh failed: {ex.Message}");
                // Continue loop, will retry after interval
            }
        }
    }

    /// <summary>
    /// Forces an immediate cache refresh.
    /// </summary>
    public async Task ForceRefreshAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        WarmingStarted?.Invoke(this, EventArgs.Empty);

        try
        {
            await _detectionService.GetInstalledPackagesAsync(forceRefresh: true);
            stopwatch.Stop();

            var stats = _detectionService.GetCacheStatistics();

            WarmingCompleted?.Invoke(this, new CacheWarmingCompletedEventArgs
            {
                Success = true,
                PackageCount = stats.PackageCount,
                Duration = stopwatch.Elapsed,
                IsInitialWarming = false
            });
        }
        catch (Exception ex)
        {
            WarmingCompleted?.Invoke(this, new CacheWarmingCompletedEventArgs
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                IsInitialWarming = false
            });
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cts.Cancel();
        _cts.Dispose();
        _warmingTask?.Wait(TimeSpan.FromSeconds(2));
        _disposed = true;
    }
}

/// <summary>
/// Event args for cache warming completion.
/// </summary>
public class CacheWarmingCompletedEventArgs : EventArgs
{
    /// <summary>Whether the warming completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Number of packages detected.</summary>
    public int PackageCount { get; init; }

    /// <summary>Time taken for warming.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether this was the initial warming on startup.</summary>
    public bool IsInitialWarming { get; init; }

    /// <summary>Error message if warming failed.</summary>
    public string? ErrorMessage { get; init; }
}
