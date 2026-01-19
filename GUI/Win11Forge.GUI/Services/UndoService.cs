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

using System.Collections.Concurrent;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Represents an undoable action with its inverse operation.
/// </summary>
public class UndoableAction
{
    /// <summary>
    /// Unique identifier for the action.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable description of the action.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Localization key for the action description.
    /// </summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>
    /// The action that was performed.
    /// </summary>
    public Func<Task>? DoAction { get; init; }

    /// <summary>
    /// The inverse action to undo.
    /// </summary>
    public Func<Task>? UndoAction { get; init; }

    /// <summary>
    /// Timestamp when the action was performed.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Category of the action (e.g., "Selection", "Settings", "Profile").
    /// </summary>
    public string Category { get; init; } = "General";

    /// <summary>
    /// Whether this action can be undone (some actions are informational only).
    /// </summary>
    public bool CanUndo => UndoAction != null;
}

/// <summary>
/// Interface for undo service operations.
/// </summary>
public interface IUndoService : IDisposable
{
    /// <summary>
    /// Whether there are actions that can be undone.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether there are actions that can be redone.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Gets the description of the next undoable action.
    /// </summary>
    string? NextUndoDescription { get; }

    /// <summary>
    /// Gets the description of the next redoable action.
    /// </summary>
    string? NextRedoDescription { get; }

    /// <summary>
    /// Maximum number of actions to keep in history.
    /// </summary>
    int MaxHistorySize { get; set; }

    /// <summary>
    /// Raised when undo/redo state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Records an undoable action.
    /// </summary>
    void RecordAction(UndoableAction action);

    /// <summary>
    /// Records a simple undoable action with description and undo callback.
    /// </summary>
    void RecordAction(string descriptionKey, Func<Task> undoAction, string category = "General");

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    Task<bool> UndoAsync();

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    Task<bool> RedoAsync();

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Gets the undo history (most recent first).
    /// </summary>
    IReadOnlyList<UndoableAction> GetUndoHistory();

    /// <summary>
    /// Gets the redo history (most recent first).
    /// </summary>
    IReadOnlyList<UndoableAction> GetRedoHistory();
}

/// <summary>
/// Service for managing undo/redo operations on non-destructive actions.
/// Thread-safe implementation supporting async operations.
/// </summary>
public class UndoService : IUndoService
{
    private readonly ConcurrentStack<UndoableAction> _undoStack = new();
    private readonly ConcurrentStack<UndoableAction> _redoStack = new();
    private readonly object _lock = new();
    private int _maxHistorySize = 50;
    private bool _disposed;

    /// <inheritdoc/>
    public bool CanUndo => !_undoStack.IsEmpty;

    /// <inheritdoc/>
    public bool CanRedo => !_redoStack.IsEmpty;

    /// <inheritdoc/>
    public string? NextUndoDescription
    {
        get
        {
            if (_undoStack.TryPeek(out var action))
            {
                return action.Description;
            }
            return null;
        }
    }

    /// <inheritdoc/>
    public string? NextRedoDescription
    {
        get
        {
            if (_redoStack.TryPeek(out var action))
            {
                return action.Description;
            }
            return null;
        }
    }

    /// <inheritdoc/>
    public int MaxHistorySize
    {
        get => _maxHistorySize;
        set => _maxHistorySize = Math.Max(1, Math.Min(value, 500));
    }

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public void RecordAction(UndoableAction action)
    {
        if (action == null) return;

        lock (_lock)
        {
            _undoStack.Push(action);

            // Clear redo stack when new action is recorded
            _redoStack.Clear();

            // Trim history if needed
            TrimHistory();
        }

        OnStateChanged();
    }

    /// <inheritdoc/>
    public void RecordAction(string descriptionKey, Func<Task> undoAction, string category = "General")
    {
        var action = new UndoableAction
        {
            DescriptionKey = descriptionKey,
            Description = GetLocalizedDescription(descriptionKey),
            UndoAction = undoAction,
            Category = category
        };

        RecordAction(action);
    }

    /// <inheritdoc/>
    public async Task<bool> UndoAsync()
    {
        UndoableAction? action = null;

        lock (_lock)
        {
            if (!_undoStack.TryPop(out action))
            {
                return false;
            }
        }

        if (action?.UndoAction == null)
        {
            return false;
        }

        try
        {
            await action.UndoAction();

            lock (_lock)
            {
                _redoStack.Push(action);
            }

            OnStateChanged();
            return true;
        }
        catch
        {
            // If undo fails, put action back
            lock (_lock)
            {
                _undoStack.Push(action);
            }
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RedoAsync()
    {
        UndoableAction? action = null;

        lock (_lock)
        {
            if (!_redoStack.TryPop(out action))
            {
                return false;
            }
        }

        if (action?.DoAction == null)
        {
            // If no DoAction, just move back to undo stack
            lock (_lock)
            {
                _undoStack.Push(action!);
            }
            OnStateChanged();
            return true;
        }

        try
        {
            await action.DoAction();

            lock (_lock)
            {
                _undoStack.Push(action);
            }

            OnStateChanged();
            return true;
        }
        catch
        {
            // If redo fails, put action back
            lock (_lock)
            {
                _redoStack.Push(action);
            }
            return false;
        }
    }

    /// <inheritdoc/>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        OnStateChanged();
    }

    /// <inheritdoc/>
    public IReadOnlyList<UndoableAction> GetUndoHistory()
    {
        lock (_lock)
        {
            return _undoStack.ToList().AsReadOnly();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<UndoableAction> GetRedoHistory()
    {
        lock (_lock)
        {
            return _redoStack.ToList().AsReadOnly();
        }
    }

    private void TrimHistory()
    {
        // Already under lock
        while (_undoStack.Count > _maxHistorySize)
        {
            var tempStack = new Stack<UndoableAction>();

            // Pop all but the oldest
            while (_undoStack.Count > 1)
            {
                if (_undoStack.TryPop(out var item))
                {
                    tempStack.Push(item);
                }
            }

            // Discard the oldest
            _undoStack.TryPop(out _);

            // Push back
            while (tempStack.Count > 0)
            {
                _undoStack.Push(tempStack.Pop());
            }

            if (_undoStack.Count <= _maxHistorySize) break;
        }
    }

    private static string GetLocalizedDescription(string key)
    {
        try
        {
            // Try to get localized string from resources
            var resourceManager = Resources.Resources.ResourceManager;
            var localized = resourceManager.GetString(key);
            return localized ?? key;
        }
        catch
        {
            return key;
        }
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Releases all resources used by the UndoService.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            lock (_lock)
            {
                _undoStack.Clear();
                _redoStack.Clear();
                StateChanged = null;
            }
        }

        _disposed = true;
    }
}
