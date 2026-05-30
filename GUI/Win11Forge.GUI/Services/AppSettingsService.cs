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
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for persisting application settings (theme, language) to JSON.
/// Settings are stored via the centralized repository path service.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
public class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions;
    private readonly string _settingsFilePath;
    private readonly object _cacheLock = new();
    private AppSettings? _cachedSettings;

    static AppSettingsService()
    {
        JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsService"/> class.
    /// </summary>
    public AppSettingsService()
        : this(new RepositoryPathService())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsService"/> class.
    /// </summary>
    /// <param name="pathService">Centralized path service.</param>
    public AppSettingsService(IRepositoryPathService pathService)
        : this((pathService ?? throw new ArgumentNullException(nameof(pathService))).SettingsFilePath)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsService"/> class with a custom settings path.
    /// </summary>
    /// <param name="settingsFilePath">Settings file path. Used by tests to isolate migration scenarios.</param>
    public AppSettingsService(string settingsFilePath)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? new RepositoryPathService().SettingsFilePath
            : settingsFilePath;
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
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                        if (settings != null)
                        {
                            if (TryMigrateThemeSettings(settings, json))
                            {
                                PersistMigratedSettings(settings);
                            }

                            _cachedSettings = settings;
                            return settings;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If file is corrupted, return defaults
                System.Diagnostics.Debug.WriteLine($"Failed to load settings (using defaults): {ex.Message}");
            }

            // Return default settings
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    /// <inheritdoc/>
    public bool SaveSettings(AppSettings settings)
    {
        lock (_cacheLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
                return true;
            }
            catch (Exception ex)
            {
                // Settings persistence is non-critical, but log for diagnostics
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first (thread-safe read)
        lock (_cacheLock)
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }
        }

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        var migrated = TryMigrateThemeSettings(settings, json);
                        lock (_cacheLock)
                        {
                            _cachedSettings = settings;
                        }

                        if (migrated)
                        {
                            await SaveSettingsAsync(settings, cancellationToken);
                        }

                        return settings;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // If file is corrupted, return defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load settings async (using defaults): {ex.Message}");
        }

        // Return and cache default settings
        var defaultSettings = new AppSettings();
        lock (_cacheLock)
        {
            _cachedSettings = defaultSettings;
        }
        return defaultSettings;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);

            lock (_cacheLock)
            {
                _cachedSettings = settings;
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Settings persistence is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to save settings async: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public void ApplySettings(AppSettings settings)
    {
        // Theme is applied via IThemeService by startup and SettingsViewModel callers.
        // Keeping this method focused avoids a settings-service/theme-service dependency cycle.

        // Apply high contrast mode
        try
        {
            App.ApplyHighContrastMode(settings.IsHighContrastEnabled);
        }
        catch (Exception ex)
        {
            // High contrast application is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to apply high contrast mode: {ex.Message}");
        }

        // Apply reduced motion override
        try
        {
            App.SetReducedMotionOverride(settings.ReducedMotionOverride);
        }
        catch (Exception ex)
        {
            // Reduced motion application is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to apply reduced motion setting: {ex.Message}");
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
        catch (Exception ex)
        {
            // Language application is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to apply language setting: {ex.Message}");
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

    private static bool TryMigrateThemeSettings(AppSettings settings, string json)
    {
        var migrated = TryMigrateThemeName(settings, json);
        migrated |= TryMigrateAccentTintName(settings, json);
        return migrated;
    }

    private static bool TryMigrateThemeName(AppSettings settings, string json)
    {
        if (!HasThemeNameProperty(json) || string.IsNullOrWhiteSpace(settings.ThemeName))
        {
            var legacyIsDark = TryReadLegacyIsDarkTheme(json);
            settings.ThemeName = legacyIsDark
                ? ThemeNames.Default
                : ThemeNames.Folio;
            return true;
        }

        var canonicalTheme = ThemeService.NormalizeThemeName(settings.ThemeName);
        if (string.Equals(settings.ThemeName, canonicalTheme, StringComparison.Ordinal))
        {
            return false;
        }

        settings.ThemeName = canonicalTheme;
        return true;
    }

    private static bool TryMigrateAccentTintName(AppSettings settings, string json)
    {
        if (!HasAccentTintNameProperty(json) || string.IsNullOrWhiteSpace(settings.AccentTintName))
        {
            settings.AccentTintName = ThemeNames.DefaultAccentTint;
            return true;
        }

        var canonicalAccentTint = ThemeService.NormalizeAccentTintName(settings.AccentTintName);
        if (string.Equals(settings.AccentTintName, canonicalAccentTint, StringComparison.Ordinal))
        {
            return false;
        }

        settings.AccentTintName = canonicalAccentTint;
        return true;
    }

    private static bool HasThemeNameProperty(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("themeName", out _)
                || document.RootElement.TryGetProperty("ThemeName", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasAccentTintNameProperty(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("accentTintName", out _)
                || document.RootElement.TryGetProperty("AccentTintName", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadLegacyIsDarkTheme(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("isDarkTheme", out var camelCaseValue)
                && (camelCaseValue.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return camelCaseValue.GetBoolean();
            }

            if (document.RootElement.TryGetProperty("IsDarkTheme", out var pascalCaseValue)
                && (pascalCaseValue.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return pascalCaseValue.GetBoolean();
            }
        }
        catch (JsonException)
        {
            return true;
        }

        return true;
    }

    private void PersistMigratedSettings(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to persist migrated settings: {ex.Message}");
        }
    }
}

/// <summary>
/// Interface for application settings service.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Loads settings from disk synchronously.
    /// Prefer LoadSettingsAsync for non-blocking operations.
    /// </summary>
    AppSettings LoadSettings();

    /// <summary>
    /// Loads settings from disk asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded settings or defaults if file doesn't exist</returns>
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves settings to disk synchronously.
    /// Prefer SaveSettingsAsync for non-blocking operations.
    /// Returns false when persistence fails.
    /// </summary>
    bool SaveSettings(AppSettings settings);

    /// <summary>
    /// Saves settings to disk asynchronously.
    /// Returns false when persistence fails.
    /// </summary>
    /// <param name="settings">Settings to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies settings to the application (theme, language).
    /// Must be called on UI thread.
    /// </summary>
    void ApplySettings(AppSettings settings);
}

/// <summary>
/// Application settings model.
/// </summary>
public class AppSettings : System.ComponentModel.DataAnnotations.IValidatableObject
{
    /// <summary>
    /// Canonical theme name.
    /// </summary>
    public string ThemeName { get; set; } = ThemeNames.Default;

    /// <summary>
    /// Canonical ThemeForge accent tint name.
    /// </summary>
    public string AccentTintName { get; set; } = ThemeNames.DefaultAccentTint;

    /// <summary>
    /// Whether the selected theme is dark. Kept for backward compatibility.
    /// </summary>
    [Obsolete("Use ThemeName. Property remains for backward compatibility during migration.", error: false)]
    public bool IsDarkTheme
    {
        get
        {
            return !ThemeNames.IsLightTheme(ThemeName);
        }
    }

    /// <summary>
    /// Whether high contrast mode is enabled for accessibility.
    /// </summary>
    public bool IsHighContrastEnabled { get; set; } = false;

    /// <summary>
    /// User override for reduced motion.
    /// True/False forces the value, null follows system preference.
    /// </summary>
    public bool? ReducedMotionOverride { get; set; }

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
    /// Timeout in minutes for update scan operations (1-30, default 5).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 30, ErrorMessage = "Update scan timeout must be between 1 and 30 minutes")]
    public int UpdateScanTimeoutMinutes { get; set; } = 5;

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
    /// Last known main window restore bounds and state.
    /// </summary>
    public WindowPlacementSettings? MainWindowPlacement { get; set; }

    /// <summary>
    /// Maximum number of undo actions to keep in history (1-100, default 50).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 100, ErrorMessage = "Max undo history must be between 1 and 100")]
    public int MaxUndoHistory { get; set; } = 50;

    /// <summary>
    /// Whether enhanced tooltips are enabled.
    /// </summary>
    public bool EnhancedTooltipsEnabled { get; set; } = true;

    #region Apps Filter State (persisted across navigation)

    /// <summary>
    /// Last search text in the Apps view.
    /// </summary>
    public string AppsLastSearchText { get; set; } = string.Empty;

    /// <summary>
    /// Last selected category filter in the Apps view.
    /// </summary>
    public string AppsLastSelectedCategory { get; set; } = string.Empty;

    /// <summary>
    /// Last selected status filter in the Apps view (0=All, 1=Installed, 2=NotInstalled, 3=Selected, 4=Favorites, 5=HasUpdates).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, 5, ErrorMessage = "Status filter must be between 0 and 5")]
    public int AppsLastStatusFilter { get; set; } = 0;

    /// <summary>
    /// Whether the Favorites column is visible in the Apps view.
    /// </summary>
    public bool AppsShowFavoritesColumn { get; set; } = true;

    /// <summary>
    /// Whether the Version column is visible in the Apps view.
    /// Hidden by default to reduce visual density.
    /// </summary>
    public bool AppsShowVersionColumn { get; set; } = false;

    /// <summary>
    /// Whether the Status column is visible in the Apps view.
    /// </summary>
    public bool AppsShowStatusColumn { get; set; } = true;

    /// <summary>
    /// Whether the Category column is visible in the Apps view.
    /// </summary>
    public bool AppsShowCategoryColumn { get; set; } = true;

    /// <summary>
    /// Whether the Sources column is visible in the Apps view.
    /// Hidden by default to reduce visual density.
    /// </summary>
    public bool AppsShowSourcesColumn { get; set; } = false;

    /// <summary>
    /// Whether the Logs column is visible in the Apps view.
    /// Hidden by default, shown after operations.
    /// </summary>
    public bool AppsShowLogsColumn { get; set; } = false;

    #endregion

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

/// <summary>
/// Persisted restore bounds and launch state for the main window.
/// </summary>
public sealed class WindowPlacementSettings
{
    /// <summary>
    /// Restore-bounds left coordinate in WPF device-independent units.
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// Restore-bounds top coordinate in WPF device-independent units.
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// Restore-bounds width in WPF device-independent units.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Restore-bounds height in WPF device-independent units.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Persisted launch state. Only Normal and Maximized are restored.
    /// </summary>
    public string WindowState { get; set; } = "Normal";
}
