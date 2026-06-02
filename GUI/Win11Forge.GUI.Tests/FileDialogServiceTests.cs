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

using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for FileDialogService option mapping and result handling.
/// </summary>
public class FileDialogServiceTests
{
    [Fact]
    public async Task ShowOpenFileDialogAsync_WhenAccepted_ReturnsSelectedPathAndAppliesOptions()
    {
        // Arrange
        FakeFileDialogAdapter fakeDialog = new FakeFileDialogAdapter
        {
            ShowDialogResult = true,
            SelectedFileName = @"C:\Temp\selected.json"
        };
        FileDialogService service = new FileDialogService(() => fakeDialog, () => new FakeFileDialogAdapter());

        FileDialogOptions options = new FileDialogOptions(
            "Import",
            "JSON files (*.json)|*.json",
            InitialDirectory: @"C:\Temp",
            DefaultExtension: ".json");

        // Act
        string? result = await service.ShowOpenAsync(options);

        // Assert
        Assert.Equal(@"C:\Temp\selected.json", result);
        Assert.True(fakeDialog.WasShown);
        Assert.Equal("Import", fakeDialog.Title);
        Assert.Equal("JSON files (*.json)|*.json", fakeDialog.Filter);
        Assert.Equal(".json", fakeDialog.DefaultExt);
        Assert.Equal(@"C:\Temp", fakeDialog.InitialDirectory);
    }

    [Fact]
    public async Task ShowOpenFileDialogAsync_WhenCancelled_ReturnsNull()
    {
        // Arrange
        FakeFileDialogAdapter fakeDialog = new FakeFileDialogAdapter
        {
            ShowDialogResult = false,
            SelectedFileName = @"C:\Temp\ignored.json"
        };
        FileDialogService service = new FileDialogService(() => fakeDialog, () => new FakeFileDialogAdapter());

        // Act
        string? result = await service.ShowOpenAsync(new FileDialogOptions("Import", "JSON files (*.json)|*.json"));

        // Assert
        Assert.Null(result);
        Assert.True(fakeDialog.WasShown);
    }

    [Fact]
    public async Task ShowSaveFileDialogAsync_WhenAccepted_ReturnsSelectedPathAndAppliesDefaultFileName()
    {
        // Arrange
        FakeFileDialogAdapter fakeDialog = new FakeFileDialogAdapter
        {
            ShowDialogResult = true,
            SelectedFileName = @"C:\Exports\settings.json"
        };
        FileDialogService service = new FileDialogService(() => new FakeFileDialogAdapter(), () => fakeDialog);

        FileDialogOptions options = new FileDialogOptions(
            "Export",
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultFileName: "settings",
            DefaultExtension: ".json");

        // Act
        string? result = await service.ShowSaveAsync(options);

        // Assert
        Assert.Equal(@"C:\Exports\settings.json", result);
        Assert.True(fakeDialog.WasShown);
        Assert.Equal("Export", fakeDialog.Title);
        Assert.Equal("JSON files (*.json)|*.json|All files (*.*)|*.*", fakeDialog.Filter);
        Assert.Equal("settings", fakeDialog.FileNameBeforeShow);
        Assert.Equal(".json", fakeDialog.DefaultExt);
    }

    [Fact]
    public async Task ShowSaveFileDialogAsync_WhenCancelled_ReturnsNull()
    {
        // Arrange
        FakeFileDialogAdapter fakeDialog = new FakeFileDialogAdapter
        {
            ShowDialogResult = null,
            SelectedFileName = @"C:\Exports\ignored.zip"
        };
        FileDialogService service = new FileDialogService(() => new FakeFileDialogAdapter(), () => fakeDialog);

        // Act
        string? result = await service.ShowSaveAsync(new FileDialogOptions("Export", "ZIP Archive (*.zip)|*.zip"));

        // Assert
        Assert.Null(result);
        Assert.True(fakeDialog.WasShown);
    }

    private sealed class FakeFileDialogAdapter : IFileDialogAdapter
    {
        public string Title { get; set; } = string.Empty;

        public string Filter { get; set; } = string.Empty;

        public string DefaultExt { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string? FileNameBeforeShow { get; private set; }

        public string? SelectedFileName { get; set; }

        public string InitialDirectory { get; set; } = string.Empty;

        public bool? ShowDialogResult { get; set; }

        public bool WasShown { get; private set; }

        public bool? ShowDialog()
        {
            WasShown = true;
            FileNameBeforeShow = FileName;
            if (SelectedFileName != null)
            {
                FileName = SelectedFileName;
            }
            return ShowDialogResult;
        }
    }
}
