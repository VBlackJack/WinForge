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

using System.Globalization;
using System.Windows;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for AppCatalogViewModel - App Catalog management functionality.
/// </summary>
public class AppCatalogViewModelTests
{
    private static List<EditableApplicationModel> CreateMockApplications()
    {
        return new List<EditableApplicationModel>
        {
            new()
            {
                AppId = "VSCode",
                Name = "Visual Studio Code",
                Category = "Development",
                Description = "Code editor",
                DefaultPriority = 10,
                Sources = new ApplicationSourcesModel { Winget = "Microsoft.VisualStudioCode" },
                Tags = new List<string> { "editor", "ide" }
            },
            new()
            {
                AppId = "Chrome",
                Name = "Google Chrome",
                Category = "Browsers",
                Description = "Web browser by Google",
                DefaultPriority = 20,
                Sources = new ApplicationSourcesModel { Winget = "Google.Chrome" },
                Tags = new List<string> { "browser", "web" }
            },
            new()
            {
                AppId = "7Zip",
                Name = "7-Zip",
                Category = "Utilities",
                Description = "File archiver",
                DefaultPriority = 30,
                Sources = new ApplicationSourcesModel { Winget = "7zip.7zip", Chocolatey = "7zip" },
                Tags = new List<string> { "archive", "compression" }
            },
            new()
            {
                AppId = "Git",
                Name = "Git",
                Category = "Development",
                Description = "Version control system",
                DefaultPriority = 5,
                Sources = new ApplicationSourcesModel { Winget = "Git.Git" },
                Tags = new List<string> { "vcs", "git" }
            },
            new()
            {
                AppId = "Firefox",
                Name = "Mozilla Firefox",
                Category = "Browsers",
                Description = "Web browser by Mozilla",
                DefaultPriority = 25,
                Sources = new ApplicationSourcesModel { Winget = "Mozilla.Firefox" },
                Tags = new List<string> { "browser", "web" }
            }
        };
    }

    private static EditableApplicationModel CreateEditableApplication(string appId, string name)
    {
        return new EditableApplicationModel
        {
            AppId = appId,
            Name = name,
            Category = "Test",
            DefaultPriority = 50,
            Sources = new ApplicationSourcesModel()
        };
    }

    private static AppCatalogViewModel CreateViewModel(
        MockApplicationDatabaseService? dbService = null,
        MockUndoService? undoService = null,
        MockPackageVerificationService? verificationService = null,
        IApplicationEditorDialogService? applicationEditorDialogService = null,
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null)
    {
        return new AppCatalogViewModel(
            dbService ?? new MockApplicationDatabaseService(),
            undoService ?? new MockUndoService(),
            verificationService ?? new MockPackageVerificationService(),
            applicationEditorDialogService ?? new TestApplicationEditorDialogService(),
            dialogService ?? new TestDialogService(),
            fileDialogService ?? new TestFileDialogService());
    }

    /// <summary>
    /// Verifies that loading applications populates the collection.
    /// </summary>
    [Fact]
    public async Task LoadApplications_ShouldPopulateCollection()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var viewModel = CreateViewModel(dbService);

