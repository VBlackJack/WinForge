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
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Implementations;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

/// <summary>
/// Integration tests for the PowerShell services.
/// These tests verify that the services can locate and interact with the repository.
/// </summary>
public class PowerShellServicesIntegrationTests
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;

    public PowerShellServicesIntegrationTests()
    {
        _pathService = new RepositoryPathService();
        _executionService = new PowerShellExecutionService(_pathService);
    }

    /// <summary>
    /// Verifies that the RepositoryPathService can locate the repository root.
    /// This is the most critical test - if this fails, nothing works.
    /// </summary>
    [Fact]
    public void RepositoryPathService_ShouldFindRoot()
    {
        // Arrange & Act
        string repositoryRoot = _pathService.RepositoryRoot;

        // Assert - Root should not be null or empty
        Assert.False(string.IsNullOrEmpty(repositoryRoot),
            "RepositoryRoot should not be null or empty");
    }

    /// <summary>
    /// Verifies that the repository root contains the expected Config/version.json file.
    /// </summary>
    [Fact]
    public void RepositoryPathService_ShouldContainVersionFile()
    {
        // Arrange
        string repositoryRoot = _pathService.RepositoryRoot;

        // Act
        string versionFilePath = Path.Combine(repositoryRoot, "Config", "version.json");
        bool versionFileExists = File.Exists(versionFilePath);

        // Assert
        Assert.True(versionFileExists,
            $"Config/version.json should exist at: {versionFilePath}");
    }

    /// <summary>
    /// Verifies that the repository root contains the Modules directory.
    /// </summary>
    [Fact]
    public void RepositoryPathService_ShouldContainModulesDirectory()
    {
        // Arrange
        string repositoryRoot = _pathService.RepositoryRoot;

        // Act
        string modulesPath = Path.Combine(repositoryRoot, "Modules");
        bool modulesExists = Directory.Exists(modulesPath);

        // Assert
        Assert.True(modulesExists,
            $"Modules directory should exist at: {modulesPath}");
    }

    /// <summary>
    /// Verifies that the repository root contains the Profiles directory.
    /// </summary>
    [Fact]
    public void RepositoryPathService_ShouldContainProfilesDirectory()
    {
        // Arrange
        string repositoryRoot = _pathService.RepositoryRoot;

        // Act
        string profilesPath = Path.Combine(repositoryRoot, "Profiles");
        bool profilesExists = Directory.Exists(profilesPath);

        // Assert
        Assert.True(profilesExists,
            $"Profiles directory should exist at: {profilesPath}");
    }

    /// <summary>
    /// Verifies that GetPath returns correct combined paths.
    /// </summary>
    [Fact]
    public void RepositoryPathService_GetPath_ShouldCombinePaths()
    {
        // Arrange & Act
        string configPath = _pathService.GetPath("Config", "version.json");

        // Assert
        Assert.EndsWith("version.json", configPath);
        Assert.Contains("Config", configPath);
    }
}

/// <summary>
/// Integration tests for VersionService.
/// </summary>
public class VersionServiceIntegrationTests
{
    private readonly IVersionService _versionService;

    public VersionServiceIntegrationTests()
    {
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService executionService = new PowerShellExecutionService(pathService);
        _versionService = new VersionServiceImpl(pathService, executionService);
    }

    /// <summary>
    /// Verifies that GetWinForgeVersionAsync returns a valid version string.
    /// </summary>
    [Fact]
    public async Task GetWinForgeVersionAsync_ShouldReturnVersion()
    {
        // Act
        string version = await _versionService.GetWinForgeVersionAsync();

        // Assert
        Assert.False(string.IsNullOrEmpty(version),
            "Version should not be null or empty");
        Assert.Matches(@"^\d{10}$", version);
    }
}

/// <summary>
/// Integration tests for ProfileManagementService.
/// </summary>
public class ProfileManagementServiceIntegrationTests
{
    private readonly IProfileManagementService _profileService;

    public ProfileManagementServiceIntegrationTests()
    {
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService executionService = new PowerShellExecutionService(pathService);
        ApplicationCacheService cacheService = new ApplicationCacheService(pathService);
        VersionServiceImpl versionService = new VersionServiceImpl(pathService, executionService);
        _profileService = new ProfileManagementServiceImpl(pathService, executionService, cacheService, versionService);
    }

    /// <summary>
    /// Verifies that GetAvailableProfilesAsync returns at least the base profiles.
    /// </summary>
    [Fact]
    public async Task GetAvailableProfilesAsync_ShouldReturnProfiles()
    {
        // Act
        List<string> profiles = await _profileService.GetAvailableProfilesAsync();

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
        // Act
        DeploymentProfileModel profile = await _profileService.LoadProfileAsync("Base");

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("Base", profile.Name);
        Assert.NotNull(profile.Applications);
        Assert.NotEmpty(profile.Applications);
    }
}

/// <summary>
/// Integration tests for ApplicationManagementService.
/// </summary>
public class ApplicationManagementServiceIntegrationTests
{
    private readonly IApplicationManagementService _appService;

    public ApplicationManagementServiceIntegrationTests()
    {
        LoggerFactory loggerFactory = new LoggerFactory();
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService executionService = new PowerShellExecutionService(pathService);
        ApplicationCacheService cacheService = new ApplicationCacheService(pathService);
        HybridDetectionService detectionService = new HybridDetectionService(loggerFactory, pathService);
        _appService = new ApplicationManagementServiceImpl(
            pathService,
            executionService,
            cacheService,
            detectionService,
            new ApplicationLauncher(cacheService));
    }

    /// <summary>
    /// Verifies that GetAllApplicationsAsync returns applications from the database.
    /// </summary>
    [Fact]
    public async Task GetAllApplicationsAsync_ShouldReturnApplications()
    {
        // Act
        List<ApplicationModel> apps = await _appService.GetAllApplicationsAsync();

        // Assert
        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
        Assert.True(apps.Count >= 60, $"Expected at least 60 applications, got {apps.Count}");
    }
}
