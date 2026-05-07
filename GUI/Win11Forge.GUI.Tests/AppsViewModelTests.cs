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
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for AppsViewModel - filtering, scanning, and installation logic.
/// </summary>
public class AppsViewModelTests
{
    /// <summary>
    /// Helper to get filtered applications as typed list from ICollectionView.
    /// </summary>
    private static List<ApplicationModel> GetFilteredApps(ICollectionView view)
        => view.Cast<ApplicationModel>().ToList();

    /// <summary>
    /// Helper to get filtered applications count from ICollectionView.
    /// </summary>
    private static int GetFilteredCount(ICollectionView view)
        => view.Cast<ApplicationModel>().Count();

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
    private static AppsViewModel CreateViewModel(
        MockPowerShellBridge? bridge = null,
        MockAppSettingsService? settings = null,
        MockDeploymentStateService? deploymentState = null,
        IAppScanCoordinator? scanCoordinator = null,
        IAppInstallationCoordinator? installationCoordinator = null,
        IAppUpdateCoordinator? updateCoordinator = null,
        IAppUninstallCoordinator? uninstallCoordinator = null,
        IPauseGate? pauseGate = null,
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null,
        IToastService? toastService = null)
    {
        var viewModel = new AppsViewModel(
            bridge ?? CreateMockBridge(),
            settings ?? CreateMockSettingsService(),
            deploymentState ?? CreateMockDeploymentStateService(),
            scanCoordinator ?? new TestAppScanCoordinator(),
            installationCoordinator ?? new TestAppInstallationCoordinator(),
            updateCoordinator ?? new TestAppUpdateCoordinator(),
            uninstallCoordinator ?? new TestAppUninstallCoordinator(),
            pauseGate ?? new TestPauseGate(),
            dialogService ?? new TestDialogService(),
            fileDialogService,
            toastService);

        WeakReferenceMessenger.Default.UnregisterAll(viewModel);
        return viewModel;
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
        var filteredApps = GetFilteredApps(viewModel.FilteredApplications);
        Assert.True(filteredApps.Count > 0,
            "Should find at least one application matching 'Visual'");
        Assert.All(filteredApps, app =>
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
        var filteredApps = GetFilteredApps(viewModel.FilteredApplications);
        Assert.True(filteredApps.Count > 0,
            "Should find at least one application in 'Development' category");
        Assert.All(filteredApps, app =>
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
        var filteredApps = GetFilteredApps(viewModel.FilteredApplications);
        Assert.All(filteredApps, app =>
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
        var originalCount = GetFilteredCount(viewModel.FilteredApplications);

        // Apply some filters
        viewModel.SearchText = "NonexistentApp12345";
        Assert.Empty(GetFilteredApps(viewModel.FilteredApplications));

        // Act
        viewModel.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Equal(originalCount, GetFilteredCount(viewModel.FilteredApplications));
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
        var lowerCount = GetFilteredCount(viewModel.FilteredApplications);

        viewModel.SearchText = "VISUAL";
        var upperCount = GetFilteredCount(viewModel.FilteredApplications);

        viewModel.SearchText = "ViSuAl";
        var mixedCount = GetFilteredCount(viewModel.FilteredApplications);

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
        Assert.Equal(GetFilteredCount(viewModel.FilteredApplications), viewModel.FilteredCount);
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
        var categoryCount = GetFilteredCount(viewModel.FilteredApplications);

        // Act - Set empty search
        viewModel.SearchText = "";

        // Assert - Should still show all apps in category
        Assert.Equal(categoryCount, GetFilteredCount(viewModel.FilteredApplications));
    }

    [Fact]
    public async Task ExportSelection_ShouldWriteSelectedApplicationIdsToChosenFile()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        fileDialogService.QueueSaveResult(filePath);
        var viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications);
        apps[0].IsSelected = true;
        apps[1].IsSelected = true;

        try
        {
            // Act
            await viewModel.ExportSelectionCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.SaveOptions);
            Assert.Equal("JSON files (*.json)|*.json", fileDialogService.SaveOptions[0].Filter);
            Assert.Equal(".json", fileDialogService.SaveOptions[0].DefaultExtension);
            Assert.Equal("win11forge-selection", fileDialogService.SaveOptions[0].DefaultFileName);

            var exportedIds = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(filePath));
            Assert.NotNull(exportedIds);
            Assert.Contains("Microsoft.VisualStudioCode", exportedIds);
            Assert.Contains("Microsoft.VisualStudio.2022.Community", exportedIds);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ImportSelection_ShouldApplySelectedApplicationIdsFromChosenFile()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "Git.Git" }));
        fileDialogService.QueueOpenResult(filePath);
        var viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();

        try
        {
            // Act
            await viewModel.ImportSelectionCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.OpenOptions);
            Assert.Equal("JSON files (*.json)|*.json", fileDialogService.OpenOptions[0].Filter);
            Assert.Equal(".json", fileDialogService.OpenOptions[0].DefaultExtension);

            var selectedApps = GetFilteredApps(viewModel.FilteredApplications)
                .Where(app => app.IsSelected)
                .ToList();
            Assert.Single(selectedApps);
            Assert.Equal("Git.Git", selectedApps[0].AppId);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExportFavorites_ShouldWriteFavoriteApplicationIdsToChosenFile()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        fileDialogService.QueueSaveResult(filePath);
        var viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        GetFilteredApps(viewModel.FilteredApplications)[0].IsFavorite = true;

        try
        {
            // Act
            await viewModel.ExportFavoritesCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.SaveOptions);
            Assert.Equal("win11forge-favorites", fileDialogService.SaveOptions[0].DefaultFileName);

            var exportedIds = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(filePath));
            Assert.NotNull(exportedIds);
            Assert.Single(exportedIds);
            Assert.Equal("Microsoft.VisualStudioCode", exportedIds[0]);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ImportFavorites_ShouldApplyFavoriteApplicationIdsFromChosenFile()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "Mozilla.Firefox" }));
        fileDialogService.QueueOpenResult(filePath);
        var viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();

        try
        {
            // Act
            await viewModel.ImportFavoritesCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.OpenOptions);
            var favoriteApps = GetFilteredApps(viewModel.FilteredApplications)
                .Where(app => app.IsFavorite)
                .ToList();
            Assert.Single(favoriteApps);
            Assert.Equal("Mozilla.Firefox", favoriteApps[0].AppId);
            Assert.Equal(1, viewModel.FavoritesCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task InstallSelected_ShouldDelegateToCoordinatorAndApplyResultCounters()
    {
        // Arrange
        var installationCoordinator = new TestAppInstallationCoordinator
        {
            Result = new AppInstallationResult(0, 2, 1, 1, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(installationCoordinator: installationCoordinator);
        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.InstallSelectedCommand.ExecuteAsync(null);

        // Assert
        var call = Assert.Single(installationCoordinator.Calls);
        var options = Assert.Single(installationCoordinator.Options);
        Assert.Equal(7, call.Count);
        Assert.True(options.ForceUpdate);
        Assert.Equal(2, viewModel.InstalledCount);
        Assert.Equal(3, viewModel.SuccessCount);
        Assert.Equal(1, viewModel.FailedCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.PartialSuccess, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task InstallApp_ShouldDelegateWithForceUpdateFalse()
    {
        // Arrange
        var installationCoordinator = new TestAppInstallationCoordinator
        {
            Result = new AppInstallationResult(0, 1, 0, 0, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(installationCoordinator: installationCoordinator);
        await viewModel.InitializeAsync();
        var app = GetFilteredApps(viewModel.FilteredApplications)[0];

        // Act
        await viewModel.InstallAppCommand.ExecuteAsync(app);

        // Assert
        var call = Assert.Single(installationCoordinator.Calls);
        var options = Assert.Single(installationCoordinator.Options);
        Assert.Same(app, Assert.Single(call));
        Assert.False(options.ForceUpdate);
    }

    [Fact]
    public async Task ScanUpdates_ShouldDelegateToCoordinatorAndApplyResultCount()
    {
        // Arrange
        var updateCoordinator = new TestAppUpdateCoordinator
        {
            ScanResult = new AppUpdateScanResult(0, 2, WasCancelled: false)
        };
        var viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.Installed;
        }

        viewModel.InstalledCount = 3;

        // Act
        await viewModel.ScanUpdatesCommand.ExecuteAsync(null);

        // Assert
        var call = Assert.Single(updateCoordinator.ScanCalls);
        Assert.Equal(3, call.Count);
        Assert.Equal(2, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsScanningUpdates);
    }

    [Fact]
    public async Task UpdateApp_ShouldDelegateSingleAppAndDecrementUpdateCount()
    {
        // Arrange
        var updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 1, 0, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        var app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.UpdateAvailable;
        viewModel.UpdatesAvailableCount = 1;

        // Act
        await viewModel.UpdateAppCommand.ExecuteAsync(app);

        // Assert
        var call = Assert.Single(updateCoordinator.UpdateCalls);
        Assert.Same(app, Assert.Single(call));
        Assert.Equal(0, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsInstalling);
    }

    [Fact]
    public async Task UpdateSelected_ShouldDelegateSelectedAppsAndApplyFilter()
    {
        // Arrange
        var updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 2, 0, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = false;
        }

        apps[0].IsSelected = true;
        apps[1].IsSelected = true;
        viewModel.UpdatesAvailableCount = 3;

        // Act
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Assert
        var call = Assert.Single(updateCoordinator.UpdateCalls);
        Assert.Equal([apps[0], apps[1]], call);
        Assert.Equal(1, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsInstalling);
    }

    [Fact]
    public async Task UninstallSelected_WhenCancelled_ShouldAskForConfirmationAndNotDelegate()
    {
        // Arrange
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var uninstallCoordinator = new TestAppUninstallCoordinator();
        var viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Empty(uninstallCoordinator.Calls);
        Assert.False(viewModel.IsUninstalling);
    }

    [Fact]
    public async Task UninstallSelected_ShouldDelegateAndApplyResultCounters()
    {
        // Arrange
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        var uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 2, 1, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.InstalledCount = 3;
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        // Assert
        var call = Assert.Single(uninstallCoordinator.Calls);
        Assert.Equal(3, call.Count);
        Assert.Equal(1, viewModel.InstalledCount);
        Assert.Equal(2, viewModel.SuccessCount);
        Assert.Equal(1, viewModel.FailedCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.PartialSuccess, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
        Assert.False(viewModel.IsUninstalling);
    }

    [Fact]
    public async Task UninstallSelected_WhenCoordinatorReportsCancelled_ShouldSetCancelledResult()
    {
        // Arrange
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        var uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 1, 0, 1, WasCancelled: true)
        };
        var viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        var apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.InstalledCount = 2;
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(uninstallCoordinator.Calls);
        Assert.Equal(1, viewModel.InstalledCount);
        Assert.Equal(1, viewModel.SuccessCount);
        Assert.Equal(0, viewModel.FailedCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.Cancelled, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UninstallApp_ShouldDelegateSingleAppAndDecrementInstalledCount()
    {
        // Arrange
        var uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 1, 0, 0, WasCancelled: false)
        };
        var viewModel = CreateViewModel(uninstallCoordinator: uninstallCoordinator);
        await viewModel.InitializeAsync();
        var app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.Installed;
        viewModel.InstalledCount = 1;

        // Act
        await viewModel.UninstallAppCommand.ExecuteAsync(app);

        // Assert
        var call = Assert.Single(uninstallCoordinator.Calls);
        Assert.Same(app, Assert.Single(call));
        Assert.Equal(0, viewModel.InstalledCount);
        Assert.False(viewModel.IsSummaryDialogOpen);
    }

    /// <summary>
    /// WF-002: Verifies that single-app install failure surfaces ErrorMessage on the banner,
    /// not just the per-row status.
    /// </summary>
    [Fact]
    public async Task InstallAppAsync_OnFailure_SurfacesErrorMessageBanner()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var failingInstaller = new TestAppInstallationCoordinator { ShouldThrow = true };
        var viewModel = CreateViewModel(bridge, installationCoordinator: failingInstaller);
        await viewModel.InitializeAsync();
        var app = bridge.Applications.First();

        // Act
        await viewModel.InstallAppCommand.ExecuteAsync(app);

        // Assert
        Assert.Equal(ApplicationStatus.Failed, app.Status);
        Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
        Assert.Contains(app.Name, viewModel.ErrorMessage!);
    }

    /// <summary>
    /// WF-002: Verifies that single-app uninstall failure surfaces ErrorMessage on the banner,
    /// not just the per-row status.
    /// </summary>
    [Fact]
    public async Task UninstallAppAsync_OnFailure_SurfacesErrorMessageBanner()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var failingUninstaller = new TestAppUninstallCoordinator { ShouldThrow = true };
        var viewModel = CreateViewModel(bridge, uninstallCoordinator: failingUninstaller);
        await viewModel.InitializeAsync();
        var app = bridge.Applications.First();

        // Act
        await viewModel.UninstallAppCommand.ExecuteAsync(app);

        // Assert
        Assert.Equal(ApplicationStatus.Failed, app.Status);
        Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
        Assert.Contains(app.Name, viewModel.ErrorMessage!);
    }

    /// <summary>
    /// WF-002: Verifies that single-app update failure surfaces ErrorMessage on the banner,
    /// not just the per-row status.
    /// </summary>
    [Fact]
    public async Task UpdateAppAsync_OnFailure_SurfacesErrorMessageBanner()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var failingUpdater = new TestAppUpdateCoordinator { ShouldThrow = true };
        var viewModel = CreateViewModel(bridge, updateCoordinator: failingUpdater);
        await viewModel.InitializeAsync();
        var app = bridge.Applications.First();

        // Act
        await viewModel.UpdateAppCommand.ExecuteAsync(app);

        // Assert
        Assert.Equal(ApplicationStatus.Failed, app.Status);
        Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
        Assert.Contains(app.Name, viewModel.ErrorMessage!);
    }

    /// <summary>
    /// WF-002: Verifies that CopyAppId failure surfaces a warning toast instead of failing silently.
    /// </summary>
    [Fact]
    public async Task CopyAppId_OnException_FiresWarningToast()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var toast = new TestToastService();
        var viewModel = CreateViewModel(bridge, toastService: toast);
        await viewModel.InitializeAsync();
        var app = bridge.Applications.First();

        // Act
        viewModel.CopyAppIdCommand.Execute(app);

        // Assert
        if (toast.Toasts.Count > 0)
        {
            var toastMessage = Assert.Single(toast.Toasts);
            Assert.Equal(ToastLevel.Warning, toastMessage.Level);
        }
    }

    /// <summary>
    /// WF-001: Verifies that Try Again after a failed install retries InstallSelectedAsync,
    /// not InitializeAsync.
    /// </summary>
    [Fact]
    public async Task RetryLastOperation_AfterFailedInstall_TriggersInstallSelected_NotInitialize()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var failingInstaller = new TestAppInstallationCoordinator { ShouldThrow = true };
        var viewModel = CreateViewModel(bridge, installationCoordinator: failingInstaller);
        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedCount();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.InstallSelectedCommand.ExecuteAsync(null));

        var initializeCallsBefore = bridge.GetAllApplicationsCallCount;
        var installCallsBefore = failingInstaller.InstallCallCount;
        failingInstaller.ShouldThrow = false;

        // Act
        await viewModel.RetryLastOperationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(initializeCallsBefore, bridge.GetAllApplicationsCallCount);
        Assert.Equal(installCallsBefore + 1, failingInstaller.InstallCallCount);
    }

    /// <summary>
    /// WF-001: Verifies that Try Again with no tracked operation falls back to InitializeAsync.
    /// </summary>
    [Fact]
    public async Task RetryLastOperation_NoTrackedOperation_FallsBackToInitialize()
    {
        // Arrange
        var bridge = CreateMockBridge();
        var viewModel = CreateViewModel(bridge);
        var initializeCallsBefore = bridge.GetAllApplicationsCallCount;

        // Act
        await viewModel.RetryLastOperationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(initializeCallsBefore + 1, bridge.GetAllApplicationsCallCount);
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

    public int GetAllApplicationsCallCount { get; private set; }

    public IReadOnlyList<ApplicationModel> Applications => _mockApplications;

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

    public Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        GetAllApplicationsCallCount++;
        return Task.FromResult(new List<ApplicationModel>(_mockApplications));
    }

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

    public Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null, CancellationToken cancellationToken = default) =>
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

    public Task<string> ExecuteScriptAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);
}

