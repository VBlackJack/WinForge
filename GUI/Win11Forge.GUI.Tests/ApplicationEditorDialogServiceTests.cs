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

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ApplicationEditorDialogService owner resolution and result handling.
/// </summary>
public class ApplicationEditorDialogServiceTests
{
    [Fact]
    public async Task ShowAddDialogAsync_WhenDialogReturnsApplication_ReturnsApplication()
    {
        // Arrange
        var owner = new object();
        var savedApplication = CreateApplication("NewApp");
        object? receivedOwner = null;

        var service = new ApplicationEditorDialogService(
            () => owner,
            dialogOwner =>
            {
                receivedOwner = dialogOwner;
                return Task.FromResult<EditableApplicationModel?>(savedApplication);
            },
            (_, _) => Task.FromResult<EditableApplicationModel?>(null));

        // Act
        var result = await service.ShowAddDialogAsync();

        // Assert
        Assert.Same(savedApplication, result);
        Assert.Same(owner, receivedOwner);
    }

    [Fact]
    public async Task ShowEditDialogAsync_WhenDialogReturnsApplication_ReturnsApplicationAndPassesInput()
    {
        // Arrange
        var owner = new object();
        var inputApplication = CreateApplication("ExistingApp");
        var savedApplication = CreateApplication("EditedApp");
        EditableApplicationModel? receivedApplication = null;

        var service = new ApplicationEditorDialogService(
            () => owner,
            _ => Task.FromResult<EditableApplicationModel?>(null),
            (_, application) =>
            {
                receivedApplication = application;
                return Task.FromResult<EditableApplicationModel?>(savedApplication);
            });

        // Act
        var result = await service.ShowEditDialogAsync(inputApplication);

        // Assert
        Assert.Same(savedApplication, result);
        Assert.Same(inputApplication, receivedApplication);
    }

    [Fact]
    public async Task ShowAddDialogAsync_WhenOwnerMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new ApplicationEditorDialogService(
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
        var service = new ApplicationEditorDialogService(
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
