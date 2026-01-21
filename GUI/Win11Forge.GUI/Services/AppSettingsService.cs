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

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using MaterialDesignThemes.Wpf;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for persisting application settings (theme, language) to JSON.
/// Settings are stored in %LOCALAPPDATA%\Win11Forge\settings.json.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions;
    private static readonly object _cacheLock = new();
    private static AppSettings? _cachedSettings;

    static AppSettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        var win11ForgePath = Path.Combine(localAppData, "Win11Forge");

        try
        {
            if (!Directory.Exists(win11ForgePath))
            {
                Directory.CreateDirectory(win11ForgePath);
            }
        }
        catch
        {
            // Fallback to temp if creation fails
            win11ForgePath = Path.Combine(Path.GetTempPath(), "Win11Forge");
            try
            {
                if (!Directory.Exists(win11ForgePath))
                {
                    Directory.CreateDirectory(win11ForgePath);
                }
            }
            catch
            {
                // Use temp directly
                win11ForgePath = Path.GetTempPath();
            }
        }

        SettingsFilePath = Path.Combine(win11ForgePath, "settings.json");

        JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc/>
    public AppSettings LoadSettings()
    {
        lock (_cacheLock)
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                        if (settings != null)
                        {
                            _cachedSettings = settings;
                            return settings;
                        }
                    }
                }
            }
            catch
            {
                // If file is corrupted, return defaults
            }

            // Return default settings
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    /// <inheritdoc/>
    public void SaveSettings(AppSettings settings)
    {
        lock (_cacheLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                _cachedSettings = settings;
            }
            catch
            {
                // Silently fail - settings persistence is non-critical
            }
        }
    }

    /// <inheritdoc/>
    public void ApplySettings(AppSettings settings)
    {
        // Apply theme
        try
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(settings.IsDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
        catch
        {
            // Theme application is non-critical
        }

        // Apply high contrast mode
        try
        {
            ApplyHighContrastMode(settings.IsHighContrastEnabled);
        }
        catch
        {
            // High contrast application is non-critical
        }

        // Apply language/culture
        try
        {
            if (!string.IsNullOrEmpty(settings.LanguageCode))
            {
                var culture = new CultureInfo(settings.LanguageCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                Resources.Resources.Culture = culture;
            }
        }
        catch
        {
            // Language application is non-critical
        }
    }

    /// <summary>
    /// Applies or removes high contrast theme resources.
    /// </summary>
    private static void ApplyHighContrastMode(bool enable)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        var highContrastUri = new Uri("Resources/HighContrastTheme.xaml", UriKind.Relative);

        // Remove existing high contrast dictionary if present
        ResourceDictionary? existingDict = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source?.OriginalString.Contains("HighContrastTheme") == true)
            {
                existingDict = dict;
                break;
            }
        }

        if (existingDict != null)
        {
            app.Resources.MergedDictionaries.Remove(existingDict);
        }

        // Add high contrast dictionary if enabled
        if (enable)
        {
            try
            {
                var highContrastDict = new ResourceDictionary { Source = highContrastUri };
                app.Resources.MergedDictionaries.Add(highContrastDict);
            }
            catch
            {
                // High contrast resources may not be available
            }
        }
    }

    /// <summary>
    /// Applies saved settings at application startup.
    /// Call this from App.xaml.cs OnStartup.
    /// </summary>
    public static void ApplyStartupSettings()
    {
        var service = new AppSettingsService();
        var settings = service.LoadSettings();
        service.ApplySettings(settings);
    }
}

/// <summary>
/// Interface for application settings service.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    AppSettings LoadSettings();

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    void SaveSettings(AppSettings settings);

    /// <summary>
    /// Applies settings to the application (theme, language).
    /// </summary>
    void ApplySettings(AppSettings settings);
}

/// <summary>
/// Application settings model.
/// </summary>
public class AppSettings : System.ComponentModel.DataAnnotations.IValidatableObject
{
    /// <summary>
    /// Whether dark theme is enabled.
    /// </summary>
    public bool IsDarkTheme { get; set; } = true;

    /// <summary>
    /// Whether high contrast mode is enabled for accessibility.
    /// </summary>
    public bool IsHighContrastEnabled { get; set; } = false;

    /// <summary>
    /// ISO language code (e.g., "en", "fr").
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Language code is required")]
    [System.ComponentModel.DataAnnotations.StringLength(10, MinimumLength = 2, ErrorMessage = "Language code must be between 2 and 10 characters")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Language code must be in format 'xx' or 'xx-XX'")]
    public string LanguageCode { get; set; } = "en";

    /// <summary>
    /// Maximum number of parallel installations (1-10, default 5).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 10, ErrorMessage = "Max parallel installs must be between 1 and 10")]
    public int MaxParallelInstalls { get; set; } = 5;

    /// <summary>
    /// Maximum number of parallel scans (1-20, default 8).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 20, ErrorMessage = "Max parallel scans must be between 1 and 20")]
    public int MaxParallelScans { get; set; } = 8;

    /// <summary>
    /// Whether this is the first run of the application.
    /// </summary>
    public bool IsFirstRun { get; set; } = true;

    /// <summary>
    /// Last selected navigation index for view state preservation.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, 10, ErrorMessage = "Navigation index must be between 0 and 10")]
    public int LastNavigationIndex { get; set; } = 0;

    /// <summary>
    /// Maximum number of undo actions to keep in history (1-100, default 50).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 100, ErrorMessage = "Max undo history must be between 1 and 100")]
    public int MaxUndoHistory { get; set; } = 50;

    /// <summary>
    /// Whether enhanced tooltips are enabled.
    /// </summary>
    public bool EnhancedTooltipsEnabled { get; set; } = true;

    /// <summary>
    /// Validates the settings model.
    /// </summary>
    public System.Collections.Generic.IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        // High contrast mode is most useful with dark theme
        // This is a warning, not an error - just informational

        // Validate that parallel settings make sense together
        if (MaxParallelInstalls > MaxParallelScans)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "MaxParallelInstalls should not exceed MaxParallelScans for optimal performance",
                new[] { nameof(MaxParallelInstalls), nameof(MaxParallelScans) });
        }
    }
}
