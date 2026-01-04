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
public class DeploymentHistoryEntry
{
    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When the deployment was executed.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>
    /// Name of the profile that was deployed.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Overall result of the deployment.
    /// </summary>
    public DeploymentResult Result { get; set; }

    /// <summary>
    /// Total number of applications attempted.
    /// </summary>
    public int TotalApps { get; set; }

    /// <summary>
    /// Number of successfully installed applications.
    /// </summary>
    public int SuccessfulApps { get; set; }

    /// <summary>
    /// Number of failed installations.
    /// </summary>
    public int FailedApps { get; set; }

    /// <summary>
    /// Number of skipped installations.
    /// </summary>
    public int SkippedApps { get; set; }

    /// <summary>
    /// Duration of the deployment in seconds.
    /// </summary>
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
            var ts = TimeSpan.FromSeconds(DurationSeconds);
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
}
