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

using System.Collections.ObjectModel;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for AppsViewModel - filtering, scanning, and installation logic.
/// </summary>
public class AppsViewModelTests
{
    /// <summary>
    /// Creates a mock PowerShellBridge for testing.
    /// </summary>
    private static MockPowerShellBridge CreateMockBridge()
    {
        return new MockPowerShellBridge();
    }

    /// <summary>
    /// Creates a mock AppSettingsService for testing.
    /// </summary>
    private static MockAppSettingsService CreateMockSettingsService()
    {
        return new MockAppSettingsService();
    }

    /// <summary>
    /// Creates a mock DeploymentStateService for testing.
    /// </summary>
    private static MockDeploymentStateService CreateMockDeploymentStateService()
    {
        return new MockDeploymentStateService();
    }

    /// <summary>
    /// Creates a configured AppsViewModel for testing.
    /// </summary>
    private static AppsViewModel CreateViewModel(MockPowerShellBridge? bridge = null, MockAppSettingsService? settings = null, MockDeploymentStateService? deploymentState = null)
    {
        return new AppsViewModel(bridge ?? CreateMockBridge(), settings ?? CreateMockSettingsService(), deploymentState ?? CreateMockDeploymentStateService());
    }

    /// <summary>
    /// Verifies that filtering by name returns only matching applications.
    /// </summary>
    [Fact]
    public async Task Filter_ShouldReturnOnlyMatchingNames()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search for "Visual"
        viewModel.SearchText = "Visual";

        // Assert
        Assert.True(viewModel.FilteredApplications.Count > 0,
            "Should find at least one application matching 'Visual'");
        Assert.All(viewModel.FilteredApplications, app =>
            Assert.True(
                app.Name.Contains("Visual", StringComparison.OrdinalIgnoreCase) ||
                app.AppId.Contains("Visual", StringComparison.OrdinalIgnoreCase) ||
                app.Description.Contains("Visual", StringComparison.OrdinalIgnoreCase),
                $"Application '{app.Name}' should match search term 'Visual'"));
    }

    /// <summary>
    /// Verifies that filtering by category returns only matching applications.
    /// </summary>
    [Fact]
    public async Task Filter_ShouldReturnOnlyMatchingCategories()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Select "Development" category
        viewModel.SelectedCategory = "Development";

        // Assert
        Assert.True(viewModel.FilteredApplications.Count > 0,
            "Should find at least one application in 'Development' category");
        Assert.All(viewModel.FilteredApplications, app =>
            Assert.Equal("Development", app.Category, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that combining name and category filters works correctly.
    /// </summary>
    [Fact]
    public async Task Filter_ShouldCombineNameAndCategory()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search for "Code" in "Development" category
        viewModel.SelectedCategory = "Development";
        viewModel.SearchText = "Code";

        // Assert
        Assert.All(viewModel.FilteredApplications, app =>
        {
            Assert.Equal("Development", app.Category, StringComparer.OrdinalIgnoreCase);
            Assert.True(
                app.Name.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                app.AppId.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                app.Description.Contains("Code", StringComparison.OrdinalIgnoreCase),
                $"Application '{app.Name}' should match both filters");
        });
    }

    /// <summary>
    /// Verifies that clearing filters restores the full list.
    /// </summary>
    [Fact]
    public async Task ClearFilters_ShouldRestoreFullList()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();
        var originalCount = viewModel.FilteredApplications.Count;

        // Apply some filters
        viewModel.SearchText = "NonexistentApp12345";
        Assert.Empty(viewModel.FilteredApplications);

        // Act
        viewModel.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Equal(originalCount, viewModel.FilteredApplications.Count);
        Assert.Empty(viewModel.SearchText);
    }

    /// <summary>
    /// Verifies that categories are extracted dynamically from applications.
    /// </summary>
    [Fact]
    public async Task Categories_ShouldBeExtractedFromApplications()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);

        // Act
        await viewModel.InitializeAsync();

        // Assert - Should have "All Categories" plus actual categories
        Assert.True(viewModel.Categories.Count >= 2,
            "Should have at least 'All Categories' and one actual category");
        Assert.Equal(Resources.Resources.Apps_CategoryAll, viewModel.Categories[0]);
    }

    /// <summary>
    /// Verifies that search is case-insensitive.
    /// </summary>
    [Fact]
    public async Task Filter_ShouldBeCaseInsensitive()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search with different cases
        viewModel.SearchText = "visual";
        var lowerCount = viewModel.FilteredApplications.Count;

        viewModel.SearchText = "VISUAL";
        var upperCount = viewModel.FilteredApplications.Count;

        viewModel.SearchText = "ViSuAl";
        var mixedCount = viewModel.FilteredApplications.Count;

        // Assert
        Assert.Equal(lowerCount, upperCount);
        Assert.Equal(lowerCount, mixedCount);
    }

    /// <summary>
    /// Verifies that FilteredCount is updated when filtering.
    /// </summary>
    [Fact]
    public async Task FilteredCount_ShouldUpdateWhenFiltering()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();
        var totalCount = viewModel.FilteredCount;

        // Act
        viewModel.SearchText = "Visual";

        // Assert
        Assert.True(viewModel.FilteredCount < totalCount,
            "FilteredCount should be less after applying filter");
        Assert.Equal(viewModel.FilteredApplications.Count, viewModel.FilteredCount);
    }

    /// <summary>
    /// Verifies that empty search returns all applications (with category filter).
    /// </summary>
    [Fact]
    public async Task Filter_EmptySearch_ShouldReturnAllInCategory()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Select a category first
        viewModel.SelectedCategory = "Development";
        var categoryCount = viewModel.FilteredApplications.Count;

        // Act - Set empty search
        viewModel.SearchText = "";

        // Assert - Should still show all apps in category
        Assert.Equal(categoryCount, viewModel.FilteredApplications.Count);
    }
}

