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
using MaterialDesignThemes.Wpf;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for persisting application settings (theme, language) to JSON.
/// Settings are stored in %LOCALAPPDATA%\Win11Forge\settings.json.
/// </summary>
public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions;
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

    /// <inheritdoc/>
    public void SaveSettings(AppSettings settings)
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
public class AppSettings
{
    /// <summary>
    /// Whether dark theme is enabled.
    /// </summary>
    public bool IsDarkTheme { get; set; } = true;

    /// <summary>
    /// ISO language code (e.g., "en", "fr").
    /// </summary>
    public string LanguageCode { get; set; } = "en";
}
