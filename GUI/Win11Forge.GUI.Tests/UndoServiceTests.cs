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
/// Tests for UndoService - undo/redo functionality.
/// </summary>
public class UndoServiceTests
{
    /// <summary>
    /// Verifies that a new UndoService has no undo/redo actions.
    /// </summary>
    [Fact]
    public void NewService_ShouldHaveNoUndoRedo()
    {
        // Arrange & Act
        using var service = new UndoService();

        // Assert
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Null(service.NextUndoDescription);
        Assert.Null(service.NextRedoDescription);
    }

    /// <summary>
    /// Verifies that recording an action enables undo.
    /// </summary>
    [Fact]
    public void RecordAction_ShouldEnableUndo()
    {
        // Arrange
        using var service = new UndoService();
        var action = new UndoableAction
        {
            Description = "Test action",
            UndoAction = () => Task.CompletedTask
        };

        // Act
        service.RecordAction(action);

        // Assert
        Assert.True(service.CanUndo);
        Assert.Equal("Test action", service.NextUndoDescription);
    }

    /// <summary>
    /// Verifies that undo executes the undo action and enables redo.
    /// </summary>
    [Fact]
    public async Task Undo_ShouldExecuteUndoAction()
    {
        // Arrange
        using var service = new UndoService();
        var undoExecuted = false;
        var action = new UndoableAction
        {
            Description = "Test action",
            UndoAction = () => { undoExecuted = true; return Task.CompletedTask; }
        };
        service.RecordAction(action);

        // Act
        var result = await service.UndoAsync();

        // Assert
        Assert.True(result);
        Assert.True(undoExecuted);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
    }

    /// <summary>
    /// Verifies that redo executes the do action and enables undo.
    /// </summary>
    [Fact]
    public async Task Redo_ShouldExecuteDoAction()
    {
        // Arrange
        using var service = new UndoService();
        var doExecuted = false;
        var action = new UndoableAction
        {
            Description = "Test action",
            UndoAction = () => Task.CompletedTask,
            DoAction = () => { doExecuted = true; return Task.CompletedTask; }
        };
        service.RecordAction(action);
        await service.UndoAsync();

        // Act
        var result = await service.RedoAsync();

        // Assert
        Assert.True(result);
        Assert.True(doExecuted);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    /// <summary>
    /// Verifies that recording new action clears redo stack.
    /// </summary>
    [Fact]
    public async Task RecordAction_ShouldClearRedoStack()
    {
        // Arrange
        using var service = new UndoService();
        var action1 = new UndoableAction
        {
            Description = "Action 1",
            UndoAction = () => Task.CompletedTask
        };
        var action2 = new UndoableAction
        {
            Description = "Action 2",
            UndoAction = () => Task.CompletedTask
        };

        service.RecordAction(action1);
        await service.UndoAsync();
        Assert.True(service.CanRedo);

        // Act - Record new action
        service.RecordAction(action2);

        // Assert
        Assert.False(service.CanRedo);
        Assert.True(service.CanUndo);
        Assert.Equal("Action 2", service.NextUndoDescription);
    }

    /// <summary>
    /// Verifies that ClearHistory removes all actions.
    /// </summary>
    [Fact]
    public async Task ClearHistory_ShouldRemoveAllActions()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction { Description = "Action 1", UndoAction = () => Task.CompletedTask });
        service.RecordAction(new UndoableAction { Description = "Action 2", UndoAction = () => Task.CompletedTask });
        await service.UndoAsync();

        // Act
        service.ClearHistory();

