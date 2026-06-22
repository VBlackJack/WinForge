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

#nullable enable

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.UserControls;

/// <summary>
/// UserControl for editing Microsoft Store installation source configuration.
/// </summary>
public partial class StoreSourceEditor : UserControl
{
    private static readonly Regex StoreIdPattern = new(@"^[A-Za-z0-9]{12}$", RegexOptions.Compiled);

    #region Dependency Properties

    /// <summary>
    /// Identifies the IsSourceEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSourceEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSourceEnabled),
            typeof(bool),
            typeof(StoreSourceEditor),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets whether this source is enabled.
    /// </summary>
    public bool IsSourceEnabled
    {
        get => (bool)GetValue(IsSourceEnabledProperty);
        set => SetValue(IsSourceEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the StoreId dependency property.
    /// </summary>
    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId),
            typeof(string),
            typeof(StoreSourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStoreIdChanged));

    /// <summary>
    /// Gets or sets the Microsoft Store app ID.
    /// </summary>
    public string StoreId
    {
        get => (string)GetValue(StoreIdProperty);
        set => SetValue(StoreIdProperty, value);
    }

    /// <summary>
    /// Identifies the IsValid dependency property.
    /// </summary>
    public static readonly DependencyProperty IsValidProperty =
        DependencyProperty.Register(
            nameof(IsValid),
            typeof(bool),
            typeof(StoreSourceEditor),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets whether the current Store ID is valid.
    /// </summary>
    public bool IsValid
    {
        get => (bool)GetValue(IsValidProperty);
        private set => SetValue(IsValidProperty, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of StoreSourceEditor.
    /// </summary>
    public StoreSourceEditor()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles StoreId changes to validate format.
    /// </summary>
    private static void OnStoreIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StoreSourceEditor editor)
        {
            editor.ValidateStoreId();
        }
    }

    /// <summary>
    /// Validates the Store ID format and updates visual indicator.
    /// </summary>
    private void ValidateStoreId()
    {
        if (string.IsNullOrWhiteSpace(StoreId))
        {
            IsValid = false;
            ValidationIcon.Visibility = Visibility.Collapsed;
            return;
        }

        IsValid = StoreIdPattern.IsMatch(StoreId);
        ValidationIcon.Visibility = Visibility.Visible;

        if (IsValid)
        {
            ValidationIcon.Symbol = SymbolRegular.CheckmarkCircle24;
            ValidationIcon.Foreground = new SolidColorBrush(Colors.Green);
        }
        else
        {
            ValidationIcon.Symbol = SymbolRegular.ErrorCircle24;
            ValidationIcon.Foreground = new SolidColorBrush(Colors.Orange);
        }
    }
}
