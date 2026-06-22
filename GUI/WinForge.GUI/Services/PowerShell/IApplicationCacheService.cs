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

using System.Text.Json;

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Service for caching and accessing the applications database.
/// </summary>
public interface IApplicationCacheService
{
    /// <summary>
    /// Default cache TTL (Time-To-Live) in minutes.
    /// </summary>
    const int DefaultCacheTtlMinutes = 30;

    /// <summary>
    /// Gets the applications cache. May be null if not loaded.
    /// </summary>
    IReadOnlyDictionary<string, JsonElement>? ApplicationsCache { get; }

    /// <summary>
    /// Gets the UTC time when the cache was last populated.
    /// Null if cache has never been loaded.
    /// </summary>
    DateTime? CacheTimestamp { get; }

    /// <summary>
    /// Gets whether the cache has expired based on the TTL.
    /// </summary>
    bool IsCacheExpired { get; }

    /// <summary>
    /// Ensures the applications database is loaded into cache.
    /// Thread-safe using double-check locking pattern with async semaphore.
    /// </summary>
    Task EnsureApplicationsCacheAsync();

    /// <summary>
    /// Tries to get application data from the cache.
    /// </summary>
    /// <param name="appId">The application ID to look up.</param>
    /// <param name="appData">The application data if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetApplicationData(string appId, out JsonElement appData);

    /// <summary>
    /// Clears the applications cache, forcing a reload on next access.
    /// </summary>
    void ClearCache();
}
