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
    /// Whether Winget is installed.
    /// </summary>
    public bool WingetInstalled { get; set; }

    /// <summary>
    /// Whether all prerequisites are met.
    /// </summary>
    public bool AllPrerequisitesMet => PowerShell7Installed && ChocolateyInstalled && WingetInstalled;

    /// <summary>
    /// Number of missing prerequisites.
    /// </summary>
    public int MissingCount =>
        (PowerShell7Installed ? 0 : 1) +
        (ChocolateyInstalled ? 0 : 1) +
        (WingetInstalled ? 0 : 1);
}
