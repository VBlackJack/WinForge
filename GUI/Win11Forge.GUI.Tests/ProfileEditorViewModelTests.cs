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
/// Tests for ProfileEditorViewModel - inheritance logic, add/remove, and save functionality.
/// Critical tests to ensure profile data integrity.
/// </summary>
public class ProfileEditorViewModelTests
{
    /// <summary>
    /// Verifies that loading a profile correctly separates inherited and local applications.
    /// This is the most critical test for data integrity.
    /// </summary>
    [Fact]
    public async Task Load_ShouldSeparateInheritedAndLocalApps()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);

        // Act - Load the "Developer" profile which inherits from "Base"
        await viewModel.InitializeEditProfileAsync("Developer");

        // Assert - Inherited apps should come from parent (Base)
        Assert.NotEmpty(viewModel.InheritedApplications);
        Assert.All(viewModel.InheritedApplications, app =>
            Assert.True(bridge.BaseProfileAppIds.Contains(app.AppId),
                $"Inherited app '{app.AppId}' should be from Base profile"));

        // Assert - Added apps should be local to Developer profile
        Assert.NotEmpty(viewModel.AddedApplications);
        Assert.All(viewModel.AddedApplications, app =>
            Assert.True(bridge.DeveloperLocalAppIds.Contains(app.AppId),
                $"Added app '{app.AppId}' should be local to Developer profile"));

        // Assert - No overlap between inherited and added
        var inheritedIds = viewModel.InheritedApplications.Select(a => a.AppId).ToHashSet();
        var addedIds = viewModel.AddedApplications.Select(a => a.AppId).ToHashSet();
        Assert.Empty(inheritedIds.Intersect(addedIds));
    }

    /// <summary>
    /// Verifies that adding an application places it in the local list only.
    /// </summary>
    [Fact]
    public async Task AddApp_ShouldAddToLocalList()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeNewProfileAsync();

        var newApp = new ApplicationModel
        {
            AppId = "New.App.Test",
            Name = "Test Application",
            Category = "Testing"
        };

        var initialCount = viewModel.AddedApplications.Count;

        // Act - Use reflection to call the internal method
        var method = typeof(ProfileEditorViewModel).GetMethod(
            "AddApplicationInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(viewModel, [newApp]);

        // Assert
        Assert.Equal(initialCount + 1, viewModel.AddedApplications.Count);
        Assert.Contains(viewModel.AddedApplications, a => a.AppId == "New.App.Test");
        Assert.DoesNotContain(viewModel.InheritedApplications, a => a.AppId == "New.App.Test");
    }

    /// <summary>
    /// Verifies that removing an application removes it from the local list.
    /// </summary>
    [Fact]
    public async Task RemoveApp_ShouldRemoveFromLocalList()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeEditProfileAsync("Developer");

        var appToRemove = viewModel.AddedApplications.First();
        var initialCount = viewModel.AddedApplications.Count;

        // Act
        viewModel.RemoveApplicationCommand.Execute(appToRemove);

        // Assert
        Assert.Equal(initialCount - 1, viewModel.AddedApplications.Count);
        Assert.DoesNotContain(viewModel.AddedApplications, a => a.AppId == appToRemove.AppId);
    }

    /// <summary>
    /// Verifies that inherited applications cannot be removed (they're in a separate read-only collection).
    /// The RemoveApplicationCommand only operates on AddedApplications.
    /// </summary>
    [Fact]
    public async Task RemoveApp_ShouldNotAffectInheritedApps()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeEditProfileAsync("Developer");

        var inheritedApp = viewModel.InheritedApplications.First();
        var initialInheritedCount = viewModel.InheritedApplications.Count;
        var initialAddedCount = viewModel.AddedApplications.Count;

        // Act - Try to remove an inherited app
        viewModel.RemoveApplicationCommand.Execute(inheritedApp);

        // Assert - Inherited collection should be unchanged
        Assert.Equal(initialInheritedCount, viewModel.InheritedApplications.Count);
        Assert.Contains(viewModel.InheritedApplications, a => a.AppId == inheritedApp.AppId);

        // Assert - Added collection should also be unchanged
        Assert.Equal(initialAddedCount, viewModel.AddedApplications.Count);
    }

    /// <summary>
    /// Verifies that save only includes local applications, not inherited ones.
    /// </summary>
    [Fact]
    public async Task Save_ShouldCallBridgeWithCorrectData()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeEditProfileAsync("Developer");

        // Capture what would be saved
        var expectedLocalAppIds = viewModel.AddedApplications.Select(a => a.AppId).ToList();

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert - Verify the bridge received correct data
        Assert.NotNull(bridge.LastSavedProfileName);
        Assert.Equal("Developer", bridge.LastSavedProfileName);
        Assert.Equal("Base", bridge.LastSavedParentProfile);
        Assert.NotNull(bridge.LastSavedAppIds);
        Assert.Equal(expectedLocalAppIds.Count, bridge.LastSavedAppIds.Count);
        Assert.All(expectedLocalAppIds, id =>
            Assert.Contains(id, bridge.LastSavedAppIds));

        // Assert - Inherited apps should NOT be in the saved list
        Assert.All(bridge.BaseProfileAppIds, id =>
            Assert.DoesNotContain(id, bridge.LastSavedAppIds));
    }

    /// <summary>
    /// Verifies that duplicate applications cannot be added.
    /// </summary>
    [Fact]
    public async Task AddApp_ShouldPreventDuplicates()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeEditProfileAsync("Developer");

        // Get an app already in Added list
        var existingApp = viewModel.AddedApplications.First();
        var initialCount = viewModel.AddedApplications.Count;

        // Act - Try to add a duplicate
        var method = typeof(ProfileEditorViewModel).GetMethod(
            "AddApplicationInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(viewModel, [existingApp]);

        // Assert - Count should not change
        Assert.Equal(initialCount, viewModel.AddedApplications.Count);
    }

    /// <summary>
    /// Verifies that apps already in inherited list cannot be added to local list.
    /// </summary>
    [Fact]
    public async Task AddApp_ShouldPreventAddingInheritedApp()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeEditProfileAsync("Developer");

        // Get an inherited app
        var inheritedApp = viewModel.InheritedApplications.First();
        var initialAddedCount = viewModel.AddedApplications.Count;

        // Act - Try to add an inherited app to the local list
        var method = typeof(ProfileEditorViewModel).GetMethod(
            "AddApplicationInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(viewModel, [inheritedApp]);

        // Assert - Added list should not change
        Assert.Equal(initialAddedCount, viewModel.AddedApplications.Count);
        Assert.DoesNotContain(viewModel.AddedApplications, a => a.AppId == inheritedApp.AppId);
    }

    /// <summary>
    /// Verifies validation prevents empty profile name.
    /// </summary>
    [Fact]
    public async Task Save_ShouldFailWithEmptyName()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeNewProfileAsync();
        viewModel.ProfileName = "";

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Null(bridge.LastSavedProfileName); // Save should not be called
    }

    /// <summary>
    /// Verifies that changing parent profile updates inherited applications.
    /// </summary>
    [Fact]
    public async Task ChangeParent_ShouldUpdateInheritedApps()
    {
        // Arrange
        var bridge = new ProfileEditorMockBridge();
        var viewModel = new ProfileEditorViewModel(bridge);
        await viewModel.InitializeNewProfileAsync();

        // Initially no parent selected
        Assert.Empty(viewModel.InheritedApplications);

        // Act - Select "Base" as parent
        viewModel.SelectedParent = "Base";

        // Wait for async loading
        await Task.Delay(100);

        // Assert - Should have inherited apps from Base
        Assert.NotEmpty(viewModel.InheritedApplications);
    }
}

