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

using WinForge.GUI.Models;
using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for ApplicationEditorDialogService owner resolution and result handling.
/// </summary>
public class ApplicationEditorDialogServiceTests
{
    [Fact]
    public async Task ShowAddDialogAsync_WhenDialogReturnsApplication_ReturnsApplication()
    {
        // Arrange
        object owner = new object();
        EditableApplicationModel savedApplication = CreateApplication("NewApp");
        object? receivedOwner = null;

        ApplicationEditorDialogService service = new ApplicationEditorDialogService(
            () => owner,
            dialogOwner =>
            {
                receivedOwner = dialogOwner;
                return Task.FromResult<EditableApplicationModel?>(savedApplication);
            },
            (_, _) => Task.FromResult<EditableApplicationModel?>(null));

        // Act
        EditableApplicationModel? result = await service.ShowAddDialogAsync();

        // Assert
        Assert.Same(savedApplication, result);
        Assert.Same(owner, receivedOwner);
    }

    [Fact]
    public async Task ShowAddDialogAsync_WhenInitialApplicationProvided_PassesItToDialog()
    {
        // Arrange
        object owner = new object();
        EditableApplicationModel initialApplication = CreateApplication("SeedApp");
        object? receivedOwner = null;
        EditableApplicationModel? receivedApplication = null;

        ApplicationEditorDialogService service = new ApplicationEditorDialogService(
            () => owner,
            (dialogOwner, application) =>
            {
                receivedOwner = dialogOwner;
                receivedApplication = application;
                return Task.FromResult<EditableApplicationModel?>(application);
            },
            (_, _) => Task.FromResult<EditableApplicationModel?>(null));

        // Act
        EditableApplicationModel? result = await service.ShowAddDialogAsync(initialApplication);

        // Assert
        Assert.Same(initialApplication, result);
        Assert.Same(owner, receivedOwner);
        Assert.Same(initialApplication, receivedApplication);
    }

    [Fact]
    public async Task ShowEditDialogAsync_WhenDialogReturnsApplication_ReturnsApplicationAndPassesInput()
    {
        // Arrange
        object owner = new object();
        EditableApplicationModel inputApplication = CreateApplication("ExistingApp");
        EditableApplicationModel savedApplication = CreateApplication("EditedApp");
        EditableApplicationModel? receivedApplication = null;

        ApplicationEditorDialogService service = new ApplicationEditorDialogService(
            () => owner,
            _ => Task.FromResult<EditableApplicationModel?>(null),
            (_, application) =>
            {
                receivedApplication = application;
                return Task.FromResult<EditableApplicationModel?>(savedApplication);
            });

        // Act
        EditableApplicationModel? result = await service.ShowEditDialogAsync(inputApplication);

        // Assert
        Assert.Same(savedApplication, result);
        Assert.Same(inputApplication, receivedApplication);
    }

    [Fact]
    public async Task ShowAddDialogAsync_WhenOwnerMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        ApplicationEditorDialogService service = new ApplicationEditorDialogService(
            () => null,
            _ => Task.FromResult<EditableApplicationModel?>(CreateApplication("Ignored")),
            (_, _) => Task.FromResult<EditableApplicationModel?>(null));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowAddDialogAsync());
    }

    [Fact]
    public async Task ShowEditDialogAsync_WhenApplicationNull_ThrowsArgumentNullException()
    {
        // Arrange
        ApplicationEditorDialogService service = new ApplicationEditorDialogService(
            () => new object(),
            _ => Task.FromResult<EditableApplicationModel?>(null),
            (_, _) => Task.FromResult<EditableApplicationModel?>(null));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowEditDialogAsync(null!));
    }

    private static EditableApplicationModel CreateApplication(string appId)
    {
        return new EditableApplicationModel
        {
            AppId = appId,
            Name = appId,
            Category = "Test",
            DefaultPriority = 50,
            Sources = new ApplicationSourcesModel()
        };
    }
}
