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
using System.Windows.Media;
using System.Xml.Linq;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Tests.TestInfrastructure;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Guards WCAG AA contrast for theme tokens that back body-sized UI text.
/// </summary>
[Collection("WpfApplication")]
public class WcagContrastTests
{
    private const double AaBodyText = 4.5;

    [Fact]
    public void ThemeForgeBridge_TextOnCard_MeetsAAForEveryTheme()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using var scope = WpfApplicationScope.Create();
            var service = new ThemeService(new MockAppSettingsService());

            foreach (var themeName in ThemeForge.Theme.ThemeNames.All)
            {
                service.ApplyTheme(themeName);

                var card = ReadBrushColor("CardBackgroundFillColorDefaultBrush");
                AssertContrastMeetsAA(ReadBrushColor("TextFillColorPrimaryBrush"), card, $"{themeName}: primary text on card");
                AssertContrastMeetsAA(ReadBrushColor("TextFillColorSecondaryBrush"), card, $"{themeName}: secondary text on card");
                AssertContrastMeetsAA(ReadBrushColor("TextFillColorTertiaryBrush"), card, $"{themeName}: tertiary text on card");
            }
        });
    }

    [Fact]
    public void ThemeForgeBridge_TextOnAccent_MeetsAAForEveryThemeAndAccentTint()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using var scope = WpfApplicationScope.Create();
            var service = new ThemeService(new MockAppSettingsService());

            foreach (var themeName in ThemeForge.Theme.ThemeNames.All)
            {
                service.ApplyTheme(themeName);

                foreach (var accentTint in ThemeForge.Theme.AccentTints.All)
                {
                    service.ApplyAccentTint(accentTint.ToString());

                    AssertContrastMeetsAA(
                        ReadBrushColor("TextOnAccentFillColorPrimaryBrush"),
                        ReadBrushColor("AccentButtonBackground"),
                        $"{themeName}/{accentTint}: text on accent");
                }
            }
        });
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

    private static Color ReadBrushColor(string key)
    {
        var brush = Assert.IsType<SolidColorBrush>(System.Windows.Application.Current.Resources[key]);
        return brush.Color;
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

    private static void AssertContrastMeetsAA(Color foreground, Color background, string context)
    {
        var ratio = ContrastRatio(foreground, background);

        Assert.True(
            ratio >= AaBodyText,
            $"{context}: {foreground} on {background} = {ratio:F2}:1, needs >= {AaBodyText}:1");
    }

    private static void AssertContrastMeetsAA(string foreground, string background, string context)
    {
        AssertContrastMeetsAA(HexToColor(foreground), HexToColor(background), context);
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static Color HexToColor(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(NormalizeHexColor(hex));
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            var normalized = channel / 255.0;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R)
            + 0.7152 * Channel(color.G)
            + 0.0722 * Channel(color.B);
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
