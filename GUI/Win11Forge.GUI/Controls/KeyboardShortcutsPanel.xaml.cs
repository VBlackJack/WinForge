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
using Res = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Keyboard shortcut entry for display.
/// </summary>
public class KeyboardShortcut
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Panel displaying available keyboard shortcuts.
/// </summary>
public partial class KeyboardShortcutsPanel : UserControl
{
    public Action? RequestClose { get; set; }

    public KeyboardShortcutsPanel()
    {
        InitializeComponent();
        LoadShortcuts();
    }

    private void LoadShortcuts()
    {
        GeneralShortcuts.ItemsSource = new[]
        {
            new KeyboardShortcut { Key = "F1", Description = Res.Help_Shortcut_Help },
            new KeyboardShortcut { Key = "F5", Description = Res.Help_Shortcut_Refresh },
            new KeyboardShortcut { Key = "Ctrl+,", Description = Res.Help_Shortcut_Settings }
        };

        AppManagerShortcuts.ItemsSource = new[]
        {
            new KeyboardShortcut { Key = "Ctrl+A", Description = Res.Help_Shortcut_SelectAll },
            new KeyboardShortcut { Key = "Escape", Description = Res.Help_Shortcut_ClearSelection },
            new KeyboardShortcut { Key = "F", Description = Res.Help_Shortcut_ToggleFavorite },
            new KeyboardShortcut { Key = "Delete", Description = Res.Help_Shortcut_Uninstall },
            new KeyboardShortcut { Key = "Enter", Description = Res.Help_Shortcut_Install },
            new KeyboardShortcut { Key = "Space", Description = Res.Help_Shortcut_ToggleSelect }
        };

        NavigationShortcuts.ItemsSource = new[]
        {
            new KeyboardShortcut { Key = "Ctrl+1", Description = Res.Help_Shortcut_Dashboard },
            new KeyboardShortcut { Key = "Ctrl+2", Description = Res.Help_Shortcut_Prerequisites },
            new KeyboardShortcut { Key = "Ctrl+3", Description = Res.Help_Shortcut_Apps },
            new KeyboardShortcut { Key = "Ctrl+4", Description = Res.Help_Shortcut_Deployment },
            new KeyboardShortcut { Key = "Ctrl+5", Description = Res.Help_Shortcut_Settings },
            new KeyboardShortcut { Key = "Ctrl+6", Description = Res.Nav_AppDatabase }
        };

        DataGridShortcuts.ItemsSource = new[]
        {
            new KeyboardShortcut { Key = "Arrow Keys", Description = Res.Help_Shortcut_GridNavigate },
            new KeyboardShortcut { Key = "Page Up/Down", Description = Res.Help_Shortcut_GridPage },
            new KeyboardShortcut { Key = "Home/End", Description = Res.Help_Shortcut_GridHomeEnd },
            new KeyboardShortcut { Key = "Enter", Description = Res.Help_Shortcut_GridSelect },
            new KeyboardShortcut { Key = "Ctrl+A", Description = Res.Help_Shortcut_GridSelectAll }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClose?.Invoke();
    }
}
