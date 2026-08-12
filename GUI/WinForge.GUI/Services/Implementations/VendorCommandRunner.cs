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
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Result of running an external command on behalf of an application operation.
/// </summary>
/// <param name="Success">Whether the command reported success.</param>
/// <param name="ExitCode">The process exit code, or -1 when the command never ran to completion.</param>
/// <param name="Output">Captured standard output, or a diagnostic message on failure.</param>
internal readonly record struct VendorCommandResult(bool Success, int ExitCode, string Output);

/// <summary>
/// Runs the external programs that install, update and detect applications.
/// </summary>
/// <remarks>
/// Every path that launches a process on behalf of a catalog entry lives here so the
/// timeout handling, output capture and kill-on-timeout behaviour exist once rather than
/// being reimplemented per operation. It also concentrates the security controls that
/// apply to catalog-driven execution: a catalog can be imported from an untrusted source,
/// so Command detection is gated by both an executable and an argument allowlist.
/// </remarks>
internal sealed class VendorCommandRunner
{
    private readonly IPowerShellExecutionService _executionService;
    private readonly IRepositoryPathService _pathService;
    private readonly ILoggingService _logger;

    private HashSet<string>? _allowedExecutables;
    private HashSet<string>? _allowedArguments;

    /// <summary>
    /// Initializes a new runner.
    /// </summary>
    /// <param name="executionService">Supplies the configured operation timeouts.</param>
    /// <param name="pathService">Resolves the allowlist configuration location.</param>
    /// <param name="logger">Receives diagnostics for failures that are otherwise swallowed.</param>
    public VendorCommandRunner(
        IPowerShellExecutionService executionService,
        IRepositoryPathService pathService,
        ILoggingService logger)
    {
        _executionService = executionService;
        _pathService = pathService;
        _logger = logger;
    }