        // Act
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(5, viewModel.Applications.Count);
        Assert.Equal(5, viewModel.TotalCount);
    }

    /// <summary>
    /// Verifies that categories are loaded correctly.
    /// </summary>
    [Fact]
    public async Task LoadApplications_ShouldLoadCategories()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(Resources.Resources.Apps_CategoryAll, viewModel.Categories);
        Assert.Contains("Development", viewModel.Categories);
        Assert.Contains("Browsers", viewModel.Categories);
        Assert.Contains("Utilities", viewModel.Categories);
    }

    /// <summary>
    /// Verifies that Add command opens the editor and saves the returned application.
    /// </summary>
    [Fact]
    public async Task Add_ShouldOpenEditorAndSaveReturnedApplication()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var undoService = new MockUndoService();
        var editorDialogService = new TestApplicationEditorDialogService();
        editorDialogService.QueueAddResult(CreateEditableApplication("NewApp", "New Application"));
        var viewModel = CreateViewModel(
            dbService: dbService,
            undoService: undoService,
            applicationEditorDialogService: editorDialogService);

        // Act
        await viewModel.AddCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, editorDialogService.ShowAddCallCount);
        Assert.Equal("NewApp", dbService.LastSavedApplication?.AppId);
        Assert.True(dbService.LastSaveWasNew);
        Assert.NotNull(undoService.LastRecordedAction);
    }

    /// <summary>
    /// Verifies that Add command does not save when the editor is cancelled.
    /// </summary>
    [Fact]
    public async Task Add_WhenEditorCancelled_ShouldNotSave()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var editorDialogService = new TestApplicationEditorDialogService();
        editorDialogService.QueueAddResult(null);
        var viewModel = CreateViewModel(
            dbService: dbService,
            applicationEditorDialogService: editorDialogService);

        // Act
        await viewModel.AddCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, editorDialogService.ShowAddCallCount);
        Assert.Null(dbService.LastSavedApplication);
    }

    /// <summary>
    /// Verifies that Edit command opens the editor with the selected application and saves the result.
    /// </summary>
    [Fact]
    public async Task Edit_ShouldOpenEditorAndSaveReturnedApplication()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var undoService = new MockUndoService();
        var editorDialogService = new TestApplicationEditorDialogService();
        editorDialogService.QueueEditResult(CreateEditableApplication("VSCode", "Edited Visual Studio Code"));
        var viewModel = CreateViewModel(
            dbService: dbService,
            undoService: undoService,
            applicationEditorDialogService: editorDialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        // Act
        await viewModel.EditCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, editorDialogService.ShowEditCallCount);
        Assert.Equal("VSCode", editorDialogService.EditRequests[0].AppId);
        Assert.Equal("Edited Visual Studio Code", dbService.LastSavedApplication?.Name);
        Assert.False(dbService.LastSaveWasNew);
        Assert.NotNull(undoService.LastRecordedAction);
    }

    /// <summary>
    /// Verifies that Edit command is disabled when no application is selected.
    /// </summary>
    [Fact]
    public async Task Edit_ShouldBeDisabledWhenNoSelection()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = null;

        // Act & Assert
        Assert.False(viewModel.EditCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that Delete command asks for confirmation and does not delete when cancelled.
    /// </summary>
    [Fact]
    public async Task Delete_WhenNotConfirmed_ShouldNotDelete()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = CreateViewModel(dbService: dbService, dialogService: dialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(0, dbService.DeleteCallCount);
        Assert.NotNull(viewModel.SelectedApplication);
    }

    /// <summary>
    /// Verifies that Delete command deletes the selected application when confirmed.
    /// </summary>
    [Fact]
    public async Task Delete_WhenConfirmed_ShouldDeleteSelectedApplication()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        var viewModel = CreateViewModel(dbService: dbService, dialogService: dialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(1, dbService.DeleteCallCount);
        Assert.Null(viewModel.SelectedApplication);
        Assert.Equal(4, viewModel.Applications.Count);
    }

    /// <summary>
    /// Verifies that Duplicate command opens the add editor with a cloned application and saves the returned application.
    /// </summary>
    [Fact]
    public async Task Duplicate_ShouldOpenEditorAndSaveReturnedApplicationAsNew()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var editorDialogService = new TestApplicationEditorDialogService();
        editorDialogService.QueueAddResult(CreateEditableApplication("VSCode-Copy", "Visual Studio Code Copy"));
        var viewModel = CreateViewModel(
            dbService: dbService,
            applicationEditorDialogService: editorDialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        var original = viewModel.Applications[0];
        original.Sources.Chocolatey = "vscode";
        original.Sources.DirectUrl = "https://example.com/vscode.exe";
        original.Sources.WingetConfig = new WingetSourceConfig
        {
            Version = "1.2.3",
            Source = "winget",
            AdditionalArgs = "--scope machine"
        };
        original.Sources.ChocolateyConfig = new ChocolateySourceConfig
        {
            Version = "1.2.3",
            AdditionalArgs = "--params global"
        };
        original.Sources.DirectDownloadConfig = new DirectDownloadSourceConfig
        {
            InstallerType = "exe",
            SilentArgs = "/S",
            FileName = "vscode.exe"
        };
        original.Detection = new ApplicationDetectionModel
        {
            Method = "Registry",
            Path = @"HKLM\Software\VSCode",
            VersionKey = "Version",
            MinVersion = "1.0.0"
        };
        viewModel.SelectedApplication = original;

        // Act
        await viewModel.DuplicateCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, editorDialogService.ShowAddCallCount);
        var duplicate = Assert.Single(editorDialogService.AddRequests);
        Assert.NotNull(duplicate);
        Assert.NotSame(original, duplicate);
        Assert.StartsWith($"{original.AppId}-copy-", duplicate.AppId);
        Assert.EndsWith(" (Copy)", duplicate.Name);
        Assert.Equal(original.Category, duplicate.Category);
        Assert.Equal(original.Description, duplicate.Description);
        Assert.Equal(original.Sources.Winget, duplicate.Sources.Winget);
        Assert.NotSame(original.Sources, duplicate.Sources);
        Assert.NotSame(original.Sources.WingetConfig, duplicate.Sources.WingetConfig);
        Assert.NotSame(original.Sources.ChocolateyConfig, duplicate.Sources.ChocolateyConfig);
        Assert.NotSame(original.Sources.DirectDownloadConfig, duplicate.Sources.DirectDownloadConfig);
        Assert.NotSame(original.Detection, duplicate.Detection);
        Assert.Equal(original.Sources.WingetConfig.Version, duplicate.Sources.WingetConfig?.Version);
        Assert.Equal(original.Sources.ChocolateyConfig.AdditionalArgs, duplicate.Sources.ChocolateyConfig?.AdditionalArgs);
        Assert.Equal(original.Sources.DirectDownloadConfig.SilentArgs, duplicate.Sources.DirectDownloadConfig?.SilentArgs);
        Assert.Equal(original.Detection.Path, duplicate.Detection?.Path);

        duplicate.Sources.WingetConfig!.Version = "modified-by-clone";
        Assert.NotEqual(duplicate.Sources.WingetConfig.Version, original.Sources.WingetConfig.Version);

        Assert.Equal("VSCode-Copy", dbService.LastSavedApplication?.AppId);
        Assert.True(dbService.LastSaveWasNew);
    }

    /// <summary>
    /// Verifies that Duplicate command is disabled without a selected application.
    /// </summary>
    [Fact]
    public async Task Duplicate_WithoutSelection_ShouldBeDisabled()
    {
        // Arrange
        var editorDialogService = new TestApplicationEditorDialogService();
        var viewModel = CreateViewModel(applicationEditorDialogService: editorDialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = null;

        // Act & Assert
        Assert.False(viewModel.DuplicateCommand.CanExecute(null));
        Assert.Equal(0, editorDialogService.ShowAddCallCount);
    }

    /// <summary>
    /// Verifies that ClearFilters resets search and category.
    /// </summary>
    [Fact]
    public async Task ClearFilters_ShouldResetFilters()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SearchText = "test";
        viewModel.SelectedCategory = "Development";

        // Act
        viewModel.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Empty(viewModel.SearchText);
        Assert.Equal(Resources.Resources.Apps_CategoryAll, viewModel.SelectedCategory);
    }

    /// <summary>
    /// Verifies that the empty state is hidden while the database is loading.
    /// </summary>
    [Fact]
    public void EmptyState_DuringLoad_NotVisible()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var converter = new Win11Forge.GUI.Resources.AndZeroToVisibilityConverter();

        // Act
        var result = converter.Convert(
            new object[] { viewModel.IsLoading, viewModel.HasLoadError, viewModel.FilteredCount },
            typeof(Visibility),
            null!,
            CultureInfo.InvariantCulture);

        // Assert
        Assert.True(viewModel.IsLoading);
        Assert.Equal(Visibility.Collapsed, result);
    }

    /// <summary>
    /// Verifies that load failures use the error state instead of the empty state.
    /// </summary>
    [Fact]
    public async Task EmptyState_AfterLoadFailure_NotVisible_ErrorVisible()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        dbService.SetLoadException(new InvalidOperationException("catalog unavailable"));
        var viewModel = CreateViewModel(dbService: dbService);
        var converter = new Win11Forge.GUI.Resources.AndZeroToVisibilityConverter();

        // Act
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        var emptyStateVisibility = converter.Convert(
            new object[] { viewModel.IsLoading, viewModel.HasLoadError, viewModel.FilteredCount },
            typeof(Visibility),
            null!,
            CultureInfo.InvariantCulture);

        // Assert
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.HasLoadError);
        Assert.Contains("catalog unavailable", viewModel.LoadErrorMessage);
        Assert.Equal(viewModel.LoadErrorMessage, viewModel.StatusMessage);
        Assert.Equal(Visibility.Collapsed, emptyStateVisibility);
    }

    /// <summary>
    /// Verifies that an empty database gets a distinct empty-state message.
    /// </summary>
    [Fact]
    public async Task EmptyState_WhenDatabaseIsEmpty_UsesDatabaseMessage()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        dbService.ReplaceApplications();
        var viewModel = CreateViewModel(dbService: dbService);

        // Act
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);

        // Assert
        var expected = Loc.ResourceManager.GetString("AppCatalog_EmptyDatabase", Loc.Culture);
        Assert.Equal(0, viewModel.TotalCount);
        Assert.Equal(expected, viewModel.EmptyStateMessage);
    }

    /// <summary>
    /// Verifies that filtered-empty results get their own empty-state message.
    /// </summary>
    [Fact]
    public void EmptyState_WhenLoadedApplicationsAreFilteredOut_UsesFilterMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.TotalCount = 5;

        // Assert
        var expected = Loc.ResourceManager.GetString("AppCatalog_EmptyFilter", Loc.Culture);
        Assert.Equal(expected, viewModel.EmptyStateMessage);
    }

    /// <summary>
    /// Verifies that Undo command executes UndoAsync on service.
    /// </summary>
    [Fact]
    public async Task Undo_ShouldCallUndoService()
    {
        // Arrange
        var undoService = new MockUndoService();
        undoService.SetCanUndo(true);
        var viewModel = CreateViewModel(undoService: undoService);

        // Act
        await viewModel.UndoCommand.ExecuteAsync(null);

        // Assert
        Assert.True(undoService.UndoWasCalled);
    }

    /// <summary>
    /// Verifies that Redo command executes RedoAsync on service.
    /// </summary>
    [Fact]
    public async Task Redo_ShouldCallUndoService()
    {
        // Arrange
        var undoService = new MockUndoService();
        undoService.SetCanRedo(true);
        var viewModel = CreateViewModel(undoService: undoService);

        // Act
        await viewModel.RedoCommand.ExecuteAsync(null);

        // Assert
        Assert.True(undoService.RedoWasCalled);
    }

    /// <summary>
    /// Verifies that ExportAll opens a save dialog and exports all IDs.
    /// </summary>
    [Fact]
    public async Task ExportAll_ShouldShowSaveDialogAndExportAllIds()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueSaveResult(@"C:\Exports\applications.json");
        var viewModel = CreateViewModel(dbService: dbService, fileDialogService: fileDialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);

        // Act
        await viewModel.ExportAllCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Equal(5, dbService.LastExportAppIds?.Count);
        Assert.Equal(@"C:\Exports\applications.json", dbService.LastExportFilePath);
    }

    /// <summary>
    /// Verifies that ExportAll displays an information dialog when there are no applications.
    /// </summary>
    [Fact]
    public async Task ExportAll_WhenNoApplications_ShouldShowInfoAndNotExport()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        var viewModel = CreateViewModel(
            dbService: dbService,
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        // Act
        await viewModel.ExportAllCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.InfoRequests);
        Assert.Empty(fileDialogService.SaveOptions);
        Assert.Null(dbService.LastExportFilePath);
    }

    /// <summary>
    /// Verifies that ExportSelected stops when the save dialog is cancelled.
    /// </summary>
    [Fact]
    public async Task ExportSelected_WhenDialogCancelled_ShouldNotExport()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueSaveResult(null);
        var viewModel = CreateViewModel(dbService: dbService, fileDialogService: fileDialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        // Act
        await viewModel.ExportSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Null(dbService.LastExportFilePath);
    }

    /// <summary>
    /// Verifies that Import opens a file dialog and imports in replace mode when Yes is selected.
    /// </summary>
    [Fact]
    public async Task Import_WhenReplaceSelected_ShouldImportWithReplaceMode()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(@"C:\Imports\applications.json");
        dialogService.QueueYesNoCancelResult(true);
        var viewModel = CreateViewModel(
            dbService: dbService,
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        // Act
        await viewModel.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(fileDialogService.OpenOptions);
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(@"C:\Imports\applications.json", dbService.LastImportFilePath);
        Assert.Equal(ImportMode.Replace, dbService.LastImportMode);
    }

    /// <summary>
    /// Verifies that Import imports in merge mode when No is selected.
    /// </summary>
    [Fact]
    public async Task Import_WhenMergeSelected_ShouldImportWithMergeMode()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(@"C:\Imports\applications.json");
        dialogService.QueueYesNoCancelResult(false);
        var viewModel = CreateViewModel(
            dbService: dbService,
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        // Act
        await viewModel.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(@"C:\Imports\applications.json", dbService.LastImportFilePath);
        Assert.Equal(ImportMode.Merge, dbService.LastImportMode);
    }

    /// <summary>
    /// Verifies that Import stops when the import mode dialog is cancelled.
    /// </summary>
    [Fact]
    public async Task Import_WhenModeCancelled_ShouldNotImport()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(@"C:\Imports\applications.json");
        dialogService.QueueYesNoCancelResult(null);
        var viewModel = CreateViewModel(
            dbService: dbService,
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        // Act
        await viewModel.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Null(dbService.LastImportFilePath);
    }

    /// <summary>
    /// Verifies that Import stops before asking for mode when the file dialog is cancelled.
    /// </summary>
    [Fact]
    public async Task Import_WhenFileDialogCancelled_ShouldNotAskForMode()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(null);
        var viewModel = CreateViewModel(
            dbService: dbService,
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        // Act
        await viewModel.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(dialogService.YesNoCancelRequests);
        Assert.Null(dbService.LastImportFilePath);
    }

    /// <summary>
    /// Verifies that Import preserves the JSON file dialog options from the code-behind flow.
    /// </summary>
    [Fact]
    public async Task Import_ShouldUseJsonOpenDialogOptions()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(null);
        var viewModel = CreateViewModel(fileDialogService: fileDialogService);

        // Act
        await viewModel.ImportCommand.ExecuteAsync(null);

        // Assert
        var options = Assert.Single(fileDialogService.OpenOptions);
        Assert.Equal(Win11Forge.GUI.Resources.Resources.AppCatalog_Import, options.Title);
        Assert.Equal("JSON files (*.json)|*.json|All files (*.*)|*.*", options.Filter);
        Assert.Equal(".json", options.DefaultExtension);
    }

    /// <summary>
    /// Verifies that SaveApplicationAsync records undo action for new app.
    /// </summary>
    [Fact]
    public async Task SaveApplicationAsync_NewApp_ShouldRecordUndoAction()
    {
        // Arrange
        var undoService = new MockUndoService();
        var viewModel = CreateViewModel(undoService: undoService);

        var newApp = new EditableApplicationModel
        {
            AppId = "NewApp",
            Name = "New Application",
            Category = "Test",
            Sources = new ApplicationSourcesModel { Winget = "Test.App" }
        };

        // Act
        await viewModel.SaveApplicationAsync(newApp, isNew: true);

        // Assert
        Assert.NotNull(undoService.LastRecordedAction);
        // Description uses the Name (localized), not AppId
        Assert.Contains("New Application", undoService.LastRecordedAction.Description);
    }

    /// <summary>
    /// Verifies that SaveApplicationAsync records undo action for edited app.
    /// </summary>
    [Fact]
    public async Task SaveApplicationAsync_EditedApp_ShouldRecordUndoAction()
    {
        // Arrange
        var undoService = new MockUndoService();
        var viewModel = CreateViewModel(undoService: undoService);

        var originalApp = new EditableApplicationModel
        {
            AppId = "TestApp",
            Name = "Original Name",
            Category = "Test",
            Sources = new ApplicationSourcesModel { Winget = "Test.App" }
        };

        var editedApp = originalApp.Clone();
        editedApp.Name = "Edited Name";

        // Act
        await viewModel.SaveApplicationAsync(editedApp, isNew: false, originalApplication: originalApp);

        // Assert
        Assert.NotNull(undoService.LastRecordedAction);
    }

    /// <summary>
    /// Verifies that ImportApplicationsAsync updates status message on success.
    /// </summary>
    [Fact]
    public async Task ImportApplicationsAsync_Success_ShouldUpdateStatus()
    {
        // Arrange
        var dbService = new MockApplicationDatabaseService();
        var viewModel = CreateViewModel(dbService);

        // Act
        var result = await viewModel.ImportApplicationsAsync(@"C:\test.json", ImportMode.Merge);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that ExportApplicationsAsync updates status message on success.
    /// </summary>
    [Fact]
    public async Task ExportApplicationsAsync_Success_ShouldUpdateStatus()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var success = await viewModel.ExportApplicationsAsync(new[] { "VSCode" }, @"C:\test.json");

        // Assert
        Assert.True(success);
        Assert.NotEmpty(viewModel.StatusMessage);
    }
}

