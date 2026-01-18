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
using System.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "Win11Forge_startup.log");

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class => Services.GetRequiredService<T>();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddWin11ForgeServices();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Called on application startup.
    /// Loads and applies persisted user settings (theme, language) BEFORE UI initialization.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            LogError("UnhandledException", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (s, args) =>
        {
            LogError("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (s, args) =>
            LogError("UnobservedTaskException", args.Exception);

        try
        {
            Log("App.OnStartup starting...");

            // Step 0: Configure dependency injection
            Log("Configuring services...");
            Services = ConfigureServices();

            // Step 1: Load settings FIRST (before any UI)
            Log("Loading settings...");
            var settingsService = Services.GetRequiredService<IAppSettingsService>();
            var settings = settingsService.LoadSettings();

            // Step 2: Apply language/culture BEFORE UI initialization
            Log($"Applying language: {settings.LanguageCode}");
            if (!string.IsNullOrEmpty(settings.LanguageCode))
            {
                try
                {
                    var culture = new CultureInfo(settings.LanguageCode);
                    Thread.CurrentThread.CurrentUICulture = culture;
                    Thread.CurrentThread.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                    Win11Forge.GUI.Resources.Resources.Culture = culture;
                }
                catch (CultureNotFoundException ex)
                {
                    Log($"Invalid culture '{settings.LanguageCode}', using system default: {ex.Message}");
                }
            }

            // Step 3: Apply theme
            Log($"Applying theme: {(settings.IsDarkTheme ? "Dark" : "Light")}");
            try
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();
                theme.SetBaseTheme(settings.IsDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
                paletteHelper.SetTheme(theme);

                // Initialize theme-adaptive accent brush
                InitializeThemeAdaptiveResources(settings.IsDarkTheme);
            }
            catch (Exception ex)
            {
                Log($"Theme application failed: {ex.Message}");
            }

            // Step 4: Call base AFTER settings are applied
            base.OnStartup(e);

            // Step 5: NOW create and show MainWindow (after culture is set)
            Log("Creating MainWindow...");
            var mainWindow = new MainWindow();
            mainWindow.Show();

            Log("Startup complete.");
        }
        catch (Exception ex)
        {
            LogError("OnStartup", ex);
            base.OnStartup(e);

            // Still try to show MainWindow even if settings failed
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception innerEx)
            {
                LogError("MainWindow creation failed", innerEx);
                throw;
            }
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    private static void LogError(string context, Exception? ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:HH:mm:ss}] ERROR in {context}:\n{ex}\n\n");
        }
        catch { }
    }

    /// <summary>
    /// Initializes theme-adaptive resources based on current theme.
    /// Dark theme uses Secondary (lime), Light theme uses Primary (purple).
    /// </summary>
    private void InitializeThemeAdaptiveResources(bool isDark)
    {
        try
        {
            // Use direct colors to ensure correct contrast in each theme
            // Dark theme: lime (#CDDC39) - visible on dark background
            // Light theme: purple (#673AB7) - visible on light background
            var accentBrush = isDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(205, 220, 57))   // Lime
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(103, 58, 183)); // DeepPurple

            Resources["ThemeAdaptiveAccentBrush"] = accentBrush;
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize theme-adaptive resources: {ex.Message}");
        }
    }
}
