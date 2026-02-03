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

using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Models;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Services;

/// <summary>
/// PowerShell bridge implementation for executing Win11Forge scripts.
/// Handles path resolution from GUI executable to repository root.
/// </summary>
public partial class PowerShellBridge : IPowerShellBridge, IDisposable
{
    private readonly string _repositoryRoot;
    private readonly IApplicationDetectionService _detectionService;
    private Dictionary<string, JsonElement>? _applicationsCache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Compiled regex for extracting version strings from winget output.
    /// Matches patterns like "3.7.7", "1.0.0-beta", "2024.1.0".
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^[\d]+[.\d\-a-zA-Z_]*", System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex WingetVersionPattern();

    /// <summary>
    /// Compiled regex for extracting leading digits from version parts.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^\d+", System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex LeadingDigitsPattern();

    /// <summary>
    /// Default timeout for short operations (queries, status checks) in milliseconds.
    /// </summary>
    private const int DefaultQueryTimeoutMs = 300000; // 5 minutes

    /// <summary>
    /// Timeout for installation operations in milliseconds.
    /// </summary>
    private const int InstallationTimeoutMs = 1800000; // 30 minutes

    /// <summary>
    /// Initializes a new instance of the PowerShellBridge with dependency injection.
    /// </summary>
    /// <param name="detectionService">The application detection service.</param>
    public PowerShellBridge(IApplicationDetectionService detectionService)
    {
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
        _repositoryRoot = ResolveRepositoryRootSafe();
    }

    /// <summary>
    /// Initializes a new instance of the PowerShellBridge.
    /// Calculates repository root relative to executable location.
    /// </summary>
    [Obsolete("Use constructor with IApplicationDetectionService injection instead")]
    public PowerShellBridge()
    {
        _detectionService = new HybridDetectionService();
        _repositoryRoot = ResolveRepositoryRootSafe();
    }

    /// <summary>
    /// Finds the PowerShell executable path.
    /// Prefers PowerShell 7+ (pwsh.exe) for null-conditional operator support.
    /// </summary>
    private static string? _powerShellPath;
    private static string GetPowerShellPath()
    {
        if (_powerShellPath != null) return _powerShellPath;

        // Try PowerShell 7+ in multiple locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Check Program Files for PowerShell 7.x installations
        if (!string.IsNullOrEmpty(programFiles))
        {
            var psBaseDir = Path.Combine(programFiles, "PowerShell");
            if (Directory.Exists(psBaseDir))
            {
                // Look for any version 7+ directory (7, 7.0, 7.4.0, etc.)
                try
                {
                    var versionDirs = Directory.GetDirectories(psBaseDir)
                        .Where(d => Path.GetFileName(d).StartsWith("7"))
                        .OrderByDescending(d => d)
                        .ToList();

                    foreach (var versionDir in versionDirs)
                    {
                        var pwshPath = Path.Combine(versionDir, "pwsh.exe");
                        if (File.Exists(pwshPath))
                        {
                            _powerShellPath = pwshPath;
                            return _powerShellPath;
                        }
                    }
                }
                catch
                {
                    // Directory enumeration failed, try direct path
                }

                // Direct path fallback
                var directPath = Path.Combine(psBaseDir, "7", "pwsh.exe");
                if (File.Exists(directPath))
                {
                    _powerShellPath = directPath;
                    return _powerShellPath;
                }
            }
        }

        // Try Microsoft Store installation via WindowsApps
        if (!string.IsNullOrEmpty(localAppData))
        {
            var storeAppPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "pwsh.exe");
            if (File.Exists(storeAppPath))
            {
                _powerShellPath = storeAppPath;
                return _powerShellPath;
            }
        }

        // Try pwsh in PATH (covers winget, scoop, chocolatey installations)
        try
        {
            var pwshInPath = FindExecutableInPath("pwsh.exe");
            if (!string.IsNullOrEmpty(pwshInPath) && File.Exists(pwshInPath))
            {
                _powerShellPath = pwshInPath;
                return _powerShellPath;
            }
        }
        catch
        {
            // PATH search failed
        }

