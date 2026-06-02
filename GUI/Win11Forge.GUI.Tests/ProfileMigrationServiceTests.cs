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
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

public class ProfileMigrationServiceTests
{
    [Fact]
    public void EnsureProfilesMigrated_WhenUserProfilesDoNotExist_ShouldCopyDefaultsAndWriteSentinel()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = CreateDefaultsDirectory(workspace);
        string defaultProfilePath = WriteProfile(defaultsDirectory, "Base", "default");
        string defaultReadmePath = Path.Combine(defaultsDirectory, "README.md");
        File.WriteAllText(defaultReadmePath, "default documentation");

        ProfileMigrationService service = CreateService(workspace);
        ProfileMigrationResult result = service.EnsureProfilesMigrated();

        Assert.True(result.MigrationPerformed);
        Assert.True(result.SourceDefaults);
        Assert.False(result.SourceLegacy);
        Assert.True(File.Exists(Path.Combine(workspace.UserProfilesDirectory, Path.GetFileName(defaultProfilePath))));
        Assert.True(File.Exists(Path.Combine(workspace.UserProfilesDirectory, Path.GetFileName(defaultReadmePath))));
        AssertSentinel(result.SentinelPath, sourceDefaults: true, sourceLegacy: false);
    }

    [Fact]
    public void EnsureProfilesMigrated_WhenSentinelExists_ShouldNotCopyAgain()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = CreateDefaultsDirectory(workspace);
        WriteProfile(defaultsDirectory, "Base", "default");

        ProfileMigrationService service = CreateService(workspace);
        service.EnsureProfilesMigrated();

        string userProfilePath = Path.Combine(
            workspace.UserProfilesDirectory,
            $"Base{Win11ForgePathNames.JsonFileExtension}");
        File.WriteAllText(userProfilePath, "user modified");

        ProfileMigrationResult secondResult = service.EnsureProfilesMigrated();

        Assert.False(secondResult.MigrationPerformed);
        Assert.Equal("user modified", File.ReadAllText(userProfilePath));
    }

    [Fact]
    public void EnsureProfilesMigrated_WhenLegacyProfilesExist_ShouldMigrateNonDefaultsAndConflicts()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = CreateDefaultsDirectory(workspace);
        WriteProfile(defaultsDirectory, "Base", "default base");
        WriteProfile(defaultsDirectory, "Office", "default office");

        string legacyDirectory = Path.Combine(workspace.RepositoryRoot, Win11ForgePathNames.ProfilesDirectoryName);
        WriteProfile(legacyDirectory, "Base", "user modified base");
        WriteProfile(legacyDirectory, "Office", "default office");
        WriteProfile(legacyDirectory, "Custom", "custom profile");

        ProfileMigrationService service = CreateService(workspace);
        ProfileMigrationResult result = service.EnsureProfilesMigrated();

        Assert.True(result.MigrationPerformed);
        Assert.True(result.SourceDefaults);
        Assert.True(result.SourceLegacy);
        Assert.Equal(
            "default base",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Base{Win11ForgePathNames.JsonFileExtension}")));
        Assert.Equal(
            "user modified base",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Base{Win11ForgePathNames.LegacyProfileConflictSuffix}{Win11ForgePathNames.JsonFileExtension}")));
        Assert.Equal(
            "custom profile",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Custom{Win11ForgePathNames.JsonFileExtension}")));
        Assert.False(File.Exists(Path.Combine(
            workspace.UserProfilesDirectory,
            $"Office{Win11ForgePathNames.LegacyProfileConflictSuffix}{Win11ForgePathNames.JsonFileExtension}")));
        AssertSentinel(result.SentinelPath, sourceDefaults: true, sourceLegacy: true);
    }

    private static ProfileMigrationService CreateService(TestWorkspace workspace)
    {
        RepositoryPathService pathService = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);
        return new ProfileMigrationService(pathService);
    }

    private static string CreateDefaultsDirectory(TestWorkspace workspace)
    {
        string defaultsDirectory = Path.Combine(
            workspace.RepositoryRoot,
            Win11ForgePathNames.ProfilesDirectoryName,
            Win11ForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultsDirectory);
        return defaultsDirectory;
    }

    private static string WriteProfile(string directory, string name, string content)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{name}{Win11ForgePathNames.JsonFileExtension}");
        File.WriteAllText(path, content);
        return path;
    }

    private static void AssertSentinel(string sentinelPath, bool sourceDefaults, bool sourceLegacy)
    {
        Assert.True(File.Exists(sentinelPath));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sentinelPath));
        JsonElement root = document.RootElement;

        Assert.Equal(Win11ForgePathNames.ProfileMigrationVersion, root.GetProperty("version").GetInt32());
        Assert.True(root.TryGetProperty("migratedAt", out JsonElement migratedAt));
        Assert.False(string.IsNullOrWhiteSpace(migratedAt.GetString()));
        Assert.Equal(sourceDefaults, root.GetProperty("sourceDefaults").GetBoolean());
        Assert.Equal(sourceLegacy, root.GetProperty("sourceLegacy").GetBoolean());
    }
}
