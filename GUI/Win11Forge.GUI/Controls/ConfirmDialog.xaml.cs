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
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Dialog type for visual styling.
/// </summary>
public enum ConfirmDialogType
{
    Question,
    Warning,
    Danger
}

/// <summary>
/// Confirmation dialog control for destructive actions.
/// </summary>
public partial class ConfirmDialog : UserControl
{
    public event EventHandler<bool>? ResultSelected;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Set focus to the Cancel button by default (safer option)
        CancelButton.Focus();
    }

    /// <summary>
    /// Configures the dialog with message and styling.
    /// </summary>
    public void Configure(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        ConfirmDialogType dialogType = ConfirmDialogType.Question)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        // Set icon and color based on type
        switch (dialogType)
        {
            case ConfirmDialogType.Warning:
                DialogIcon.Kind = PackIconKind.AlertCircle;
                DialogIcon.Foreground = Application.Current.TryFindResource("WarningIconBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(230, 81, 0));
                break;

            case ConfirmDialogType.Danger:
                DialogIcon.Kind = PackIconKind.AlertOctagon;
                DialogIcon.Foreground = Application.Current.TryFindResource("ErrorIconBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(211, 47, 47));
                ConfirmButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                ConfirmButton.Background = Application.Current.TryFindResource("StatusFailedBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(244, 67, 54));
                break;

            case ConfirmDialogType.Question:
            default:
                DialogIcon.Kind = PackIconKind.HelpCircle;
                DialogIcon.Foreground = Application.Current.TryFindResource("PrimaryHueMidBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(103, 58, 183));
                break;
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        ResultSelected?.Invoke(this, true);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultSelected?.Invoke(this, false);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ResultSelected?.Invoke(this, false);
            DialogHost.CloseDialogCommand.Execute(null, null);
            e.Handled = true;
        }
    }
}
