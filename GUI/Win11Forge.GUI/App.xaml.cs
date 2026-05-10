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
using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Views;

namespace Win11Forge.GUI;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
{
    private const int AnimationFastMs = 150;
    private const int AnimationNormalMs = 300;
    private const int AnimationSlowMs = 500;
    private const int AnimationMicroMs = 50;

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Win11Forge",
        "Logs");
    private static readonly string LogPath = Path.Combine(LogDirectory, "Win11Forge_startup.log");
    private static bool? _reducedMotionOverride;

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
    /// Gets whether reduced motion is enabled.
    /// Uses user override when available, otherwise follows system preference.
    /// </summary>
    public static bool ReducedMotion => _reducedMotionOverride ?? AnimationHelper.ReducedMotion;

    /// <summary>
    /// Sets the user override for reduced motion and reapplies animation resources.
    /// </summary>
    /// <param name="enabled">True/False to force a value, null to follow system preference.</param>
    internal static void SetReducedMotionOverride(bool? enabled)
    {
        _reducedMotionOverride = enabled;
        ApplyAnimationResources(ReducedMotion);
    }

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

            // Step 4: Apply theme through the centralized theme service
            Log($"Applying theme: {settings.ThemeName}");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_ApplyingTheme);
            try
            {
                var themeService = Services.GetRequiredService<IThemeService>();
                themeService.ApplyTheme(settings.ThemeName);
            }
            catch (Exception ex)
            {
                Log($"Theme application failed: {ex.Message}");
            }

            // Step 5: Detect reduced motion preference for accessibility
            SetReducedMotionOverride(settings.ReducedMotionOverride);
            splash.ApplyReducedMotionPreference();
            Log($"Reduced motion preference: {ReducedMotion}");
            InitializeAnimationResources();

            // Step 6: Call base AFTER settings are applied
            base.OnStartup(e);

            // Step 6b: Re-apply visual settings once the WPF startup pipeline is initialized.
            // This prevents occasional theme mismatches on some VM/remote sessions.
            try
            {
                var themeService = Services.GetRequiredService<IThemeService>();
                themeService.ApplyTheme(settings.ThemeName);
                ApplyHighContrastMode(settings.IsHighContrastEnabled);
            }
            catch (Exception ex)
            {
                Log($"Post-startup visual settings re-apply failed: {ex.Message}");
            }

            // Step 7: NOW create and show MainWindow (after culture is set)
            Log("Creating MainWindow...");
            splash.UpdateStatus(Win11Forge.GUI.Resources.Resources.Splash_LoadingInterface);
            var mainWindow = new MainWindow();

            // Keep the persisted in-app theme stable across navigation and restarts.
            // Do not auto-follow OS theme changes unless an explicit user option is added.

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
            Directory.CreateDirectory(LogDirectory);
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
            Directory.CreateDirectory(LogDirectory);
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
    /// Sets animation durations to zero when reduced motion is enabled.
    /// </summary>
    private void InitializeAnimationResources()
    {
        try
        {
            ApplyAnimationResources(ReducedMotion);
            Log($"Animation resources initialized (reduced motion: {ReducedMotion})");
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize animation resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies animation duration resources for the current reduced motion mode.
    /// </summary>
    private static void ApplyAnimationResources(bool reducedMotion)
    {
        var app = Current;
        if (app?.Resources == null)
            return;

        ApplyAnimationResources(app.Resources, reducedMotion);
    }

    /// <summary>
    /// Applies animation duration resources to a target dictionary.
    /// </summary>
    internal static void ApplyAnimationResources(ResourceDictionary resources, bool reducedMotion)
    {
        ArgumentNullException.ThrowIfNull(resources);

        resources["AnimationFast"] = reducedMotion
            ? new Duration(TimeSpan.Zero)
            : new Duration(TimeSpan.FromMilliseconds(AnimationFastMs));
        resources["AnimationNormal"] = reducedMotion
            ? new Duration(TimeSpan.Zero)
            : new Duration(TimeSpan.FromMilliseconds(AnimationNormalMs));
        resources["AnimationSlow"] = reducedMotion
            ? new Duration(TimeSpan.Zero)
            : new Duration(TimeSpan.FromMilliseconds(AnimationSlowMs));
        resources["AnimationMicro"] = reducedMotion
            ? new Duration(TimeSpan.Zero)
            : new Duration(TimeSpan.FromMilliseconds(AnimationMicroMs));
    }

    /// <summary>
    /// Applies or removes high contrast resources.
    /// </summary>
    internal static void ApplyHighContrastMode(bool enable)
    {
        var app = Current;
        if (app?.Resources == null) return;

        var highContrastUri = new Uri("Resources/HighContrastTheme.xaml", UriKind.Relative);

        ResourceDictionary? existingDict = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source?.OriginalString.Contains("HighContrastTheme", StringComparison.Ordinal) == true)
            {
                existingDict = dict;
                break;
            }
        }

        if (existingDict != null)
        {
            app.Resources.MergedDictionaries.Remove(existingDict);
        }

        if (enable)
        {
            try
            {
                var highContrastDict = new ResourceDictionary { Source = highContrastUri };
                app.Resources.MergedDictionaries.Add(highContrastDict);

                // Override commonly-used Fluent brush keys so high contrast is actually visible.
                SwapIfExists(app, "ApplicationBackgroundBrush", "HighContrastBackgroundBrush");
                SwapIfExists(app, "SolidBackgroundFillColorBaseBrush", "HighContrastBackgroundBrush");
                SwapIfExists(app, "SolidBackgroundFillColorSecondaryBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "CardBackgroundFillColorDefaultBrush", "HighContrastCardBrush");
                SwapIfExists(app, "CardBackgroundFillColorSecondaryBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "ControlFillColorDefaultBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "TextFillColorPrimaryBrush", "HighContrastTextPrimaryBrush");
                SwapIfExists(app, "TextFillColorSecondaryBrush", "HighContrastTextSecondaryBrush");
                SwapIfExists(app, "ControlStrokeColorDefaultBrush", "HighContrastBorderBrush");
                SwapIfExists(app, "DividerStrokeColorDefaultBrush", "HighContrastBorderLightBrush");
                SwapIfExists(app, "SubtleDataGridLineBrush", "HighContrastBorderLightBrush");
                SwapIfExists(app, "SystemAccentColorPrimaryBrush", "HighContrastPrimaryBrush");
                SwapIfExists(app, "SystemAccentColorSecondaryBrush", "HighContrastSecondaryBrush");
                SwapIfExists(app, "FocusIndicatorBrush", "HighContrastFocusBorderBrush");
                SwapIfExists(app, "SystemFillColorCriticalBrush", "HighContrastErrorBrush");
                SwapIfExists(app, "ErrorTextBrush", "HighContrastErrorBrush");
                SwapIfExists(app, "WarningTextBrush", "HighContrastWarningBrush");
                SwapIfExists(app, "SuccessTextBrush", "HighContrastSuccessBrush");
                // WPF-UI uses these foregrounds for accent-painted Button states.
                SwapIfExists(app, "TextOnAccentFillColorPrimaryBrush", "HighContrastPrimaryForegroundBrush");
                SwapIfExists(app, "TextOnAccentFillColorSecondaryBrush", "HighContrastPrimaryForegroundBrush");
                SwapIfExists(app, "TextOnAccentFillColorDisabledBrush", "HighContrastTextDisabledBrush");
                SwapIfExists(app, "SourceWingetBadgeBackgroundBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "SourceWingetBadgeBorderBrush", "HighContrastBorderBrush");
                SwapIfExists(app, "SourceWingetBadgeForegroundBrush", "HighContrastTextPrimaryBrush");
                SwapIfExists(app, "SourceChocolateyBadgeBackgroundBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "SourceChocolateyBadgeBorderBrush", "HighContrastBorderBrush");
                SwapIfExists(app, "SourceChocolateyBadgeForegroundBrush", "HighContrastTextPrimaryBrush");
                SwapIfExists(app, "SourceStoreBadgeBackgroundBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "SourceStoreBadgeBorderBrush", "HighContrastBorderBrush");
                SwapIfExists(app, "SourceStoreBadgeForegroundBrush", "HighContrastTextPrimaryBrush");
                SwapIfExists(app, "SourceDirectBadgeBackgroundBrush", "HighContrastSurfaceBrush");
                SwapIfExists(app, "SourceDirectBadgeBorderBrush", "HighContrastBorderBrush");
                SwapIfExists(app, "SourceDirectBadgeForegroundBrush", "HighContrastTextPrimaryBrush");

                // Canonical button taxonomy -> High Contrast variants.
                // Each canonical Style is replaced with the matching HighContrast<Name>Style
                // so explicitly-styled buttons repaint correctly when HC mode is enabled.
                SwapIfExists(app, "HeroPrimaryButton",      "HighContrastHeroPrimaryButtonStyle");
                SwapIfExists(app, "PrimaryButton",          "HighContrastPrimaryButtonStyle");
                SwapIfExists(app, "SecondaryButton",        "HighContrastSecondaryButtonStyle");
                SwapIfExists(app, "OutlinedButton",         "HighContrastOutlinedButtonStyle");
                SwapIfExists(app, "WarningPrimaryButton",   "HighContrastWarningPrimaryButtonStyle");
                SwapIfExists(app, "DestructiveButton",      "HighContrastOutlinedButtonStyle");
                SwapIfExists(app, "DestructiveSolidButton", "HighContrastDestructiveSolidButtonStyle");
                SwapIfExists(app, "IconButton",             "HighContrastIconButtonStyle");
                SwapIfExists(app, "StatsCardButton",        "HighContrastStatsCardButtonStyle");
                SwapIfExists(app, "QuickActionButton",      "HighContrastQuickActionButtonStyle");
                SwapIfExists(app, "FavoriteIconButton",     "HighContrastFavoriteIconButtonStyle");
            }
            catch (Exception ex)
            {
                Log($"Failed to load high contrast resources: {ex.Message}");
            }
        }
        else
        {
            // Only re-apply standard resources when we were actually leaving high contrast mode.
            // Avoid forcing a theme change during normal settings reload when high contrast is already disabled.
            if (existingDict != null)
            {
                try
                {
                    if (IsServicesInitialized)
                    {
                        var themeService = GetService<IThemeService>();
                        if (themeService.CurrentTheme is ThemeNames.Light)
                        {
                            ApplyThemeResources(false);
                        }
                        else
                        {
                            RemoveDraculaThemeDictionaries(app);
                            themeService.ApplyTheme(themeService.CurrentTheme);
                        }
                    }
                    else
                    {
                        var isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
                        ApplyThemeResources(isDark);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to restore standard resources after high contrast: {ex.Message}");
                }
            }
        }
    }

    private static void RemoveDraculaThemeDictionaries(Application app)
    {
        var dictionaries = app.Resources.MergedDictionaries
            .Where(dictionary => dictionary.Source?.OriginalString.Contains(
                ThemeNames.DraculaResourcePathPrefix,
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var dictionary in dictionaries)
        {
            app.Resources.MergedDictionaries.Remove(dictionary);
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
            // Accent brush: Light blue for dark theme, Windows blue for light theme
            var accentBrush = isDark
                ? new SolidColorBrush(Color.FromRgb(96, 205, 255))   // #60CDFF - Accent Blue
                : new SolidColorBrush(Color.FromRgb(0, 95, 184));    // #005FB8 - Windows 11 Blue
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

        // Primary/Secondary hue - restore dark theme originals
        app.Resources["PrimaryHueMidBrush"] = new SolidColorBrush(Color.FromRgb(96, 205, 255));       // #60CDFF
        app.Resources["SecondaryHueMidBrush"] = new SolidColorBrush(Color.FromRgb(139, 195, 74));     // #8BC34A

        // Accent text colors - restore dark theme (light variants for dark backgrounds)
        app.Resources["AccentGreenTextBrush"] = new SolidColorBrush(Color.FromRgb(129, 199, 132));    // #81C784
        app.Resources["AccentOrangeTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 183, 77));    // #FFB74D

        // Accent colors - restore dark theme (bright for dark backgrounds)
        app.Resources["FavoriteActiveBrush"] = new SolidColorBrush(Color.FromRgb(255, 215, 0));       // #FFD700
        app.Resources["FavoriteTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 143, 0));         // #FF8F00
        app.Resources["RequiredBrush"] = new SolidColorBrush(Color.FromRgb(255, 193, 7));             // #FFC107
        app.Resources["ManualInstallBadgeBrush"] = new SolidColorBrush(Color.FromRgb(255, 138, 0));   // #FF8A00
        app.Resources["SubtleDataGridLineBrush"] = new SolidColorBrush(Color.FromArgb(51, 138, 144, 168)); // #338A90A8

        // Skeleton - restore dark theme (white-based opacity)
        app.Resources["SkeletonBaseBrush"] = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));   // #1AFFFFFF
        app.Resources["SkeletonHighlightBrush"] = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF

        // Badge foregrounds - white text on saturated dark-theme backgrounds
        app.Resources["BadgePrimaryForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["BadgeSecondaryForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["PrimaryHueLightForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["SecondaryHueLightForegroundBrush"] = new SolidColorBrush(Colors.White);

        app.Resources["SourceWingetBadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(38, 59, 130, 246));      // #263B82F6
        app.Resources["SourceWingetBadgeBorderBrush"] = new SolidColorBrush(Color.FromRgb(59, 130, 246));              // #3B82F6
        app.Resources["SourceWingetBadgeForegroundBrush"] = new SolidColorBrush(Color.FromRgb(147, 197, 253));         // #93C5FD
        app.Resources["SourceChocolateyBadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(38, 22, 163, 74));  // #2616A34A
        app.Resources["SourceChocolateyBadgeBorderBrush"] = new SolidColorBrush(Color.FromRgb(34, 197, 94));           // #22C55E
        app.Resources["SourceChocolateyBadgeForegroundBrush"] = new SolidColorBrush(Color.FromRgb(134, 239, 172));     // #86EFAC
        app.Resources["SourceStoreBadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(38, 20, 184, 166));      // #2614B8A6
        app.Resources["SourceStoreBadgeBorderBrush"] = new SolidColorBrush(Color.FromRgb(20, 184, 166));               // #14B8A6
        app.Resources["SourceStoreBadgeForegroundBrush"] = new SolidColorBrush(Color.FromRgb(94, 234, 212));           // #5EEAD4
        app.Resources["SourceDirectBadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(42, 249, 115, 22));     // #2AF97316
        app.Resources["SourceDirectBadgeBorderBrush"] = new SolidColorBrush(Color.FromRgb(249, 115, 22));              // #F97316
        app.Resources["SourceDirectBadgeForegroundBrush"] = new SolidColorBrush(Color.FromRgb(253, 186, 116));         // #FDBA74
    }

    /// <summary>
    /// Applies enhanced contrast colors for light theme to improve readability.
    /// Updates status, error/warning/success, skeleton, and badge colors
    /// to use darker variants that meet WCAG AA contrast ratios on light backgrounds.
    /// </summary>
    private static void ApplyLightThemeEnhancements(Application app)
    {
        // Status colors - darker variants for light backgrounds
        SwapIfExists(app, "StatusInstalledBrush", "StatusInstalledLightBrush");
        SwapIfExists(app, "StatusFailedBrush", "StatusFailedLightBrush");
        SwapIfExists(app, "StatusInstallingBrush", "StatusInstallingLightBrush");
        SwapIfExists(app, "StatusSkippedBrush", "StatusSkippedLightBrush");
        SwapIfExists(app, "StatusPendingBrush", "StatusPendingLightBrush");

        // Error/Warning/Success - darker text for light backgrounds
        SwapIfExists(app, "ErrorTextBrush", "ErrorTextLightBrush");
        SwapIfExists(app, "ErrorIconBrush", "ErrorIconLightBrush");
        SwapIfExists(app, "ErrorBorderBrush", "ErrorBorderLightBrush");
        SwapIfExists(app, "ErrorBackgroundBrush", "ErrorBackgroundLightBrush");
        SwapIfExists(app, "WarningTextBrush", "WarningTextLightBrush");
        SwapIfExists(app, "WarningIconBrush", "WarningIconLightBrush");
        SwapIfExists(app, "WarningBorderBrush", "WarningBorderLightBrush");
        SwapIfExists(app, "WarningBackgroundBrush", "WarningBackgroundLightBrush");
        SwapIfExists(app, "SuccessTextBrush", "SuccessTextLightBrush");
        SwapIfExists(app, "SuccessIconBrush", "SuccessIconLightBrush");
        SwapIfExists(app, "SuccessBorderBrush", "SuccessBorderLightBrush");
        SwapIfExists(app, "SuccessBackgroundBrush", "SuccessBackgroundLightBrush");

        // Primary/Secondary hue - muted for light backgrounds
        SwapIfExists(app, "PrimaryHueMidBrush", "PrimaryHueMidLightBrush");
        SwapIfExists(app, "SecondaryHueMidBrush", "SecondaryHueMidLightBrush");

        // Accent text colors - darker variants for light backgrounds
        SwapIfExists(app, "AccentGreenTextBrush", "AccentGreenTextLightBrush");
        SwapIfExists(app, "AccentOrangeTextBrush", "AccentOrangeTextLightBrush");

        // Accent colors - toned down for light backgrounds
        SwapIfExists(app, "FavoriteActiveBrush", "FavoriteActiveLightBrush");
        SwapIfExists(app, "FavoriteTextBrush", "FavoriteTextLightBrush");
        SwapIfExists(app, "RequiredBrush", "RequiredLightBrush");
        SwapIfExists(app, "ManualInstallBadgeBrush", "ManualInstallBadgeLightBrush");
        SwapIfExists(app, "SubtleDataGridLineBrush", "SubtleDataGridLineLightBrush");

        // Card/control borders - stronger for light theme visual hierarchy
        app.Resources["ControlElevationBorderBrush"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));  // #C8C8C8
        app.Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        // Skeleton - black-based opacity for light backgrounds
        app.Resources["SkeletonBaseBrush"] = new SolidColorBrush(Color.FromArgb(26, 0, 0, 0));        // 10% black
        app.Resources["SkeletonHighlightBrush"] = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0));   // 20% black

        // Badge foregrounds - keep white for saturated primary badges, use dark text for lighter badges.
        app.Resources["BadgePrimaryForegroundBrush"] = new SolidColorBrush(Colors.White);
        app.Resources["BadgeSecondaryForegroundBrush"] = new SolidColorBrush(Color.FromRgb(33, 33, 33)); // #212121
        app.Resources["PrimaryHueLightForegroundBrush"] = new SolidColorBrush(Color.FromRgb(33, 33, 33)); // #212121
        app.Resources["SecondaryHueLightForegroundBrush"] = new SolidColorBrush(Color.FromRgb(33, 33, 33)); // #212121

        SwapIfExists(app, "SourceWingetBadgeBackgroundBrush", "SourceWingetBadgeBackgroundLightBrush");
        SwapIfExists(app, "SourceWingetBadgeBorderBrush", "SourceWingetBadgeBorderLightBrush");
        SwapIfExists(app, "SourceWingetBadgeForegroundBrush", "SourceWingetBadgeForegroundLightBrush");
        SwapIfExists(app, "SourceChocolateyBadgeBackgroundBrush", "SourceChocolateyBadgeBackgroundLightBrush");
        SwapIfExists(app, "SourceChocolateyBadgeBorderBrush", "SourceChocolateyBadgeBorderLightBrush");
        SwapIfExists(app, "SourceChocolateyBadgeForegroundBrush", "SourceChocolateyBadgeForegroundLightBrush");
        SwapIfExists(app, "SourceStoreBadgeBackgroundBrush", "SourceStoreBadgeBackgroundLightBrush");
        SwapIfExists(app, "SourceStoreBadgeBorderBrush", "SourceStoreBadgeBorderLightBrush");
        SwapIfExists(app, "SourceStoreBadgeForegroundBrush", "SourceStoreBadgeForegroundLightBrush");
        SwapIfExists(app, "SourceDirectBadgeBackgroundBrush", "SourceDirectBadgeBackgroundLightBrush");
        SwapIfExists(app, "SourceDirectBadgeBorderBrush", "SourceDirectBadgeBorderLightBrush");
        SwapIfExists(app, "SourceDirectBadgeForegroundBrush", "SourceDirectBadgeForegroundLightBrush");
    }

    /// <summary>
    /// Swaps a target resource with a source resource only if the source exists.
    /// Prevents null injection into the resource dictionary during theme switching.
    /// </summary>
    private static void SwapIfExists(Application app, string targetKey, string sourceKey)
    {
        var source = app.TryFindResource(sourceKey);
        if (source != null)
        {
            app.Resources[targetKey] = source;
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
