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
using System.Text;
using System.Text.RegularExpressions;
using WinForge.GUI.Configuration;

namespace WinForge.GUI.Services;

/// <summary>
/// Service implementation for package discovery across Winget, Chocolatey and Microsoft Store.
/// </summary>
public partial class PackageSearchService : IPackageSearchService
{
    private readonly ILoggingService _logger;
    private readonly Lazy<bool> _wingetAvailable;
    private readonly Lazy<bool> _chocoAvailable;

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiEscapeRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ColumnSplitRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}\s\.\-_+]")]
    private static partial Regex UnsafeQueryCharsRegex();

    /// <summary>
    /// Initializes a new instance of PackageSearchService.
    /// </summary>
    public PackageSearchService(ILoggerFactory? loggerFactory = null)
    {
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<PackageSearchService>();
        _wingetAvailable = new Lazy<bool>(CheckWingetAvailable);
        _chocoAvailable = new Lazy<bool>(CheckChocoAvailable);
    }

    /// <inheritdoc/>
    public bool IsWingetAvailable => _wingetAvailable.Value;

    /// <inheritdoc/>
    public bool IsChocolateyAvailable => _chocoAvailable.Value;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PackageSearchResult>> SearchWingetAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        if (!IsWingetAvailable)
        {
            throw new InvalidOperationException("Winget is not available.");
        }

        string safeQuery = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            return Array.Empty<PackageSearchResult>();
        }

        (int exitCode, string? output, string? errorOutput) = await RunProcessAsync(
            "winget",
            new[] { "search", safeQuery, "--source", "winget", "--accept-source-agreements" },
            cancellationToken);

        if (exitCode != 0 && !ContainsNoResultMarker(output, errorOutput))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput) ? output : errorOutput);
        }

        IReadOnlyList<PackageSearchResult> results = ParseWingetOutput(output, PackageSource.Winget, "winget");
        return results.Take(NormalizeMaxResults(maxResults)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PackageSearchResult>> SearchChocolateyAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        if (!IsChocolateyAvailable)
        {
            throw new InvalidOperationException("Chocolatey is not available.");
        }

        string safeQuery = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            return Array.Empty<PackageSearchResult>();
        }

        (int exitCode, string? output, string? errorOutput) = await RunProcessAsync(
            "choco",
            new[] { "search", safeQuery, "--limit-output" },
            cancellationToken);

        if (exitCode != 0 && !ContainsNoResultMarker(output, errorOutput))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput) ? output : errorOutput);
        }

        IReadOnlyList<PackageSearchResult> results = ParseChocolateyOutput(output);
        return results.Take(NormalizeMaxResults(maxResults)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PackageSearchResult>> SearchStoreAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        if (!IsWingetAvailable)
        {
            throw new InvalidOperationException("Winget is not available.");
        }

        string safeQuery = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            return Array.Empty<PackageSearchResult>();
        }

        (int exitCode, string? output, string? errorOutput) = await RunProcessAsync(
            "winget",
            new[] { "search", safeQuery, "--source", "msstore", "--accept-source-agreements" },
            cancellationToken);

        if (exitCode != 0 && !ContainsNoResultMarker(output, errorOutput))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput) ? output : errorOutput);
        }

        IReadOnlyList<PackageSearchResult> results = ParseWingetOutput(output, PackageSource.Store, "msstore");
        return results.Take(NormalizeMaxResults(maxResults)).ToList();
    }

    private static int NormalizeMaxResults(int maxResults)
    {
        if (maxResults <= 0) return 15;
        return Math.Min(maxResults, 50);
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        string trimmed = query.Trim();
        if (trimmed.Length > 120)
        {
            trimmed = trimmed[..120];
        }

        // Keep search user-friendly while dropping control/shell-sensitive characters.
        trimmed = UnsafeQueryCharsRegex().Replace(trimmed, string.Empty);
        return trimmed.Trim();
    }

    private static bool ContainsNoResultMarker(string output, string errorOutput)
    {
        string combined = $"{output}\n{errorOutput}";
        return combined.Contains("No package found", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("0 packages found", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PackageSearchResult> ParseChocolateyOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<PackageSearchResult>();
        }

        HashSet<string> uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<PackageSearchResult> results = new List<PackageSearchResult>();
        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string text = StripAnsi(line).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text.StartsWith("Chocolatey", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("packages found", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("0 packages found", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separatorIndex = text.IndexOf('|');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string packageId = text[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(packageId) || !uniqueIds.Add(packageId))
            {
                continue;
            }

            string versionRaw = text[(separatorIndex + 1)..].Trim();
            string? version = string.IsNullOrWhiteSpace(versionRaw) ? null : versionRaw;

            results.Add(new PackageSearchResult(
                packageId,
                packageId,
                version,
                PackageSource.Chocolatey));
        }

        return results;
    }

    private static IReadOnlyList<PackageSearchResult> ParseWingetOutput(
        string output,
        PackageSource source,
        string expectedSourceToken)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<PackageSearchResult>();
        }

        List<string> lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => StripAnsi(l).TrimEnd())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            return Array.Empty<PackageSearchResult>();
        }

        int startIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string current = lines[i].Trim();
            if (LooksLikeWingetHeader(current))
            {
                startIndex = i + 1;
                if (startIndex < lines.Count && IsSeparatorLine(lines[startIndex]))
                {
                    startIndex++;
                }
                break;
            }
        }

        List<PackageSearchResult> results = new List<PackageSearchResult>();
        HashSet<string> uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = startIndex; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || IsSeparatorLine(line) || IsWingetNoiseLine(line))
            {
                continue;
            }

            string[] columns = ColumnSplitRegex()
                .Split(line)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();

            if (columns.Length < 3)
            {
                continue;
            }

            string packageName = columns[0].Trim();
            string packageId = columns[1].Trim();
            string? version = NormalizeVersion(columns[2].Trim());
            string sourceToken = columns.Length >= 4 ? columns[3].Trim() : expectedSourceToken;

            if (string.IsNullOrWhiteSpace(packageId) ||
                string.IsNullOrWhiteSpace(packageName) ||
                !uniqueIds.Add(packageId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sourceToken) &&
                !sourceToken.Equals(expectedSourceToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new PackageSearchResult(
                packageId,
                packageName,
                version,
                source));
        }

        return results;
    }

    private static string? NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static bool LooksLikeWingetHeader(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] columns = ColumnSplitRegex()
            .Split(line.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (columns.Length < 3)
        {
            return false;
        }

        bool hasIdColumn = columns.Any(c => c.Equals("Id", StringComparison.OrdinalIgnoreCase));
        bool hasVersionColumn = columns.Any(c => c.Equals("Version", StringComparison.OrdinalIgnoreCase));
        return hasIdColumn && hasVersionColumn;
    }

    private static bool IsSeparatorLine(string line)
    {
        return line.Trim().All(ch => ch == '-' || ch == ' ');
    }

    private static bool IsWingetNoiseLine(string line)
    {
        return line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("No package found", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("The following source", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Do you agree", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Agreements", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripAnsi(string value) => AnsiEscapeRegex().Replace(value, string.Empty);

    private static bool CheckWingetAvailable()
    {
        try
        {
            (int exitCode, string _, string _) = RunProcessSync("winget", "--version");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckChocoAvailable()
    {
        try
        {
            (int exitCode, string _, string _) = RunProcessSync("choco", "--version");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output, string ErrorOutput) RunProcessSync(string fileName, string arguments)
    {
        using Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string errorOutput = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        return (process.ExitCode, output, errorOutput);
    }

    private async Task<(int ExitCode, string Output, string ErrorOutput)> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        using Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeoutDefaults.PackageOperation);

        try
        {
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeoutCts.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException($"{fileName} search timed out.");
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }

        return (process.ExitCode, await outputTask, await errorTask);
    }

    private void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to kill process {process.StartInfo.FileName}: {ex.Message}");
        }
    }
}
