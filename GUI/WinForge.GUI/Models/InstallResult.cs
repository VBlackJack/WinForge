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

using System.ComponentModel.DataAnnotations;

namespace WinForge.GUI.Models;

/// <summary>
/// Result of an application installation attempt.
/// Implements IValidatableObject for cross-property validation.
/// </summary>
public class InstallResult : IValidatableObject
{
    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Result message (success or error description).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Full installation log output.</summary>
    public string Logs { get; init; } = string.Empty;

    /// <summary>Installation method used (strongly typed).</summary>
    public InstallationMethod MethodType { get; init; } = InstallationMethod.Unknown;

    /// <summary>Installation method used (string for backwards compatibility).</summary>
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
            MethodType = method.ToInstallationMethod(),
            AlreadyInstalled = alreadyInstalled
        };
    }

    /// <summary>
    /// Creates a successful installation result with strongly typed method.
    /// </summary>
    public static InstallResult Successful(string message, string logs, InstallationMethod methodType, bool alreadyInstalled = false)
    {
        return new InstallResult
        {
            Success = true,
            Message = message,
            Logs = logs,
            Method = methodType.ToDisplayString(),
            MethodType = methodType,
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
            Message = $"{Resources.Resources.Common_DryRun} Would install: {appName}",
            Logs = $"Simulation mode - no changes made\nApplication: {appName}",
            IsDryRun = true
        };
    }

    /// <summary>
    /// Creates a manual install required result.
    /// </summary>
    public static InstallResult ManualInstallRequired(string appName, string officialUrl)
    {
        return new InstallResult
        {
            Success = false,
            Message = $"Manual installation required: {appName}",
            Logs = $"This application requires manual installation.\nPlease download from: {officialUrl}",
            IsManualInstallRequired = true
        };
    }

    /// <summary>Whether manual installation is required for this application.</summary>
    public bool IsManualInstallRequired { get; init; }

    /// <summary>
    /// Validates the semantic consistency of the result.
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Collection of validation results</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Success should be false if manual install is required
        if (IsManualInstallRequired && Success)
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_ManualInstallCannotBeSuccess,
                new[] { nameof(Success), nameof(IsManualInstallRequired) });
        }

        // Dry run results should always be successful
        if (IsDryRun && !Success)
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_DryRunShouldSucceed,
                new[] { nameof(Success), nameof(IsDryRun) });
        }

        // Message should not be empty for failed installations
        if (!Success && !IsDryRun && string.IsNullOrWhiteSpace(Message))
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_FailedInstallNeedsMessage,
                new[] { nameof(Message) });
        }
    }
}

/// <summary>
/// Result of checking for application updates.
/// </summary>
public class UpdateCheckResult
{
    /// <summary>Whether an update is available.</summary>
    public bool HasUpdate { get; init; }

    /// <summary>Current installed version.</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>Available version for update.</summary>
    public string AvailableVersion { get; init; } = string.Empty;

    /// <summary>Error message if check failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a result indicating an update is available.
    /// </summary>
    public static UpdateCheckResult UpdateAvailable(string currentVersion, string availableVersion)
    {
        return new UpdateCheckResult
        {
            HasUpdate = true,
            CurrentVersion = currentVersion,
            AvailableVersion = availableVersion
        };
    }

    /// <summary>
    /// Creates a result indicating no update is available.
    /// </summary>
    public static UpdateCheckResult UpToDate(string currentVersion = "")
    {
        return new UpdateCheckResult
        {
            HasUpdate = false,
            CurrentVersion = currentVersion
        };
    }

    /// <summary>
    /// Creates a result indicating the check failed.
    /// </summary>
    public static UpdateCheckResult Failed(string errorMessage)
    {
        return new UpdateCheckResult
        {
            HasUpdate = false,
            ErrorMessage = errorMessage
        };
    }
}
