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

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Service for caching and accessing the applications database.
/// </summary>
public class ApplicationCacheService : IApplicationCacheService, IDisposable
{
    private readonly IRepositoryPathService _pathService;
    private Dictionary<string, JsonElement>? _applicationsCache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime? _cacheTimestamp;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ApplicationCacheService.
    /// </summary>
    /// <param name="pathService">The repository path service.</param>
    public ApplicationCacheService(IRepositoryPathService pathService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, JsonElement>? ApplicationsCache => _applicationsCache;

    /// <inheritdoc/>
    public DateTime? CacheTimestamp => _cacheTimestamp;

    /// <inheritdoc/>
    public bool IsCacheExpired
    {
        get
        {
            if (_cacheTimestamp == null) return true;
            return (DateTime.UtcNow - _cacheTimestamp.Value).TotalMinutes > IApplicationCacheService.DefaultCacheTtlMinutes;
        }
    }

    /// <inheritdoc/>
    public async Task EnsureApplicationsCacheAsync()
    {
        // Quick check without lock - also check for expiration
        if (_applicationsCache != null && !IsCacheExpired) return;

        await _cacheLock.WaitAsync();
        try
        {
            // Double-check inside lock - also check for expiration
            if (_applicationsCache != null && !IsCacheExpired) return;

            string dbPath = _pathService.GetPath("Apps", "Database", "applications.json");

            if (!File.Exists(dbPath))
            {
                _applicationsCache = new Dictionary<string, JsonElement>();
                _cacheTimestamp = DateTime.UtcNow;
                return;
            }

            string jsonContent = await File.ReadAllTextAsync(dbPath);

            using JsonDocument document = JsonDocument.Parse(jsonContent);

            Dictionary<string, JsonElement> newCache = new Dictionary<string, JsonElement>();

            if (document.RootElement.TryGetProperty("Applications", out JsonElement apps))
            {
                foreach (JsonProperty app in apps.EnumerateObject())
                {
                    newCache[app.Name] = app.Value.Clone();
                }
            }

            // Atomic assignment after building the entire cache
            _applicationsCache = newCache;
            _cacheTimestamp = DateTime.UtcNow;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc/>
    public bool TryGetApplicationData(string appId, out JsonElement appData)
    {
        appData = default;

        if (_applicationsCache == null)
        {
            return false;
        }

        return _applicationsCache.TryGetValue(appId, out appData);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        // Use timeout to prevent indefinite blocking on UI thread
        // If lock cannot be acquired within 100ms, proceed anyway
        // (cache will be cleared on next async operation)
        bool lockAcquired = _cacheLock.Wait(TimeSpan.FromMilliseconds(100));
        try
        {
            _applicationsCache = null;
            _cacheTimestamp = null;
        }
        finally
        {
            if (lockAcquired)
            {
                _cacheLock.Release();
            }
        }
    }

    /// <summary>
    /// Releases resources used by the ApplicationCacheService.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the ApplicationCacheService.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cacheLock.Dispose();
        }

        _disposed = true;
    }
}