/// <summary>
/// Mock implementation of IDeploymentStateService for unit testing.
/// </summary>
internal class MockDeploymentStateService : IDeploymentStateService
{
    public bool IsDeploying => false;
    public bool IsPaused => false;
    public bool IsCancelled => false;
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
    public void ClearApplicationLogs() { }
    public void RequestPause() => PauseRequested?.Invoke(this, EventArgs.Empty);
    public void RequestResume() => ResumeRequested?.Invoke(this, EventArgs.Empty);
    public void RequestCancel() => CancelRequested?.Invoke(this, EventArgs.Empty);
    public void Dispose() { }

    // Suppress unused event warnings
    private void SuppressWarnings()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Test implementation of IAppScanCoordinator for AppsViewModel unit tests.
/// </summary>
internal sealed class TestAppScanCoordinator : IAppScanCoordinator
{
    public List<IReadOnlyCollection<ApplicationModel>> Calls { get; } = [];

    public AppScanResult Result { get; set; } = new(0, 0, 0, WasCancelled: false);

    public Task<AppScanResult> ScanAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(applications);

        var completed = 0;
        foreach (var app in applications)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, applications.Count, app));
        }

        return Task.FromResult(Result with { Total = applications.Count });
    }
}

/// <summary>
/// Test implementation of IAppInstallationCoordinator for AppsViewModel unit tests.
/// </summary>
internal sealed class TestAppInstallationCoordinator : IAppInstallationCoordinator
{
    public List<IReadOnlyCollection<ApplicationModel>> Calls { get; } = [];

