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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Log levels for the application logging system.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Interface for application logging service.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs a debug message.
    /// </summary>
    void LogDebug(string message, [CallerMemberName] string? caller = null);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message, [CallerMemberName] string? caller = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message, [CallerMemberName] string? caller = null);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void LogError(string message, Exception? exception = null, [CallerMemberName] string? caller = null);

    /// <summary>
    /// Logs a message with the specified level.
    /// </summary>
    void Log(LogLevel level, string message, Exception? exception = null, [CallerMemberName] string? caller = null);
}

/// <summary>
/// Default logging service implementation using Debug output.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly string _categoryName;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of LoggingService.
    /// </summary>
    /// <param name="categoryName">Optional category name for log entries.</param>
    public LoggingService(string? categoryName = null)
    {
        _categoryName = categoryName ?? "Win11Forge";
    }

    /// <inheritdoc/>
    public void LogDebug(string message, [CallerMemberName] string? caller = null)
    {
        Log(LogLevel.Debug, message, null, caller);
    }

    /// <inheritdoc/>
    public void LogInfo(string message, [CallerMemberName] string? caller = null)
    {
        Log(LogLevel.Info, message, null, caller);
    }

    /// <inheritdoc/>
    public void LogWarning(string message, [CallerMemberName] string? caller = null)
    {
        Log(LogLevel.Warning, message, null, caller);
    }

    /// <inheritdoc/>
    public void LogError(string message, Exception? exception = null, [CallerMemberName] string? caller = null)
    {
        Log(LogLevel.Error, message, exception, caller);
    }

    /// <inheritdoc/>
    public void Log(LogLevel level, string message, Exception? exception = null, [CallerMemberName] string? caller = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var levelStr = level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };

        var callerInfo = string.IsNullOrEmpty(caller) ? "" : $"[{caller}] ";
        var logMessage = $"[{timestamp}] [{levelStr}] [{_categoryName}] {callerInfo}{message}";

        if (exception != null)
        {
            logMessage += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
            if (exception.StackTrace != null)
            {
                logMessage += $"\n  StackTrace: {exception.StackTrace}";
            }
        }

        lock (_lock)
        {
            Debug.WriteLine(logMessage);
        }
    }
}

/// <summary>
/// Factory for creating category-specific loggers.
/// </summary>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    ILoggingService CreateLogger(string categoryName);

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    ILoggingService CreateLogger<T>();
}

/// <summary>
/// Default implementation of logger factory.
/// </summary>
public class LoggerFactory : ILoggerFactory
{
    /// <inheritdoc/>
    public ILoggingService CreateLogger(string categoryName)
    {
        return new LoggingService(categoryName);
    }

    /// <inheritdoc/>
    public ILoggingService CreateLogger<T>()
    {
        return new LoggingService(typeof(T).Name);
    }
}
