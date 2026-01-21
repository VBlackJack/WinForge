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
using System.Management.Automation;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Hybrid application detection service that combines multiple detection methods
/// for optimal performance and coverage.
///
/// Detection priority:
/// 1. Registry scan (fastest, ~20ms for full scan)
/// 2. WinGet PowerShell module (returns objects, ~500ms)
/// 3. AppX packages (Store apps, ~100ms)
///
/// Uses aggressive caching with configurable TTL.
/// </summary>
public class HybridDetectionService : IApplicationDetectionService, IDisposable
{
    private readonly RegistryDetectionService _registryService;
    private readonly JsonApplicationDetectionService _jsonDetectionService;
    private readonly object _cacheLock = new();
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    // Cache
    private BatchDetectionResult? _cache;
    private DateTime _cacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    // Update cache
    private IReadOnlyList<UpdateInfo>? _updateCache;
    private DateTime _updateCacheTime = DateTime.MinValue;
    private readonly TimeSpan _updateCacheTtl = TimeSpan.FromMinutes(10);

    // Statistics
    private int _cacheHits;
    private int _cacheMisses;
    private readonly List<TimeSpan> _detectionTimes = new();

    // WinGet module availability
    private bool? _winGetModuleAvailable;
    private readonly object _winGetCheckLock = new();

    /// <summary>
    /// Event raised when the cache is refreshed.
    /// </summary>
    public event EventHandler<CacheRefreshedEventArgs>? CacheRefreshed;

    /// <summary>
    /// Event raised when the cache is invalidated.
    /// </summary>
    public event EventHandler<CacheInvalidatedEventArgs>? CacheInvalidated;

    public HybridDetectionService()
    {
        _registryService = new RegistryDetectionService();
        _jsonDetectionService = new JsonApplicationDetectionService();
    }

    /// <summary>
    /// Invalidates the cache for a specific application after installation/uninstallation.
    /// This triggers an immediate refresh on next access.
    /// </summary>
    /// <param name="appId">The application ID that was modified.</param>
    /// <param name="reason">The reason for invalidation.</param>
    public void InvalidateCacheForApp(string appId, CacheInvalidationReason reason)
    {
        lock (_cacheLock)
        {
            // Remove specific app from cache if present
            if (_cache?.Packages != null && _cache.Packages is Dictionary<string, InstalledPackageInfo> dict)
            {
                dict.Remove(appId);
            }

            // If it's an install/uninstall, we should force a full refresh on next access
            if (reason == CacheInvalidationReason.ApplicationInstalled ||
                reason == CacheInvalidationReason.ApplicationUninstalled)
            {
                _cacheTime = DateTime.MinValue; // Force cache expiration
            }
        }

        CacheInvalidated?.Invoke(this, new CacheInvalidatedEventArgs
        {
            AppId = appId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });

        Debug.WriteLine($"Cache invalidated for {appId}: {reason}");
    }

    /// <summary>
    /// Invalidates the entire cache, forcing a refresh on next access.
    /// </summary>
    /// <param name="reason">The reason for invalidation.</param>
    public void InvalidateAllCache(CacheInvalidationReason reason)
    {
        lock (_cacheLock)
        {
            _cacheTime = DateTime.MinValue;
            _updateCacheTime = DateTime.MinValue;
        }

        CacheInvalidated?.Invoke(this, new CacheInvalidatedEventArgs
        {
            AppId = null,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });

        Debug.WriteLine($"Full cache invalidated: {reason}");
    }

