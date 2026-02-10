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
using System.Windows.Media;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.UserControls;

/// <summary>
/// UserControl for editing Chocolatey installation source configuration.
/// </summary>
public partial class ChocolateySourceEditor : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the IsSourceEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSourceEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSourceEnabled),
            typeof(bool),
            typeof(ChocolateySourceEditor),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets whether this source is enabled.
    /// </summary>
    public bool IsSourceEnabled
    {
        get => (bool)GetValue(IsSourceEnabledProperty);
        set => SetValue(IsSourceEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the PackageName dependency property.
    /// </summary>
    public static readonly DependencyProperty PackageNameProperty =
        DependencyProperty.Register(
            nameof(PackageName),
            typeof(string),
            typeof(ChocolateySourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPackageNameChanged));

    /// <summary>
    /// Gets or sets the Chocolatey package name.
    /// </summary>
    public string PackageName
    {
        get => (string)GetValue(PackageNameProperty);
        set => SetValue(PackageNameProperty, value);
    }

    /// <summary>
    /// Identifies the Version dependency property.
    /// </summary>
    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(
            nameof(Version),
            typeof(string),
            typeof(ChocolateySourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the specific version to install.
    /// </summary>
    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    /// <summary>
    /// Identifies the IsTesting dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTestingProperty =
        DependencyProperty.Register(
            nameof(IsTesting),
            typeof(bool),
            typeof(ChocolateySourceEditor),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets whether a test is in progress.
    /// </summary>
    public bool IsTesting
    {
        get => (bool)GetValue(IsTestingProperty);
        set => SetValue(IsTestingProperty, value);
    }

    /// <summary>
    /// Identifies the TestResult dependency property.
    /// </summary>
    public static readonly DependencyProperty TestResultProperty =
        DependencyProperty.Register(
            nameof(TestResult),
            typeof(string),
            typeof(ChocolateySourceEditor),
            new PropertyMetadata(null, OnTestResultChanged));

    /// <summary>
    /// Gets or sets the test result message.
    /// </summary>
    public string? TestResult
    {
        get => (string?)GetValue(TestResultProperty);
        set => SetValue(TestResultProperty, value);
    }

    /// <summary>
    /// Identifies the TestSuccess dependency property.
    /// </summary>
    public static readonly DependencyProperty TestSuccessProperty =
        DependencyProperty.Register(
            nameof(TestSuccess),
            typeof(bool?),
            typeof(ChocolateySourceEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets whether the last test was successful.
    /// </summary>
    public bool? TestSuccess
    {
        get => (bool?)GetValue(TestSuccessProperty);
        set => SetValue(TestSuccessProperty, value);
    }

    /// <summary>
    /// Identifies the TestCommand dependency property.
    /// </summary>
    public static readonly DependencyProperty TestCommandProperty =
        DependencyProperty.Register(
            nameof(TestCommand),
            typeof(ICommand),
            typeof(ChocolateySourceEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the command to execute when testing the package.
    /// </summary>
    public ICommand? TestCommand
    {
        get => (ICommand?)GetValue(TestCommandProperty);
        set => SetValue(TestCommandProperty, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of ChocolateySourceEditor.
    /// </summary>
    public ChocolateySourceEditor()
    {
        InitializeComponent();

        // Set default test command if none provided
        if (TestCommand == null)
        {
            TestCommand = new AsyncRelayCommand(ExecuteDefaultTestAsync, CanExecuteTest);
        }
    }

    /// <summary>
    /// Handles PackageName changes to clear test results.
    /// </summary>
    private static void OnPackageNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChocolateySourceEditor editor)
        {
            editor.TestResult = null;
            editor.TestSuccess = null;
        }
    }

    /// <summary>
    /// Handles TestResult changes to update the visual indicator.
    /// </summary>
    private static void OnTestResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChocolateySourceEditor editor)
        {
            editor.UpdateTestResultVisuals();
        }
    }

    /// <summary>
    /// Updates the test result visual indicators.
    /// </summary>
    private void UpdateTestResultVisuals()
    {
        if (TestSuccess == true)
        {
            TestResultIcon.Symbol = SymbolRegular.CheckmarkCircle24;
            TestResultIcon.Foreground = Application.Current?.TryFindResource("StatusInstalledBrush") as Brush ?? new SolidColorBrush(Colors.Green);
        }
        else if (TestSuccess == false)
        {
            TestResultIcon.Symbol = SymbolRegular.ErrorCircle24;
            TestResultIcon.Foreground = Application.Current?.TryFindResource("StatusFailedBrush") as Brush ?? new SolidColorBrush(Colors.Red);
        }
    }

    /// <summary>
    /// Determines if the test command can execute.
    /// </summary>
    private bool CanExecuteTest()
    {
        return !string.IsNullOrWhiteSpace(PackageName) && !IsTesting;
    }

    /// <summary>
    /// Executes the default test (placeholder - should be overridden by parent).
    /// </summary>
    private async Task ExecuteDefaultTestAsync()
    {
        IsTesting = true;
        TestResult = null;

        try
        {
            await Task.Delay(1000);
            TestSuccess = true;
            TestResult = Loc.SourceEditor_TestPlaceholder;
        }
        catch (Exception ex)
        {
            TestSuccess = false;
            TestResult = ex.Message;
        }
        finally
        {
            IsTesting = false;
        }
    }
}
