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

#nullable enable

using System.IO;
using System.Text.Json;
using WinForge.GUI.Configuration;
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.Services;

/// <summary>
/// Service that performs application detection using the methods defined in applications.json.
/// Supports Registry, Command, File, and Windows feature detection methods.
/// This enables proper detection of runtimes (.NET, Java, VC++ Redist, etc.) that use
/// custom registry paths or command-based detection. Per-configuration probing is
/// delegated to <see cref="IDetectionProbe"/>.
/// </summary>
public class JsonApplicationDetectionService
{
    private Dictionary<string, ApplicationJsonEntry>? _applicationDatabase;
    private Dictionary<string, string>? _wingetIdToJsonKey;
    private readonly object _loadLock = new();
    private readonly string _databasePath;
    private readonly ILoggingService _logger;
    private readonly IDetectionProbe _probe;

    public JsonApplicationDetectionService(
        IRepositoryPathService pathService,
        ILoggerFactory? loggerFactory = null,
        IDetectionProbe? detectionProbe = null)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<JsonApplicationDetectionService>();
        _probe = detectionProbe ?? new DetectionProbe(loggerFactory);

        _databasePath = pathService.GetPath(
            WinForgePathNames.AppsDirectoryName,
            WinForgePathNames.DatabaseDirectoryName,
            WinForgePathNames.ApplicationsDatabaseFileName);
    }

    /// <summary>
    /// Loads the application database from applications.json.
    /// </summary>
    private void EnsureDatabaseLoaded()
    {
        if (_applicationDatabase != null) return;

        lock (_loadLock)
        {
            if (_applicationDatabase != null) return;

            try
            {
                if (!File.Exists(_databasePath))
                {
                    _logger.LogWarning($"Applications database not found at: {_databasePath}");
                    _applicationDatabase = new Dictionary<string, ApplicationJsonEntry>();
                    _wingetIdToJsonKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                string json = File.ReadAllText(_databasePath);
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                // Parse the root structure which contains "Applications" property
                ApplicationsDatabase? root = JsonSerializer.Deserialize<ApplicationsDatabase>(json, options);

                _applicationDatabase = root?.Applications
                    ?? new Dictionary<string, ApplicationJsonEntry>();

                // Build reverse lookup: WinGet ID -> JSON key
                _wingetIdToJsonKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, ApplicationJsonEntry> kvp in _applicationDatabase)
                {
                    string? wingetId = kvp.Value.Sources?.Winget;
                    if (!string.IsNullOrEmpty(wingetId))
                    {
                        _wingetIdToJsonKey[wingetId] = kvp.Key;
                    }
                    // Also index by Chocolatey ID
                    string? chocoId = kvp.Value.Sources?.Chocolatey;
                    if (!string.IsNullOrEmpty(chocoId) && !_wingetIdToJsonKey.ContainsKey(chocoId))
                    {
                        _wingetIdToJsonKey[chocoId] = kvp.Key;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error loading applications database", ex);
                _applicationDatabase = new Dictionary<string, ApplicationJsonEntry>();
                _wingetIdToJsonKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Gets all applications that have detection methods defined.
    /// </summary>
    public IReadOnlyDictionary<string, ApplicationJsonEntry> GetApplicationsWithDetection()
    {
        EnsureDatabaseLoaded();
        return _applicationDatabase!
            .Where(kvp => kvp.Value.Detection != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Detects a specific application using its configured detection method.
    /// Accepts either JSON key (e.g., "GoogleChrome") or WinGet ID (e.g., "Google.Chrome").
    /// </summary>
    /// <param name="appId">The application ID (JSON key or WinGet ID).</param>
    /// <returns>Package info if installed, null otherwise.</returns>
    public async Task<InstalledPackageInfo?> DetectApplicationAsync(string appId)
    {
        EnsureDatabaseLoaded();

        // Try direct lookup first (JSON key)
        if (_applicationDatabase!.TryGetValue(appId, out ApplicationJsonEntry? app) && app.Detection != null)
        {
            string primaryId = app.GetPrimaryId(appId);
            return await DetectAsync(primaryId, app.Name, app.Detection);
        }

        // Try reverse lookup (WinGet/Chocolatey ID)
        if (_wingetIdToJsonKey!.TryGetValue(appId, out string? jsonKey) &&
            _applicationDatabase.TryGetValue(jsonKey, out app) && app.Detection != null)
        {
            return await DetectAsync(appId, app.Name, app.Detection);
        }

        return null;
    }

    /// <summary>
    /// Detects all applications with defined detection methods.
    /// Returns only the ones that are installed.
    /// Keys are WinGet IDs (or fallback to JSON key if no WinGet ID).
    /// </summary>
    /// <param name="appIds">Optional list of app IDs to check (WinGet IDs). If null, checks all apps with detection methods.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of installed packages keyed by WinGet ID.</returns>
    public async Task<Dictionary<string, InstalledPackageInfo>> DetectAllAsync(
        IEnumerable<string>? appIds = null,
        CancellationToken cancellationToken = default)
    {
        EnsureDatabaseLoaded();

        Dictionary<string, InstalledPackageInfo> results = new Dictionary<string, InstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<KeyValuePair<string, ApplicationJsonEntry>> appsToCheck;

        if (appIds != null)
        {
            // Filter by provided IDs - could be JSON keys or WinGet IDs
            HashSet<string> appIdSet = new HashSet<string>(appIds, StringComparer.OrdinalIgnoreCase);
            appsToCheck = _applicationDatabase!.Where(kvp =>
            {
                if (kvp.Value.Detection == null) return false;
                // Match by JSON key
                if (appIdSet.Contains(kvp.Key)) return true;
                // Match by WinGet ID
                if (!string.IsNullOrEmpty(kvp.Value.Sources?.Winget) && appIdSet.Contains(kvp.Value.Sources.Winget)) return true;
                // Match by Chocolatey ID
                if (!string.IsNullOrEmpty(kvp.Value.Sources?.Chocolatey) && appIdSet.Contains(kvp.Value.Sources.Chocolatey)) return true;
                return false;
            });
        }
        else
        {
            appsToCheck = _applicationDatabase!.Where(kvp => kvp.Value.Detection != null);
        }

        // Use parallel detection for performance, but limit concurrency
        using SemaphoreSlim semaphore = new SemaphoreSlim(4);
        IEnumerable<Task<(string JsonKey, string PrimaryId, InstalledPackageInfo? Result)>> tasks = appsToCheck.Select(async kvp =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Use WinGet ID as the key for results (matches ApplicationModel.AppId)
                string primaryId = kvp.Value.GetPrimaryId(kvp.Key);
                InstalledPackageInfo? result = await DetectAsync(primaryId, kvp.Value.Name, kvp.Value.Detection!);
                return (JsonKey: kvp.Key, PrimaryId: primaryId, Result: result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        (string JsonKey, string PrimaryId, InstalledPackageInfo? Result)[] detectionResults = await Task.WhenAll(tasks);

        foreach ((string? jsonKey, string? primaryId, InstalledPackageInfo? info) in detectionResults)
        {
            if (info != null)
            {
                results[jsonKey] = info;
                if (!string.Equals(jsonKey, primaryId, StringComparison.OrdinalIgnoreCase))
                {
                    results[primaryId] = info;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Performs detection based on the configured method.
    /// </summary>
    private async Task<InstalledPackageInfo?> DetectAsync(string appId, string appName, DetectionConfiguration config)
    {
        try
        {
            DetectionProbeResult result = await _probe.ProbeAsync(config, PathValidationPolicy.Strict);
            if (result.Outcome != DetectionOutcome.Found)
            {
                return null;
            }

            return new InstalledPackageInfo
            {
                Id = appId,
                Name = appName,
                InstalledVersion = result.Version ?? Loc.Status_Installed,
                Source = result.Source
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Detection error for {appId}", ex);
            return null;
        }
    }

    /// <summary>
    /// Reloads the application database from disk.
    /// </summary>
    public void ReloadDatabase()
    {
        lock (_loadLock)
        {
            _applicationDatabase = null;
            _wingetIdToJsonKey = null;
        }
        EnsureDatabaseLoaded();
    }
}