internal sealed class TestApplicationEditorDialogService : IApplicationEditorDialogService
{
    private readonly Queue<EditableApplicationModel?> _addResults = new();
    private readonly Queue<EditableApplicationModel?> _editResults = new();

    public int ShowAddCallCount { get; private set; }

    public int ShowEditCallCount { get; private set; }

    public List<EditableApplicationModel?> AddRequests { get; } = [];

    public List<EditableApplicationModel> EditRequests { get; } = [];

    public void QueueAddResult(EditableApplicationModel? application)
    {
        _addResults.Enqueue(application);
    }

    public void QueueEditResult(EditableApplicationModel? application)
    {
        _editResults.Enqueue(application);
    }

    public Task<EditableApplicationModel?> ShowAddDialogAsync(EditableApplicationModel? initialApplication = null)
    {
        ShowAddCallCount++;
        AddRequests.Add(initialApplication);
        return Task.FromResult(_addResults.Count > 0 ? _addResults.Dequeue() : null);
    }

    public Task<EditableApplicationModel?> ShowEditDialogAsync(EditableApplicationModel application)
    {
        ShowEditCallCount++;
        EditRequests.Add(application);
        return Task.FromResult(_editResults.Count > 0 ? _editResults.Dequeue() : null);
    }
}

internal sealed class TestDialogService : IDialogService
{
    private readonly Queue<bool> _confirmResults = new();
    private readonly Queue<bool?> _yesNoCancelResults = new();

