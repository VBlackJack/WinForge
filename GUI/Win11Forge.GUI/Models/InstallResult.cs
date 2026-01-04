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

namespace Win11Forge.GUI.Models;

/// <summary>
/// Result of an application installation attempt.
/// </summary>
public class InstallResult
{
    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Result message (success or error description).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Full installation log output.</summary>
    public string Logs { get; init; } = string.Empty;

    /// <summary>Installation method used (Winget, Chocolatey, etc.).</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Whether the application was already installed.</summary>
    public bool AlreadyInstalled { get; init; }

    /// <summary>Whether this was a dry run (simulation).</summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// Creates a successful installation result.
    /// </summary>
    public static InstallResult Successful(string message, string logs, string method = "", bool alreadyInstalled = false)
    {
        return new InstallResult
        {
            Success = true,
            Message = message,
            Logs = logs,
            Method = method,
            AlreadyInstalled = alreadyInstalled
        };
    }

    /// <summary>
    /// Creates a failed installation result.
    /// </summary>
    public static InstallResult Failed(string message, string logs)
    {
        return new InstallResult
        {
            Success = false,
            Message = message,
            Logs = logs
        };
    }

    /// <summary>
    /// Creates a dry run result.
    /// </summary>
    public static InstallResult DryRun(string appName)
    {
        return new InstallResult
        {
            Success = true,
            Message = $"[DRY RUN] Would install: {appName}",
            Logs = $"Simulation mode - no changes made\nApplication: {appName}",
            IsDryRun = true
        };
    }
}
