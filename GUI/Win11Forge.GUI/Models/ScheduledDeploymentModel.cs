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
using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents a scheduled deployment in the GUI.
/// </summary>
public partial class ScheduledDeploymentModel : ObservableValidator
{
    /// <summary>Unique identifier for the scheduled deployment.</summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Deployment ID is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Deployment ID must be between 1 and 32 characters")]
    private string _id = string.Empty;

    /// <summary>Name of the deployment profile to run.</summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Profile name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Profile name must be between 1 and 256 characters")]
    private string _profileName = string.Empty;

    /// <summary>Scheduled execution time.</summary>
    [ObservableProperty]
    private DateTime _scheduledTime = DateTime.Now.AddHours(1);

    /// <summary>Type of trigger (OneTime, Daily, Weekly, AtStartup, AtLogon).</summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Trigger type is required")]
    private ScheduledTriggerType _triggerType = ScheduledTriggerType.OneTime;

    /// <summary>Current status of the scheduled deployment.</summary>
    [ObservableProperty]
    private ScheduledDeploymentStatus _status = ScheduledDeploymentStatus.Pending;

    /// <summary>User who created the deployment.</summary>
    [ObservableProperty]
    [StringLength(256, ErrorMessage = "Created by must not exceed 256 characters")]
    private string _createdBy = Environment.UserName;

    /// <summary>When the deployment was created.</summary>
    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    /// <summary>Last execution time.</summary>
    [ObservableProperty]
    private DateTime? _lastRunTime;

    /// <summary>Result of last execution.</summary>
    [ObservableProperty]
    [StringLength(256, ErrorMessage = "Last run result must not exceed 256 characters")]
    private string? _lastRunResult;

    /// <summary>Whether to run in parallel mode.</summary>
    [ObservableProperty]
    private bool _runParallel;

    /// <summary>Whether to run in test/dry-run mode.</summary>
    [ObservableProperty]
    private bool _testMode;

    /// <summary>Days of week for weekly trigger.</summary>
    [ObservableProperty]
    private DayOfWeek[] _daysOfWeek = [];

    /// <summary>Whether this deployment is selected in the UI.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Gets a formatted display of the scheduled time.</summary>
    public string ScheduledTimeDisplay => TriggerType switch
    {
        ScheduledTriggerType.AtStartup => Resources.Resources.ScheduledDeployment_AtStartup,
        ScheduledTriggerType.AtLogon => Resources.Resources.ScheduledDeployment_AtLogon,
        ScheduledTriggerType.Daily => $"{Resources.Resources.ScheduledDeployment_Daily} {ScheduledTime:HH:mm}",
        ScheduledTriggerType.Weekly => $"{Resources.Resources.ScheduledDeployment_Weekly} {ScheduledTime:HH:mm}",
        _ => ScheduledTime.ToString("g")
    };

    /// <summary>Gets the status display text.</summary>
    public string StatusDisplay => Status switch
    {
        ScheduledDeploymentStatus.Pending => Resources.Resources.ScheduledDeployment_Status_Pending,
        ScheduledDeploymentStatus.Running => Resources.Resources.ScheduledDeployment_Status_Running,
        ScheduledDeploymentStatus.Completed => Resources.Resources.ScheduledDeployment_Status_Completed,
        ScheduledDeploymentStatus.Failed => Resources.Resources.ScheduledDeployment_Status_Failed,
        ScheduledDeploymentStatus.Cancelled => Resources.Resources.ScheduledDeployment_Status_Cancelled,
        _ => Resources.Resources.ScheduledDeployment_Status_Unknown
    };
}

/// <summary>
/// Types of scheduled triggers.
/// </summary>
public enum ScheduledTriggerType
{
    /// <summary>Run once at the specified time.</summary>
    OneTime,

    /// <summary>Run daily at the specified time.</summary>
    Daily,

    /// <summary>Run weekly on specified days.</summary>
    Weekly,

    /// <summary>Run at system startup.</summary>
    AtStartup,

    /// <summary>Run at user logon.</summary>
    AtLogon
}

/// <summary>
/// Status of a scheduled deployment.
/// </summary>
public enum ScheduledDeploymentStatus
{
    /// <summary>Waiting to run.</summary>
    Pending,

    /// <summary>Currently executing.</summary>
    Running,

    /// <summary>Completed successfully.</summary>
    Completed,

    /// <summary>Failed to execute.</summary>
    Failed,

    /// <summary>Cancelled by user.</summary>
    Cancelled,

    /// <summary>Unknown status.</summary>
    Unknown
}
