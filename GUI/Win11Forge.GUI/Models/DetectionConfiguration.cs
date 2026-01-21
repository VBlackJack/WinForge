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

using System.Text.Json.Serialization;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents the detection configuration for an application from applications.json.
/// Supports multiple detection methods: Registry, Command, File, WindowsFeature.
/// </summary>
public class DetectionConfiguration
{
    /// <summary>
    /// The detection method type.
    /// </summary>
    [JsonPropertyName("Method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// For Registry/File: The path to check.
    /// For Registry: e.g., "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    /// For File: e.g., "%ProgramFiles%\App\app.exe"
    /// </summary>
    [JsonPropertyName("Path")]
    public string? Path { get; set; }

    /// <summary>
    /// For Registry: The registry value name containing the version.
    /// </summary>
    [JsonPropertyName("VersionKey")]
    public string? VersionKey { get; set; }

    /// <summary>
    /// For Registry: Specific registry value to check (for non-version checks).
    /// </summary>
    [JsonPropertyName("RegistryValue")]
    public string? RegistryValue { get; set; }

    /// <summary>
    /// For Registry: Expected value for boolean presence check.
    /// </summary>
    [JsonPropertyName("ExpectedValue")]
    public string? ExpectedValue { get; set; }

    /// <summary>
    /// For Command: The command to execute (e.g., "dotnet --list-runtimes").
    /// </summary>
    [JsonPropertyName("Command")]
    public string? Command { get; set; }

    /// <summary>
    /// For Command: Optional filter string that must be present in output.
    /// Used to check specific runtimes (e.g., "Microsoft.WindowsDesktop.App 8").
    /// </summary>
    [JsonPropertyName("Arguments")]
    public string? Arguments { get; set; }

    /// <summary>
    /// Regex pattern to extract version from command output or registry value.
    /// </summary>
    [JsonPropertyName("VersionRegex")]
    public string? VersionRegex { get; set; }

    /// <summary>
    /// For WindowsFeature: The feature name to check.
    /// </summary>
    [JsonPropertyName("FeatureName")]
    public string? FeatureName { get; set; }
}

/// <summary>
/// Root structure of applications.json database.
/// </summary>
public class ApplicationsDatabase
{
    [JsonPropertyName("DatabaseVersion")]
    public string? DatabaseVersion { get; set; }

    [JsonPropertyName("TotalApplications")]
    public int TotalApplications { get; set; }

    [JsonPropertyName("Applications")]
    public Dictionary<string, ApplicationJsonEntry>? Applications { get; set; }
}

/// <summary>
/// Represents a minimal application entry from applications.json for detection purposes.
/// </summary>
public class ApplicationJsonEntry
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Category")]
    public string? Category { get; set; }

    [JsonPropertyName("Sources")]
    public ApplicationSources? Sources { get; set; }

    [JsonPropertyName("Detection")]
    public DetectionConfiguration? Detection { get; set; }

    [JsonPropertyName("Tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Gets the primary identifier for this application (WinGet ID preferred).
    /// </summary>
    public string GetPrimaryId(string jsonKey)
    {
        // Prefer WinGet ID as it's what the GUI uses for ApplicationModel.AppId
        if (!string.IsNullOrEmpty(Sources?.Winget))
            return Sources.Winget;
        if (!string.IsNullOrEmpty(Sources?.Chocolatey))
            return Sources.Chocolatey;
        if (!string.IsNullOrEmpty(Sources?.Store))
            return Sources.Store;
        return jsonKey;
    }
}

/// <summary>
/// Installation sources for an application.
/// </summary>
public class ApplicationSources
{
    [JsonPropertyName("Winget")]
    public string? Winget { get; set; }

    [JsonPropertyName("Chocolatey")]
    public string? Chocolatey { get; set; }

    [JsonPropertyName("Store")]
    public string? Store { get; set; }

    [JsonPropertyName("DirectUrl")]
    public string? DirectUrl { get; set; }
}

/// <summary>
/// Detection method string constants matching JSON values.
/// </summary>
public static class DetectionMethodStrings
{
    public const string Registry = "Registry";
    public const string Command = "Command";
    public const string File = "File";
    public const string WindowsFeature = "WindowsFeature";
}