/// <summary>
/// Mock implementation of IPowerShellBridge for ProfileEditorViewModel testing.
/// Provides configurable profile data for testing inheritance scenarios.
/// </summary>
internal class ProfileEditorMockBridge : IPowerShellBridge
{
    /// <summary>
    /// App IDs that belong to the Base profile.
    /// </summary>
    public List<string> BaseProfileAppIds { get; } = ["7zip.7zip", "Notepad++.Notepad++", "Git.Git"];

    /// <summary>
    /// App IDs that are locally added in the Developer profile (not inherited).
    /// </summary>
    public List<string> DeveloperLocalAppIds { get; } = ["Microsoft.VisualStudioCode", "Docker.DockerDesktop"];

    /// <summary>
    /// Last saved profile name (for verification).
    /// </summary>
    public string? LastSavedProfileName { get; private set; }

    /// <summary>
    /// Last saved parent profile (for verification).
    /// </summary>
    public string? LastSavedParentProfile { get; private set; }

    /// <summary>
    /// Last saved app IDs (for verification).
    /// </summary>
    public List<string>? LastSavedAppIds { get; private set; }

    private readonly List<ApplicationModel> _allApplications;

    public ProfileEditorMockBridge()
    {
        _allApplications =
        [
            new ApplicationModel { AppId = "7zip.7zip", Name = "7-Zip", Category = "Utilities" },
            new ApplicationModel { AppId = "Notepad++.Notepad++", Name = "Notepad++", Category = "Utilities" },
            new ApplicationModel { AppId = "Git.Git", Name = "Git", Category = "Development" },
            new ApplicationModel { AppId = "Microsoft.VisualStudioCode", Name = "VS Code", Category = "Development" },
            new ApplicationModel { AppId = "Docker.DockerDesktop", Name = "Docker Desktop", Category = "Development" },
            new ApplicationModel { AppId = "Mozilla.Firefox", Name = "Firefox", Category = "Browsers" },
            new ApplicationModel { AppId = "Google.Chrome", Name = "Chrome", Category = "Browsers" }
        ];
    }

