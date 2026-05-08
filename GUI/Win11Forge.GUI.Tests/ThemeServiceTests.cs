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
using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for the theme service catalogue and revision contract.
/// </summary>
[Collection("WpfApplication")]
public class ThemeServiceTests
{
    [Fact]
    public void ApplyTheme_KnownTheme_SetsCurrentTheme()
    {
        var service = CreateService();

        service.ApplyTheme(ThemeNames.DraculaPro);

        Assert.Equal(ThemeNames.DraculaPro, service.CurrentTheme);
    }

    [Fact]
    public void ApplyTheme_SameThemeTwice_RevisionIncrementsOnceOnly()
    {
        var service = CreateService();

        service.ApplyTheme(ThemeNames.DraculaPro);
        var revisionAfterFirstApply = service.ThemeRevision;
        service.ApplyTheme(ThemeNames.DraculaPro);

        Assert.Equal(1, revisionAfterFirstApply);
        Assert.Equal(revisionAfterFirstApply, service.ThemeRevision);
    }

    [Fact]
    public void ApplyTheme_DifferentThemes_RevisionMonotonicallyIncreases()
    {
        var service = CreateService();

        service.ApplyTheme(ThemeNames.Light);
        var lightRevision = service.ThemeRevision;
        service.ApplyTheme(ThemeNames.DraculaPro);
        var draculaRevision = service.ThemeRevision;
        service.ApplyTheme(ThemeNames.Blade);
        var bladeRevision = service.ThemeRevision;

        Assert.True(lightRevision < draculaRevision);
        Assert.True(draculaRevision < bladeRevision);
    }

    [Fact]
    public void ApplyTheme_UnknownTheme_FallsBackToDefault()
    {
        var service = CreateService();

        service.ApplyTheme("NonExistent");

        Assert.Equal(ThemeNames.Default, service.CurrentTheme);
    }

    [Fact]
    public void ApplyTheme_AnyTheme_RaisesThemeChangedExactlyOnce()
    {
        var service = CreateService();
        var eventCount = 0;
        string? lastTheme = null;
        service.ThemeChanged += themeName =>
        {
            eventCount++;
            lastTheme = themeName;
        };

        service.ApplyTheme(ThemeNames.DraculaPro);

        Assert.Equal(1, eventCount);
        Assert.Equal(ThemeNames.DraculaPro, lastTheme);
    }

    [Fact]
    public void ApplyPaletteBridgeResources_DraculaPalette_MapsToFluentResourceKeys()
    {
        var resources = new ResourceDictionary
        {
            ["BackgroundBrush"] = Brush("#22232E"),
            ["SurfaceBrush"] = Brush("#1B1C25"),
            ["CardBrush"] = Brush("#3B3D51"),
            ["AccentBrush"] = Brush("#C4A5FF"),
            ["AccentHoverBrush"] = Brush("#D8BFFF"),
            ["AccentPressedBrush"] = Brush("#A88BE0"),
            ["TextPrimaryBrush"] = Brush("#F7F7F3"),
            ["TextSecondaryBrush"] = Brush("#9AA5C8"),
            ["TextTertiaryBrush"] = Brush("#8890B0"),
            ["TextDisabledBrush"] = Brush("#7E88B6"),
            ["BorderBrush"] = Brush("#7E88B6"),
            ["HighlightBrush"] = Brush("#4A4E66"),
            ["SuccessBrush"] = Brush("#6CF7A1"),
            ["WarningBrush"] = Brush("#FFBE75"),
            ["ErrorBrush"] = Brush("#FF6C7A"),
            ["InfoBrush"] = Brush("#8DEBFF"),
            ["ErrorTextBrush"] = Brush("#FF6C7A"),
            ["WarningTextBrush"] = Brush("#FFBE75"),
            ["SuccessTextBrush"] = Brush("#6CF7A1"),
            ["BadgeTextBrush"] = Brush("#14151C"),
            ["TextOnAccentBrush"] = Brush("#FFFFFF"),
            ["OverlayBackground"] = Brush("#B3000000"),
            ["AccentColor"] = Color("#C4A5FF"),
            ["AccentHoverColor"] = Color("#D8BFFF"),
            ["AccentPressedColor"] = Color("#A88BE0")
        };

        ThemeService.ApplyPaletteBridgeResources(resources);

        AssertBrush("#22232E", resources["ApplicationBackgroundBrush"]);
        AssertBrush("#3B3D51", resources["CardBackgroundFillColorDefaultBrush"]);
        AssertBrush("#F7F7F3", resources["TextFillColorPrimaryBrush"]);
        AssertBrush("#9AA5C8", resources["TextFillColorSecondaryBrush"]);
        AssertBrush("#C4A5FF", resources["SystemAccentColorPrimaryBrush"]);
        AssertBrush("#14151C", resources["TextOnAccentFillColorPrimaryBrush"]);
        AssertBrush("#C4A5FF", resources["PrimaryHueLightBrush"]);
        AssertBrush("#14151C", resources["BadgePrimaryForegroundBrush"]);
        Assert.Equal(Color("#C4A5FF"), resources["SystemAccentColor"]);
    }

