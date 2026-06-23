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

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using WinForge.GUI.Configuration;
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services;

/// <summary>
/// Shared detection engine for probing ad-hoc and catalog detection configurations.
/// </summary>
public sealed class DetectionProbe : IDetectionProbe
{
    private const int CommandTimeoutMs = 5000;
    private const string WindowsFeatureElevationRequiredDetail =
        "Administrator privileges are required to query Windows features.";

    private static readonly string[] WindowsFeatureElevationErrorMarkers =
    [
        "requires elevation",
        "elevated",
        "administrator",
        "administrateur",
        "access is denied",
        "denied",
        "refus",
        "privilege",
        "privil"
    ];

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

    /// <summary>
    /// Name of the JSON array property holding the Command detection allowlist.
    /// </summary>
    private const string AllowedExecutablesPropertyName = "allowedExecutables";

    /// <summary>
    /// Names of the JSON array properties holding the registry-path validation policy.
    /// </summary>
    private const string AllowedRegistryPatternsPropertyName = "allowedPatterns";
    private const string BlockedRegistryPatternsPropertyName = "blockedPatterns";

    /// <summary>
    /// Maximum registry path length accepted (guards against DoS via very deep paths).
    /// </summary>
    private const int MaxRegistryPathLength = 512;

    private readonly ILoggingService _logger;

    /// <summary>
    /// Base names of executables permitted for the Command detection method.
    /// Loaded fail-closed: any load failure yields an empty set, which denies
    /// every Command detection rather than allowing all.
    /// </summary>
    private readonly HashSet<string> _allowedDetectionExecutables;

    /// <summary>
    /// Registry-path validation patterns, read from the same Config file as the
    /// PowerShell stack so the two cannot drift. Loaded fail-closed: any load failure
    /// yields an empty allow-list, which denies every Registry detection rather than
    /// allowing all (never fails open on an empty block-list).
    /// </summary>
    private readonly IReadOnlyList<Regex> _allowedRegistryPatterns;
    private readonly IReadOnlyList<Regex> _blockedRegistryPatterns;

    /// <summary>
    /// Initializes a new instance of the detection probe.
    /// </summary>
    public DetectionProbe(
        ILoggerFactory? loggerFactory = null,
        IRepositoryPathService? repositoryPathService = null)
    {
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<DetectionProbe>();
        _allowedDetectionExecutables = LoadAllowedDetectionExecutables(repositoryPathService);
        (_allowedRegistryPatterns, _blockedRegistryPatterns) = LoadRegistryPolicy(repositoryPathService);
    }

