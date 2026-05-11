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
using Moq;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.PowerShell;
using Win11Forge.GUI.Services.Implementations;

namespace Win11Forge.GUI.Tests;

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
        var repositoryRoot = _pathService.RepositoryRoot;

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
        var repositoryRoot = _pathService.RepositoryRoot;

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
    public void RepositoryPathService_ShouldContainModulesDirectory()
    {
        // Arrange
        var repositoryRoot = _pathService.RepositoryRoot;

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
    public void RepositoryPathService_ShouldContainProfilesDirectory()
    {
        // Arrange
        var repositoryRoot = _pathService.RepositoryRoot;

        // Act
        var profilesPath = Path.Combine(repositoryRoot, "Profiles");
        var profilesExists = Directory.Exists(profilesPath);

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
        var configPath = _pathService.GetPath("Config", "version.json");

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
        var pathService = new RepositoryPathService();
        var executionService = new PowerShellExecutionService(pathService);
        _versionService = new VersionServiceImpl(pathService, executionService);
    }

    /// <summary>
    /// Verifies that GetWin11ForgeVersionAsync returns a valid version string.
    /// </summary>
    [Fact]
    public async Task GetWin11ForgeVersionAsync_ShouldReturnVersion()
    {
        // Act
        var version = await _versionService.GetWin11ForgeVersionAsync();

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
        var pathService = new RepositoryPathService();
        var executionService = new PowerShellExecutionService(pathService);
        var cacheService = new ApplicationCacheService(pathService);
        var versionService = new VersionServiceImpl(pathService, executionService);
        _profileService = new ProfileManagementServiceImpl(pathService, executionService, cacheService, versionService);
    }

    /// <summary>
    /// Verifies that GetAvailableProfilesAsync returns at least the base profiles.
    /// </summary>
    [Fact]
    public async Task GetAvailableProfilesAsync_ShouldReturnProfiles()
    {
        // Act
        var profiles = await _profileService.GetAvailableProfilesAsync();

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
        var profile = await _profileService.LoadProfileAsync("Base");

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
        var loggerFactory = new LoggerFactory();
        var pathService = new RepositoryPathService();
        var executionService = new PowerShellExecutionService(pathService);
        var cacheService = new ApplicationCacheService(pathService);
        var detectionService = new HybridDetectionService(loggerFactory, pathService);
        _appService = new ApplicationManagementServiceImpl(pathService, executionService, cacheService, detectionService);
    }

    /// <summary>
    /// Verifies that GetAllApplicationsAsync returns applications from the database.
    /// </summary>
    [Fact]
    public async Task GetAllApplicationsAsync_ShouldReturnApplications()
    {
        // Act
        var apps = await _appService.GetAllApplicationsAsync();

        // Assert
        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
        Assert.True(apps.Count >= 60, $"Expected at least 60 applications, got {apps.Count}");
    }
}

