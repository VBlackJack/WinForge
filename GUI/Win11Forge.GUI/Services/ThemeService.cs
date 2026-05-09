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

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Resources;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Centralized UI-thread theme service for WPF-UI Light and Dracula ResourceDictionary themes.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string HighContrastResourceMarker = "HighContrastTheme";
    private const string ThemeFileSuffix = "Theme.xaml";
    private const string DisplayKeyPrefix = "Settings_ThemeName_";
    private const string AccentBrushKey = "AccentBrush";
    private const string AccentColorKey = "AccentColor";
    private const string ThemeAdaptiveAccentBrushKey = "ThemeAdaptiveAccentBrush";
    private const byte FallbackAccentRed = 96;
    private const byte FallbackAccentGreen = 205;
    private const byte FallbackAccentBlue = 255;

    private static readonly IReadOnlyDictionary<string, string> PaletteBrushResourceMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplicationBackgroundBrush"] = "BackgroundBrush",
            ["SolidBackgroundFillColorBaseBrush"] = "BackgroundBrush",
            ["SolidBackgroundFillColorSecondaryBrush"] = "SurfaceBrush",
            ["LayerFillColorDefaultBrush"] = "SurfaceBrush",
            ["LayerFillColorAltBrush"] = "BackgroundBrush",
            ["CardBackgroundFillColorDefaultBrush"] = "CardBrush",
            ["CardBackgroundFillColorSecondaryBrush"] = "SurfaceBrush",
            ["ControlFillColorDefaultBrush"] = "SurfaceBrush",
            ["ControlFillColorSecondaryBrush"] = "HighlightBrush",
            ["ControlFillColorTertiaryBrush"] = "CardBrush",
            ["ControlFillColorDisabledBrush"] = "SurfaceBrush",
            ["SubtleFillColorSecondaryBrush"] = "HighlightBrush",
            ["SubtleFillColorTertiaryBrush"] = "HighlightBrush",
            ["ControlStrokeColorDefaultBrush"] = "BorderBrush",
            ["ControlStrokeColorSecondaryBrush"] = "HighlightBrush",
            ["ControlElevationBorderBrush"] = "BorderBrush",
            ["DividerStrokeColorDefaultBrush"] = "BorderBrush",
            ["TextFillColorPrimaryBrush"] = "TextPrimaryBrush",
            ["TextFillColorSecondaryBrush"] = "TextSecondaryBrush",
            ["TextFillColorTertiaryBrush"] = "TextTertiaryBrush",
            ["TextFillColorDisabledBrush"] = "TextDisabledBrush",
            ["TextOnAccentFillColorPrimaryBrush"] = "BadgeTextBrush",
            ["TextOnAccentFillColorSecondaryBrush"] = "BadgeTextBrush",
            ["TextOnAccentFillColorDisabledBrush"] = "TextDisabledBrush",
            ["SystemAccentColorPrimaryBrush"] = "AccentBrush",
            ["SystemAccentColorSecondaryBrush"] = "AccentHoverBrush",
            ["SystemAccentColorTertiaryBrush"] = "AccentPressedBrush",
            ["AccentTextFillColorPrimaryBrush"] = "AccentBrush",
            ["AccentTextFillColorSecondaryBrush"] = "AccentHoverBrush",
            ["AccentTextFillColorTertiaryBrush"] = "AccentPressedBrush",
            ["ControlFillColorInputActiveBrush"] = "CardBrush",
            ["ControlFillColorTransparentBrush"] = "SurfaceBrush",
            ["ControlStrongFillColorDefaultBrush"] = "AccentBrush",
            ["ControlStrongFillColorDisabledBrush"] = "TextDisabledBrush",
            ["ControlAltFillColorTransparentBrush"] = "BackgroundBrush",
            ["ControlAltFillColorSecondaryBrush"] = "SurfaceBrush",
            ["ControlAltFillColorTertiaryBrush"] = "CardBrush",
            ["ControlAltFillColorQuarternaryBrush"] = "HighlightBrush",
            ["ControlAltFillColorDisabledBrush"] = "SurfaceBrush",
            ["ControlStrokeColorOnAccentDefaultBrush"] = "BadgeTextBrush",
            ["ControlStrokeColorOnAccentSecondaryBrush"] = "BadgeTextBrush",
            ["ControlStrokeColorOnAccentTertiaryBrush"] = "BadgeTextBrush",
            ["ControlStrokeColorOnAccentDisabledBrush"] = "TextDisabledBrush",
            ["CardStrokeColorDefaultBrush"] = "BorderBrush",
            ["CardStrokeColorDefaultSolidBrush"] = "BorderBrush",
            ["SystemFillColorSuccessBrush"] = "SuccessBrush",
            ["SystemFillColorCautionBrush"] = "WarningBrush",
            ["SystemFillColorCriticalBrush"] = "ErrorBrush",
            ["SystemFillColorNeutralBrush"] = "TextDisabledBrush",
            ["SystemFillColorSolidNeutralBrush"] = "SurfaceBrush",
            ["ThemeAdaptiveAccentBrush"] = "AccentBrush",
            ["PrimaryHueMidBrush"] = "AccentBrush",
            ["SecondaryHueMidBrush"] = "InfoBrush",
            ["StatusPendingBrush"] = "TextDisabledBrush",
            ["StatusInstallingBrush"] = "InfoBrush",
            ["StatusInstalledBrush"] = "SuccessBrush",
            ["StatusFailedBrush"] = "ErrorBrush",
            ["StatusSkippedBrush"] = "WarningBrush",
            ["StatusSuccessBrush"] = "SuccessBrush",
            ["ErrorBackgroundBrush"] = "ErrorBrush",
            ["ErrorBorderBrush"] = "ErrorBrush",
            ["ErrorTextBrush"] = "ErrorTextBrush",
            ["ErrorIconBrush"] = "ErrorTextBrush",
            ["ValidationErrorBorderBrush"] = "ErrorBrush",
            ["WarningBackgroundBrush"] = "WarningBrush",
            ["WarningBorderBrush"] = "WarningBrush",
            ["WarningTextBrush"] = "WarningTextBrush",
            ["WarningIconBrush"] = "WarningTextBrush",
            ["SuccessBackgroundBrush"] = "SuccessBrush",
            ["SuccessBorderBrush"] = "SuccessBrush",
            ["SuccessTextBrush"] = "SuccessTextBrush",
            ["SuccessIconBrush"] = "SuccessTextBrush",
            ["ManualInstallBadgeBrush"] = "WarningBrush",
            ["RequiredBrush"] = "WarningBrush",
            ["PrimaryHueLightBrush"] = "AccentBrush",
            ["SecondaryHueLightBrush"] = "InfoBrush",
            ["BadgePrimaryForegroundBrush"] = "BadgeTextBrush",
            ["BadgeSecondaryForegroundBrush"] = "BadgeTextBrush",
            ["PrimaryHueLightForegroundBrush"] = "BadgeTextBrush",
            ["SecondaryHueLightForegroundBrush"] = "BadgeTextBrush",
            ["DialogOverlayBackgroundBrush"] = "OverlayBackground",
            ["SkeletonBaseBrush"] = "SurfaceBrush",
            ["SkeletonHighlightBrush"] = "HighlightBrush"
        };

    private static readonly IReadOnlyDictionary<string, string> PaletteColorResourceMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SystemAccentColor"] = "AccentColor",
            ["SystemAccentColorPrimary"] = "AccentColor",
            ["SystemAccentColorSecondary"] = "AccentHoverColor",
            ["SystemAccentColorTertiary"] = "AccentPressedColor"
        };

    private static readonly IReadOnlyList<string> PaletteBridgeResourceKeys =
        PaletteBrushResourceMap.Keys
            .Concat(PaletteColorResourceMap.Keys)
            .ToArray();

    private static readonly IReadOnlyList<ThemeDescriptor> ThemeCatalogue =
    [
        new ThemeDescriptor(
            ThemeNames.Light,
            false,
            null,
            BuildDisplayKey(ThemeNames.Light)),
        CreateDraculaDescriptor(ThemeNames.DraculaPro, true),
        CreateDraculaDescriptor(ThemeNames.Alucard, false),
        CreateDraculaDescriptor(ThemeNames.Blade, true),
        CreateDraculaDescriptor(ThemeNames.Buffy, true),
        CreateDraculaDescriptor(ThemeNames.Lincoln, true),
        CreateDraculaDescriptor(ThemeNames.Morbius, true),
        CreateDraculaDescriptor(ThemeNames.VanHelsing, true)
    ];

    private static readonly IReadOnlyDictionary<string, ThemeDescriptor> ThemeLookup =
        ThemeCatalogue.ToDictionary(theme => theme.Name, StringComparer.OrdinalIgnoreCase);

    private readonly IAppSettingsService _settingsService;
    private readonly Action _applyHighContrastMode;
    private int _themeRevision;
    private bool _hasAppliedTheme;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="settingsService">Settings service used to persist canonical fallbacks.</param>
    public ThemeService(IAppSettingsService settingsService)
        : this(settingsService, () => App.ApplyHighContrastMode(true))
    {
    }

    internal ThemeService(IAppSettingsService settingsService, Action applyHighContrastMode)
    {
        _settingsService = settingsService;
        _applyHighContrastMode = applyHighContrastMode;
    }

    /// <inheritdoc/>
    public string CurrentTheme { get; private set; } = ThemeNames.Default;

    /// <inheritdoc/>
    public int ThemeRevision => _themeRevision;

    /// <inheritdoc/>
    public IReadOnlyList<ThemeDescriptor> AvailableThemes => ThemeCatalogue;

    /// <inheritdoc/>
    public event Action<string>? ThemeChanged;

    /// <inheritdoc/>
    public void ApplyTheme(string? themeName)
    {
        var descriptor = ResolveTheme(themeName);
        PersistCanonicalThemeIfNeeded(themeName, descriptor.Name);

        var app = Application.Current;
        if (app is null)
        {
            if (_hasAppliedTheme && string.Equals(CurrentTheme, descriptor.Name, StringComparison.Ordinal))
            {
                return;
            }

            CommitTheme(descriptor.Name);
            return;
        }

        if (_hasAppliedTheme
            && string.Equals(CurrentTheme, descriptor.Name, StringComparison.Ordinal)
            && HasExpectedResourceDictionary(app, descriptor))
        {
            ReapplyHighContrastIfEnabled(app);
            return;
        }

        try
        {
            ClearPaletteBridgeResources(app.Resources);
            RemoveDraculaResourceDictionaries(app);
            MergeResourceDictionary(app, descriptor);
            ApplyWpfUiTheme(descriptor);
            ApplyPaletteResources(app, descriptor);
            ReapplyHighContrastIfEnabled(app);
            CommitTheme(descriptor.Name);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply theme '{descriptor.Name}': {ex.Message}");
        }
    }

    private static ThemeDescriptor CreateDraculaDescriptor(string name, bool isDark)
    {
        return new ThemeDescriptor(
            name,
            isDark,
            new Uri($"{ThemeNames.DraculaResourcePathPrefix}{name}{ThemeFileSuffix}", UriKind.Relative),
            BuildDisplayKey(name));
    }

    private static string BuildDisplayKey(string name)
    {
        return $"{DisplayKeyPrefix}{name}";
    }

    private static ThemeDescriptor ResolveTheme(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return ThemeLookup[ThemeNames.Default];
        }

        return ThemeLookup.TryGetValue(themeName, out var descriptor)
            ? descriptor
            : ThemeLookup[ThemeNames.Default];
    }

    private void PersistCanonicalThemeIfNeeded(string? requestedThemeName, string canonicalThemeName)
    {
        if (string.Equals(requestedThemeName, canonicalThemeName, StringComparison.Ordinal))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                if (!string.Equals(settings.ThemeName, canonicalThemeName, StringComparison.Ordinal))
                {
                    settings.ThemeName = canonicalThemeName;
                    await _settingsService.SaveSettingsAsync(settings, default);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to persist canonical theme '{canonicalThemeName}': {ex.Message}");
            }
        });
    }

    private static bool HasExpectedResourceDictionary(Application app, ThemeDescriptor descriptor)
    {
        if (descriptor.ResourceUri is null)
        {
            return !HasDraculaResourceDictionary(app);
        }

        return app.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Equals(
                descriptor.ResourceUri.OriginalString,
                StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasDraculaResourceDictionary(Application app)
    {
        return app.Resources.MergedDictionaries.Any(IsDraculaResourceDictionary);
    }

    private static bool IsDraculaResourceDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Source?.OriginalString.Contains(
            ThemeNames.DraculaResourcePathPrefix,
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void RemoveDraculaResourceDictionaries(Application app)
    {
        var dictionaries = app.Resources.MergedDictionaries
            .Where(IsDraculaResourceDictionary)
            .ToList();

        foreach (var dictionary in dictionaries)
        {
            app.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    private static void MergeResourceDictionary(Application app, ThemeDescriptor descriptor)
    {
        if (descriptor.ResourceUri is null)
        {
            return;
        }

        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = descriptor.ResourceUri
        });
    }

    private static void ApplyWpfUiTheme(ThemeDescriptor descriptor)
    {
        var appTheme = descriptor.IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        var backdrop = descriptor.ResourceUri is null
            ? WindowBackdropType.Mica
            : WindowBackdropType.None;
        ApplicationThemeManager.Apply(appTheme, backdrop);
    }

    private static void ApplyPaletteResources(Application app, ThemeDescriptor descriptor)
    {
        if (descriptor.ResourceUri is null)
        {
            App.ApplyThemeResources(false);
            return;
        }

        var accentColor = ResolveAccentColor(app);
        ApplicationAccentColorManager.Apply(accentColor);
        ApplyPaletteBridgeResources(app.Resources);
        app.Resources[ThemeAdaptiveAccentBrushKey] = new SolidColorBrush(accentColor);
    }

    internal static void ApplyPaletteBridgeResources(ResourceDictionary resources)
    {
        foreach (var (targetKey, sourceKey) in PaletteBrushResourceMap)
        {
            if (TryFindResource(resources, sourceKey) is SolidColorBrush brush)
            {
                resources[targetKey] = new SolidColorBrush(brush.Color);
            }
        }

        foreach (var (targetKey, sourceKey) in PaletteColorResourceMap)
        {
            if (TryFindResource(resources, sourceKey) is Color color)
            {
                resources[targetKey] = color;
            }
        }
    }

    internal static void ClearPaletteBridgeResources(ResourceDictionary resources)
    {
        foreach (var key in PaletteBridgeResourceKeys)
        {
            resources.Remove(key);
        }
    }

    private static object? TryFindResource(ResourceDictionary resources, string key)
    {
        if (resources.Contains(key))
        {
            return resources[key];
        }

        for (var index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var resource = TryFindResource(resources.MergedDictionaries[index], key);
            if (resource is not null)
            {
                return resource;
            }
        }

        return null;
    }

    private static Color ResolveAccentColor(Application app)
    {
        if (app.TryFindResource(AccentColorKey) is Color accentColor)
        {
            return accentColor;
        }

        if (app.TryFindResource(AccentBrushKey) is SolidColorBrush accentBrush)
        {
            return accentBrush.Color;
        }

        return Color.FromRgb(FallbackAccentRed, FallbackAccentGreen, FallbackAccentBlue);
    }

    internal void ReapplyHighContrastIfEnabled(bool isHighContrastEnabled)
    {
        if (isHighContrastEnabled)
        {
            _applyHighContrastMode();
        }
    }

    private void ReapplyHighContrastIfEnabled(Application app)
    {
        try
        {
            ReapplyHighContrastIfEnabled(HasHighContrastResourceDictionary(app));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to re-apply high contrast resources after theme change: {ex.Message}");
        }
    }

    private static bool HasHighContrastResourceDictionary(Application app)
    {
        return app.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains(
                HighContrastResourceMarker,
                StringComparison.OrdinalIgnoreCase) == true);
    }

    private void CommitTheme(string canonicalThemeName)
    {
        CurrentTheme = canonicalThemeName;
        _hasAppliedTheme = true;
        _themeRevision++;
        ThemeChanged?.Invoke(canonicalThemeName);
    }
}
