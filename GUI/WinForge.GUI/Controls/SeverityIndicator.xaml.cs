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

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WinForge.GUI.Resources;
using Wpf.Ui.Controls;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.Controls;

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
        Application app = Application.Current;
        (SymbolRegular icon, Brush? background, Brush? border, Brush? foreground) = Severity switch
        {
            SeverityLevel.Success => (
                SymbolRegular.CheckmarkCircle24,
                app?.TryFindResource("SuccessBackgroundBrush") as Brush ?? ThemeFallbackBrushes.SuccessSubtleBackground,
                app?.TryFindResource("SuccessBorderBrush") as Brush ?? ThemeFallbackBrushes.Success,
                app?.TryFindResource("SuccessIconBrush") as Brush ?? ThemeFallbackBrushes.Success),
            SeverityLevel.Warning => (
                SymbolRegular.Warning24,
                app?.TryFindResource("WarningBackgroundBrush") as Brush ?? ThemeFallbackBrushes.WarningSubtleBackground,
                app?.TryFindResource("WarningBorderBrush") as Brush ?? ThemeFallbackBrushes.Warning,
                app?.TryFindResource("WarningIconBrush") as Brush ?? ThemeFallbackBrushes.Warning),
            SeverityLevel.Error => (
                SymbolRegular.DismissCircle24,
                app?.TryFindResource("ErrorBackgroundBrush") as Brush ?? ThemeFallbackBrushes.ErrorSubtleBackground,
                app?.TryFindResource("ErrorBorderBrush") as Brush ?? ThemeFallbackBrushes.Error,
                app?.TryFindResource("ErrorIconBrush") as Brush ?? ThemeFallbackBrushes.Error),
            SeverityLevel.Critical => (
                SymbolRegular.ShieldError24,
                ThemeFallbackBrushes.CriticalBackground,
                ThemeFallbackBrushes.Error,
                ThemeFallbackBrushes.Error),
            _ => (
                SymbolRegular.Info24,
                ThemeFallbackBrushes.InfoSubtleBackground,
                ThemeFallbackBrushes.Info,
                ThemeFallbackBrushes.Info)
        };

        SeverityIcon.Symbol = icon;
        SeverityIcon.Foreground = foreground;
        IndicatorBorder.Background = background;
        IndicatorBorder.BorderBrush = border;

        // Set AutomationProperties for accessibility (localized; screen readers must
        // not hear an English severity word in non-English UI cultures).
        string severityName = Severity switch
        {
            SeverityLevel.Success => Loc.Severity_Success,
            SeverityLevel.Warning => Loc.Severity_Warning,
            SeverityLevel.Error => Loc.Severity_Error,
            SeverityLevel.Critical => Loc.Severity_Critical,
            _ => Loc.Severity_Info
        };
        AutomationProperties.SetName(
            this,
            string.Format(CultureInfo.CurrentCulture, Loc.Severity_IndicatorNameFormat, severityName));
    }
}
