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
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Views;

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
    /// Gets whether the user prefers reduced motion (accessibility setting).
    /// </summary>
    public static bool ReducedMotion => AnimationHelper.ReducedMotion;

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
    /// Shows splash screen during initialization for better UX.
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

        Views.SplashScreen? splash = null;

        try
        {
            Log("App.OnStartup starting...");

            // Step 0: Show splash screen immediately
            Log("Showing splash screen...");
            splash = new Views.SplashScreen();
            splash.Show();
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_Initializing);

            // Step 1: Configure dependency injection
            Log("Configuring services...");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_ConfiguringServices);
            Services = ConfigureServices();

            // Step 2: Load settings FIRST (before any UI)
            Log("Loading settings...");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_LoadingSettings);
            var settingsService = Services.GetRequiredService<IAppSettingsService>();
            var settings = settingsService.LoadSettings();

            // Get version for splash screen
            try
            {
                var powerShellBridge = Services.GetService<IPowerShellBridge>();
                if (powerShellBridge != null)
                {
                    var versionPath = Path.Combine(powerShellBridge.RepositoryRoot, "Config", "version.json");
                    if (File.Exists(versionPath))
                    {
                        var versionJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(versionPath));
                        if (versionJson.RootElement.TryGetProperty("Version", out var versionElement))
                        {
                            splash.SetVersion(versionElement.GetString() ?? "3.0.0");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Version display is non-critical, but log for diagnostics
                System.Diagnostics.Debug.WriteLine($"Failed to get version for splash screen: {ex.Message}");
            }

            // Step 3: Apply language/culture BEFORE UI initialization
            Log($"Applying language: {settings.LanguageCode}");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_ApplyingLanguage);
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

            // Step 4: Apply theme
            Log($"Applying theme: {(settings.IsDarkTheme ? "Dark" : "Light")}");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_ApplyingTheme);
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

            // Step 5: Detect reduced motion preference for accessibility
            Log($"Reduced motion preference: {ReducedMotion}");
            InitializeAnimationResources();

            // Step 6: Call base AFTER settings are applied
            base.OnStartup(e);

            // Step 7: NOW create and show MainWindow (after culture is set)
            Log("Creating MainWindow...");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_LoadingInterface);
            var mainWindow = new MainWindow();

            // Close splash and show main window
            splash.CloseWithAnimation();
            mainWindow.Show();

            Log("Startup complete.");
        }
        catch (Exception ex)
        {
            LogError("OnStartup", ex);

            // Close splash if it's open
            try
            {
                splash?.Close();
            }
            catch (Exception closeEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to close splash screen: {closeEx.Message}");
            }

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
        catch (Exception ex)
        {
            // File logging failed - use debug output as fallback
            System.Diagnostics.Debug.WriteLine($"[Win11Forge] {message} (file log failed: {ex.Message})");
        }
    }

    private static void LogError(string context, Exception? ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:HH:mm:ss}] ERROR in {context}:\n{ex}\n\n");
        }
        catch (Exception logEx)
        {
            // File logging failed - use debug output as fallback
            System.Diagnostics.Debug.WriteLine($"[Win11Forge ERROR] {context}: {ex?.Message} (file log failed: {logEx.Message})");
        }
    }

    /// <summary>
    /// Initializes animation resources based on reduced motion preference.
    /// Sets animation durations to zero if user prefers reduced motion.
    /// </summary>
    private void InitializeAnimationResources()
    {
        try
        {
            if (ReducedMotion)
            {
                // Override animation durations with instant durations
                Resources["AnimationFast"] = new Duration(TimeSpan.Zero);
                Resources["AnimationNormal"] = new Duration(TimeSpan.Zero);
                Resources["AnimationSlow"] = new Duration(TimeSpan.Zero);
                Log("Animation durations set to zero (reduced motion enabled)");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize animation resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes theme-adaptive resources based on current theme.
    /// Dark theme uses Secondary (lime), Light theme uses Primary (purple).
    /// Applies enhanced contrast colors for light theme to meet WCAG AA standards.
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

            // Apply light theme contrast enhancements
            if (!isDark)
            {
                ApplyLightThemeContrastEnhancements();
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize theme-adaptive resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies enhanced contrast colors for light theme to improve readability.
    /// Updates status colors to use darker variants that meet WCAG AA contrast ratios.
    /// </summary>
    private void ApplyLightThemeContrastEnhancements()
    {
        try
        {
            // Update status colors with higher contrast versions for light theme
            Resources["StatusInstalledBrush"] = Resources["StatusInstalledLightBrush"];
            Resources["StatusFailedBrush"] = Resources["StatusFailedLightBrush"];
            Resources["StatusInstallingBrush"] = Resources["StatusInstallingLightBrush"];
            Resources["StatusSkippedBrush"] = Resources["StatusSkippedLightBrush"];
            Resources["StatusPendingBrush"] = Resources["StatusPendingLightBrush"];

            // Update skeleton colors for light theme visibility
            Resources["SkeletonBaseBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(26, 0, 0, 0)); // 10% black
            Resources["SkeletonHighlightBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(51, 0, 0, 0)); // 20% black

            Log("Light theme contrast enhancements applied");
        }
        catch (Exception ex)
        {
            Log($"Failed to apply light theme contrast enhancements: {ex.Message}");
        }
    }

    /// <summary>
    /// Called on application exit.
    /// Disposes all services that implement IDisposable.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log("Application exiting, disposing services...");

            // Dispose the service provider which will dispose all IDisposable services
            if (Services is IDisposable disposableServices)
            {
                disposableServices.Dispose();
            }

            Log("Services disposed successfully.");
        }
        catch (Exception ex)
        {
            LogError("OnExit", ex);
        }

        base.OnExit(e);
    }
}