        // Try Windows PowerShell as last resort
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(systemPath))
        {
            var winPsPath = Path.Combine(systemPath, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPsPath))
            {
                _powerShellPath = winPsPath;
                return _powerShellPath;
            }
        }

        // Final fallback
        _powerShellPath = "pwsh";
        return _powerShellPath;
    }

    /// <summary>
    /// Searches for an executable in the PATH environment variable.
    /// </summary>
    private static string? FindExecutableInPath(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Invalid path, skip
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a PowerShell instance using external process.
    /// The returned wrapper uses process-based execution.
    /// </summary>
    private PowerShellProcessWrapper CreatePowerShellInstance()
    {
        return new PowerShellProcessWrapper(GetPowerShellPath(), GetSafeRepositoryRoot());
    }

    /// <summary>
    /// Executes a PowerShell script using external process.
    /// This avoids SDK issues in single-file deployments.
    /// </summary>
    private async Task<string> ExecutePowerShellScriptAsync(string script)
    {
        var psPath = GetPowerShellPath();
        var repoRoot = GetSafeRepositoryRoot();

        // Escape script for command line
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        // Read stdout and stderr concurrently to prevent deadlocks
        // This is critical for long-running installations like Office
        // that may produce large amounts of output on both streams
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for both streams AND the process to complete with timeout
        using var timeoutCts = new CancellationTokenSource(DefaultQueryTimeoutMs);
        try
        {
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeoutCts.Token));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
            throw new TimeoutException($"PowerShell script execution timed out after {DefaultQueryTimeoutMs / 1000} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"PowerShell error: {error}");
        }

        return output;
    }

    /// <summary>
    /// Resolves repository root with guaranteed non-null result.
    /// </summary>
    private static string ResolveRepositoryRootSafe()
    {
        try
        {
            var result = ResolveRepositoryRoot();
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }
        catch
        {
            // Fall through to fallbacks
        }

        // Fallback chain - try each option until we get a non-null value
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                return dir;
            }
        }

        var currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            return currentDir;
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            return baseDir;
        }

        // Ultimate fallback - temp folder (always exists)
        return Path.GetTempPath();
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _repositoryRoot;

    /// <summary>
    /// Gets the repository root, throwing if not properly initialized.
    /// </summary>
    private string GetSafeRepositoryRoot()
    {
        if (string.IsNullOrEmpty(_repositoryRoot))
        {
            throw new InvalidOperationException(
                $"Repository root is not initialized. " +
                $"ProcessPath={Environment.ProcessPath}, " +
                $"BaseDirectory={AppContext.BaseDirectory}, " +
                $"CurrentDirectory={Environment.CurrentDirectory}");
        }
        return _repositoryRoot;
    }

    /// <summary>
    /// Validates and sanitizes a profile name to prevent path traversal attacks.
    /// </summary>
    /// <param name="profileName">The profile name to validate.</param>
    /// <returns>The validated profile name.</returns>
    /// <exception cref="ArgumentException">Thrown if the profile name is invalid.</exception>
    private static string ValidateProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));
        }

        // Check for path traversal attempts
        if (profileName.Contains("..") || profileName.Contains('/') || profileName.Contains('\\'))
        {
            throw new ArgumentException("Profile name contains invalid characters.", nameof(profileName));
        }

        // Check for other invalid path characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (profileName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException("Profile name contains invalid characters.", nameof(profileName));
        }

        // Limit length to prevent buffer issues
        if (profileName.Length > 100)
        {
            throw new ArgumentException("Profile name is too long.", nameof(profileName));
        }

        return profileName;
    }

    /// <summary>
    /// Validates that a file path is within the expected directory to prevent path traversal.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="expectedBaseDir">The expected base directory.</param>
    /// <returns>The validated absolute path.</returns>
    /// <exception cref="ArgumentException">Thrown if the path escapes the expected directory.</exception>
    private static string ValidatePathWithinDirectory(string filePath, string expectedBaseDir)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullBaseDir = Path.GetFullPath(expectedBaseDir);

        // Ensure the base directory ends with a separator for proper prefix checking
        if (!fullBaseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            fullBaseDir += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path traversal detected: {filePath} is outside of {expectedBaseDir}", nameof(filePath));
        }

        return fullPath;
    }

    /// <summary>
    /// Validates an application ID to prevent injection attacks.
    /// Blocks all PowerShell special characters including backticks, $, (), etc.
    /// </summary>
    /// <param name="appId">The application ID to validate.</param>
    /// <returns>The validated application ID.</returns>
    /// <exception cref="ArgumentException">Thrown when the ID contains invalid characters.</exception>
    private static string ValidateAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("Application ID cannot be empty.", nameof(appId));
        }

        // AppIds should only contain alphanumeric, dots, hyphens, underscores, and spaces
        // This BLOCKS all PowerShell injection vectors:
        // - Backticks (`) for escape sequences
        // - Dollar signs ($) for variables and subexpressions
        // - Parentheses () for subexpressions
        // - Semicolons (;) for command chaining
        // - Pipes (|) for command piping
        // - Single/double quotes for string manipulation
        // Examples: Microsoft.VisualStudioCode, 7zip.7zip, VideoLAN.VLC
        foreach (char c in appId)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_' && c != ' ')
            {
                throw new ArgumentException($"Application ID contains invalid character: '{c}'", nameof(appId));
            }
        }

        // Limit length to prevent buffer issues
        if (appId.Length > 200)
        {
            throw new ArgumentException("Application ID is too long.", nameof(appId));
        }

        return appId;
    }

    /// <summary>
    /// Escapes a string for safe use in PowerShell single-quoted strings.
    /// Even though ValidateAppId blocks dangerous characters, this provides defense in depth.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for use in PowerShell.</returns>
    internal static string EscapeForPowerShell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // In single-quoted PowerShell strings, only single quotes need escaping (doubled)
        // But we also remove any potentially dangerous characters as defense in depth
        return value
            .Replace("'", "''")           // Escape single quotes
            .Replace("`", "")             // Remove backticks (escape char)
            .Replace("$", "")             // Remove dollar signs (variable expansion)
            .Replace("(", "")             // Remove opening parens (subexpression)
            .Replace(")", "");            // Remove closing parens (subexpression)
    }

    /// <inheritdoc/>
    public async Task<string> GetWin11ForgeVersionAsync()
    {
        var repoRoot = GetSafeRepositoryRoot();
        var versionFilePath = Path.Combine(repoRoot, "Config", "version.json");

        if (!File.Exists(versionFilePath))
        {
            return "Unknown";
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(versionFilePath);
            using var document = JsonDocument.Parse(jsonContent);

            if (document.RootElement.TryGetProperty("Version", out var versionElement))
            {
                return versionElement.GetString() ?? "Unknown";
            }

            return "Unknown";
        }
        catch (Exception)
        {
            return "Error";
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableProfilesAsync()
    {
        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");

        if (!Directory.Exists(profilesDir))
        {
            return [];
        }

        return await Task.Run(() =>
        {
            var profiles = Directory.GetFiles(profilesDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(name => name)
                .ToList();

            return profiles;
        });
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> LoadProfileAsync(string profileName)
    {
        // Validate profile name to prevent path traversal
        profileName = ValidateProfileName(profileName);

        // Ensure applications database is loaded
        await EnsureApplicationsCacheAsync();

        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");

        // Load profile directly from JSON (no PowerShell needed)
        return await LoadProfileFromJsonAsync(profileName, profilesDir, new List<string>());
    }

    /// <summary>
    /// Loads a profile from JSON file with inheritance support.
    /// </summary>
    private async Task<DeploymentProfileModel> LoadProfileFromJsonAsync(
        string profileName,
        string profilesDir,
        List<string> inheritanceChain)
    {
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        // Defense-in-depth: verify the resolved path stays within profiles directory
        profilePath = ValidatePathWithinDirectory(profilePath, profilesDir);

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Profile not found: {profileName}");
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var profile = new DeploymentProfileModel
        {
            Name = GetJsonString(root, "Name") ?? profileName,
            Description = GetJsonString(root, "Description") ?? string.Empty,
            Version = GetJsonString(root, "Version") ?? "1.0.0"
        };

        // Track inheritance chain
        inheritanceChain.Add(profileName);

        // Handle inheritance
        var allAppIds = new List<string>();

        if (root.TryGetProperty("Inherits", out var inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parentName in inheritsElement.EnumerateArray())
            {
                var parentNameStr = parentName.GetString();
                if (!string.IsNullOrEmpty(parentNameStr) && !inheritanceChain.Contains(parentNameStr))
                {
                    profile.InheritedFrom.Add(parentNameStr);

                    // Load parent profile and merge apps
                    var parentProfile = await LoadProfileFromJsonAsync(parentNameStr, profilesDir, inheritanceChain);
                    foreach (var app in parentProfile.Applications)
                    {
                        if (!allAppIds.Contains(app.AppId))
                        {
                            allAppIds.Add(app.AppId);
                        }
                    }
                }
            }
        }

        // Add this profile's applications
        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                string? appId = null;

                if (appElement.ValueKind == JsonValueKind.String)
                {
                    appId = appElement.GetString();
                }
                else if (appElement.ValueKind == JsonValueKind.Object)
                {
                    appId = GetJsonString(appElement, "AppId");
                }

                if (!string.IsNullOrEmpty(appId) && !allAppIds.Contains(appId))
                {
                    allAppIds.Add(appId);
                }
            }
        }

        // Convert app IDs to ApplicationModels
        profile.Applications = new ObservableCollection<ApplicationModel>(
            allAppIds.Select(appId => CreateApplicationModel(appId))
                     .OrderBy(a => a.Priority)
        );

        return profile;
    }

    /// <summary>
    /// Creates an ApplicationModel from an app ID using the applications cache.
    /// </summary>
    private ApplicationModel CreateApplicationModel(string appId)
    {
        var app = new ApplicationModel
        {
            AppId = appId,
            Name = appId,
            Category = Loc.Common_Unknown,
            Priority = 50,
            IsRequired = false,
            Status = ApplicationStatus.Pending,
            IsSelected = true
        };

        // Enrich from applications database
        if (_applicationsCache != null && _applicationsCache.TryGetValue(appId, out var appData))
        {
            app.Name = GetJsonString(appData, "Name") ?? appId;
            app.Category = GetJsonString(appData, "Category") ?? "Unknown";
            app.Description = GetJsonString(appData, "Description") ?? string.Empty;

            if (appData.TryGetProperty("DefaultPriority", out var priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            if (appData.TryGetProperty("DefaultRequired", out var requiredProp) &&
                requiredProp.ValueKind == JsonValueKind.True)
            {
                app.IsRequired = true;
            }
        }

        return app;
    }

    /// <inheritdoc/>
    public async Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        Action<string>? progressCallback = null)
    {
        // Handle dry run mode
        if (isDryRun)
        {
            progressCallback?.Invoke($"{Resources.Resources.Common_DryRun} Simulating installation of {app.Name}...");
            await Task.Delay(500); // Simulate some work
            return InstallResult.DryRun(app.Name);
        }

        var repoRoot = GetSafeRepositoryRoot();
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1").Replace("\\", "/");
        var dbModulePath = Path.Combine(repoRoot, "Modules", "ApplicationDatabase.psm1").Replace("\\", "/");
        var enginePath = Path.Combine(repoRoot, "Modules", "InstallationEngine.psm1").Replace("\\", "/");
        var orchestratorPath = Path.Combine(repoRoot, "Modules", "InstallationOrchestrator.psm1").Replace("\\", "/");

        return await Task.Run(async () =>
        {
            var logBuilder = new System.Text.StringBuilder();
            var outputLines = new System.Collections.Generic.List<string>();

            try
            {
                progressCallback?.Invoke($"Preparing to install {app.Name}...");

                // Build the ForceUpdate switch if needed
                var forceUpdateSwitch = forceUpdate ? " -ForceUpdate" : "";

                // Security: Validate AppId before interpolating into PowerShell script
                var validatedAppId = ValidateAppId(app.AppId);

                // Build a PowerShell script that outputs status messages and JSON result
                // Use 6>&1 to redirect Information stream (Write-Host in PS5+) to stdout
                var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'Continue'

# Redirect Write-Host to stdout for streaming capture
$script:OriginalHost = $host.UI.RawUI

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{orchestratorPath}' -Force -ErrorAction Stop

    $app = Get-ApplicationById -AppId '{EscapeForPowerShell(validatedAppId)}'
    if (-not $app) {{
        Write-Output '[STATUS] Application not found in database'
        @{{ Success = $false; Message = 'Application not found in database'; Method = ''; AlreadyInstalled = $false }} | ConvertTo-Json -Compress
        exit
    }}

    Write-Output '[STATUS] Starting installation...'
    $result = Install-Application -Application $app{forceUpdateSwitch}
    $result | ConvertTo-Json -Compress
}} catch {{
    Write-Output ""[ERROR] $($_.Exception.Message)""
    @{{ Success = $false; Message = $_.Exception.Message; Method = ''; AlreadyInstalled = $false }} | ConvertTo-Json -Compress
}}
";

                progressCallback?.Invoke($"Installing {app.Name}...");

                // Execute with streaming output
                var psPath = GetPowerShellPath();
                var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = psPath,
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };

                // Handle stdout line by line for real-time streaming
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = e.Data.Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputLines.Add(line);
                            logBuilder.AppendLine(line);

                            // Forward status messages to progress callback
                            if (line.StartsWith("[STATUS]") || line.StartsWith("[ERROR]") ||
                                line.StartsWith("[INFO]") || line.StartsWith("[SUCCESS]") ||
                                line.StartsWith("[WARNING]"))
                            {
                                progressCallback?.Invoke($"{app.Name}: {line}");
                            }
                            else if (line.Contains("Installing") || line.Contains("Downloading") ||
                                     line.Contains("Verifying") || line.Contains("Completed") ||
                                     line.Contains("already installed", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("Successfully", StringComparison.OrdinalIgnoreCase))
                            {
                                progressCallback?.Invoke($"{app.Name}: {line}");
                            }
                        }
                    }
                };

                // Handle stderr
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        var cleanLine = ExtractReadableMessage(e.Data);
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            logBuilder.AppendLine($"[STDERR] {cleanLine}");
                            progressCallback?.Invoke($"{app.Name}: [Error] {cleanLine}");
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process with installation timeout
                using var installTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
                try
                {
                    await process.WaitForExitAsync(installTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (installTimeoutCts.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                    throw new TimeoutException($"Installation of {app.Name} timed out after {InstallationTimeoutMs / 60000} minutes");
                }

                // Find JSON line in output (last non-empty line that starts with {)
                string? jsonLine = null;
                for (int i = outputLines.Count - 1; i >= 0; i--)
                {
                    var trimmed = outputLines[i].Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        jsonLine = trimmed;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(jsonLine))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonLine);
                        var root = doc.RootElement;

                        var success = root.TryGetProperty("Success", out var successProp) &&
                                      successProp.ValueKind == JsonValueKind.True;
                        var message = root.TryGetProperty("Message", out var msgProp)
                            ? msgProp.GetString() ?? string.Empty
                            : string.Empty;
                        var method = root.TryGetProperty("Method", out var methodProp)
                            ? methodProp.GetString() ?? string.Empty
                            : string.Empty;
                        var alreadyInstalled = root.TryGetProperty("AlreadyInstalled", out var aiProp) &&
                                               aiProp.ValueKind == JsonValueKind.True;

                        logBuilder.AppendLine($"Result: Success={success}, Method={method}, Message={message}");

                        if (success)
                        {
                            progressCallback?.Invoke($"Completed: {app.Name}");
                            return InstallResult.Successful(message, logBuilder.ToString(), method, alreadyInstalled);
                        }
                        else
                        {
                            progressCallback?.Invoke($"Failed: {app.Name} - {message}");
                            return InstallResult.Failed(message, logBuilder.ToString());
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON parsing failed, treat as error
                        return InstallResult.Failed($"Invalid response format", logBuilder.ToString());
                    }
                }

                // No JSON found, check if there's any output indicating success
                var fullOutput = string.Join("\n", outputLines);
                if (fullOutput.Contains("successfully", StringComparison.OrdinalIgnoreCase) ||
                    fullOutput.Contains("installed", StringComparison.OrdinalIgnoreCase))
                {
                    return InstallResult.Successful(
                        $"Installation completed for {app.Name}",
                        logBuilder.ToString());
                }

                return InstallResult.Failed(
                    "Installation completed but status unknown",
                    logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UninstallApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null)
    {
        await EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            var logBuilder = new System.Text.StringBuilder();

            try
            {
                progressCallback?.Invoke($"Preparing to uninstall {app.Name}...");
                logBuilder.AppendLine($"Uninstalling: {app.Name}");

                // Get package IDs from cache
                string? wingetId = null;
                string? chocoPackage = null;

                if (_applicationsCache != null && _applicationsCache.TryGetValue(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = GetJsonString(sources, "Winget");
                        chocoPackage = GetJsonString(sources, "Chocolatey");
                    }
                }

                // Try Winget first
                if (!string.IsNullOrEmpty(wingetId))
                {
                    progressCallback?.Invoke($"Uninstalling via Winget: {wingetId}");
                    logBuilder.AppendLine($"Uninstalling via Winget: {wingetId}");

                    var wingetResult = await ExecuteUninstallCommandAsync(
                        "winget",
                        $"uninstall --id \"{wingetId}\" --silent --accept-source-agreements",
                        logBuilder);

                    if (wingetResult.Success)
                    {
                        progressCallback?.Invoke($"Uninstalled: {app.Name}");
                        logBuilder.AppendLine("Winget uninstallation succeeded");
                        return InstallResult.Successful(
                            $"Successfully uninstalled {app.Name}",
                            logBuilder.ToString(),
                            "Winget",
                            alreadyInstalled: false);
                    }

                    logBuilder.AppendLine($"Winget uninstallation failed (exit code: {wingetResult.ExitCode})");
                }

                // Try Chocolatey as fallback
                if (!string.IsNullOrEmpty(chocoPackage))
                {
                    progressCallback?.Invoke($"Uninstalling via Chocolatey: {chocoPackage}");
                    logBuilder.AppendLine($"Uninstalling via Chocolatey: {chocoPackage}");

                    var chocoResult = await ExecuteUninstallCommandAsync(
                        "choco",
                        $"uninstall {chocoPackage} -y --no-progress",
                        logBuilder);

                    if (chocoResult.Success)
                    {
                        progressCallback?.Invoke($"Uninstalled: {app.Name}");
                        logBuilder.AppendLine("Chocolatey uninstallation succeeded");
                        return InstallResult.Successful(
                            $"Successfully uninstalled {app.Name}",
                            logBuilder.ToString(),
                            "Chocolatey",
                            alreadyInstalled: false);
                    }

                    logBuilder.AppendLine($"Chocolatey uninstallation failed (exit code: {chocoResult.ExitCode})");
                }

                // Both methods failed or no sources available
                var errorMsg = string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? "No uninstall sources available for this application"
                    : "All uninstallation methods failed";

                progressCallback?.Invoke($"Failed: {app.Name}");
                logBuilder.AppendLine($"Uninstallation failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// SAFETY: This method uses ONLY read-only commands:
    /// - winget list: Gets installed version (SAFE)
    /// - winget show: Gets available version from repository (SAFE)
    /// NEVER uses 'winget upgrade' which can trigger installations!
    /// </remarks>
    public async Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
    {
        // Only check installed apps
        if (app.Status != ApplicationStatus.Installed &&
            app.Status != ApplicationStatus.AlreadyInstalled &&
            app.Status != ApplicationStatus.UpdateAvailable)
        {
            return UpdateCheckResult.UpToDate();
        }

        await EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            try
            {
                // Get Winget ID from cache
                string? wingetId = null;

                if (_applicationsCache != null && _applicationsCache.TryGetValue(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = GetJsonString(sources, "Winget");
                    }
                }

                if (string.IsNullOrEmpty(wingetId))
                {
                    return UpdateCheckResult.UpToDate();
                }

                // SAFE: Get installed version using 'winget list' (read-only)
                var installedVersion = await GetInstalledVersionAsync(wingetId);

                // SAFE: Get available version using 'winget show' (read-only)
                var availableVersion = await GetRepositoryVersionAsync(wingetId);

                // If we couldn't get the installed version, return with what we have
                if (string.IsNullOrEmpty(installedVersion))
                {
                    return UpdateCheckResult.UpToDate();
                }

                // If no available version found, app is up to date (or not in repo)
                if (string.IsNullOrEmpty(availableVersion))
                {
                    return UpdateCheckResult.UpToDate(installedVersion);
                }

                // Compare versions to determine if update is available
                var comparison = CompareVersions(installedVersion, availableVersion);

                if (comparison < 0)
                {
                    // Installed version is older than available
                    return UpdateCheckResult.UpdateAvailable(installedVersion, availableVersion);
                }

                return UpdateCheckResult.UpToDate(installedVersion);
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Failed(ex.Message);
            }
        });
    }

    /// <summary>
    /// Gets the installed version of a package using winget list (SAFE: read-only).
    /// </summary>
    private async Task<string> GetInstalledVersionAsync(string wingetId)
    {
        try
        {
            // SAFE COMMAND: winget list only reads information, never modifies
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"list --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                return string.Empty;
            }

            // Clean output from progress indicators before parsing
            var cleanOutput = CleanWingetOutput(output);

            // Parse version from list output using column-based parsing
            return ParseVersionFromWingetList(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleans winget output by removing progress spinner characters.
    /// Winget uses \r to animate progress, resulting in lines with multiple segments.
    /// This method extracts only the actual content from each line.
    /// </summary>
    private static string CleanWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        var cleanLines = new List<string>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            // Split by \r and take the last non-empty segment
            // This removes progress spinner characters that overwrite earlier content
            var segments = line.Split('\r');
            var lastSegment = segments
                .Select(s => s.Trim())
                .LastOrDefault(s => !string.IsNullOrEmpty(s) &&
                                    !s.All(c => c == '-' || c == '\\' || c == '|' || c == '/' || c == ' '));

            if (!string.IsNullOrEmpty(lastSegment))
            {
                cleanLines.Add(lastSegment);
            }
        }

        return string.Join("\n", cleanLines);
    }

    /// <summary>
    /// Gets the available version from repository using winget show (SAFE: read-only).
    /// </summary>
    private async Task<string> GetRepositoryVersionAsync(string wingetId)
    {
        try
        {
            // SAFE COMMAND: winget show only displays info, never modifies
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"show --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                return string.Empty;
            }

            // Clean output from progress indicators before parsing
            var cleanOutput = CleanWingetOutput(output);

            // Parse version from show output
            return ParseVersionFromWingetShow(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses version from 'winget list' output.
    /// Output format: Name  Id  Version  Available  Source
    /// </summary>
    private static string ParseVersionFromWingetList(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the header line to determine the Version column position
        int versionColumnStart = -1;
        int sourceColumnStart = -1;
        string? headerLine = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines and separator lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("-"))
                continue;

            // Find header line (contains "Version" and either "Source" or "Quelle")
            if ((trimmedLine.Contains("Version", StringComparison.OrdinalIgnoreCase)) &&
                (trimmedLine.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                 trimmedLine.Contains("Quelle", StringComparison.OrdinalIgnoreCase)))
            {
                headerLine = line;
                versionColumnStart = line.IndexOf("Version", StringComparison.OrdinalIgnoreCase);

                // Find Source column (or Quelle in German)
                sourceColumnStart = line.IndexOf("Source", StringComparison.OrdinalIgnoreCase);
                if (sourceColumnStart < 0)
                    sourceColumnStart = line.IndexOf("Quelle", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            // Skip if we haven't found header yet
            if (versionColumnStart < 0 || headerLine == null)
                continue;

            // Skip the separator line after header
            if (trimmedLine.Contains("---"))
                continue;

            // This is a data line - extract version using column positions
            if (line.Length > versionColumnStart)
            {
                int endPos = sourceColumnStart > versionColumnStart
                    ? Math.Min(sourceColumnStart, line.Length)
                    : line.Length;

                var versionPart = line.Substring(versionColumnStart, endPos - versionColumnStart).Trim();

                // Extract version number pattern (handles versions like "3.7.7", "1.0.0-beta", etc.)
                var versionMatch = WingetVersionPattern().Match(versionPart);

                if (versionMatch.Success && versionMatch.Value.Contains('.'))
                {
                    return versionMatch.Value;
                }

                // Fallback: return first word if it starts with a digit
                var firstWord = versionPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstWord) && char.IsDigit(firstWord[0]))
                {
                    return firstWord;
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Parses version from 'winget show' output.
    /// Looks for "Version:" line in the output.
    /// </summary>
    private static string ParseVersionFromWingetShow(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();

            // Look for "Version:" or "Version :" pattern (handles various locales)
            if (trimmedLine.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0 && colonIndex < line.Length - 1)
                {
                    var version = line.Substring(colonIndex + 1).Trim();

                    // Clean up version string - extract version pattern
                    // Handles: "3.7.7", "1.0.0-beta", "2024.1.0", etc.
                    var match = WingetVersionPattern().Match(version);

                    if (match.Success && match.Value.Contains('.'))
                    {
                        return match.Value;
                    }

                    // Return trimmed version if no match
                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Compares two version strings.
    /// Returns -1 if v1 &lt; v2, 0 if equal, 1 if v1 &gt; v2.
    /// </summary>
    private static int CompareVersions(string v1, string v2)
    {
        if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
        if (string.IsNullOrEmpty(v1)) return -1;
        if (string.IsNullOrEmpty(v2)) return 1;

        // Try to parse as Version objects
        var parts1 = v1.Split('.', '-').Select(p =>
        {
            var numMatch = LeadingDigitsPattern().Match(p);
            return numMatch.Success ? int.Parse(numMatch.Value) : 0;
        }).ToArray();

        var parts2 = v2.Split('.', '-').Select(p =>
        {
            var numMatch = LeadingDigitsPattern().Match(p);
            return numMatch.Success ? int.Parse(numMatch.Value) : 0;
        }).ToArray();

        var maxLength = Math.Max(parts1.Length, parts2.Length);

        for (int i = 0; i < maxLength; i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;

            if (p1 < p2) return -1;
            if (p1 > p2) return 1;
        }

        return 0;
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null)
    {
        await EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            var logBuilder = new System.Text.StringBuilder();

            try
            {
                progressCallback?.Invoke($"Preparing to update {app.Name}...");
                logBuilder.AppendLine($"Updating: {app.Name}");

                // Get package IDs from cache
                string? wingetId = null;
                string? chocoPackage = null;

                if (_applicationsCache != null && _applicationsCache.TryGetValue(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = GetJsonString(sources, "Winget");
                        chocoPackage = GetJsonString(sources, "Chocolatey");
                    }
                }

                // Try Winget first
                if (!string.IsNullOrEmpty(wingetId))
                {
                    progressCallback?.Invoke($"Updating via Winget: {wingetId}");
                    logBuilder.AppendLine($"Updating via Winget: {wingetId}");

                    var wingetResult = await ExecuteUpdateCommandAsync(
                        "winget",
                        $"upgrade --id \"{wingetId}\" --silent --accept-package-agreements --accept-source-agreements",
                        logBuilder);

                    if (wingetResult.Success)
                    {
                        progressCallback?.Invoke($"Updated: {app.Name}");
                        logBuilder.AppendLine("Winget update succeeded");
                        return InstallResult.Successful(
                            $"Successfully updated {app.Name}",
                            logBuilder.ToString(),
                            "Winget");
                    }

                    // Exit code -1978335189 means no update available (already up-to-date)
                    if (wingetResult.ExitCode == -1978335189)
                    {
                        progressCallback?.Invoke($"Already up to date: {app.Name}");
                        logBuilder.AppendLine("Application is already up to date");
                        return InstallResult.Successful(
                            $"{app.Name} is already up to date",
                            logBuilder.ToString(),
                            "Winget",
                            alreadyInstalled: true);
                    }

                    logBuilder.AppendLine($"Winget update failed (exit code: {wingetResult.ExitCode})");
                }

                // Try Chocolatey as fallback
                if (!string.IsNullOrEmpty(chocoPackage))
                {
                    progressCallback?.Invoke($"Updating via Chocolatey: {chocoPackage}");
                    logBuilder.AppendLine($"Updating via Chocolatey: {chocoPackage}");

                    var chocoResult = await ExecuteUpdateCommandAsync(
                        "choco",
                        $"upgrade {chocoPackage} -y --no-progress",
                        logBuilder);

                    if (chocoResult.Success)
                    {
                        progressCallback?.Invoke($"Updated: {app.Name}");
                        logBuilder.AppendLine("Chocolatey update succeeded");
                        return InstallResult.Successful(
                            $"Successfully updated {app.Name}",
                            logBuilder.ToString(),
                            "Chocolatey");
                    }

                    logBuilder.AppendLine($"Chocolatey update failed (exit code: {chocoResult.ExitCode})");
                }

                // Both methods failed or no sources available
                var errorMsg = string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? "No update sources available for this application"
                    : "All update methods failed";

                progressCallback?.Invoke($"Failed: {app.Name}");
                logBuilder.AppendLine($"Update failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public async Task<bool> LaunchApplicationAsync(ApplicationModel app)
    {
        await EnsureApplicationsCacheAsync();

        return await Task.Run(() =>
        {
            try
            {
                // Get executable name from cache if available
                string? executableName = null;

                if (_applicationsCache != null && _applicationsCache.TryGetValue(app.AppId, out var appData))
                {
                    // Try to get executable name from app data
                    executableName = GetJsonString(appData, "Executable");
                }

                // Strategy 1: Use executable name if available
                if (!string.IsNullOrEmpty(executableName))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = executableName,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch
                    {
                        // Continue to next strategy
                    }
                }

                // Build list of search terms from app name and ID
                var searchTerms = new List<string> { app.Name };

                // Add parts from AppId (e.g., "Audacity.Audacity" -> "Audacity")
                if (!string.IsNullOrEmpty(app.AppId))
                {
                    var idParts = app.AppId.Split('.');
                    searchTerms.AddRange(idParts.Where(p => p.Length > 2));
                }

                // Strategy 2: Try to find in Start Menu with multiple search terms
                var startMenuPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
                };

                foreach (var startMenuPath in startMenuPaths)
                {
                    var programsPath = Path.Combine(startMenuPath, "Programs");
                    if (!Directory.Exists(programsPath))
                        continue;

                    try
                    {
                        var shortcuts = Directory.GetFiles(programsPath, "*.lnk", SearchOption.AllDirectories);

                        // First pass: exact name match
                        var exactMatch = shortcuts.FirstOrDefault(s =>
                            Path.GetFileNameWithoutExtension(s)
                                .Equals(app.Name, StringComparison.OrdinalIgnoreCase));

                        if (exactMatch != null)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = exactMatch,
                                UseShellExecute = true
                            });
                            return true;
                        }

                        // Second pass: partial match with any search term
                        foreach (var term in searchTerms.Distinct())
                        {
                            var matchingShortcut = shortcuts.FirstOrDefault(s =>
                            {
                                var shortcutName = Path.GetFileNameWithoutExtension(s);
                                return shortcutName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                       term.Contains(shortcutName, StringComparison.OrdinalIgnoreCase);
                            });

                            if (matchingShortcut != null)
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = matchingShortcut,
                                    UseShellExecute = true
                                });
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Continue searching
                    }
                }

                // Strategy 3: Search in Program Files for executable
                var programDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
                };

                foreach (var programDir in programDirs.Where(d => Directory.Exists(d)))
                {
                    foreach (var term in searchTerms.Distinct())
                    {
                        try
                        {
                            // Look for app folder
                            var appFolders = Directory.GetDirectories(programDir, $"*{term}*",
                                SearchOption.TopDirectoryOnly);

                            foreach (var folder in appFolders)
                            {
                                // Look for main executable
                                var exeFiles = Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly);
                                var mainExe = exeFiles.FirstOrDefault(e =>
                                {
                                    var exeName = Path.GetFileNameWithoutExtension(e);
                                    return searchTerms.Any(t =>
                                        exeName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                        t.Contains(exeName, StringComparison.OrdinalIgnoreCase));
                                }) ?? exeFiles.FirstOrDefault();

                                if (mainExe != null)
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = mainExe,
                                        UseShellExecute = true
                                    });
                                    return true;
                                }
                            }
                        }
                        catch
                        {
                            // Continue searching
                        }
                    }
                }

                // Strategy 4: Try common executable names in PATH
                var possibleExeNames = searchTerms
                    .SelectMany(name => new[]
                    {
                        $"{name}.exe",
                        $"{name.Replace(" ", "")}.exe",
                        $"{name.Replace(" ", "-")}.exe",
                        $"{name.ToLowerInvariant()}.exe",
                        $"{name.Replace(" ", "").ToLowerInvariant()}.exe"
                    })
                    .Distinct();

                foreach (var exeName in possibleExeNames)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exeName,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch
                    {
                        // Continue to next
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Executes an update command and returns the result.
    /// </summary>
    private async Task<(bool Success, int ExitCode, string Output)> ExecuteUpdateCommandAsync(
        string command,
        string arguments,
        System.Text.StringBuilder logBuilder)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait with installation timeout (updates can take as long as installs)
            using var updateTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(updateTimeoutCts.Token));
            }
            catch (OperationCanceledException) when (updateTimeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                logBuilder.AppendLine($"[ERROR] Update command timed out after {InstallationTimeoutMs / 60000} minutes");
                return (false, -1, "Update command timed out");
            }

            var output = await outputTask;
            var error = await errorTask;

            logBuilder.AppendLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                logBuilder.AppendLine($"[stderr] {error}");
            }

            // Exit code 0 means success
            return (process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command execution failed: {ex.Message}");
            return (false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Executes an uninstall command and returns the result.
    /// </summary>
    private async Task<(bool Success, int ExitCode, string Output)> ExecuteUninstallCommandAsync(
        string command,
        string arguments,
        System.Text.StringBuilder logBuilder)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait with installation timeout (uninstalls can take time too)
            using var uninstallTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(uninstallTimeoutCts.Token));
            }
            catch (OperationCanceledException) when (uninstallTimeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                logBuilder.AppendLine($"[ERROR] Uninstall command timed out after {InstallationTimeoutMs / 60000} minutes");
                return (false, -1, "Uninstall command timed out");
            }

            var output = await outputTask;
            var error = await errorTask;

            logBuilder.AppendLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                logBuilder.AppendLine($"[stderr] {error}");
            }

            // Exit code 0 means success
            return (process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command execution failed: {ex.Message}");
            return (false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Converts a PowerShell result (Hashtable/PSObject) to DeploymentProfileModel.
    /// </summary>
    private DeploymentProfileModel ConvertToDeploymentProfile(PSObject? psObject)
    {
        if (psObject?.BaseObject is not Hashtable profileData)
        {
            throw new InvalidOperationException("Profile data is not a valid Hashtable");
        }

        var profile = new DeploymentProfileModel
        {
            Name = GetStringValue(profileData, "Name"),
            Description = GetStringValue(profileData, "Description"),
            Version = GetStringValue(profileData, "Version")
        };

        // Get inherited profiles list
        if (profileData["InheritedFrom"] is object[] inheritedArray)
        {
            profile.InheritedFrom = inheritedArray.Select(i => i?.ToString() ?? string.Empty).ToList();
        }

        // Convert applications
        if (profileData["Applications"] is object[] applicationsArray)
        {
            profile.Applications = new ObservableCollection<ApplicationModel>(
                applicationsArray
                    .Select(ConvertToApplicationModel)
                    .Where(a => a != null)
                    .Cast<ApplicationModel>()
                    .OrderBy(a => a.Priority)
            );
        }

        return profile;
    }

    /// <summary>
    /// Converts a PowerShell application object to ApplicationModel.
    /// Enriches with data from the applications database.
    /// </summary>
    private ApplicationModel? ConvertToApplicationModel(object? appObj)
    {
        if (appObj == null) return null;

        string? appId = null;
        int priority = 50;
        bool isRequired = false;

        // Handle different input formats
        if (appObj is PSObject psApp)
        {
            appId = psApp.Properties["AppId"]?.Value?.ToString();
            if (psApp.Properties["Priority"]?.Value is int p) priority = p;
            if (psApp.Properties["Required"]?.Value is bool r) isRequired = r;
        }
        else if (appObj is Hashtable htApp)
        {
            appId = htApp["AppId"]?.ToString();
            if (htApp["Priority"] is int p) priority = p;
            if (htApp["Required"] is bool r) isRequired = r;
        }
        else if (appObj is string strAppId)
        {
            appId = strAppId;
        }

        if (string.IsNullOrEmpty(appId)) return null;

        // Enrich from applications database
        var app = new ApplicationModel
        {
            AppId = appId,
            Priority = priority,
            IsRequired = isRequired,
            Status = ApplicationStatus.Pending,
            IsSelected = true
        };

        // Look up additional info from database
        if (_applicationsCache != null && _applicationsCache.TryGetValue(appId, out var appData))
        {
            app.Name = GetJsonString(appData, "Name") ?? appId;
            app.Category = GetJsonString(appData, "Category") ?? "Unknown";
            app.Description = GetJsonString(appData, "Description") ?? string.Empty;

            if (appData.TryGetProperty("DefaultPriority", out var defaultPriority) &&
                priority == 50)
            {
                app.Priority = defaultPriority.GetInt32();
            }

            if (appData.TryGetProperty("DefaultRequired", out var defaultRequired))
            {
                app.IsRequired = defaultRequired.GetBoolean();
            }
        }
        else
        {
            app.Name = appId;
            app.Category = Loc.Common_Unknown;
        }

        return app;
    }

    /// <summary>
    /// Ensures the applications database is loaded into cache.
    /// Thread-safe using double-check locking pattern with async semaphore.
    /// </summary>
    private async Task EnsureApplicationsCacheAsync()
    {
        // Quick check without lock
        if (_applicationsCache != null) return;

        await _cacheLock.WaitAsync();
        try
        {
            // Double-check inside lock
            if (_applicationsCache != null) return;

            var repoRoot = GetSafeRepositoryRoot();
            var dbPath = Path.Combine(repoRoot, "Apps", "Database", "applications.json");

            if (!File.Exists(dbPath))
            {
                _applicationsCache = new Dictionary<string, JsonElement>();
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(dbPath);
            using var document = JsonDocument.Parse(jsonContent);

            var newCache = new Dictionary<string, JsonElement>();

            if (document.RootElement.TryGetProperty("Applications", out var apps))
            {
                foreach (var app in apps.EnumerateObject())
                {
                    newCache[app.Name] = app.Value.Clone();
                }
            }

            // Atomic assignment after building the entire cache
            _applicationsCache = newCache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Safely gets a string value from a Hashtable.
    /// </summary>
    private static string GetStringValue(Hashtable ht, string key)
    {
        return ht[key]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Safely gets a string from a JsonElement.
    /// </summary>
    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    /// <summary>
    /// Executes a PowerShell script file from the repository.
    /// </summary>
    public async Task<string> ExecuteScriptAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = GetSafeRepositoryRoot();
        var scriptPath = Path.Combine(repoRoot, relativePath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script not found: {scriptPath}");
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var ps = CreatePowerShellInstance();
            ps.AddCommand("Set-ExecutionPolicy")
              .AddParameter("ExecutionPolicy", "RemoteSigned")
              .AddParameter("Scope", "Process")
              .AddParameter("Force");
            ps.Invoke();
            ps.Commands.Clear();

            ps.AddScript($"& '{scriptPath}'");
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell error: {errors}");
            }

            return string.Join(Environment.NewLine, results.Select(r => r?.ToString() ?? string.Empty));
        });
    }

    /// <summary>
    /// Executes a PowerShell command string.
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = GetSafeRepositoryRoot();
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ps = CreatePowerShellInstance();

            ps.AddScript($"Set-Location '{repoRoot}'");
            ps.Invoke();
            ps.Commands.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            ps.AddScript(command);
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell error: {errors}");
            }

            return string.Join(Environment.NewLine, results.Select(r => r?.ToString() ?? string.Empty));
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        await EnsureApplicationsCacheAsync();

        if (_applicationsCache == null || _applicationsCache.Count == 0)
        {
            return [];
        }

        var applications = new List<ApplicationModel>();

        foreach (var kvp in _applicationsCache)
        {
            var appId = kvp.Key;
            var appData = kvp.Value;

            var app = new ApplicationModel
            {
                AppId = appId,
                Name = GetJsonString(appData, "Name") ?? appId,
                Category = GetJsonString(appData, "Category") ?? "Unknown",
                Description = GetJsonString(appData, "Description") ?? string.Empty,
                Status = ApplicationStatus.Pending,
                IsSelected = false
            };

            // Get priority from DefaultPriority
            if (appData.TryGetProperty("DefaultPriority", out var priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            // Get required from DefaultRequired
            if (appData.TryGetProperty("DefaultRequired", out var requiredProp) &&
                (requiredProp.ValueKind == JsonValueKind.True || requiredProp.ValueKind == JsonValueKind.False))
            {
                app.IsRequired = requiredProp.GetBoolean();
            }

            // Build sources list from Sources object
            var sourcesList = new List<string>();
            if (appData.TryGetProperty("Sources", out var sourcesObj) &&
                sourcesObj.ValueKind == JsonValueKind.Object)
            {
                if (sourcesObj.TryGetProperty("Winget", out var winget) &&
                    winget.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(winget.GetString()))
                {
                    sourcesList.Add("Winget");
                }
                if (sourcesObj.TryGetProperty("Chocolatey", out var choco) &&
                    choco.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(choco.GetString()))
                {
                    sourcesList.Add("Chocolatey");
                }
                if (sourcesObj.TryGetProperty("Store", out var store) &&
                    store.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(store.GetString()))
                {
                    sourcesList.Add("Store");
                }
                if (sourcesObj.TryGetProperty("DirectUrl", out var url) &&
                    url.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(url.GetString()))
                {
                    sourcesList.Add("Direct");
                }
            }

            app.Sources = string.Join(", ", sourcesList);

            // Get ManualInstallOnly flag
            if (appData.TryGetProperty("ManualInstallOnly", out var manualProp) &&
                manualProp.ValueKind == JsonValueKind.True)
            {
                app.ManualInstallOnly = true;
            }

            // Get official URL (Homepage field)
            app.OfficialUrl = GetJsonString(appData, "Homepage") ?? string.Empty;

            // Get install notes
            app.InstallNotes = GetJsonString(appData, "InstallNotes") ?? string.Empty;

            applications.Add(app);
        }

        return applications.OrderBy(a => a.Name).ToList();
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetRawProfileAsync(string profileName)
    {
        // Validate profile name to prevent path traversal
        profileName = ValidateProfileName(profileName);

        await EnsureApplicationsCacheAsync();

        var repoRoot = GetSafeRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "Modules", "ProfileManager.psm1");
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1");
        var profilesDir = Path.Combine(repoRoot, "Profiles");
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        // Defense-in-depth: verify the resolved path stays within profiles directory
        profilePath = ValidatePathWithinDirectory(profilePath, profilesDir);

        return await Task.Run(() =>
        {
            using var ps = CreatePowerShellInstance();

            // Set execution policy
            ps.AddCommand("Set-ExecutionPolicy")
              .AddParameter("ExecutionPolicy", "RemoteSigned")
              .AddParameter("Scope", "Process")
              .AddParameter("Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Import modules
            ps.AddScript($"Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue");
            ps.Invoke();
            ps.Commands.Clear();

            ps.AddScript($"Import-Module '{modulePath}' -Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Call Import-ProfileJson to get raw profile
            ps.AddCommand("Import-ProfileJson")
              .AddParameter("Path", profilePath);

            var results = ps.Invoke();

            if (ps.HadErrors || results.Count == 0)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"Failed to load raw profile: {errors}");
            }

            return ConvertToRawDeploymentProfile(results[0]);
        });
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName)
    {
        return await LoadProfileAsync(profileName);
    }

    /// <summary>
    /// Converts a raw PowerShell profile object to DeploymentProfileModel.
    /// Only includes applications defined in this specific profile (not inherited).
    /// </summary>
    private DeploymentProfileModel ConvertToRawDeploymentProfile(PSObject? psObject)
    {
        if (psObject == null)
        {
            throw new InvalidOperationException("Profile object is null");
        }

        var profile = new DeploymentProfileModel
        {
            Name = psObject.Properties["Name"]?.Value?.ToString() ?? string.Empty,
            Description = psObject.Properties["Description"]?.Value?.ToString() ?? string.Empty,
            Version = psObject.Properties["Version"]?.Value?.ToString() ?? string.Empty
        };

        // Get parent profile (Inherits property)
        var inherits = psObject.Properties["Inherits"]?.Value;
        if (inherits is object[] inheritArray)
        {
            profile.InheritedFrom = inheritArray.Select(i => i?.ToString() ?? string.Empty).ToList();
        }
        else if (inherits is string inheritStr && !string.IsNullOrEmpty(inheritStr))
        {
            profile.InheritedFrom = [inheritStr];
        }

        // Get applications defined in this profile only
        var applications = psObject.Properties["Applications"]?.Value;
        if (applications is object[] appArray)
        {
            var appModels = new List<ApplicationModel>();

            foreach (var appObj in appArray)
            {
                var appModel = ConvertToApplicationModel(appObj);
                if (appModel != null)
                {
                    appModels.Add(appModel);
                }
            }

            profile.Applications = new ObservableCollection<ApplicationModel>(
                appModels.OrderBy(a => a.Priority));
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task<ApplicationStatus> GetApplicationStatusAsync(string appId)
    {
        var repoRoot = GetSafeRepositoryRoot();
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1").Replace("\\", "/");
        var dbModulePath = Path.Combine(repoRoot, "Modules", "ApplicationDatabase.psm1").Replace("\\", "/");
        var enginePath = Path.Combine(repoRoot, "Modules", "InstallationEngine.psm1").Replace("\\", "/");
        var detectionPath = Path.Combine(repoRoot, "Modules", "ApplicationDetection.psm1").Replace("\\", "/");

        try
        {
            // Use external PowerShell process for reliability
            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'SilentlyContinue'

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{detectionPath}' -Force -ErrorAction Stop

    $app = Get-ApplicationById -AppId '{EscapeForPowerShell(appId)}'
    if (-not $app) {{
        Write-Output 'NOT_FOUND'
        exit
    }}

    $isInstalled = Test-ApplicationInstalled -Application $app
    if ($isInstalled) {{
        Write-Output 'INSTALLED'
    }} else {{
        Write-Output 'NOT_INSTALLED'
    }}
}} catch {{
    Write-Output 'ERROR'
}}
";

            var output = await ExecutePowerShellScriptAsync(script);
            var result = output.Trim().Split('\n').LastOrDefault()?.Trim() ?? string.Empty;

            return result switch
            {
                "INSTALLED" => ApplicationStatus.Installed,
                "NOT_INSTALLED" => ApplicationStatus.Pending,
                _ => ApplicationStatus.Pending
            };
        }
        catch
        {
            return ApplicationStatus.Pending;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps)
    {
        if (apps == null || apps.Count == 0)
        {
            return new Dictionary<string, BatchAppStatus>();
        }

        var repoRoot = GetSafeRepositoryRoot();
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1").Replace("\\", "/");
        var dbModulePath = Path.Combine(repoRoot, "Modules", "ApplicationDatabase.psm1").Replace("\\", "/");
        var enginePath = Path.Combine(repoRoot, "Modules", "InstallationEngine.psm1").Replace("\\", "/");
        var detectionPath = Path.Combine(repoRoot, "Modules", "ApplicationDetection.psm1").Replace("\\", "/");

        try
        {
            // Build list of AppIds for PowerShell to look up from database
            var appIds = apps.Select(a => a.AppId).ToList();
            var appIdsJson = EscapeForPowerShell(JsonSerializer.Serialize(appIds));

            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'SilentlyContinue'

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{detectionPath}' -Force -ErrorAction Stop

    # Parse AppIds from JSON and get full app objects from database
    $appIds = '{appIdsJson}' | ConvertFrom-Json
    $apps = @()
    foreach ($appId in $appIds) {{
        $app = Get-ApplicationById -AppId $appId
        if ($app) {{
            $apps += $app
        }}
    }}

    # Call batch detection function
    $results = Get-ApplicationsInstallationStatus -Applications $apps

    # Output results as JSON
    $results | ConvertTo-Json -Compress
}} catch {{
    Write-Output ""___BATCH_ERROR___: $($_.Exception.Message)""
}}
";

            var output = await ExecutePowerShellScriptAsync(script);
            var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Check for error
            foreach (var line in lines)
            {
                if (line.Contains("___BATCH_ERROR___"))
                {
                    // Batch detection failed, return null to trigger fallback
                    return null;
                }
            }

            // Find JSON output (last line that looks like JSON)
            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    var result = new Dictionary<string, BatchAppStatus>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        var appId = prop.Name;
                        var isInstalled = false;
                        string? version = null;

                        // New format: { AppId: { IsInstalled: bool, Version: string } }
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("IsInstalled", out var installedProp))
                            {
                                isInstalled = installedProp.ValueKind == JsonValueKind.True;
                            }
                            if (prop.Value.TryGetProperty("Version", out var versionProp) &&
                                versionProp.ValueKind == JsonValueKind.String)
                            {
                                version = versionProp.GetString();
                            }
                        }
                        // Legacy format: { AppId: bool }
                        else if (prop.Value.ValueKind == JsonValueKind.True ||
                                 prop.Value.ValueKind == JsonValueKind.False)
                        {
                            isInstalled = prop.Value.GetBoolean();
                        }

                        var status = isInstalled ? ApplicationStatus.Installed : ApplicationStatus.Pending;
                        result[appId] = new BatchAppStatus(status, version);
                    }

                    return result;
                }
            }

            // No valid JSON found, return null for fallback
            return null;
        }
        catch
        {
            // Return null to indicate batch detection failed, caller should fallback
            return null;
        }
    }

    /// <summary>
    /// Gets batch application status using optimized registry-first detection.
    /// This method is 10-50x faster than PowerShell-based detection.
    ///
    /// Performance: ~300ms for 100 apps vs ~3000ms with PowerShell
    /// </summary>
    /// <param name="apps">Applications to check.</param>
    /// <returns>Dictionary of application statuses, or null if detection failed.</returns>
    public async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusFastAsync(IReadOnlyList<ApplicationModel> apps)
    {
        if (apps == null || apps.Count == 0)
        {
            return new Dictionary<string, BatchAppStatus>();
        }

        try
        {
            var detectionResult = await _detectionService.GetInstalledPackagesAsync();
            var result = new Dictionary<string, BatchAppStatus>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                var appId = app.AppId;

                // Try to find the application using multiple identifiers
                InstalledPackageInfo? packageInfo = null;

                // Try AppId first (often matches WinGet ID)
                if (!string.IsNullOrEmpty(appId))
                {
                    packageInfo = detectionResult.GetPackage(appId);
                }

                // Try app name (fuzzy match)
                if (packageInfo == null && !string.IsNullOrEmpty(app.Name))
                {
                    var normalizedName = app.Name.ToLowerInvariant().Replace(" ", "").Replace("-", "");
                    packageInfo = detectionResult.GetPackage(normalizedName);
                }

                // Try partial match on app name
                if (packageInfo == null && !string.IsNullOrEmpty(app.Name))
                {
                    // Search through all packages for a name match
                    var appNameLower = app.Name.ToLowerInvariant();
                    foreach (var pkg in detectionResult.Packages.Values)
                    {
                        if (pkg.Name.ToLowerInvariant().Contains(appNameLower) ||
                            appNameLower.Contains(pkg.Name.ToLowerInvariant()))
                        {
                            packageInfo = pkg;
                            break;
                        }
                    }
                }

                if (packageInfo != null)
                {
                    result[appId] = new BatchAppStatus(
                        ApplicationStatus.Installed,
                        packageInfo.InstalledVersion);
                }
                else
                {
                    result[appId] = new BatchAppStatus(ApplicationStatus.Pending, null);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fast batch detection failed: {ex.Message}");
            return null; // Caller should fallback to PowerShell detection
        }
    }

    /// <summary>
    /// Pre-warms the application detection cache for faster subsequent operations.
    /// Call this during application startup.
    /// </summary>
    public async Task WarmDetectionCacheAsync()
    {
        await _detectionService.WarmCacheAsync();
    }

    /// <summary>
    /// Gets cache statistics for diagnostics.
    /// </summary>
    public CacheStatistics GetDetectionCacheStatistics()
    {
        return _detectionService.GetCacheStatistics();
    }

    /// <summary>
    /// Clears the detection cache.
    /// </summary>
    public void ClearDetectionCache()
    {
        _detectionService.ClearCache();
    }

    /// <summary>
    /// Gets available updates using optimized batch detection.
    /// Single call returns all updates (~500ms total vs ~3500ms per app).
    /// </summary>
    public async Task<IReadOnlyList<UpdateInfo>> GetAvailableUpdatesAsync()
    {
        return await _detectionService.GetAvailableUpdatesAsync();
    }

    /// <inheritdoc/>
    public async Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds)
    {
        // Validate profile names to prevent path traversal
        profileName = ValidateProfileName(profileName);
        if (!string.IsNullOrEmpty(parentProfile))
        {
            parentProfile = ValidateProfileName(parentProfile);
        }

        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        // Defense-in-depth: verify the resolved path stays within profiles directory
        profilePath = ValidatePathWithinDirectory(profilePath, profilesDir);

        // Ensure profiles directory exists
        if (!Directory.Exists(profilesDir))
        {
            Directory.CreateDirectory(profilesDir);
        }

        // Get current version for the profile
        var version = await GetWin11ForgeVersionAsync();
        if (version == "Unknown" || version == "Error")
        {
            version = "2.6.0";
        }

        await Task.Run(() =>
        {
            // Build the profile JSON structure
            var profileObj = new Dictionary<string, object>
            {
                ["Name"] = profileName,
                ["Description"] = description,
                ["Version"] = version
            };

            // Add inheritance if parent is specified
            if (!string.IsNullOrEmpty(parentProfile) &&
                parentProfile != Resources.Resources.Editor_NoParent)
            {
                profileObj["Inherits"] = new[] { parentProfile };
            }

            // Add applications as string array of AppIds (database mode)
            if (addedAppIds.Count > 0)
            {
                profileObj["Applications"] = addedAppIds.ToArray();
            }
            else
            {
                profileObj["Applications"] = Array.Empty<string>();
            }

            // Serialize with indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase
            };

            var jsonContent = JsonSerializer.Serialize(profileObj, options);

            // Write to file
            File.WriteAllText(profilePath, jsonContent);
        });
    }

    /// <inheritdoc/>
    public async Task<SystemInfoModel> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfoModel
            {
                Hostname = Environment.MachineName,
                Username = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount
            };

            try
            {
                // Get Windows version using native .NET (more reliable than PowerShell SDK)
                info.WindowsVersion = GetWindowsVersionNative();
                info.WindowsBuild = GetWindowsBuildNative();

                // Get total memory using native .NET
                info.TotalMemoryGB = GetTotalMemoryNative();

                // Check if running as administrator using native .NET
                info.IsAdministrator = IsRunningAsAdministrator();

                // Check Winget availability using process
                var wingetVer = GetCommandVersion("winget", "--version");
                info.WingetAvailable = !string.IsNullOrEmpty(wingetVer);
                info.WingetVersion = wingetVer;

                // Check Chocolatey availability using process
                var chocoVer = GetCommandVersion("choco", "--version");
                info.ChocolateyAvailable = !string.IsNullOrEmpty(chocoVer);
                info.ChocolateyVersion = chocoVer;
            }
            catch
            {
                // Return partial info on error
            }

            return info;
        });
    }

    /// <summary>
    /// Gets Windows version string using native .NET/Registry.
    /// </summary>
    private static string GetWindowsVersionNative()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var productName = key?.GetValue("ProductName")?.ToString() ?? "Windows";
            var displayVersion = key?.GetValue("DisplayVersion")?.ToString();
            if (!string.IsNullOrEmpty(displayVersion))
            {
                return $"{productName} ({displayVersion})";
            }
            return productName;
        }
        catch
        {
            return $"Windows {Environment.OSVersion.Version.Major}";
        }
    }

    /// <summary>
    /// Gets Windows build number using native .NET/Registry.
    /// </summary>
    private static string GetWindowsBuildNative()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? key?.GetValue("CurrentBuild")?.ToString();
            var ubr = key?.GetValue("UBR")?.ToString();
            if (!string.IsNullOrEmpty(build))
            {
                return !string.IsNullOrEmpty(ubr) ? $"{build}.{ubr}" : build;
            }
            return Environment.OSVersion.Version.Build.ToString();
        }
        catch
        {
            return Environment.OSVersion.Version.Build.ToString();
        }
    }

    /// <summary>
    /// Gets total physical memory using native .NET.
    /// </summary>
    private static double GetTotalMemoryNative()
    {
        try
        {
            // Use GC to get approximate total memory (not perfect but works without WMI)
            var gcInfo = GC.GetGCMemoryInfo();
            return Math.Round(gcInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 1);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if the current process is running as administrator.
    /// </summary>
    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets version string from a command using process execution.
    /// </summary>
    private static string GetCommandVersion(string command, string arguments)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<PrerequisitesStatus> CheckPrerequisitesAsync()
    {
        // Refresh environment variables to pick up any changes from installations
        RefreshEnvironmentVariables();

        var script = @"
$result = @{}

# Check PowerShell 7
try {
    $ver = pwsh --version 2>$null
    $result.PowerShell7Installed = $null -ne $ver
    $result.PowerShellVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""PowerShell 7 check failed: $($_.Exception.Message)""
    $result.PowerShell7Installed = $false
    $result.PowerShellVersion = 'Not installed'
}

# Check Winget
try {
    $ver = winget --version 2>$null
    $result.WingetInstalled = $null -ne $ver
    $result.WingetVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""Winget check failed: $($_.Exception.Message)""
    $result.WingetInstalled = $false
    $result.WingetVersion = 'Not installed'
}

# Check Chocolatey
try {
    $ver = choco --version 2>$null
    $result.ChocolateyInstalled = $null -ne $ver
    $result.ChocolateyVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""Chocolatey check failed: $($_.Exception.Message)""
    $result.ChocolateyInstalled = $false
    $result.ChocolateyVersion = 'Not installed'
}

# Check .NET Core
try {
    $runtimes = dotnet --list-runtimes 2>$null
    $result.DotNetInstalled = $null -ne $runtimes -and $runtimes.Count -gt 0
    if ($result.DotNetInstalled) {
        $versions = @($runtimes | ForEach-Object { if ($_ -match 'Microsoft\.NETCore\.App (\d+\.\d+)') { $matches[1] } }) | Select-Object -Unique
        $result.DotNetVersion = ($versions -join ', ')
    } else {
        $result.DotNetVersion = 'Not installed'
    }
} catch {
    Write-Warning "".NET Core check failed: $($_.Exception.Message)""
    $result.DotNetInstalled = $false
    $result.DotNetVersion = 'Not installed'
}

# Check .NET Framework
try {
    $fxKey = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
    if ($fxKey -and $fxKey.Release) {
        $result.DotNetFrameworkInstalled = $true
        $releaseNum = $fxKey.Release
        $fxVersion = switch ($true) {
            ($releaseNum -ge 533320) { '4.8.1' }
            ($releaseNum -ge 528040) { '4.8' }
            ($releaseNum -ge 461808) { '4.7.2' }
            default { '4.x' }
        }
        $result.DotNetFrameworkVersion = $fxVersion
    } else {
        $result.DotNetFrameworkInstalled = $false
        $result.DotNetFrameworkVersion = 'Not installed'
    }
} catch {
    Write-Warning "".NET Framework check failed: $($_.Exception.Message)""
    $result.DotNetFrameworkInstalled = $false
    $result.DotNetFrameworkVersion = 'Not installed'
}

# Check Visual C++ Redistributable
try {
    $vcKeys = @(
        'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
    )
    $vcInfo = $null
    foreach ($key in $vcKeys) {
        if (Test-Path $key) {
            $vcInfo = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            if ($vcInfo) { break }
        }
    }
    if ($vcInfo -and $vcInfo.Version) {
        $result.VCRedistInstalled = $true
        $result.VCRedistVersion = ""2015-2022 ($($vcInfo.Version))""
    } else {
        $result.VCRedistInstalled = $false
        $result.VCRedistVersion = 'Not installed'
    }
} catch {
    Write-Warning ""Visual C++ Redistributable check failed: $($_.Exception.Message)""
    $result.VCRedistInstalled = $false
    $result.VCRedistVersion = 'Not installed'
}

# Check Java
try {
    $javaVer = java -version 2>&1 | Select-Object -First 1
    if ($LASTEXITCODE -eq 0 -and $javaVer) {
        $result.JavaInstalled = $true
        $result.JavaVersion = $javaVer.ToString().Trim()
    } else {
        $result.JavaInstalled = $false
        $result.JavaVersion = 'Not installed'
    }
} catch {
    Write-Warning ""Java check failed: $($_.Exception.Message)""
    $result.JavaInstalled = $false
    $result.JavaVersion = 'Not installed'
}

$result | ConvertTo-Json -Compress
";

        try
        {
            var output = await ExecutePowerShellScriptAsync(script);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    return new PrerequisitesStatus
                    {
                        PowerShell7Installed = GetJsonBool(root, "PowerShell7Installed"),
                        PowerShellVersion = GetJsonString(root, "PowerShellVersion") ?? string.Empty,
                        WingetInstalled = GetJsonBool(root, "WingetInstalled"),
                        WingetVersion = GetJsonString(root, "WingetVersion") ?? string.Empty,
                        ChocolateyInstalled = GetJsonBool(root, "ChocolateyInstalled"),
                        ChocolateyVersion = GetJsonString(root, "ChocolateyVersion") ?? string.Empty,
                        DotNetInstalled = GetJsonBool(root, "DotNetInstalled"),
                        DotNetVersion = GetJsonString(root, "DotNetVersion") ?? string.Empty,
                        DotNetFrameworkInstalled = GetJsonBool(root, "DotNetFrameworkInstalled"),
                        DotNetFrameworkVersion = GetJsonString(root, "DotNetFrameworkVersion") ?? string.Empty,
                        VCRedistInstalled = GetJsonBool(root, "VCRedistInstalled"),
                        VCRedistVersion = GetJsonString(root, "VCRedistVersion") ?? string.Empty,
                        JavaInstalled = GetJsonBool(root, "JavaInstalled"),
                        JavaVersion = GetJsonString(root, "JavaVersion") ?? string.Empty
                    };
                }
            }
        }
        catch
        {
            // Return default status on error
        }

        return new PrerequisitesStatus();
    }

    private static bool GetJsonBool(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Refreshes environment variables in the current process from the registry.
    /// This is needed after installing software that modifies PATH.
    /// </summary>
    private static void RefreshEnvironmentVariables()
    {
        try
        {
            // Read PATH from Machine and User registry keys
            using var machineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
            using var userKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Environment");

            var machinePath = machineKey?.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var userPath = userKey?.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";

            // Combine and set new PATH
            var combinedPath = $"{machinePath};{userPath}";
            // Expand environment variables
            combinedPath = Environment.ExpandEnvironmentVariables(combinedPath);
            // Remove duplicate semicolons
            while (combinedPath.Contains(";;"))
            {
                combinedPath = combinedPath.Replace(";;", ";");
            }

            Environment.SetEnvironmentVariable("Path", combinedPath, EnvironmentVariableTarget.Process);

            // Also refresh other common variables
            var commonVars = new[] { "JAVA_HOME", "ChocolateyInstall", "DOTNET_ROOT" };
            foreach (var varName in commonVars)
            {
                var machineValue = machineKey?.GetValue(varName) as string;
                var userValue = userKey?.GetValue(varName) as string;
                var value = userValue ?? machineValue;
                if (!string.IsNullOrEmpty(value))
                {
                    Environment.SetEnvironmentVariable(varName, value, EnvironmentVariableTarget.Process);
                }
            }
        }
        catch
        {
            // Environment refresh is non-critical
        }
    }

    /// <summary>
    /// Extracts readable messages from PowerShell output, filtering out CLIXML serialization.
    /// </summary>
    private static string ExtractReadableMessage(string line)
    {
        // If the line contains CLIXML, extract all ToString messages
        if (line.Contains("<Objs") || line.Contains("<ToString>"))
        {
            var messages = new List<string>();

            // Extract all ToString content (these contain the actual messages)
            var toStringPattern = new System.Text.RegularExpressions.Regex(
                @"<ToString>([^<]*)</ToString>",
                System.Text.RegularExpressions.RegexOptions.Compiled);

            var matches = toStringPattern.Matches(line);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var message = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    // Decode XML entities and newline markers
                    message = message.Replace("_x000D__x000A_", "\n")
                                     .Replace("_x000A_", "\n")
                                     .Replace("&lt;", "<")
                                     .Replace("&gt;", ">")
                                     .Replace("&amp;", "&")
                                     .Trim();

                    // Skip duplicate or empty messages
                    if (!string.IsNullOrWhiteSpace(message) && !messages.Contains(message))
                    {
                        messages.Add(message);
                    }
                }
            }

            return string.Join("\n", messages);
        }

        // Skip XML-looking lines
        if (line.StartsWith("<") && line.Contains(">"))
        {
            return string.Empty;
        }

        // Pass through normal text lines
        return line;
    }

    /// <inheritdoc/>
    public async Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = GetSafeRepositoryRoot();
        var prerequisitesModule = Path.Combine(repoRoot, "Modules", "Prerequisites.psm1").Replace("\\", "/");
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1").Replace("\\", "/");
        var localizationPath = Path.Combine(repoRoot, "Core", "Localization.psm1").Replace("\\", "/");
        var moduleLoaderPath = Path.Combine(repoRoot, "Core", "ModuleLoader.psm1").Replace("\\", "/");

        progressCallback?.Invoke(Resources.Resources.Prerequisites_Starting);

        try
        {
            progressCallback?.Invoke(Resources.Resources.Prerequisites_LoadingModules);

            // Build a PowerShell script that outputs progress in real-time
            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'Continue'

try {{
    # Load core modules explicitly to ensure all dependencies are available
    Import-Module '{moduleLoaderPath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{localizationPath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{prerequisitesModule}' -Force -ErrorAction Stop

    # Run prerequisites installation
    $result = Start-PrerequisitesInstallation

    # Output success marker
    Write-Output '___SUCCESS___'
}} catch {{
    Write-Output ""___ERROR___: $($_.Exception.Message)""
}}
";

            progressCallback?.Invoke(Resources.Resources.Prerequisites_Installing);

            // Execute with real-time output streaming
            var result = await ExecutePowerShellWithStreamingAsync(script, progressCallback, cancellationToken);

            if (result.Success)
            {
                // Refresh environment variables in current process
                RefreshEnvironmentVariables();
                progressCallback?.Invoke(Resources.Resources.Prerequisites_Complete);
                return true;
            }
            else
            {
                progressCallback?.Invoke($"Error: {result.ErrorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executes a PowerShell script with real-time output streaming.
    /// </summary>
    private async Task<(bool Success, string ErrorMessage)> ExecutePowerShellWithStreamingAsync(
        string script,
        Action<string>? outputCallback,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var psPath = GetPowerShellPath();
        var repoRoot = GetSafeRepositoryRoot();

        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        var success = false;
        var errorMessage = string.Empty;
        var outputComplete = new TaskCompletionSource<bool>();

        // Handle stdout line by line
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                var line = e.Data.Trim();

                if (line == "___SUCCESS___")
                {
                    success = true;
                }
                else if (line.StartsWith("___ERROR___:"))
                {
                    errorMessage = line.Substring("___ERROR___:".Length).Trim();
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    // Filter out CLIXML serialized data and extract readable messages
                    var cleanLine = ExtractReadableMessage(line);
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                    {
                        outputCallback?.Invoke(cleanLine);
                    }
                }
            }
        };

        // Handle stderr
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                outputCallback?.Invoke($"[ERROR] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait with installation timeout for prerequisites, linked with external cancellation
        using var prereqTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(prereqTimeoutCts.Token, cancellationToken);
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }

            if (cancellationToken.IsCancellationRequested)
            {
                throw; // Re-throw for external cancellation
            }

            return (false, $"Prerequisites installation timed out after {InstallationTimeoutMs / 60000} minutes");
        }

        // If process exited with 0 and we saw success marker, it's successful
        if (process.ExitCode == 0 && success)
        {
            return (true, string.Empty);
        }

        // If we have an error message, return it
        if (!string.IsNullOrEmpty(errorMessage))
        {
            return (false, errorMessage);
        }

        // Fallback: check exit code
        if (process.ExitCode == 0)
        {
            return (true, string.Empty);
        }

        return (false, $"Process exited with code {process.ExitCode}");
    }

    /// <summary>
    /// Finds the PowerShell executable path (pwsh.exe or powershell.exe).
    /// </summary>
    private static string FindPowerShellExecutable()
    {
        // Check for pwsh.exe in common locations
        var pwshPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerShell", "7", "pwsh.exe"),
            @"C:\Program Files\PowerShell\7\pwsh.exe"
        };

        foreach (var path in pwshPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fall back to Windows PowerShell
        return "powershell.exe";
    }

    /// <summary>
    /// Resolves the repository root path from the executable location.
    /// Walks up directories looking for repository markers (Config/version.json, Modules/).
    /// </summary>
    private static string ResolveRepositoryRoot()
    {
        // Build list of candidate paths, filtering out nulls
        var candidatePaths = new List<string>();

        // Priority 1: ProcessPath directory (best for single-file apps)
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                candidatePaths.Add(dir);
            }
        }

        // Priority 2: BaseDirectory
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            candidatePaths.Add(baseDir);
        }

        // Priority 3: CurrentDirectory
        var currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            candidatePaths.Add(currentDir);
        }

        foreach (var basePath in candidatePaths)
        {
            var result = TryFindRepositoryRoot(basePath);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Win11Forge repository root. Searched from: {string.Join(", ", candidatePaths)}");
    }

    /// <summary>
    /// Attempts to find repository root by walking up from a base path.
    /// </summary>
    private static string? TryFindRepositoryRoot(string basePath)
    {
        var currentDir = new DirectoryInfo(basePath);

        while (currentDir != null)
        {
            // Check for Config/version.json
            var versionFile = Path.Combine(currentDir.FullName, "Config", "version.json");
            if (File.Exists(versionFile))
            {
                return currentDir.FullName;
            }

            // Check for Modules/InstallationEngine.psm1
            var modulesDir = Path.Combine(currentDir.FullName, "Modules");
            if (Directory.Exists(modulesDir))
            {
                var coreModule = Path.Combine(modulesDir, "InstallationEngine.psm1");
                if (File.Exists(coreModule))
                {
                    return currentDir.FullName;
                }
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Releases all resources used by the PowerShellBridge.
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
            _cacheLock.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// Wrapper that mimics PowerShell SDK API but uses external process execution.
/// This is needed because the PowerShell SDK doesn't work in single-file deployments.
/// </summary>
internal class PowerShellProcessWrapper : IDisposable
{
    private readonly string _psPath;
    private readonly string _workingDir;
    private readonly List<string> _scripts = new();
    private readonly List<string> _errors = new();
    private readonly List<object> _results = new();
    private bool _hadErrors;

    public PowerShellProcessWrapper(string psPath, string workingDir)
    {
        _psPath = psPath;
        _workingDir = workingDir;
    }

    public bool HadErrors => _hadErrors;
    public PowerShellStreams Streams => new(_errors);

    public PowerShellProcessWrapper AddCommand(string command)
    {
        _scripts.Add(command);
        return this;
    }

    public PowerShellProcessWrapper AddParameter(string name, object? value = null)
    {
        if (_scripts.Count > 0)
        {
            var lastScript = _scripts[^1];
            if (value != null)
            {
                var valueStr = PowerShellBridge.EscapeForPowerShell(value.ToString() ?? "");
                _scripts[^1] = $"{lastScript} -{name} '{valueStr}'";
            }
            else
            {
                // Switch parameter (no value)
                _scripts[^1] = $"{lastScript} -{name}";
            }
        }
        return this;
    }

    public PowerShellProcessWrapper AddScript(string script)
    {
        _scripts.Add(script);
        return this;
    }

    public System.Collections.ObjectModel.Collection<PSObject> Invoke()
    {
        var result = new System.Collections.ObjectModel.Collection<PSObject>();

        if (_scripts.Count == 0)
            return result;

        var fullScript = string.Join("; ", _scripts);
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(fullScript));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = _workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            // Wait with timeout (5 minutes for script execution)
            const int processTimeoutMs = 300000; // 5 minutes
            if (!process.WaitForExit(processTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                throw new TimeoutException($"Script execution timed out after {processTimeoutMs / 1000} seconds");
            }

            if (!string.IsNullOrEmpty(error))
            {
                _errors.Add(error);
                _hadErrors = true;
            }

            if (!string.IsNullOrEmpty(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(new PSObject(line.Trim()));
                }
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            _hadErrors = true;
        }

        return result;
    }

    public void Clear()
    {
        _scripts.Clear();
    }

    public PowerShellCommands Commands => new(this);

    public void Dispose()
    {
        _scripts.Clear();
        _errors.Clear();
    }
}

/// <summary>
/// Wrapper for PowerShell streams.
/// </summary>
internal class PowerShellStreams
{
    private readonly List<string> _errors;

    public PowerShellStreams(List<string> errors)
    {
        _errors = errors;
    }

    public IEnumerable<PowerShellErrorRecord> Error => _errors.Select(e => new PowerShellErrorRecord(e));
    public PowerShellDataCollection Information => new();
    public PowerShellDataCollection Warning => new();
    public PowerShellDataCollection Verbose => new();
}

/// <summary>
/// Wrapper for error records.
/// </summary>
internal class PowerShellErrorRecord
{
    private readonly string _message;

    public PowerShellErrorRecord(string message)
    {
        _message = message;
    }

    public override string ToString() => _message;
}

/// <summary>
/// Wrapper for data collection with DataAdded event.
/// </summary>
internal class PowerShellDataCollection
{
    private readonly List<string> _items = new();

    public event EventHandler<PowerShellDataAddedEventArgs>? DataAdded;

    public string? this[int index] => index >= 0 && index < _items.Count ? _items[index] : null;

    public void Add(string item)
    {
        _items.Add(item);
        DataAdded?.Invoke(this, new PowerShellDataAddedEventArgs { Index = _items.Count - 1 });
    }
}

/// <summary>
/// Event args for data added.
/// </summary>
internal class PowerShellDataAddedEventArgs : EventArgs
{
    public int Index { get; set; }
}

/// <summary>
/// Wrapper for commands collection.
/// </summary>
internal class PowerShellCommands
{
    private readonly PowerShellProcessWrapper _wrapper;

    public PowerShellCommands(PowerShellProcessWrapper wrapper)
    {
        _wrapper = wrapper;
    }

    public void Clear() => _wrapper.Clear();
}

/// <summary>
/// Simple PSObject replacement for process-based execution.
/// </summary>
internal class PSObject
{
    public object? BaseObject { get; }

    public PSObject(object? baseObject = null)
    {
        BaseObject = baseObject;
    }

    public PSObjectProperties Properties => new();

    public override string? ToString() => BaseObject?.ToString();
}

/// <summary>
/// Properties collection for PSObject.
/// </summary>
internal class PSObjectProperties
{
    public PSObjectProperty? this[string name] => null;
}

/// <summary>
/// Property for PSObject.
/// </summary>
internal class PSObjectProperty
{
    public object? Value { get; set; }
}