    /// <inheritdoc/>
    public async Task<BatchDetectionResult> GetInstalledPackagesAsync(bool forceRefresh = false)
    {
        // Check cache first
        if (!forceRefresh)
        {
            lock (_cacheLock)
            {
                if (_cache != null && DateTime.UtcNow - _cacheTime < _cacheTtl)
                {
                    Interlocked.Increment(ref _cacheHits);
                    return new BatchDetectionResult
                    {
                        Packages = _cache.Packages,
                        DetectionTime = _cache.DetectionTime,
                        FromCache = true,
                        Timestamp = _cache.Timestamp
                    };
                }
            }
        }

        // Acquire semaphore to prevent concurrent refreshes
        await _refreshSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring semaphore
            if (!forceRefresh)
            {
                lock (_cacheLock)
                {
                    if (_cache != null && DateTime.UtcNow - _cacheTime < _cacheTtl)
                    {
                        Interlocked.Increment(ref _cacheHits);
                        return new BatchDetectionResult
                        {
                            Packages = _cache.Packages,
                            DetectionTime = _cache.DetectionTime,
                            FromCache = true,
                            Timestamp = _cache.Timestamp
                        };
                    }
                }
            }

            Interlocked.Increment(ref _cacheMisses);
            return await RefreshCacheInternalAsync(forceRefresh);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    /// <summary>
    /// Internal method to refresh the cache.
    /// </summary>
    private async Task<BatchDetectionResult> RefreshCacheInternalAsync(bool manual)
    {
        var stopwatch = Stopwatch.StartNew();
        var allPackages = new Dictionary<string, InstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

        // Step 1: Registry scan (fastest, ~20ms)
        try
        {
            var registryPackages = await _registryService.ScanInstalledApplicationsAsync();
            foreach (var kvp in registryPackages)
            {
                allPackages[kvp.Key] = kvp.Value;
            }
            Debug.WriteLine($"Registry scan: {registryPackages.Count} packages");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry scan failed: {ex.Message}");
        }

        // Step 2: WinGet PowerShell module (if available, ~500ms)
        if (await IsWinGetModuleAvailableAsync())
        {
            try
            {
                var wingetPackages = await GetWinGetPackagesAsync();
                foreach (var pkg in wingetPackages)
                {
                    // WinGet provides better ID matching, merge with registry data
                    if (allPackages.TryGetValue(pkg.Id, out var existing))
                    {
                        // Update with WinGet's available version info
                        allPackages[pkg.Id] = new InstalledPackageInfo
                        {
                            Id = pkg.Id,
                            Name = existing.Name,
                            InstalledVersion = !string.IsNullOrEmpty(pkg.InstalledVersion)
                                ? pkg.InstalledVersion
                                : existing.InstalledVersion,
                            AvailableVersion = pkg.AvailableVersion,
                            Publisher = existing.Publisher ?? pkg.Publisher,
                            InstallLocation = existing.InstallLocation,
                            UninstallString = existing.UninstallString,
                            Source = DetectionSource.WinGet
                        };
                    }
                    else
                    {
                        allPackages[pkg.Id] = pkg;
                    }
                }
                Debug.WriteLine($"WinGet scan: {wingetPackages.Count} packages");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WinGet scan failed: {ex.Message}");
            }
        }

        // Step 3: AppX packages (Store apps, ~100ms)
        try
        {
            var appxPackages = await GetAppXPackagesAsync();
            foreach (var pkg in appxPackages)
            {
                if (!allPackages.ContainsKey(pkg.Id))
                {
                    allPackages[pkg.Id] = pkg;
                }
            }
            Debug.WriteLine($"AppX scan: {appxPackages.Count} packages");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppX scan failed: {ex.Message}");
        }

        // Step 4: JSON database detection (for runtimes with Command/custom Registry detection)
        // This catches .NET runtimes, Java, VC++ Redist, etc. that have special detection methods
        try
        {
            var jsonPackages = await _jsonDetectionService.DetectAllAsync();
            foreach (var kvp in jsonPackages)
            {
                // JSON detection has priority for its defined apps (especially runtimes)
                // because it uses the correct detection method from applications.json
                allPackages[kvp.Key] = kvp.Value;
            }
            Debug.WriteLine($"JSON database scan: {jsonPackages.Count} packages");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"JSON database scan failed: {ex.Message}");
        }

        stopwatch.Stop();

        var result = new BatchDetectionResult
        {
            Packages = allPackages,
            DetectionTime = stopwatch.Elapsed,
            FromCache = false,
            Timestamp = DateTime.UtcNow
        };

        // Update cache
        lock (_cacheLock)
        {
            _cache = result;
            _cacheTime = DateTime.UtcNow;
            _detectionTimes.Add(stopwatch.Elapsed);
            if (_detectionTimes.Count > 10)
                _detectionTimes.RemoveAt(0);
        }

        Debug.WriteLine($"Total detection: {allPackages.Count} packages in {stopwatch.ElapsedMilliseconds}ms");

        // Raise event
        CacheRefreshed?.Invoke(this, new CacheRefreshedEventArgs
        {
            PackageCount = allPackages.Count,
            DetectionTime = stopwatch.Elapsed,
            ManualRefresh = manual
        });

        return result;
    }