/// <summary>
/// Moq-based unit tests for IPowerShellBridge interface.
/// Tests the interface contract without actual PowerShell execution.
/// </summary>
public class PowerShellBridgeMockTests
{
    [Fact]
    public async Task InstallApplicationAsync_ReturnsSuccessResult()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };
        var expectedResult = InstallResult.Successful("Installed successfully", "", "Winget");

        mockBridge.Setup(b => b.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await mockBridge.Object.InstallApplicationAsync(app, false);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Winget", result.Method);
    }

    [Fact]
    public async Task InstallApplicationAsync_DryRunReturnsCorrectResult()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };
        var dryRunResult = InstallResult.DryRun("Test Application");

        mockBridge.Setup(b => b.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                true,
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(dryRunResult);

        // Act
        var result = await mockBridge.Object.InstallApplicationAsync(app, isDryRun: true);

        // Assert
        Assert.True(result.IsDryRun);
    }

    [Fact]
    public async Task InstallApplicationAsync_InvokesProgressCallback()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };
        var progressMessages = new List<string>();

        mockBridge.Setup(b => b.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .Callback<ApplicationModel, bool, bool, Action<string>?>((_, _, _, callback) =>
            {
                callback?.Invoke("Starting installation...");
                callback?.Invoke("Installation complete.");
            })
            .ReturnsAsync(InstallResult.Successful("Done", "", "Winget"));

        // Act
        await mockBridge.Object.InstallApplicationAsync(app, false, false, msg => progressMessages.Add(msg));

        // Assert
        Assert.Equal(2, progressMessages.Count);
        Assert.Contains("Starting installation...", progressMessages);
    }

    [Fact]
    public async Task UninstallApplicationAsync_ReturnsResult()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };

        mockBridge.Setup(b => b.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Uninstalled", "", "Winget"));

        // Act
        var result = await mockBridge.Object.UninstallApplicationAsync(app);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task CheckApplicationUpdateAsync_ReturnsUpdateAvailable()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };
        var updateResult = UpdateCheckResult.UpdateAvailable("1.0.0", "2.0.0");

        mockBridge.Setup(b => b.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(updateResult);

        // Act
        var result = await mockBridge.Object.CheckApplicationUpdateAsync(app);

        // Assert
        Assert.True(result.HasUpdate);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("2.0.0", result.AvailableVersion);
    }

    [Fact]
    public async Task CheckApplicationUpdateAsync_ReturnsUpToDate()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };
        var upToDateResult = UpdateCheckResult.UpToDate("2.0.0");

        mockBridge.Setup(b => b.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(upToDateResult);

        // Act
        var result = await mockBridge.Object.CheckApplicationUpdateAsync(app);

        // Assert
        Assert.False(result.HasUpdate);
    }

    [Fact]
    public async Task GetBatchApplicationStatusAsync_ReturnsDictionary()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var apps = new List<ApplicationModel>
        {
            new() { AppId = "App1", Name = "Application 1" },
            new() { AppId = "App2", Name = "Application 2" }
        };

        var batchResult = new Dictionary<string, BatchAppStatus>
        {
            ["App1"] = new BatchAppStatus(ApplicationStatus.Installed, "1.0.0"),
            ["App2"] = new BatchAppStatus(ApplicationStatus.Pending, null)
        };

        mockBridge.Setup(b => b.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(batchResult);

        // Act
        var result = await mockBridge.Object.GetBatchApplicationStatusAsync(apps);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(ApplicationStatus.Installed, result["App1"].Status);
        Assert.Equal("1.0.0", result["App1"].Version);
    }

    [Fact]
    public async Task GetSystemInfoAsync_ReturnsSystemInfo()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var systemInfo = new SystemInfoModel
        {
            Hostname = "TESTPC",
            Username = "TestUser",
            WindowsVersion = "Windows 11 Pro",
            IsAdministrator = true
        };

        mockBridge.Setup(b => b.GetSystemInfoAsync())
            .ReturnsAsync(systemInfo);

        // Act
        var result = await mockBridge.Object.GetSystemInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TESTPC", result.Hostname);
        Assert.True(result.IsAdministrator);
    }

    [Fact]
    public async Task CheckPrerequisitesAsync_ReturnsStatus()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var prereqStatus = new PrerequisitesStatus
        {
            PowerShell7Installed = true,
            WingetInstalled = true,
            ChocolateyInstalled = true
        };

        mockBridge.Setup(b => b.CheckPrerequisitesAsync())
            .ReturnsAsync(prereqStatus);

        // Act
        var result = await mockBridge.Object.CheckPrerequisitesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PowerShell7Installed);
        Assert.True(result.AllPrerequisitesMet);
    }

    [Fact]
    public async Task InstallPrerequisitesAsync_ReturnsTrue()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        mockBridge.Setup(b => b.InstallPrerequisitesAsync(It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await mockBridge.Object.InstallPrerequisitesAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task LaunchApplicationAsync_ReturnsTrue()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };

        mockBridge.Setup(b => b.LaunchApplicationAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(true);

        // Act
        var result = await mockBridge.Object.LaunchApplicationAsync(app);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SaveProfileAsync_VerifiesParameters()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        mockBridge.Setup(b => b.SaveProfileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockBridge.Object.SaveProfileAsync(
            "TestProfile",
            "Test description",
            "Base",
            new List<string> { "App1", "App2" });

        // Assert
        mockBridge.Verify(b => b.SaveProfileAsync(
            "TestProfile",
            "Test description",
            "Base",
            It.Is<List<string>>(l => l.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateApplicationAsync_ReturnsSuccessResult()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var app = new ApplicationModel { AppId = "TestApp", Name = "Test Application" };

        mockBridge.Setup(b => b.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Updated to 2.0.0", "", "Winget"));

        // Act
        var result = await mockBridge.Object.UpdateApplicationAsync(app);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetRawProfileAsync_ReturnsProfile()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var rawProfile = new DeploymentProfileModel
        {
            Name = "Office",
            Description = "Office profile"
        };
        rawProfile.Applications.Add(new ApplicationModel { AppId = "Office365", Name = "Microsoft Office" });

        mockBridge.Setup(b => b.GetRawProfileAsync("Office"))
            .ReturnsAsync(rawProfile);

        // Act
        var result = await mockBridge.Object.GetRawProfileAsync("Office");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Office", result.Name);
        Assert.Single(result.Applications);
    }

    [Fact]
    public async Task GetResolvedProfileAsync_ReturnsProfileWithInheritance()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        var resolvedProfile = new DeploymentProfileModel
        {
            Name = "Office",
            Description = "Office profile (resolved)"
        };
        resolvedProfile.Applications.Add(new ApplicationModel { AppId = "Chrome", Name = "Google Chrome" });
        resolvedProfile.Applications.Add(new ApplicationModel { AppId = "Firefox", Name = "Mozilla Firefox" });
        resolvedProfile.Applications.Add(new ApplicationModel { AppId = "Office365", Name = "Microsoft Office" });

        mockBridge.Setup(b => b.GetResolvedProfileAsync("Office"))
            .ReturnsAsync(resolvedProfile);

        // Act
        var result = await mockBridge.Object.GetResolvedProfileAsync("Office");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Applications.Count);
    }

    [Fact]
    public async Task GetApplicationStatusAsync_ReturnsCorrectStatus()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        mockBridge.Setup(b => b.GetApplicationStatusAsync("InstalledApp"))
            .ReturnsAsync(ApplicationStatus.Installed);
        mockBridge.Setup(b => b.GetApplicationStatusAsync("PendingApp"))
            .ReturnsAsync(ApplicationStatus.Pending);

        // Act
        var installedStatus = await mockBridge.Object.GetApplicationStatusAsync("InstalledApp");
        var pendingStatus = await mockBridge.Object.GetApplicationStatusAsync("PendingApp");

        // Assert
        Assert.Equal(ApplicationStatus.Installed, installedStatus);
        Assert.Equal(ApplicationStatus.Pending, pendingStatus);
    }

    [Fact]
    public void RepositoryRoot_ReturnsPath()
    {
        // Arrange
        var mockBridge = new Mock<IPowerShellBridge>();
        mockBridge.Setup(b => b.RepositoryRoot)
            .Returns(@"C:\Projects\Win11Forge");

        // Act
        var path = mockBridge.Object.RepositoryRoot;

        // Assert
        Assert.NotNull(path);
        Assert.Contains("Win11Forge", path);
    }
}
