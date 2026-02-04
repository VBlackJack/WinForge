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
using Win11Forge.GUI.Controls;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Dialog type enumeration for error categorization.
/// </summary>
public enum DialogType
{
    Information,
    Success,
    Warning,
    Error
}

/// <summary>
/// Dialog result with user action.
/// </summary>
public enum DialogAction
{
    Ok,
    Cancel,
    Retry,
    Help
}

/// <summary>
/// Service for displaying consistent dialogs throughout the application.
/// Provides actionable error messages with retry and help options.
/// </summary>
public class DialogService : IDialogService
{
    /// <inheritdoc/>
    public async Task<DialogAction> ShowErrorAsync(
        string title,
        string message,
        string? details = null,
        bool showRetry = false,
        string? helpUrl = null)
    {
        var result = DialogAction.Ok;

        var dialog = new ErrorDialogContent
        {
            Title = title,
            Message = message,
            Details = details,
            ShowRetry = showRetry,
            HelpUrl = helpUrl
        };

        dialog.ActionSelected += (_, action) => result = action;

        try
        {
            await DialogHost.Show(dialog, "RootDialog");
        }
        catch
        {
            // Fallback to MessageBox if DialogHost unavailable
            MessageBox.Show(
                $"{message}\n\n{details}",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new InfoDialogContent
        {
            Title = title,
            Message = message,
            DialogType = DialogType.Information
        };

        try
        {
            await DialogHost.Show(dialog, "RootDialog");
        }
        catch
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <inheritdoc/>
    public async Task ShowSuccessAsync(string title, string message)
    {
        var dialog = new InfoDialogContent
        {
            Title = title,
            Message = message,
            DialogType = DialogType.Success
        };

        try
        {
            await DialogHost.Show(dialog, "RootDialog");
        }
        catch
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShowConfirmAsync(string title, string message, string? confirmText = null, string? cancelText = null)
    {
        var result = false;

        var dialog = new ConfirmDialog();
        dialog.Configure(
            title,
            message,
            confirmText ?? Resources.Resources.Common_OK,
            cancelText ?? Resources.Resources.Common_Cancel,
            ConfirmDialogType.Question);

        dialog.ResultSelected += (_, confirmed) => result = confirmed;

        try
        {
            await DialogHost.Show(dialog, "RootDialog");
        }
        catch
        {
            result = MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        return result;
    }
}

/// <summary>
/// Interface for dialog service.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows an error dialog with optional retry and help actions.
    /// </summary>
    Task<DialogAction> ShowErrorAsync(
        string title,
        string message,
        string? details = null,
        bool showRetry = false,
        string? helpUrl = null);

    /// <summary>
    /// Shows an information dialog.
    /// </summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Shows a success dialog.
    /// </summary>
    Task ShowSuccessAsync(string title, string message);

    /// <summary>
    /// Shows a confirmation dialog and returns user choice.
    /// </summary>
    Task<bool> ShowConfirmAsync(string title, string message, string? confirmText = null, string? cancelText = null);
}

/// <summary>
/// Content for error dialogs with actionable options.
/// </summary>
public class ErrorDialogContent
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool ShowRetry { get; set; }
    public string? HelpUrl { get; set; }

    public event EventHandler<DialogAction>? ActionSelected;

    public void SelectAction(DialogAction action)
    {
        ActionSelected?.Invoke(this, action);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }
}

/// <summary>
/// Content for info/success dialogs.
/// </summary>
public class InfoDialogContent
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DialogType DialogType { get; set; } = DialogType.Information;

    public void Close()
    {
        DialogHost.CloseDialogCommand.Execute(null, null);
    }
}

/// <summary>
/// Content for confirmation dialogs.
/// </summary>
public class ConfirmDialogContent
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ConfirmText { get; set; } = Resources.Resources.Common_OK;
    public string CancelText { get; set; } = Resources.Resources.Common_Cancel;

    public event EventHandler<bool>? ResultSelected;

    public void Confirm()
    {
        ResultSelected?.Invoke(this, true);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    public void Cancel()
    {
        ResultSelected?.Invoke(this, false);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }
}
