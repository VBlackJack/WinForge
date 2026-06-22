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
/// Represents the method used to detect if an application is installed.
/// </summary>
public enum DetectionMethod
{
    /// <summary>Detection method not specified.</summary>
    None = 0,

    /// <summary>Detect via Windows Registry key existence.</summary>
    Registry = 1,

    /// <summary>Detect via file existence on disk.</summary>
    File = 2,

    /// <summary>Detect via command execution (e.g., "git --version").</summary>
    Command = 3,

    /// <summary>Detect via Windows Store/AppX package.</summary>
    StoreApp = 4,

    /// <summary>Detect via Windows Optional Feature.</summary>
    WindowsFeature = 5,

    /// <summary>Detect via Windows Capability.</summary>
    WindowsCapability = 6,

    /// <summary>Detect via Winget package list.</summary>
    Winget = 7
}

/// <summary>
/// Extension methods for DetectionMethod enum.
/// </summary>
public static class DetectionMethodExtensions
{
    /// <summary>
    /// Converts a string to DetectionMethod enum.
    /// </summary>
    public static DetectionMethod ToDetectionMethod(this string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return DetectionMethod.None;

        return method.ToLowerInvariant() switch
        {
            "registry" => DetectionMethod.Registry,
            "file" => DetectionMethod.File,
            "command" or "cmd" => DetectionMethod.Command,
            "storeapp" or "store" or "appx" => DetectionMethod.StoreApp,
            "windowsfeature" or "feature" => DetectionMethod.WindowsFeature,
            "windowscapability" or "capability" => DetectionMethod.WindowsCapability,
            "winget" => DetectionMethod.Winget,
            _ => DetectionMethod.None
        };
    }

    /// <summary>
    /// Gets the relative speed of this detection method (lower is faster).
    /// </summary>
    public static int GetRelativeSpeed(this DetectionMethod method)
    {
        return method switch
        {
            DetectionMethod.Registry => 1,      // ~20ms
            DetectionMethod.File => 2,          // ~50-100ms
            DetectionMethod.StoreApp => 3,      // ~100-200ms
            DetectionMethod.Command => 4,       // ~200-500ms
            DetectionMethod.WindowsFeature => 5, // ~300-500ms
            DetectionMethod.WindowsCapability => 5,
            DetectionMethod.Winget => 6,        // ~500-2000ms
            _ => 10
        };
    }
}
