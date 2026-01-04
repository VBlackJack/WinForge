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
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for PowerShellBridge - the critical C# to PowerShell bridge.
/// These tests ensure the GUI can locate and interact with PowerShell scripts.
/// </summary>
public class PowerShellBridgeTests
{
    /// <summary>
    /// Verifies that the PowerShellBridge can locate the repository root.
    /// This is the most critical test - if this fails, nothing works.
    /// </summary>
    [Fact]
    public void ResolveRepositoryRoot_ShouldFindRoot()
    {
        // Arrange & Act
        var bridge = new PowerShellBridge();
        var repositoryRoot = bridge.RepositoryRoot;

        // Assert - Root should not be null or empty
        Assert.False(string.IsNullOrEmpty(repositoryRoot),
            "RepositoryRoot should not be null or empty");
    }

    /// <summary>
    /// Verifies that the repository root contains the expected Config/version.json file.
    /// </summary>
    [Fact]
    public void ResolveRepositoryRoot_ShouldContainVersionFile()
    {
        // Arrange
        var bridge = new PowerShellBridge();
        var repositoryRoot = bridge.RepositoryRoot;

        // Act
        var versionFilePath = Path.Combine(repositoryRoot, "Config", "version.json");
        var versionFileExists = File.Exists(versionFilePath);

        // Assert
        Assert.True(versionFileExists,
            $"Config/version.json should exist at: {versionFilePath}");
    }

    /// <summary>
    /// Verifies that the repository root contains the Modules directory.
    /// </summary>
    [Fact]
    public void ResolveRepositoryRoot_ShouldContainModulesDirectory()
    {
        // Arrange
        var bridge = new PowerShellBridge();
        var repositoryRoot = bridge.RepositoryRoot;

        // Act
        var modulesPath = Path.Combine(repositoryRoot, "Modules");
        var modulesExists = Directory.Exists(modulesPath);

        // Assert
        Assert.True(modulesExists,
            $"Modules directory should exist at: {modulesPath}");
    }

    /// <summary>
    /// Verifies that the repository root contains the Profiles directory.
    /// </summary>
    [Fact]
    public void ResolveRepositoryRoot_ShouldContainProfilesDirectory()
    {
        // Arrange
        var bridge = new PowerShellBridge();
        var repositoryRoot = bridge.RepositoryRoot;

        // Act
        var profilesPath = Path.Combine(repositoryRoot, "Profiles");
        var profilesExists = Directory.Exists(profilesPath);

        // Assert
        Assert.True(profilesExists,
            $"Profiles directory should exist at: {profilesPath}");
    }

    /// <summary>
    /// Verifies that GetWin11ForgeVersionAsync returns a valid version string.
    /// </summary>
    [Fact]
    public async Task GetWin11ForgeVersionAsync_ShouldReturnVersion()
    {
        // Arrange
        var bridge = new PowerShellBridge();

        // Act
        var version = await bridge.GetWin11ForgeVersionAsync();

        // Assert
        Assert.False(string.IsNullOrEmpty(version),
            "Version should not be null or empty");
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
    }

    /// <summary>
    /// Verifies that GetAvailableProfilesAsync returns at least the base profiles.
    /// </summary>
    [Fact]
    public async Task GetAvailableProfilesAsync_ShouldReturnProfiles()
    {
        // Arrange
        var bridge = new PowerShellBridge();

        // Act
        var profiles = await bridge.GetAvailableProfilesAsync();

        // Assert
        Assert.NotNull(profiles);
        Assert.NotEmpty(profiles);
        Assert.Contains("Base", profiles);
    }

    /// <summary>
    /// Verifies that LoadProfileAsync can load the Base profile.
    /// </summary>
    [Fact]
    public async Task LoadProfileAsync_Base_ShouldLoadSuccessfully()
    {
        // Arrange
        var bridge = new PowerShellBridge();

        // Act
        var profile = await bridge.LoadProfileAsync("Base");

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("Base", profile.Name);
        Assert.NotNull(profile.Applications);
        Assert.NotEmpty(profile.Applications);
    }

    /// <summary>
    /// Verifies that GetAllApplicationsAsync returns applications from the database.
    /// </summary>
    [Fact]
    public async Task GetAllApplicationsAsync_ShouldReturnApplications()
    {
        // Arrange
        var bridge = new PowerShellBridge();

        // Act
        var apps = await bridge.GetAllApplicationsAsync();

        // Assert
        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
        Assert.True(apps.Count >= 60, $"Expected at least 60 applications, got {apps.Count}");
    }
}