    public List<(string Title, string Message)> InfoRequests { get; } = [];

    public List<(string Title, string Message)> ErrorRequests { get; } = [];

    public List<(string Title, string Message, string? ConfirmText, string? CancelText)> ConfirmRequests { get; } = [];

    public List<(string Title, string Message, string? YesText, string? NoText, string? CancelText)> YesNoCancelRequests { get; } = [];

    public void QueueConfirmResult(bool result)
    {
        _confirmResults.Enqueue(result);
    }

    public void QueueYesNoCancelResult(bool? result)
    {
        _yesNoCancelResults.Enqueue(result);
    }

    public Task<DialogAction> ShowErrorAsync(
        string title,
        string message,
        string? details = null,
        bool showRetry = false,
        string? helpUrl = null)
    {
        ErrorRequests.Add((title, message));
        return Task.FromResult(DialogAction.Ok);
    }

    public Task ShowInfoAsync(string title, string message)
    {
        InfoRequests.Add((title, message));
        return Task.CompletedTask;
    }

    public Task ShowSuccessAsync(string title, string message)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string? confirmText = null, string? cancelText = null)
    {
        ConfirmRequests.Add((title, message, confirmText, cancelText));
        return Task.FromResult(_confirmResults.Count > 0 && _confirmResults.Dequeue());
    }

    public Task<bool?> ShowYesNoCancelAsync(
        string title,
        string message,
        string? yesText = null,
        string? noText = null,
        string? cancelText = null)
    {
        YesNoCancelRequests.Add((title, message, yesText, noText, cancelText));
        return Task.FromResult(_yesNoCancelResults.Count > 0 ? _yesNoCancelResults.Dequeue() : null);
    }

    public Task ShowContentAsync(string title, object content, string? closeButtonText = null)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of IApplicationDatabaseService for unit testing.
