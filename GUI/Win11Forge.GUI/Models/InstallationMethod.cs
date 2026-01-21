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

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents the installation method used to install an application.
/// </summary>
public enum InstallationMethod
{
    /// <summary>Installation method not specified or unknown.</summary>
    Unknown = 0,

    /// <summary>Installed via Windows Package Manager (winget).</summary>
    Winget = 1,

    /// <summary>Installed via Chocolatey package manager.</summary>
    Chocolatey = 2,

    /// <summary>Installed via Microsoft Store.</summary>
    Store = 3,

    /// <summary>Installed via direct download URL.</summary>
    Direct = 4,

    /// <summary>Requires manual installation by the user.</summary>
    Manual = 5,

    /// <summary>Installed as a Windows Feature.</summary>
    WindowsFeature = 6,

    /// <summary>Installed as a Windows Capability.</summary>
    WindowsCapability = 7
}

/// <summary>
/// Extension methods for InstallationMethod enum.
/// </summary>
public static class InstallationMethodExtensions
{
    /// <summary>
    /// Converts a string to InstallationMethod enum.
    /// </summary>
    public static InstallationMethod ToInstallationMethod(this string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return InstallationMethod.Unknown;

        return method.ToLowerInvariant() switch
        {
            "winget" => InstallationMethod.Winget,
            "chocolatey" or "choco" => InstallationMethod.Chocolatey,
            "store" or "msstore" => InstallationMethod.Store,
            "direct" or "directurl" or "url" => InstallationMethod.Direct,
            "manual" => InstallationMethod.Manual,
            "windowsfeature" or "feature" => InstallationMethod.WindowsFeature,
            "windowscapability" or "capability" => InstallationMethod.WindowsCapability,
            _ => InstallationMethod.Unknown
        };
    }

    /// <summary>
    /// Converts InstallationMethod enum to display string.
    /// </summary>
    public static string ToDisplayString(this InstallationMethod method)
    {
        return method switch
        {
            InstallationMethod.Winget => "Winget",
            InstallationMethod.Chocolatey => "Chocolatey",
            InstallationMethod.Store => "Microsoft Store",
            InstallationMethod.Direct => "Direct Download",
            InstallationMethod.Manual => "Manual",
            InstallationMethod.WindowsFeature => "Windows Feature",
            InstallationMethod.WindowsCapability => "Windows Capability",
            _ => "Unknown"
        };
    }
}
