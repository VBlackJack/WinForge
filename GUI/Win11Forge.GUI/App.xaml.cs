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
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
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
    /// Thread-safe initialization with defensive null check.
    /// </summary>
    private static IServiceProvider? _services;
    public static IServiceProvider Services
    {
        get => _services ?? throw new InvalidOperationException(
            "Services not initialized. Ensure OnStartup has completed before accessing Services.");
        private set => _services = value;
    }

    /// <summary>
    /// Gets whether services have been initialized.
    /// Use this to safely check before accessing Services during startup.
    /// </summary>
    public static bool IsServicesInitialized => _services != null;

    /// <summary>
    /// Gets whether the user prefers reduced motion (accessibility setting).
    /// </summary>
    public static bool ReducedMotion => AnimationHelper.ReducedMotion;

    /// <summary>
    /// Gets a service from the DI container.
    /// Throws InvalidOperationException if services are not yet initialized.
    /// </summary>
    public static T GetService<T>() where T : class
    {
        if (_services == null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve service {typeof(T).Name}: Services not initialized.");
        }
        return _services.GetRequiredService<T>();
    }

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

            // Step 4: Apply theme using WPF UI ApplicationThemeManager
            Log($"Applying theme: {(settings.IsDarkTheme ? "Dark" : "Light")}");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_ApplyingTheme);
            try
            {
                var appTheme = settings.IsDarkTheme
                    ? ApplicationTheme.Dark
                    : ApplicationTheme.Light;
                ApplicationThemeManager.Apply(appTheme, WindowBackdropType.Mica);

                // Apply all theme-adaptive resources (accent, status, error/warning/success, skeleton)
                ApplyThemeResources(settings.IsDarkTheme);
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

            // Watch for OS theme changes
            SystemThemeWatcher.Watch(mainWindow);

            // Set as main window FIRST (prevents shutdown when splash closes)
            MainWindow = mainWindow;
            mainWindow.Show();

            // Close splash AFTER main window is visible
            splash.CloseWithAnimation();

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
    /// Applies all theme-adaptive resources based on the current theme.
    /// Centralized method called both at startup and on theme toggle.
    /// Handles accent colors, status colors, error/warning/success colors,
    /// skeleton brushes, and badge foreground brushes for both directions.
    /// </summary>
    internal static void ApplyThemeResources(bool isDark)
    {
        var app = Application.Current;
        if (app?.Resources == null) return;

        try
        {
            // Accent brush: Blue for dark, Purple for light
            var accentBrush = isDark
                ? new SolidColorBrush(Color.FromRgb(96, 205, 255))   // #60CDFF - Accent Blue
                : new SolidColorBrush(Color.FromRgb(107, 78, 170));  // #6B4EAA - Accent Purple
            app.Resources["ThemeAdaptiveAccentBrush"] = accentBrush;

            if (isDark)
            {
                RestoreDarkThemeDefaults(app);
            }
            else
            {
                ApplyLightThemeEnhancements(app);
            }

            Log($"{(isDark ? "Dark" : "Light")} theme resources applied");
        }
        catch (Exception ex)
        {
            Log($"Failed to apply theme resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores all brushes to their dark theme defaults (matching FluentThemeBridge.xaml).
    /// Called when switching from light to dark theme.
    /// </summary>
    private static void RestoreDarkThemeDefaults(Application app)
    {
        // Status colors - restore dark theme originals
        app.Resources["StatusInstalledBrush"] = new SolidColorBrush(Color.FromRgb(76, 175, 80));    // #4CAF50
        app.Resources["StatusFailedBrush"] = new SolidColorBrush(Color.FromRgb(244, 67, 54));       // #F44336
        app.Resources["StatusInstallingBrush"] = new SolidColorBrush(Color.FromRgb(33, 150, 243));   // #2196F3
        app.Resources["StatusSkippedBrush"] = new SolidColorBrush(Color.FromRgb(245, 124, 0));      // #F57C00
        app.Resources["StatusPendingBrush"] = new SolidColorBrush(Color.FromRgb(158, 158, 158));     // #9E9E9E

        // Error/Warning/Success - restore dark theme originals (light text on dark backgrounds)
        app.Resources["ErrorTextBrush"] = new SolidColorBrush(Color.FromRgb(239, 83, 80));          // #EF5350
        app.Resources["ErrorIconBrush"] = new SolidColorBrush(Color.FromRgb(239, 83, 80));          // #EF5350
        app.Resources["ErrorBorderBrush"] = new SolidColorBrush(Color.FromRgb(239, 83, 80));        // #EF5350
        app.Resources["ErrorBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(51, 244, 67, 54)); // #33F44336
        app.Resources["WarningTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 183, 77));       // #FFB74D
        app.Resources["WarningIconBrush"] = new SolidColorBrush(Color.FromRgb(255, 183, 77));       // #FFB74D
        app.Resources["WarningBorderBrush"] = new SolidColorBrush(Color.FromRgb(245, 124, 0));      // #F57C00
        app.Resources["WarningBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(51, 245, 124, 0)); // #33F57C00
        app.Resources["SuccessTextBrush"] = new SolidColorBrush(Color.FromRgb(129, 199, 132));      // #81C784
        app.Resources["SuccessIconBrush"] = new SolidColorBrush(Color.FromRgb(129, 199, 132));      // #81C784
        app.Resources["SuccessBorderBrush"] = new SolidColorBrush(Color.FromRgb(76, 175, 80));      // #4CAF50
        app.Resources["SuccessBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(51, 76, 175, 80)); // #334CAF50

        // Skeleton - restore dark theme (white-based opacity)
        app.Resources["SkeletonBaseBrush"] = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));   // #1AFFFFFF
        app.Resources["SkeletonHighlightBrush"] = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF

        // Badge foregrounds - white text on saturated dark-theme backgrounds
        app.Resources["BadgePrimaryForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["BadgeSecondaryForegroundBrush"] = new SolidColorBrush(Colors.White);
    }

    /// <summary>
    /// Applies enhanced contrast colors for light theme to improve readability.
    /// Updates status, error/warning/success, skeleton, and badge colors
    /// to use darker variants that meet WCAG AA contrast ratios on light backgrounds.
    /// </summary>
    private static void ApplyLightThemeEnhancements(Application app)
    {
        // Status colors - darker variants for light backgrounds
        app.Resources["StatusInstalledBrush"] = app.Resources["StatusInstalledLightBrush"];
        app.Resources["StatusFailedBrush"] = app.Resources["StatusFailedLightBrush"];
        app.Resources["StatusInstallingBrush"] = app.Resources["StatusInstallingLightBrush"];
        app.Resources["StatusSkippedBrush"] = app.Resources["StatusSkippedLightBrush"];
        app.Resources["StatusPendingBrush"] = app.Resources["StatusPendingLightBrush"];

        // Error/Warning/Success - darker text for light backgrounds
        app.Resources["ErrorTextBrush"] = app.Resources["ErrorTextLightBrush"];
        app.Resources["ErrorIconBrush"] = app.Resources["ErrorIconLightBrush"];
        app.Resources["ErrorBorderBrush"] = app.Resources["ErrorBorderLightBrush"];
        app.Resources["ErrorBackgroundBrush"] = app.Resources["ErrorBackgroundLightBrush"];
        app.Resources["WarningTextBrush"] = app.Resources["WarningTextLightBrush"];
        app.Resources["WarningIconBrush"] = app.Resources["WarningIconLightBrush"];
        app.Resources["WarningBorderBrush"] = app.Resources["WarningBorderLightBrush"];
        app.Resources["WarningBackgroundBrush"] = app.Resources["WarningBackgroundLightBrush"];
        app.Resources["SuccessTextBrush"] = app.Resources["SuccessTextLightBrush"];
        app.Resources["SuccessIconBrush"] = app.Resources["SuccessIconLightBrush"];
        app.Resources["SuccessBorderBrush"] = app.Resources["SuccessBorderLightBrush"];
        app.Resources["SuccessBackgroundBrush"] = app.Resources["SuccessBackgroundLightBrush"];

        // Skeleton - black-based opacity for light backgrounds
        app.Resources["SkeletonBaseBrush"] = new SolidColorBrush(Color.FromArgb(26, 0, 0, 0));        // 10% black
        app.Resources["SkeletonHighlightBrush"] = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0));   // 20% black

        // Badge foregrounds - keep white for most saturated backgrounds,
        // but dark text for lighter secondary backgrounds
        app.Resources["BadgePrimaryForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["BadgeSecondaryForegroundBrush"] = new SolidColorBrush(Color.FromRgb(33, 33, 33)); // #212121
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
