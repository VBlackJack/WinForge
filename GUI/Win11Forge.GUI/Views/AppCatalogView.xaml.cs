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

#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Views;

/// <summary>
/// Code-behind for AppCatalogView.xaml.
/// Handles view-specific logic and events.
/// </summary>
public partial class AppCatalogView : UserControl
{
    /// <summary>
    /// Initializes a new instance of AppCatalogView.
    /// </summary>
    public AppCatalogView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles the Loaded event to load data.
    /// </summary>
    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is AppCatalogViewModel viewModel)
            {
                // Load data
                await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            App.GetService<ILoggingService>().LogError("AppCatalogView load failed", ex);
            IDialogService dialogService = App.GetService<IDialogService>();
            await dialogService.ShowErrorAsync(
                Loc.Common_Error,
                string.Format(Loc.AppCatalog_LoadError, ex.Message));
        }
    }

    /// <summary>
    /// Handles double-click on a DataGrid row to edit.
    /// </summary>
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AppCatalogViewModel viewModel && viewModel.EditCommand.CanExecute(null))
        {
            viewModel.EditCommand.Execute(null);
        }
    }

}
