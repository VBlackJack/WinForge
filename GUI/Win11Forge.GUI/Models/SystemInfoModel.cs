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

using System.ComponentModel.DataAnnotations;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Contains system information for display on the Dashboard.
/// </summary>
public class SystemInfoModel
{
    /// <summary>
    /// Computer hostname.
    /// </summary>
    [StringLength(256, ErrorMessage = "Hostname must not exceed 256 characters")]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Current username.
    /// </summary>
    [StringLength(256, ErrorMessage = "Username must not exceed 256 characters")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Windows version (e.g., "Windows 11 Pro").
    /// </summary>
    [StringLength(128, ErrorMessage = "Windows version must not exceed 128 characters")]
    public string WindowsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Windows build number (e.g., "22631.2715").
    /// </summary>
    [StringLength(64, ErrorMessage = "Windows build must not exceed 64 characters")]
    public string WindowsBuild { get; set; } = string.Empty;

    /// <summary>
    /// Whether Winget is available.
    /// </summary>
    public bool WingetAvailable { get; set; }

    /// <summary>
    /// Winget version if available.
    /// </summary>
    [StringLength(64, ErrorMessage = "Winget version must not exceed 64 characters")]
    public string WingetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Chocolatey is available.
    /// </summary>
    public bool ChocolateyAvailable { get; set; }

    /// <summary>
    /// Chocolatey version if available.
    /// </summary>
    [StringLength(64, ErrorMessage = "Chocolatey version must not exceed 64 characters")]
    public string ChocolateyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether running with administrator privileges.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Total physical memory in GB.
    /// </summary>
    [Range(0, 10000, ErrorMessage = "Total memory must be between 0 and 10000 GB")]
    public double TotalMemoryGB { get; set; }

    /// <summary>
    /// Number of logical processors.
    /// </summary>
    [Range(1, 1024, ErrorMessage = "Processor count must be between 1 and 1024")]
    public int ProcessorCount { get; set; }
}
