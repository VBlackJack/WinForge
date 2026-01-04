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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// Handles theme and language configuration.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly PaletteHelper _paletteHelper = new();

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
    /// Initializes a new instance of SettingsViewModel.
    /// </summary>
    public SettingsViewModel()
    {
        // Initialize available languages
        AvailableLanguages =
        [
            new LanguageOption("en", "English"),
            new LanguageOption("fr", "Français")
        ];

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
    /// Loads the current theme and language settings.
    /// </summary>
    private void LoadCurrentSettings()
    {
        // Get current theme
        var theme = _paletteHelper.GetTheme();
        IsDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark;

        // Get current culture
        var currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentCulture)
                          ?? AvailableLanguages.First();
    }

    /// <summary>
    /// Called when IsDarkTheme changes.
    /// </summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplyTheme(value);
    }

    /// <summary>
    /// Applies the selected theme.
    /// </summary>
    private void ApplyTheme(bool isDark)
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);

        StatusMessage = Resources.Resources.Settings_ThemeApplied;
    }

    /// <summary>
    /// Called when SelectedLanguage changes.
    /// </summary>
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value == null) return;

        // Check if language actually changed
        var currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (value.Code != currentCulture)
        {
            RestartRequired = true;
            StatusMessage = Resources.Resources.Settings_RestartRequired;
        }
    }

    /// <summary>
    /// Applies the selected language.
    /// </summary>
    [RelayCommand]
    private void ApplyLanguage()
    {
        if (SelectedLanguage == null) return;

        // Set the culture
        var culture = new CultureInfo(SelectedLanguage.Code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // Update resources
        Resources.Resources.Culture = culture;

        RestartRequired = true;
        StatusMessage = Resources.Resources.Settings_RestartRequired;
    }

    /// <summary>
    /// Clears the deployment history.
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var historyService = new DeploymentHistoryService();
        await historyService.ClearHistoryAsync();
        StatusMessage = Resources.Resources.Settings_HistoryCleared;
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
