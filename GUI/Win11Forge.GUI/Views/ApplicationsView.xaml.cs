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
using Microsoft.Win32;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;
using Win11Forge.GUI.Views.Dialogs;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Views;

/// <summary>
/// Code-behind for ApplicationsView.xaml.
/// Handles view-specific logic and events.
/// </summary>
public partial class ApplicationsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of ApplicationsView.
    /// </summary>
    public ApplicationsView()
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
            if (DataContext is ApplicationsViewModel viewModel)
            {
                // Subscribe to events
                viewModel.OpenEditorRequested += OnOpenEditorRequested;
                viewModel.ConfirmDeleteRequested += OnConfirmDeleteRequested;
                viewModel.ImportRequested += OnImportRequested;
                viewModel.ExportRequested += OnExportRequested;

                // Load data
                await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplicationsView load failed: {ex}");
            MessageBox.Show(
                string.Format(Loc.AppDb_LoadError, ex.Message),
                Loc.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles the Unloaded event to clean up event subscriptions.
    /// Prevents memory leaks by unsubscribing from ViewModel events.
    /// </summary>
    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApplicationsViewModel viewModel)
        {
            // Unsubscribe from events to prevent memory leaks
            viewModel.OpenEditorRequested -= OnOpenEditorRequested;
            viewModel.ConfirmDeleteRequested -= OnConfirmDeleteRequested;
            viewModel.ImportRequested -= OnImportRequested;
            viewModel.ExportRequested -= OnExportRequested;
        }
    }

    /// <summary>
    /// Handles double-click on a DataGrid row to edit.
    /// </summary>
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ApplicationsViewModel viewModel && viewModel.EditCommand.CanExecute(null))
        {
            viewModel.EditCommand.Execute(null);
        }
    }

    /// <summary>
    /// Handles the request to open the application editor.
    /// </summary>
    private async void OnOpenEditorRequested(object? sender, ApplicationEditorEventArgs e)
    {
        var ownerWindow = Window.GetWindow(this);
        if (ownerWindow == null)
        {
            MessageBox.Show(
                Loc.AppDb_EditorOwnerNotFound,
                Loc.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            var savedApplication = e.IsNew
                ? await ApplicationEditorDialog.ShowAddDialogAsync(ownerWindow)
                : await ApplicationEditorDialog.ShowEditDialogAsync(ownerWindow, e.Application);

            if (savedApplication != null && DataContext is ApplicationsViewModel viewModel)
            {
                await viewModel.SaveApplicationAsync(savedApplication, e.IsNew, e.OriginalApplication);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(Loc.AppDb_EditorOpenError, ex.Message, ex.StackTrace),
                Loc.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles the request to confirm deletion.
    /// </summary>
    private void OnConfirmDeleteRequested(object? sender, ConfirmDeleteEventArgs e)
    {
        var message = string.Format(
            Loc.AppDb_DeleteConfirm,
            e.AppName,
            e.AppId);

        var result = MessageBox.Show(
            message,
            Loc.AppDb_DeleteTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        e.Confirmed = result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Handles the request to import applications.
    /// </summary>
    private async void OnImportRequested(object? sender, EventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.AppDb_Import,
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true && DataContext is ApplicationsViewModel viewModel)
            {
                // Show import mode selection dialog
                var modeResult = MessageBox.Show(
                    Loc.AppDb_ImportModeConfirm,
                    Loc.AppDb_Import,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (modeResult == MessageBoxResult.Cancel) return;

                var mode = modeResult == MessageBoxResult.Yes ? ImportMode.Replace : ImportMode.Merge;
                await viewModel.ImportApplicationsAsync(dialog.FileName, mode);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Import failed: {ex}");
            MessageBox.Show(
                string.Format(Loc.AppDb_ImportError, ex.Message),
                Loc.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles the request to export applications.
    /// </summary>
    private async void OnExportRequested(object? sender, ExportEventArgs e)
    {
        try
        {
            if (e.AppIds.Count == 0)
            {
                MessageBox.Show(
                    Loc.AppDb_NoExportSelection,
                    Loc.AppDb_Export,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = Loc.AppDb_Export,
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"applications-export-{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true && DataContext is ApplicationsViewModel viewModel)
            {
                await viewModel.ExportApplicationsAsync(e.AppIds, dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export failed: {ex}");
            MessageBox.Show(
                string.Format(Loc.AppDb_ExportError, ex.Message),
                Loc.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
