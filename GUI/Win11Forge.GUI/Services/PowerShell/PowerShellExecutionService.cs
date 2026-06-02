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
using System.Text.RegularExpressions;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Service for executing PowerShell scripts and commands.
/// </summary>
public class PowerShellExecutionService : IPowerShellExecutionService
{
    private readonly IRepositoryPathService _pathService;
    private readonly ILoggingService _logger;
    private static string? _powerShellPath;

    /// <summary>
    /// Maximum allowed output size in bytes (100 MB) to prevent DoS via memory exhaustion.
    /// </summary>
    private const int MaxOutputSizeBytes = 100 * 1024 * 1024;

    /// <inheritdoc/>
    public int DefaultQueryTimeoutMs => 300000; // 5 minutes

    /// <inheritdoc/>
    public int InstallationTimeoutMs => 2850000; // 47.5 minutes (must exceed Office C2R 45 min timeout)

    /// <summary>
    /// Initializes a new instance of the PowerShellExecutionService.
    /// </summary>
    /// <param name="pathService">The repository path service.</param>
    public PowerShellExecutionService(IRepositoryPathService pathService, ILoggerFactory? loggerFactory = null)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<PowerShellExecutionService>();
    }

    /// <inheritdoc/>
    public string GetPowerShellPath()
    {
        if (_powerShellPath != null) return _powerShellPath;

        // Try PowerShell 7+ in multiple locations
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Check Program Files for PowerShell 7.x installations
        if (!string.IsNullOrEmpty(programFiles))
        {
            string psBaseDir = Path.Combine(programFiles, "PowerShell");
            if (Directory.Exists(psBaseDir))
            {
                // Look for any version 7+ directory (7, 7.0, 7.4.0, etc.)
                try
                {
                    List<string> versionDirs = Directory.GetDirectories(psBaseDir)
                        .Where(d => Path.GetFileName(d).StartsWith("7"))
                        .OrderByDescending(d => d)
                        .ToList();

                    foreach (string? versionDir in versionDirs)
                    {
                        string pwshPath = Path.Combine(versionDir, "pwsh.exe");
                        if (File.Exists(pwshPath))
                        {
                            _powerShellPath = pwshPath;
                            return _powerShellPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Directory enumeration failed, try direct path
                    _logger.LogWarning($"[PowerShellExecutionService] Directory enumeration failed: {ex.Message}");
                }

                // Direct path fallback
                string directPath = Path.Combine(psBaseDir, "7", "pwsh.exe");
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
            string storeAppPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "pwsh.exe");
            if (File.Exists(storeAppPath))
            {
                _powerShellPath = storeAppPath;
                return _powerShellPath;
            }
        }

        // Try pwsh in PATH (covers winget, scoop, chocolatey installations)
        try
        {
            string? pwshInPath = FindExecutableInPath("pwsh.exe");
            if (!string.IsNullOrEmpty(pwshInPath) && File.Exists(pwshInPath))
            {
                _powerShellPath = pwshInPath;
                return _powerShellPath;
            }
        }
        catch (Exception ex)
        {
            // PATH search failed
            _logger.LogWarning($"[PowerShellExecutionService] PATH search failed: {ex.Message}");
        }

        // Try Windows PowerShell as last resort
        string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(systemPath))
        {
            string winPsPath = Path.Combine(systemPath, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPsPath))
            {
                _powerShellPath = winPsPath;
                return _powerShellPath;
            }
        }

        // Final fallback with validation warning
        // Try 'powershell' first (more commonly available), then 'pwsh'
        foreach (string? candidate in new[] { "powershell", "pwsh" })
        {
            try
            {
                string? foundPath = FindExecutableInPath($"{candidate}.exe");
                if (!string.IsNullOrEmpty(foundPath))
                {
                    _powerShellPath = foundPath;
                    _logger.LogWarning($"[PowerShellExecutionService] Using fallback PowerShell: {_powerShellPath}");
                    return _powerShellPath;
                }
            }
            catch (Exception ex)
            {
                // Continue to next candidate
                _logger.LogWarning($"[PowerShellExecutionService] Candidate {candidate} failed: {ex.Message}");
            }
        }

        // Last resort - may fail at runtime if not in PATH
        _logger.LogWarning("[PowerShellExecutionService] WARNING: No PowerShell installation found. Using 'pwsh' and hoping it's in PATH.");
        _powerShellPath = "pwsh";
        return _powerShellPath;
    }

    /// <summary>
    /// Searches for an executable in the PATH environment variable.
    /// </summary>
    private string? FindExecutableInPath(string executableName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        string[] paths = pathEnv.Split(Path.PathSeparator);
        foreach (string path in paths)
        {
            try
            {
                string fullPath = Path.Combine(path.Trim(), executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                // Invalid path, skip - this is expected for some PATH entries
                _logger.LogDebug($"[PowerShellExecutionService] Invalid PATH entry skipped: {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a PowerShell instance wrapper for fluent script building.
    /// Internal method for use by other services in this assembly.
    /// </summary>
    /// <returns>A PowerShellProcessWrapper instance.</returns>
    internal PowerShellProcessWrapper CreatePowerShellInstance()
    {
        return new PowerShellProcessWrapper(GetPowerShellPath(), _pathService.GetSafeRepositoryRoot());
    }

    /// <inheritdoc/>
    public async Task<string> ExecutePowerShellScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string psPath = GetPowerShellPath();
        string repoRoot = _pathService.GetSafeRepositoryRoot();

        // Escape script for command line
        string encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        Process process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();

            // Read stdout and stderr concurrently with size limits to prevent DoS
            Task<string> outputTask = ReadStreamWithLimitAsync(process.StandardOutput, MaxOutputSizeBytes);
            Task<string> errorTask = ReadStreamWithLimitAsync(process.StandardError, MaxOutputSizeBytes);

            // Wait for both streams AND the process to complete with timeout
            using CancellationTokenSource timeoutCts = new CancellationTokenSource(DefaultQueryTimeoutMs);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(linkedCts.Token));
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { _logger.LogWarning($"Process kill failed (best effort): {ex.Message}"); }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new TimeoutException($"PowerShell script execution timed out after {DefaultQueryTimeoutMs / 1000} seconds");
            }

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"PowerShell error: {error}");
            }

            return output;
        }
        finally
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
                _logger.LogWarning($"Process cleanup failed: {ex.Message}");
            }
            process.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> ExecutePowerShellWithStreamingAsync(
        string script,
        Action<string>? outputCallback,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string psPath = GetPowerShellPath();
        string repoRoot = _pathService.GetSafeRepositoryRoot();

        string encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        Process process = new Process { StartInfo = startInfo };
        bool success = false;
        string errorMessage = string.Empty;

        try
        {
            // Handle stdout line by line
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    string line = e.Data.Trim();

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
                        string cleanLine = ExtractReadableMessage(line);
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            outputCallback?.Invoke(cleanLine);
                        }
                    }
                }
            };

            // Handle stderr - filter out CLIXML serialization noise
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string data = e.Data.Trim();

                    // Skip CLIXML header and serialized data
                    if (data.StartsWith("#< CLIXML") ||
                        data.StartsWith("<Objs") ||
                        data.StartsWith("</Objs") ||
                        data.StartsWith("<Obj ") ||
                        data.StartsWith("</Obj>") ||
                        data.StartsWith("<TNRef") ||
                        data.StartsWith("<TN ") ||
                        data.StartsWith("</TN>") ||
                        data.StartsWith("<T>") ||
                        data.StartsWith("<MS>") ||
                        data.StartsWith("</MS>") ||
                        data.StartsWith("<Props>") ||
                        data.StartsWith("</Props>") ||
                        data.StartsWith("<I64 ") ||
                        data.StartsWith("<PR ") ||
                        data.StartsWith("<LST>") ||
                        data.StartsWith("</LST>") ||
                        data.StartsWith("<S ") ||
                        data.StartsWith("<B ") ||
                        data.StartsWith("<U32 ") ||
                        data.StartsWith("<DT ") ||
                        data.StartsWith("<AV>") ||
                        data.StartsWith("<AI>") ||
                        data.StartsWith("<Nil") ||
                        data.Contains("S=\"progress\"") ||
                        data.Contains("S=\"information\"") ||
                        data.Contains("S=\"warning\""))
                    {
                        // Skip CLIXML noise, but try to extract readable content if present
                        string cleanLine = ExtractReadableMessage(data);
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            outputCallback?.Invoke(cleanLine);
                        }
                        return;
                    }

                    // Only prefix with [ERROR] for actual error content
                    outputCallback?.Invoke($"[ERROR] {data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait with installation timeout, linked with external cancellation
            using CancellationTokenSource prereqTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(prereqTimeoutCts.Token, cancellationToken);
            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { _logger.LogWarning($"Process kill failed (best effort): {ex.Message}"); }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return (false, $"Operation timed out after {InstallationTimeoutMs / 60000} minutes");
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
        finally
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
                _logger.LogWarning($"Process cleanup failed: {ex.Message}");
            }
            process.Dispose();
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
            List<string> messages = new List<string>();

            // Extract all ToString content (these contain the actual messages)
            Regex toStringPattern = new Regex(
                @"<ToString>([^<]*)</ToString>",
                RegexOptions.Compiled);

            MatchCollection matches = toStringPattern.Matches(line);
            foreach (Match match in matches)
            {
                string message = match.Groups[1].Value;
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

    /// <summary>
    /// Reads from a stream with a maximum size limit to prevent DoS via memory exhaustion.
    /// </summary>
    /// <param name="reader">The stream reader to read from.</param>
    /// <param name="maxBytes">Maximum bytes to read before truncating.</param>
    /// <returns>The content read from the stream, potentially truncated.</returns>
    private static async Task<string> ReadStreamWithLimitAsync(StreamReader reader, int maxBytes)
    {
        char[] buffer = new char[8192];
        StringBuilder result = new System.Text.StringBuilder();
        int totalBytesRead = 0;
        bool truncated = false;

        while (true)
        {
            int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (charsRead == 0) break;

            // Estimate byte count (UTF-16 chars can be 2-4 bytes in UTF-8)
            int estimatedBytes = charsRead * 2;

            if (totalBytesRead + estimatedBytes > maxBytes)
            {
                // Calculate how many chars we can still accept
                int remainingBytes = maxBytes - totalBytesRead;
                int charsToTake = Math.Max(0, remainingBytes / 2);

                if (charsToTake > 0)
                {
                    result.Append(buffer, 0, Math.Min(charsRead, charsToTake));
                }

                truncated = true;
                break;
            }

            result.Append(buffer, 0, charsRead);
            totalBytesRead += estimatedBytes;
        }

        if (truncated)
        {
            result.AppendLine();
            result.AppendLine($"[OUTPUT TRUNCATED: Exceeded {maxBytes / (1024 * 1024)} MB limit]");
        }

        return result.ToString();
    }
}
