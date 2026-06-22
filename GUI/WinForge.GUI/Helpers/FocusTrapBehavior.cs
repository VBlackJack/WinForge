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

namespace Win11Forge.GUI.Helpers;

/// <summary>
/// Attached behavior that traps keyboard focus within a container element.
/// Essential for accessibility compliance in modal dialogs.
/// </summary>
public static class FocusTrapBehavior
{
    /// <summary>
    /// Identifies the IsEnabled attached property.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FocusTrapBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// Gets the IsEnabled value for the specified element.
    /// </summary>
    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Sets the IsEnabled value for the specified element.
    /// </summary>
    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the InitialFocusElement attached property.
    /// </summary>
    public static readonly DependencyProperty InitialFocusElementProperty =
        DependencyProperty.RegisterAttached(
            "InitialFocusElement",
            typeof(UIElement),
            typeof(FocusTrapBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets the element that should receive initial focus.
    /// </summary>
    public static UIElement? GetInitialFocusElement(DependencyObject obj)
    {
        return (UIElement?)obj.GetValue(InitialFocusElementProperty);
    }

    /// <summary>
    /// Sets the element that should receive initial focus.
    /// </summary>
    public static void SetInitialFocusElement(DependencyObject obj, UIElement? value)
    {
        obj.SetValue(InitialFocusElementProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if ((bool)e.NewValue)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
            element.GotFocus += OnGotFocus;

            // Set initial focus when loaded
            if (element is FrameworkElement fe)
            {
                if (fe.IsLoaded)
                {
                    SetInitialFocus(element);
                }
                else
                {
                    fe.Loaded += OnLoaded;
                }
            }
        }
        else
        {
            element.PreviewKeyDown -= OnPreviewKeyDown;
            element.GotFocus -= OnGotFocus;

            if (element is FrameworkElement fe)
            {
                fe.Loaded -= OnLoaded;
            }
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            SetInitialFocus(element);
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        // Ensure focus stays within the trap
        if (sender is UIElement container && e.OriginalSource is UIElement focused)
        {
            if (!IsDescendantOf(focused, container))
            {
                SetInitialFocus(container);
                e.Handled = true;
            }
        }
    }

    private static void SetInitialFocus(UIElement container)
    {
        UIElement? initialElement = GetInitialFocusElement(container);
        if (initialElement != null && initialElement.Focusable)
        {
            initialElement.Focus();
            Keyboard.Focus(initialElement);
            return;
        }

        // Find first focusable element
        UIElement? firstFocusable = FindFirstFocusableElement(container);
        if (firstFocusable != null)
        {
            firstFocusable.Focus();
            Keyboard.Focus(firstFocusable);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || sender is not UIElement container)
            return;

        List<UIElement> focusableElements = GetFocusableElements(container);
        if (focusableElements.Count == 0)
            return;

        UIElement? currentElement = Keyboard.FocusedElement as UIElement;
        if (currentElement == null)
            return;

        int currentIndex = focusableElements.IndexOf(currentElement);

        // Handle Tab navigation
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            // Shift+Tab - go backwards
            if (currentIndex <= 0 || currentIndex == -1)
            {
                // Wrap to last element
                focusableElements[^1].Focus();
                e.Handled = true;
            }
        }
        else
        {
            // Tab - go forward
            if (currentIndex >= focusableElements.Count - 1 || currentIndex == -1)
            {
                // Wrap to first element
                focusableElements[0].Focus();
                e.Handled = true;
            }
        }
    }

    private static List<UIElement> GetFocusableElements(UIElement container)
    {
        List<UIElement> elements = new List<UIElement>();
        CollectFocusableElements(container, elements);
        return elements;
    }

    private static void CollectFocusableElements(DependencyObject parent, List<UIElement> elements)
    {
        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is UIElement element)
            {
                if (IsFocusable(element))
                {
                    elements.Add(element);
                }

                // Recursively check children
                CollectFocusableElements(child, elements);
            }
        }
    }

    private static bool IsFocusable(UIElement element)
    {
        if (!element.IsVisible || !element.IsEnabled || !element.Focusable)
            return false;

        // Check if it's a standard interactive control
        if (element is Button or TextBox or CheckBox or RadioButton or ComboBox or ListBox or Slider)
            return true;

        // Check if it has explicit Focusable set
        if (element is Control control && control.Focusable)
            return true;

        return false;
    }

    private static UIElement? FindFirstFocusableElement(UIElement container)
    {
        List<UIElement> elements = GetFocusableElements(container);
        return elements.Count > 0 ? elements[0] : null;
    }

    private static bool IsDescendantOf(DependencyObject element, DependencyObject container)
    {
        DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent == container)
                return true;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return false;
    }
}
