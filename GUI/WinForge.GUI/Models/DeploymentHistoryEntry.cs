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

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WinForge.GUI.Models;

/// <summary>
/// Result status of a deployment.
/// </summary>
public enum DeploymentResult
{
    Success,
    PartialSuccess,
    Failed,
    Cancelled
}

/// <summary>
/// Represents a single deployment history entry.
/// </summary>
public class DeploymentHistoryEntry : IValidatableObject
{
    /// <summary>
    /// Seconds per day constant.
    /// </summary>
    private const int SecondsPerDay = 86400;

    /// <summary>
    /// Maximum deployment duration in days.
    /// </summary>
    private const int MaxDeploymentDurationDays = 7;

    /// <summary>
    /// Maximum deployment duration in seconds.
    /// </summary>
    public const int MaxDeploymentDurationSeconds = SecondsPerDay * MaxDeploymentDurationDays;

    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    [Required]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When the deployment was executed.
    /// </summary>
    [Required]
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>
    /// Name of the profile that was deployed.
    /// </summary>
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_ProfileName_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(128, MinimumLength = 1, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_ProfileName_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Overall result of the deployment.
    /// </summary>
    [Required]
    [EnumDataType(typeof(DeploymentResult))]
    public DeploymentResult Result { get; set; }

    /// <summary>
    /// Total number of applications attempted.
    /// </summary>
    [Range(0, 10000, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_TotalApps_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public int TotalApps { get; set; }

    /// <summary>
    /// Number of successfully installed applications.
    /// </summary>
    [Range(0, 10000, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_SuccessfulApps_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public int SuccessfulApps { get; set; }

    /// <summary>
    /// Number of failed installations.
    /// </summary>
    [Range(0, 10000, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_FailedApps_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public int FailedApps { get; set; }

    /// <summary>
    /// Number of skipped installations.
    /// </summary>
    [Range(0, 10000, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_SkippedApps_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public int SkippedApps { get; set; }

    /// <summary>
    /// Duration of the deployment in seconds.
    /// </summary>
    [Range(0, MaxDeploymentDurationSeconds, ErrorMessageResourceName = nameof(Resources.Resources.Validation_History_Duration_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Whether this was a dry run (simulation).
    /// </summary>
    public bool IsDryRun { get; set; }

    /// <summary>
    /// Gets a formatted duration string.
    /// </summary>
    public string FormattedDuration
    {
        get
        {
            TimeSpan ts = TimeSpan.FromSeconds(DurationSeconds);
            if (ts.TotalMinutes < 1)
                return $"{ts.Seconds}s";
            if (ts.TotalHours < 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
    }

    /// <summary>
    /// Gets a formatted date string.
    /// </summary>
    public string FormattedDate => Date.ToString("g");

    /// <summary>
    /// Gets a summary string for display.
    /// </summary>
    public string Summary => $"{SuccessfulApps}/{TotalApps} apps installed";

    /// <summary>
    /// Validates the model with complex business rules.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Sum of apps should equal total
        int calculatedTotal = SuccessfulApps + FailedApps + SkippedApps;
        if (calculatedTotal > TotalApps)
        {
            yield return new ValidationResult(
                "Sum of successful, failed, and skipped apps cannot exceed total apps",
                new[] { nameof(TotalApps), nameof(SuccessfulApps), nameof(FailedApps), nameof(SkippedApps) });
        }

        // Date should not be in the future
        if (Date > DateTime.Now.AddMinutes(1))
        {
            yield return new ValidationResult(
                "Deployment date cannot be in the future",
                new[] { nameof(Date) });
        }

        // Validate result consistency
        if (Result == DeploymentResult.Success && FailedApps > 0)
        {
            yield return new ValidationResult(
                "Success result cannot have failed apps",
                new[] { nameof(Result), nameof(FailedApps) });
        }

        if (Result == DeploymentResult.Failed && SuccessfulApps == TotalApps && TotalApps > 0)
        {
            yield return new ValidationResult(
                "Failed result cannot have all apps successful",
                new[] { nameof(Result), nameof(SuccessfulApps) });
        }
    }
}
