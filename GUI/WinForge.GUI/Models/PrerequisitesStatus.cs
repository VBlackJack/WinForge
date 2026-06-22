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
using System.Linq;

namespace WinForge.GUI.Models;

/// <summary>
/// Represents the status of a single prerequisite.
/// </summary>
public class PrerequisiteItem
{
    /// <summary>
    /// Prerequisite name.
    /// </summary>
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_Name_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(128, MinimumLength = 1, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_Name_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this prerequisite is installed.
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Version string or status message.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_Version_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Category of prerequisite (PackageManager, Runtime, etc.)
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_Category_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this prerequisite provides.
    /// </summary>
    [StringLength(512, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_Description_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
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
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_PSVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^(\d+\.\d+(\.\d+)?(\.\d+)?)?$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_PSVersion_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string PowerShellVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Chocolatey is installed.
    /// </summary>
    public bool ChocolateyInstalled { get; set; }

    /// <summary>
    /// Chocolatey version string.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_ChocoVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^(\d+\.\d+(\.\d+)?)?$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_ChocoVersion_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string ChocolateyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Winget is installed.
    /// </summary>
    public bool WingetInstalled { get; set; }

    /// <summary>
    /// Winget version string.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_WingetVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^(v?\d+\.\d+(\.\d+)?)?$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_WingetVersion_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string WingetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether .NET Core runtimes are installed.
    /// </summary>
    public bool DotNetInstalled { get; set; }

    /// <summary>
    /// .NET Core version info.
    /// </summary>
    [StringLength(128, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_DotNetVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether .NET Framework 4.8.1 is installed.
    /// </summary>
    public bool DotNetFrameworkInstalled { get; set; }

    /// <summary>
    /// .NET Framework version string.
    /// </summary>
    [StringLength(64, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_DotNetFrameworkVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^(\d+\.\d+(\.\d+)?(\.\d+)?)?$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_DotNetFrameworkVersion_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string DotNetFrameworkVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Visual C++ Redistributables are installed.
    /// </summary>
    public bool VCRedistInstalled { get; set; }

    /// <summary>
    /// VC++ Redistributable version string.
    /// </summary>
    [StringLength(128, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_VCRedistVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string VCRedistVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether Java Runtime is installed.
    /// </summary>
    public bool JavaInstalled { get; set; }

    /// <summary>
    /// Java version string.
    /// </summary>
    [StringLength(128, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Prereq_JavaVersion_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
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
    /// Array of required prerequisite installation statuses.
    /// </summary>
    private bool[] RequiredPrerequisiteStatuses => new[]
    {
        PowerShell7Installed,
        ChocolateyInstalled,
        WingetInstalled
    };

    /// <summary>
    /// Number of missing required prerequisites.
    /// </summary>
    public int MissingCount => RequiredPrerequisiteStatuses.Count(s => !s);

    /// <summary>
    /// Total number of required prerequisites.
    /// </summary>
    public int RequiredCount => RequiredPrerequisiteStatuses.Length;

    /// <summary>
    /// Array of all prerequisite installation statuses for dynamic counting.
    /// </summary>
    private bool[] AllPrerequisiteStatuses => new[]
    {
        PowerShell7Installed,
        ChocolateyInstalled,
        WingetInstalled,
        DotNetInstalled,
        DotNetFrameworkInstalled,
        VCRedistInstalled,
        JavaInstalled
    };

    /// <summary>
    /// Total number of missing prerequisites (including optional).
    /// </summary>
    public int TotalMissingCount => AllPrerequisiteStatuses.Count(s => !s);

    /// <summary>
    /// Total number of prerequisites (calculated dynamically).
    /// </summary>
    public int TotalCount => AllPrerequisiteStatuses.Length;

    /// <summary>
    /// Number of installed prerequisites.
    /// </summary>
    public int InstalledCount => AllPrerequisiteStatuses.Count(s => s);
}
