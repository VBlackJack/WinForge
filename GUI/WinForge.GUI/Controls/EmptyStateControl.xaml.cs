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
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Unified empty state control for consistent empty state displays across the application.
/// Provides icon, title, optional subtitle, and optional action button.
/// </summary>
public partial class EmptyStateControl : UserControl
{
    /// <summary>
    /// Identifies the IconKind dependency property.
    /// </summary>
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            nameof(IconKind),
            typeof(SymbolRegular),
            typeof(EmptyStateControl),
            new PropertyMetadata(SymbolRegular.ErrorCircle24));

    /// <summary>
    /// Identifies the IconSize dependency property.
    /// </summary>
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(EmptyStateControl),
            new PropertyMetadata(64.0));

    /// <summary>
    /// Identifies the Title dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the Subtitle dependency property.
    /// </summary>
    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Identifies the IconAccessibilityName dependency property.
    /// </summary>
    public static readonly DependencyProperty IconAccessibilityNameProperty =
        DependencyProperty.Register(
            nameof(IconAccessibilityName),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata("Empty state icon"));

    /// <summary>
    /// Identifies the ActionText dependency property.
    /// </summary>
    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(
            nameof(ActionText),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Identifies the ActionCommand dependency property.
    /// </summary>
    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(
            nameof(ActionCommand),
            typeof(ICommand),
            typeof(EmptyStateControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Initializes a new instance of EmptyStateControl.
    /// </summary>
    public EmptyStateControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the icon kind to display.
    /// </summary>
    public SymbolRegular IconKind
    {
        get => (SymbolRegular)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon size (width and height).
    /// </summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the title text.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the optional subtitle text.
    /// </summary>
    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the accessibility name for the icon.
    /// </summary>
    public string IconAccessibilityName
    {
        get => (string)GetValue(IconAccessibilityNameProperty);
        set => SetValue(IconAccessibilityNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the optional action button text.
    /// </summary>
    public string? ActionText
    {
        get => (string?)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the optional action command.
    /// </summary>
    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }
}
