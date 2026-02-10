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
using Win11Forge.GUI.Models;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Views;

/// <summary>
/// Code-behind for the Application Manager view.
/// </summary>
public partial class AppsView : UserControl
{
    public AppsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles checkbox state changes to update selected count.
    /// </summary>
    private void SelectionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel)
        {
            viewModel.UpdateSelectedCount();
        }
    }

    /// <summary>
    /// Handles unified dialog closing event for all dialogs.
    /// </summary>
    private void DialogHost_DialogClosing(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel)
        {
            // Close whichever dialog is currently open
            if (viewModel.IsLogViewerOpen)
            {
                viewModel.CloseLogViewerCommand.Execute(null);
            }
            else if (viewModel.IsSummaryDialogOpen)
            {
                viewModel.CloseSummaryDialogCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Copies the log output to clipboard.
    /// </summary>
    private void CopyLogsToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel && viewModel.LogViewerApplication?.LogOutput != null)
        {
            Clipboard.SetText(viewModel.LogViewerApplication.LogOutput);
        }
    }

    /// <summary>
    /// Handles keyboard shortcuts on the DataGrid.
    /// </summary>
    private void ApplicationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dataGrid || DataContext is not AppsViewModel viewModel)
            return;

        // Space: Toggle selection on focused row
        if (e.Key == Key.Space)
        {
            if (dataGrid.SelectedItem is ApplicationModel app)
            {
                viewModel.ToggleSelectionCommand.Execute(app);
                e.Handled = true;
            }
        }
        // Ctrl+A: Select all
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            viewModel.SelectAllCommand.Execute(null);
            e.Handled = true;
        }
        // Escape: Deselect all
        else if (e.Key == Key.Escape)
        {
            viewModel.SelectNoneCommand.Execute(null);
            e.Handled = true;
        }
        // F: Toggle favorite on focused row
        else if (e.Key == Key.F)
        {
            if (dataGrid.SelectedItem is ApplicationModel app)
            {
                viewModel.ToggleFavoriteCommand.Execute(app);
                e.Handled = true;
            }
        }
        // Delete: Uninstall focused app (if installed)
        else if (e.Key == Key.Delete)
        {
            if (dataGrid.SelectedItem is ApplicationModel app &&
                (app.Status == ApplicationStatus.Installed ||
                 app.Status == ApplicationStatus.AlreadyInstalled))
            {
                viewModel.UninstallAppCommand.Execute(app);
                e.Handled = true;
            }
        }
        // Enter: Install focused app
        else if (e.Key == Key.Enter)
        {
            if (dataGrid.SelectedItem is ApplicationModel app && !app.ManualInstallOnly)
            {
                viewModel.InstallAppCommand.Execute(app);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Gets the selected ApplicationModel from the DataGrid.
    /// </summary>
    private ApplicationModel? GetSelectedApp()
    {
        return ApplicationsDataGrid.SelectedItem as ApplicationModel;
    }

    /// <summary>
    /// Context menu: Launch application.
    /// </summary>
    private void ContextMenu_Launch_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.LaunchAppCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Open Homepage.
    /// </summary>
    private void ContextMenu_OpenHomepage_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.OpenWebsiteCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Install.
    /// </summary>
    private void ContextMenu_Install_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.InstallAppCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Uninstall.
    /// </summary>
    private void ContextMenu_Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.UninstallAppCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Update.
    /// </summary>
    private void ContextMenu_Update_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.UpdateAppCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Scan single app.
    /// </summary>
    private void ContextMenu_Scan_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.ScanAppCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Scan selected apps.
    /// </summary>
    private void ContextMenu_ScanSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel)
        {
            viewModel.ScanSelectedCommand.Execute(null);
        }
    }

    /// <summary>
    /// Context menu: Toggle Selection.
    /// </summary>
    private void ContextMenu_ToggleSelection_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.ToggleSelectionCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: View Logs.
    /// </summary>
    private void ContextMenu_ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.ViewLogsCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Copy App ID.
    /// </summary>
    private void ContextMenu_CopyAppId_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.CopyAppIdCommand.Execute(app);
        }
    }

    /// <summary>
    /// Context menu: Toggle Favorite.
    /// </summary>
    private void ContextMenu_ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedApp();
        if (app != null && DataContext is AppsViewModel viewModel)
        {
            viewModel.ToggleFavoriteCommand.Execute(app);
        }
    }
}