/// <summary>
/// Mock implementation of IPowerShellBridge for unit testing.
/// Returns predefined data without executing PowerShell.
/// </summary>
internal class MockPowerShellBridge : IPowerShellBridge
{
    private readonly List<ApplicationModel> _mockApplications;

    public MockPowerShellBridge()
    {
        _mockApplications =
        [
            new ApplicationModel
            {
                Name = "Visual Studio Code",
                AppId = "Microsoft.VisualStudioCode",
                Category = "Development",
                Description = "Code editing. Redefined.",
                Priority = 10,
                Sources = "Winget"
            },
            new ApplicationModel
            {
                Name = "Visual Studio 2022",
                AppId = "Microsoft.VisualStudio.2022.Community",
                Category = "Development",
                Description = "Full IDE for .NET development",
                Priority = 20,
                Sources = "Winget"
            },
            new ApplicationModel
            {
                Name = "Git",
                AppId = "Git.Git",
                Category = "Development",
                Description = "Distributed version control system",
                Priority = 5,
                Sources = "Winget, Chocolatey"
            },
            new ApplicationModel
            {
                Name = "7-Zip",
                AppId = "7zip.7zip",
                Category = "Utilities",
                Description = "File archiver with high compression ratio",
                Priority = 15,
                Sources = "Winget, Chocolatey"
            },
            new ApplicationModel
            {
                Name = "Notepad++",
                AppId = "Notepad++.Notepad++",
                Category = "Utilities",
                Description = "Free source code editor",
                Priority = 25,
                Sources = "Winget, Chocolatey"
            },
            new ApplicationModel
            {
                Name = "Firefox",
                AppId = "Mozilla.Firefox",
                Category = "Browsers",
                Description = "Fast, private and free web browser",
                Priority = 30,
                Sources = "Winget"
            },
            new ApplicationModel
            {
                Name = "Chrome",
                AppId = "Google.Chrome",
                Category = "Browsers",
                Description = "Fast, secure browser by Google",
                Priority = 35,
                Sources = "Winget"
            }
        ];
    }

    public string RepositoryRoot => @"C:\Test\Win11Forge";

    public Task<string> GetWin11ForgeVersionAsync() => Task.FromResult("3.0.0");

    public Task<List<string>> GetAvailableProfilesAsync() =>
        Task.FromResult(new List<string> { "Base", "Developer", "Gaming" });

    public Task<DeploymentProfileModel> LoadProfileAsync(string profileName) =>
        Task.FromResult(new DeploymentProfileModel { Name = profileName });

    public Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        Action<string>? progressCallback = null) =>
        Task.FromResult(new InstallResult { Success = true, AlreadyInstalled = false });

    public Task<List<ApplicationModel>> GetAllApplicationsAsync() =>
        Task.FromResult(new List<ApplicationModel>(_mockApplications));

    public Task<ApplicationStatus> GetApplicationStatusAsync(string appId) =>
        Task.FromResult(ApplicationStatus.Pending);

    public Task<DeploymentProfileModel> GetRawProfileAsync(string profileName) =>
        Task.FromResult(new DeploymentProfileModel { Name = profileName });

    public Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName) =>
        LoadProfileAsync(profileName);

    public Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds) =>
        Task.CompletedTask;

    public Task<SystemInfoModel> GetSystemInfoAsync() =>
        Task.FromResult(new SystemInfoModel
        {
            Hostname = "TEST-PC",
            WindowsVersion = "Windows 11 Pro",
            WindowsBuild = "22000",
            IsAdministrator = true
        });

    public Task<PrerequisitesStatus> CheckPrerequisitesAsync() =>
        Task.FromResult(new PrerequisitesStatus
        {
            PowerShell7Installed = true,
            PowerShellVersion = "7.4.0",
            ChocolateyInstalled = true,
            WingetInstalled = true
        });

    public Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null) =>
        Task.FromResult(true);

    public Task<InstallResult> UninstallApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null) =>
        Task.FromResult(new InstallResult { Success = true });

    public Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app) =>
        Task.FromResult(UpdateCheckResult.UpToDate());

    public Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null) =>
        Task.FromResult(new InstallResult { Success = true });

    public Task<bool> LaunchApplicationAsync(ApplicationModel app) =>
        Task.FromResult(true);

    public Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps) =>
        Task.FromResult<Dictionary<string, BatchAppStatus>?>(new Dictionary<string, BatchAppStatus>());
}

/// <summary>
/// Mock implementation of IDeploymentStateService for unit testing.
/// </summary>
internal class MockDeploymentStateService : IDeploymentStateService
{
    public bool IsDeploying => false;
    public bool IsPaused => false;
    public string? StatusMessage => null;
    public string? CurrentAppName => null;
    public int CompletedCount => 0;
    public int TotalCount => 0;
    public double ProgressPercentage => 0;
    public string? ElapsedTime => null;
    public string? EstimatedTimeRemaining => null;
    public ObservableCollection<ApplicationModel> Applications { get; } = new();

    public event EventHandler? StateChanged;
    public event EventHandler? PauseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? CancelRequested;

    public void StartDeployment(IEnumerable<ApplicationModel> apps) { }
    public void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage) { }
    public void UpdateTime(string? elapsed, string? remaining) { }
    public void SetPaused(bool isPaused) { }
    public void EndDeployment() { }
    public void RequestPause() => PauseRequested?.Invoke(this, EventArgs.Empty);
    public void RequestResume() => ResumeRequested?.Invoke(this, EventArgs.Empty);
    public void RequestCancel() => CancelRequested?.Invoke(this, EventArgs.Empty);

    // Suppress unused event warnings
    private void SuppressWarnings()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
