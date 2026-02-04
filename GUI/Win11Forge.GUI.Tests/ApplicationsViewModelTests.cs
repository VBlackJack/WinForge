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
/// Tests for ApplicationsViewModel - application database management functionality.
/// </summary>
public class ApplicationsViewModelTests
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

    private static ApplicationsViewModel CreateViewModel(
        MockApplicationDatabaseService? dbService = null,
        MockUndoService? undoService = null,
        MockPackageVerificationService? verificationService = null)
    {
        return new ApplicationsViewModel(
            dbService ?? new MockApplicationDatabaseService(),
            undoService ?? new MockUndoService(),
            verificationService ?? new MockPackageVerificationService());
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
    /// Verifies that Add command triggers OpenEditorRequested event.
    /// </summary>
    [Fact]
    public void Add_ShouldRaiseOpenEditorRequested()
    {
        // Arrange
        var viewModel = CreateViewModel();
        ApplicationEditorEventArgs? receivedArgs = null;
        viewModel.OpenEditorRequested += (_, args) => receivedArgs = args;

        // Act
        viewModel.AddCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.True(receivedArgs.IsNew);
        Assert.NotNull(receivedArgs.Application);
    }

    /// <summary>
    /// Verifies that Edit command triggers OpenEditorRequested with selected app.
    /// </summary>
    [Fact]
    public async Task Edit_ShouldRaiseOpenEditorRequestedWithClone()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        ApplicationEditorEventArgs? receivedArgs = null;
        viewModel.OpenEditorRequested += (_, args) => receivedArgs = args;

        // Act
        viewModel.EditCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.False(receivedArgs.IsNew);
        Assert.NotNull(receivedArgs.Application);
        Assert.Equal(viewModel.SelectedApplication.AppId, receivedArgs.Application.AppId);
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
    /// Verifies that Delete command requests confirmation.
    /// </summary>
    [Fact]
    public async Task Delete_ShouldRequestConfirmation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        ConfirmDeleteEventArgs? receivedArgs = null;
        viewModel.ConfirmDeleteRequested += (_, args) =>
        {
            receivedArgs = args;
            args.Confirmed = false; // Don't actually delete
        };

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(viewModel.SelectedApplication.AppId, receivedArgs.AppId);
    }

    /// <summary>
    /// Verifies that Duplicate creates a copy with modified ID.
    /// </summary>
    [Fact]
    public async Task Duplicate_ShouldCreateCopyWithModifiedId()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];
        var originalId = viewModel.SelectedApplication.AppId;

        ApplicationEditorEventArgs? receivedArgs = null;
        viewModel.OpenEditorRequested += (_, args) => receivedArgs = args;

        // Act
        viewModel.DuplicateCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.True(receivedArgs.IsNew);
        Assert.Equal($"{originalId}_Copy", receivedArgs.Application.AppId);
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
    /// Verifies that ExportAll triggers ExportRequested with all IDs.
    /// </summary>
    [Fact]
    public async Task ExportAll_ShouldRequestExportWithAllIds()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);

        ExportEventArgs? receivedArgs = null;
        viewModel.ExportRequested += (_, args) => receivedArgs = args;

        // Act
        viewModel.ExportAllCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(5, receivedArgs.AppIds.Count);
    }

    /// <summary>
    /// Verifies that Import triggers ImportRequested event.
    /// </summary>
    [Fact]
    public void Import_ShouldRaiseImportRequested()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var eventRaised = false;
        viewModel.ImportRequested += (_, _) => eventRaised = true;

        // Act
        viewModel.ImportCommand.Execute(null);

        // Assert
        Assert.True(eventRaised);
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

/// <summary>
/// Mock implementation of IApplicationDatabaseService for unit testing.
/// </summary>
internal class MockApplicationDatabaseService : IApplicationDatabaseService
{
    private readonly List<EditableApplicationModel> _applications;
    private ApplicationValidationResult? _customValidationResult;

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

    public string DatabasePath => @"C:\Test\applications.json";

    public event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;

    public Task<IEnumerable<EditableApplicationModel>> LoadApplicationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<EditableApplicationModel>>(_applications);

    public Task<EditableApplicationModel?> GetApplicationAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult(_applications.FirstOrDefault(a => a.AppId == appId));

    public Task<ApplicationSaveResult> SaveApplicationAsync(EditableApplicationModel application, bool isNew, CancellationToken cancellationToken = default)
    {
        if (isNew)
        {
            _applications.Add(application);
        }
        return Task.FromResult(new ApplicationSaveResult(true));
    }

    public Task<bool> DeleteApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
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
        => Task.FromResult(new ApplicationImportResult(true, 2, 1, 0, Enumerable.Empty<string>()));

    public Task<bool> ExportApplicationsAsync(IEnumerable<string> appIds, string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

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
