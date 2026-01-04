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
/// Contains system information for display on the Dashboard.
/// </summary>
public class SystemInfoModel
{
    /// <summary>
    /// Computer hostname.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Current username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Windows version (e.g., "Windows 11 Pro").
    /// </summary>
    public string WindowsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Windows build number (e.g., "22631.2715").
    /// </summary>
    public string WindowsBuild { get; set; } = string.Empty;

    /// <summary>
    /// Whether Winget is available.
    /// </summary>
    public bool WingetAvailable { get; set; }

    /// <summary>
    /// Winget version if available.
    /// </summary>
    public string WingetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Chocolatey is available.
    /// </summary>
    public bool ChocolateyAvailable { get; set; }

    /// <summary>
    /// Chocolatey version if available.
    /// </summary>
    public string ChocolateyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether running with administrator privileges.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Total physical memory in GB.
    /// </summary>
    public double TotalMemoryGB { get; set; }

    /// <summary>
    /// Number of logical processors.
    /// </summary>
    public int ProcessorCount { get; set; }
}
