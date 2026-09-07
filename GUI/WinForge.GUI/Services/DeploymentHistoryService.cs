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
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services;

/// <summary>
/// Service for managing deployment history.
/// Stores history in a JSON file resolved by the centralized path service.
/// </summary>
public class DeploymentHistoryService : IDeploymentHistoryService, IDisposable
{
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes the history service with centralized path resolution.
    /// </summary>
    public DeploymentHistoryService()
        : this(new RepositoryPathService())
    {
    }

    /// <summary>
    /// Initializes the history service with centralized path resolution.
    /// </summary>
    /// <param name="pathService">Centralized path service.</param>
    public DeploymentHistoryService(IRepositoryPathService pathService)
        : this((pathService ?? throw new ArgumentNullException(nameof(pathService))).DeploymentHistoryFilePath)
    {
    }

    internal DeploymentHistoryService(string historyFilePath)
    {
        _historyFilePath = string.IsNullOrWhiteSpace(historyFilePath)
            ? throw new ArgumentException("History file path cannot be empty.", nameof(historyFilePath))
            : historyFilePath;

        string? directory = Path.GetDirectoryName(_historyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc/>
    public async Task AddEntryAsync(DeploymentHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _fileLock.WaitAsync();
        try
        {
            List<DeploymentHistoryEntry> history = await LoadHistoryInternalAsync();
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
            List<DeploymentHistoryEntry> history = await LoadHistoryInternalAsync();
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
            File.Delete(_historyFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Loads history from the JSON file.
    /// Returns an empty list only when the file does not exist.
    /// </summary>
    private async Task<List<DeploymentHistoryEntry>> LoadHistoryInternalAsync()
    {
        try
        {
            string json = await File.ReadAllTextAsync(_historyFilePath);
            List<DeploymentHistoryEntry>? history = JsonSerializer.Deserialize<List<DeploymentHistoryEntry>>(json, _jsonOptions);
            return history ?? throw new JsonException("Deployment history must contain an array.");
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    /// <summary>
    /// Saves history to the JSON file.
    /// Replaces the destination only after the complete JSON has been written.
    /// </summary>
    private async Task SaveHistoryInternalAsync(List<DeploymentHistoryEntry> history)
    {
        string tempPath = _historyFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Ensure directory exists before writing
            string? directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(history, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _historyFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Releases all resources used by the DeploymentHistoryService.
    /// </summary>
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
            _fileLock.Dispose();
        }

        _disposed = true;
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
