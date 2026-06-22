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

namespace WinForge.GUI.Resources;

/// <summary>
/// Canonical theme name constants.
/// </summary>
public static class ThemeNames
{
    public const string Dracula = ThemeForge.Theme.ThemeNames.Dracula;
    public const string Drakul = ThemeForge.Theme.ThemeNames.Drakul;
    public const string Striga = ThemeForge.Theme.ThemeNames.Striga;
    public const string Cinder = ThemeForge.Theme.ThemeNames.Cinder;
    public const string Bracken = ThemeForge.Theme.ThemeNames.Bracken;
    public const string Tarn = ThemeForge.Theme.ThemeNames.Tarn;
    public const string Mortis = ThemeForge.Theme.ThemeNames.Mortis;
    public const string Slate = ThemeForge.Theme.ThemeNames.Slate;
    public const string Voivode = ThemeForge.Theme.ThemeNames.Voivode;
    public const string Carmilla = ThemeForge.Theme.ThemeNames.Carmilla;
    public const string Whitby = ThemeForge.Theme.ThemeNames.Whitby;
    public const string Vesper = ThemeForge.Theme.ThemeNames.Vesper;
    public const string Parchment = ThemeForge.Theme.ThemeNames.Parchment;
    public const string Folio = ThemeForge.Theme.ThemeNames.Folio;
    public const string Wormwood = ThemeForge.Theme.ThemeNames.Wormwood;
    public const string Sconce = ThemeForge.Theme.ThemeNames.Sconce;

    /// <summary>
    /// Legacy WinForge theme names kept only for settings migration.
    /// </summary>
    public const string Light = "Light";
    public const string DraculaPro = "DraculaPro";
    public const string Alucard = "Alucard";
    public const string Blade = "Blade";
    public const string Buffy = "Buffy";
    public const string Lincoln = "Lincoln";
    public const string Morbius = "Morbius";
    public const string VanHelsing = "VanHelsing";

    /// <summary>
    /// Fallback theme for fresh installs and unknown values.
    /// </summary>
    public const string Default = Drakul;

    /// <summary>
    /// Fallback accent tint for fresh installs and unknown values.
    /// </summary>
    public const string DefaultAccentTint = nameof(ThemeForge.Theme.AccentTint.Default);

    /// <summary>
    /// Returns whether a canonical or legacy theme should use WPF light chrome.
    /// </summary>
    public static bool IsLightTheme(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return false;
        }

        if (string.Equals(themeName, Light, StringComparison.Ordinal)
            || string.Equals(themeName, Alucard, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(
            ThemeForge.Theme.ThemeNames.GetFamily(themeName),
            "Light",
            StringComparison.Ordinal);
    }
}
