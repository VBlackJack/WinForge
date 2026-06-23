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

using System.IO;
using System.Text.Json;
using WinForge.GUI.Configuration;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services;

/// <summary>
/// Loads the shared Command-detection executable allowlist. This is a security
/// control consumed by every code path that may launch an executable named by an
/// (potentially untrusted) application catalog: the detection probe and the
/// application-management update verification. Centralizing the loader guarantees
/// the two cannot drift apart.
///
/// It fails closed - a missing repository path service, a missing/unreadable/malformed
/// allowlist file all yield an empty set, which denies every Command detection rather
/// than permitting arbitrary executables.
/// </summary>
internal static class DetectionExecutableAllowlist
{
    /// <summary>
    /// Name of the JSON array property holding the Command detection allowlist.
    /// </summary>
    private const string AllowedExecutablesPropertyName = "allowedExecutables";

    /// <summary>
    /// Loads the allowed executable base names from the shared configuration file.
    /// </summary>
    /// <param name="repositoryPathService">Resolves the configuration file location.</param>
    /// <param name="logger">Logger used to report fail-closed conditions.</param>
    /// <returns>
    /// A case-insensitive set of allowed executable base names. Empty when the
    /// allowlist cannot be loaded (fail-closed).
    /// </returns>
    public static HashSet<string> Load(IRepositoryPathService? repositoryPathService, ILoggingService logger)
    {
        HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (repositoryPathService is null)
        {
            logger.LogWarning(
                "Detection allowlist unavailable (no repository path service); Command detection is disabled.");
            return allowed;
        }

        string allowlistPath = repositoryPathService.GetPath(
            WinForgePathNames.ConfigDirectoryName,
            WinForgePathNames.DetectionAllowlistFileName);

        if (!File.Exists(allowlistPath))
        {
            logger.LogError(
                $"Detection allowlist file not found at '{allowlistPath}'; Command detection is disabled.");
            return allowed;
        }

        try
        {
            string json = File.ReadAllText(allowlistPath);
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty(AllowedExecutablesPropertyName, out JsonElement executables) &&
                executables.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in executables.EnumerateArray())
                {
                    string? value = entry.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        allowed.Add(value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to load detection allowlist; Command detection is disabled.", ex);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return allowed;
    }
}
