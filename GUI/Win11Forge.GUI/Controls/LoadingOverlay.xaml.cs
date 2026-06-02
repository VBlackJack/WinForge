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
using System.Windows.Media.Animation;
using Win11Forge.GUI.Helpers;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// A loading overlay control that blocks user interaction while showing progress.
/// Supports both indeterminate and determinate progress modes.
/// </summary>
public partial class LoadingOverlay : UserControl
{
    /// <summary>
    /// Event raised when the cancel button is clicked.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Identifies the Message dependency property.
    /// </summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    /// <summary>
    /// Gets or sets the main loading message.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Identifies the SubMessage dependency property.
    /// </summary>
    public static readonly DependencyProperty SubMessageProperty =
        DependencyProperty.Register(
            nameof(SubMessage),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty, OnSubMessageChanged));

    /// <summary>
    /// Gets or sets the secondary message (e.g., "This may take a moment").
    /// </summary>
    public string SubMessage
    {
        get => (string)GetValue(SubMessageProperty);
        set => SetValue(SubMessageProperty, value);
    }

    /// <summary>
    /// Identifies the Progress dependency property.
    /// </summary>
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(LoadingOverlay),
            new PropertyMetadata(0.0, OnProgressChanged));

    /// <summary>
    /// Gets or sets the progress value (0-100) for determinate mode.
    /// </summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Identifies the IsIndeterminate dependency property.
    /// </summary>
    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(true, OnIsIndeterminateChanged));

    /// <summary>
    /// Gets or sets whether the progress indicator is indeterminate.
    /// </summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// Identifies the CanCancel dependency property.
    /// </summary>
    public static readonly DependencyProperty CanCancelProperty =
        DependencyProperty.Register(
            nameof(CanCancel),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false, OnCanCancelChanged));

    /// <summary>
    /// Gets or sets whether the operation can be cancelled.
    /// </summary>
    public bool CanCancel
    {
        get => (bool)GetValue(CanCancelProperty);
        set => SetValue(CanCancelProperty, value);
    }

    /// <summary>
    /// Identifies the ProgressText dependency property.
    /// </summary>
    public static readonly DependencyProperty ProgressTextValueProperty =
        DependencyProperty.Register(
            nameof(ProgressTextValue),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty, OnProgressTextChanged));

    /// <summary>
    /// Gets or sets the progress text (e.g., "3 of 10").
    /// </summary>
    public string ProgressTextValue
    {
        get => (string)GetValue(ProgressTextValueProperty);
        set => SetValue(ProgressTextValueProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the loading overlay with the specified message.
    /// </summary>
    /// <param name="message">The loading message to display.</param>
    /// <param name="subMessage">Optional secondary message.</param>
    public void Show(string message, string? subMessage = null)
    {
        Message = message;
        SubMessage = subMessage ?? string.Empty;
        IsIndeterminate = true;

        if (App.ReducedMotion)
        {
            Opacity = 1;
        }
        else
        {
            AnimationHelper.FadeIn(this);
        }

        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Shows the loading overlay with determinate progress.
    /// </summary>
    /// <param name="message">The loading message to display.</param>
    /// <param name="progress">Initial progress value (0-100).</param>
    /// <param name="progressText">Optional progress text (e.g., "3 of 10").</param>
    public void ShowWithProgress(string message, double progress, string? progressText = null)
    {
        Message = message;
        Progress = progress;
        ProgressTextValue = progressText ?? string.Empty;
        IsIndeterminate = false;

        if (App.ReducedMotion)
        {
            Opacity = 1;
        }
        else
        {
            AnimationHelper.FadeIn(this);
        }

        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the loading overlay.
    /// </summary>
    public void Hide()
    {
        if (App.ReducedMotion)
        {
            Visibility = Visibility.Collapsed;
        }
        else
        {
            DoubleAnimation fadeOut = Helpers.AnimationHelper.CreateFadeAnimation(1.0, 0.0, 200);
            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                BeginAnimation(OpacityProperty, null);
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    /// <summary>
    /// Updates the progress value and optional text.
    /// </summary>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="progressText">Optional progress text.</param>
    public void UpdateProgress(double progress, string? progressText = null)
    {
        Progress = progress;
        if (progressText != null)
        {
            ProgressTextValue = progressText;
        }
    }

    /// <summary>
    /// Updates the loading message.
    /// </summary>
    /// <param name="message">New message to display.</param>
    public void UpdateMessage(string message)
    {
        Message = message;
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            overlay.MessageText.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private static void OnSubMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            string text = e.NewValue as string ?? string.Empty;
            overlay.SubMessageText.Text = text;
            overlay.SubMessageText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay && e.NewValue is double progress)
        {
            overlay.DeterminateProgress.Value = progress;
        }
    }

    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay && e.NewValue is bool isIndeterminate)
        {
            overlay.IndeterminateProgress.Visibility = isIndeterminate ? Visibility.Visible : Visibility.Collapsed;
            overlay.DeterminateProgress.Visibility = isIndeterminate ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnCanCancelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay && e.NewValue is bool canCancel)
        {
            overlay.CancelButton.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnProgressTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            string text = e.NewValue as string ?? string.Empty;
            overlay.ProgressText.Text = text;
            overlay.ProgressText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
