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
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Loc = Win11Forge.GUI.Resources.Resources;

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
/// Uses WPF UI ContentDialog for Fluent Design modal dialogs.
/// </summary>
public class DialogService : IDialogService
{
    private ContentDialogHost? _dialogHost;

    /// <summary>
    /// Sets the dialog host used for ContentDialogs.
    /// Call this from MainWindow after initialization.
    /// </summary>
    public void SetDialogHost(ContentDialogHost dialogHost)
    {
        _dialogHost = dialogHost;
    }

    /// <inheritdoc/>
    public async Task<DialogAction> ShowErrorAsync(
        string title,
        string message,
        string? details = null,
        bool showRetry = false,
        string? helpUrl = null)
    {
        if (_dialogHost == null)
        {
            System.Windows.MessageBox.Show(
                $"{message}\n\n{details}",
                title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return DialogAction.Ok;
        }

        try
        {
            var content = new StackPanel();
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (!string.IsNullOrEmpty(details))
            {
                content.Children.Add(new Expander
                {
                    Header = Loc.Common_Details ?? "Details",
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = details,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12
                    }
                });
            }

            var dialog = new ContentDialog(_dialogHost)
            {
                Title = title,
                Content = content,
                CloseButtonText = Loc.Common_OK ?? "OK"
            };
            if (showRetry)
            {
                dialog.PrimaryButtonText = Loc.Common_TryAgain ?? "Retry";
            }

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? DialogAction.Retry : DialogAction.Ok;
        }
        catch
        {
            System.Windows.MessageBox.Show(
                $"{message}\n\n{details}",
                title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return DialogAction.Ok;
        }
    }

    /// <inheritdoc/>
    public async Task ShowInfoAsync(string title, string message)
    {
        if (_dialogHost == null)
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new ContentDialog(_dialogHost)
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = Loc.Common_OK ?? "OK"
            };
            await dialog.ShowAsync();
        }
        catch
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    /// <inheritdoc/>
    public async Task ShowSuccessAsync(string title, string message)
    {
        if (_dialogHost == null)
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new ContentDialog(_dialogHost)
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = Loc.Common_OK ?? "OK"
            };
            await dialog.ShowAsync();
        }
        catch
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShowConfirmAsync(string title, string message, string? confirmText = null, string? cancelText = null)
    {
        if (_dialogHost == null)
        {
            return System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.OK;
        }

        try
        {
            var dialog = new ContentDialog(_dialogHost)
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = confirmText ?? Loc.Common_OK ?? "OK",
                CloseButtonText = cancelText ?? Loc.Common_Cancel ?? "Cancel"
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        catch
        {
            return System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.OK;
        }
    }

    /// <inheritdoc/>
    public async Task<bool?> ShowYesNoCancelAsync(
        string title,
        string message,
        string? yesText = null,
        string? noText = null,
        string? cancelText = null)
    {
        if (_dialogHost == null)
        {
            return ShowYesNoCancelMessageBox(title, message);
        }

        try
        {
            var dialog = new ContentDialog(_dialogHost)
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = yesText ?? Loc.Common_Yes ?? "Yes",
                SecondaryButtonText = noText ?? Loc.Common_No ?? "No",
                CloseButtonText = cancelText ?? Loc.Common_Cancel ?? "Cancel"
            };

            var result = await dialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => true,
                ContentDialogResult.Secondary => false,
                _ => null
            };
        }
        catch
        {
            return ShowYesNoCancelMessageBox(title, message);
        }
    }

    private static bool? ShowYesNoCancelMessageBox(string title, string message)
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        return result switch
        {
            System.Windows.MessageBoxResult.Yes => true,
            System.Windows.MessageBoxResult.No => false,
            _ => null
        };
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

    /// <summary>
    /// Shows a three-choice confirmation dialog and returns true for Yes, false for No, or null for Cancel.
    /// </summary>
    Task<bool?> ShowYesNoCancelAsync(
        string title,
        string message,
        string? yesText = null,
        string? noText = null,
        string? cancelText = null);
}
