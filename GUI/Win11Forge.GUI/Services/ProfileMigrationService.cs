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

using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Migrates packaged and legacy install profiles into writable user data.
/// </summary>
public interface IProfileMigrationService
{
    /// <summary>
    /// Ensures profile defaults and legacy install profiles are migrated once.
    /// </summary>
    /// <returns>Migration result details.</returns>
    ProfileMigrationResult EnsureProfilesMigrated();
}

/// <summary>
/// Result of a profile migration attempt.
/// </summary>
/// <param name="MigrationPerformed">Whether migration work was performed.</param>
/// <param name="SourceLegacy">Whether legacy install profiles were migrated.</param>
/// <param name="SourceDefaults">Whether default profiles were copied.</param>
/// <param name="SentinelPath">Path to the migration sentinel file.</param>
public sealed record ProfileMigrationResult(
    bool MigrationPerformed,
    bool SourceLegacy,
    bool SourceDefaults,
    string SentinelPath);

/// <summary>
/// File-based profile migration service.
/// </summary>
public sealed class ProfileMigrationService : IProfileMigrationService
{
    private static readonly JsonSerializerOptions SentinelJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRepositoryPathService _pathService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileMigrationService"/> class.
    /// </summary>
    /// <param name="pathService">Centralized path service.</param>
    public ProfileMigrationService(IRepositoryPathService pathService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    /// <inheritdoc/>
    public ProfileMigrationResult EnsureProfilesMigrated()
    {
        var userProfilesDirectory = _pathService.UserProfilesDirectory;
        var sentinelPath = Path.Combine(
            userProfilesDirectory,
            Win11ForgePathNames.ProfileMigrationSentinelFileName);

        if (File.Exists(sentinelPath))
        {
            return new ProfileMigrationResult(false, false, false, sentinelPath);
        }

        var userProfilesAlreadyExisted = Directory.Exists(userProfilesDirectory);
        Directory.CreateDirectory(userProfilesDirectory);

        var sourceDefaults = false;
        if (!userProfilesAlreadyExisted)
        {
            sourceDefaults = CopyDefaultProfiles(userProfilesDirectory);
        }

        var sourceLegacy = MigrateLegacyProfiles(userProfilesDirectory);
        WriteSentinel(sentinelPath, sourceLegacy, sourceDefaults);

        return new ProfileMigrationResult(true, sourceLegacy, sourceDefaults, sentinelPath);
    }

    private bool CopyDefaultProfiles(string userProfilesDirectory)
    {
        var defaultProfilesDirectory = _pathService.DefaultProfilesDirectory;
        if (!Directory.Exists(defaultProfilesDirectory))
        {
            return false;
        }

        var copied = false;
        foreach (var sourceFile in Directory.GetFiles(defaultProfilesDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(defaultProfilesDirectory, sourceFile);
            var targetFile = Path.Combine(userProfilesDirectory, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile))
            {
                continue;
            }

            File.Copy(sourceFile, targetFile);
            copied = true;
        }

        return copied;
    }

    // TODO: add resumption-after-failure coverage.
    private bool MigrateLegacyProfiles(string userProfilesDirectory)
    {
        var legacyProfilesDirectory = _pathService.LegacyInstallProfilesDirectory;
        if (!Directory.Exists(legacyProfilesDirectory))
        {
            return false;
        }

        var defaultProfileFiles = GetDefaultProfileFiles();
        var defaultProfilesDirectory = _pathService.DefaultProfilesDirectory;
        var legacyAndDefaultsAreSameDirectory = string.Equals(
            Path.GetFullPath(legacyProfilesDirectory).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(defaultProfilesDirectory).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

        var migrated = false;
        foreach (var legacyFile in Directory.GetFiles(
            legacyProfilesDirectory,
            $"*{Win11ForgePathNames.JsonFileExtension}",
            SearchOption.TopDirectoryOnly))
        {
            if (legacyAndDefaultsAreSameDirectory)
            {
                continue;
            }

            var fileName = Path.GetFileName(legacyFile);
            var isDefaultProfile = defaultProfileFiles.TryGetValue(fileName, out var defaultFile);
            if (isDefaultProfile && defaultFile != null && FilesHaveSameContent(legacyFile, defaultFile))
            {
                continue;
            }

            var targetFile = GetLegacyProfileTargetPath(userProfilesDirectory, legacyFile);
            if (targetFile == null)
            {
                continue;
            }

            File.Copy(legacyFile, targetFile);
            migrated = true;
        }

        return migrated;
    }

    private Dictionary<string, string> GetDefaultProfileFiles()
    {
        var defaultProfilesDirectory = _pathService.DefaultProfilesDirectory;
        if (!Directory.Exists(defaultProfilesDirectory))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.GetFiles(
                defaultProfilesDirectory,
                $"*{Win11ForgePathNames.JsonFileExtension}",
                SearchOption.TopDirectoryOnly)
            .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetLegacyProfileTargetPath(string userProfilesDirectory, string legacyFile)
    {
        var fileName = Path.GetFileName(legacyFile);
        var targetFile = Path.Combine(userProfilesDirectory, fileName);

        if (!File.Exists(targetFile))
        {
            return targetFile;
        }

        if (FilesHaveSameContent(legacyFile, targetFile))
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        for (var index = 1; index < 1000; index++)
        {
            var suffix = index == 1
                ? Win11ForgePathNames.LegacyProfileConflictSuffix
                : $"{Win11ForgePathNames.LegacyProfileConflictSuffix}-{index}";
            var candidate = Path.Combine(
                userProfilesDirectory,
                $"{baseName}{suffix}{Win11ForgePathNames.JsonFileExtension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            if (FilesHaveSameContent(legacyFile, candidate))
            {
                return null;
            }
        }

        throw new IOException($"Could not find a migration target for legacy profile '{fileName}'.");
    }

    private static bool FilesHaveSameContent(string firstPath, string secondPath)
    {
        var first = new FileInfo(firstPath);
        var second = new FileInfo(secondPath);
        if (!first.Exists || !second.Exists || first.Length != second.Length)
        {
            return false;
        }

        return File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath));
    }

    private static void WriteSentinel(string sentinelPath, bool sourceLegacy, bool sourceDefaults)
    {
        var payload = new ProfileMigrationSentinel(
            Win11ForgePathNames.ProfileMigrationVersion,
            DateTimeOffset.UtcNow,
            sourceLegacy,
            sourceDefaults);

        var json = JsonSerializer.Serialize(payload, SentinelJsonOptions);
        File.WriteAllText(sentinelPath, json);
    }

    private sealed record ProfileMigrationSentinel(
        int Version,
        DateTimeOffset MigratedAt,
        bool SourceLegacy,
        bool SourceDefaults);
}