    /// <inheritdoc/>
    public async Task<InstalledPackageInfo?> GetPackageInfoAsync(string appId)
    {
        var result = await GetInstalledPackagesAsync();
        return result.GetPackage(appId);
    }

    /// <inheritdoc/>
    public async Task<string?> GetInstalledVersionAsync(string appId)
    {
        var info = await GetPackageInfoAsync(appId);
        return info?.InstalledVersion;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UpdateInfo>> GetAvailableUpdatesAsync()
    {
        // Check update cache
        lock (_cacheLock)
        {
            if (_updateCache != null && DateTime.UtcNow - _updateCacheTime < _updateCacheTtl)
            {
                return _updateCache;
            }
        }

        var updates = new List<UpdateInfo>();

        if (await IsWinGetModuleAvailableAsync())
        {
            try
            {
                updates = await GetWinGetUpdatesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WinGet update check failed: {ex.Message}");
            }
        }

        // Update cache
        lock (_cacheLock)
        {
            _updateCache = updates;
            _updateCacheTime = DateTime.UtcNow;
        }

        return updates;
    }

    /// <inheritdoc/>
    public async Task WarmCacheAsync()
    {
        Debug.WriteLine("Starting cache pre-warming...");
        var stopwatch = Stopwatch.StartNew();

        // Warm main detection cache
        await GetInstalledPackagesAsync(forceRefresh: true);

        // Optionally pre-fetch updates in background
        _ = Task.Run(async () =>
        {
            try
            {
                await GetAvailableUpdatesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update pre-fetch failed: {ex.Message}");
            }
        });

        stopwatch.Stop();
        Debug.WriteLine($"Cache pre-warming completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache = null;
            _cacheTime = DateTime.MinValue;
            _updateCache = null;
            _updateCacheTime = DateTime.MinValue;
            _cacheHits = 0;
            _cacheMisses = 0;
            _detectionTimes.Clear();
        }
    }

    /// <inheritdoc/>
    public CacheStatistics GetCacheStatistics()
    {
        lock (_cacheLock)
        {
            return new CacheStatistics
            {
                Hits = _cacheHits,
                Misses = _cacheMisses,
                PackageCount = _cache?.Count ?? 0,
                CacheAge = _cache != null ? DateTime.UtcNow - _cacheTime : TimeSpan.Zero,
                LastRefresh = _cache?.Timestamp,
                AverageDetectionTime = _detectionTimes.Count > 0
                    ? TimeSpan.FromMilliseconds(_detectionTimes.Average(t => t.TotalMilliseconds))
                    : TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Checks if the Microsoft.WinGet.Client PowerShell module is available.
    /// </summary>
    private async Task<bool> IsWinGetModuleAvailableAsync()
    {
        lock (_winGetCheckLock)
        {
            if (_winGetModuleAvailable.HasValue)
                return _winGetModuleAvailable.Value;
        }

        var isAvailable = await Task.Run(() =>
        {
            try
            {
                using var ps = PowerShell.Create();
                ps.AddCommand("Get-Module")
                  .AddParameter("ListAvailable")
                  .AddParameter("Name", "Microsoft.WinGet.Client");

                var result = ps.Invoke();
                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        });

        lock (_winGetCheckLock)
        {
            _winGetModuleAvailable = isAvailable;
        }

        Debug.WriteLine($"WinGet module available: {isAvailable}");
        return isAvailable;
    }

    /// <summary>
    /// Gets installed packages using the WinGet PowerShell module.
    /// Returns objects directly, no text parsing required.
    /// </summary>
    private async Task<List<InstalledPackageInfo>> GetWinGetPackagesAsync()
    {
        return await Task.Run(() =>
        {
            var packages = new List<InstalledPackageInfo>();

            try
            {
                using var ps = PowerShell.Create();

                // Import module and get packages
                ps.AddScript(@"
                    Import-Module Microsoft.WinGet.Client -ErrorAction SilentlyContinue
                    Get-WinGetPackage -ErrorAction SilentlyContinue | Select-Object Id, Name, InstalledVersion, IsUpdateAvailable, Source
                ");

                var results = ps.Invoke();

                foreach (var result in results)
                {
                    if (result?.BaseObject == null) continue;

                    var id = result.Properties["Id"]?.Value?.ToString();
                    var name = result.Properties["Name"]?.Value?.ToString();

                    if (string.IsNullOrEmpty(id)) continue;

                    packages.Add(new InstalledPackageInfo
                    {
                        Id = id,
                        Name = name ?? id,
                        InstalledVersion = result.Properties["InstalledVersion"]?.Value?.ToString() ?? "",
                        AvailableVersion = null, // Will be populated separately
                        Source = DetectionSource.WinGet
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WinGet module error: {ex.Message}");
            }

            return packages;
        });
    }

    /// <summary>
    /// Gets available updates using the WinGet PowerShell module.
    /// Single call returns all updates (~500ms total).
    /// </summary>
    private async Task<List<UpdateInfo>> GetWinGetUpdatesAsync()
    {
        return await Task.Run(() =>
        {
            var updates = new List<UpdateInfo>();

            try
            {
                using var ps = PowerShell.Create();

                // Get packages with updates available
                ps.AddScript(@"
                    Import-Module Microsoft.WinGet.Client -ErrorAction SilentlyContinue
                    Get-WinGetPackage -ErrorAction SilentlyContinue |
                        Where-Object { $_.IsUpdateAvailable } |
                        Select-Object Id, Name, InstalledVersion, @{N='AvailableVersion';E={$_.AvailableVersions[0]}}, Source
                ");

                var results = ps.Invoke();

                foreach (var result in results)
                {
                    if (result?.BaseObject == null) continue;

                    var id = result.Properties["Id"]?.Value?.ToString();
                    if (string.IsNullOrEmpty(id)) continue;

                    updates.Add(new UpdateInfo
                    {
                        Id = id,
                        Name = result.Properties["Name"]?.Value?.ToString() ?? id,
                        CurrentVersion = result.Properties["InstalledVersion"]?.Value?.ToString() ?? "",
                        NewVersion = result.Properties["AvailableVersion"]?.Value?.ToString() ?? "",
                        Source = result.Properties["Source"]?.Value?.ToString() ?? "winget"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WinGet update check error: {ex.Message}");
            }

            return updates;
        });
    }

    /// <summary>
    /// Gets installed AppX/MSIX packages (Microsoft Store apps).
    /// </summary>
    private async Task<List<InstalledPackageInfo>> GetAppXPackagesAsync()
    {
        return await Task.Run(() =>
        {
            var packages = new List<InstalledPackageInfo>();

            try
            {
                using var ps = PowerShell.Create();
                ps.AddCommand("Get-AppxPackage")
                  .AddParameter("ErrorAction", "SilentlyContinue");

                var results = ps.Invoke();

                foreach (var result in results)
                {
                    if (result?.BaseObject == null) continue;

                    var name = result.Properties["Name"]?.Value?.ToString();
                    var packageFullName = result.Properties["PackageFullName"]?.Value?.ToString();
                    var version = result.Properties["Version"]?.Value?.ToString();
                    var publisher = result.Properties["Publisher"]?.Value?.ToString();

                    if (string.IsNullOrEmpty(name)) continue;

                    // Skip internal framework packages and system apps
                    // Note: .NET Desktop Runtime and other runtimes are detected via JSON database
                    // detection (Step 4) using their proper detection methods (command/registry)
                    if (name.StartsWith("Microsoft.NET.") || name.StartsWith("Microsoft.VCLibs."))
                        continue;
                    if (name.StartsWith("Microsoft.Windows.") && !name.Contains("Terminal"))
                        continue;
                    // Skip internal framework packages but allow user-facing apps
                    if (name.StartsWith("Microsoft.UI.") || name.StartsWith("Microsoft.WinUI."))
                        continue;

                    packages.Add(new InstalledPackageInfo
                    {
                        Id = name,
                        Name = packageFullName ?? name,
                        InstalledVersion = version ?? "",
                        Publisher = publisher,
                        Source = DetectionSource.AppX
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppX scan error: {ex.Message}");
            }

            return packages;
        });
    }

    public void Dispose()
    {
        _refreshSemaphore.Dispose();
    }
}