    [Fact]
    public void ClearPaletteBridgeResources_RemovesOnlyBridgeTargets()
    {
        var resources = new ResourceDictionary
        {
            ["BackgroundBrush"] = Brush("#22232E"),
            ["ApplicationBackgroundBrush"] = Brush("#22232E"),
            ["TextFillColorPrimaryBrush"] = Brush("#F7F7F3"),
            ["SystemAccentColor"] = Color("#C4A5FF")
        };

        ThemeService.ClearPaletteBridgeResources(resources);

        Assert.True(resources.Contains("BackgroundBrush"));
        Assert.False(resources.Contains("ApplicationBackgroundBrush"));
        Assert.False(resources.Contains("TextFillColorPrimaryBrush"));
        Assert.False(resources.Contains("SystemAccentColor"));
    }

    [Fact]
    public void ReapplyHighContrastIfEnabled_WhenEnabled_InvokesHighContrastAction()
    {
        var applyCount = 0;
        var service = CreateService(() => applyCount++);

        service.ReapplyHighContrastIfEnabled(isHighContrastEnabled: true);

        Assert.Equal(1, applyCount);
    }

    [Fact]
    public void ReapplyHighContrastIfEnabled_WhenDisabled_DoesNotInvokeHighContrastAction()
    {
        var applyCount = 0;
        var service = CreateService(() => applyCount++);

        service.ReapplyHighContrastIfEnabled(isHighContrastEnabled: false);

        Assert.Equal(0, applyCount);
    }

    [Fact]
    public void ApplyTheme_SourceReappliesHighContrastOnSameThemeEarlyReturn()
    {
        var source = ReadThemeServiceSource();
        var expectedDictionaryCheck = source.IndexOf("&& HasExpectedResourceDictionary(app, descriptor)", StringComparison.Ordinal);
        var branchStart = source.LastIndexOf("if (_hasAppliedTheme", expectedDictionaryCheck, StringComparison.Ordinal);
        var branchEnd = source.IndexOf("try", expectedDictionaryCheck, StringComparison.Ordinal);
        var branch = source[branchStart..branchEnd];

        Assert.Contains("ReapplyHighContrastIfEnabled(app);", branch, StringComparison.Ordinal);
        Assert.True(
            branch.IndexOf("ReapplyHighContrastIfEnabled(app);", StringComparison.Ordinal)
            < branch.IndexOf("return;", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyTheme_SourceReappliesHighContrastAfterPaletteResourcesBeforeCommit()
    {
        var source = ReadThemeServiceSource();
        var paletteIndex = source.IndexOf("ApplyPaletteResources(app, descriptor);", StringComparison.Ordinal);
        var reapplyIndex = source.IndexOf("ReapplyHighContrastIfEnabled(app);", paletteIndex, StringComparison.Ordinal);
        var commitIndex = source.IndexOf("CommitTheme(descriptor.Name);", paletteIndex, StringComparison.Ordinal);

        Assert.True(paletteIndex >= 0);
        Assert.True(reapplyIndex > paletteIndex);
        Assert.True(commitIndex > reapplyIndex);
    }

    [Fact]
    public void ReapplyHighContrastIfEnabled_SourceUsesHighContrastDictionaryMarker()
    {
        var source = ReadThemeServiceSource();

        Assert.Contains("private const string HighContrastResourceMarker = \"HighContrastTheme\";", source, StringComparison.Ordinal);
        Assert.Contains("ReapplyHighContrastIfEnabled(HasHighContrastResourceDictionary(app));", source, StringComparison.Ordinal);
        Assert.Contains("HighContrastResourceMarker", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDraculaResourceDictionaries_SourceDoesNotRemoveHighContrastDictionary()
    {
        var source = ReadThemeServiceSource();
        var methodStart = source.IndexOf("private static bool IsDraculaResourceDictionary", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private static void RemoveDraculaResourceDictionaries", methodStart, StringComparison.Ordinal);
        var draculaPredicate = source[methodStart..methodEnd];

        Assert.Contains("DraculaResourceMarker", draculaPredicate, StringComparison.Ordinal);
        Assert.DoesNotContain("HighContrastResourceMarker", draculaPredicate, StringComparison.Ordinal);
    }

    private static ThemeService CreateService()
    {
        return CreateService(() => { });
    }

    private static ThemeService CreateService(Action applyHighContrastMode)
    {
        return new ThemeService(new MockAppSettingsService(), applyHighContrastMode);
    }

    private static string ReadThemeServiceSource()
    {
        return File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Services", "ThemeService.cs"));
    }

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

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush(Color(color));
    }

    private static Color Color(string color)
    {
        return (Color)ColorConverter.ConvertFromString(color);
    }

    private static void AssertBrush(string expectedColor, object? actual)
    {
        var brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color(expectedColor), brush.Color);
    }

}
