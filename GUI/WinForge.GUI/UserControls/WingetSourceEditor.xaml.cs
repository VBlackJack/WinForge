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
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Services;
using Wpf.Ui.Controls;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.UserControls;

/// <summary>
/// UserControl for editing Winget installation source configuration.
/// </summary>
public partial class WingetSourceEditor : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the IsSourceEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSourceEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSourceEnabled),
            typeof(bool),
            typeof(WingetSourceEditor),
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
    /// Identifies the PackageId dependency property.
    /// </summary>
    public static readonly DependencyProperty PackageIdProperty =
        DependencyProperty.Register(
            nameof(PackageId),
            typeof(string),
            typeof(WingetSourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPackageIdChanged));

    /// <summary>
    /// Gets or sets the Winget package ID.
    /// </summary>
    public string PackageId
    {
        get => (string)GetValue(PackageIdProperty);
        set => SetValue(PackageIdProperty, value);
    }

    /// <summary>
    /// Identifies the Version dependency property.
    /// </summary>
    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(
            nameof(Version),
            typeof(string),
            typeof(WingetSourceEditor),
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
    /// Identifies the SourceRepository dependency property.
    /// </summary>
    public static readonly DependencyProperty SourceRepositoryProperty =
        DependencyProperty.Register(
            nameof(SourceRepository),
            typeof(string),
            typeof(WingetSourceEditor),
            new FrameworkPropertyMetadata("winget", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the source repository (winget or msstore).
    /// </summary>
    public string SourceRepository
    {
        get => (string)GetValue(SourceRepositoryProperty);
        set => SetValue(SourceRepositoryProperty, value);
    }

    /// <summary>
    /// Identifies the IsTesting dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTestingProperty =
        DependencyProperty.Register(
            nameof(IsTesting),
            typeof(bool),
            typeof(WingetSourceEditor),
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
            typeof(WingetSourceEditor),
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
            typeof(WingetSourceEditor),
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
            typeof(WingetSourceEditor),
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
    /// Initializes a new instance of WingetSourceEditor.
    /// </summary>
    public WingetSourceEditor()
    {
        InitializeComponent();

        // Set default test command if none provided
        if (TestCommand == null)
        {
            TestCommand = new AsyncRelayCommand(ExecuteDefaultTestAsync, CanExecuteTest);
        }
    }

    /// <summary>
    /// Handles PackageId changes to update IsEnabled based on content.
    /// </summary>
    private static void OnPackageIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WingetSourceEditor editor)
        {
            // Clear test result when package ID changes
            editor.TestResult = null;
            editor.TestSuccess = null;
        }
    }

    /// <summary>
    /// Handles TestResult changes to update the visual indicator.
    /// </summary>
    private static void OnTestResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WingetSourceEditor editor)
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
        return !string.IsNullOrWhiteSpace(PackageId) && !IsTesting;
    }

    /// <summary>
    /// Executes a package verification test for the configured Winget package ID.
    /// </summary>
    private async Task ExecuteDefaultTestAsync()
    {
        string? packageId = PackageId?.Trim();
        if (string.IsNullOrWhiteSpace(packageId))
        {
            TestSuccess = null;
            TestResult = null;
            return;
        }

        IsTesting = true;
        TestSuccess = null;
        TestResult = Loc.Verify_Verifying;

        try
        {
            IPackageVerificationService verificationService = ResolveVerificationService();

            if (!verificationService.IsWingetAvailable)
            {
                TestSuccess = false;
                TestResult = Loc.Verify_WingetUnavailable;
                return;
            }

            PackageVerificationResult result = await verificationService.VerifyWingetPackageAsync(packageId);

            if (!result.IsSuccess)
            {
                TestSuccess = false;
                TestResult = string.Format(Loc.Verify_Error, result.ErrorMessage ?? string.Empty);
                return;
            }

            if (result.Exists)
            {
                string packageDisplay = string.IsNullOrWhiteSpace(result.Version)
                    ? result.PackageId
                    : $"{result.PackageId} ({result.Version})";

                TestSuccess = true;
                TestResult = string.Format(Loc.Verify_PackageFound, packageDisplay);
                return;
            }

            TestSuccess = false;
            TestResult = Loc.Verify_PackageNotFound;
        }
        catch (OperationCanceledException)
        {
            TestSuccess = false;
            TestResult = Loc.SourceEditor_UrlTestTimeout;
        }
        catch (Exception ex)
        {
            TestSuccess = false;
            TestResult = string.Format(Loc.Verify_Error, ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    private static IPackageVerificationService ResolveVerificationService()
    {
        if (App.IsServicesInitialized)
        {
            try
            {
                return App.GetService<IPackageVerificationService>();
            }
            catch
            {
                // Fall back to direct construction if DI is not available.
            }
        }

        return new PackageVerificationService();
    }
}
