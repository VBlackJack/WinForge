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
}
