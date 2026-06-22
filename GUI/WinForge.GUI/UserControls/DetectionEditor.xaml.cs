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
using Microsoft.Win32;
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using Wpf.Ui.Controls;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.UserControls;

/// <summary>
/// UserControl for editing application detection configuration.
/// </summary>
public partial class DetectionEditor : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the Method dependency property.
    /// </summary>
    public static readonly DependencyProperty MethodProperty =
        DependencyProperty.Register(
            nameof(Method),
            typeof(string),
            typeof(DetectionEditor),
            new FrameworkPropertyMetadata(nameof(DetectionMethod.Registry), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnMethodChanged));

    /// <summary>
    /// Gets or sets the detection method.
    /// </summary>
    public string Method
    {
        get => (string)GetValue(MethodProperty);
        set => SetValue(MethodProperty, value);
    }

    /// <summary>
    /// Identifies the Path dependency property.
    /// </summary>
    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register(
            nameof(Path),
            typeof(string),
            typeof(DetectionEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPathChanged));

    /// <summary>
    /// Gets or sets the detection path.
    /// </summary>
    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>
    /// Identifies the VersionKey dependency property.
    /// </summary>
    public static readonly DependencyProperty VersionKeyProperty =
        DependencyProperty.Register(
            nameof(VersionKey),
            typeof(string),
            typeof(DetectionEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the version registry key name.
    /// </summary>
    public string VersionKey
    {
        get => (string)GetValue(VersionKeyProperty);
        set => SetValue(VersionKeyProperty, value);
    }

    /// <summary>
    /// Identifies the MinVersion dependency property.
    /// </summary>
    public static readonly DependencyProperty MinVersionProperty =
        DependencyProperty.Register(
            nameof(MinVersion),
            typeof(string),
            typeof(DetectionEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the minimum version requirement.
    /// </summary>
    public string MinVersion
    {
        get => (string)GetValue(MinVersionProperty);
        set => SetValue(MinVersionProperty, value);
    }

    /// <summary>
    /// Identifies the IsTesting dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTestingProperty =
        DependencyProperty.Register(
            nameof(IsTesting),
            typeof(bool),
            typeof(DetectionEditor),
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
            typeof(DetectionEditor),
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
            typeof(DetectionEditor),
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
    /// Identifies the DetectedVersion dependency property.
    /// </summary>
    public static readonly DependencyProperty DetectedVersionProperty =
        DependencyProperty.Register(
            nameof(DetectedVersion),
            typeof(string),
            typeof(DetectionEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the detected version from the test.
    /// </summary>
    public string? DetectedVersion
    {
        get => (string?)GetValue(DetectedVersionProperty);
        set => SetValue(DetectedVersionProperty, value);
    }

    /// <summary>
    /// Identifies the TestCommand dependency property.
    /// </summary>
    public static readonly DependencyProperty TestCommandProperty =
        DependencyProperty.Register(
            nameof(TestCommand),
            typeof(ICommand),
            typeof(DetectionEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the command to test detection.
    /// </summary>
    public ICommand? TestCommand
    {
        get => (ICommand?)GetValue(TestCommandProperty);
        set => SetValue(TestCommandProperty, value);
    }

    /// <summary>
    /// Identifies the BrowseCommand dependency property.
    /// </summary>
    public static readonly DependencyProperty BrowseCommandProperty =
        DependencyProperty.Register(
            nameof(BrowseCommand),
            typeof(ICommand),
            typeof(DetectionEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the command to browse for a file.
    /// </summary>
    public ICommand? BrowseCommand
    {
        get => (ICommand?)GetValue(BrowseCommandProperty);
        set => SetValue(BrowseCommandProperty, value);
    }

    #endregion

    private readonly IDetectionProbe _probe;

    /// <summary>
    /// Initializes a new instance of DetectionEditor.
    /// </summary>
    public DetectionEditor()
    {
        InitializeComponent();
        try
        {
            _probe = App.GetService<IDetectionProbe>();
        }
        catch
        {
            _probe = new DetectionProbe();
        }

        // Set default commands
        if (TestCommand == null)
        {
            TestCommand = new AsyncRelayCommand(ExecuteTestAsync, CanExecuteTest);
        }
        if (BrowseCommand == null)
        {
            BrowseCommand = new RelayCommand(ExecuteBrowse);
        }

        // Initialize panel visibility
        UpdatePanelVisibility();
        RefreshTestCommandState();
    }

    /// <summary>
    /// Handles Method changes to update panel visibility.
    /// </summary>
    private static void OnMethodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DetectionEditor editor)
        {
            editor.UpdatePanelVisibility();
            editor.ClearTestResult();
            editor.RefreshTestCommandState();
        }
    }

    /// <summary>
    /// Handles Path changes to clear test result.
    /// </summary>
    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DetectionEditor editor)
        {
            editor.ClearTestResult();
            editor.RefreshTestCommandState();
        }
    }

    /// <summary>
    /// Handles TestResult changes to update visuals.
    /// </summary>
    private static void OnTestResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DetectionEditor editor)
        {
            editor.UpdateTestResultVisuals();
        }
    }

    /// <summary>
    /// Updates panel visibility based on selected method.
    /// </summary>
    private void UpdatePanelVisibility()
    {
        DetectionMethod method = Method.ToDetectionMethod();

        RegistryPanel.Visibility = method == DetectionMethod.Registry ? Visibility.Visible : Visibility.Collapsed;
        FilePanel.Visibility = method == DetectionMethod.File ? Visibility.Visible : Visibility.Collapsed;
        CommandPanel.Visibility = method == DetectionMethod.Command ? Visibility.Visible : Visibility.Collapsed;
        WindowsFeaturePanel.Visibility = method == DetectionMethod.WindowsFeature ? Visibility.Visible : Visibility.Collapsed;
        StoreAppPanel.Visibility = method == DetectionMethod.StoreApp ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Clears the test result.
    /// </summary>
    private void ClearTestResult()
    {
        TestResult = null;
        TestSuccess = null;
        DetectedVersion = null;
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
        else
        {
            // Neutral/informational state (e.g. StoreApp check not supported): reset stale success/error icon.
            TestResultIcon.Symbol = SymbolRegular.Info24;
            TestResultIcon.Foreground = Application.Current?.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? new SolidColorBrush(Colors.Gray);
        }
    }

    /// <summary>
    /// Re-evaluates whether the test command can execute.
    /// </summary>
    private void RefreshTestCommandState()
    {
        (TestCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Determines if the test can execute.
    /// </summary>
    private bool CanExecuteTest()
    {
        return !string.IsNullOrWhiteSpace(Path) && !IsTesting;
    }

    /// <summary>
    /// Executes the detection test.
    /// </summary>
    private async Task ExecuteTestAsync()
    {
        IsTesting = true;
        ClearTestResult();

        try
        {
            DetectionMethod method = Method.ToDetectionMethod();
            if (method == DetectionMethod.StoreApp)
            {
                TestSuccess = null;
                TestResult = Loc.Detection_StoreAppCheckNotSupported;
                return;
            }

            DetectionConfiguration config = BuildConfiguration(method);
            DetectionProbeResult result = await _probe.ProbeAsync(config, PathValidationPolicy.AdHoc);
            ApplyResult(method, result);
        }
        catch (Exception ex)
        {
            TestSuccess = false;
            TestResult = string.Format(Loc.Detection_Error, ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// Builds a detection configuration from the editor fields.
    /// </summary>
    private DetectionConfiguration BuildConfiguration(DetectionMethod method)
    {
        DetectionConfiguration config = new DetectionConfiguration { Method = method.ToString() };

        switch (method)
        {
            case DetectionMethod.Registry:
                config.Path = Path;
                config.VersionKey = VersionKey;
                break;
            case DetectionMethod.File:
                config.Path = Path;
                break;
            case DetectionMethod.Command:
                config.Command = Path;
                break;
            case DetectionMethod.WindowsFeature:
                config.FeatureName = Path;
                break;
        }

        return config;
    }

    /// <summary>
    /// Applies a probe result to the editor test result properties.
    /// </summary>
    private void ApplyResult(DetectionMethod method, DetectionProbeResult result)
    {
        switch (result.Outcome)
        {
            case DetectionOutcome.Found:
                TestSuccess = true;
                TestResult = Loc.Detection_Found;
                DetectedVersion = result.Version != null ? string.Format(Loc.Detection_Version, result.Version) : null;
                break;
            case DetectionOutcome.NotFound:
                TestSuccess = false;
                TestResult = Loc.Detection_NotFound;
                break;
            case DetectionOutcome.InvalidInput:
                TestSuccess = false;
                TestResult = method switch
                {
                    DetectionMethod.Command => Loc.Detection_InvalidCommandPath,
                    DetectionMethod.WindowsFeature => Loc.Detection_InvalidFeatureName,
                    DetectionMethod.Registry => Loc.Detection_InvalidRegistryPath,
                    _ => Loc.Detection_InvalidExpandedPath
                };
                break;
            case DetectionOutcome.Error:
                TestSuccess = false;
                TestResult = string.Format(Loc.Detection_Error, result.Detail ?? string.Empty);
                break;
            default:
                TestSuccess = null;
                TestResult = Loc.Detection_UnknownMethod;
                break;
        }
    }

    /// <summary>
    /// Executes file browse dialog.
    /// </summary>
    private void ExecuteBrowse()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = Loc.Detection_SelectFile
        };

        if (dialog.ShowDialog() == true)
        {
            Path = dialog.FileName;
        }
    }
}
