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
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for managing deployment history.
/// Stores history in a JSON file in LocalAppData.
/// </summary>
public class DeploymentHistoryService : IDeploymentHistoryService
{
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes the history service.
    /// </summary>
    public DeploymentHistoryService()
    {
        // Try multiple fallback paths to ensure we always have a valid location
        var appDataPath = GetValidStoragePath();

        var win11ForgePath = Path.Combine(appDataPath, "Win11Forge");

        // Ensure directory exists
        try
        {
            if (!Directory.Exists(win11ForgePath))
            {
                Directory.CreateDirectory(win11ForgePath);
            }
        }
        catch (Exception)
        {
            // If we can't create the directory, use temp folder
            win11ForgePath = Path.Combine(Path.GetTempPath(), "Win11Forge");
            if (!Directory.Exists(win11ForgePath))
            {
                Directory.CreateDirectory(win11ForgePath);
            }
        }

        _historyFilePath = Path.Combine(win11ForgePath, "history.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Gets a valid storage path using multiple fallback strategies.
    /// </summary>
    private static string GetValidStoragePath()
    {
        // Strategy 1: LocalApplicationData (preferred)
        var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(Path.GetDirectoryName(path) ?? path))
        {
            return path;
        }

        // Strategy 2: UserProfile\AppData\Local
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            path = Path.Combine(userProfile, "AppData", "Local");
            if (Directory.Exists(userProfile))
            {
                return path;
            }
        }

        // Strategy 3: CommonApplicationData (ProgramData)
        path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Strategy 4: Temp folder (ultimate fallback - always exists)
        return Path.GetTempPath();
    }

    /// <inheritdoc/>
    public async Task AddEntryAsync(DeploymentHistoryEntry entry)
    {
        await _fileLock.WaitAsync();
        try
        {
            var history = await LoadHistoryInternalAsync();
            history.Insert(0, entry); // Add at beginning (newest first)

            // Keep only the last 100 entries
            if (history.Count > 100)
            {
                history = history.Take(100).ToList();
            }

            await SaveHistoryInternalAsync(history);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<List<DeploymentHistoryEntry>> GetHistoryAsync(int limit = 50)
    {
        await _fileLock.WaitAsync();
        try
        {
            var history = await LoadHistoryInternalAsync();
            return history.Take(limit).ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<List<DeploymentHistoryEntry>> GetRecentHistoryAsync(int count = 5)
    {
        return await GetHistoryAsync(count);
    }

    /// <inheritdoc/>
    public async Task ClearHistoryAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(_historyFilePath))
            {
                File.Delete(_historyFilePath);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Loads history from the JSON file.
    /// </summary>
    private async Task<List<DeploymentHistoryEntry>> LoadHistoryInternalAsync()
    {
        if (!File.Exists(_historyFilePath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyFilePath);
            var history = JsonSerializer.Deserialize<List<DeploymentHistoryEntry>>(json, _jsonOptions);
            return history ?? [];
        }
        catch (JsonException)
        {
            // If file is corrupted, start fresh
            return [];
        }
    }

    /// <summary>
    /// Saves history to the JSON file.
    /// </summary>
    private async Task SaveHistoryInternalAsync(List<DeploymentHistoryEntry> history)
    {
        var json = JsonSerializer.Serialize(history, _jsonOptions);
        await File.WriteAllTextAsync(_historyFilePath, json);
    }
}

/// <summary>
/// Interface for deployment history service.
/// </summary>
public interface IDeploymentHistoryService
{
    /// <summary>
    /// Adds a new deployment entry to history.
    /// </summary>
    Task AddEntryAsync(DeploymentHistoryEntry entry);

    /// <summary>
    /// Gets deployment history entries.
    /// </summary>
    /// <param name="limit">Maximum number of entries to return</param>
    Task<List<DeploymentHistoryEntry>> GetHistoryAsync(int limit = 50);

    /// <summary>
    /// Gets recent deployment history for dashboard display.
    /// </summary>
    /// <param name="count">Number of entries to return</param>
    Task<List<DeploymentHistoryEntry>> GetRecentHistoryAsync(int count = 5);

    /// <summary>
    /// Clears all history entries.
    /// </summary>
    Task ClearHistoryAsync();
}
