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

namespace WinForge.GUI.Services.PowerShell;

/// <summary>
/// Service for executing PowerShell scripts and commands.
/// </summary>
public interface IPowerShellExecutionService
{
    /// <summary>
    /// Default timeout for short operations (queries, status checks) in milliseconds.
    /// </summary>
    int DefaultQueryTimeoutMs { get; }

    /// <summary>
    /// Timeout for installation operations in milliseconds.
    /// </summary>
    int InstallationTimeoutMs { get; }

    /// <summary>
    /// Gets the PowerShell executable path (pwsh.exe or powershell.exe).
    /// </summary>
    /// <returns>The path to the PowerShell executable.</returns>
    string GetPowerShellPath();

    /// <summary>
    /// Executes a PowerShell script using external process.
    /// </summary>
    /// <param name="script">The PowerShell script to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The script output.</returns>
    Task<string> ExecutePowerShellScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PowerShell script with real-time output streaming.
    /// </summary>
    /// <param name="script">The PowerShell script to execute.</param>
    /// <param name="outputCallback">Callback for real-time output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status and any error message.</returns>
    Task<(bool Success, string ErrorMessage)> ExecutePowerShellWithStreamingAsync(
        string script,
        Action<string>? outputCallback,
        CancellationToken cancellationToken = default);

}
