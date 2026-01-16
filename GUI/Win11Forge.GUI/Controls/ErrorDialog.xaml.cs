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
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Error dialog control with actionable options.
/// </summary>
public partial class ErrorDialog : UserControl
{
    private string? _helpUrl;

    public event EventHandler<DialogAction>? ActionSelected;

    public ErrorDialog()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Set focus to the OK button when dialog opens
        OkButton.Focus();
    }

    /// <summary>
    /// Configures the dialog with error information.
    /// </summary>
    public void Configure(string title, string message, string? details = null, bool showRetry = false, string? helpUrl = null)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        _helpUrl = helpUrl;

        if (!string.IsNullOrEmpty(details))
        {
            DetailsExpander.Visibility = Visibility.Visible;
            DetailsText.Text = details;
        }

        if (showRetry)
        {
            RetryButton.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrEmpty(helpUrl))
        {
            HelpButton.Visibility = Visibility.Visible;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ActionSelected?.Invoke(this, DialogAction.Ok);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        ActionSelected?.Invoke(this, DialogAction.Retry);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_helpUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _helpUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently fail
            }
        }
        ActionSelected?.Invoke(this, DialogAction.Help);
    }

    private void CopyDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var details = $"{TitleText.Text}\n\n{MessageText.Text}";
            if (!string.IsNullOrEmpty(DetailsText.Text))
            {
                details += $"\n\n--- Details ---\n{DetailsText.Text}";
            }
            Clipboard.SetText(details);

            // Provide visual feedback
            if (CopyDetailsButton.Content is string originalContent)
            {
                CopyDetailsButton.Content = "✓";
                CopyDetailsButton.IsEnabled = false;

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    CopyDetailsButton.Content = originalContent;
                    CopyDetailsButton.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
        }
        catch
        {
            // Silently fail
        }
    }
}
