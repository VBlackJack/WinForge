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
using WinForge.GUI.Configuration;
using WinForge.GUI.Services;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

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
            $"Base{WinForgePathNames.JsonFileExtension}");
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

        string legacyDirectory = Path.Combine(workspace.RepositoryRoot, WinForgePathNames.ProfilesDirectoryName);
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
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Base{WinForgePathNames.JsonFileExtension}")));
        Assert.Equal(
            "user modified base",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Base{WinForgePathNames.LegacyProfileConflictSuffix}{WinForgePathNames.JsonFileExtension}")));
        Assert.Equal(
            "custom profile",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Custom{WinForgePathNames.JsonFileExtension}")));
        Assert.False(File.Exists(Path.Combine(
            workspace.UserProfilesDirectory,
            $"Office{WinForgePathNames.LegacyProfileConflictSuffix}{WinForgePathNames.JsonFileExtension}")));
        AssertSentinel(result.SentinelPath, sourceDefaults: true, sourceLegacy: true);
    }

    /// <summary>
    /// The sentinel is written only after migration returns, so a run that dies partway
    /// leaves no sentinel and the next start must retry. This pins that the retry
    /// completes the work instead of duplicating what already landed.
    /// </summary>
    [Fact]
    public void EnsureProfilesMigrated_AfterAnInterruptedRun_ShouldResumeWithoutDuplicating()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = CreateDefaultsDirectory(workspace);
        WriteProfile(defaultsDirectory, "Base", "default base");

        string legacyDirectory = Path.Combine(workspace.RepositoryRoot, WinForgePathNames.ProfilesDirectoryName);
        WriteProfile(legacyDirectory, "Custom", "custom profile");
        WriteProfile(legacyDirectory, "Other", "other profile");

        // Simulate a run interrupted after copying one legacy profile: the file is
        // present, the sentinel is not.
        Directory.CreateDirectory(workspace.UserProfilesDirectory);
        WriteProfile(workspace.UserProfilesDirectory, "Custom", "custom profile");

        ProfileMigrationService service = CreateService(workspace);
        ProfileMigrationResult result = service.EnsureProfilesMigrated();

        Assert.True(result.MigrationPerformed);

        // The already-migrated profile is recognised by content and not copied again.
        Assert.False(File.Exists(Path.Combine(
            workspace.UserProfilesDirectory,
            $"Custom{WinForgePathNames.LegacyProfileConflictSuffix}{WinForgePathNames.JsonFileExtension}")));
        Assert.Equal(
            "custom profile",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Custom{WinForgePathNames.JsonFileExtension}")));

        // The profile the interrupted run had not reached is migrated now.
        Assert.Equal(
            "other profile",
            File.ReadAllText(Path.Combine(workspace.UserProfilesDirectory, $"Other{WinForgePathNames.JsonFileExtension}")));

        Assert.True(File.Exists(result.SentinelPath));
    }

    /// <summary>
    /// If the sentinel cannot be written, migration must stay pending rather than be
    /// silently recorded as done.
    /// </summary>
    [Fact]
    public void EnsureProfilesMigrated_WhenTheSentinelCannotBeWritten_ShouldLeaveMigrationPending()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = CreateDefaultsDirectory(workspace);
        WriteProfile(defaultsDirectory, "Base", "default base");

        // A directory at the sentinel path makes File.WriteAllText fail.
        Directory.CreateDirectory(workspace.UserProfilesDirectory);
        string sentinelPath = Path.Combine(
            workspace.UserProfilesDirectory,
            WinForgePathNames.ProfileMigrationSentinelFileName);
        Directory.CreateDirectory(sentinelPath);

        ProfileMigrationService service = CreateService(workspace);

        // The exact type depends on the platform (a directory in the way surfaces as
        // UnauthorizedAccessException on Windows); what matters is that it fails loudly.
        Assert.ThrowsAny<SystemException>(() => service.EnsureProfilesMigrated());
        Assert.False(File.Exists(sentinelPath));

        // Once the blocker is gone the next run completes and records the sentinel.
        Directory.Delete(sentinelPath);
        ProfileMigrationResult result = service.EnsureProfilesMigrated();

        Assert.True(result.MigrationPerformed);
        Assert.True(File.Exists(result.SentinelPath));
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
            WinForgePathNames.ProfilesDirectoryName,
            WinForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultsDirectory);
        return defaultsDirectory;
    }

    private static string WriteProfile(string directory, string name, string content)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{name}{WinForgePathNames.JsonFileExtension}");
        File.WriteAllText(path, content);
        return path;
    }

    private static void AssertSentinel(string sentinelPath, bool sourceDefaults, bool sourceLegacy)
    {
        Assert.True(File.Exists(sentinelPath));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sentinelPath));
        JsonElement root = document.RootElement;

        Assert.Equal(WinForgePathNames.ProfileMigrationVersion, root.GetProperty("version").GetInt32());
        Assert.True(root.TryGetProperty("migratedAt", out JsonElement migratedAt));
        Assert.False(string.IsNullOrWhiteSpace(migratedAt.GetString()));
        Assert.Equal(sourceDefaults, root.GetProperty("sourceDefaults").GetBoolean());
        Assert.Equal(sourceLegacy, root.GetProperty("sourceLegacy").GetBoolean());
    }
}
