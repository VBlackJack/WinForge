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

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Type of skeleton loader layout.
/// </summary>
public enum SkeletonType
{
    /// <summary>
    /// Default card layout with title and content lines.
    /// </summary>
    Card,

    /// <summary>
    /// List item layout with icon placeholder and text.
    /// </summary>
    ListItem,

    /// <summary>
    /// Table row layout with multiple columns.
    /// </summary>
    TableRow,

    /// <summary>
    /// Dashboard statistic card with number and label.
    /// </summary>
    StatCard,

    /// <summary>
    /// Profile/Avatar layout with circular image and text.
    /// </summary>
    Profile
}

/// <summary>
/// A contextual skeleton loader placeholder for loading states.
/// Supports multiple layout types for different UI contexts.
/// </summary>
public partial class SkeletonLoader : UserControl
{
    /// <summary>
    /// Identifies the SkeletonType dependency property.
    /// </summary>
    public static readonly DependencyProperty SkeletonTypeProperty =
        DependencyProperty.Register(
            nameof(SkeletonType),
            typeof(SkeletonType),
            typeof(SkeletonLoader),
            new PropertyMetadata(SkeletonType.Card, OnSkeletonTypeChanged));

    /// <summary>
    /// Gets or sets the type of skeleton layout to display.
    /// </summary>
    public SkeletonType SkeletonType
    {
        get => (SkeletonType)GetValue(SkeletonTypeProperty);
        set => SetValue(SkeletonTypeProperty, value);
    }

    /// <summary>
    /// Identifies the ItemCount dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemCountProperty =
        DependencyProperty.Register(
            nameof(ItemCount),
            typeof(int),
            typeof(SkeletonLoader),
            new PropertyMetadata(1, OnSkeletonTypeChanged));

    /// <summary>
    /// Gets or sets the number of skeleton items to display.
    /// </summary>
    public int ItemCount
    {
        get => (int)GetValue(ItemCountProperty);
        set => SetValue(ItemCountProperty, value);
    }

    /// <summary>
    /// Identifies the ShowAnimation dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowAnimationProperty =
        DependencyProperty.Register(
            nameof(ShowAnimation),
            typeof(bool),
            typeof(SkeletonLoader),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets whether to show the pulsing animation.
    /// Respects reduced motion preferences when true.
    /// </summary>
    public bool ShowAnimation
    {
        get => (bool)GetValue(ShowAnimationProperty);
        set => SetValue(ShowAnimationProperty, value);
    }

    public SkeletonLoader()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateVisibility();
    }

    private static void OnSkeletonTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkeletonLoader loader)
        {
            loader.UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        // Update content visibility based on SkeletonType
        // Find the named elements in the visual tree
        var cardContent = FindName("CardContent") as UIElement;
        var listItemContent = FindName("ListItemContent") as UIElement;
        var tableRowContent = FindName("TableRowContent") as UIElement;
        var statCardContent = FindName("StatCardContent") as UIElement;
        var profileContent = FindName("ProfileContent") as UIElement;

        if (cardContent != null) cardContent.Visibility = SkeletonType == SkeletonType.Card ? Visibility.Visible : Visibility.Collapsed;
        if (listItemContent != null) listItemContent.Visibility = SkeletonType == SkeletonType.ListItem ? Visibility.Visible : Visibility.Collapsed;
        if (tableRowContent != null) tableRowContent.Visibility = SkeletonType == SkeletonType.TableRow ? Visibility.Visible : Visibility.Collapsed;
        if (statCardContent != null) statCardContent.Visibility = SkeletonType == SkeletonType.StatCard ? Visibility.Visible : Visibility.Collapsed;
        if (profileContent != null) profileContent.Visibility = SkeletonType == SkeletonType.Profile ? Visibility.Visible : Visibility.Collapsed;
    }
}
