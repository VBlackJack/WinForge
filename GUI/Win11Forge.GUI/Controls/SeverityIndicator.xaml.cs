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
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Severity levels for the indicator.
/// </summary>
public enum SeverityLevel
{
    /// <summary>Informational message.</summary>
    Info,
    /// <summary>Success message.</summary>
    Success,
    /// <summary>Warning message.</summary>
    Warning,
    /// <summary>Error message.</summary>
    Error,
    /// <summary>Critical error message.</summary>
    Critical
}

/// <summary>
/// A visual indicator control that displays messages with severity-based styling.
/// </summary>
public partial class SeverityIndicator : UserControl
{
    /// <summary>
    /// Identifies the Severity dependency property.
    /// </summary>
    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(
            nameof(Severity),
            typeof(SeverityLevel),
            typeof(SeverityIndicator),
            new PropertyMetadata(SeverityLevel.Info, OnSeverityChanged));

    /// <summary>
    /// Identifies the ShowIcon dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(
            nameof(ShowIcon),
            typeof(bool),
            typeof(SeverityIndicator),
            new PropertyMetadata(true, OnShowIconChanged));

    /// <summary>
    /// Gets or sets the severity level.
    /// </summary>
    public SeverityLevel Severity
    {
        get => (SeverityLevel)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the severity icon.
    /// </summary>
    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public SeverityIndicator()
    {
        InitializeComponent();
        UpdateSeverityAppearance();
    }

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SeverityIndicator indicator)
        {
            indicator.UpdateSeverityAppearance();
        }
    }

    private static void OnShowIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SeverityIndicator indicator)
        {
            indicator.SeverityIcon.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateSeverityAppearance()
    {
        var (icon, background, border, foreground) = GetSeverityStyles(Severity);

        SeverityIcon.Symbol = icon;
        SeverityIcon.Foreground = foreground;

        IndicatorBorder.Background = background;
        IndicatorBorder.BorderBrush = border;

        // Set AutomationProperties for accessibility
        AutomationProperties.SetName(this, $"{Severity} indicator");
    }

    private static (SymbolRegular Icon, Brush Background, Brush Border, Brush Foreground) GetSeverityStyles(SeverityLevel severity)
    {
        return severity switch
        {
            SeverityLevel.Success => (
                SymbolRegular.CheckmarkCircle24,
                new SolidColorBrush(Color.FromArgb(0x1E, 0x4C, 0xAF, 0x50)),
                new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            ),
            SeverityLevel.Warning => (
                SymbolRegular.Warning24,
                new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x98, 0x00)),
                new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D))
            ),
            SeverityLevel.Error => (
                SymbolRegular.DismissCircle24,
                new SolidColorBrush(Color.FromArgb(0x33, 0xF4, 0x43, 0x36)),
                new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
            ),
            SeverityLevel.Critical => (
                SymbolRegular.ShieldError24,
                new SolidColorBrush(Color.FromArgb(0x4D, 0xF4, 0x43, 0x36)),
                new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)),
                new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
            ),
            _ => ( // Info
                SymbolRegular.Info24,
                new SolidColorBrush(Color.FromArgb(0x1E, 0x21, 0x96, 0xF3)),
                new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3))
            )
        };
    }
}