/// </summary>
internal class MockApplicationDatabaseService : IApplicationDatabaseService
{
    private readonly List<EditableApplicationModel> _applications;
    private ApplicationValidationResult? _customValidationResult;
    private Exception? _loadException;

    public EditableApplicationModel? LastSavedApplication { get; private set; }

    public bool LastSaveWasNew { get; private set; }

    public int DeleteCallCount { get; private set; }

    public string? LastImportFilePath { get; private set; }

    public ImportMode? LastImportMode { get; private set; }

    public List<string>? LastExportAppIds { get; private set; }

    public string? LastExportFilePath { get; private set; }

    public void SetValidationResult(ApplicationValidationResult result)
    {
        _customValidationResult = result;
    }

    public MockApplicationDatabaseService()
    {
        _applications = new List<EditableApplicationModel>
        {
            new()
            {
                AppId = "VSCode",
                Name = "Visual Studio Code",
                Category = "Development",
                Description = "Code editor",
                DefaultPriority = 10,
                Sources = new ApplicationSourcesModel { Winget = "Microsoft.VisualStudioCode" },
                Tags = new List<string> { "editor", "ide" }
            },
            new()
            {
                AppId = "Chrome",
                Name = "Google Chrome",
                Category = "Browsers",
                Description = "Web browser by Google",
                DefaultPriority = 20,
                Sources = new ApplicationSourcesModel { Winget = "Google.Chrome" },
                Tags = new List<string> { "browser", "web" }
            },
            new()
            {
                AppId = "7Zip",
                Name = "7-Zip",
                Category = "Utilities",
                Description = "File archiver",
                DefaultPriority = 30,
                Sources = new ApplicationSourcesModel { Winget = "7zip.7zip", Chocolatey = "7zip" },
                Tags = new List<string> { "archive", "compression" }
            },
            new()
            {
                AppId = "Git",
                Name = "Git",
                Category = "Development",
                Description = "Version control system",
                DefaultPriority = 5,
                Sources = new ApplicationSourcesModel { Winget = "Git.Git" },
                Tags = new List<string> { "vcs", "git" }
            },
            new()
            {
                AppId = "Firefox",
                Name = "Mozilla Firefox",
                Category = "Browsers",
                Description = "Web browser by Mozilla",
                DefaultPriority = 25,
                Sources = new ApplicationSourcesModel { Winget = "Mozilla.Firefox" },
                Tags = new List<string> { "browser", "web" }
            }
        };
    }

