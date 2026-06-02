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

using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.UserControls;

/// <summary>
/// UserControl for editing direct download installation source configuration.
/// </summary>
public partial class DirectDownloadSourceEditor : UserControl
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    #region Dependency Properties

    /// <summary>
    /// Identifies the IsSourceEnabled dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSourceEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSourceEnabled),
            typeof(bool),
            typeof(DirectDownloadSourceEditor),
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
    /// Identifies the DownloadUrl dependency property.
    /// </summary>
    public static readonly DependencyProperty DownloadUrlProperty =
        DependencyProperty.Register(
            nameof(DownloadUrl),
            typeof(string),
            typeof(DirectDownloadSourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDownloadUrlChanged));

    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    public string DownloadUrl
    {
        get => (string)GetValue(DownloadUrlProperty);
        set => SetValue(DownloadUrlProperty, value);
    }

    /// <summary>
    /// Identifies the InstallerType dependency property.
    /// </summary>
    public static readonly DependencyProperty InstallerTypeProperty =
        DependencyProperty.Register(
            nameof(InstallerType),
            typeof(string),
            typeof(DirectDownloadSourceEditor),
            new FrameworkPropertyMetadata("exe", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the installer type (exe, msi, msix, zip).
    /// </summary>
    public string InstallerType
    {
        get => (string)GetValue(InstallerTypeProperty);
        set => SetValue(InstallerTypeProperty, value);
    }

    /// <summary>
    /// Identifies the SilentArgs dependency property.
    /// </summary>
    public static readonly DependencyProperty SilentArgsProperty =
        DependencyProperty.Register(
            nameof(SilentArgs),
            typeof(string),
            typeof(DirectDownloadSourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the silent installation arguments.
    /// </summary>
    public string SilentArgs
    {
        get => (string)GetValue(SilentArgsProperty);
        set => SetValue(SilentArgsProperty, value);
    }

    /// <summary>
    /// Identifies the Checksum dependency property.
    /// </summary>
    public static readonly DependencyProperty ChecksumProperty =
        DependencyProperty.Register(
            nameof(Checksum),
            typeof(string),
            typeof(DirectDownloadSourceEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the file checksum.
    /// </summary>
    public string Checksum
    {
        get => (string)GetValue(ChecksumProperty);
        set => SetValue(ChecksumProperty, value);
    }

    /// <summary>
    /// Identifies the IsTesting dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTestingProperty =
        DependencyProperty.Register(
            nameof(IsTesting),
            typeof(bool),
            typeof(DirectDownloadSourceEditor),
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
            typeof(DirectDownloadSourceEditor),
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
            typeof(DirectDownloadSourceEditor),
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
            typeof(DirectDownloadSourceEditor),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the command to execute when testing the URL.
    /// </summary>
    public ICommand? TestCommand
    {
        get => (ICommand?)GetValue(TestCommandProperty);
        set => SetValue(TestCommandProperty, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of DirectDownloadSourceEditor.
    /// </summary>
    public DirectDownloadSourceEditor()
    {
        InitializeComponent();

        // Set default test command if none provided
        if (TestCommand == null)
        {
            TestCommand = new AsyncRelayCommand(ExecuteUrlTestAsync, CanExecuteTest);
        }
    }

    /// <summary>
    /// Handles DownloadUrl changes to clear test results.
    /// </summary>
    private static void OnDownloadUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DirectDownloadSourceEditor editor)
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
        if (d is DirectDownloadSourceEditor editor)
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
        return !string.IsNullOrWhiteSpace(DownloadUrl) && !IsTesting && IsValidUrl(DownloadUrl);
    }

    /// <summary>
    /// Validates if the URL is well-formed.
    /// </summary>
    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Executes an HTTP HEAD request to test the URL.
    /// </summary>
    private async Task ExecuteUrlTestAsync()
    {
        IsTesting = true;
        TestResult = null;

        try
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, DownloadUrl);
            using HttpResponseMessage response = await SharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                long? contentLength = response.Content.Headers.ContentLength;
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";

                TestSuccess = true;
                TestResult = contentLength.HasValue
                    ? string.Format(Loc.SourceEditor_UrlTestSuccess, FormatFileSize(contentLength.Value), contentType)
                    : string.Format(Loc.SourceEditor_UrlTestSuccessNoSize, contentType);
            }
            else
            {
                TestSuccess = false;
                TestResult = string.Format(Loc.SourceEditor_UrlTestFailed, (int)response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (HttpRequestException ex)
        {
            TestSuccess = false;
            TestResult = string.Format(Loc.SourceEditor_UrlTestError, ex.Message);
        }
        catch (TaskCanceledException)
        {
            TestSuccess = false;
            TestResult = Loc.SourceEditor_UrlTestTimeout;
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

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