        // Assert
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Empty(service.GetUndoHistory());
        Assert.Empty(service.GetRedoHistory());
    }

    /// <summary>
    /// Verifies that MaxHistorySize limits undo stack.
    /// </summary>
    [Fact]
    public void MaxHistorySize_ShouldLimitUndoStack()
    {
        // Arrange
        using var service = new UndoService();
        service.MaxHistorySize = 3;

        // Act - Add more actions than max
        for (int i = 1; i <= 5; i++)
        {
            service.RecordAction(new UndoableAction
            {
                Description = $"Action {i}",
                UndoAction = () => Task.CompletedTask
            });
        }

        // Assert
        var history = service.GetUndoHistory();
        Assert.True(history.Count <= 3);
    }

    /// <summary>
    /// Verifies that StateChanged event is raised when recording actions.
    /// </summary>
    [Fact]
    public void RecordAction_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = new UndoService();
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.RecordAction(new UndoableAction
        {
            Description = "Test",
            UndoAction = () => Task.CompletedTask
        });

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that StateChanged event is raised when undoing.
    /// </summary>
    [Fact]
    public async Task Undo_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction
        {
            Description = "Test",
            UndoAction = () => Task.CompletedTask
        });

        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        await service.UndoAsync();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that StateChanged event is raised when redoing.
    /// </summary>
    [Fact]
    public async Task Redo_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction
        {
            Description = "Test",
            UndoAction = () => Task.CompletedTask,
            DoAction = () => Task.CompletedTask
        });
        await service.UndoAsync();

        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        await service.RedoAsync();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that Undo returns false when stack is empty.
    /// </summary>
    [Fact]
    public async Task Undo_EmptyStack_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UndoService();

        // Act
        var result = await service.UndoAsync();

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Redo returns false when stack is empty.
    /// </summary>
    [Fact]
    public async Task Redo_EmptyStack_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UndoService();

        // Act
        var result = await service.RedoAsync();

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that GetUndoHistory returns actions in correct order.
    /// </summary>
    [Fact]
    public void GetUndoHistory_ShouldReturnMostRecentFirst()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction { Description = "Action 1", UndoAction = () => Task.CompletedTask });
        service.RecordAction(new UndoableAction { Description = "Action 2", UndoAction = () => Task.CompletedTask });
        service.RecordAction(new UndoableAction { Description = "Action 3", UndoAction = () => Task.CompletedTask });

        // Act
        var history = service.GetUndoHistory();

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal("Action 3", history[0].Description);
        Assert.Equal("Action 2", history[1].Description);
        Assert.Equal("Action 1", history[2].Description);
    }

    /// <summary>
    /// Verifies that RecordAction with description key works correctly.
    /// </summary>
    [Fact]
    public void RecordAction_WithDescriptionKey_ShouldWork()
    {
        // Arrange
        using var service = new UndoService();

        // Act
        service.RecordAction("test.key", () => Task.CompletedTask, "TestCategory");

        // Assert
        Assert.True(service.CanUndo);
        var history = service.GetUndoHistory();
        Assert.Single(history);
        Assert.Equal("TestCategory", history[0].Category);
    }

    /// <summary>
    /// Verifies that Dispose cleans up resources.
    /// </summary>
    [Fact]
    public void Dispose_ShouldClearStacks()
    {
        // Arrange
        var service = new UndoService();
        service.RecordAction(new UndoableAction
        {
            Description = "Test",
            UndoAction = () => Task.CompletedTask
        });

        // Act
        service.Dispose();

        // Assert
        Assert.Empty(service.GetUndoHistory());
        Assert.Empty(service.GetRedoHistory());
    }

    /// <summary>
    /// Verifies that failed undo puts action back on stack.
    /// </summary>
    [Fact]
    public async Task Undo_WhenFails_ShouldRestoreAction()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction
        {
            Description = "Failing action",
            UndoAction = () => throw new InvalidOperationException("Test failure")
        });

        // Act
        var result = await service.UndoAsync();

        // Assert
        Assert.False(result);
        Assert.True(service.CanUndo); // Action should still be on stack
    }

    /// <summary>
    /// Verifies that failed redo puts action back on stack.
    /// </summary>
    [Fact]
    public async Task Redo_WhenFails_ShouldRestoreAction()
    {
        // Arrange
        using var service = new UndoService();
        service.RecordAction(new UndoableAction
        {
            Description = "Action",
            UndoAction = () => Task.CompletedTask,
            DoAction = () => throw new InvalidOperationException("Test failure")
        });
        await service.UndoAsync();

        // Act
        var result = await service.RedoAsync();

        // Assert
        Assert.False(result);
        Assert.True(service.CanRedo); // Action should still be on redo stack
    }

    /// <summary>
    /// Verifies that UndoableAction has correct default values.
    /// </summary>
    [Fact]
    public void UndoableAction_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var action = new UndoableAction();

        // Assert
        Assert.NotEmpty(action.Id);
        Assert.Empty(action.Description);
        Assert.Empty(action.DescriptionKey);
        Assert.Null(action.DoAction);
        Assert.Null(action.UndoAction);
        Assert.Equal("General", action.Category);
        Assert.False(action.CanUndo);
        Assert.True(action.Timestamp <= DateTime.UtcNow);
    }

    /// <summary>
    /// Verifies that UndoableAction.CanUndo is true when UndoAction is set.
    /// </summary>
    [Fact]
    public void UndoableAction_CanUndo_ShouldBeTrueWhenUndoActionSet()
    {
        // Arrange & Act
        var action = new UndoableAction
        {
            UndoAction = () => Task.CompletedTask
        };

        // Assert
        Assert.True(action.CanUndo);
    }

    /// <summary>
    /// Verifies MaxHistorySize property validation.
    /// </summary>
    [Fact]
    public void MaxHistorySize_ShouldClampValues()
    {
        // Arrange
        using var service = new UndoService();

        // Act & Assert - Too low
        service.MaxHistorySize = -10;
        Assert.Equal(1, service.MaxHistorySize);

        // Act & Assert - Too high
        service.MaxHistorySize = 1000;
        Assert.Equal(500, service.MaxHistorySize);

        // Act & Assert - Valid
        service.MaxHistorySize = 100;
        Assert.Equal(100, service.MaxHistorySize);
    }
}