    public void SetLoadException(Exception exception)
    {
        _loadException = exception;
    }

    public void ReplaceApplications(params EditableApplicationModel[] applications)
    {
        _applications.Clear();
        _applications.AddRange(applications);
    }

    public string DatabasePath => @"C:\Test\applications.json";

    public event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;

    public Task<IEnumerable<EditableApplicationModel>> LoadApplicationsAsync(CancellationToken cancellationToken = default)
    {
        if (_loadException != null)
        {
            throw _loadException;
        }

        return Task.FromResult<IEnumerable<EditableApplicationModel>>(_applications);
    }

    public Task<EditableApplicationModel?> GetApplicationAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult(_applications.FirstOrDefault(a => a.AppId == appId));

    public Task<ApplicationSaveResult> SaveApplicationAsync(EditableApplicationModel application, bool isNew, CancellationToken cancellationToken = default)
    {
        LastSavedApplication = application;
        LastSaveWasNew = isNew;

        if (isNew)
        {
            _applications.Add(application);
        }
        return Task.FromResult(new ApplicationSaveResult(true));
    }

    public Task<bool> DeleteApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;

        var app = _applications.FirstOrDefault(a => a.AppId == appId);
        if (app != null)
        {
            _applications.Remove(app);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<ApplicationValidationResult> ValidateApplicationAsync(EditableApplicationModel application, bool isNew, CancellationToken cancellationToken = default)
    {
        if (_customValidationResult != null)
        {
            var result = _customValidationResult;
            _customValidationResult = null;
            return Task.FromResult(result);
        }
        return Task.FromResult(ApplicationValidationResult.Valid());
    }

    public Task<bool> ApplicationExistsAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult(_applications.Any(a => a.AppId == appId));

    public Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_applications.Select(a => a.Category).Distinct());

