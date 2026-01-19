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
/// Represents the status of a single prerequisite.
/// </summary>
public class PrerequisiteItem
{
    /// <summary>
    /// Prerequisite name.
    /// </summary>
    [Required(ErrorMessage = "Prerequisite name is required")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 128 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this prerequisite is installed.
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Version string or status message.
    /// </summary>
    [StringLength(64, ErrorMessage = "Version must not exceed 64 characters")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Category of prerequisite (PackageManager, Runtime, etc.)
    /// </summary>
    [StringLength(64, ErrorMessage = "Category must not exceed 64 characters")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this prerequisite provides.
    /// </summary>
    [StringLength(512, ErrorMessage = "Description must not exceed 512 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this prerequisite is required (vs optional).
    /// </summary>
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Represents the status of system prerequisites.
/// </summary>
public class PrerequisitesStatus
{
    /// <summary>
    /// Whether PowerShell 7+ is installed.
    /// </summary>
    public bool PowerShell7Installed { get; set; }

    /// <summary>
    /// PowerShell version string.
    /// </summary>
    public string PowerShellVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Chocolatey is installed.
    /// </summary>
    public bool ChocolateyInstalled { get; set; }

    /// <summary>
    /// Chocolatey version string.
    /// </summary>
    public string ChocolateyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Winget is installed.
    /// </summary>
    public bool WingetInstalled { get; set; }

    /// <summary>
    /// Winget version string.
    /// </summary>
    public string WingetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether .NET Core runtimes are installed.
    /// </summary>
    public bool DotNetInstalled { get; set; }

    /// <summary>
    /// .NET Core version info.
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether .NET Framework 4.8.1 is installed.
    /// </summary>
    public bool DotNetFrameworkInstalled { get; set; }

    /// <summary>
    /// .NET Framework version string.
    /// </summary>
    public string DotNetFrameworkVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Visual C++ Redistributables are installed.
    /// </summary>
    public bool VCRedistInstalled { get; set; }

    /// <summary>
    /// VC++ Redistributable version string.
    /// </summary>
    public string VCRedistVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Java Runtime is installed.
    /// </summary>
    public bool JavaInstalled { get; set; }

    /// <summary>
    /// Java version string.
    /// </summary>
    public string JavaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether all required prerequisites are met (package managers + PS7).
    /// </summary>
    public bool AllPrerequisitesMet => PowerShell7Installed && ChocolateyInstalled && WingetInstalled;

    /// <summary>
    /// Whether all prerequisites including optional ones are met.
    /// </summary>
    public bool AllInstalled => PowerShell7Installed && ChocolateyInstalled && WingetInstalled &&
                                 DotNetInstalled && DotNetFrameworkInstalled && VCRedistInstalled && JavaInstalled;

    /// <summary>
    /// Number of missing required prerequisites.
    /// </summary>
    public int MissingCount =>
        (PowerShell7Installed ? 0 : 1) +
        (ChocolateyInstalled ? 0 : 1) +
        (WingetInstalled ? 0 : 1);

    /// <summary>
    /// Total number of missing prerequisites (including optional).
    /// </summary>
    public int TotalMissingCount =>
        (PowerShell7Installed ? 0 : 1) +
        (ChocolateyInstalled ? 0 : 1) +
        (WingetInstalled ? 0 : 1) +
        (DotNetInstalled ? 0 : 1) +
        (DotNetFrameworkInstalled ? 0 : 1) +
        (VCRedistInstalled ? 0 : 1) +
        (JavaInstalled ? 0 : 1);

    /// <summary>
    /// Total number of prerequisites.
    /// </summary>
    public int TotalCount => 7;

    /// <summary>
    /// Number of installed prerequisites.
    /// </summary>
    public int InstalledCount => TotalCount - TotalMissingCount;
}
