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
/// Estimates remaining time for batch operations based on elapsed time and progress.
/// </summary>
public class ProgressEstimator
{
    private readonly object _syncRoot = new();
    private readonly Stopwatch _stopwatch = new();
    private int _totalItems;
    private int _completedItems;
    private readonly Queue<double> _recentRates = new();
    private const int MaxRateSamples = 5;

    /// <summary>
    /// Starts a new progress tracking session.
    /// </summary>
    public void Start(int totalItems)
    {
        lock (_syncRoot)
        {
            _totalItems = totalItems;
            _completedItems = 0;
            _recentRates.Clear();
            _stopwatch.Restart();
        }
    }

    /// <summary>
    /// Updates progress with a completed item.
    /// </summary>
    public void UpdateProgress(int completedItems)
    {
        lock (_syncRoot)
        {
            var previousCompleted = _completedItems;
            _completedItems = completedItems;

            // Calculate rate for this batch
            if (_completedItems > previousCompleted && _stopwatch.Elapsed.TotalSeconds > 0)
            {
                var rate = _completedItems / _stopwatch.Elapsed.TotalSeconds;
                _recentRates.Enqueue(rate);

                // Keep only recent samples for better accuracy
                while (_recentRates.Count > MaxRateSamples)
                {
                    _recentRates.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Gets the estimated time remaining.
    /// Returns null if not enough data to estimate.
    /// </summary>
    public TimeSpan? GetEstimatedTimeRemaining()
    {
        lock (_syncRoot)
        {
            if (_completedItems == 0 || _recentRates.Count == 0)
            {
                return null;
            }

            var remainingItems = _totalItems - _completedItems;
            if (remainingItems <= 0)
            {
                return TimeSpan.Zero;
            }

            // Use average of recent rates for smoother estimate
            var avgRate = _recentRates.Average();
            if (avgRate <= 0)
            {
                return null;
            }

            var estimatedSeconds = remainingItems / avgRate;
            return TimeSpan.FromSeconds(estimatedSeconds);
        }
    }

    /// <summary>
    /// Gets a formatted string for the estimated time remaining.
    /// </summary>
    public string GetFormattedTimeRemaining()
    {
        var remaining = GetEstimatedTimeRemaining();

        if (remaining == null)
        {
            return Resources.Resources.Progress_Calculating;
        }

        if (remaining.Value.TotalMinutes >= 1)
        {
            return string.Format(
                Resources.Resources.Progress_TimeRemaining,
                string.Format(Resources.Resources.Progress_TimeMinutes, (int)remaining.Value.TotalMinutes));
        }

        return string.Format(
            Resources.Resources.Progress_TimeRemaining,
            string.Format(Resources.Resources.Progress_TimeSeconds, (int)remaining.Value.TotalSeconds));
    }

    /// <summary>
    /// Stops tracking and returns total elapsed time.
    /// </summary>
    public TimeSpan Stop()
    {
        lock (_syncRoot)
        {
            _stopwatch.Stop();
            return _stopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Gets the elapsed time.
    /// </summary>
    public TimeSpan Elapsed
    {
        get
        {
            lock (_syncRoot)
            {
                return _stopwatch.Elapsed;
            }
        }
    }

    /// <summary>
    /// Gets a formatted string for the elapsed time.
    /// </summary>
    public string GetFormattedElapsedTime()
    {
        TimeSpan elapsed;
        lock (_syncRoot)
        {
            elapsed = _stopwatch.Elapsed;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        return $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }
}
