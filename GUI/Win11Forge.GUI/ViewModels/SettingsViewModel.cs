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

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// Handles theme and language configuration with persistence.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly PaletteHelper _paletteHelper = new();
    private readonly IAppSettingsService _settingsService;
    private readonly IDeploymentHistoryService _historyService;
    private string _initialLanguageCode = string.Empty;

    /// <summary>
    /// Whether dark theme is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isDarkTheme = true;

    /// <summary>
    /// Available languages.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LanguageOption> _availableLanguages = [];

    /// <summary>
    /// Selected language.
    /// </summary>
    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    /// <summary>
    /// Whether restart is required for language change.
    /// </summary>
    [ObservableProperty]
    private bool _restartRequired;

    /// <summary>
    /// Status message for settings changes.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Win11Forge version string.
    /// </summary>
    [ObservableProperty]
    private string _appVersion = string.Empty;

    /// <summary>
    /// Maximum number of parallel installations (1-10).
    /// </summary>
    [ObservableProperty]
    private int _maxParallelInstalls = 5;

    /// <summary>
    /// Maximum number of parallel scans (1-20).
    /// </summary>
    [ObservableProperty]
    private int _maxParallelScans = 8;

    /// <summary>
    /// Available parallel install options.
    /// </summary>
    public int[] ParallelInstallOptions { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>
    /// Available parallel scan options.
    /// </summary>
    public int[] ParallelScanOptions { get; } = [1, 2, 4, 6, 8, 10, 12, 16, 20];

    /// <summary>
    /// Initializes a new instance of SettingsViewModel with default services.
    /// </summary>
    public SettingsViewModel()
        : this(new AppSettingsService(), new DeploymentHistoryService())
    {
    }

    /// <summary>
    /// Initializes a new instance of SettingsViewModel with injected services.
    /// </summary>
    public SettingsViewModel(IAppSettingsService settingsService, IDeploymentHistoryService historyService)
    {
        _settingsService = settingsService;
        _historyService = historyService;

        // Initialize available languages
        AvailableLanguages =
        [
            new LanguageOption("en", "English"),
            new LanguageOption("fr", "Francais")
        ];

        // Initialize version
        AppVersion = "3.1.0";

        // Load current settings
        LoadCurrentSettings();
    }

    /// <inheritdoc/>
    public override Task InitializeAsync()
    {
        LoadCurrentSettings();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the current theme and language settings from persisted storage.
    /// </summary>
    private void LoadCurrentSettings()
    {
        var settings = _settingsService.LoadSettings();

        // Get current theme from settings
        IsDarkTheme = settings.IsDarkTheme;

        // Get language from settings
        _initialLanguageCode = settings.LanguageCode;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == settings.LanguageCode)
                          ?? AvailableLanguages.First();

        // Get parallel installs setting
        MaxParallelInstalls = Math.Clamp(settings.MaxParallelInstalls, 1, 10);
        MaxParallelScans = Math.Clamp(settings.MaxParallelScans, 1, 20);

        // Apply theme immediately
        ApplyThemeInternal(IsDarkTheme);
    }

    /// <summary>
    /// Called when MaxParallelInstalls changes.
    /// </summary>
    partial void OnMaxParallelInstallsChanged(int value)
    {
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ParallelInstallsApplied;
    }

    /// <summary>
    /// Called when MaxParallelScans changes.
    /// </summary>
    partial void OnMaxParallelScansChanged(int value)
    {
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ParallelScansApplied;
    }

    /// <summary>
    /// Called when IsDarkTheme changes.
    /// </summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplyThemeInternal(value);
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ThemeApplied;
    }

    /// <summary>
    /// Applies the theme without saving (internal use).
    /// </summary>
    private void ApplyThemeInternal(bool isDark)
    {
        try
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);
        }
        catch
        {
            // Theme application is non-critical
        }
    }

    /// <summary>
    /// Called when SelectedLanguage changes.
    /// </summary>
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value == null) return;

        // Check if language actually changed from initial
        if (value.Code != _initialLanguageCode)
        {
            RestartRequired = true;
        }
    }

    /// <summary>
    /// Applies the selected language and saves settings.
    /// </summary>
    [RelayCommand]
    private void ApplyLanguage()
    {
        if (SelectedLanguage == null) return;

        // Save the language setting (will take effect on restart)
        SaveSettings();

        // Set the culture for immediate partial effect
        try
        {
            var culture = new CultureInfo(SelectedLanguage.Code);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Resources.Resources.Culture = culture;
        }
        catch
        {
            // Language application is non-critical
        }

        RestartRequired = true;
        StatusMessage = Resources.Resources.Settings_RestartRequired;
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            IsDarkTheme = IsDarkTheme,
            LanguageCode = SelectedLanguage?.Code ?? "en",
            MaxParallelInstalls = MaxParallelInstalls,
            MaxParallelScans = MaxParallelScans
        };

        _settingsService.SaveSettings(settings);
    }

    /// <summary>
    /// Clears the deployment history with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        // Show confirmation dialog
        var result = System.Windows.MessageBox.Show(
            Resources.Resources.Confirm_ClearHistory_Message,
            Resources.Resources.Confirm_ClearHistory_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await _historyService.ClearHistoryAsync();
        StatusMessage = Resources.Resources.Settings_HistoryCleared;
    }

    /// <summary>
    /// Opens the log folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            // Get the Win11Forge Logs folder path (LocalAppData)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logFolderPath = Path.Combine(localAppData, "Win11Forge", "Logs");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            // Open in Windows Explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = logFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Opens the GitHub repository in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/VBlackJack/Win11Forge",
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser launch is non-critical
        }
    }
}

/// <summary>
/// Represents a language option.
/// </summary>
public class LanguageOption
{
    /// <summary>
    /// ISO language code (e.g., "en", "fr").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Display name (e.g., "English", "Français").
    /// </summary>
    public string DisplayName { get; }

    public LanguageOption(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
