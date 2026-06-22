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

using System.Collections.ObjectModel;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Represents an error entry in the history.
/// </summary>
public class ErrorHistoryEntry
{
    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// Error severity level.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>
    /// Short error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Detailed error information (stack trace, etc.).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Source/context of the error (e.g., "Installation", "Detection").
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Application name if error is app-specific.
    /// </summary>
    public string? ApplicationName { get; init; }

    /// <summary>
    /// Formatted timestamp for display.
    /// </summary>
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");

    /// <summary>
    /// Formatted date for grouping.
    /// </summary>
    public string FormattedDate => Timestamp.ToString("yyyy-MM-dd");
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    Warning,
    Error,
    Critical
}

/// <summary>
/// Service for tracking error history.
/// </summary>
public interface IErrorHistoryService
{
    /// <summary>
    /// Observable collection of recent errors.
    /// </summary>
    ObservableCollection<ErrorHistoryEntry> Errors { get; }

    /// <summary>
    /// Number of errors in history.
    /// </summary>
    int ErrorCount { get; }

    /// <summary>
    /// Whether there are any errors in history.
    /// </summary>
    bool HasErrors { get; }

    /// <summary>
    /// Event raised when error count changes.
    /// </summary>
    event EventHandler? ErrorCountChanged;

    /// <summary>
    /// Adds an error to the history.
    /// </summary>
    void AddError(string message, string? details = null, string? source = null, string? applicationName = null, ErrorSeverity severity = ErrorSeverity.Error);

    /// <summary>
    /// Clears all errors from history.
    /// </summary>
    void ClearErrors();
}

/// <summary>
/// Implementation of error history service.
/// Keeps track of the last N errors (default 50).
/// </summary>
public class ErrorHistoryService : IErrorHistoryService
{
    private const int MaxErrorCount = 50;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public ObservableCollection<ErrorHistoryEntry> Errors { get; } = [];

    /// <inheritdoc/>
    public int ErrorCount => Errors.Count;

    /// <inheritdoc/>
    public bool HasErrors => Errors.Count > 0;

    /// <inheritdoc/>
    public event EventHandler? ErrorCountChanged;

    /// <inheritdoc/>
    public void AddError(string message, string? details = null, string? source = null, string? applicationName = null, ErrorSeverity severity = ErrorSeverity.Error)
    {
        ErrorHistoryEntry entry = new ErrorHistoryEntry
        {
            Message = message,
            Details = details,
            Source = source,
            ApplicationName = applicationName,
            Severity = severity,
            Timestamp = DateTime.Now
        };

        lock (_lock)
        {
            // Insert at the beginning (most recent first)
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Errors.Insert(0, entry);

                // Remove oldest entries if over limit
                while (Errors.Count > MaxErrorCount)
                {
                    Errors.RemoveAt(Errors.Count - 1);
                }

                ErrorCountChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    /// <inheritdoc/>
    public void ClearErrors()
    {
        lock (_lock)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Errors.Clear();
                ErrorCountChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}