    public List<AppInstallationOptions> Options { get; } = [];

    public AppInstallationResult Result { get; set; } = new(0, 0, 0, 0, 0, WasCancelled: false);

    public bool ShouldThrow { get; set; }

    public int InstallCallCount => Calls.Count;

    public Task<AppInstallationResult> InstallAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        AppInstallationOptions options,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(applications);
        Options.Add(options);

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Installation failed for test.");
        }

        var completed = 0;
        foreach (var app in applications)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, applications.Count, app));
        }

        return Task.FromResult(Result with { Total = applications.Count });
    }
}

/// <summary>
/// Test implementation of IAppUpdateCoordinator for AppsViewModel unit tests.
/// </summary>
internal sealed class TestAppUpdateCoordinator : IAppUpdateCoordinator
{
    public List<IReadOnlyCollection<ApplicationModel>> ScanCalls { get; } = [];

    public List<IReadOnlyCollection<ApplicationModel>> UpdateCalls { get; } = [];

    public AppUpdateScanResult ScanResult { get; set; } = new(0, 0, WasCancelled: false);

    public AppUpdateResult UpdateResult { get; set; } = new(0, 0, 0, 0, WasCancelled: false);

    public bool ShouldThrow { get; set; }

    public Task<AppUpdateScanResult> ScanForUpdatesAsync(
        IReadOnlyCollection<ApplicationModel> installedApps,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ScanCalls.Add(installedApps);

        var completed = 0;
        foreach (var app in installedApps)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, installedApps.Count, app));
        }

        return Task.FromResult(ScanResult with { Total = installedApps.Count });
    }

    public Task<AppUpdateResult> UpdateAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add(applications);

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Update failed for test.");
        }

        var completed = 0;
        foreach (var app in applications)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, applications.Count, app));
        }

        return Task.FromResult(UpdateResult with { Total = applications.Count });
    }
}

/// <summary>
/// Test implementation of IAppUninstallCoordinator for AppsViewModel unit tests.
/// </summary>
internal sealed class TestAppUninstallCoordinator : IAppUninstallCoordinator
{
    public List<IReadOnlyCollection<ApplicationModel>> Calls { get; } = [];

    public AppUninstallResult Result { get; set; } = new(0, 0, 0, 0, WasCancelled: false);

    public bool ShouldThrow { get; set; }

    public Task<AppUninstallResult> UninstallAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(applications);

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Uninstall failed for test.");
        }

        var completed = 0;
        foreach (var app in applications)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, applications.Count, app));
        }

        return Task.FromResult(Result with { Total = applications.Count });
    }
}

/// <summary>
/// Test implementation of IPauseGate for AppsViewModel unit tests.
/// </summary>
internal sealed class TestPauseGate : IPauseGate
{
    public int PauseCallCount { get; private set; }
    public int ResumeCallCount { get; private set; }

    public void Pause() => PauseCallCount++;

    public void Resume() => ResumeCallCount++;

    public void Wait(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
