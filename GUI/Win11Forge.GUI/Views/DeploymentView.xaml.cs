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
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Views;

/// <summary>
/// Monitoring view for tracking deployment progress.
/// </summary>
public partial class DeploymentView : UserControl
{
    public DeploymentView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles dialog closing to update ViewModel state.
    /// </summary>
    private void DialogHost_DialogClosing(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DeploymentViewModel vm)
        {
            vm.CloseLogViewerCommand.Execute(null);
        }
    }

    /// <summary>
    /// Copies logs to clipboard.
    /// </summary>
    private void CopyLogsToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DeploymentViewModel vm && vm.LogViewerApplication != null)
        {
            var logs = vm.LogViewerApplication.LogOutput;
            if (!string.IsNullOrEmpty(logs))
            {
                Clipboard.SetText(logs);
            }
        }
    }

    /// <summary>
    /// Closes the log viewer with Escape.
    /// </summary>
    private void LogViewerOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not DeploymentViewModel vm)
        {
            return;
        }

        vm.CloseLogViewerCommand.Execute(null);
        e.Handled = true;
    }
}
