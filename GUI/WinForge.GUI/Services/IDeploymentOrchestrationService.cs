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

using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

/// <summary>
/// High-level deployment orchestration service.
/// Coordinates profile-based deployments with state management and rollback support.
/// </summary>
public interface IDeploymentOrchestrationService
{
    /// <summary>
    /// Gets whether a deployment is currently in progress.
    /// </summary>
    bool IsDeploymentInProgress { get; }

    /// <summary>
    /// Gets the current deployment state.
    /// </summary>
    DeploymentState CurrentState { get; }

    /// <summary>
    /// Deploys a profile with all its applications.
    /// </summary>
    /// <param name="profileName">Name of the profile to deploy</param>
    /// <param name="options">Deployment options</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment execution result</returns>
    Task<DeploymentExecutionResult> DeployProfileAsync(
        string profileName,
        DeploymentOptions options,
        IProgress<DeploymentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a deployment before execution.
    /// Checks for dependency conflicts, disk space, etc.
    /// </summary>
    /// <param name="profileName">Name of the profile to validate</param>
    /// <returns>Validation result with any issues found</returns>
    Task<DeploymentValidationResult> ValidateDeploymentAsync(string profileName);

    /// <summary>
    /// Gets the deployment history.
    /// </summary>
    /// <param name="limit">Maximum number of entries to return</param>
    /// <returns>List of deployment history entries</returns>
    Task<IReadOnlyList<DeploymentHistoryEntry>> GetDeploymentHistoryAsync(int limit = 50);

    /// <summary>
    /// Creates a rollback point before deployment.
    /// </summary>
    /// <param name="description">Description of the rollback point</param>
    /// <returns>Rollback point identifier</returns>
    Task<string> CreateRollbackPointAsync(string description);

    /// <summary>
    /// Event raised when deployment state changes.
    /// </summary>
    event EventHandler<DeploymentStateChangedEventArgs>? StateChanged;
}

/// <summary>
/// Options for deployment execution.
/// </summary>
public class DeploymentOptions
{
    /// <summary>
    /// If true, simulates deployment without making changes.
    /// </summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// Maximum parallel installation jobs.
    /// </summary>
    public int Parallelism { get; init; } = 1;

    /// <summary>
    /// Whether to force update already installed applications.
    /// </summary>
    public bool ForceUpdate { get; init; }

    /// <summary>
    /// Whether to create a rollback point before deployment.
    /// </summary>
    public bool CreateRollbackPoint { get; init; } = true;

    /// <summary>
    /// Update strategy for applications.
    /// </summary>
    public UpdateStrategy UpdateStrategy { get; init; } = UpdateStrategy.SkipInstalled;
}

/// <summary>
/// Represents the current state of a deployment.
/// </summary>
public enum DeploymentState
{
    /// <summary>No deployment active.</summary>
    Idle,

    /// <summary>Validating deployment prerequisites.</summary>
    Validating,

    /// <summary>Creating rollback point.</summary>
    CreatingRollbackPoint,

    /// <summary>Installing applications.</summary>
    Installing,

    /// <summary>Deployment paused by user.</summary>
    Paused,

    /// <summary>Deployment completed successfully.</summary>
    Completed,

    /// <summary>Deployment failed.</summary>
    Failed,

    /// <summary>Deployment cancelled by user.</summary>
    Cancelled,

    /// <summary>Rolling back changes.</summary>
    RollingBack
}

/// <summary>
/// Progress information for deployment operations.
/// </summary>
public class DeploymentProgress
{
    /// <summary>Name of the current application being processed.</summary>
    public string? CurrentApplicationName { get; init; }

    /// <summary>Current progress index (1-based).</summary>
    public int CurrentIndex { get; init; }

    /// <summary>Total number of applications.</summary>
    public int TotalCount { get; init; }

    /// <summary>Progress percentage (0-100).</summary>
    public double PercentComplete => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;

    /// <summary>Current status message.</summary>
    public string? Message { get; init; }

    /// <summary>Estimated time remaining.</summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Result of deployment validation.
/// </summary>
public class DeploymentValidationResult
{
    /// <summary>Whether the deployment can proceed.</summary>
    public bool IsValid { get; init; }

    /// <summary>List of validation issues.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>Total number of applications to be deployed.</summary>
    public int ApplicationCount { get; init; }

    /// <summary>Estimated disk space required in bytes.</summary>
    public long EstimatedDiskSpaceRequired { get; init; }
}

/// <summary>
/// A validation issue found during deployment validation.
/// </summary>
public class ValidationIssue
{
    /// <summary>Severity of the issue.</summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>Description of the issue.</summary>
    public required string Message { get; init; }

    /// <summary>Application ID related to this issue, if applicable.</summary>
    public string? ApplicationId { get; init; }
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Warning that doesn't block deployment.</summary>
    Warning,

    /// <summary>Error that blocks deployment.</summary>
    Error
}

/// <summary>
/// Event arguments for deployment state changes.
/// </summary>
public class DeploymentStateChangedEventArgs : EventArgs
{
    /// <summary>Previous deployment state.</summary>
    public DeploymentState PreviousState { get; init; }

    /// <summary>New deployment state.</summary>
    public DeploymentState NewState { get; init; }

    /// <summary>Optional message describing the state change.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Complete result of a deployment execution.
/// </summary>
public class DeploymentExecutionResult
{
    /// <summary>Overall result status.</summary>
    public DeploymentResult Result { get; init; }

    /// <summary>Total number of applications processed.</summary>
    public int TotalApplications { get; init; }

    /// <summary>Number of successfully installed applications.</summary>
    public int SuccessfulInstallations { get; init; }

    /// <summary>Number of failed installations.</summary>
    public int FailedInstallations { get; init; }

    /// <summary>Number of skipped applications.</summary>
    public int SkippedApplications { get; init; }

    /// <summary>Total duration of the deployment.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Individual results per application.</summary>
    public IReadOnlyList<ApplicationDeploymentResult> ApplicationResults { get; init; } = Array.Empty<ApplicationDeploymentResult>();

    /// <summary>Error message if deployment failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Rollback point ID if one was created.</summary>
    public string? RollbackPointId { get; init; }
}

/// <summary>
/// Result of deploying a single application.
/// </summary>
public class ApplicationDeploymentResult
{
    /// <summary>Application ID.</summary>
    public required string ApplicationId { get; init; }

    /// <summary>Application name.</summary>
    public required string ApplicationName { get; init; }

    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Whether the application was already installed.</summary>
    public bool WasAlreadyInstalled { get; init; }

    /// <summary>Whether the application was skipped.</summary>
    public bool WasSkipped { get; init; }

    /// <summary>Error message if installation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Version that was installed.</summary>
    public string? InstalledVersion { get; init; }
}
