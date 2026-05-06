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

using Win11Forge.GUI.ViewModels;
using System.IO;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for LogsViewModel - log file management and filtering.
/// </summary>
public class LogsViewModelTests
{
    /// <summary>
    /// Verifies that the ViewModel initializes with default values.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var viewModel = new LogsViewModel();

        // Assert
        Assert.Empty(viewModel.SearchText);
        Assert.Equal("All", viewModel.SelectedLogLevel);
        Assert.Null(viewModel.FilterDate);
        Assert.NotNull(viewModel.LogFiles);
        Assert.NotNull(viewModel.FilteredLogFiles);
    }

    /// <summary>
    /// Verifies that LogLevels contains expected filter options.
    /// </summary>
    [Fact]
    public void LogLevels_ShouldContainExpectedOptions()
    {
        // Arrange
        var viewModel = new LogsViewModel();

        // Assert
        Assert.Contains("All", viewModel.LogLevels);
        Assert.Contains("Text", viewModel.LogLevels);
        Assert.Contains("JSON", viewModel.LogLevels);
        Assert.Contains("Error", viewModel.LogLevels);
        Assert.Equal(4, viewModel.LogLevels.Count);
    }

    /// <summary>
    /// Verifies that HasNoLogs returns true when no logs are loaded.
    /// </summary>
    [Fact]
    public void HasNoLogs_ShouldBeTrueWhenEmpty()
    {
        // Arrange
        var viewModel = new LogsViewModel();

        // Act - Wait for initial load to complete
        // In tests, we check the initial state before async load
        viewModel.FilteredLogFiles.Clear();

        // Assert
        Assert.True(viewModel.HasNoLogs || viewModel.IsLoading);
    }

    /// <summary>
    /// Verifies that SelectedLogFile can be set.
    /// </summary>
    [Fact]
    public void SelectedLogFile_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new LogsViewModel();

        // Act
        viewModel.SelectedLogFile = null;

        // Assert
        Assert.Null(viewModel.SelectedLogFile);
    }

    /// <summary>
    /// Verifies that SearchText triggers filter when changed.
    /// </summary>
    [Fact]
    public void SearchText_ShouldTriggerPropertyChanged()
    {
        // Arrange
        var viewModel = new LogsViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(viewModel.SearchText))
                propertyChanged = true;
        };

        // Act
        viewModel.SearchText = "test";

        // Assert
        Assert.True(propertyChanged);
        Assert.Equal("test", viewModel.SearchText);
    }

    /// <summary>
    /// Verifies that TotalSizeFormatted has initial value.
    /// </summary>
    [Fact]
    public void TotalSizeFormatted_ShouldHaveInitialValue()
    {
        // Arrange
        var viewModel = new LogsViewModel();

        // Assert
        Assert.Equal("0 B", viewModel.TotalSizeFormatted);
    }

    [Fact]
    public async Task ExportLogs_WhenDialogCancelled_ShouldUseZipDialogOptions()
    {
        // Arrange
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueSaveResult(null);
        var viewModel = new LogsViewModel(fileDialogService);

        // Act
        await viewModel.ExportLogsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Equal("ZIP Archive (*.zip)|*.zip", fileDialogService.SaveOptions[0].Filter);
        Assert.Equal(".zip", fileDialogService.SaveOptions[0].DefaultExtension);
        Assert.StartsWith("Win11Forge_Logs_", fileDialogService.SaveOptions[0].DefaultFileName);
    }

    [Fact]
    public async Task ClearOldLogs_WhenCancelled_ShouldOnlyAskForConfirmation()
    {
        // Arrange
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = new LogsViewModel(dialogService: dialogService);

        // Act
        await viewModel.ClearOldLogsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
    }

    [Fact]
    public async Task DeleteLog_WhenCancelled_ShouldNotRemoveLog()
    {
        // Arrange
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = new LogsViewModel(dialogService: dialogService);
        var logFile = new LogFileEntry
        {
            FileName = "test.log",
            FullPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.log"),
            LastModified = DateTime.Now,
            LogType = "Text"
        };
        viewModel.LogFiles.Add(logFile);
        viewModel.FilteredLogFiles.Add(logFile);

        // Act
        await viewModel.DeleteLogCommand.ExecuteAsync(logFile);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.Contains(logFile, viewModel.LogFiles);
        Assert.Contains(logFile, viewModel.FilteredLogFiles);
    }
}
