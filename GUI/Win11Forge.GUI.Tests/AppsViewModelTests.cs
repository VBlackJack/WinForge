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
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.Services.PowerShell;
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

    private static ApplicationModel FindApp(AppsViewModel viewModel, string appId)
    {
        return GetFilteredApps(viewModel.FilteredApplications).Single(app => app.AppId == appId);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int i = 0; i < 50; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private static void ClearSelection(AppsViewModel viewModel)
    {
        foreach (ApplicationModel app in GetFilteredApps(viewModel.FilteredApplications))
        {
            app.IsSelected = false;
        }

        viewModel.UpdateSelectedCount();
    }

    private static string Loc(string key)
    {
        return Resources.Resources.ResourceManager.GetString(key, Resources.Resources.Culture) ?? key;
    }

    private static string FormatLoc(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Loc(key), args);
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
        IToastService? toastService = null,
        IRepositoryPathService? pathService = null)
    {
        AppsViewModel viewModel = new AppsViewModel(
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
            toastService,
            pathService);

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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search for "Visual"
        viewModel.SearchText = "Visual";

        // Assert
        List<ApplicationModel> filteredApps = GetFilteredApps(viewModel.FilteredApplications);
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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Select "Development" category
        viewModel.SelectedCategory = "Development";

        // Assert
        List<ApplicationModel> filteredApps = GetFilteredApps(viewModel.FilteredApplications);
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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search for "Code" in "Development" category
        viewModel.SelectedCategory = "Development";
        viewModel.SearchText = "Code";

        // Assert
        List<ApplicationModel> filteredApps = GetFilteredApps(viewModel.FilteredApplications);
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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();
        int originalCount = GetFilteredCount(viewModel.FilteredApplications);

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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);

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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Act - Search with different cases
        viewModel.SearchText = "visual";
        int lowerCount = GetFilteredCount(viewModel.FilteredApplications);

        viewModel.SearchText = "VISUAL";
        int upperCount = GetFilteredCount(viewModel.FilteredApplications);

        viewModel.SearchText = "ViSuAl";
        int mixedCount = GetFilteredCount(viewModel.FilteredApplications);

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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();
        int totalCount = viewModel.FilteredCount;

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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        // Select a category first
        viewModel.SelectedCategory = "Development";
        int categoryCount = GetFilteredCount(viewModel.FilteredApplications);

        // Act - Set empty search
        viewModel.SearchText = "";

        // Assert - Should still show all apps in category
        Assert.Equal(categoryCount, GetFilteredCount(viewModel.FilteredApplications));
    }

    [Fact]
    public async Task SelectWithUpdates_ShouldOnlyMutateFilteredApplications()
    {
        // Arrange
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        ApplicationModel visibleUpdate = bridge.Applications.Single(app => app.AppId == "Git.Git");
        ApplicationModel hiddenUpdate = bridge.Applications.Single(app => app.AppId == "Mozilla.Firefox");
        visibleUpdate.Status = ApplicationStatus.UpdateAvailable;
        hiddenUpdate.Status = ApplicationStatus.UpdateAvailable;
        hiddenUpdate.IsSelected = false;
        viewModel.SelectedCategory = "Development";

        // Act
        viewModel.SelectWithUpdatesCommand.Execute(null);

        // Assert
        Assert.True(visibleUpdate.IsSelected);
        Assert.False(hiddenUpdate.IsSelected);
    }

    [Fact]
    public async Task SelectNotInstalled_ShouldOnlyMutateFilteredApplications()
    {
        // Arrange
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        ApplicationModel hiddenNotInstalled = bridge.Applications.Single(app => app.AppId == "Mozilla.Firefox");
        hiddenNotInstalled.Status = ApplicationStatus.Pending;
        hiddenNotInstalled.IsSelected = false;
        viewModel.SelectedCategory = "Development";

        // Act
        viewModel.SelectNotInstalledCommand.Execute(null);

        // Assert
        Assert.All(GetFilteredApps(viewModel.FilteredApplications), app => Assert.True(app.IsSelected));
        Assert.False(hiddenNotInstalled.IsSelected);
    }

    [Fact]
    public async Task SelectFavorites_ShouldOnlyMutateFilteredApplications()
    {
        // Arrange
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        await viewModel.InitializeAsync();

        ApplicationModel visibleFavorite = bridge.Applications.Single(app => app.AppId == "Git.Git");
        ApplicationModel hiddenFavorite = bridge.Applications.Single(app => app.AppId == "Mozilla.Firefox");
        visibleFavorite.IsFavorite = true;
        hiddenFavorite.IsFavorite = true;
        hiddenFavorite.IsSelected = false;
        viewModel.SelectedCategory = "Development";

        // Act
        viewModel.SelectFavoritesCommand.Execute(null);

        // Assert
        Assert.True(visibleFavorite.IsSelected);
        Assert.False(hiddenFavorite.IsSelected);
    }

    [Fact]
    public async Task ExportSelection_ShouldWriteSelectedApplicationIdsToChosenFile()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = tempDirectory.GetFilePath("selection.json");
        fileDialogService.QueueSaveResult(filePath);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications);
        apps[0].IsSelected = true;
        apps[1].IsSelected = true;

        // Act
        await viewModel.ExportSelectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Equal("JSON files (*.json)|*.json", fileDialogService.SaveOptions[0].Filter);
        Assert.Equal(".json", fileDialogService.SaveOptions[0].DefaultExtension);
        Assert.Equal("win11forge-selection", fileDialogService.SaveOptions[0].DefaultFileName);
        Assert.True(File.Exists(filePath), $"Expected selection export file at {filePath}.");

        List<string>? exportedIds = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(filePath));
        Assert.NotNull(exportedIds);
        Assert.Contains("Microsoft.VisualStudioCode", exportedIds);
        Assert.Contains("Microsoft.VisualStudio.2022.Community", exportedIds);
    }

    [Fact]
    public async Task ImportSelection_ShouldApplySelectedApplicationIdsFromChosenFile()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = tempDirectory.GetFilePath("selection.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "Git.Git" }));
        fileDialogService.QueueOpenResult(filePath);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        // Act
        await viewModel.ImportSelectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(fileDialogService.OpenOptions);
        Assert.Equal("JSON files (*.json)|*.json", fileDialogService.OpenOptions[0].Filter);
        Assert.Equal(".json", fileDialogService.OpenOptions[0].DefaultExtension);

        List<ApplicationModel> selectedApps = GetFilteredApps(viewModel.FilteredApplications)
            .Where(app => app.IsSelected)
            .ToList();
        Assert.Single(selectedApps);
        Assert.Equal("Git.Git", selectedApps[0].AppId);
    }

    [Fact]
    public async Task ExportFavorites_ShouldWriteFavoriteApplicationIdsToChosenFile()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = tempDirectory.GetFilePath("favorites.json");
        fileDialogService.QueueSaveResult(filePath);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        GetFilteredApps(viewModel.FilteredApplications)[0].IsFavorite = true;

        // Act
        await viewModel.ExportFavoritesCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Equal("win11forge-favorites", fileDialogService.SaveOptions[0].DefaultFileName);
        Assert.True(File.Exists(filePath), $"Expected favorites export file at {filePath}.");

        List<string>? exportedIds = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(filePath));
        Assert.NotNull(exportedIds);
        Assert.Single(exportedIds);
        Assert.Equal("Microsoft.VisualStudioCode", exportedIds[0]);
    }

    [Fact]
    public async Task ImportFavorites_ShouldApplyFavoriteApplicationIdsFromChosenFile()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = tempDirectory.GetFilePath("favorites.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "Mozilla.Firefox" }));
        fileDialogService.QueueOpenResult(filePath);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService);
        await viewModel.InitializeAsync();
        foreach (ApplicationModel app in GetFilteredApps(viewModel.FilteredApplications))
        {
            app.IsFavorite = false;
        }
        viewModel.FavoritesCount = 0;

        // Act
        await viewModel.ImportFavoritesCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(fileDialogService.OpenOptions);
        List<ApplicationModel> favoriteApps = GetFilteredApps(viewModel.FilteredApplications)
            .Where(app => app.IsFavorite)
            .ToList();
        Assert.Single(favoriteApps);
        Assert.Equal("Mozilla.Firefox", favoriteApps[0].AppId);
        Assert.Equal(1, viewModel.FavoritesCount);
    }

    [Fact]
    public async Task ImportSelection_WithExistingSelectionAndReplace_ShouldClearCurrentAndSummarizeMissing()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        TestDialogService dialogService = new TestDialogService();
        string filePath = tempDirectory.GetFilePath("selection.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "git.git", "Missing.App", " " }));
        fileDialogService.QueueOpenResult(filePath);
        dialogService.QueueYesNoCancelResult(true);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService, dialogService: dialogService);
        await viewModel.InitializeAsync();
        FindApp(viewModel, "Mozilla.Firefox").IsSelected = true;
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.ImportSelectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        (string Title, string Message, string? YesText, string? NoText, string? CancelText) request = Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(Loc("Apps_Import_Replace"), request.YesText);
        Assert.Equal(Loc("Apps_Import_Merge"), request.NoText);
        Assert.Equal(Resources.Resources.Common_Cancel, request.CancelText);
        Assert.True(FindApp(viewModel, "Git.Git").IsSelected);
        Assert.False(FindApp(viewModel, "Mozilla.Firefox").IsSelected);
        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Equal(FormatLoc("Apps_ImportSelection_Status", 1, 1, 1), viewModel.StatusMessage);
    }

    [Fact]
    public async Task ImportFavorites_WithExistingFavoritesAndMerge_ShouldPreserveCurrentAndAddMatches()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        TestDialogService dialogService = new TestDialogService();
        string filePath = tempDirectory.GetFilePath("favorites.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "mozilla.firefox", "Mozilla.Firefox", "Missing.App" }));
        fileDialogService.QueueOpenResult(filePath);
        dialogService.QueueYesNoCancelResult(false);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService, dialogService: dialogService);
        await viewModel.InitializeAsync();
        FindApp(viewModel, "Google.Chrome").IsFavorite = true;
        viewModel.FavoritesCount = 1;

        // Act
        await viewModel.ImportFavoritesCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.True(FindApp(viewModel, "Google.Chrome").IsFavorite);
        Assert.True(FindApp(viewModel, "Mozilla.Firefox").IsFavorite);
        Assert.Equal(2, viewModel.FavoritesCount);
        Assert.Equal(FormatLoc("Apps_ImportFavorites_Status", 1, 1, 2), viewModel.StatusMessage);
    }

    [Fact]
    public async Task ImportSelection_WhenConflictCancelled_ShouldLeaveCurrentSelectionUntouched()
    {
        // Arrange
        using TestTemporaryDirectory tempDirectory = new TestTemporaryDirectory();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        TestDialogService dialogService = new TestDialogService();
        string filePath = tempDirectory.GetFilePath("selection.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { "Mozilla.Firefox" }));
        fileDialogService.QueueOpenResult(filePath);
        dialogService.QueueYesNoCancelResult(null);
        AppsViewModel viewModel = CreateViewModel(fileDialogService: fileDialogService, dialogService: dialogService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);
        FindApp(viewModel, "Git.Git").IsSelected = true;
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.ImportSelectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.True(FindApp(viewModel, "Git.Git").IsSelected);
        Assert.False(FindApp(viewModel, "Mozilla.Firefox").IsSelected);
        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task InstallSelected_ShouldDelegateToCoordinatorAndApplyResultCounters()
    {
        // Arrange
        TestAppInstallationCoordinator installationCoordinator = new TestAppInstallationCoordinator
        {
            Result = new AppInstallationResult(0, 2, 1, 1, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(installationCoordinator: installationCoordinator);
        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.InstallSelectedCommand.ExecuteAsync(null);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(installationCoordinator.Calls);
        AppInstallationOptions options = Assert.Single(installationCoordinator.Options);
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
        TestAppInstallationCoordinator installationCoordinator = new TestAppInstallationCoordinator
        {
            Result = new AppInstallationResult(0, 1, 0, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(installationCoordinator: installationCoordinator);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];

        // Act
        await viewModel.InstallAppCommand.ExecuteAsync(app);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(installationCoordinator.Calls);
        AppInstallationOptions options = Assert.Single(installationCoordinator.Options);
        Assert.Same(app, Assert.Single(call));
        Assert.False(options.ForceUpdate);
    }

    [Fact]
    public async Task ScanUpdates_ShouldDelegateToCoordinatorAndApplyResultCount()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            ScanResult = new AppUpdateScanResult(0, 2, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.Installed;
        }

        viewModel.InstalledCount = 3;

        // Act
        await viewModel.ScanUpdatesCommand.ExecuteAsync(null);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(updateCoordinator.ScanCalls);
        Assert.Equal(3, call.Count);
        Assert.Equal(2, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsScanningUpdates);
    }

    [Fact]
    public async Task UpdateApp_ShouldDelegateSingleAppAndDecrementUpdateCount()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 1, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.UpdateAvailable;
        viewModel.UpdatesAvailableCount = 1;

        // Act
        await viewModel.UpdateAppCommand.ExecuteAsync(app);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(updateCoordinator.UpdateCalls);
        Assert.Same(app, Assert.Single(call));
        Assert.Equal(0, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsInstalling);
    }

    [Fact]
    public async Task UpdateSelected_ShouldDelegateSelectedAppsAndApplyFilter()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 2, 0, 0, WasCancelled: false)
        };
        MockDeploymentStateService deploymentState = CreateMockDeploymentStateService();
        AppsViewModel viewModel = CreateViewModel(
            deploymentState: deploymentState,
            updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (ApplicationModel? app in apps)
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
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(updateCoordinator.UpdateCalls);
        Assert.Equal([apps[0], apps[1]], call);
        Assert.Equal(1, viewModel.UpdatesAvailableCount);
        Assert.False(viewModel.IsInstalling);
        Assert.True(viewModel.IsSummaryDialogOpen);
        Assert.Equal(2, viewModel.BatchProgressTotal);
        Assert.Equal(2, viewModel.BatchProgressCurrent);
        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.SuccessCount);
        Assert.Equal(0, viewModel.FailedCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.Success, viewModel.LastDeploymentResult);
        Assert.True(updateCoordinator.LastCancellationToken.CanBeCanceled);
        Assert.Equal(1, deploymentState.StartDeploymentCallCount);
        Assert.Equal(1, deploymentState.EndDeploymentCallCount);
        Assert.NotEmpty(deploymentState.ProgressUpdates);
        Assert.NotEmpty(deploymentState.TimeUpdates);
    }

    [Fact]
    public async Task UpdateSelected_ShouldIgnoreQueuedProgressReportsAfterFinalProgress()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 2, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = true;
        }

        viewModel.UpdatesAvailableCount = 2;
        QueuedSynchronizationContext progressContext = new QueuedSynchronizationContext();
        SynchronizationContext? previousContext = SynchronizationContext.Current;

        // Act
        Task updateTask;
        SynchronizationContext.SetSynchronizationContext(progressContext);
        try
        {
            updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        await updateTask;

        // Assert
        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);
        Assert.Equal(2, progressContext.PendingCount);

        progressContext.RunOne();

        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);

        progressContext.RunAll();

        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);
    }

    [Fact]
    public async Task UpdateSelected_WhileRunning_ShouldHidePauseResumeControls()
    {
        // Arrange
        TaskCompletionSource<bool> updateBlocker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateBlocker = updateBlocker,
            UpdateResult = new AppUpdateResult(0, 1, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.UpdateAvailable;
        app.IsSelected = true;
        viewModel.UpdatesAvailableCount = 1;

        // Act
        Task updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        try
        {
            await WaitForConditionAsync(() => viewModel.IsUpdating);

            // Assert
            Assert.True(viewModel.IsInstalling);
            Assert.True(viewModel.IsUpdating);
            Assert.False(viewModel.ShowPauseButton);
            Assert.False(viewModel.ShowResumeButton);
            Assert.False(viewModel.PauseCommand.CanExecute(null));
            Assert.False(viewModel.ResumeCommand.CanExecute(null));
        }
        finally
        {
            updateBlocker.TrySetResult(true);
        }

        await updateTask;
        Assert.False(viewModel.IsUpdating);
        Assert.False(viewModel.IsInstalling);
        Assert.False(viewModel.IsPaused);
    }

    [Fact]
    public async Task CancelBatch_WhenDeclined_ShouldNotRequestCancellation()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        TestPauseGate pauseGate = new TestPauseGate();
        AppsViewModel viewModel = CreateViewModel(dialogService: dialogService, pauseGate: pauseGate);
        viewModel.BatchProgressCurrent = 2;
        viewModel.BatchProgressTotal = 7;
        viewModel.IsPaused = true;

        // Act
        await viewModel.CancelBatchCommand.ExecuteAsync(null);

        // Assert
        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Contains("2", request.Message, StringComparison.Ordinal);
        Assert.Contains("7", request.Message, StringComparison.Ordinal);
        Assert.True(viewModel.IsPaused);
        Assert.Equal(0, pauseGate.ResumeCallCount);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task CancelBatch_WhenConfirmed_ShouldSetStatusAndResumePausedBatch()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestPauseGate pauseGate = new TestPauseGate();
        AppsViewModel viewModel = CreateViewModel(dialogService: dialogService, pauseGate: pauseGate);
        viewModel.BatchProgressCurrent = 3;
        viewModel.BatchProgressTotal = 8;
        viewModel.IsPaused = true;

        // Act
        await viewModel.CancelBatchCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc("Cancel_InProgress"), viewModel.StatusMessage);
        Assert.False(viewModel.IsPaused);
        Assert.Equal(1, pauseGate.ResumeCallCount);
    }

    [Fact]
    public async Task UpdateSelected_WhenNothingSelected_ShouldUpdateAllAvailableUpdates()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 2, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = false;
        }
        apps[2].Status = ApplicationStatus.Installed;
        viewModel.UpdatesAvailableCount = 2;

        // Act
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(updateCoordinator.UpdateCalls);
        Assert.Equal([apps[0], apps[1]], call);
        Assert.Equal(0, viewModel.UpdatesAvailableCount);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UpdateSelected_WhenPartialFailure_ShouldShowPartialSummary()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 1, 1, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = true;
        }
        viewModel.UpdatesAvailableCount = 2;

        // Act
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.UpdatesAvailableCount);
        Assert.Equal(1, viewModel.SuccessCount);
        Assert.Equal(1, viewModel.FailedCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.PartialSuccess, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UpdateSelected_WhenCancelled_ShouldShowCancelledSummary()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 1, 0, 1, WasCancelled: true)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = true;
        }
        viewModel.UpdatesAvailableCount = 2;

        // Act
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.UpdatesAvailableCount);
        Assert.Equal(1, viewModel.SuccessCount);
        Assert.Equal(0, viewModel.FailedCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.Cancelled, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UpdateSelected_WhenAllFail_ShouldShowFailedSummary()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator
        {
            UpdateResult = new AppUpdateResult(0, 0, 2, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.UpdateAvailable;
            app.IsSelected = true;
        }
        viewModel.UpdatesAvailableCount = 2;

        // Act
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, viewModel.UpdatesAvailableCount);
        Assert.Equal(0, viewModel.SuccessCount);
        Assert.Equal(2, viewModel.FailedCount);
        Assert.Equal(0, viewModel.SkippedCount);
        Assert.Equal(DeploymentResult.Failed, viewModel.LastDeploymentResult);
        Assert.True(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UpdateSelected_WhenCoordinatorThrows_ShouldCleanupDeploymentState()
    {
        // Arrange
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator { ShouldThrow = true };
        MockDeploymentStateService deploymentState = CreateMockDeploymentStateService();
        AppsViewModel viewModel = CreateViewModel(
            deploymentState: deploymentState,
            updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.UpdateAvailable;
        app.IsSelected = true;
        viewModel.UpdatesAvailableCount = 1;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.UpdateSelectedCommand.ExecuteAsync(null));

        // Assert
        Assert.False(viewModel.IsInstalling);
        Assert.False(viewModel.IsPaused);
        Assert.Equal(1, deploymentState.StartDeploymentCallCount);
        Assert.Equal(1, deploymentState.EndDeploymentCallCount);
    }

    [Fact]
    public async Task UninstallSelected_WhenCancelled_ShouldAskForConfirmationAndNotDelegate()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator();
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel? app in apps)
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
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 2, 1, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(3).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.InstalledCount = 3;
        viewModel.UpdateSelectedCount();

        // Act
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(uninstallCoordinator.Calls);
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
    public async Task UninstallSelected_ShouldIgnoreQueuedProgressReportsAfterFinalProgress()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 2, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.InstalledCount = 2;
        viewModel.UpdateSelectedCount();
        QueuedSynchronizationContext progressContext = new QueuedSynchronizationContext();
        SynchronizationContext? previousContext = SynchronizationContext.Current;

        // Act
        Task uninstallTask;
        SynchronizationContext.SetSynchronizationContext(progressContext);
        try
        {
            uninstallTask = viewModel.UninstallSelectedCommand.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        await uninstallTask;

        // Assert
        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);
        Assert.Equal(2, progressContext.PendingCount);

        progressContext.RunOne();

        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);

        progressContext.RunAll();

        Assert.Equal(100, viewModel.BatchProgressPercent);
        Assert.Equal(2, viewModel.BatchProgressCurrent);
    }

    [Fact]
    public async Task UninstallSelected_WhenCoordinatorReportsCancelled_ShouldSetCancelledResult()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 1, 0, 1, WasCancelled: true)
        };
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = GetFilteredApps(viewModel.FilteredApplications).Take(2).ToList();
        foreach (ApplicationModel? app in apps)
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
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator
        {
            Result = new AppUninstallResult(0, 1, 0, 0, WasCancelled: false)
        };
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.Installed;
        viewModel.InstalledCount = 1;

        // Act
        await viewModel.UninstallAppCommand.ExecuteAsync(app);

        // Assert
        (string Title, string Message, string? ConfirmText, string? CancelText) confirmation = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Resources.Resources.Confirm_Uninstall_Btn, confirmation.ConfirmText);
        Assert.Equal(Resources.Resources.Common_Cancel, confirmation.CancelText);
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(uninstallCoordinator.Calls);
        Assert.Same(app, Assert.Single(call));
        Assert.Equal(0, viewModel.InstalledCount);
        Assert.False(viewModel.IsSummaryDialogOpen);
    }

    [Fact]
    public async Task UninstallApp_WhenCancelled_ShouldAskForConfirmationAndNotDelegate()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator();
        AppsViewModel viewModel = CreateViewModel(
            uninstallCoordinator: uninstallCoordinator,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        ApplicationModel app = GetFilteredApps(viewModel.FilteredApplications)[0];
        app.Status = ApplicationStatus.Installed;
        viewModel.InstalledCount = 1;

        // Act
        await viewModel.UninstallAppCommand.ExecuteAsync(app);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Empty(uninstallCoordinator.Calls);
        Assert.Equal(1, viewModel.InstalledCount);
    }

    /// <summary>
    /// WF-002: Verifies that single-app install failure surfaces ErrorMessage on the banner,
    /// not just the per-row status.
    /// </summary>
    [Fact]
    public async Task InstallAppAsync_OnFailure_SurfacesErrorMessageBanner()
    {
        // Arrange
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator failingInstaller = new TestAppInstallationCoordinator { ShouldThrow = true };
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: failingInstaller);
        await viewModel.InitializeAsync();
        ApplicationModel app = bridge.Applications.First();

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
        MockPowerShellBridge bridge = CreateMockBridge();
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        TestAppUninstallCoordinator failingUninstaller = new TestAppUninstallCoordinator { ShouldThrow = true };
        AppsViewModel viewModel = CreateViewModel(
            bridge,
            uninstallCoordinator: failingUninstaller,
            dialogService: dialogService);
        await viewModel.InitializeAsync();
        ApplicationModel app = bridge.Applications.First();

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
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppUpdateCoordinator failingUpdater = new TestAppUpdateCoordinator { ShouldThrow = true };
        AppsViewModel viewModel = CreateViewModel(bridge, updateCoordinator: failingUpdater);
        await viewModel.InitializeAsync();
        ApplicationModel app = bridge.Applications.First();

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
        MockPowerShellBridge bridge = CreateMockBridge();
        TestToastService toast = new TestToastService();
        AppsViewModel viewModel = CreateViewModel(bridge, toastService: toast);
        await viewModel.InitializeAsync();
        ApplicationModel app = bridge.Applications.First();

        // Act
        viewModel.CopyAppIdCommand.Execute(app);

        // Assert
        if (toast.Toasts.Count > 0)
        {
            (string Message, ToastLevel Level) toastMessage = Assert.Single(toast.Toasts);
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
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator failingInstaller = new TestAppInstallationCoordinator { ShouldThrow = true };
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: failingInstaller);
        await viewModel.InitializeAsync();
        viewModel.UpdateSelectedCount();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.InstallSelectedCommand.ExecuteAsync(null));

        int initializeCallsBefore = bridge.GetAllApplicationsCallCount;
        int installCallsBefore = failingInstaller.InstallCallCount;
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
        MockPowerShellBridge bridge = CreateMockBridge();
        AppsViewModel viewModel = CreateViewModel(bridge);
        int initializeCallsBefore = bridge.GetAllApplicationsCallCount;

        // Act
        await viewModel.RetryLastOperationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(initializeCallsBefore + 1, bridge.GetAllApplicationsCallCount);
    }

    [Fact]
    public async Task ProfileSelector_WhenNoProfileSelected_ShowsCustomEntry()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        AppsViewModel viewModel = CreateViewModel(bridge, pathService: profiles.PathService);

        // Act
        await viewModel.InitializeAsync();

        // Assert
        Assert.Null(viewModel.SelectedProfile);
        Assert.NotNull(viewModel.SelectedProfileSelectorItem);
        Assert.Same(viewModel.ProfileSelectorItems[0], viewModel.SelectedProfileSelectorItem);
        Assert.True(viewModel.SelectedProfileSelectorItem.IsCustom);
        Assert.Equal(Resources.Resources.Apps_CustomProfile, viewModel.SelectedProfileSelectorItem.DisplayName);
        Assert.Equal(
            [Resources.Resources.Apps_CustomProfile, "Work"],
            viewModel.ProfileSelectorItems.Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public async Task ProfileSelector_SelectingCustomEntry_ClearsSelectedProfile()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        AppsViewModel viewModel = CreateViewModel(bridge, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        // Act
        viewModel.SelectedProfileSelectorItem = viewModel.ProfileSelectorItems.Single(item => item.ProfileName == "Work");
        await WaitForConditionAsync(() => viewModel.SelectedProfile == "Work");

        viewModel.SelectedProfileSelectorItem = viewModel.ProfileSelectorItems.Single(item => item.IsCustom);
        await WaitForConditionAsync(() => viewModel.SelectedProfile is null);

        // Assert
        Assert.Null(viewModel.SelectedProfile);
        Assert.NotNull(viewModel.SelectedProfileSelectorItem);
        Assert.True(viewModel.SelectedProfileSelectorItem.IsCustom);
        Assert.False(viewModel.HasProfileApplied);
    }

    [Fact]
    public async Task ProfileSelection_WithManualSelectionAndCancel_RestoresManualSelection()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueYesNoCancelResult(null);
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);
        ApplicationModel manualApp = FindApp(viewModel, "Mozilla.Firefox");
        manualApp.IsSelected = true;
        viewModel.UpdateSelectedCount();

        // Act
        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => dialogService.YesNoCancelRequests.Count == 1 && viewModel.SelectedProfile is null);

        // Assert
        Assert.True(manualApp.IsSelected);
        Assert.False(FindApp(viewModel, "Git.Git").IsSelected);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_WithManualSelectionAndReplace_ReplacesManualSelection()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueYesNoCancelResult(true);
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);
        ApplicationModel manualApp = FindApp(viewModel, "Mozilla.Firefox");
        manualApp.IsSelected = true;
        viewModel.UpdateSelectedCount();

        // Act
        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected && !manualApp.IsSelected);

        // Assert
        (string Title, string Message, string? YesText, string? NoText, string? CancelText) request = Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(
            string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Resources.Resources.Profile_Apply_Message_HasManualSelection,
                1,
                "Work"),
            request.Message);
        Assert.Equal(Resources.Resources.Profile_Apply_Replace, request.YesText);
        Assert.Equal(Resources.Resources.Profile_Apply_Merge, request.NoText);
        Assert.Equal(Resources.Resources.Common_Cancel, request.CancelText);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_WithManualSelectionAndMerge_PreservesManualSelection()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueYesNoCancelResult(false);
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);
        ApplicationModel manualApp = FindApp(viewModel, "Mozilla.Firefox");
        manualApp.IsSelected = true;
        viewModel.UpdateSelectedCount();

        // Act
        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected && manualApp.IsSelected);

        // Assert
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(2, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_WithoutManualSelection_AppliesSilently()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work"];
        TestDialogService dialogService = new TestDialogService();
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        // Act
        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected);

        // Assert
        Assert.Empty(dialogService.YesNoCancelRequests);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_FromAppliedProfileWithoutManualChanges_AppliesSilently()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]),
            ("Gaming", [], ["Google.Chrome"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work", "Gaming"];
        TestDialogService dialogService = new TestDialogService();
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected);
        dialogService.YesNoCancelRequests.Clear();

        // Act
        viewModel.SelectedProfile = "Gaming";
        await WaitForConditionAsync(() =>
            FindApp(viewModel, "Google.Chrome").IsSelected &&
            !FindApp(viewModel, "Git.Git").IsSelected);

        // Assert
        Assert.Empty(dialogService.YesNoCancelRequests);
        Assert.Equal("Gaming", viewModel.SelectedProfile);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_FromAppliedProfileWithManualChangeAndCancel_RestoresPreviousProfile()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]),
            ("Gaming", [], ["Google.Chrome"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work", "Gaming"];
        TestDialogService dialogService = new TestDialogService();
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected);
        ApplicationModel manualApp = FindApp(viewModel, "Mozilla.Firefox");
        manualApp.IsSelected = true;
        viewModel.UpdateSelectedCount();

        dialogService.QueueYesNoCancelResult(null);

        // Act
        viewModel.SelectedProfile = "Gaming";
        await WaitForConditionAsync(() =>
            dialogService.YesNoCancelRequests.Count == 1 &&
            viewModel.SelectedProfile == "Work");

        // Assert
        (string Title, string Message, string? YesText, string? NoText, string? CancelText) request = Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(Resources.Resources.Profile_Apply_Replace, request.YesText);
        Assert.Equal(Resources.Resources.Profile_Apply_Merge, request.NoText);
        Assert.Equal(Resources.Resources.Common_Cancel, request.CancelText);
        Assert.True(FindApp(viewModel, "Git.Git").IsSelected);
        Assert.True(manualApp.IsSelected);
        Assert.False(FindApp(viewModel, "Google.Chrome").IsSelected);
        Assert.Equal(2, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileSelection_FromAppliedProfileWithManualChangeAndReplace_ReplacesSelection()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Work", [], ["Git.Git"]),
            ("Gaming", [], ["Google.Chrome"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Work", "Gaming"];
        TestDialogService dialogService = new TestDialogService();
        AppsViewModel viewModel = CreateViewModel(bridge, dialogService: dialogService, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        viewModel.SelectedProfile = "Work";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected);
        ApplicationModel manualApp = FindApp(viewModel, "Mozilla.Firefox");
        manualApp.IsSelected = true;
        viewModel.UpdateSelectedCount();

        dialogService.QueueYesNoCancelResult(true);

        // Act
        viewModel.SelectedProfile = "Gaming";
        await WaitForConditionAsync(() =>
            FindApp(viewModel, "Google.Chrome").IsSelected &&
            !FindApp(viewModel, "Git.Git").IsSelected &&
            !manualApp.IsSelected);

        // Assert
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal("Gaming", viewModel.SelectedProfile);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task ProfileTierMapping_UsesCustomInheritanceChain()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Base", [], ["Git.Git"]),
            ("Enterprise", ["Base"], ["Microsoft.VisualStudioCode"]),
            ("Developer", ["Enterprise"], ["Mozilla.Firefox"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Base", "Enterprise", "Developer"];
        AppsViewModel viewModel = CreateViewModel(bridge, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        // Act
        viewModel.SelectedProfile = "Developer";
        await WaitForConditionAsync(() => FindApp(viewModel, "Mozilla.Firefox").IsSelected);

        // Assert
        Assert.Equal("Base", FindApp(viewModel, "Git.Git").ProfileTier);
        Assert.Equal("Enterprise", FindApp(viewModel, "Microsoft.VisualStudioCode").ProfileTier);
        Assert.Equal("Developer", FindApp(viewModel, "Mozilla.Firefox").ProfileTier);
    }

    [Fact]
    public async Task ProfileTierMapping_MostSpecificProfileWinsForDuplicateApps()
    {
        // Arrange
        using TestProfilesDirectory profiles = new TestProfilesDirectory(
            ("Base", [], ["Git.Git"]),
            ("Enterprise", ["Base"], ["Git.Git"]));
        MockPowerShellBridge bridge = CreateMockBridge();
        bridge.AvailableProfiles = ["Base", "Enterprise"];
        AppsViewModel viewModel = CreateViewModel(bridge, pathService: profiles.PathService);
        await viewModel.InitializeAsync();
        ClearSelection(viewModel);

        // Act
        viewModel.SelectedProfile = "Enterprise";
        await WaitForConditionAsync(() => FindApp(viewModel, "Git.Git").IsSelected);

        // Assert
        Assert.Equal("Enterprise", FindApp(viewModel, "Git.Git").ProfileTier);
    }

    /// <summary>
    /// Verifies that resuming an interrupted Install batch only re-runs the
    /// applications that were not yet recorded as completed in the checkpoint.
    /// This is the core safety contract of the resume feature.
    /// </summary>
    [Fact]
    public async Task ResumeBatchAsync_OnInstallKind_ShouldOnlyRunCoordinatorOnRemainingApps()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator installCoordinator = new TestAppInstallationCoordinator();
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: installCoordinator);
        await viewModel.InitializeAsync();

        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Install,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: ["Microsoft.VisualStudioCode", "Git.Git", "Mozilla.Firefox"],
            Completed: [new BatchCompletedItem("Microsoft.VisualStudioCode", BatchItemOutcome.Installed, DateTimeOffset.UtcNow)],
            Options: new BatchOptions(ForceUpdate: false));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Single(installCoordinator.Calls);
        string[] resumed = installCoordinator.Calls[0].Select(a => a.AppId).ToArray();
        Assert.Equal(new[] { "Git.Git", "Mozilla.Firefox" }, resumed);
        Assert.DoesNotContain("Microsoft.VisualStudioCode", resumed);
    }

    /// <summary>
    /// Mirror of the Install filtering test for the Update path: also ensures
    /// already-completed AppIds are not re-run on resume. Guards against a
    /// future refactor that would diverge the Update branch from the shared
    /// Plan \ Completed filter.
    /// </summary>
    [Fact]
    public async Task ResumeBatchAsync_OnUpdateKind_ShouldOnlyRunCoordinatorOnRemainingApps()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppUpdateCoordinator updateCoordinator = new TestAppUpdateCoordinator();
        AppsViewModel viewModel = CreateViewModel(bridge, updateCoordinator: updateCoordinator);
        await viewModel.InitializeAsync();

        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Update,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: ["Microsoft.VisualStudioCode", "Git.Git", "Mozilla.Firefox"],
            Completed: [new BatchCompletedItem("Microsoft.VisualStudioCode", BatchItemOutcome.Updated, DateTimeOffset.UtcNow)],
            Options: new BatchOptions(ForceUpdate: false));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Single(updateCoordinator.UpdateCalls);
        string[] resumed = updateCoordinator.UpdateCalls[0].Select(a => a.AppId).ToArray();
        Assert.Equal(new[] { "Git.Git", "Mozilla.Firefox" }, resumed);
        Assert.DoesNotContain("Microsoft.VisualStudioCode", resumed);
    }

    [Fact]
    public async Task ResumeBatchAsync_OnUninstallKind_ShouldRouteToUninstallCoordinator()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator installCoordinator = new TestAppInstallationCoordinator();
        TestAppUninstallCoordinator uninstallCoordinator = new TestAppUninstallCoordinator();
        AppsViewModel viewModel = CreateViewModel(
            bridge,
            installationCoordinator: installCoordinator,
            uninstallCoordinator: uninstallCoordinator);
        await viewModel.InitializeAsync();

        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Uninstall,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: ["Git.Git"],
            Completed: [],
            Options: new BatchOptions(ForceUpdate: false));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Empty(installCoordinator.Calls);
        Assert.Single(uninstallCoordinator.Calls);
        Assert.Equal("Git.Git", uninstallCoordinator.Calls[0].Single().AppId);
    }

    [Fact]
    public async Task ResumeBatchAsync_WithAllItemsCompleted_ShouldNotInvokeAnyCoordinator()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator installCoordinator = new TestAppInstallationCoordinator();
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: installCoordinator);
        await viewModel.InitializeAsync();

        string[] planIds = new[] { "Git.Git", "Mozilla.Firefox" };
        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Install,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: planIds,
            Completed: planIds
                .Select(id => new BatchCompletedItem(id, BatchItemOutcome.Installed, DateTimeOffset.UtcNow))
                .ToArray(),
            Options: new BatchOptions(ForceUpdate: false));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Empty(installCoordinator.Calls);
    }

    [Fact]
    public async Task ResumeBatchAsync_WithUnknownAppIdsInCatalog_ShouldSkipMissingAndProceed()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator installCoordinator = new TestAppInstallationCoordinator();
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: installCoordinator);
        await viewModel.InitializeAsync();

        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Install,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: ["Git.Git", "Some.AppRemovedFromCatalog", "Mozilla.Firefox"],
            Completed: [],
            Options: new BatchOptions(ForceUpdate: false));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Single(installCoordinator.Calls);
        string[] resumed = installCoordinator.Calls[0].Select(a => a.AppId).ToArray();
        Assert.Equal(new[] { "Git.Git", "Mozilla.Firefox" }, resumed);
    }

    [Fact]
    public async Task ResumeBatchAsync_ShouldPropagateForceUpdateFromCheckpoint()
    {
        MockPowerShellBridge bridge = CreateMockBridge();
        TestAppInstallationCoordinator installCoordinator = new TestAppInstallationCoordinator();
        AppsViewModel viewModel = CreateViewModel(bridge, installationCoordinator: installCoordinator);
        await viewModel.InitializeAsync();

        BatchCheckpoint checkpoint = new BatchCheckpoint(
            SchemaVersion: BatchCheckpoint.CurrentSchemaVersion,
            BatchId: Guid.NewGuid(),
            OperationKind: BatchOperationKind.Install,
            State: BatchState.InProgress,
            StartedAt: DateTimeOffset.UtcNow,
            LastCheckpointAt: DateTimeOffset.UtcNow,
            Plan: ["Git.Git"],
            Completed: [],
            Options: new BatchOptions(ForceUpdate: true));

        await viewModel.ResumeBatchAsync(checkpoint);

        Assert.Single(installCoordinator.Options);
        Assert.True(installCoordinator.Options[0].ForceUpdate);
    }

    private sealed class TestTemporaryDirectory : IDisposable
    {
        private const int DeleteRetryCount = 5;
        private const int DeleteRetryDelayMilliseconds = 50;

        public TestTemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "Win11Forge.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string GetFilePath(string fileName)
        {
            return Path.Combine(DirectoryPath, fileName);
        }

        public void Dispose()
        {
            DeleteDirectoryWithRetry(DirectoryPath);
        }

        private static void DeleteDirectoryWithRetry(string directoryPath)
        {
            for (int attempt = 1; attempt <= DeleteRetryCount; attempt++)
            {
                try
                {
                    if (Directory.Exists(directoryPath))
                    {
                        Directory.Delete(directoryPath, recursive: true);
                    }

                    return;
                }
                catch (IOException) when (attempt < DeleteRetryCount)
                {
                    System.Threading.Thread.Sleep(DeleteRetryDelayMilliseconds);
                }
                catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount)
                {
                    System.Threading.Thread.Sleep(DeleteRetryDelayMilliseconds);
                }
            }
        }
    }

    private sealed class TestProfilesDirectory : IDisposable
    {
        private readonly string _profilesPath;
        private readonly string _rootPath;

        public TestProfilesDirectory(params (string Name, string[] Inherits, string[] Applications)[] profiles)
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "Win11Forge.Tests",
                Guid.NewGuid().ToString("N"));
            PathService = new RepositoryPathService(
                _rootPath,
                [Path.Combine(_rootPath, "UserData")]);
            _profilesPath = PathService.LegacyInstallProfilesDirectory;

            Directory.CreateDirectory(_profilesPath);
            foreach ((string Name, string[] Inherits, string[] Applications) profile in profiles)
            {
                var payload = new {
                    profile.Name,
                    Description = $"{profile.Name} test profile",
                    Version = "1.0.0",
                    Inherits = profile.Inherits,
                    Applications = profile.Applications
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(
                    Path.Combine(_profilesPath, $"{profile.Name}{Win11ForgePathNames.JsonFileExtension}"),
                    json);
            }
        }

        public IRepositoryPathService PathService { get; }

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
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

    public string RepositoryRoot { get; set; } = @"C:\Test\Win11Forge";

    public List<string> AvailableProfiles { get; set; } = ["Base", "Developer", "Gaming"];

    public int GetAllApplicationsCallCount { get; private set; }

    public IReadOnlyList<ApplicationModel> Applications => _mockApplications;

    public Task<string> GetWin11ForgeVersionAsync() => Task.FromResult("3.0.0");

    public Task<List<string>> GetAvailableProfilesAsync() =>
        Task.FromResult(new List<string>(AvailableProfiles));

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
    public bool IsDeploying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsCancelled { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? CurrentAppName { get; private set; }
    public int CompletedCount { get; private set; }
    public int TotalCount { get; private set; }
    public double ProgressPercentage => TotalCount > 0 ? CompletedCount * 100.0 / TotalCount : 0;
    public string? ElapsedTime { get; private set; }
    public string? EstimatedTimeRemaining { get; private set; }
    public ObservableCollection<ApplicationModel> Applications { get; } = new();
    public int StartDeploymentCallCount { get; private set; }
    public int EndDeploymentCallCount { get; private set; }
    public List<IReadOnlyList<ApplicationModel>> StartedDeployments { get; } = [];
    public List<(string? CurrentAppName, int Completed, int Total, string? StatusMessage)> ProgressUpdates { get; } = [];
    public List<(string? Elapsed, string? Remaining)> TimeUpdates { get; } = [];

    public event EventHandler? StateChanged;
    public event EventHandler? PauseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? CancelRequested;

    public void StartDeployment(IEnumerable<ApplicationModel> apps)
    {
        List<ApplicationModel> appList = apps.ToList();
        StartDeploymentCallCount++;
        StartedDeployments.Add(appList);
        IsDeploying = true;
        IsCancelled = false;
        CurrentAppName = null;
        CompletedCount = 0;
        TotalCount = appList.Count;
        Applications.Clear();
        foreach (ApplicationModel? app in appList)
        {
            Applications.Add(app);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage)
    {
        CurrentAppName = currentAppName;
        CompletedCount = completed;
        TotalCount = total;
        StatusMessage = statusMessage;
        ProgressUpdates.Add((currentAppName, completed, total, statusMessage));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTime(string? elapsed, string? remaining)
    {
        ElapsedTime = elapsed;
        EstimatedTimeRemaining = remaining;
        TimeUpdates.Add((elapsed, remaining));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndDeployment()
    {
        EndDeploymentCallCount++;
        IsDeploying = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearApplicationLogs()
    {
        foreach (ApplicationModel app in Applications)
        {
            app.LogOutput = string.Empty;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
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

internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = [];

    public int PendingCount => _callbacks.Count;

    public override void Post(SendOrPostCallback d, object? state)
    {
        _callbacks.Enqueue((d, state));
    }

    public void RunOne()
    {
        (SendOrPostCallback callback, object? state) = _callbacks.Dequeue();
        SynchronizationContext? previousContext = Current;
        SetSynchronizationContext(this);
        try
        {
            callback(state);
        }
        finally
        {
            SetSynchronizationContext(previousContext);
        }
    }

    public void RunAll()
    {
        while (_callbacks.Count > 0)
        {
            RunOne();
        }
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

        int completed = 0;
        foreach (ApplicationModel app in applications)
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

        int completed = 0;
        foreach (ApplicationModel app in applications)
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

    public TaskCompletionSource<bool>? UpdateBlocker { get; set; }

    public bool ShouldThrow { get; set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<AppUpdateScanResult> ScanForUpdatesAsync(
        IReadOnlyCollection<ApplicationModel> installedApps,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ScanCalls.Add(installedApps);

        int completed = 0;
        foreach (ApplicationModel app in installedApps)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, installedApps.Count, app));
        }

        return Task.FromResult(ScanResult with { Total = installedApps.Count });
    }

    public async Task<AppUpdateResult> UpdateAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add(applications);
        LastCancellationToken = cancellationToken;

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Update failed for test.");
        }

        if (UpdateBlocker is not null)
        {
            await UpdateBlocker.Task.WaitAsync(cancellationToken);
        }

        int completed = 0;
        foreach (ApplicationModel app in applications)
        {
            completed++;
            progress?.Report(new AppOperationProgress(completed, applications.Count, app));
        }

        return UpdateResult with { Total = applications.Count };
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

        int completed = 0;
        foreach (ApplicationModel app in applications)
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
