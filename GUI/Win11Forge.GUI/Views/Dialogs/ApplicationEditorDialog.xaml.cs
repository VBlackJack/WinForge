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
using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.ViewModels;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Views.Dialogs;

/// <summary>
/// Dialog for adding or editing application definitions.
/// </summary>
public partial class ApplicationEditorDialog : Window
{
    private readonly ApplicationEditorViewModel _viewModel;

    /// <summary>
    /// Gets whether the dialog was saved successfully.
    /// </summary>
    public bool SavedSuccessfully => _viewModel.DialogResult;

    /// <summary>
    /// Initializes a new instance of ApplicationEditorDialog.
    /// </summary>
    public ApplicationEditorDialog()
    {
        InitializeComponent();

        _viewModel = App.GetService<ApplicationEditorViewModel>();
        DataContext = _viewModel;

        // Subscribe to events
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.ConfirmDiscardRequested += OnConfirmDiscardRequested;
        _viewModel.NewCategoryRequested += OnNewCategoryRequested;

        Closed += OnDialogClosed;
    }

    /// <summary>
    /// Initializes the dialog for a new or existing application.
    /// </summary>
    /// <param name="application">The application to edit.</param>
    /// <param name="isNew">Whether this is a new application.</param>
    public async Task InitializeAsync(EditableApplicationModel application, bool isNew)
    {
        await _viewModel.InitializeAsync(application, isNew);
    }

    /// <summary>
    /// Handles the close request from the ViewModel.
    /// </summary>
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = _viewModel.DialogResult;
        Close();
    }

    /// <summary>
    /// Handles the confirm discard request.
    /// </summary>
    private void OnConfirmDiscardRequested(object? sender, ConfirmDiscardEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            Loc.AppEditor_DiscardChanges,
            Loc.AppEditor_DiscardTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        e.Discard = result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Handles the new category request.
    /// </summary>
    private void OnNewCategoryRequested(object? sender, NewCategoryEventArgs e)
    {
        var dialog = new InputDialog(
            Loc.AppEditor_NewCategoryTitle,
            Loc.AppEditor_NewCategoryPrompt,
            string.Empty)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            e.NewCategory = dialog.InputText;
        }
    }

    /// <summary>
    /// Handles the Verify button click.
    /// </summary>
    private async void VerifyButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.VerifySourcesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Cleans up when the dialog closes.
    /// </summary>
    private void OnDialogClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.ConfirmDiscardRequested -= OnConfirmDiscardRequested;
        _viewModel.NewCategoryRequested -= OnNewCategoryRequested;
        _viewModel.Cleanup();
    }

    /// <summary>
    /// Shows the dialog for adding a new application.
    /// </summary>
    /// <param name="owner">Owner window.</param>
    /// <returns>The new application if saved, null if cancelled.</returns>
    public static async Task<EditableApplicationModel?> ShowAddDialogAsync(Window owner)
    {
        var dialog = new ApplicationEditorDialog { Owner = owner };
        var newApp = new EditableApplicationModel
        {
            DefaultPriority = 50,
            Sources = new ApplicationSourcesModel(),
            Detection = new ApplicationDetectionModel()
        };

        await dialog.InitializeAsync(newApp, isNew: true);

        return dialog.ShowDialog() == true ? dialog._viewModel.Application : null;
    }

    /// <summary>
    /// Shows the dialog for editing an existing application.
    /// </summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="application">The application to edit (will be cloned).</param>
    /// <returns>The edited application if saved, null if cancelled.</returns>
    public static async Task<EditableApplicationModel?> ShowEditDialogAsync(Window owner, EditableApplicationModel application)
    {
        var dialog = new ApplicationEditorDialog { Owner = owner };
        var clone = application.Clone();

        // Ensure Detection is not null
        clone.Detection ??= new ApplicationDetectionModel();

        await dialog.InitializeAsync(clone, isNew: false);

        return dialog.ShowDialog() == true ? dialog._viewModel.Application : null;
    }
}

/// <summary>
/// Simple input dialog for text entry.
/// </summary>
public class InputDialog : Window
{
    private readonly System.Windows.Controls.TextBox _textBox;

    /// <summary>
    /// Gets the input text.
    /// </summary>
    public string InputText => _textBox.Text;

    /// <summary>
    /// Creates a new input dialog.
    /// </summary>
    public InputDialog(string title, string prompt, string defaultValue)
    {
        Title = title;
        Width = 400;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.SystemColors.ControlBrush;

        var grid = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(16)
        };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

        var label = new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8)
        };
        System.Windows.Controls.Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _textBox = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 0, 0, 16)
        };
        System.Windows.Controls.Grid.SetRow(_textBox, 1);
        grid.Children.Add(_textBox);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = Loc.AppEditor_Cancel,
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        var okButton = new System.Windows.Controls.Button
        {
            Content = Loc.Common_OK,
            Width = 80,
            IsDefault = true
        };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        grid.Children.Add(buttonPanel);

        Content = grid;

        Loaded += (s, e) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }
}
