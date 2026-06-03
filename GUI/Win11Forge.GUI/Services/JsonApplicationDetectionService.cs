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
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;
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
    private readonly ILoggingService _logger;

    private const int CommandTimeoutMs = 5000;

    /// <summary>
    /// Regex pattern for validating Windows feature names.
    /// Only allows alphanumeric characters, hyphens, and underscores.
    /// </summary>
    private static readonly Regex ValidFeatureNamePattern = new(
        @"^[a-zA-Z0-9\-_]+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Regex timeout for version extraction patterns to prevent ReDoS attacks.
    /// Intentionally not configurable: surfacing this to user config would let an
    /// attacker who controls the configuration disable the protection entirely.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

    public JsonApplicationDetectionService(IRepositoryPathService pathService, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<JsonApplicationDetectionService>();

        _databasePath = pathService.GetPath(
            Win11ForgePathNames.AppsDirectoryName,
            Win11ForgePathNames.DatabaseDirectoryName,
            Win11ForgePathNames.ApplicationsDatabaseFileName);
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
            _logger.LogError($"Detection error for {appId}", ex);
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
            (RegistryKey? hive, string? subKey) = ParseRegistryPath(config.Path);
            if (hive == null || subKey == null) return null;

            using RegistryKey? key = hive.OpenSubKey(subKey);
            if (key == null) return null;

            string? version = null;

            // Get version from VersionKey if specified
            if (!string.IsNullOrEmpty(config.VersionKey))
            {
                object? versionValue = key.GetValue(config.VersionKey);
                if (versionValue != null)
                {
                    version = versionValue.ToString();

                    // Apply regex if specified (with timeout to prevent ReDoS)
                    if (!string.IsNullOrEmpty(config.VersionRegex) && version != null)
                    {
                        try
                        {
                            Match match = Regex.Match(version, config.VersionRegex, RegexOptions.None, RegexTimeout);
                            if (match.Success && match.Groups.Count > 1)
                            {
                                version = match.Groups[1].Value;
                            }
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            _logger.LogWarning($"Version regex timed out for {appId} - possible ReDoS pattern");
                        }
                    }
                }
            }
            // Check for specific registry value
            else if (!string.IsNullOrEmpty(config.RegistryValue))
            {
                object? value = key.GetValue(config.RegistryValue);
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
            _logger.LogError($"Registry detection failed for {appId}", ex);
            return null;
        }
    }

    /// <summary>
    /// Parses a registry path like "HKLM:\SOFTWARE\..." into hive and subkey.
    /// </summary>
    private static (RegistryKey? Hive, string? SubKey) ParseRegistryPath(string path)
    {
        // Handle PowerShell-style paths (HKLM:\, HKCU:\, etc.)
        string normalizedPath = path.Replace("\\", "\\").TrimEnd('\\');

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
            string[] parts = config.Command.Split(' ', 2);
            string executable = parts[0];
            string args = parts.Length > 1 ? parts[1] : "";

            // Try to resolve common executables to full paths
            executable = ResolveExecutablePath(executable);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = psi };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Command not found = not installed
                _logger.LogWarning($"Command detection: '{executable}' not found for {appId}: {ex.Message}");
                return null;
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();

            // Some programs output version to stderr (like java -version)
            string combinedOutput = output + "\n" + errorOutput;

            bool completed = process.WaitForExit(CommandTimeoutMs);
            if (!completed)
            {
                try { process.Kill(); }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Process kill failed (non-critical): {ex.Message}");
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
                string searchText = combinedOutput;
                if (!string.IsNullOrEmpty(config.Arguments))
                {
                    string[] lines = combinedOutput.Split('\n');
                    string? matchingLine = lines.FirstOrDefault(l =>
                        l.Contains(config.Arguments, StringComparison.OrdinalIgnoreCase));
                    if (matchingLine != null)
                    {
                        searchText = matchingLine;
                    }
                }

                try
                {
                    Match match = Regex.Match(searchText, config.VersionRegex, RegexOptions.None, RegexTimeout);
                    if (match.Success)
                    {
                        version = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.LogWarning($"Version regex timed out for {appId} - possible ReDoS pattern");
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
            _logger.LogError($"Command detection failed for {appId}", ex);
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
            string expandedPath = Environment.ExpandEnvironmentVariables(config.Path);

            // Security: Validate expanded path for safety
            if (!IsValidExpandedPath(expandedPath))
            {
                _logger.LogWarning($"Security: Invalid expanded path for {appId}: {expandedPath}");
                return null;
            }

            // Security: Removed File.Exists check to prevent TOCTOU race condition
            // FileVersionInfo.GetVersionInfo will throw if file doesn't exist, which is caught below
            string? version = null;

            // Try to get file version - handles file not found atomically
            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(expandedPath);
                version = versionInfo.FileVersion ?? versionInfo.ProductVersion;
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist - not installed
                return null;
            }
            catch
            {
                // File exists but version info unavailable
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
            _logger.LogError($"File detection failed for {appId}", ex);
            return null;
        }
    }

    /// <summary>
    /// Validates an expanded file path for security.
    /// Blocks paths with dangerous patterns that could result from malicious environment variables.
    /// </summary>
    private bool IsValidExpandedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Block null bytes
        if (path.Contains('\0')) return false;

        // Block unexpanded environment variables (indicates potential attack or misconfiguration)
        if (path.Contains('%')) return false;

        // Security: Block path traversal sequences before and after normalization
        if (path.Contains(".."))
        {
            _logger.LogWarning("Security: Path traversal blocked in pre-normalized path");
            return false;
        }

        // Block command injection characters
        char[] dangerousChars = new[] { ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'' };
        if (path.IndexOfAny(dangerousChars) >= 0) return false;

        // Validate it's a plausible file path (contains drive letter or UNC path)
        if (!Path.IsPathRooted(path)) return false;

        // Security: Normalize the path and verify it doesn't escape to unexpected locations
        try
        {
            string normalizedPath = Path.GetFullPath(path);

            // After normalization, ensure no path traversal remains
            if (normalizedPath.Contains(".."))
            {
                _logger.LogWarning("Security: Path traversal blocked in normalized path");
                return false;
            }

            // Ensure normalized path starts with expected root (system drive or Program Files)
            string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string systemDrive = windowsPath?.Length >= 3 ? windowsPath.Substring(0, 3) : "C:\\";
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] allowedRoots = new[] { programFiles, programFilesX86, localAppData, appData, userProfile, systemDrive };

            bool isAllowed = false;
            foreach (string? root in allowedRoots)
            {
                if (!string.IsNullOrEmpty(root) && normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                _logger.LogWarning($"Security: Path outside allowed roots blocked: {normalizedPath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Security: Path normalization failed: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Detects Windows feature.
    /// </summary>
    private async Task<InstalledPackageInfo?> DetectWindowsFeatureAsync(string appId, string appName, DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.FeatureName)) return null;

        // Security: Validate feature name to prevent command injection
        // Windows feature names only contain alphanumeric characters, hyphens, and underscores
        if (!ValidFeatureNamePattern.IsMatch(config.FeatureName))
        {
            _logger.LogWarning($"Invalid Windows feature name rejected for security: {appId}");
            return null;
        }

        try
        {
            // Security: Use -EncodedCommand with Base64 encoding to prevent command injection
            // This is more secure than escaping quotes in string interpolation
            string command = $"(Get-WindowsOptionalFeature -Online -FeatureName '{config.FeatureName}').State";
            byte[] commandBytes = System.Text.Encoding.Unicode.GetBytes(command);
            string encodedCommand = Convert.ToBase64String(commandBytes);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -EncodedCommand {encodedCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,  // Security: Redirect stderr to prevent deadlock
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return null;

            // Read both stdout and stderr to prevent deadlock
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            // Wait for both streams with timeout
            bool completedInTime = process.WaitForExit(CommandTimeoutMs);

            string output = await outputTask;
            string error = await errorTask;  // Consume stderr even if not used

            if (!completedInTime)
            {
                _logger.LogWarning($"Windows feature detection timed out for {appId}");
                return null;
            }

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning($"Windows feature detection stderr for {appId}: {error}");
            }

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
            _logger.LogError($"Windows feature detection failed for {appId}", ex);
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

        string executableLower = executable.ToLowerInvariant();
        string? pathResolvedExecutable = ResolveExecutableFromPath(executable);
        if (!string.IsNullOrEmpty(pathResolvedExecutable))
        {
            return pathResolvedExecutable;
        }

        // Common executable paths
        Dictionary<string, string[]> knownPaths = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
            },
            ["codex"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.ps1"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "codex.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "codex.exe")
            },
            ["claude"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.ps1")
            },
            ["agy"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "agy.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "agy.exe")
            },
            ["ollama"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe")
            },
            ["aish"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "aish.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "aish.exe")
            }
        };

        // Check if we have known paths for this executable
        if (knownPaths.TryGetValue(executableLower, out string[]? paths))
        {
            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }
        }

        // Return original - will rely on PATH
        return executable;
    }

    private static string? ResolveExecutableFromPath(string executable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        IReadOnlyList<string> extensions = GetExecutableExtensions(executable);
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory.Trim(), executable + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetExecutableExtensions(string executable)
    {
        if (!string.IsNullOrEmpty(Path.GetExtension(executable)))
        {
            return [string.Empty];
        }

        string? pathext = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathext))
        {
            return [".exe", ".cmd", ".bat", ".ps1", string.Empty];
        }

        return pathext.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
