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

using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ApplicationEditorViewModel - application editor dialog functionality.
/// </summary>
public class ApplicationEditorViewModelTests
{
    private static ApplicationEditorViewModel CreateViewModel(
        MockApplicationDatabaseService? dbService = null,
        MockPackageVerificationService? verificationService = null,
        MockPackageSearchService? searchService = null)
    {
        return new ApplicationEditorViewModel(
            dbService ?? new MockApplicationDatabaseService(),
            verificationService ?? new MockPackageVerificationService(),
            searchService ?? new MockPackageSearchService());
    }

    private static EditableApplicationModel CreateTestApplication()
    {
        return new EditableApplicationModel
        {
            AppId = "TestApp",
            Name = "Test Application",
            Category = "Development",
            Description = "A test application",
            DefaultPriority = 50,
            Sources = new ApplicationSourcesModel
            {
                Winget = "Test.Application",
                WingetConfig = new WingetSourceConfig(),
                ChocolateyConfig = new ChocolateySourceConfig(),
                DirectDownloadConfig = new DirectDownloadSourceConfig()
            }
        };
    }

    /// <summary>
    /// Verifies that initialization sets IsNewApplication correctly.
    /// </summary>
    [Fact]
    public async Task Initialize_NewApplication_ShouldSetIsNewTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = new EditableApplicationModel { Sources = new ApplicationSourcesModel() };

        // Act
        await viewModel.InitializeAsync(app, isNew: true);

