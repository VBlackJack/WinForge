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
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Resources;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using ThemeForgeAccentTint = ThemeForge.Theme.AccentTint;
using ThemeForgeAccentTints = ThemeForge.Theme.AccentTints;
using ThemeForgeIThemeService = ThemeForge.Theme.IThemeService;
using ThemeForgeNames = ThemeForge.Theme.ThemeNames;
using ThemeForgeThemeService = ThemeForge.Theme.ThemeService;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Win11Forge compatibility wrapper around the ThemeForge theme engine.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string HighContrastResourceMarker = "HighContrastTheme";
    private const string DisplayKeyPrefix = "Settings_ThemeName_";
    private const string AccentDisplayKeyPrefix = "Settings_AccentTintName_";
    private const byte FallbackAccentRed = 189;
    private const byte FallbackAccentGreen = 147;
    private const byte FallbackAccentBlue = 249;

    private static readonly IReadOnlyDictionary<string, string> LegacyThemeMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeNames.Light] = ThemeNames.Folio,
            [ThemeNames.Alucard] = ThemeNames.Parchment,
            [ThemeNames.DraculaPro] = ThemeNames.Drakul,
            [ThemeNames.Blade] = ThemeNames.Drakul,
            [ThemeNames.Buffy] = ThemeNames.Drakul,
            [ThemeNames.Lincoln] = ThemeNames.Drakul,
            [ThemeNames.Morbius] = ThemeNames.Drakul,
            [ThemeNames.VanHelsing] = ThemeNames.Drakul,
            ["Dark"] = ThemeNames.Drakul
        };

    private static readonly IReadOnlyList<ThemeDescriptor> ThemeCatalogue =
        ThemeForgeNames.All
            .Select(name => new ThemeDescriptor(
                name,
                !ThemeNames.IsLightTheme(name),
                null,
                BuildDisplayKey(name)))
            .ToArray();

    private static readonly IReadOnlyDictionary<string, ThemeDescriptor> ThemeLookup =
        ThemeCatalogue.ToDictionary(theme => theme.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<AccentTintDescriptor> AccentTintCatalogue =
        ThemeForgeAccentTints.All
            .Select(tint =>
            {
                string name = tint.ToString();
                return new AccentTintDescriptor(name, BuildAccentDisplayKey(name));
            })
            .ToArray();

    private static readonly IReadOnlyDictionary<string, string> AccentTintLookup =
        AccentTintCatalogue.ToDictionary(tint => tint.Name, tint => tint.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<string> PaletteBridgeResourceKeys =
    [
        "ApplicationBackgroundBrush",
        "SolidBackgroundFillColorBaseBrush",
        "SolidBackgroundFillColorSecondaryBrush",
        "LayerFillColorDefaultBrush",
        "LayerFillColorAltBrush",
        "CardBackgroundFillColorDefaultBrush",
        "CardBackgroundFillColorSecondaryBrush",
        "ControlFillColorDefaultBrush",
        "ControlFillColorSecondaryBrush",
        "ControlFillColorTertiaryBrush",
        "ControlFillColorDisabledBrush",
        "SubtleFillColorSecondaryBrush",
        "SubtleFillColorTertiaryBrush",
        "ControlStrokeColorDefaultBrush",
        "ControlStrokeColorSecondaryBrush",
        "ControlElevationBorderBrush",
        "DividerStrokeColorDefaultBrush",
        "TextFillColorPrimaryBrush",
        "TextFillColorSecondaryBrush",
        "TextFillColorTertiaryBrush",
        "TextFillColorDisabledBrush",
        "TextOnAccentFillColorPrimaryBrush",
        "TextOnAccentFillColorSecondaryBrush",
        "TextOnAccentFillColorDisabledBrush",
        "SystemAccentColorPrimaryBrush",
        "SystemAccentColorSecondaryBrush",
        "SystemAccentColorTertiaryBrush",
        "AccentTextFillColorPrimaryBrush",
        "AccentTextFillColorSecondaryBrush",
        "AccentTextFillColorTertiaryBrush",
        "AccentFillColorDefaultBrush",
        "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush",
        "AccentFillColorDisabledBrush",
        "AccentFillColorSelectedTextBackgroundBrush",
        "AccentButtonBackground",
        "AccentButtonBackgroundPointerOver",
        "AccentButtonBackgroundPressed",
        "AccentButtonBackgroundDisabled",
        "AccentButtonForeground",
        "AccentButtonForegroundPointerOver",
        "AccentButtonForegroundPressed",
        "AccentButtonForegroundDisabled",
        "AccentButtonBorderBrush",
        "AccentButtonBorderBrushPointerOver",
        "AccentButtonBorderBrushPressed",
        "AccentControlElevationBorderBrush",
        "ButtonBackground",
        "ButtonForeground",
        "ButtonBorderBrush",
        "ButtonBackgroundPointerOver",
        "ButtonBackgroundPressed",
        "ButtonBackgroundDisabled",
        "ButtonForegroundPointerOver",
        "ButtonForegroundPressed",
        "ButtonForegroundDisabled",
        "ButtonBorderBrushPressed",
        "ButtonBorderBrushDisabled",
        "CheckBoxBackground",
        "CheckBoxForeground",
        "CheckBoxBorderBrush",
        "CheckBoxCheckBorderBrush",
        "CheckBoxCheckGlyphForeground",
        "CheckBoxCheckBackgroundFillChecked",
        "CheckBoxCheckBackgroundFillCheckedPointerOver",
        "CheckBoxCheckBackgroundFillCheckedPressed",
        "CheckBoxCheckBackgroundFillUncheckedPointerOver",
        "CheckBoxCheckBackgroundFillUncheckedPressed",
        "CheckBoxCheckBackgroundFillUncheckedDisabled",
        "CheckBoxCheckBackgroundStrokeUncheckedDisabled",
        "CheckBoxForegroundUncheckedDisabled",
        "ControlFillColorInputActiveBrush",
        "ControlFillColorTransparentBrush",
        "ControlStrongFillColorDefaultBrush",
        "ControlStrongFillColorDisabledBrush",
        "ControlAltFillColorTransparentBrush",
        "ControlAltFillColorSecondaryBrush",
        "ControlAltFillColorTertiaryBrush",
        "ControlAltFillColorQuarternaryBrush",
        "ControlAltFillColorDisabledBrush",
        "ControlStrokeColorOnAccentDefaultBrush",
        "ControlStrokeColorOnAccentSecondaryBrush",
        "ControlStrokeColorOnAccentTertiaryBrush",
        "ControlStrokeColorOnAccentDisabledBrush",
        "CardStrokeColorDefaultBrush",
        "CardStrokeColorDefaultSolidBrush",
        "SystemFillColorSuccessBrush",
        "SystemFillColorCautionBrush",
        "SystemFillColorCriticalBrush",
        "SystemFillColorNeutralBrush",
        "SystemFillColorSolidNeutralBrush",
        "ThemeAdaptiveAccentBrush",
        "PrimaryHueMidBrush",
        "SecondaryHueMidBrush",
        "PrimaryHueLightBrush",
        "SecondaryHueLightBrush",
        "StatusPendingBrush",
        "StatusInstallingBrush",
        "StatusInstalledBrush",
        "StatusFailedBrush",
        "StatusSkippedBrush",
        "StatusSuccessBrush",
        "ErrorBackgroundBrush",
        "ErrorBorderBrush",
        "ErrorTextBrush",
        "ErrorIconBrush",
        "ValidationErrorBorderBrush",
        "WarningBackgroundBrush",
        "WarningBorderBrush",
        "WarningTextBrush",
        "WarningIconBrush",
        "SuccessBackgroundBrush",
        "SuccessBorderBrush",
        "SuccessTextBrush",
        "SuccessIconBrush",
        "ManualInstallBadgeBrush",
        "RequiredBrush",
        "BadgePrimaryForegroundBrush",
        "BadgeSecondaryForegroundBrush",
        "PrimaryHueLightForegroundBrush",
        "SecondaryHueLightForegroundBrush",
        "DialogOverlayBackgroundBrush",
        "SkeletonBaseBrush",
        "SkeletonHighlightBrush",
        "SystemAccentColor",
        "SystemAccentColorPrimary",
        "SystemAccentColorSecondary",
        "SystemAccentColorTertiary",
        "AccentFillColorDefault",
        "AccentFillColorSecondary",
        "AccentFillColorTertiary",
        "PaletteRedColor",
        "PaletteGreenColor",
        "PaletteOrangeColor",
        "PaletteLightBlueColor"
    ];

    private readonly IAppSettingsService _settingsService;
    private readonly Action _applyHighContrastMode;
    private ThemeForgeIThemeService? _themeEngine;
    private string _currentTheme = ThemeNames.Default;
    private string _currentAccentTint = ThemeNames.DefaultAccentTint;
    private int _themeRevision;

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
    public string CurrentTheme => _currentTheme;

    /// <inheritdoc/>
    public int ThemeRevision => _themeRevision;

    /// <inheritdoc/>
    public IReadOnlyList<ThemeDescriptor> AvailableThemes => ThemeCatalogue;

    /// <inheritdoc/>
    public string CurrentAccentTint => _currentAccentTint;

    /// <inheritdoc/>
    public IReadOnlyList<AccentTintDescriptor> AvailableAccentTints => AccentTintCatalogue;

    /// <inheritdoc/>
    public event Action<string>? ThemeChanged;

    /// <inheritdoc/>
    public void ApplyTheme(string? themeName)
    {
        string canonicalTheme = NormalizeThemeName(themeName);
        PersistCanonicalThemeIfNeeded(themeName, canonicalTheme);

        Application? app = Application.Current;
        if (app is null)
        {
            CommitWithoutApplication(canonicalTheme, _currentAccentTint);
            return;
        }

        try
        {
            ThemeForgeIThemeService engine = GetThemeEngine(app);
            int revisionBefore = engine.ThemeRevision;
            bool changed = !string.Equals(engine.CurrentTheme, canonicalTheme, StringComparison.Ordinal);

            if (changed)
            {
                ClearPaletteBridgeResources(app.Resources);
                engine.ApplyTheme(canonicalTheme);
            }

            ApplyWpfUiTheme(canonicalTheme);
            ApplyPaletteBridgeResources(app.Resources);
            ReapplyHighContrastIfEnabled(app);
            CommitFromEngine(engine, revisionBefore);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply theme '{canonicalTheme}': {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void ApplyAccentTint(string? accentTintName)
    {
        string canonicalTint = NormalizeAccentTintName(accentTintName);
        PersistCanonicalAccentTintIfNeeded(accentTintName, canonicalTint);

        Application? app = Application.Current;
        if (app is null)
        {
            CommitWithoutApplication(_currentTheme, canonicalTint);
            return;
        }

        try
        {
            ThemeForgeIThemeService engine = GetThemeEngine(app);
            if (string.IsNullOrWhiteSpace(engine.CurrentTheme))
            {
                engine.ApplyTheme(_currentTheme);
            }

            int revisionBefore = engine.ThemeRevision;
            ThemeForgeAccentTint tint = Enum.Parse<ThemeForgeAccentTint>(canonicalTint, ignoreCase: false);
            if (engine.CurrentAccentTint != tint)
            {
                ClearPaletteBridgeResources(app.Resources);
                engine.ApplyAccentTint(tint);
            }

            ApplyPaletteBridgeResources(app.Resources);
            ReapplyHighContrastIfEnabled(app);
            CommitFromEngine(engine, revisionBefore);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply accent tint '{canonicalTint}': {ex.Message}");
        }
    }

    internal static string NormalizeThemeName(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return ThemeNames.Default;
        }

        string trimmed = themeName.Trim();
        if (ThemeLookup.TryGetValue(trimmed, out ThemeDescriptor? descriptor))
        {
            return descriptor.Name;
        }

        return LegacyThemeMap.TryGetValue(trimmed, out string? mappedTheme)
            ? mappedTheme
            : ThemeNames.Default;
    }

    internal static string NormalizeAccentTintName(string? accentTintName)
    {
        if (string.IsNullOrWhiteSpace(accentTintName))
        {
            return ThemeNames.DefaultAccentTint;
        }

        string trimmed = accentTintName.Trim();
        return AccentTintLookup.TryGetValue(trimmed, out string? canonical)
            ? canonical
            : ThemeNames.DefaultAccentTint;
    }

    internal static void ApplyPaletteBridgeResources(ResourceDictionary resources)
    {
        SolidColorBrush background = ResolveBrush(resources, "#282A36", "BackgroundBrush");
        SolidColorBrush surface = ResolveBrush(resources, "#282A36", "SurfaceBrush", "BackgroundBrush");
        SolidColorBrush card = ResolveBrush(resources, "#44475A", "SurfaceAltBrush", "CurrentLineBrush", "SurfaceBrush");
        SolidColorBrush highlight = ResolveBrush(resources, "#44475A", "SurfaceAltBrush", "SelectionBrush", "CurrentLineBrush");
        SolidColorBrush border = ResolveBrush(resources, "#44475A", "BorderBrush", "SurfaceAltBrush");
        SolidColorBrush textPrimary = ResolveBrush(resources, "#F8F8F2", "TextPrimaryBrush", "ForegroundBrush");
        SolidColorBrush textSecondary = ResolveReadableTextBrush(
            resources,
            ResolveBrush(resources, "#B3BBD6", "TextSecondaryBrush", "CommentBrush"),
            card,
            textPrimary);
        SolidColorBrush textDisabled = CloneBrush(textSecondary, 0.65);
        SolidColorBrush accent = ResolveBrush(resources, "#BD93F9", "AccentBrush", "PurpleBrush");
        SolidColorBrush accentHover = ResolveBrush(resources, "#D5BEFC", "AccentHoverBrush", "AccentBrush", "PurpleBrush");
        SolidColorBrush accentPressed = ResolveBrush(resources, "#A170E6", "AccentPressedBrush", "AccentBrush", "PurpleBrush");
        SolidColorBrush badgeText = ResolveReadableTextBrush(resources, background, accent, textPrimary);
        SolidColorBrush success = ResolveBrush(resources, "#50FA7B", "SuccessBrush", "GreenBrush");
        SolidColorBrush warning = ResolveBrush(resources, "#FFB86C", "WarningBrush", "OrangeBrush");
        SolidColorBrush error = ResolveBrush(resources, "#FF5555", "ErrorBrush", "RedBrush");
        SolidColorBrush info = ResolveBrush(resources, "#8BE9FD", "InfoBrush", "CyanBrush");
        SolidColorBrush overlay = CreateBrush("#B3000000");

        SetBrush(resources, "ApplicationBackgroundBrush", background);
        SetBrush(resources, "SolidBackgroundFillColorBaseBrush", background);
        SetBrush(resources, "SolidBackgroundFillColorSecondaryBrush", surface);
        SetBrush(resources, "LayerFillColorDefaultBrush", surface);
        SetBrush(resources, "LayerFillColorAltBrush", background);
        SetBrush(resources, "CardBackgroundFillColorDefaultBrush", card);
        SetBrush(resources, "CardBackgroundFillColorSecondaryBrush", surface);
        SetBrush(resources, "ControlFillColorDefaultBrush", surface);
        SetBrush(resources, "ControlFillColorSecondaryBrush", highlight);
        SetBrush(resources, "ControlFillColorTertiaryBrush", card);
        SetBrush(resources, "ControlFillColorDisabledBrush", surface);
        SetBrush(resources, "SubtleFillColorSecondaryBrush", highlight);
        SetBrush(resources, "SubtleFillColorTertiaryBrush", highlight);
        SetBrush(resources, "ControlStrokeColorDefaultBrush", border);
        SetBrush(resources, "ControlStrokeColorSecondaryBrush", highlight);
        SetBrush(resources, "ControlElevationBorderBrush", border);
        SetBrush(resources, "DividerStrokeColorDefaultBrush", border);
        SetBrush(resources, "TextFillColorPrimaryBrush", textPrimary);
        SetBrush(resources, "TextFillColorSecondaryBrush", textSecondary);
        SetBrush(resources, "TextFillColorTertiaryBrush", textSecondary);
        SetBrush(resources, "TextFillColorDisabledBrush", textDisabled);
        SetBrush(resources, "TextOnAccentFillColorPrimaryBrush", badgeText);
        SetBrush(resources, "TextOnAccentFillColorSecondaryBrush", badgeText);
        SetBrush(resources, "TextOnAccentFillColorDisabledBrush", textDisabled);
        SetBrush(resources, "SystemAccentColorPrimaryBrush", accent);
        SetBrush(resources, "SystemAccentColorSecondaryBrush", accentHover);
        SetBrush(resources, "SystemAccentColorTertiaryBrush", accentPressed);
        SetBrush(resources, "AccentTextFillColorPrimaryBrush", accent);
        SetBrush(resources, "AccentTextFillColorSecondaryBrush", accentHover);
        SetBrush(resources, "AccentTextFillColorTertiaryBrush", accentPressed);
        SetBrush(resources, "AccentFillColorDefaultBrush", accent);
        SetBrush(resources, "AccentFillColorSecondaryBrush", accentHover);
        SetBrush(resources, "AccentFillColorTertiaryBrush", accentPressed);
        SetBrush(resources, "AccentFillColorDisabledBrush", textDisabled);
        SetBrush(resources, "AccentFillColorSelectedTextBackgroundBrush", accent);
        SetBrush(resources, "AccentButtonBackground", accent);
        SetBrush(resources, "AccentButtonBackgroundPointerOver", accentHover);
        SetBrush(resources, "AccentButtonBackgroundPressed", accentPressed);
        SetBrush(resources, "AccentButtonBackgroundDisabled", textDisabled);
        SetBrush(resources, "AccentButtonForeground", badgeText);
        SetBrush(resources, "AccentButtonForegroundPointerOver", badgeText);
        SetBrush(resources, "AccentButtonForegroundPressed", badgeText);
        SetBrush(resources, "AccentButtonForegroundDisabled", textDisabled);
        SetBrush(resources, "AccentButtonBorderBrush", accent);
        SetBrush(resources, "AccentButtonBorderBrushPointerOver", accentHover);
        SetBrush(resources, "AccentButtonBorderBrushPressed", accentPressed);
        SetBrush(resources, "AccentControlElevationBorderBrush", accent);
        SetBrush(resources, "ButtonBackground", surface);
        SetBrush(resources, "ButtonForeground", textPrimary);
        SetBrush(resources, "ButtonBorderBrush", border);
        SetBrush(resources, "ButtonBackgroundPointerOver", highlight);
        SetBrush(resources, "ButtonBackgroundPressed", highlight);
        SetBrush(resources, "ButtonBackgroundDisabled", surface);
        SetBrush(resources, "ButtonForegroundPointerOver", textPrimary);
        SetBrush(resources, "ButtonForegroundPressed", textPrimary);
        SetBrush(resources, "ButtonForegroundDisabled", textDisabled);
        SetBrush(resources, "ButtonBorderBrushPressed", border);
        SetBrush(resources, "ButtonBorderBrushDisabled", border);
        SetBrush(resources, "CheckBoxBackground", surface);
        SetBrush(resources, "CheckBoxForeground", textPrimary);
        SetBrush(resources, "CheckBoxBorderBrush", border);
        SetBrush(resources, "CheckBoxCheckBorderBrush", border);
        SetBrush(resources, "CheckBoxCheckGlyphForeground", badgeText);
        SetBrush(resources, "CheckBoxCheckBackgroundFillChecked", accent);
        SetBrush(resources, "CheckBoxCheckBackgroundFillCheckedPointerOver", accentHover);
        SetBrush(resources, "CheckBoxCheckBackgroundFillCheckedPressed", accentPressed);
        SetBrush(resources, "CheckBoxCheckBackgroundFillUncheckedPointerOver", highlight);
        SetBrush(resources, "CheckBoxCheckBackgroundFillUncheckedPressed", highlight);
        SetBrush(resources, "CheckBoxCheckBackgroundFillUncheckedDisabled", surface);
        SetBrush(resources, "CheckBoxCheckBackgroundStrokeUncheckedDisabled", border);
        SetBrush(resources, "CheckBoxForegroundUncheckedDisabled", textDisabled);
        SetBrush(resources, "ControlFillColorInputActiveBrush", card);
        SetBrush(resources, "ControlFillColorTransparentBrush", surface);
        SetBrush(resources, "ControlStrongFillColorDefaultBrush", accent);
        SetBrush(resources, "ControlStrongFillColorDisabledBrush", textDisabled);
        SetBrush(resources, "ControlAltFillColorTransparentBrush", background);
        SetBrush(resources, "ControlAltFillColorSecondaryBrush", surface);
        SetBrush(resources, "ControlAltFillColorTertiaryBrush", card);
        SetBrush(resources, "ControlAltFillColorQuarternaryBrush", highlight);
        SetBrush(resources, "ControlAltFillColorDisabledBrush", surface);
        SetBrush(resources, "ControlStrokeColorOnAccentDefaultBrush", badgeText);
        SetBrush(resources, "ControlStrokeColorOnAccentSecondaryBrush", badgeText);
        SetBrush(resources, "ControlStrokeColorOnAccentTertiaryBrush", badgeText);
        SetBrush(resources, "ControlStrokeColorOnAccentDisabledBrush", textDisabled);
        SetBrush(resources, "CardStrokeColorDefaultBrush", border);
        SetBrush(resources, "CardStrokeColorDefaultSolidBrush", border);
        SetBrush(resources, "SystemFillColorSuccessBrush", success);
        SetBrush(resources, "SystemFillColorCautionBrush", warning);
        SetBrush(resources, "SystemFillColorCriticalBrush", error);
        SetBrush(resources, "SystemFillColorNeutralBrush", textDisabled);
        SetBrush(resources, "SystemFillColorSolidNeutralBrush", surface);
        SetBrush(resources, "ThemeAdaptiveAccentBrush", accent);
        SetBrush(resources, "PrimaryHueMidBrush", accent);
        SetBrush(resources, "SecondaryHueMidBrush", info);
        SetBrush(resources, "PrimaryHueLightBrush", accent);
        SetBrush(resources, "SecondaryHueLightBrush", info);
        SetBrush(resources, "StatusPendingBrush", textDisabled);
        SetBrush(resources, "StatusInstallingBrush", info);
        SetBrush(resources, "StatusInstalledBrush", success);
        SetBrush(resources, "StatusFailedBrush", error);
        SetBrush(resources, "StatusSkippedBrush", warning);
        SetBrush(resources, "StatusSuccessBrush", success);
        SetBrush(resources, "ErrorBackgroundBrush", error);
        SetBrush(resources, "ErrorBorderBrush", error);
        SetBrush(resources, "ErrorTextBrush", error);
        SetBrush(resources, "ErrorIconBrush", error);
        SetBrush(resources, "ValidationErrorBorderBrush", error);
        SetBrush(resources, "WarningBackgroundBrush", warning);
        SetBrush(resources, "WarningBorderBrush", warning);
        SetBrush(resources, "WarningTextBrush", warning);
        SetBrush(resources, "WarningIconBrush", warning);
        SetBrush(resources, "SuccessBackgroundBrush", success);
        SetBrush(resources, "SuccessBorderBrush", success);
        SetBrush(resources, "SuccessTextBrush", success);
        SetBrush(resources, "SuccessIconBrush", success);
        SetBrush(resources, "ManualInstallBadgeBrush", warning);
        SetBrush(resources, "RequiredBrush", warning);
        SetBrush(resources, "BadgePrimaryForegroundBrush", badgeText);
        SetBrush(resources, "BadgeSecondaryForegroundBrush", badgeText);
        SetBrush(resources, "PrimaryHueLightForegroundBrush", badgeText);
        SetBrush(resources, "SecondaryHueLightForegroundBrush", badgeText);
        SetBrush(resources, "DialogOverlayBackgroundBrush", overlay);
        SetBrush(resources, "SkeletonBaseBrush", surface);
        SetBrush(resources, "SkeletonHighlightBrush", highlight);

        SetColor(resources, "SystemAccentColor", accent.Color);
        SetColor(resources, "SystemAccentColorPrimary", accent.Color);
        SetColor(resources, "SystemAccentColorSecondary", accentHover.Color);
        SetColor(resources, "SystemAccentColorTertiary", accentPressed.Color);
        SetColor(resources, "AccentFillColorDefault", accent.Color);
        SetColor(resources, "AccentFillColorSecondary", accentHover.Color);
        SetColor(resources, "AccentFillColorTertiary", accentPressed.Color);
        SetColor(resources, "PaletteRedColor", error.Color);
        SetColor(resources, "PaletteGreenColor", success.Color);
        SetColor(resources, "PaletteOrangeColor", warning.Color);
        SetColor(resources, "PaletteLightBlueColor", info.Color);
    }

    internal static void ClearPaletteBridgeResources(ResourceDictionary resources)
    {
        foreach (string key in PaletteBridgeResourceKeys)
        {
            resources.Remove(key);
        }
    }

    internal void ReapplyHighContrastIfEnabled(bool isHighContrastEnabled)
    {
        if (isHighContrastEnabled)
        {
            _applyHighContrastMode();
        }
    }

    private static string BuildDisplayKey(string name)
    {
        return $"{DisplayKeyPrefix}{name}";
    }

    private static string BuildAccentDisplayKey(string name)
    {
        return $"{AccentDisplayKeyPrefix}{name}";
    }

    private ThemeForgeIThemeService GetThemeEngine(Application app)
    {
        return _themeEngine ??= new ThemeForgeThemeService(app, ThemeForgeNames.All);
    }

    private static void ApplyWpfUiTheme(string canonicalTheme)
    {
        ApplicationTheme appTheme = ThemeNames.IsLightTheme(canonicalTheme)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;

        ApplicationThemeManager.Apply(appTheme, WindowBackdropType.None);
    }

    private static SolidColorBrush ResolveBrush(ResourceDictionary resources, string fallbackColor, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (TryFindResource(resources, key) is SolidColorBrush brush)
            {
                return CloneBrush(brush);
            }
        }

        return CreateBrush(fallbackColor);
    }

    private static SolidColorBrush ResolveReadableTextBrush(
        ResourceDictionary resources,
        SolidColorBrush preferred,
        SolidColorBrush background,
        SolidColorBrush fallback)
    {
        if (ContrastRatio(preferred.Color, background.Color) >= 4.5)
        {
            return CloneBrush(preferred);
        }

        SolidColorBrush liftedDraculaComment = CreateBrush("#B3BBD6");
        if (ContrastRatio(liftedDraculaComment.Color, background.Color) >= 4.5)
        {
            return liftedDraculaComment;
        }

        if (ContrastRatio(fallback.Color, background.Color) >= 4.5)
        {
            return CloneBrush(fallback);
        }

        SolidColorBrush black = CreateBrush("#000000");
        SolidColorBrush white = CreateBrush("#FFFFFF");
        return ContrastRatio(black.Color, background.Color) >= ContrastRatio(white.Color, background.Color)
            ? black
            : white;
    }

    private static object? TryFindResource(ResourceDictionary resources, string key)
    {
        if (resources.Contains(key))
        {
            return resources[key];
        }

        for (int index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            object? resource = TryFindResource(resources.MergedDictionaries[index], key);
            if (resource is not null)
            {
                return resource;
            }
        }

        return null;
    }

    private static void SetBrush(ResourceDictionary resources, string key, SolidColorBrush brush)
    {
        resources[key] = CloneBrush(brush);
    }

    private static void SetColor(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = color;
    }

    private static SolidColorBrush CloneBrush(SolidColorBrush brush, double opacity = 1.0)
    {
        return new SolidColorBrush(brush.Color)
        {
            Opacity = opacity
        };
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        double foregroundLuminance = RelativeLuminance(foreground);
        double backgroundLuminance = RelativeLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            double normalized = channel / 255.0;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R)
            + 0.7152 * Channel(color.G)
            + 0.0722 * Channel(color.B);
    }

    private static Color ResolveAccentColor(Application app)
    {
        if (app.TryFindResource("AccentBrush") is SolidColorBrush accentBrush)
        {
            return accentBrush.Color;
        }

        return Color.FromRgb(FallbackAccentRed, FallbackAccentGreen, FallbackAccentBlue);
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

    private void CommitFromEngine(ThemeForgeIThemeService engine, int revisionBefore)
    {
        _currentTheme = string.IsNullOrWhiteSpace(engine.CurrentTheme)
            ? _currentTheme
            : engine.CurrentTheme;
        _currentAccentTint = engine.CurrentAccentTint.ToString();
        _themeRevision = engine.ThemeRevision;

        if (engine.ThemeRevision != revisionBefore)
        {
            Application? app = Application.Current;
            if (app is not null)
            {
                ApplicationAccentColorManager.Apply(ResolveAccentColor(app));
            }

            ThemeChanged?.Invoke(_currentTheme);
        }
    }

    private void CommitWithoutApplication(string canonicalTheme, string canonicalTint)
    {
        if (string.Equals(_currentTheme, canonicalTheme, StringComparison.Ordinal)
            && string.Equals(_currentAccentTint, canonicalTint, StringComparison.Ordinal))
        {
            return;
        }

        _currentTheme = canonicalTheme;
        _currentAccentTint = canonicalTint;
        _themeRevision++;
        ThemeChanged?.Invoke(_currentTheme);
    }

    private void PersistCanonicalThemeIfNeeded(string? requestedThemeName, string canonicalThemeName)
    {
        PersistCanonicalSettingIfNeeded(
            requestedThemeName,
            canonicalThemeName,
            settings => settings.ThemeName,
            (settings, value) => settings.ThemeName = value,
            "theme");
    }

    private void PersistCanonicalAccentTintIfNeeded(string? requestedAccentTintName, string canonicalAccentTintName)
    {
        PersistCanonicalSettingIfNeeded(
            requestedAccentTintName,
            canonicalAccentTintName,
            settings => settings.AccentTintName,
            (settings, value) => settings.AccentTintName = value,
            "accent tint");
    }

    private void PersistCanonicalSettingIfNeeded(
        string? requestedValue,
        string canonicalValue,
        Func<AppSettings, string> getCurrentValue,
        Action<AppSettings, string> setCurrentValue,
        string settingName)
    {
        if (string.Equals(requestedValue?.Trim(), canonicalValue, StringComparison.Ordinal))
        {
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                AppSettings settings = await _settingsService.LoadSettingsAsync();
                if (!string.Equals(getCurrentValue(settings), canonicalValue, StringComparison.Ordinal))
                {
                    setCurrentValue(settings, canonicalValue);
                    await _settingsService.SaveSettingsAsync(settings, default);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to persist canonical {settingName} '{canonicalValue}': {ex.Message}");
            }
        }).SafeFireAndForget();
    }
}