    /// <summary>
    /// Runs a vendor package-manager command (winget/chocolatey) under the installation timeout.
    /// </summary>
    /// <param name="command">The executable to run.</param>
    /// <param name="arguments">Arguments passed to the executable.</param>
    /// <param name="operationLabel">Label used only in the timeout message.</param>
    /// <param name="logBuilder">The deployment log being built.</param>
    /// <returns>The command result.</returns>
    public async Task<VendorCommandResult> RunAsync(
        string command,
        string arguments,
        string operationLabel,
        StringBuilder logBuilder)
    {
        try
        {
            using Process process = new Process { StartInfo = CreateStartInfo(command, arguments) };
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_executionService.InstallationTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeoutCts.Token));
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                KillQuietly(process);
                logBuilder.AppendLine(
                    $"[ERROR] {operationLabel} timed out after {_executionService.InstallationTimeoutMs / 60000} minutes");
                return new VendorCommandResult(false, -1, $"{operationLabel} timed out");
            }

            string output = await outputTask;
            string error = await errorTask;

            DeploymentLog.AppendVendorOutputSummary(logBuilder, output, error);

            return new VendorCommandResult(process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command execution failed: {ex.Message}");
            return new VendorCommandResult(false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Runs a Command-detection probe declared by a catalog entry.
    /// </summary>
    /// <remarks>
    /// Two allowlists gate this, because either alone is insufficient. The executable list
    /// permits interpreters (python, node, pwsh, ruby, perl, php) that accept code as an
    /// argument, and screening for shell metacharacters does not stop them:
    /// "pwsh -Command Start-Process calc" contains none. Detection only ever needs to ask a
    /// program for its version, so the arguments are allowlisted too. Both load fail-closed.
    /// </remarks>
    /// <param name="commandLine">The full command line from the catalog.</param>
    /// <param name="logBuilder">The deployment log being built.</param>
    /// <returns>The probe result.</returns>
    public async Task<VendorCommandResult> RunDetectionAsync(string commandLine, StringBuilder logBuilder)
    {
        try
        {
            string[] commandParts = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length == 0)
            {
                logBuilder.AppendLine("Command detection failed: command line is empty");
                return new VendorCommandResult(false, -1, string.Empty);
            }

            _allowedExecutables ??= DetectionExecutableAllowlist.Load(_pathService, _logger);
            string executableName = Path.GetFileName(commandParts[0]);
            if (!_allowedExecutables.Contains(executableName))
            {
                logBuilder.AppendLine(
                    $"Command detection blocked: '{commandParts[0]}' is not in the detection allowlist");
                return new VendorCommandResult(false, -1, string.Empty);
            }

            _allowedArguments ??= DetectionExecutableAllowlist.LoadAllowedArguments(_pathService, _logger);
            string detectionArguments = commandParts.Length > 1 ? commandParts[1] : string.Empty;
            if (!DetectionArgumentGuard.IsAllowed(detectionArguments, _allowedArguments))
            {
                logBuilder.AppendLine(
                    $"Command detection blocked: arguments for '{commandParts[0]}' are not on the detection argument allowlist");
                return new VendorCommandResult(false, -1, string.Empty);
            }

            using Process process = new Process
            {
                StartInfo = CreateStartInfo(commandParts[0], detectionArguments, useUtf8: false)
            };
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeoutCts.Token));
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                KillQuietly(process);
                logBuilder.AppendLine(
                    $"[ERROR] Command detection timed out after {_executionService.DefaultQueryTimeoutMs / 1000} seconds");
                return new VendorCommandResult(false, -1, "Command detection timed out");
            }

            string output = await outputTask;
            string error = await errorTask;

            logBuilder.AppendLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                logBuilder.AppendLine($"[stderr] {error}");
            }

            return new VendorCommandResult(process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command detection failed: {ex.Message}");
            return new VendorCommandResult(false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Runs a winget version query and parses the result.
    /// </summary>
    /// <param name="wingetId">Package id, used only for diagnostics.</param>
    /// <param name="arguments">Winget arguments.</param>
    /// <param name="parseVersion">Parser applied to the cleaned output.</param>
    /// <param name="operationLabel">Label used only in the failure message.</param>
    /// <returns>The parsed version, or an empty string on failure or timeout.</returns>
    public async Task<string> QueryWingetVersionAsync(
        string wingetId,
        string arguments,
        Func<string, string> parseVersion,
        string operationLabel)
    {
        try
        {
            using Process process = new Process { StartInfo = CreateStartInfo("winget", arguments) };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                KillQuietly(process);
                return string.Empty;
            }

            return parseVersion(CleanWingetOutput(output));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to query {operationLabel} for '{wingetId}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Strips winget's progress-spinner redraws from captured output.
    /// </summary>
    /// <remarks>
    /// Winget animates progress by rewriting a line with carriage returns, so a captured
    /// line holds every intermediate frame. Only the final segment carries information;
    /// segments made purely of spinner glyphs are dropped.
    /// </remarks>
    /// <param name="output">Raw winget output.</param>
    /// <returns>The output with spinner frames removed.</returns>
    public static string CleanWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return output;
        }

        List<string> cleanLines = new List<string>();

        foreach (string line in output.Split('\n'))
        {
            string? lastSegment = line.Split('\r')
                .Select(segment => segment.Trim())
                .LastOrDefault(segment => !string.IsNullOrEmpty(segment) &&
                                          !segment.All(c => c is '-' or '\\' or '|' or '/' or ' '));

            if (!string.IsNullOrEmpty(lastSegment))
            {
                cleanLines.Add(lastSegment);
            }
        }

        return string.Join("\n", cleanLines);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string arguments, bool useUtf8 = true)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (useUtf8)
        {
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
        }

        return startInfo;
    }

    /// <summary>
    /// Kills a timed-out process tree. Failure here is not actionable: the caller is
    /// already returning a timeout, and the process may have exited on its own.
    /// </summary>
    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort.
        }
    }
}