    /// <summary>
    /// Loads the Command detection allowlist from the shared configuration file.
    /// This is a security control: it is loaded once and cached for the probe's
    /// lifetime. It fails closed - a missing, unreadable, or malformed file
    /// produces an empty allowlist, disabling Command detection entirely rather
    /// than permitting arbitrary executables.
    /// </summary>
    private HashSet<string> LoadAllowedDetectionExecutables(IRepositoryPathService? repositoryPathService)
    {
        HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (repositoryPathService is null)
        {
            _logger.LogWarning(
                "Detection allowlist unavailable (no repository path service); Command detection is disabled.");
            return allowed;
        }

        string allowlistPath = repositoryPathService.GetPath(
            WinForgePathNames.ConfigDirectoryName,
            WinForgePathNames.DetectionAllowlistFileName);

        if (!File.Exists(allowlistPath))
        {
            _logger.LogError(
                $"Detection allowlist file not found at '{allowlistPath}'; Command detection is disabled.");
            return allowed;
        }

        try
        {
            string json = File.ReadAllText(allowlistPath);
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty(AllowedExecutablesPropertyName, out JsonElement executables) &&
                executables.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in executables.EnumerateArray())
                {
                    string? value = entry.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        allowed.Add(value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load detection allowlist; Command detection is disabled.", ex);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return allowed;
    }

    /// <summary>
    /// Loads the registry-path validation policy from the shared configuration file.
    /// Fails closed: a missing, unreadable, or malformed file (or one with no usable
    /// allowed patterns) produces an empty allow-list, denying every Registry
    /// detection rather than permitting arbitrary registry reads.
    /// </summary>
    private (IReadOnlyList<Regex> Allowed, IReadOnlyList<Regex> Blocked) LoadRegistryPolicy(
        IRepositoryPathService? repositoryPathService)
    {
        List<Regex> allowed = new List<Regex>();
        List<Regex> blocked = new List<Regex>();

        if (repositoryPathService is null)
        {
            _logger.LogWarning(
                "Registry detection policy unavailable (no repository path service); Registry detection is disabled.");
            return (allowed, blocked);
        }

        string policyPath = repositoryPathService.GetPath(
            WinForgePathNames.ConfigDirectoryName,
            WinForgePathNames.DetectionRegistryPolicyFileName);

        if (!File.Exists(policyPath))
        {
            _logger.LogError(
                $"Registry detection policy not found at '{policyPath}'; Registry detection is disabled.");
            return (allowed, blocked);
        }

        try
        {
            string json = File.ReadAllText(policyPath);
            using JsonDocument document = JsonDocument.Parse(json);
            allowed = CompileRegistryPatterns(document.RootElement, AllowedRegistryPatternsPropertyName);
            blocked = CompileRegistryPatterns(document.RootElement, BlockedRegistryPatternsPropertyName);

            if (allowed.Count == 0)
            {
                _logger.LogError(
                    $"Registry detection policy '{policyPath}' has no usable allowed patterns; Registry detection is disabled.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load registry detection policy; Registry detection is disabled.", ex);
            return (new List<Regex>(), new List<Regex>());
        }

        return (allowed, blocked);
    }

    /// <summary>
    /// Compiles the regex patterns from a named JSON array property. Patterns are
    /// matched case-insensitively, mirroring the PowerShell -match operator.
    /// </summary>
    private static List<Regex> CompileRegistryPatterns(JsonElement root, string propertyName)
    {
        List<Regex> patterns = new List<Regex>();

        if (root.TryGetProperty(propertyName, out JsonElement array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in array.EnumerateArray())
            {
                string? value = entry.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    patterns.Add(new Regex(
                        value,
                        RegexOptions.IgnoreCase | RegexOptions.Compiled,
                        RegexTimeout));
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// Validates a registry path against the shared allow/block policy. A path is
    /// allowed only when it matches an allowed pattern and no blocked pattern. With
    /// an empty allow-list (fail-closed load) every path is denied.
    /// </summary>
    private bool IsRegistryPathAllowed(string path)
    {
        string normalizedPath = path.Replace('/', '\\');

        try
        {
            foreach (Regex blocked in _blockedRegistryPatterns)
            {
                if (blocked.IsMatch(normalizedPath))
                {
                    return false;
                }
            }

            if (normalizedPath.Length > MaxRegistryPathLength)
            {
                return false;
            }

            foreach (Regex allowed in _allowedRegistryPatterns)
            {
                if (allowed.IsMatch(normalizedPath))
                {
                    return true;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed on a pathological path that times out a pattern match.
            return false;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<DetectionProbeResult> ProbeAsync(
        DetectionConfiguration config,
        PathValidationPolicy pathPolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return config.Method.ToDetectionMethod() switch
            {
                DetectionMethod.Registry => DetectRegistry(config),
                DetectionMethod.Command => await DetectCommandAsync(config, cancellationToken),
                DetectionMethod.File => DetectFile(config, pathPolicy),
                DetectionMethod.WindowsFeature => await DetectWindowsFeatureAsync(config, cancellationToken),
                _ => DetectionProbeResult.Unsupported($"Unsupported detection method: {config.Method}")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Detection probe failed", ex);
            return DetectionProbeResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Detects application via registry.
    /// </summary>
    private DetectionProbeResult DetectRegistry(DetectionConfiguration config)
    {
        if (string.IsNullOrEmpty(config.Path))
        {
            return DetectionProbeResult.InvalidInput("Registry path is required.");
        }

        try
        {
            // Security: validate the path against the shared registry policy before
            // opening any key (mirrors the PowerShell DetectionRegistryGuard, same file).
            if (!IsRegistryPathAllowed(config.Path))
            {
                _logger.LogWarning(
                    $"Registry detection blocked: '{config.Path}' is not allowed by the registry policy.");
                return DetectionProbeResult.NotFound("Registry path is not allowed for detection.");
            }

            (RegistryKey? hive, string? subKey) = ParseRegistryPath(config.Path);
            if (hive == null || subKey == null)
            {
                return DetectionProbeResult.InvalidInput("Unsupported registry hive.");
            }

            using RegistryKey? key = hive.OpenSubKey(subKey);
            if (key == null)
            {
                return DetectionProbeResult.NotFound();
            }

            string? version = null;

            if (!string.IsNullOrEmpty(config.VersionKey))
            {
                object? versionValue = key.GetValue(config.VersionKey);
                if (versionValue == null)
                {
                    return DetectionProbeResult.NotFound();
                }

                version = versionValue.ToString();

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
                        _logger.LogWarning("Version regex timed out during registry detection - possible ReDoS pattern");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(config.RegistryValue))
            {
                object? value = key.GetValue(config.RegistryValue);
                if (value == null)
                {
                    return DetectionProbeResult.NotFound();
                }

                if (!string.IsNullOrEmpty(config.ExpectedValue) &&
                    value.ToString() != config.ExpectedValue)
                {
                    return DetectionProbeResult.NotFound();
                }

                version = value.ToString();
            }

            return DetectionProbeResult.Found(DetectionSource.Registry, version);
        }
        catch (Exception ex)
        {
            _logger.LogError("Registry detection failed", ex);
            return DetectionProbeResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Parses a registry path like "HKLM:\SOFTWARE\..." into hive and subkey.
    /// </summary>
    private static (RegistryKey? Hive, string? SubKey) ParseRegistryPath(string path)
    {
        // XM1: real separator normalization (the previous Replace("\\","\\") was a no-op).
        string normalizedPath = path.Replace('/', '\\').TrimEnd('\\');

        RegistryKey? hive = null;
        string? subKey = null;

        if (normalizedPath.StartsWith("HKLM:\\", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.LocalMachine;
            subKey = normalizedPath.Contains(":\\", StringComparison.Ordinal)
                ? normalizedPath[(normalizedPath.IndexOf(":\\", StringComparison.Ordinal) + 2)..]
                : normalizedPath[(normalizedPath.IndexOf('\\') + 1)..];
        }
        else if (normalizedPath.StartsWith("HKCU:\\", StringComparison.OrdinalIgnoreCase) ||
                 normalizedPath.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.CurrentUser;
            subKey = normalizedPath.Contains(":\\", StringComparison.Ordinal)
                ? normalizedPath[(normalizedPath.IndexOf(":\\", StringComparison.Ordinal) + 2)..]
                : normalizedPath[(normalizedPath.IndexOf('\\') + 1)..];
        }

        return (hive, subKey);
    }

    /// <summary>
    /// Detects application via command execution.
    /// </summary>
    private async Task<DetectionProbeResult> DetectCommandAsync(
        DetectionConfiguration config,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.Command))
        {
            return DetectionProbeResult.InvalidInput("Command is required.");
        }

        try
        {
            string[] parts = config.Command.Split(' ', 2);
            string executable = ResolveExecutablePath(parts[0]);

            // Security: only allowlisted executables may be launched for Command detection.
            // Mirrors the PowerShell modules' allowlist gate and closes the arbitrary command
            // execution path that an imported catalog could otherwise reach.
            string executableName = Path.GetFileName(executable);
            if (!_allowedDetectionExecutables.Contains(executableName))
            {
                _logger.LogWarning(
                    $"Command detection blocked: '{executable}' is not in the detection allowlist.");
                return DetectionProbeResult.NotFound("Executable is not allowed for command detection.");
            }

            string arguments = parts.Length > 1 ? parts[1] : string.Empty;

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = processStartInfo };

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                _logger.LogWarning($"Command detection: '{executable}' not found: {ex.Message}");
                return DetectionProbeResult.NotFound(ex.Message);
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task waitTask = process.WaitForExitAsync(cancellationToken);
            Task delayTask = Task.Delay(CommandTimeoutMs, cancellationToken);
            Task completedTask = await Task.WhenAny(waitTask, delayTask);
            if (completedTask != waitTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Process kill failed (non-critical): {ex.Message}");
                }

                return DetectionProbeResult.NotFound("Command timed out.");
            }

            await waitTask;

            string output = await outputTask;
            string errorOutput = await errorTask;
            string combinedOutput = output + "\n" + errorOutput;

            if (!string.IsNullOrEmpty(config.Arguments) &&
                !combinedOutput.Contains(config.Arguments, StringComparison.OrdinalIgnoreCase))
            {
                return DetectionProbeResult.NotFound();
            }

            string? version = null;

            if (!string.IsNullOrEmpty(config.VersionRegex))
            {
                string searchText = combinedOutput;
                if (!string.IsNullOrEmpty(config.Arguments))
                {
                    string[] lines = combinedOutput.Split('\n');
                    string? matchingLine = lines.FirstOrDefault(line =>
                        line.Contains(config.Arguments, StringComparison.OrdinalIgnoreCase));
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
                    _logger.LogWarning("Version regex timed out during command detection - possible ReDoS pattern");
                }
            }

            return DetectionProbeResult.Found(DetectionSource.Command, version);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Command detection failed", ex);
            return DetectionProbeResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Detects application via file existence.
    /// </summary>
    private DetectionProbeResult DetectFile(DetectionConfiguration config, PathValidationPolicy pathPolicy)
    {
        if (string.IsNullOrEmpty(config.Path))
        {
            return DetectionProbeResult.InvalidInput("File path is required.");
        }

        try
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(config.Path);

            if (!IsValidExpandedPath(expandedPath, pathPolicy))
            {
                _logger.LogWarning($"Security: Invalid expanded path: {expandedPath}");
                return DetectionProbeResult.InvalidInput("Invalid expanded path.");
            }

            string? version = null;

            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(expandedPath);
                version = versionInfo.FileVersion ?? versionInfo.ProductVersion;
            }
            catch (FileNotFoundException)
            {
                return DetectionProbeResult.NotFound();
            }
            catch
            {
                version = null;
            }

            return DetectionProbeResult.Found(DetectionSource.File, version);
        }
        catch (Exception ex)
        {
            _logger.LogError("File detection failed", ex);
            return DetectionProbeResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Validates an expanded file path for security.
    /// Blocks paths with dangerous patterns that could result from malicious environment variables.
    /// </summary>
    private bool IsValidExpandedPath(string path, PathValidationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (path.Contains('\0')) return false;
        if (path.Contains('%')) return false;

        if (path.Contains(".."))
        {
            _logger.LogWarning("Security: Path traversal blocked in pre-normalized path");
            return false;
        }

        char[] dangerousChars = new[] { ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'' };
        if (path.IndexOfAny(dangerousChars) >= 0) return false;

        if (!Path.IsPathRooted(path)) return false;

        try
        {
            string normalizedPath = Path.GetFullPath(path);

            if (normalizedPath.Contains(".."))
            {
                _logger.LogWarning("Security: Path traversal blocked in normalized path");
                return false;
            }

            if (policy == PathValidationPolicy.Strict && !IsAllowedCatalogRoot(normalizedPath))
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

    private static bool IsAllowedCatalogRoot(string normalizedPath)
    {
        string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string systemDrive = windowsPath.Length >= 3 ? windowsPath.Substring(0, 3) : "C:\\";
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] allowedRoots = new[] { programFiles, programFilesX86, localAppData, appData, userProfile, systemDrive };

        foreach (string root in allowedRoots)
        {
            if (!string.IsNullOrEmpty(root) && normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects Windows feature.
    /// </summary>
    private async Task<DetectionProbeResult> DetectWindowsFeatureAsync(
        DetectionConfiguration config,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.FeatureName))
        {
            return DetectionProbeResult.InvalidInput("Windows feature name is required.");
        }

        if (!ValidFeatureNamePattern.IsMatch(config.FeatureName))
        {
            _logger.LogWarning("Invalid Windows feature name rejected for security");
            return DetectionProbeResult.InvalidInput("Invalid Windows feature name.");
        }

        try
        {
            string command = $"(Get-WindowsOptionalFeature -Online -FeatureName '{config.FeatureName}').State";
            byte[] commandBytes = Encoding.Unicode.GetBytes(command);
            string encodedCommand = Convert.ToBase64String(commandBytes);

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -EncodedCommand {encodedCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(processStartInfo);
            if (process == null)
            {
                return DetectionProbeResult.NotFound();
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task waitTask = process.WaitForExitAsync(cancellationToken);
            Task delayTask = Task.Delay(CommandTimeoutMs, cancellationToken);
            Task completedTask = await Task.WhenAny(waitTask, delayTask);

            if (completedTask != waitTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogWarning("Windows feature detection timed out");
                return DetectionProbeResult.NotFound("Windows feature detection timed out.");
            }

            await waitTask;

            string output = await outputTask;
            string error = await errorTask;
            int exitCode = process.ExitCode;

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning($"Windows feature detection stderr: {error}");
            }

            return ClassifyWindowsFeatureResult(exitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Windows feature detection failed", ex);
            return DetectionProbeResult.Error(ex.Message);
        }
    }

    internal static DetectionProbeResult ClassifyWindowsFeatureResult(
        int exitCode,
        string output,
        string error)
    {
        if (output.Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return DetectionProbeResult.Found(DetectionSource.WindowsFeature, "enabled");
        }

        if (exitCode == 0)
        {
            return DetectionProbeResult.NotFound();
        }

        string trimmedError = error.Trim();
        if (IsWindowsFeatureElevationError(trimmedError))
        {
            return DetectionProbeResult.Error(WindowsFeatureElevationRequiredDetail);
        }

        if (!string.IsNullOrEmpty(trimmedError))
        {
            return DetectionProbeResult.Error(trimmedError);
        }

        return DetectionProbeResult.Error($"Windows feature detection failed with exit code {exitCode}.");
    }

    private static bool IsWindowsFeatureElevationError(string error)
    {
        return WindowsFeatureElevationErrorMarkers.Any(marker =>
            error.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves common executable names to their full paths.
    /// This helps when the executable isn't in the GUI process's PATH.
    /// </summary>
    private static string ResolveExecutablePath(string executable)
    {
        if (Path.IsPathRooted(executable) && File.Exists(executable))
            return executable;

        string executableLower = executable.ToLowerInvariant();
        string? pathResolvedExecutable = ResolveExecutableFromPath(executable);
        if (!string.IsNullOrEmpty(pathResolvedExecutable))
        {
            return pathResolvedExecutable;
        }

        Dictionary<string, string[]> knownPaths = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe")
            },
            ["java"] = new[]
            {
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

        string? pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExt))
        {
            return [".exe", ".cmd", ".bat", ".ps1", string.Empty];
        }

        return pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
