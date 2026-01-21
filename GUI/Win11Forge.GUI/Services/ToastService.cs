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

using System.Windows;
using MaterialDesignThemes.Wpf;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Toast notification severity levels.
/// </summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Service for displaying toast/snackbar notifications.
/// </summary>
public class ToastService : IToastService
{
    private ISnackbarMessageQueue? _messageQueue;

    /// <summary>
    /// Sets the snackbar message queue from the main window.
    /// </summary>
    public void SetMessageQueue(ISnackbarMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    /// <inheritdoc/>
    public void Show(string message, ToastLevel level = ToastLevel.Info, int durationMs = 3000)
    {
        if (_messageQueue == null) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _messageQueue.Enqueue(
                message,
                null,
                null,
                null,
                false,
                true,
                TimeSpan.FromMilliseconds(durationMs));
        });
    }

    /// <inheritdoc/>
    public void ShowWithAction(string message, string actionText, Action action, ToastLevel level = ToastLevel.Info)
    {
        if (_messageQueue == null) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _messageQueue.Enqueue(
                message,
                actionText,
                _ => action(),
                null,
                false,
                true,
                TimeSpan.FromSeconds(5));
        });
    }

    /// <inheritdoc/>
    public void ShowSuccess(string message)
    {
        Show(message, ToastLevel.Success, 2000);
    }

    /// <inheritdoc/>
    public void ShowError(string message)
    {
        Show(message, ToastLevel.Error, 5000);
    }

    /// <inheritdoc/>
    public void ShowWarning(string message)
    {
        Show(message, ToastLevel.Warning, 4000);
    }

    /// <inheritdoc/>
    public void ShowInfo(string message)
    {
        Show(message, ToastLevel.Info, 3000);
    }
}

/// <summary>
/// Interface for toast notification service.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Shows a toast notification.
    /// </summary>
    void Show(string message, ToastLevel level = ToastLevel.Info, int durationMs = 3000);

    /// <summary>
    /// Shows a toast with an action button.
    /// </summary>
    void ShowWithAction(string message, string actionText, Action action, ToastLevel level = ToastLevel.Info);

    /// <summary>
    /// Shows a success toast.
    /// </summary>
    void ShowSuccess(string message);

    /// <summary>
    /// Shows an error toast.
    /// </summary>
    void ShowError(string message);

    /// <summary>
    /// Shows a warning toast.
    /// </summary>
    void ShowWarning(string message);

    /// <summary>
    /// Shows an info toast.
    /// </summary>
    void ShowInfo(string message);
}