    public string RepositoryRoot => @"C:\Test\Win11Forge";

    public Task<string> GetWin11ForgeVersionAsync() => Task.FromResult("3.0.0");

    public Task<List<string>> GetAvailableProfilesAsync() =>
        Task.FromResult(new List<string> { "Base", "Developer", "Gaming" });

    public Task<DeploymentProfileModel> LoadProfileAsync(string profileName) =>
        GetResolvedProfileAsync(profileName);

    public Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        Action<string>? progressCallback = null) =>
        Task.FromResult(new InstallResult { Success = true });

    public Task<List<ApplicationModel>> GetAllApplicationsAsync() =>
        Task.FromResult(new List<ApplicationModel>(_allApplications));

    public Task<ApplicationStatus> GetApplicationStatusAsync(string appId) =>
        Task.FromResult(ApplicationStatus.Pending);

    /// <summary>
    /// Returns the raw profile without inheritance (only local apps).
    /// </summary>
    public Task<DeploymentProfileModel> GetRawProfileAsync(string profileName)
    {
        var profile = new DeploymentProfileModel { Name = profileName };

        if (profileName == "Developer")
        {
            // Developer profile inherits from Base and has its own apps
            profile.InheritedFrom = new List<string> { "Base" };
            profile.Applications = new ObservableCollection<ApplicationModel>(
                _allApplications.Where(a => DeveloperLocalAppIds.Contains(a.AppId)));
        }
        else if (profileName == "Base")
        {
            // Base profile has no parent
            profile.InheritedFrom = new List<string>();
            profile.Applications = new ObservableCollection<ApplicationModel>(
                _allApplications.Where(a => BaseProfileAppIds.Contains(a.AppId)));
        }
        else
        {
            profile.InheritedFrom = new List<string>();
            profile.Applications = new ObservableCollection<ApplicationModel>();
        }

        return Task.FromResult(profile);
    }

    /// <summary>
    /// Returns the resolved profile with all inherited apps merged.
    /// </summary>
    public Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName)
    {
        var profile = new DeploymentProfileModel { Name = profileName };

        if (profileName == "Base")
        {
            // Base profile apps
            profile.Applications = new ObservableCollection<ApplicationModel>(
                _allApplications.Where(a => BaseProfileAppIds.Contains(a.AppId)));
        }
        else if (profileName == "Developer")
        {
            // Developer = Base apps + Developer local apps
            var allIds = BaseProfileAppIds.Concat(DeveloperLocalAppIds).ToHashSet();
            profile.Applications = new ObservableCollection<ApplicationModel>(
                _allApplications.Where(a => allIds.Contains(a.AppId)));
        }
        else
        {
            profile.Applications = new ObservableCollection<ApplicationModel>();
        }

        return Task.FromResult(profile);
    }

    public Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds)
    {
        // Capture the save request for verification
        LastSavedProfileName = profileName;
        LastSavedParentProfile = parentProfile;
        LastSavedAppIds = new List<string>(addedAppIds);
        return Task.CompletedTask;
    }

    public Task<SystemInfoModel> GetSystemInfoAsync() =>
        Task.FromResult(new SystemInfoModel { Hostname = "TEST-PC" });

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
}
