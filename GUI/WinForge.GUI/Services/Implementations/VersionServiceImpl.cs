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

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WinForge.GUI.Services;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Implementation of IVersionService for retrieving version information.
/// </summary>
public partial class VersionServiceImpl : IVersionService
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly ILoggingService _logger;

    /// <summary>
    /// Compiled regex for extracting version strings from winget output.
    /// Matches patterns like "3.7.7", "1.0.0-beta", "2024.1.0".
    /// </summary>
    [GeneratedRegex(@"^[\d]+[.\d\-a-zA-Z_]*")]
    private static partial Regex WingetVersionPattern();

    /// <summary>
    /// Compiled regex for extracting leading digits from version parts.
    /// </summary>
    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingDigitsPattern();

    /// <summary>
    /// Initializes a new instance of the VersionServiceImpl.
    /// </summary>
    /// <param name="pathService">The repository path service.</param>
    /// <param name="executionService">The PowerShell execution service.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public VersionServiceImpl(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        ILoggerFactory? loggerFactory = null)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<VersionServiceImpl>();
    }

    /// <inheritdoc/>
    public async Task<string> GetWinForgeVersionAsync()
    {
        string versionFilePath = _pathService.GetPath("Config", "version.json");

        if (!File.Exists(versionFilePath))
        {
            return "Unknown";
        }

        try
        {
            string jsonContent = await File.ReadAllTextAsync(versionFilePath);
            using JsonDocument document = JsonDocument.Parse(jsonContent);

            if (document.RootElement.TryGetProperty("Version", out JsonElement versionElement))
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

    /// <summary>
    /// Gets the installed version of a package using winget list (SAFE: read-only).
    /// </summary>
    /// <param name="wingetId">The Winget package ID.</param>
    /// <returns>The installed version string, or empty if not found.</returns>
    public async Task<string> GetInstalledVersionAsync(string wingetId)
    {
        try
        {
            // SAFE COMMAND: winget list only reads information, never modifies
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"list --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Process kill failed (best effort): {ex.Message}");
                }
                return string.Empty;
            }

            // Clean output from progress indicators before parsing
            string cleanOutput = CleanWingetOutput(output);

            // Parse version from list output using column-based parsing
            return ParseVersionFromWingetList(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the available version from repository using winget show (SAFE: read-only).
    /// </summary>
    /// <param name="wingetId">The Winget package ID.</param>
    /// <returns>The available version string, or empty if not found.</returns>
    public async Task<string> GetRepositoryVersionAsync(string wingetId)
    {
        try
        {
            // SAFE COMMAND: winget show only displays info, never modifies
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"show --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Process kill failed (best effort): {ex.Message}");
                }
                return string.Empty;
            }

            // Clean output from progress indicators before parsing
            string cleanOutput = CleanWingetOutput(output);

            // Parse version from show output
            return ParseVersionFromWingetShow(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleans winget output by removing progress spinner characters.
    /// </summary>
    private static string CleanWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        List<string> cleanLines = new List<string>();
        string[] lines = output.Split('\n');

        foreach (string line in lines)
        {
            // Split by \r and take the last non-empty segment
            string[] segments = line.Split('\r');
            string? lastSegment = segments
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
    /// Parses version from 'winget list' output.
    /// </summary>
    public static string ParseVersionFromWingetList(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int versionColumnStart = -1;
        int sourceColumnStart = -1;
        string? headerLine = null;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("-"))
                continue;

            if ((trimmedLine.Contains("Version", StringComparison.OrdinalIgnoreCase)) &&
                (trimmedLine.Contains("Source", StringComparison.OrdinalIgnoreCase) ||
                 trimmedLine.Contains("Quelle", StringComparison.OrdinalIgnoreCase)))
            {
                headerLine = line;
                versionColumnStart = line.IndexOf("Version", StringComparison.OrdinalIgnoreCase);

                sourceColumnStart = line.IndexOf("Source", StringComparison.OrdinalIgnoreCase);
                if (sourceColumnStart < 0)
                    sourceColumnStart = line.IndexOf("Quelle", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (versionColumnStart < 0 || headerLine == null)
                continue;

            if (trimmedLine.Contains("---"))
                continue;

            if (line.Length > versionColumnStart)
            {
                int endPos = sourceColumnStart > versionColumnStart
                    ? Math.Min(sourceColumnStart, line.Length)
                    : line.Length;

                string versionPart = line.Substring(versionColumnStart, endPos - versionColumnStart).Trim();

                Match versionMatch = WingetVersionPattern().Match(versionPart);

                if (versionMatch.Success && versionMatch.Value.Contains('.'))
                {
                    return versionMatch.Value;
                }

                string? firstWord = versionPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
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
    /// </summary>
    public static string ParseVersionFromWingetShow(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0 && colonIndex < line.Length - 1)
                {
                    string version = line.Substring(colonIndex + 1).Trim();

                    Match match = WingetVersionPattern().Match(version);

                    if (match.Success && match.Value.Contains('.'))
                    {
                        return match.Value;
                    }

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
    public static int CompareVersions(string v1, string v2)
    {
        if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
        if (string.IsNullOrEmpty(v1)) return -1;
        if (string.IsNullOrEmpty(v2)) return 1;

        int[] parts1 = v1.Split('.', '-').Select(p =>
        {
            Match numMatch = LeadingDigitsPattern().Match(p);
            return numMatch.Success ? int.Parse(numMatch.Value) : 0;
        }).ToArray();

        int[] parts2 = v2.Split('.', '-').Select(p =>
        {
            Match numMatch = LeadingDigitsPattern().Match(p);
            return numMatch.Success ? int.Parse(numMatch.Value) : 0;
        }).ToArray();

        int maxLength = Math.Max(parts1.Length, parts2.Length);

        for (int i = 0; i < maxLength; i++)
        {
            int p1 = i < parts1.Length ? parts1[i] : 0;
            int p2 = i < parts2.Length ? parts2[i] : 0;

            if (p1 < p2) return -1;
            if (p1 > p2) return 1;
        }

        return 0;
    }
}
