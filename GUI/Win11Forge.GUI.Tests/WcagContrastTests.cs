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

using System.IO;
using System.Xml.Linq;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Guards WCAG AA contrast for theme tokens that back body-sized UI text.
/// </summary>
public class WcagContrastTests
{
    private const double AaBodyText = 4.5;

    private static readonly string[] DraculaThemes =
    [
        "AlucardTheme.xaml",
        "BladeTheme.xaml",
        "BuffyTheme.xaml",
        "DraculaProTheme.xaml",
        "LincolnTheme.xaml",
        "MorbiusTheme.xaml",
        "VanHelsingTheme.xaml"
    ];

    [Theory]
    [MemberData(nameof(DraculaThemeNames))]
    public void DraculaTheme_TextPrimaryOnCard_MeetsAA(string themeFile)
    {
        var colors = ParseThemeColors(themeFile);

        AssertContrastMeetsAA(
            colors["TextPrimaryColor"],
            colors["SurfaceColor"],
            $"{themeFile}: TextPrimaryColor on Card");
    }

    [Theory]
    [MemberData(nameof(DraculaThemeNames))]
    public void DraculaTheme_TextSecondaryOnCard_MeetsAA(string themeFile)
    {
        var colors = ParseThemeColors(themeFile);

        AssertContrastMeetsAA(
            colors["TextSecondaryColor"],
            colors["SurfaceColor"],
            $"{themeFile}: TextSecondaryColor on Card");
    }

    [Theory]
    [MemberData(nameof(DraculaThemeNames))]
    public void DraculaTheme_TextTertiaryOnCard_MeetsAA(string themeFile)
    {
        var colors = ParseThemeColors(themeFile);

        AssertContrastMeetsAA(
            colors["TextTertiaryBrush"],
            colors["SurfaceColor"],
            $"{themeFile}: TextTertiaryBrush on Card");
    }

    [Theory]
    [InlineData("WarningTextLightBrush")]
    [InlineData("AccentOrangeTextLightBrush")]
    [InlineData("StatusSkippedLightBrush")]
    public void LightTheme_OrangeBrushesMeetAAWithWhiteForeground(string brushKey)
    {
        var colors = ParseFluentThemeBridgeBrushColors();

        AssertContrastMeetsAA("#FFFFFF", colors[brushKey], $"{brushKey} with white foreground");
    }

    [Fact]
    public void HighContrast_AppCatalogHeaderTextOnMappedSurface_MeetsAA()
    {
        var colors = ParseHighContrastBrushColors();
        var appSource = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml.cs"));

        Assert.Contains(
            "SwapIfExists(app, \"SolidBackgroundFillColorSecondaryBrush\", \"HighContrastSurfaceBrush\")",
            appSource,
            StringComparison.Ordinal);
        AssertContrastMeetsAA(
            colors["HighContrastTextPrimaryBrush"],
            colors["HighContrastSurfaceBrush"],
            "High contrast AppCatalog header primary text");
        AssertContrastMeetsAA(
            colors["HighContrastTextSecondaryBrush"],
            colors["HighContrastSurfaceBrush"],
            "High contrast AppCatalog header secondary text");
    }

    public static IEnumerable<object[]> DraculaThemeNames =>
        DraculaThemes.Select(theme => new object[] { theme });

    private static Dictionary<string, string> ParseThemeColors(string themeFile)
    {
        var doc = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Themes", "Dracula", themeFile));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var element in doc.Descendants())
        {
            if (element.Name.LocalName == "Color")
            {
                var key = element.Attribute(x + "Key")?.Value;
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(element.Value))
                {
                    colors[key] = NormalizeHexColor(element.Value);
                }
            }
            else if (element.Name.LocalName == "SolidColorBrush"
                     && string.Equals(element.Attribute(x + "Key")?.Value, "TextTertiaryBrush", StringComparison.Ordinal))
            {
                var color = element.Attribute("Color")?.Value;
                if (!string.IsNullOrWhiteSpace(color))
                {
                    colors["TextTertiaryBrush"] = NormalizeHexColor(color);
                }
            }
        }

        return colors;
    }

    private static Dictionary<string, string> ParseFluentThemeBridgeBrushColors()
    {
        var doc = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "FluentThemeBridge.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var element in doc.Descendants().Where(element => element.Name.LocalName == "SolidColorBrush"))
        {
            var key = element.Attribute(x + "Key")?.Value;
            var color = element.Attribute("Color")?.Value;

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(color) && color.StartsWith("#", StringComparison.Ordinal))
            {
                colors[key] = NormalizeHexColor(color);
            }
        }

        return colors;
    }

    private static Dictionary<string, string> ParseHighContrastBrushColors()
    {
        var doc = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "HighContrastTheme.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var element in doc.Descendants().Where(element => element.Name.LocalName == "SolidColorBrush"))
        {
            var key = element.Attribute(x + "Key")?.Value;
            var color = element.Attribute("Color")?.Value;

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(color))
            {
                colors[key] = NormalizeHexColor(color);
            }
        }

        return colors;
    }

    private static void AssertContrastMeetsAA(string foreground, string background, string context)
    {
        var ratio = ContrastRatio(foreground, background);

        Assert.True(
            ratio >= AaBodyText,
            $"{context}: {foreground} on {background} = {ratio:F2}:1, needs >= {AaBodyText}:1");
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(HexToRgb(foreground));
        var backgroundLuminance = RelativeLuminance(HexToRgb(background));
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (double R, double G, double B) HexToRgb(string hex)
    {
        hex = NormalizeHexColor(hex).TrimStart('#');
        if (hex.Length != 6)
        {
            throw new ArgumentException($"Expected 6-digit RGB hex color, got {hex}.");
        }

        return (
            Convert.ToInt32(hex[..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16));
    }

    private static double RelativeLuminance((double R, double G, double B) rgb)
    {
        static double Channel(double channel)
        {
            channel /= 255.0;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(rgb.R) + 0.7152 * Channel(rgb.G) + 0.0722 * Channel(rgb.B);
    }

    private static string NormalizeHexColor(string color) => color.Trim().ToUpperInvariant();

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(relativeParts)} from {AppContext.BaseDirectory}.");
    }
}
