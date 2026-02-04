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
using System.Text.RegularExpressions;

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Service for executing PowerShell scripts and commands.
/// </summary>
public class PowerShellExecutionService : IPowerShellExecutionService
{
    private readonly IRepositoryPathService _pathService;
    private static string? _powerShellPath;

    /// <summary>
    /// Maximum allowed output size in bytes (100 MB) to prevent DoS via memory exhaustion.
    /// </summary>
    private const int MaxOutputSizeBytes = 100 * 1024 * 1024;

    /// <inheritdoc/>
    public int DefaultQueryTimeoutMs => 300000; // 5 minutes

    /// <inheritdoc/>
    public int InstallationTimeoutMs => 1800000; // 30 minutes

    /// <summary>
    /// Initializes a new instance of the PowerShellExecutionService.
    /// </summary>
    /// <param name="pathService">The repository path service.</param>
    public PowerShellExecutionService(IRepositoryPathService pathService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    /// <inheritdoc/>
    public string GetPowerShellPath()
    {
        if (_powerShellPath != null) return _powerShellPath;

        // Try PowerShell 7+ in multiple locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
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
                catch (Exception ex)
                {
                    // Directory enumeration failed, try direct path
                    System.Diagnostics.Debug.WriteLine($"[PowerShellExecutionService] Directory enumeration failed: {ex.Message}");
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
        catch (Exception ex)
        {
            // PATH search failed
            System.Diagnostics.Debug.WriteLine($"[PowerShellExecutionService] PATH search failed: {ex.Message}");
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

        // Final fallback with validation warning
        // Try 'powershell' first (more commonly available), then 'pwsh'
        foreach (var candidate in new[] { "powershell", "pwsh" })
        {
            try
            {
                var foundPath = FindExecutableInPath($"{candidate}.exe");
                if (!string.IsNullOrEmpty(foundPath))
                {
                    _powerShellPath = foundPath;
                    System.Diagnostics.Debug.WriteLine($"[PowerShellExecutionService] Using fallback PowerShell: {_powerShellPath}");
                    return _powerShellPath;
                }
            }
            catch (Exception ex)
            {
                // Continue to next candidate
                System.Diagnostics.Debug.WriteLine($"[PowerShellExecutionService] Candidate {candidate} failed: {ex.Message}");
            }
        }

        // Last resort - may fail at runtime if not in PATH
        System.Diagnostics.Debug.WriteLine("[PowerShellExecutionService] WARNING: No PowerShell installation found. Using 'pwsh' and hoping it's in PATH.");
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
            catch (Exception ex)
            {
                // Invalid path, skip - this is expected for some PATH entries
                System.Diagnostics.Debug.WriteLine($"[PowerShellExecutionService] Invalid PATH entry skipped: {ex.Message}");
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

        var psPath = GetPowerShellPath();
        var repoRoot = _pathService.GetSafeRepositoryRoot();

        // Escape script for command line
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();

            // Read stdout and stderr concurrently with size limits to prevent DoS
            var outputTask = ReadStreamWithLimitAsync(process.StandardOutput, MaxOutputSizeBytes);
            var errorTask = ReadStreamWithLimitAsync(process.StandardError, MaxOutputSizeBytes);

            // Wait for both streams AND the process to complete with timeout
            using var timeoutCts = new CancellationTokenSource(DefaultQueryTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(linkedCts.Token));
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

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
                Debug.WriteLine($"Process cleanup failed: {ex.Message}");
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

        var psPath = GetPowerShellPath();
        var repoRoot = _pathService.GetSafeRepositoryRoot();

        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        var success = false;
        var errorMessage = string.Empty;

        try
        {
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

            // Handle stderr - filter out CLIXML serialization noise
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    var data = e.Data.Trim();

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
                        var cleanLine = ExtractReadableMessage(data);
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
            using var prereqTimeoutCts = new CancellationTokenSource(InstallationTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(prereqTimeoutCts.Token, cancellationToken);
            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }

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
                Debug.WriteLine($"Process cleanup failed: {ex.Message}");
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
            var messages = new List<string>();

            // Extract all ToString content (these contain the actual messages)
            var toStringPattern = new Regex(
                @"<ToString>([^<]*)</ToString>",
                RegexOptions.Compiled);

            var matches = toStringPattern.Matches(line);
            foreach (Match match in matches)
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

    /// <summary>
    /// Reads from a stream with a maximum size limit to prevent DoS via memory exhaustion.
    /// </summary>
    /// <param name="reader">The stream reader to read from.</param>
    /// <param name="maxBytes">Maximum bytes to read before truncating.</param>
    /// <returns>The content read from the stream, potentially truncated.</returns>
    private static async Task<string> ReadStreamWithLimitAsync(StreamReader reader, int maxBytes)
    {
        var buffer = new char[8192];
        var result = new System.Text.StringBuilder();
        var totalBytesRead = 0;
        var truncated = false;

        while (!reader.EndOfStream)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (charsRead == 0) break;

            // Estimate byte count (UTF-16 chars can be 2-4 bytes in UTF-8)
            var estimatedBytes = charsRead * 2;

            if (totalBytesRead + estimatedBytes > maxBytes)
            {
                // Calculate how many chars we can still accept
                var remainingBytes = maxBytes - totalBytesRead;
                var charsToTake = Math.Max(0, remainingBytes / 2);

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
