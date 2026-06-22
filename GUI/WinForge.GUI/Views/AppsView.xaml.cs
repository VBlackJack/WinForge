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
using WinForge.GUI.Models;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Views;

public partial class AppsView : UserControl
{
    public AppsView()
    {
        InitializeComponent();
    }

    private void SelectionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel)
        {
            viewModel.UpdateSelectedCount();
        }
    }

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

    private void DialogOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not AppsViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsLogViewerOpen)
        {
            viewModel.CloseLogViewerCommand.Execute(null);
            e.Handled = true;
        }
        else if (viewModel.IsSummaryDialogOpen)
        {
            viewModel.CloseSummaryDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CopyLogsToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppsViewModel viewModel && viewModel.LogViewerApplication?.LogOutput != null)
        {
            Clipboard.SetText(viewModel.LogViewerApplication.LogOutput);
        }
    }

    private void ApplicationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dataGrid || DataContext is not AppsViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            viewModel.SelectAllCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.SelectNoneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (dataGrid.SelectedItem is not ApplicationModel app)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                viewModel.ToggleSelectionCommand.Execute(app);
                break;
            case Key.F:
                viewModel.ToggleFavoriteCommand.Execute(app);
                break;
            case Key.Delete when app.Status is ApplicationStatus.Installed or ApplicationStatus.AlreadyInstalled:
                viewModel.UninstallAppCommand.Execute(app);
                break;
            case Key.Enter when !app.ManualInstallOnly:
                viewModel.InstallAppCommand.Execute(app);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
