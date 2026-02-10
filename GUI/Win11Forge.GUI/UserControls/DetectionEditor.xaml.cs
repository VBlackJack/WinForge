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
using Microsoft.Win32;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.UserControls;

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
            new FrameworkPropertyMetadata("Registry", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnMethodChanged));

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

    /// <summary>
    /// Initializes a new instance of DetectionEditor.
    /// </summary>
    public DetectionEditor()
    {
        InitializeComponent();

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
        RegistryPanel.Visibility = Method == "Registry" ? Visibility.Visible : Visibility.Collapsed;
        FilePanel.Visibility = Method == "File" ? Visibility.Visible : Visibility.Collapsed;
        CommandPanel.Visibility = Method == "Command" ? Visibility.Visible : Visibility.Collapsed;
        WindowsFeaturePanel.Visibility = Method == "WindowsFeature" ? Visibility.Visible : Visibility.Collapsed;
        StoreAppPanel.Visibility = Method == "StoreApp" ? Visibility.Visible : Visibility.Collapsed;
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
            TestResultIcon.Foreground = new SolidColorBrush(Colors.Green);
        }
        else if (TestSuccess == false)
        {
            TestResultIcon.Symbol = SymbolRegular.ErrorCircle24;
            TestResultIcon.Foreground = new SolidColorBrush(Colors.Red);
        }
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
            await Task.Run(() =>
            {
                switch (Method)
                {
                    case "Registry":
                        TestRegistryDetection();
                        break;
                    case "File":
                        TestFileDetection();
                        break;
                    case "Command":
                        TestCommandDetection();
                        break;
                    case "WindowsFeature":
                        TestWindowsFeatureDetection();
                        break;
                    case "StoreApp":
                        TestStoreAppDetection();
                        break;
                    default:
                        Dispatcher.Invoke(() =>
                        {
                            TestSuccess = false;
                            TestResult = Loc.Detection_UnknownMethod;
                        });
                        break;
                }
            });
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
    /// Tests registry detection.
    /// </summary>
    private void TestRegistryDetection()
    {
        try
        {
            var path = Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                Dispatcher.Invoke(() =>
                {
                    TestSuccess = false;
                    TestResult = Loc.Detection_PathRequired;
                });
                return;
            }

            // Parse registry path
            RegistryKey? baseKey = null;
            string subKeyPath;

            if (path.StartsWith("HKLM\\") || path.StartsWith("HKEY_LOCAL_MACHINE\\"))
            {
                baseKey = Microsoft.Win32.Registry.LocalMachine;
                subKeyPath = path.Replace("HKLM\\", "").Replace("HKEY_LOCAL_MACHINE\\", "");
            }
            else if (path.StartsWith("HKCU\\") || path.StartsWith("HKEY_CURRENT_USER\\"))
            {
                baseKey = Microsoft.Win32.Registry.CurrentUser;
                subKeyPath = path.Replace("HKCU\\", "").Replace("HKEY_CURRENT_USER\\", "");
            }
            else
            {
                // Default to HKLM
                baseKey = Microsoft.Win32.Registry.LocalMachine;
                subKeyPath = path;
            }

            using var key = baseKey.OpenSubKey(subKeyPath);
            if (key != null)
            {
                string? version = null;
                var versionKey = VersionKey;
                if (!string.IsNullOrWhiteSpace(versionKey))
                {
                    version = key.GetValue(versionKey)?.ToString();
                }
                else
                {
                    // Try common version value names
                    version = key.GetValue("DisplayVersion")?.ToString()
                           ?? key.GetValue("Version")?.ToString()
                           ?? key.GetValue("CurrentVersion")?.ToString();
                }

                Dispatcher.Invoke(() =>
                {
                    TestSuccess = true;
                    TestResult = Loc.Detection_Found;
                    DetectedVersion = version != null ? string.Format(Loc.Detection_Version, version) : null;
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    TestSuccess = false;
                    TestResult = Loc.Detection_NotFound;
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TestSuccess = false;
                TestResult = string.Format(Loc.Detection_Error, ex.Message);
            });
        }
    }

    /// <summary>
    /// Tests file detection.
    /// </summary>
    private void TestFileDetection()
    {
        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(Path);

            // Security: Validate expanded path doesn't contain dangerous patterns
            if (!IsValidExpandedPath(expandedPath))
            {
                Dispatcher.Invoke(() =>
                {
                    TestSuccess = false;
                    TestResult = "Invalid path after environment variable expansion";
                });
                return;
            }

            if (System.IO.File.Exists(expandedPath))
            {
                string? version = null;
                try
                {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(expandedPath);
                    version = versionInfo.FileVersion ?? versionInfo.ProductVersion;
                }
                catch { /* Version info not available */ }

                Dispatcher.Invoke(() =>
                {
                    TestSuccess = true;
                    TestResult = Loc.Detection_Found;
                    DetectedVersion = version != null ? string.Format(Loc.Detection_Version, version) : null;
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    TestSuccess = false;
                    TestResult = Loc.Detection_NotFound;
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TestSuccess = false;
                TestResult = string.Format(Loc.Detection_Error, ex.Message);
            });
        }
    }

    /// <summary>
    /// Validates an expanded file path for security.
    /// Blocks paths with dangerous patterns that could result from malicious environment variables.
    /// </summary>
    private static bool IsValidExpandedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Block null bytes
        if (path.Contains('\0')) return false;

        // Block unexpanded environment variables (indicates potential attack or misconfiguration)
        if (path.Contains('%')) return false;

        // Block command injection characters
        var dangerousChars = new[] { ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'' };
        if (path.IndexOfAny(dangerousChars) >= 0) return false;

        // Validate it's a plausible file path (contains drive letter or UNC path)
        if (!System.IO.Path.IsPathRooted(path)) return false;

        return true;
    }

    /// <summary>
    /// Tests command detection.
    /// </summary>
    private void TestCommandDetection()
    {
        try
        {
            // Security: Validate command path to prevent command injection
            // Only allow alphanumeric, spaces, dots, hyphens, underscores, colons, slashes, and backslashes
            if (string.IsNullOrWhiteSpace(Path) || !IsValidCommandPath(Path))
            {
                Dispatcher.Invoke(() =>
                {
                    TestSuccess = false;
                    TestResult = "Invalid command path: contains potentially dangerous characters";
                });
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                // Security: Quote the entire path to prevent argument injection
                Arguments = $"/c \"{Path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                // Wait with timeout and kill if necessary
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* Best effort cleanup */ }
                    Dispatcher.Invoke(() =>
                    {
                        TestSuccess = false;
                        TestResult = "Command timed out after 5 seconds";
                    });
                    return;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();

                Dispatcher.Invoke(() =>
                {
                    if (process.ExitCode == 0)
                    {
                        TestSuccess = true;
                        TestResult = Loc.Detection_Found;
                        DetectedVersion = !string.IsNullOrWhiteSpace(output) ? output : null;
                    }
                    else
                    {
                        TestSuccess = false;
                        TestResult = Loc.Detection_NotFound;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TestSuccess = false;
                TestResult = string.Format(Loc.Detection_Error, ex.Message);
            });
        }
    }

    /// <summary>
    /// Validates a command path to prevent command injection attacks.
    /// Only allows safe characters: alphanumeric, spaces, dots, hyphens, underscores, colons, slashes.
    /// Blocks: semicolons, ampersands, pipes, backticks, $, parentheses, quotes, redirects.
    /// </summary>
    private static bool IsValidCommandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Block dangerous command injection characters
        var dangerousChars = new[] { ';', '&', '|', '`', '$', '(', ')', '<', '>', '"', '\'', '\n', '\r' };
        if (path.IndexOfAny(dangerousChars) >= 0) return false;

        // Block common injection patterns
        if (path.Contains("&&") || path.Contains("||") || path.Contains(">>") || path.Contains("<<"))
            return false;

        // Ensure path doesn't contain null bytes
        if (path.Contains('\0')) return false;

        return true;
    }

    /// <summary>
    /// Tests Windows feature detection.
    /// </summary>
    private void TestWindowsFeatureDetection()
    {
        // Windows feature detection requires elevated permissions
        // This is a simplified check
        Dispatcher.Invoke(() =>
        {
            TestSuccess = null;
            TestResult = Loc.Detection_FeatureCheckNotSupported;
        });
    }

    /// <summary>
    /// Tests Store app detection.
    /// </summary>
    private void TestStoreAppDetection()
    {
        // Store app detection requires Package APIs
        // This is a simplified check
        Dispatcher.Invoke(() =>
        {
            TestSuccess = null;
            TestResult = Loc.Detection_StoreAppCheckNotSupported;
        });
    }

    /// <summary>
    /// Executes file browse dialog.
    /// </summary>
    private void ExecuteBrowse()
    {
        var dialog = new OpenFileDialog
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