    public Task<ApplicationImportResult> ImportApplicationsAsync(string filePath, ImportMode mode, CancellationToken cancellationToken = default)
    {
        LastImportFilePath = filePath;
        LastImportMode = mode;
        return Task.FromResult(new ApplicationImportResult(true, 2, 1, 0, Enumerable.Empty<string>()));
    }

    public Task<bool> ExportApplicationsAsync(IEnumerable<string> appIds, string filePath, CancellationToken cancellationToken = default)
    {
        LastExportAppIds = appIds.ToList();
        LastExportFilePath = filePath;
        return Task.FromResult(true);
    }

    public Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(@"C:\Test\backup.json");

    protected virtual void OnDatabaseChanged(DatabaseChangedEventArgs e)
    {
        DatabaseChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Mock implementation of IUndoService for unit testing.
/// Suppresses StateChanged event to avoid Dispatcher issues in tests.
/// </summary>
internal class MockUndoService : IUndoService
{
    private bool _canUndo;
    private bool _canRedo;
    private bool _suppressStateChanged = true;

    public bool CanUndo => _canUndo;
    public bool CanRedo => _canRedo;
    public string? NextUndoDescription => "Undo action";
    public string? NextRedoDescription => "Redo action";
    public int MaxHistorySize { get; set; } = 50;

    public bool UndoWasCalled { get; private set; }
    public bool RedoWasCalled { get; private set; }
    public UndoableAction? LastRecordedAction { get; private set; }

    /// <summary>
    /// Set to false to enable StateChanged events (may cause Dispatcher issues in WPF tests).
    /// </summary>
    public bool SuppressStateChanged
    {
        get => _suppressStateChanged;
        set => _suppressStateChanged = value;
    }

    public event EventHandler? StateChanged;

    public void SetCanUndo(bool canUndo)
    {
        _canUndo = canUndo;
        if (!_suppressStateChanged) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCanRedo(bool canRedo)
    {
        _canRedo = canRedo;
        if (!_suppressStateChanged) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordAction(UndoableAction action)
    {
        LastRecordedAction = action;
        _canUndo = true;
        if (!_suppressStateChanged) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordAction(string descriptionKey, Func<Task> undoAction, string category = "General")
    {
        LastRecordedAction = new UndoableAction
        {
            DescriptionKey = descriptionKey,
            Description = descriptionKey,
            UndoAction = undoAction,
            Category = category
        };
        _canUndo = true;
        if (!_suppressStateChanged) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> UndoAsync()
    {
        UndoWasCalled = true;
        return Task.FromResult(true);
    }

    public Task<bool> RedoAsync()
    {
        RedoWasCalled = true;
        return Task.FromResult(true);
    }

    public void ClearHistory()
    {
        _canUndo = false;
        _canRedo = false;
        LastRecordedAction = null;
    }

    public IReadOnlyList<UndoableAction> GetUndoHistory() => Array.Empty<UndoableAction>();
    public IReadOnlyList<UndoableAction> GetRedoHistory() => Array.Empty<UndoableAction>();

    public void Dispose() { }
}

/// <summary>
/// Mock implementation of IPackageVerificationService for unit testing.
/// </summary>
internal class MockPackageVerificationService : IPackageVerificationService
{
    public bool IsWingetAvailable => true;
    public bool IsChocolateyAvailable => true;

    public Task<PackageVerificationResult> VerifyWingetPackageAsync(string packageId, CancellationToken cancellationToken = default)
        => Task.FromResult(PackageVerificationResult.Found(packageId, PackageSource.Winget));

    public Task<PackageVerificationResult> VerifyChocolateyPackageAsync(string packageName, CancellationToken cancellationToken = default)
        => Task.FromResult(PackageVerificationResult.Found(packageName, PackageSource.Chocolatey));

    public Task<PackageVerificationResult> VerifyStoreProductAsync(string storeId, CancellationToken cancellationToken = default)
        => Task.FromResult(PackageVerificationResult.Found(storeId, PackageSource.Store));

    public Task<PackageVerificationResult> VerifyDirectUrlAsync(string url, CancellationToken cancellationToken = default)
        => Task.FromResult(PackageVerificationResult.Found(url, PackageSource.DirectUrl));

    public Task<ApplicationSourcesVerificationResult> VerifyAllSourcesAsync(ApplicationSourcesForVerification sources, CancellationToken cancellationToken = default)
    {
        var result = new ApplicationSourcesVerificationResult(
            sources.Winget != null ? PackageVerificationResult.Found(sources.Winget, PackageSource.Winget) : null,
            sources.Chocolatey != null ? PackageVerificationResult.Found(sources.Chocolatey, PackageSource.Chocolatey) : null,
            sources.Store != null ? PackageVerificationResult.Found(sources.Store, PackageSource.Store) : null,
            sources.DirectUrl != null ? PackageVerificationResult.Found(sources.DirectUrl, PackageSource.DirectUrl) : null
        );
        return Task.FromResult(result);
    }
}

/// <summary>
/// Mock implementation of IPackageSearchService for unit testing.
/// </summary>
internal class MockPackageSearchService : IPackageSearchService
{
    private readonly List<PackageSearchResult> _wingetResults = new();
    private readonly List<PackageSearchResult> _chocolateyResults = new();
    private readonly List<PackageSearchResult> _storeResults = new();

    public bool IsWingetAvailable { get; set; } = true;
    public bool IsChocolateyAvailable { get; set; } = true;

    public string? LastWingetQuery { get; private set; }
    public string? LastChocolateyQuery { get; private set; }
    public string? LastStoreQuery { get; private set; }

    public void SetWingetResults(params PackageSearchResult[] results)
    {
        _wingetResults.Clear();
        _wingetResults.AddRange(results);
    }

    public void SetChocolateyResults(params PackageSearchResult[] results)
    {
        _chocolateyResults.Clear();
        _chocolateyResults.AddRange(results);
    }

    public void SetStoreResults(params PackageSearchResult[] results)
    {
        _storeResults.Clear();
        _storeResults.AddRange(results);
    }

    public Task<IReadOnlyList<PackageSearchResult>> SearchWingetAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        LastWingetQuery = query;
        return Task.FromResult<IReadOnlyList<PackageSearchResult>>(
            _wingetResults.Take(maxResults).ToList());
    }

    public Task<IReadOnlyList<PackageSearchResult>> SearchChocolateyAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        LastChocolateyQuery = query;
        return Task.FromResult<IReadOnlyList<PackageSearchResult>>(
            _chocolateyResults.Take(maxResults).ToList());
    }

    public Task<IReadOnlyList<PackageSearchResult>> SearchStoreAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        LastStoreQuery = query;
        return Task.FromResult<IReadOnlyList<PackageSearchResult>>(
            _storeResults.Take(maxResults).ToList());
    }
}
