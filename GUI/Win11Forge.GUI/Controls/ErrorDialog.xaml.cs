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

using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Services;
using Res = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Error dialog control with actionable options and recovery guidance.
/// </summary>
public partial class ErrorDialog : UserControl
{
    private string? _helpUrl;
    private string? _errorCode;
    private const string GitHubIssuesUrl = "https://github.com/VBlackJack/Win11Forge/issues/new";

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
        ConfigureAdvanced(title, message, details, showRetry, helpUrl, null, null, null);
    }

    /// <summary>
    /// Configures the dialog with full error information including recovery suggestions.
    /// </summary>
    public void ConfigureAdvanced(
        string title,
        string message,
        string? details = null,
        bool showRetry = false,
        string? helpUrl = null,
        string? errorCode = null,
        IEnumerable<string>? recoverySuggestions = null,
        string? offlineHelpText = null)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        _helpUrl = helpUrl;
        _errorCode = errorCode;

        // Show error code badge if provided
        if (!string.IsNullOrEmpty(errorCode))
        {
            ErrorCodeBadge.Visibility = Visibility.Visible;
            ErrorCodeText.Text = errorCode;
        }

        // Show recovery suggestions if provided
        if (recoverySuggestions != null)
        {
            var suggestions = recoverySuggestions.ToList();
            if (suggestions.Count > 0)
            {
                RecoverySuggestionsPanel.Visibility = Visibility.Visible;
                RecoverySuggestionsList.ItemsSource = suggestions;
            }
        }
        else
        {
            // Auto-generate recovery suggestions based on error type
            var autoSuggestions = GenerateRecoverySuggestions(title, message, details);
            if (autoSuggestions.Count > 0)
            {
                RecoverySuggestionsPanel.Visibility = Visibility.Visible;
                RecoverySuggestionsList.ItemsSource = autoSuggestions;
            }
        }

        // Show offline help text if provided
        if (!string.IsNullOrEmpty(offlineHelpText))
        {
            OfflineHelpPanel.Visibility = Visibility.Visible;
            OfflineHelpText.Text = offlineHelpText;
        }

        if (!string.IsNullOrEmpty(details))
        {
            DetailsExpander.Visibility = Visibility.Visible;
            DetailsText.Text = details;
            ReportIssueButton.Visibility = Visibility.Visible;
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

    /// <summary>
    /// Generates automatic recovery suggestions based on error content.
    /// </summary>
    private List<string> GenerateRecoverySuggestions(string title, string message, string? details)
    {
        var suggestions = new List<string>();
        var lowerMessage = (message + " " + (details ?? "")).ToLowerInvariant();
        var lowerTitle = title.ToLowerInvariant();

        // Network-related errors
        if (lowerMessage.Contains("network") || lowerMessage.Contains("internet") ||
            lowerMessage.Contains("connection") || lowerMessage.Contains("timeout") ||
            lowerMessage.Contains("download") || lowerMessage.Contains("fetch"))
        {
            suggestions.Add(Res.Recovery_CheckInternet);
            suggestions.Add(Res.Recovery_CheckFirewall);
            suggestions.Add(Res.Recovery_TryAgainLater);
        }

        // Permission/Admin errors
        if (lowerMessage.Contains("access denied") || lowerMessage.Contains("permission") ||
            lowerMessage.Contains("administrator") || lowerMessage.Contains("elevated") ||
            lowerMessage.Contains("unauthorized"))
        {
            suggestions.Add(Res.Recovery_RunAsAdmin);
            suggestions.Add(Res.Recovery_CheckPermissions);
        }

        // File/Path errors
        if (lowerMessage.Contains("file not found") || lowerMessage.Contains("path") ||
            lowerMessage.Contains("directory") || lowerMessage.Contains("does not exist"))
        {
            suggestions.Add(Res.Recovery_CheckPath);
            suggestions.Add(Res.Recovery_ReinstallApp);
        }

        // Installation errors
        if (lowerTitle.Contains("install") || lowerMessage.Contains("install") ||
            lowerMessage.Contains("winget") || lowerMessage.Contains("chocolatey"))
        {
            suggestions.Add(Res.Recovery_CheckPrerequisites);
            suggestions.Add(Res.Recovery_RetryInstall);
            suggestions.Add(Res.Recovery_ManualInstall);
        }

        // PowerShell errors
        if (lowerMessage.Contains("powershell") || lowerMessage.Contains("script") ||
            lowerMessage.Contains("execution policy"))
        {
            suggestions.Add(Res.Recovery_CheckPowerShell);
            suggestions.Add(Res.Recovery_UpdatePowerShell);
        }

        // If no specific suggestions, add generic ones
        if (suggestions.Count == 0)
        {
            suggestions.Add(Res.Recovery_Generic_Retry);
            suggestions.Add(Res.Recovery_Generic_Restart);
            suggestions.Add(Res.Recovery_Generic_CheckLogs);
        }

        return suggestions;
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
            if (!string.IsNullOrEmpty(_errorCode))
            {
                details += $"\n\nError Code: {_errorCode}";
            }
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

    private void ReportIssueButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Build issue body with error details
            var issueTitle = WebUtility.UrlEncode($"Error: {TitleText.Text}");
            var issueBody = WebUtility.UrlEncode(
                $"## Error Details\n\n" +
                $"**Title:** {TitleText.Text}\n" +
                $"**Message:** {MessageText.Text}\n" +
                (!string.IsNullOrEmpty(_errorCode) ? $"**Error Code:** {_errorCode}\n" : "") +
                $"\n## Technical Details\n\n```\n{DetailsText.Text}\n```\n\n" +
                $"## Environment\n\n" +
                $"- OS: Windows {Environment.OSVersion.Version}\n" +
                $"- .NET: {Environment.Version}\n" +
                $"- Win11Forge Version: 3.2.1\n"
            );

            var url = $"{GitHubIssuesUrl}?title={issueTitle}&body={issueBody}";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail - fallback to just opening issues page
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/VBlackJack/Win11Forge/issues",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
