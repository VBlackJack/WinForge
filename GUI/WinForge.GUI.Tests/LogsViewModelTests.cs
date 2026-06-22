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
using Win11Forge.GUI.Tests.TestInfrastructure;
using Win11Forge.GUI.ViewModels;
using Loc = Win11Forge.GUI.Resources.Resources;

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
        LogsViewModel viewModel = new LogsViewModel();

        // Assert
        Assert.Empty(viewModel.SearchText);
        Assert.Equal(Loc.Logs_Filter_All, viewModel.SelectedLogLevel);
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
        LogsViewModel viewModel = new LogsViewModel();

        // Assert
        Assert.Contains(Loc.Logs_Filter_All, viewModel.LogLevels);
        Assert.Contains(Loc.Logs_Filter_Text, viewModel.LogLevels);
        Assert.Contains(Loc.Logs_Filter_Json, viewModel.LogLevels);
        Assert.Contains(Loc.Logs_Filter_Error, viewModel.LogLevels);
        Assert.Equal(4, viewModel.LogLevels.Count);
    }

    /// <summary>
    /// Verifies that HasNoLogs returns true when no logs are loaded.
    /// </summary>
    [Fact]
    public void HasNoLogs_ShouldBeTrueWhenEmpty()
    {
        // Arrange
        LogsViewModel viewModel = new LogsViewModel();

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
        LogsViewModel viewModel = new LogsViewModel();

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
        LogsViewModel viewModel = new LogsViewModel();
        bool propertyChanged = false;
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
        LogsViewModel viewModel = new LogsViewModel();

        // Assert
        Assert.Equal("0 B", viewModel.TotalSizeFormatted);
    }

    [Fact]
    public async Task ExportLogs_WhenDialogCancelled_ShouldUseZipDialogOptions()
    {
        // Arrange
        TestFileDialogService fileDialogService = new TestFileDialogService();
        fileDialogService.QueueSaveResult(null);
        LogsViewModel viewModel = new LogsViewModel(fileDialogService);

        // Act
        await viewModel.ExportLogsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(fileDialogService.SaveOptions);
        Assert.Equal(Loc.Logs_Export_Filter, fileDialogService.SaveOptions[0].Filter);
        Assert.Equal(".zip", fileDialogService.SaveOptions[0].DefaultExtension);
        Assert.StartsWith("Win11Forge_Logs_", fileDialogService.SaveOptions[0].DefaultFileName);
    }

    [Fact]
    public async Task ClearOldLogs_WhenCancelled_ShouldOnlyAskForConfirmation()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        LogsViewModel viewModel = new LogsViewModel(dialogService: dialogService);

        // Act
        await viewModel.ClearOldLogsCommand.ExecuteAsync(null);

        // Assert
        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_ClearOldLogs_Title, request.Title);
        Assert.Equal(Loc.Confirm_ClearOldLogs_Message, request.Message);
        Assert.Equal(Loc.Confirm_ClearOldLogs_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task DeleteLog_WhenCancelled_ShouldNotRemoveLog()
    {
        // Arrange
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        LogsViewModel viewModel = new LogsViewModel(dialogService: dialogService);
        LogFileEntry logFile = new LogFileEntry
        {
            FileName = "test.log",
            FullPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.log"),
            LastModified = DateTime.Now,
            LogType = Loc.Logs_Filter_Text
        };
        viewModel.LogFiles.Add(logFile);
        viewModel.FilteredLogFiles.Add(logFile);

        // Act
        await viewModel.DeleteLogCommand.ExecuteAsync(logFile);

        // Assert
        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_DeleteLog_Title, request.Title);
        Assert.Equal(string.Format(Loc.Confirm_DeleteLog_Message, logFile.FileName), request.Message);
        Assert.Equal(Loc.Confirm_Delete_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
        Assert.Contains(logFile, viewModel.LogFiles);
        Assert.Contains(logFile, viewModel.FilteredLogFiles);
    }

    [Fact]
    public void LogsViewModel_UserFacingStatusAndDialogStrings_ShouldUseResources()
    {
        string sourcePath = RepositoryPathHelper.FindFile(
            "GUI",
            "Win11Forge.GUI",
            "ViewModels",
            "LogsViewModel.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("StatusMessage = $\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusMessage = \"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowConfirmAsync(\"", source, StringComparison.Ordinal);

        string[] forbiddenUserFacingStrings = new[]
        {
            "Loading logs...",
            "log files found",
            "Error loading logs:",
            "Error opening folder:",
            "ZIP Archive (*.zip)|*.zip",
            "No log files could be exported",
            "Logs exported to",
            "Export failed:",
            "old log files",
            "Error clearing logs:",
            "Log file path is empty",
            "Log file not found:",
            "Error opening log:",
            "Path copied to clipboard",
            "Delete Log File",
            "Error deleting log:"
        };

        foreach (string? text in forbiddenUserFacingStrings)
        {
            Assert.DoesNotContain(text, source, StringComparison.Ordinal);
        }
    }
}
