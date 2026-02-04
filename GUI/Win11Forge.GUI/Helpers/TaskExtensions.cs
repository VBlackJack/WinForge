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

namespace Win11Forge.GUI.Helpers;

/// <summary>
/// Extension methods for safe Task handling.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a fire-and-forget task with proper exception handling.
    /// Prevents unobserved task exceptions while still logging errors.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="onException">Optional callback for exception handling.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on captured synchronization context.</param>
    public static async void SafeFireAndForget(
        this Task task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex)
        {
            HandleException(ex, onException);
        }
    }

    /// <summary>
    /// Safely executes a fire-and-forget task with proper exception handling.
    /// Prevents unobserved task exceptions while still logging errors.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <param name="task">The task to execute.</param>
    /// <param name="onException">Optional callback for exception handling.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on captured synchronization context.</param>
    public static async void SafeFireAndForget<T>(
        this Task<T> task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex)
        {
            HandleException(ex, onException);
        }
    }

    /// <summary>
    /// Safely executes a ValueTask fire-and-forget with proper exception handling.
    /// </summary>
    /// <param name="task">The ValueTask to execute.</param>
    /// <param name="onException">Optional callback for exception handling.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on captured synchronization context.</param>
    public static async void SafeFireAndForget(
        this ValueTask task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex)
        {
            HandleException(ex, onException);
        }
    }

    /// <summary>
    /// Safely executes a ValueTask fire-and-forget with proper exception handling.
    /// </summary>
    /// <typeparam name="T">The result type of the ValueTask.</typeparam>
    /// <param name="task">The ValueTask to execute.</param>
    /// <param name="onException">Optional callback for exception handling.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on captured synchronization context.</param>
    public static async void SafeFireAndForget<T>(
        this ValueTask<T> task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex)
        {
            HandleException(ex, onException);
        }
    }

    /// <summary>
    /// Common exception handling logic for fire-and-forget operations.
    /// Safety note: async void methods can crash the app if exceptions escape.
    /// This method ensures all exceptions are properly caught and logged.
    /// </summary>
    /// <param name="ex">The exception that was caught.</param>
    /// <param name="onException">Optional callback for exception handling.</param>
    private static void HandleException(Exception ex, Action<Exception>? onException)
    {
        // Expected exceptions during normal operation - no logging needed
        if (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        // Always log to debug output for diagnostics first (guaranteed to not throw)
        System.Diagnostics.Debug.WriteLine($"[SafeFireAndForget] Unhandled exception: {ex}");

        // Invoke callback if provided - wrap in try-catch to prevent callback exceptions
        // from crashing the app (since we're in async void context)
        if (onException != null)
        {
            try
            {
                onException.Invoke(ex);
            }
            catch (Exception callbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeFireAndForget] Exception in callback: {callbackEx}");
            }
        }
    }
}
