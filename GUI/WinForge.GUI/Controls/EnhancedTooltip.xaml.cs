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

namespace Win11Forge.GUI.Controls;

/// <summary>
/// An enhanced tooltip control with title, description, and optional keyboard shortcut.
/// </summary>
public partial class EnhancedTooltip : UserControl
{
    /// <summary>
    /// Identifies the Title dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(EnhancedTooltip),
            new PropertyMetadata(null));

    /// <summary>
    /// Identifies the Description dependency property.
    /// </summary>
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description),
            typeof(string),
            typeof(EnhancedTooltip),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the Shortcut dependency property.
    /// </summary>
    public static readonly DependencyProperty ShortcutProperty =
        DependencyProperty.Register(
            nameof(Shortcut),
            typeof(string),
            typeof(EnhancedTooltip),
            new PropertyMetadata(null));

    /// <summary>
    /// Identifies the IconKind dependency property.
    /// </summary>
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            nameof(IconKind),
            typeof(SymbolRegular),
            typeof(EnhancedTooltip),
            new PropertyMetadata(SymbolRegular.Info24));

    /// <summary>
    /// Identifies the ShowIcon dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(
            nameof(ShowIcon),
            typeof(bool),
            typeof(EnhancedTooltip),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets the tooltip title.
    /// </summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the tooltip description.
    /// </summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets the keyboard shortcut hint.
    /// </summary>
    public string? Shortcut
    {
        get => (string?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon kind.
    /// </summary>
    public SymbolRegular IconKind
    {
        get => (SymbolRegular)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the icon.
    /// </summary>
    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public EnhancedTooltip()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates an enhanced tooltip for use with ToolTip property.
    /// </summary>
    public static EnhancedTooltip Create(string description, string? title = null, string? shortcut = null, SymbolRegular icon = SymbolRegular.Info24)
    {
        return new EnhancedTooltip
        {
            Title = title,
            Description = description,
            Shortcut = shortcut,
            IconKind = icon,
            ShowIcon = title != null
        };
    }
}
