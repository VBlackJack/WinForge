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
/// Contains system information for display on the Dashboard.
/// </summary>
public class SystemInfoModel
{
    /// <summary>
    /// Computer hostname.
    /// </summary>
    [StringLength(256, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_Hostname_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Current username.
    /// </summary>
    [StringLength(256, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_Username_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Windows version (e.g., "Windows 11 Pro").
    /// </summary>
    [StringLength(128, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_WindowsVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string WindowsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Windows build number (e.g., "22631.2715").
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_WindowsBuild_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string WindowsBuild { get; set; } = string.Empty;

    /// <summary>
    /// Whether Winget is available.
    /// </summary>
    public bool WingetAvailable { get; set; }

    /// <summary>
    /// Winget version if available.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_WingetVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string WingetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Chocolatey is available.
    /// </summary>
    public bool ChocolateyAvailable { get; set; }

    /// <summary>
    /// Chocolatey version if available.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_ChocolateyVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string ChocolateyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether running with administrator privileges.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Total physical memory in GB.
    /// </summary>
    [Range(0, 10000, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_TotalMemory_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public double TotalMemoryGB { get; set; }

    /// <summary>
    /// Number of logical processors.
    /// </summary>
    [Range(1, 1024, ErrorMessageResourceName = nameof(Resources.Resources.Validation_SystemInfo_ProcessorCount_Range), ErrorMessageResourceType = typeof(Resources.Resources))]
    public int ProcessorCount { get; set; }
}