        // Assert
        Assert.True(viewModel.IsNewApplication);
    }

    /// <summary>
    /// Verifies that initialization sets IsNewApplication correctly for edit mode.
    /// </summary>
    [Fact]
    public async Task Initialize_ExistingApplication_ShouldSetIsNewFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();

        // Act
        await viewModel.InitializeAsync(app, isNew: false);

        // Assert
        Assert.False(viewModel.IsNewApplication);
    }

    /// <summary>
    /// Verifies that CanEditAppId is true only for new applications.
    /// </summary>
    [Fact]
    public async Task CanEditAppId_ShouldBeTrueOnlyForNew()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();

        // Act - New app
        await viewModel.InitializeAsync(app.Clone(), isNew: true);
        var canEditNew = viewModel.CanEditAppId;

        // Act - Existing app
        await viewModel.InitializeAsync(app.Clone(), isNew: false);
        var canEditExisting = viewModel.CanEditAppId;

        // Assert
        Assert.True(canEditNew);
        Assert.False(canEditExisting);
    }

    /// <summary>
    /// Verifies that DialogTitle differs based on mode.
    /// </summary>
    [Fact]
    public async Task DialogTitle_ShouldDifferByMode()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();

        // Act - New app
        await viewModel.InitializeAsync(app.Clone(), isNew: true);
        var newTitle = viewModel.DialogTitle;

        // Act - Existing app
        await viewModel.InitializeAsync(app.Clone(), isNew: false);
        var editTitle = viewModel.DialogTitle;

        // Assert
        Assert.NotEqual(newTitle, editTitle);
    }

    /// <summary>
    /// Verifies that IsDirty is false after initialization.
    /// </summary>
    [Fact]
    public async Task Initialize_ShouldSetIsDirtyFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();

        // Act
        await viewModel.InitializeAsync(app, isNew: false);

        // Assert
        Assert.False(viewModel.IsDirty);
    }

    /// <summary>
    /// Verifies that changing Application properties sets IsDirty.
    /// </summary>
    [Fact]
    public async Task PropertyChange_ShouldSetIsDirty()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: false);

        // Act
        viewModel.Application.Name = "Changed Name";

        // Assert
        Assert.True(viewModel.IsDirty);
    }

    /// <summary>
    /// Verifies that Categories are loaded from database service.
    /// </summary>
    [Fact]
    public async Task Initialize_ShouldLoadCategories()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();

        // Act
        await viewModel.InitializeAsync(app, isNew: true);

        // Assert
        Assert.NotEmpty(viewModel.Categories);
        Assert.Contains("Development", viewModel.Categories);
    }

    /// <summary>
    /// Verifies that source enabled states are initialized correctly.
    /// </summary>
    [Fact]
    public async Task Initialize_ExistingApp_ShouldSetSourceEnabledBasedOnContent()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = new EditableApplicationModel
        {
            AppId = "Test",
            Name = "Test",
            Category = "Test",
            Sources = new ApplicationSourcesModel
            {
                Winget = "Test.Package",
                Chocolatey = null,
                Store = null,
                DirectUrl = null,
                WingetConfig = new WingetSourceConfig(),
                ChocolateyConfig = new ChocolateySourceConfig(),
                DirectDownloadConfig = new DirectDownloadSourceConfig()
            }
        };

        // Act
        await viewModel.InitializeAsync(app, isNew: false);

        // Assert
        Assert.True(viewModel.WingetEnabled);
        Assert.False(viewModel.ChocolateyEnabled);
        Assert.False(viewModel.StoreEnabled);
        Assert.False(viewModel.DirectDownloadEnabled);
    }

    /// <summary>
    /// Verifies that all sources are enabled by default for new apps.
    /// </summary>
    [Fact]
    public async Task Initialize_NewApp_ShouldEnableAllSources()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = new EditableApplicationModel
        {
            Sources = new ApplicationSourcesModel
            {
                WingetConfig = new WingetSourceConfig(),
                ChocolateyConfig = new ChocolateySourceConfig(),
                DirectDownloadConfig = new DirectDownloadSourceConfig()
            }
        };

        // Act
        await viewModel.InitializeAsync(app, isNew: true);

        // Assert
        Assert.True(viewModel.WingetEnabled);
        Assert.True(viewModel.ChocolateyEnabled);
        Assert.True(viewModel.StoreEnabled);
        Assert.True(viewModel.DirectDownloadEnabled);
    }

    /// <summary>
    /// Verifies that Cancel raises CloseRequested when not dirty.
    /// </summary>
    [Fact]
    public async Task Cancel_NotDirty_ShouldRaiseCloseRequested()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: false);

        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        // Act
        viewModel.CancelCommand.Execute(null);

        // Assert
        Assert.True(closeRequested);
        Assert.False(viewModel.DialogResult);
    }

    /// <summary>
    /// Verifies that Cancel prompts for confirmation when dirty.
    /// </summary>
    [Fact]
    public async Task Cancel_WhenDirty_ShouldRequestConfirmation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: false);

        // Make dirty
        viewModel.Application.Name = "Changed";

        ConfirmDiscardEventArgs? receivedArgs = null;
        viewModel.ConfirmDiscardRequested += (_, args) =>
        {
            receivedArgs = args;
            args.Discard = false; // Cancel the discard
        };

        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        // Act
        viewModel.CancelCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.False(closeRequested); // Should not close if discard was cancelled
    }

    /// <summary>
    /// Verifies that Save validates and saves the application.
    /// </summary>
    [Fact]
    public async Task Save_ValidApplication_ShouldSaveAndClose()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: true);

        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.True(closeRequested);
        Assert.True(viewModel.DialogResult);
    }

    /// <summary>
    /// Verifies that Save shows validation message on failure.
    /// </summary>
    [Fact]
    public async Task Save_InvalidApplication_ShouldShowValidationMessage()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        dbService.SetValidationResult(ApplicationValidationResult.Invalid("AppId", "ID is required"));
        var viewModel = CreateViewModel(dbService);

        var app = new EditableApplicationModel
        {
            AppId = "",
            Name = "Test",
            Sources = new ApplicationSourcesModel
            {
                WingetConfig = new WingetSourceConfig(),
                ChocolateyConfig = new ChocolateySourceConfig(),
                DirectDownloadConfig = new DirectDownloadSourceConfig()
            }
        };
        await viewModel.InitializeAsync(app, isNew: true);

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEmpty(viewModel.ValidationMessage);
        Assert.False(viewModel.DialogResult);
    }

    /// <summary>
    /// Verifies that VerifySources calls verification service.
    /// </summary>
    [Fact]
    public async Task VerifySources_ShouldCallVerificationService()
    {
        // Arrange
        var verificationService = new MockPackageVerificationService();
        var viewModel = CreateViewModel(verificationService: verificationService);
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: false);

        // Act
        await viewModel.VerifySourcesCommand.ExecuteAsync(null);

        // Assert
        Assert.True(viewModel.VerificationSuccess);
        Assert.NotEmpty(viewModel.VerificationResult);
    }

    /// <summary>
    /// Verifies that Cleanup removes event subscriptions.
    /// </summary>
    [Fact]
    public async Task Cleanup_ShouldNotThrow()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: false);

        // Act & Assert (should not throw)
        viewModel.Cleanup();
    }

    /// <summary>
    /// Verifies that selecting "Add New Category" triggers NewCategoryRequested event.
    /// </summary>
    [Fact]
    public async Task SelectAddNewCategory_ShouldRaiseNewCategoryRequested()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: true);

        NewCategoryEventArgs? receivedArgs = null;
        viewModel.NewCategoryRequested += (_, args) =>
        {
            receivedArgs = args;
            args.NewCategory = "NewTestCategory";
        };

        // Act
        viewModel.SelectedCategory = Resources.Resources.AppEditor_AddNewCategory;

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Contains("NewTestCategory", viewModel.Categories);
        Assert.Equal("NewTestCategory", viewModel.Application.Category);
    }

    /// <summary>
    /// Verifies that changing properties clears validation message.
    /// </summary>
    [Fact]
    public async Task PropertyChange_ShouldClearValidationMessage()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        dbService.SetValidationResult(ApplicationValidationResult.Invalid("Name", "Invalid"));
        var viewModel = CreateViewModel(dbService);
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: true);

        // Trigger validation error
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(viewModel.ValidationMessage);

        // Act - Change a property
        viewModel.Application.Description = "Changed description";

        // Assert
        Assert.Empty(viewModel.ValidationMessage);
    }

    /// <summary>
    /// Verifies that Winget package search populates results.
    /// </summary>
    [Fact]
    public async Task SearchWingetPackages_ShouldPopulateResults()
    {
        // Arrange
        var searchService = new MockPackageSearchService();
        searchService.SetWingetResults(
            new PackageSearchResult("Microsoft.VisualStudioCode", "Visual Studio Code", "1.100.0", PackageSource.Winget));

        var viewModel = CreateViewModel(searchService: searchService);
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: true);
        viewModel.WingetSearchQuery = "visual";

        // Act
        await viewModel.SearchWingetPackagesCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(viewModel.WingetSearchResults);
        Assert.Equal("Microsoft.VisualStudioCode", viewModel.WingetSearchResults[0].PackageId);
        Assert.Equal("visual", searchService.LastWingetQuery);
    }

    /// <summary>
    /// Verifies that applying a Winget search result updates source and metadata.
    /// </summary>
    [Fact]
    public async Task ApplyWingetSearchResult_ShouldPopulateSourceAndMetadata()
    {
        // Arrange
        var searchService = new MockPackageSearchService();
        searchService.SetWingetResults(
            new PackageSearchResult("Google.Chrome", "Google Chrome", "123.0.0", PackageSource.Winget));

        var viewModel = CreateViewModel(searchService: searchService);
        var app = new EditableApplicationModel
        {
            AppId = string.Empty,
            Name = string.Empty,
            Category = "Browser",
            Sources = new ApplicationSourcesModel
            {
                WingetConfig = new WingetSourceConfig(),
                ChocolateyConfig = new ChocolateySourceConfig(),
                DirectDownloadConfig = new DirectDownloadSourceConfig()
            }
        };

        await viewModel.InitializeAsync(app, isNew: true);
        viewModel.WingetSearchQuery = "chrome";
        await viewModel.SearchWingetPackagesCommand.ExecuteAsync(null);

        // Act
        viewModel.ApplyWingetSearchResultCommand.Execute(null);

        // Assert
        Assert.Equal("Google.Chrome", viewModel.Application.Sources.Winget);
        Assert.True(viewModel.WingetEnabled);
        Assert.Equal("Google Chrome", viewModel.Application.Name);
        Assert.Equal("Google.Chrome", viewModel.Application.AppId);
    }

    /// <summary>
    /// Verifies that Store package search uses store search service and returns results.
    /// </summary>
    [Fact]
    public async Task SearchStorePackages_ShouldPopulateResults()
    {
        // Arrange
        var searchService = new MockPackageSearchService();
        searchService.SetStoreResults(
            new PackageSearchResult("9WZDNCRFJBMP", "Spotify Music", null, PackageSource.Store));

        var viewModel = CreateViewModel(searchService: searchService);
        var app = CreateTestApplication();
        await viewModel.InitializeAsync(app, isNew: true);
        viewModel.StoreSearchQuery = "spotify";

        // Act
        await viewModel.SearchStorePackagesCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(viewModel.StoreSearchResults);
        Assert.Equal("9WZDNCRFJBMP", viewModel.StoreSearchResults[0].PackageId);
        Assert.Equal("spotify", searchService.LastStoreQuery);
    }
}

