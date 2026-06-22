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

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;
using PS = System.Management.Automation.PowerShell;

namespace WinForge.GUI.Services;

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
    private static readonly TimeSpan WingetCliQueryTimeout = TimeSpan.FromMinutes(5);

    private readonly RegistryDetectionService _registryService;
    private readonly JsonApplicationDetectionService _jsonDetectionService;
    private readonly ILoggingService _logger;
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

    // Disposal flag
    private bool _disposed;

    /// <summary>
    /// Event raised when the cache is refreshed.
    /// </summary>
    public event EventHandler<CacheRefreshedEventArgs>? CacheRefreshed;

    /// <summary>
    /// Event raised when the cache is invalidated.
    /// </summary>
    public event EventHandler<CacheInvalidatedEventArgs>? CacheInvalidated;

    public HybridDetectionService(
        ILoggerFactory loggerFactory,
        IRepositoryPathService pathService,
        IDetectionProbe? detectionProbe = null)
    {
        _registryService = new RegistryDetectionService(loggerFactory);
        _jsonDetectionService = new JsonApplicationDetectionService(
            pathService ?? throw new ArgumentNullException(nameof(pathService)),
            loggerFactory,
            detectionProbe);
        _logger = loggerFactory?.CreateLogger<HybridDetectionService>() ?? throw new ArgumentNullException(nameof(loggerFactory));
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

        _logger.LogDebug($"Cache invalidated for {appId}: {reason}");
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

        _logger.LogDebug($"Full cache invalidated: {reason}");
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
        Stopwatch stopwatch = Stopwatch.StartNew();
        Dictionary<string, InstalledPackageInfo> allPackages = new Dictionary<string, InstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

        // Step 1: Registry scan (fastest, ~20ms)
        try
        {
            Dictionary<string, InstalledPackageInfo> registryPackages = await _registryService.ScanInstalledApplicationsAsync();
            foreach (KeyValuePair<string, InstalledPackageInfo> kvp in registryPackages)
            {
                allPackages[kvp.Key] = kvp.Value;
            }
            _logger.LogDebug($"Registry scan: {registryPackages.Count} packages");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Registry scan failed: {ex.Message}");
        }

        // Step 2: WinGet PowerShell module (if available, ~500ms)
        if (await IsWinGetModuleAvailableAsync())
        {
            try
            {
                List<InstalledPackageInfo> wingetPackages = await GetWinGetPackagesAsync();
                foreach (InstalledPackageInfo pkg in wingetPackages)
                {
                    // WinGet provides better ID matching, merge with registry data
                    if (allPackages.TryGetValue(pkg.Id, out InstalledPackageInfo? existing))
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
                _logger.LogDebug($"WinGet scan: {wingetPackages.Count} packages");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"WinGet scan failed: {ex.Message}");
            }
        }

        // Step 3: AppX packages (Store apps, ~100ms)
        try
        {
            List<InstalledPackageInfo> appxPackages = await GetAppXPackagesAsync();
            foreach (InstalledPackageInfo pkg in appxPackages)
            {
                if (!allPackages.ContainsKey(pkg.Id))
                {
                    allPackages[pkg.Id] = pkg;
                }
            }
            _logger.LogDebug($"AppX scan: {appxPackages.Count} packages");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"AppX scan failed: {ex.Message}");
        }

        // Step 4: JSON database detection (for runtimes with Command/custom Registry detection)
        // This catches .NET runtimes, Java, VC++ Redist, etc. that have special detection methods
        try
        {
            Dictionary<string, InstalledPackageInfo> jsonPackages = await _jsonDetectionService.DetectAllAsync();
            foreach (KeyValuePair<string, InstalledPackageInfo> kvp in jsonPackages)
            {
                // JSON detection has priority for its defined apps (especially runtimes)
                // because it uses the correct detection method from applications.json
                allPackages[kvp.Key] = kvp.Value;
            }
            _logger.LogDebug($"JSON database scan: {jsonPackages.Count} packages");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"JSON database scan failed: {ex.Message}");
        }

        stopwatch.Stop();

        BatchDetectionResult result = new BatchDetectionResult
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

        _logger.LogInfo($"Total detection: {allPackages.Count} packages in {stopwatch.ElapsedMilliseconds}ms");

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
        BatchDetectionResult result = await GetInstalledPackagesAsync();
        return result.GetPackage(appId);
    }

    /// <inheritdoc/>
    public async Task<string?> GetInstalledVersionAsync(string appId)
    {
        InstalledPackageInfo? info = await GetPackageInfoAsync(appId);
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

        List<UpdateInfo> updates = new List<UpdateInfo>();

        if (await IsWinGetModuleAvailableAsync())
        {
            try
            {
                updates = await GetWinGetUpdatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"WinGet update check failed: {ex.Message}");
            }
        }

        if (updates.Count == 0)
        {
            try
            {
                updates = await GetWingetCliUpdatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"WinGet CLI update check failed: {ex.Message}");
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
        _logger.LogInfo("Starting cache pre-warming...");
        Stopwatch stopwatch = Stopwatch.StartNew();

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
                _logger.LogWarning($"Update pre-fetch failed: {ex.Message}");
            }
        });

        stopwatch.Stop();
        _logger.LogInfo($"Cache pre-warming completed in {stopwatch.ElapsedMilliseconds}ms");
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

        bool isAvailable = await Task.Run(() =>
        {
            try
            {
                using PS ps = PS.Create();
                ps.AddCommand("Get-Module")
                  .AddParameter("ListAvailable")
                  .AddParameter("Name", "Microsoft.WinGet.Client");

                Collection<System.Management.Automation.PSObject> result = ps.Invoke();
                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        }).ConfigureAwait(false);

        lock (_winGetCheckLock)
        {
            _winGetModuleAvailable = isAvailable;
        }

        _logger.LogDebug($"WinGet module available: {isAvailable}");
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
            List<InstalledPackageInfo> packages = new List<InstalledPackageInfo>();

            try
            {
                using PS ps = PS.Create();

                // Import module and get packages
                ps.AddScript(@"
                    Import-Module Microsoft.WinGet.Client -ErrorAction SilentlyContinue
                    Get-WinGetPackage -ErrorAction SilentlyContinue | Select-Object Id, Name, InstalledVersion, IsUpdateAvailable, Source
                ");

                Collection<System.Management.Automation.PSObject> results = ps.Invoke();

                foreach (System.Management.Automation.PSObject? result in results)
                {
                    if (result?.BaseObject == null) continue;

                    string? id = result.Properties["Id"]?.Value?.ToString();
                    string? name = result.Properties["Name"]?.Value?.ToString();

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
                _logger.LogWarning($"WinGet module error: {ex.Message}");
            }

            return packages;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets available updates using the WinGet PowerShell module.
    /// Single call returns all updates (~500ms total).
    /// </summary>
    private async Task<List<UpdateInfo>> GetWinGetUpdatesAsync()
    {
        return await Task.Run(() =>
        {
            List<UpdateInfo> updates = new List<UpdateInfo>();

            try
            {
                using PS ps = PS.Create();

                // Get packages with updates available
                ps.AddScript(@"
                    Import-Module Microsoft.WinGet.Client -ErrorAction SilentlyContinue
                    Get-WinGetPackage -ErrorAction SilentlyContinue |
                        Where-Object { $_.IsUpdateAvailable } |
                        Select-Object Id, Name, InstalledVersion, @{N='AvailableVersion';E={$_.AvailableVersions[0]}}, Source
                ");

                Collection<System.Management.Automation.PSObject> results = ps.Invoke();

                foreach (System.Management.Automation.PSObject? result in results)
                {
                    if (result?.BaseObject == null) continue;

                    string? id = result.Properties["Id"]?.Value?.ToString();
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
                _logger.LogWarning($"WinGet update check error: {ex.Message}");
            }

            return updates;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets available updates using the winget CLI when the WinGet PowerShell module is unavailable.
    /// </summary>
    private async Task<List<UpdateInfo>> GetWingetCliUpdatesAsync()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = "upgrade --include-unknown --accept-source-agreements --disable-interactivity",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(WingetCliQueryTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            _logger.LogWarning("WinGet CLI update check timed out.");
            return [];
        }

        string output = await outputTask.ConfigureAwait(false);
        string error = await errorTask.ConfigureAwait(false);
        string combinedOutput = string.IsNullOrWhiteSpace(error)
            ? output
            : string.Concat(output, Environment.NewLine, error);

        return ParseWingetUpgradeOutput(combinedOutput);
    }

    private static List<UpdateInfo> ParseWingetUpgradeOutput(string output)
    {
        List<UpdateInfo> updates = new List<UpdateInfo>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return updates;
        }

        WingetUpgradeColumnLayout? layout = null;
        foreach (string line in SplitWingetOutputLines(output))
        {
            if (TryGetWingetUpgradeColumnLayout(line, out WingetUpgradeColumnLayout currentLayout))
            {
                layout = currentLayout;
                continue;
            }

            if (layout is null || IsWingetUpgradeNoiseLine(line))
            {
                continue;
            }

            WingetUpgradeColumnLayout columns = layout.Value;
            string name = GetColumnValue(line, columns.NameStart, columns.IdStart);
            string id = GetColumnValue(line, columns.IdStart, columns.VersionStart);
            string currentVersion = GetColumnValue(line, columns.VersionStart, columns.AvailableStart);
            string availableVersion = GetColumnValue(line, columns.AvailableStart, columns.SourceStart);
            string source = GetColumnValue(line, columns.SourceStart, line.Length);

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(availableVersion))
            {
                continue;
            }

            updates.Add(new UpdateInfo
            {
                Id = id,
                Name = name,
                CurrentVersion = currentVersion,
                NewVersion = availableVersion,
                Source = string.IsNullOrWhiteSpace(source) ? "winget" : source
            });
        }

        return updates;
    }

    private static IEnumerable<string> SplitWingetOutputLines(string output)
    {
        foreach (string rawLine in output.Split('\n'))
        {
            foreach (string segment in rawLine.Split('\r'))
            {
                string line = segment.TrimEnd();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }
    }

    private static bool TryGetWingetUpgradeColumnLayout(string line, out WingetUpgradeColumnLayout layout)
    {
        int idStart = line.IndexOf("ID", StringComparison.OrdinalIgnoreCase);
        int versionStart = line.IndexOf("Version", StringComparison.OrdinalIgnoreCase);
        int availableStart = line.IndexOf("Available", StringComparison.OrdinalIgnoreCase);
        if (availableStart < 0)
        {
            availableStart = line.IndexOf("Disponible", StringComparison.OrdinalIgnoreCase);
        }

        int sourceStart = line.IndexOf("Source", StringComparison.OrdinalIgnoreCase);
        if (sourceStart < 0)
        {
            sourceStart = line.IndexOf("Quelle", StringComparison.OrdinalIgnoreCase);
        }

        if (idStart > 0 &&
            versionStart > idStart &&
            availableStart > versionStart &&
            sourceStart > availableStart)
        {
            layout = new WingetUpgradeColumnLayout(0, idStart, versionStart, availableStart, sourceStart);
            return true;
        }

        layout = default;
        return false;
    }

    private static bool IsWingetUpgradeNoiseLine(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length == 0 ||
               trimmed.All(ch => ch == '-' || ch == ' ') ||
               trimmed is "-" or "\\" or "|" or "/" ||
               trimmed.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.Contains("mises", StringComparison.OrdinalIgnoreCase) &&
                trimmed.Contains("disponibles", StringComparison.OrdinalIgnoreCase)) ||
               trimmed.StartsWith("The following packages", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Les packages suivants", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("No installed package", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("No available upgrade", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Aucun package", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetColumnValue(string line, int start, int end)
    {
        if (start < 0 || start >= line.Length || end <= start)
        {
            return string.Empty;
        }

        int safeEnd = Math.Min(end, line.Length);
        return line.Substring(start, safeEnd - start).Trim();
    }

    private readonly record struct WingetUpgradeColumnLayout(
        int NameStart,
        int IdStart,
        int VersionStart,
        int AvailableStart,
        int SourceStart);

    /// <summary>
    /// Gets installed AppX/MSIX packages (Microsoft Store apps).
    /// </summary>
    private async Task<List<InstalledPackageInfo>> GetAppXPackagesAsync()
    {
        return await Task.Run(() =>
        {
            List<InstalledPackageInfo> packages = new List<InstalledPackageInfo>();

            try
            {
                using PS ps = PS.Create();
                ps.AddCommand("Get-AppxPackage")
                  .AddParameter("ErrorAction", "SilentlyContinue");

                Collection<System.Management.Automation.PSObject> results = ps.Invoke();

                foreach (System.Management.Automation.PSObject? result in results)
                {
                    if (result?.BaseObject == null) continue;

                    string? name = result.Properties["Name"]?.Value?.ToString();
                    string? packageFullName = result.Properties["PackageFullName"]?.Value?.ToString();
                    string? version = result.Properties["Version"]?.Value?.ToString();
                    string? publisher = result.Properties["Publisher"]?.Value?.ToString();
                    string? installLocation = result.Properties["InstallLocation"]?.Value?.ToString();

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
                        Name = name,
                        InstalledVersion = version ?? "",
                        Publisher = publisher,
                        InstallLocation = installLocation,
                        Source = DetectionSource.AppX
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"AppX scan error: {ex.Message}");
            }

            return packages;
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _refreshSemaphore.Dispose();

            // Clear event handlers to prevent memory leaks
            CacheRefreshed = null;
            CacheInvalidated = null;

            // Clear caches
            lock (_cacheLock)
            {
                _cache = null;
                _updateCache = null;
                _detectionTimes.Clear();
            }
        }

        _disposed = true;
    }
}
