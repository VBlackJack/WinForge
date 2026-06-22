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

namespace WinForge.GUI.Models;

/// <summary>
/// Represents information about an installed package from various detection sources.
/// </summary>
public class InstalledPackageInfo
{
    /// <summary>Package identifier (e.g., WinGet ID, registry key name).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name of the package.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Currently installed version.</summary>
    public string InstalledVersion { get; init; } = string.Empty;

    /// <summary>Available version for update (null if no update available).</summary>
    public string? AvailableVersion { get; init; }

    /// <summary>Publisher/vendor of the package.</summary>
    public string? Publisher { get; init; }

    /// <summary>Source of detection (Registry, WinGet, AppX, Command).</summary>
    public DetectionSource Source { get; init; }

    /// <summary>Whether an update is available.</summary>
    public bool HasUpdate => !string.IsNullOrEmpty(AvailableVersion) &&
                             AvailableVersion != InstalledVersion;

    /// <summary>Installation location if known.</summary>
    public string? InstallLocation { get; init; }

    /// <summary>Uninstall command if available.</summary>
    public string? UninstallString { get; init; }
}

/// <summary>
/// Source from which the package was detected.
/// </summary>
public enum DetectionSource
{
    /// <summary>Detected via Windows Registry uninstall keys.</summary>
    Registry,

    /// <summary>Detected via WinGet package manager.</summary>
    WinGet,

    /// <summary>Detected via AppX/MSIX packages.</summary>
    AppX,

    /// <summary>Detected via command-line version check.</summary>
    Command,

    /// <summary>Detected via Chocolatey.</summary>
    Chocolatey,

    /// <summary>Detected via file existence check.</summary>
    File,

    /// <summary>Detected via Windows optional feature.</summary>
    WindowsFeature,

    /// <summary>Detected via JSON application database.</summary>
    JsonDatabase,

    /// <summary>Detection source unknown.</summary>
    Unknown
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    /// <summary>Package identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name of the package.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Currently installed version.</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>New version available.</summary>
    public string NewVersion { get; init; } = string.Empty;

    /// <summary>Source of the update.</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// Result of a batch application status check.
/// </summary>
public class BatchDetectionResult
{
    /// <summary>All detected packages indexed by ID (case-insensitive).</summary>
    public Dictionary<string, InstalledPackageInfo> Packages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Time taken for detection.</summary>
    public TimeSpan DetectionTime { get; init; }

    /// <summary>Whether results came from cache.</summary>
    public bool FromCache { get; init; }

    /// <summary>Timestamp when this result was generated.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Number of packages detected.</summary>
    public int Count => Packages.Count;

    /// <summary>Checks if a package is installed.</summary>
    public bool IsInstalled(string appId) => Packages.ContainsKey(appId);

    /// <summary>Gets package info if installed.</summary>
    public InstalledPackageInfo? GetPackage(string appId) =>
        Packages.TryGetValue(appId, out InstalledPackageInfo? info) ? info : null;
}
