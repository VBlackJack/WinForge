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

namespace WinForge.GUI.Services;

/// <summary>
/// Interface for application detection services.
/// Provides fast detection of installed applications and available updates.
/// </summary>
public interface IApplicationDetectionService
{
    /// <summary>
    /// Gets all installed packages using optimized detection methods.
    /// Results are cached for performance.
    /// </summary>
    /// <param name="forceRefresh">If true, bypasses cache and forces fresh detection.</param>
    /// <returns>Batch detection result containing all detected packages.</returns>
    Task<BatchDetectionResult> GetInstalledPackagesAsync(bool forceRefresh = false);

    /// <summary>
    /// Checks if a specific application is installed.
    /// Uses cached data when available for fast lookup.
    /// </summary>
    /// <param name="appId">The application identifier to check.</param>
    /// <returns>Package info if installed, null otherwise.</returns>
    Task<InstalledPackageInfo?> GetPackageInfoAsync(string appId);

    /// <summary>
    /// Gets the installed version of a specific application.
    /// </summary>
    /// <param name="appId">The application identifier.</param>
    /// <returns>Version string if installed, null otherwise.</returns>
    Task<string?> GetInstalledVersionAsync(string appId);

    /// <summary>
    /// Gets all available updates in a single batch operation.
    /// Much faster than checking updates per-application.
    /// </summary>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<UpdateInfo>> GetAvailableUpdatesAsync();

    /// <summary>
    /// Pre-warms the detection cache.
    /// Call this on application startup for faster subsequent operations.
    /// </summary>
    Task WarmCacheAsync();

    /// <summary>
    /// Clears all cached detection data.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets cache statistics for diagnostics.
    /// </summary>
    CacheStatistics GetCacheStatistics();

    /// <summary>
    /// Event raised when cache is refreshed.
    /// </summary>
    event EventHandler<CacheRefreshedEventArgs>? CacheRefreshed;

    /// <summary>
    /// Event raised when cache is invalidated.
    /// </summary>
    event EventHandler<CacheInvalidatedEventArgs>? CacheInvalidated;

    /// <summary>
    /// Invalidates the cache for a specific application after installation/uninstallation.
    /// </summary>
    /// <param name="appId">The application ID that was modified.</param>
    /// <param name="reason">The reason for invalidation.</param>
    void InvalidateCacheForApp(string appId, CacheInvalidationReason reason);

    /// <summary>
    /// Invalidates the entire cache, forcing a refresh on next access.
    /// </summary>
    /// <param name="reason">The reason for invalidation.</param>
    void InvalidateAllCache(CacheInvalidationReason reason);
}

/// <summary>
/// Statistics about the detection cache.
/// </summary>
public class CacheStatistics
{
    /// <summary>Number of cache hits.</summary>
    public int Hits { get; set; }

    /// <summary>Number of cache misses.</summary>
    public int Misses { get; set; }

    /// <summary>Total packages in cache.</summary>
    public int PackageCount { get; set; }

    /// <summary>Age of the cache.</summary>
    public TimeSpan CacheAge { get; set; }

    /// <summary>Last refresh time.</summary>
    public DateTime? LastRefresh { get; set; }

    /// <summary>Average detection time.</summary>
    public TimeSpan AverageDetectionTime { get; set; }

    /// <summary>Hit ratio as percentage.</summary>
    public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) * 100 : 0;
}

/// <summary>
/// Event args for cache refresh events.
/// </summary>
public class CacheRefreshedEventArgs : EventArgs
{
    /// <summary>Number of packages detected.</summary>
    public int PackageCount { get; init; }

    /// <summary>Time taken for detection.</summary>
    public TimeSpan DetectionTime { get; init; }

    /// <summary>Whether refresh was triggered by user.</summary>
    public bool ManualRefresh { get; init; }
}

/// <summary>
/// Reason for cache invalidation.
/// </summary>
public enum CacheInvalidationReason
{
    /// <summary>An application was installed.</summary>
    ApplicationInstalled,

    /// <summary>An application was uninstalled.</summary>
    ApplicationUninstalled,

    /// <summary>An application was updated.</summary>
    ApplicationUpdated,

    /// <summary>Manual user request.</summary>
    UserRequested,

    /// <summary>System configuration changed.</summary>
    SystemChanged,

    /// <summary>Cache TTL expired.</summary>
    TtlExpired
}

/// <summary>
/// Event args for cache invalidation events.
/// </summary>
public class CacheInvalidatedEventArgs : EventArgs
{
    /// <summary>The application ID that triggered invalidation, or null for full cache.</summary>
    public string? AppId { get; init; }

    /// <summary>The reason for invalidation.</summary>
    public CacheInvalidationReason Reason { get; init; }

    /// <summary>When the invalidation occurred.</summary>
    public DateTime Timestamp { get; init; }
}
