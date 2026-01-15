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

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Represents a single breadcrumb item.
/// </summary>
public class BreadcrumbItem
{
    public string Label { get; set; } = string.Empty;
    public bool IsFirst { get; set; }
    public bool IsClickable { get; set; } = true;
    public string FontWeight => IsClickable ? "Normal" : "Bold";
    public ICommand? NavigateCommand { get; set; }
}

/// <summary>
/// Breadcrumb navigation control for multi-step flows.
/// </summary>
public partial class Breadcrumb : UserControl
{
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(ObservableCollection<BreadcrumbItem>),
            typeof(Breadcrumb),
            new PropertyMetadata(new ObservableCollection<BreadcrumbItem>(), OnItemsChanged));

    public ObservableCollection<BreadcrumbItem> Items
    {
        get => (ObservableCollection<BreadcrumbItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public Breadcrumb()
    {
        InitializeComponent();
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Breadcrumb breadcrumb && e.NewValue is ObservableCollection<BreadcrumbItem> items)
        {
            breadcrumb.BreadcrumbItems.ItemsSource = items;
        }
    }

    /// <summary>
    /// Sets breadcrumb items from a simple string array.
    /// </summary>
    public void SetItems(string[] labels, int currentIndex, Action<int>? navigateAction = null)
    {
        var items = new ObservableCollection<BreadcrumbItem>();

        for (int i = 0; i < labels.Length; i++)
        {
            var index = i;
            items.Add(new BreadcrumbItem
            {
                Label = labels[i],
                IsFirst = i == 0,
                IsClickable = i < currentIndex,
                NavigateCommand = navigateAction != null
                    ? new RelayCommand(() => navigateAction(index))
                    : null
            });
        }

        Items = items;
        BreadcrumbItems.ItemsSource = items;
    }
}
