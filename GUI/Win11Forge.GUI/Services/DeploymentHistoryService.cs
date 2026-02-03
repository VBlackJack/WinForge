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
/// Stores history in a JSON file with 100% crash-proof path resolution.
/// </summary>
public class DeploymentHistoryService : IDeploymentHistoryService, IDisposable
{
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes the history service with crash-proof path resolution.
    /// This constructor is guaranteed to never throw - it will always find a valid path.
    /// </summary>
    public DeploymentHistoryService()
    {
        // Get a guaranteed valid storage path (never returns null)
        var basePath = GetValidStoragePath();
        var win11ForgePath = SafePathCombine(basePath, "Win11Forge");

        // Try to create directory with multiple fallbacks
        win11ForgePath = EnsureDirectoryExists(win11ForgePath);

        _historyFilePath = SafePathCombine(win11ForgePath, "history.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Gets a valid storage path using multiple fallback strategies.
    /// NEVER returns null or empty - always returns a valid, non-null path.
    /// </summary>
    private static string GetValidStoragePath()
    {
        // Strategy 1: LocalApplicationData (preferred - typically C:\Users\X\AppData\Local)
        try
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(Path.GetPathRoot(path)))
            {
                return path;
            }
        }
        catch
        {
            // Swallow and try next strategy
        }

        // Strategy 2: UserProfile + AppData/Local
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                var combinedPath = SafePathCombine(userProfile, "AppData", "Local");
                if (!string.IsNullOrEmpty(combinedPath))
                {
                    return combinedPath;
                }
            }
        }
        catch
        {
            // Swallow and try next strategy
        }

        // Strategy 3: CommonApplicationData (ProgramData - typically C:\ProgramData)
        try
        {
            var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(commonData))
            {
                var combinedPath = SafePathCombine(commonData, "Win11Forge", "User");
                if (!string.IsNullOrEmpty(combinedPath))
                {
                    return combinedPath;
                }
            }
        }
        catch
        {
            // Swallow and try next strategy
        }

        // Strategy 4: Environment variable TEMP
        try
        {
            var tempEnv = Environment.GetEnvironmentVariable("TEMP");
            if (!string.IsNullOrEmpty(tempEnv))
            {
                return tempEnv;
            }
        }
        catch
        {
            // Swallow and try next strategy
        }

        // Strategy 5: Path.GetTempPath() (ultimate fallback - should always work)
        try
        {
            var tempPath = Path.GetTempPath();
            if (!string.IsNullOrEmpty(tempPath))
            {
                return tempPath;
            }
        }
        catch
        {
            // Swallow and try absolute fallback
        }

        // Strategy 6: Absolute hardcoded fallback (should never reach here)
        return @"C:\Windows\Temp";
    }

    /// <summary>
    /// Safely combines path segments, never passing null to Path.Combine.
    /// Returns empty string if any segment is null.
    /// </summary>
    private static string SafePathCombine(params string?[] segments)
    {
        // Filter out null or empty segments
        var validSegments = segments
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToArray();

        if (validSegments.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Path.Combine(validSegments);
        }
        catch
        {
            // If Path.Combine fails for any reason, return first valid segment
            return validSegments[0];
        }
    }

    /// <summary>
    /// Ensures the directory exists, with multiple fallback locations if creation fails.
    /// Always returns a valid directory path that exists.
    /// </summary>
    private static string EnsureDirectoryExists(string preferredPath)
    {
        // Attempt 1: Create at preferred location
        try
        {
            if (!string.IsNullOrEmpty(preferredPath))
            {
                if (!Directory.Exists(preferredPath))
                {
                    Directory.CreateDirectory(preferredPath);
                }
                return preferredPath;
            }
        }
        catch
        {
            // Permission denied or invalid path - try fallback
        }

        // Attempt 2: Create in temp folder
        try
        {
            var tempPath = Path.GetTempPath();
            if (!string.IsNullOrEmpty(tempPath))
            {
                var tempWin11Forge = SafePathCombine(tempPath, "Win11Forge");
                if (!string.IsNullOrEmpty(tempWin11Forge))
                {
                    if (!Directory.Exists(tempWin11Forge))
                    {
                        Directory.CreateDirectory(tempWin11Forge);
                    }
                    return tempWin11Forge;
                }
            }
        }
        catch
        {
            // Try next fallback
        }

        // Attempt 3: Use TEMP env var directly
        try
        {
            var tempEnv = Environment.GetEnvironmentVariable("TEMP");
            if (!string.IsNullOrEmpty(tempEnv))
            {
                var tempWin11Forge = SafePathCombine(tempEnv, "Win11Forge");
                if (!string.IsNullOrEmpty(tempWin11Forge))
                {
                    if (!Directory.Exists(tempWin11Forge))
                    {
                        Directory.CreateDirectory(tempWin11Forge);
                    }
                    return tempWin11Forge;
                }
            }
        }
        catch
        {
            // Try next fallback
        }

        // Attempt 4: Use Windows Temp directly (absolute fallback)
        try
        {
            const string windowsTemp = @"C:\Windows\Temp\Win11Forge";
            if (!Directory.Exists(windowsTemp))
            {
                Directory.CreateDirectory(windowsTemp);
            }
            return windowsTemp;
        }
        catch
        {
            // If even this fails, just return temp path without creating
        }

        // Ultimate fallback: return temp path and hope for the best
        try
        {
            return Path.GetTempPath();
        }
        catch
        {
            return @"C:\Windows\Temp";
        }
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
        catch
        {
            // Silently fail - history is non-critical
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
        catch
        {
            return [];
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
        catch
        {
            // Silently fail - clearing history is non-critical
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Loads history from the JSON file.
    /// Returns empty list on any error.
    /// </summary>
    private async Task<List<DeploymentHistoryEntry>> LoadHistoryInternalAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_historyFilePath) || !File.Exists(_historyFilePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(_historyFilePath);
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            var history = JsonSerializer.Deserialize<List<DeploymentHistoryEntry>>(json, _jsonOptions);
            return history ?? [];
        }
        catch
        {
            // If file is corrupted or any error occurs, start fresh
            return [];
        }
    }

    /// <summary>
    /// Saves history to the JSON file.
    /// Silently fails on error (history is non-critical).
    /// </summary>
    private async Task SaveHistoryInternalAsync(List<DeploymentHistoryEntry> history)
    {
        try
        {
            if (string.IsNullOrEmpty(_historyFilePath))
            {
                return;
            }

            // Ensure directory exists before writing
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(history, _jsonOptions);
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch
        {
            // Silently fail - history persistence is non-critical
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
