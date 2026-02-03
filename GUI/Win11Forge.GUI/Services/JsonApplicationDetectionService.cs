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

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Win11Forge.GUI.Models;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service that performs application detection using the methods defined in applications.json.
/// Supports Registry, Command, and File-based detection methods.
/// This enables proper detection of runtimes (.NET, Java, VC++ Redist, etc.) that use
/// custom registry paths or command-based detection.
/// </summary>
public class JsonApplicationDetectionService
{
    private Dictionary<string, ApplicationJsonEntry>? _applicationDatabase;
    private Dictionary<string, string>? _wingetIdToJsonKey;
    private readonly object _loadLock = new();
    private readonly string _databasePath;

    private const int CommandTimeoutMs = 5000;

    public JsonApplicationDetectionService()
    {
        // Find applications.json relative to the executable or repo root
        var basePath = AppDomain.CurrentDomain.BaseDirectory;

        // Try multiple possible locations
        // Structure: Win11Forge\GUI\ (exe here) and Win11Forge\Apps\Database\ (json here)
        var possiblePaths = new[]
        {
            // Published/installed: GUI\..\Apps\Database\
            Path.Combine(basePath, "..", "Apps", "Database", "applications.json"),
            // Development: GUI\bin\Debug\net8.0-windows\..\..\..\..\..\..\Apps\Database\
            Path.Combine(basePath, "..", "..", "..", "..", "..", "..", "Apps", "Database", "applications.json"),
            // Alternative development path
            Path.Combine(basePath, "..", "..", "..", "..", "Apps", "Database", "applications.json"),
            // Installed in Program Files
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Win11Forge", "Apps", "Database", "applications.json"),
            // Same folder as exe (fallback)
            Path.Combine(basePath, "Apps", "Database", "applications.json")
        };

        _databasePath = possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
        Debug.WriteLine($"JsonApplicationDetectionService: Using database path: {_databasePath}");
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
                    Debug.WriteLine($"Applications database not found at: {_databasePath}");
                    _applicationDatabase = new Dictionary<string, ApplicationJsonEntry>();
                    _wingetIdToJsonKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                var json = File.ReadAllText(_databasePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                // Parse the root structure which contains "Applications" property
                var root = JsonSerializer.Deserialize<ApplicationsDatabase>(json, options);

                _applicationDatabase = root?.Applications
                    ?? new Dictionary<string, ApplicationJsonEntry>();

                // Build reverse lookup: WinGet ID -> JSON key
                _wingetIdToJsonKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var appsWithDetection = 0;
                foreach (var kvp in _applicationDatabase)
                {
                    var wingetId = kvp.Value.Sources?.Winget;
                    if (!string.IsNullOrEmpty(wingetId))
                    {
                        _wingetIdToJsonKey[wingetId] = kvp.Key;
                    }
                    // Also index by Chocolatey ID
                    var chocoId = kvp.Value.Sources?.Chocolatey;
                    if (!string.IsNullOrEmpty(chocoId) && !_wingetIdToJsonKey.ContainsKey(chocoId))
                    {
                        _wingetIdToJsonKey[chocoId] = kvp.Key;
                    }
                    if (kvp.Value.Detection != null)
                    {
                        appsWithDetection++;
                    }
                }

                Debug.WriteLine($"Loaded {_applicationDatabase.Count} applications from database ({appsWithDetection} with detection methods)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading applications database: {ex.Message}");
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
        if (_applicationDatabase!.TryGetValue(appId, out var app) && app.Detection != null)
        {
            var primaryId = app.GetPrimaryId(appId);
            return await DetectAsync(primaryId, app.Name, app.Detection);
        }

        // Try reverse lookup (WinGet/Chocolatey ID)
        if (_wingetIdToJsonKey!.TryGetValue(appId, out var jsonKey) &&
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

        var results = new Dictionary<string, InstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<KeyValuePair<string, ApplicationJsonEntry>> appsToCheck;

        if (appIds != null)
        {
            // Filter by provided IDs - could be JSON keys or WinGet IDs
            var appIdSet = new HashSet<string>(appIds, StringComparer.OrdinalIgnoreCase);
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
        var semaphore = new SemaphoreSlim(4);
        var tasks = appsToCheck.Select(async kvp =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Use WinGet ID as the key for results (matches ApplicationModel.AppId)
                var primaryId = kvp.Value.GetPrimaryId(kvp.Key);
                var result = await DetectAsync(primaryId, kvp.Value.Name, kvp.Value.Detection!);
                return (primaryId, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var detectionResults = await Task.WhenAll(tasks);

        foreach (var (appId, info) in detectionResults)
        {
            if (info != null)
            {
                results[appId] = info;
                Debug.WriteLine($"JSON detection: {appId} = INSTALLED (v{info.InstalledVersion}, source: {info.Source})");
            }
        }

        Debug.WriteLine($"JSON detection complete: {results.Count} installed out of {detectionResults.Length} checked");
        return results;
    }

    /// <summary>
    /// Performs detection based on the configured method.
    /// </summary>
    private async Task<InstalledPackageInfo?> DetectAsync(string appId, string appName, DetectionConfiguration config)
    {
        try
        {
            return config.Method switch
            {
                DetectionMethodStrings.Registry => DetectRegistry(appId, appName, config),
                DetectionMethodStrings.Command => await DetectCommandAsync(appId, appName, config),
                DetectionMethodStrings.File => DetectFile(appId, appName, config),
                DetectionMethodStrings.WindowsFeature => await DetectWindowsFeatureAsync(appId, appName, config),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Detection error for {appId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detects application via registry.
    /// </summary>
    private InstalledPackageInfo? DetectRegistry(string appId, string appName, DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.Path)) return null;

        try
        {
            // Parse registry path (supports HKLM:, HKCU:, etc.)
            var (hive, subKey) = ParseRegistryPath(config.Path);
            if (hive == null || subKey == null) return null;

            using var key = hive.OpenSubKey(subKey);
            if (key == null) return null;

            string? version = null;

            // Get version from VersionKey if specified
            if (!string.IsNullOrEmpty(config.VersionKey))
            {
                var versionValue = key.GetValue(config.VersionKey);
                if (versionValue != null)
                {
                    version = versionValue.ToString();

                    // Apply regex if specified
                    if (!string.IsNullOrEmpty(config.VersionRegex) && version != null)
                    {
                        var match = Regex.Match(version, config.VersionRegex);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            version = match.Groups[1].Value;
                        }
                    }
                }
            }
            // Check for specific registry value
            else if (!string.IsNullOrEmpty(config.RegistryValue))
            {
                var value = key.GetValue(config.RegistryValue);
                if (value == null) return null;

                // If ExpectedValue is set, check for match
                if (!string.IsNullOrEmpty(config.ExpectedValue))
                {
                    if (value.ToString() != config.ExpectedValue)
                    {
                        return null;
                    }
                }
                version = value.ToString();
            }
            // Just checking if key exists
            else
            {
                version = Loc.Status_Installed;
            }

            return new InstalledPackageInfo
            {
                Id = appId,
                Name = appName,
                InstalledVersion = version ?? Loc.Status_Installed,
                Source = DetectionSource.Registry
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry detection failed for {appId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a registry path like "HKLM:\SOFTWARE\..." into hive and subkey.
    /// </summary>
    private static (RegistryKey? Hive, string? SubKey) ParseRegistryPath(string path)
    {
        // Handle PowerShell-style paths (HKLM:\, HKCU:\, etc.)
        var normalizedPath = path.Replace("\\", "\\").TrimEnd('\\');

        RegistryKey? hive = null;
        string? subKey = null;

        if (normalizedPath.StartsWith("HKLM:\\", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.LocalMachine;
            subKey = normalizedPath.Contains(":\\")
                ? normalizedPath[(normalizedPath.IndexOf(":\\", StringComparison.Ordinal) + 2)..]
                : normalizedPath[(normalizedPath.IndexOf('\\') + 1)..];
        }
        else if (normalizedPath.StartsWith("HKCU:\\", StringComparison.OrdinalIgnoreCase) ||
                 normalizedPath.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.CurrentUser;
            subKey = normalizedPath.Contains(":\\")
                ? normalizedPath[(normalizedPath.IndexOf(":\\", StringComparison.Ordinal) + 2)..]
                : normalizedPath[(normalizedPath.IndexOf('\\') + 1)..];
        }

        return (hive, subKey);
    }

    /// <summary>
    /// Detects application via command execution.
    /// </summary>
    private async Task<InstalledPackageInfo?> DetectCommandAsync(string appId, string appName, DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.Command)) return null;

        try
        {
            // Parse command and arguments
            var parts = config.Command.Split(' ', 2);
            var executable = parts[0];
            var args = parts.Length > 1 ? parts[1] : "";

            // Try to resolve common executables to full paths
            executable = ResolveExecutablePath(executable);

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Command not found = not installed
                Debug.WriteLine($"Command detection: '{executable}' not found for {appId}: {ex.Message}");
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();

            // Some programs output version to stderr (like java -version)
            var combinedOutput = output + "\n" + errorOutput;

            var completed = process.WaitForExit(CommandTimeoutMs);
            if (!completed)
            {
                try { process.Kill(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Process kill failed (non-critical): {ex.Message}");
                }
                return null;
            }

            // If Arguments is set, it's a filter that must be present in output
            // This is used for things like "dotnet --list-runtimes" with "Microsoft.WindowsDesktop.App 8"
            if (!string.IsNullOrEmpty(config.Arguments))
            {
                if (!combinedOutput.Contains(config.Arguments, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            string? version = null;

            // Extract version using regex
            if (!string.IsNullOrEmpty(config.VersionRegex))
            {
                // For filtered output, try to find version on the line containing the filter
                var searchText = combinedOutput;
                if (!string.IsNullOrEmpty(config.Arguments))
                {
                    var lines = combinedOutput.Split('\n');
                    var matchingLine = lines.FirstOrDefault(l =>
                        l.Contains(config.Arguments, StringComparison.OrdinalIgnoreCase));
                    if (matchingLine != null)
                    {
                        searchText = matchingLine;
                    }
                }

                var match = Regex.Match(searchText, config.VersionRegex);
                if (match.Success)
                {
                    version = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                }
            }

            return new InstalledPackageInfo
            {
                Id = appId,
                Name = appName,
                InstalledVersion = version ?? Loc.Status_Installed,
                Source = DetectionSource.Command
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Command detection failed for {appId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detects application via file existence.
    /// </summary>
    private InstalledPackageInfo? DetectFile(string appId, string appName, DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.Path)) return null;

        try
        {
            // Expand environment variables
            var expandedPath = Environment.ExpandEnvironmentVariables(config.Path);

            if (!File.Exists(expandedPath)) return null;

            string? version = null;

            // Try to get file version
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(expandedPath);
                version = versionInfo.FileVersion ?? versionInfo.ProductVersion;
            }
            catch
            {
                version = Loc.Status_Installed;
            }

            return new InstalledPackageInfo
            {
                Id = appId,
                Name = appName,
                InstalledVersion = version ?? Loc.Status_Installed,
                Source = DetectionSource.File
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File detection failed for {appId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detects Windows feature.
    /// </summary>
    private async Task<InstalledPackageInfo?> DetectWindowsFeatureAsync(string appId, string appName, DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.FeatureName)) return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"(Get-WindowsOptionalFeature -Online -FeatureName '{config.FeatureName}').State\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit(CommandTimeoutMs);

            if (output.Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase))
            {
                return new InstalledPackageInfo
                {
                    Id = appId,
                    Name = appName,
                    InstalledVersion = "enabled",
                    Source = DetectionSource.WindowsFeature
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Windows feature detection failed for {appId}: {ex.Message}");
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

    /// <summary>
    /// Resolves common executable names to their full paths.
    /// This helps when the executable isn't in the GUI process's PATH.
    /// </summary>
    private static string ResolveExecutablePath(string executable)
    {
        // If it's already a full path, return it
        if (Path.IsPathRooted(executable) && File.Exists(executable))
            return executable;

        var executableLower = executable.ToLowerInvariant();

        // Common executable paths
        var knownPaths = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe")
            },
            ["java"] = new[]
            {
                // Check JAVA_HOME first
                Environment.GetEnvironmentVariable("JAVA_HOME") is string javaHome && !string.IsNullOrEmpty(javaHome)
                    ? Path.Combine(javaHome, "bin", "java.exe")
                    : "",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java", "jdk-21", "bin", "java.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium", "jdk-21", "bin", "java.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "jdk-21", "bin", "java.exe")
            },
            ["node"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe")
            },
            ["python"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python312", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311", "python.exe")
            },
            ["git"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe")
            }
        };

        // Check if we have known paths for this executable
        if (knownPaths.TryGetValue(executableLower, out var paths))
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Debug.WriteLine($"Resolved '{executable}' to '{path}'");
                    return path;
                }
            }
        }

        // Return original - will rely on PATH
        return executable;
    }
}
