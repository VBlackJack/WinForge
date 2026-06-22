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

using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Tests.TestInfrastructure;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for the ThemeForge-backed theme service catalogue and revision contract.
/// </summary>
[Collection("WpfApplication")]
public class ThemeServiceTests
{
    [Fact]
    public void AvailableThemes_UsesThemeForgeCatalogue()
    {
        ThemeService service = CreateService();

        Assert.Equal(ThemeForge.Theme.ThemeNames.All.Count, service.AvailableThemes.Count);
        Assert.Equal(ThemeNames.Drakul, service.AvailableThemes[1].Name);
        Assert.DoesNotContain(service.AvailableThemes, theme => theme.Name == ThemeNames.Light);
    }

    [Fact]
    public void AvailableAccentTints_UsesThemeForgeCatalogue()
    {
        ThemeService service = CreateService();

        Assert.Equal(ThemeForge.Theme.AccentTints.All.Count, service.AvailableAccentTints.Count);
        Assert.Contains(service.AvailableAccentTints, tint => tint.Name == "Default");
        Assert.Contains(service.AvailableAccentTints, tint => tint.Name == "Purple");
    }

    [Fact]
    public void ApplyTheme_KnownTheme_SetsCurrentTheme()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ThemeService service = CreateService();

            service.ApplyTheme(ThemeNames.Drakul);

            Assert.Equal(ThemeNames.Drakul, service.CurrentTheme);
        });
    }

    [Fact]
    public void ApplyTheme_SameThemeTwice_RevisionIncrementsOnceOnly()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ThemeService service = CreateService();

            service.ApplyTheme(ThemeNames.Drakul);
            int revisionAfterFirstApply = service.ThemeRevision;
            service.ApplyTheme(ThemeNames.Drakul);

            Assert.Equal(1, revisionAfterFirstApply);
            Assert.Equal(revisionAfterFirstApply, service.ThemeRevision);
        });
    }

    [Fact]
    public void ApplyTheme_DifferentThemes_RevisionMonotonicallyIncreases()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ThemeService service = CreateService();

            service.ApplyTheme(ThemeNames.Drakul);
            int drakulRevision = service.ThemeRevision;
            service.ApplyTheme(ThemeNames.Folio);
            int folioRevision = service.ThemeRevision;
            service.ApplyTheme(ThemeNames.Sconce);
            int sconceRevision = service.ThemeRevision;

            Assert.True(drakulRevision < folioRevision);
            Assert.True(folioRevision < sconceRevision);
        });
    }

    [Fact]
    public void ApplyTheme_UnknownTheme_FallsBackToDefault()
    {
        ThemeService service = CreateService();

        service.ApplyTheme("NonExistent");

        Assert.Equal(ThemeNames.Default, service.CurrentTheme);
    }

    [Fact]
    public void ApplyTheme_LegacyTheme_NormalizesToThemeForgeTheme()
    {
        ThemeService service = CreateService();

        service.ApplyTheme(ThemeNames.DraculaPro);

        Assert.Equal(ThemeNames.Drakul, service.CurrentTheme);
    }

    [Fact]
    public void ApplyTheme_AnyTheme_RaisesThemeChangedExactlyOnce()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ThemeService service = CreateService();
            int eventCount = 0;
            string? lastTheme = null;
            service.ThemeChanged += themeName =>
            {
                eventCount++;
                lastTheme = themeName;
            };

            service.ApplyTheme(ThemeNames.Drakul);

            Assert.Equal(1, eventCount);
            Assert.Equal(ThemeNames.Drakul, lastTheme);
        });
    }

    [Fact]
    public void ApplyAccentTint_AfterTheme_UpdatesCurrentTintAndBridgeAccent()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ThemeService service = CreateService();

            service.ApplyTheme(ThemeNames.Drakul);
            service.ApplyAccentTint("Green");

            Assert.Equal("Green", service.CurrentAccentTint);
            Assert.Equal(2, service.ThemeRevision);
            AssertBrush("#50FA7B", scope.Application.Resources["ThemeAdaptiveAccentBrush"]);
            Assert.Equal(Color("#50FA7B"), scope.Application.Resources["SystemAccentColor"]);
        });
    }

    [Fact]
    public void ApplyPaletteBridgeResources_ThemeForgePalette_MapsToFluentResourceKeys()
    {
        ResourceDictionary resources = new ResourceDictionary
        {
            ["BackgroundBrush"] = Brush("#282A36"),
            ["SurfaceBrush"] = Brush("#282A36"),
            ["SurfaceAltBrush"] = Brush("#44475A"),
            ["AccentBrush"] = Brush("#BD93F9"),
            ["AccentHoverBrush"] = Brush("#D5BEFC"),
            ["AccentPressedBrush"] = Brush("#A170E6"),
            ["TextPrimaryBrush"] = Brush("#F8F8F2"),
            ["TextSecondaryBrush"] = Brush("#6272A4"),
            ["BorderBrush"] = Brush("#44475A"),
            ["SuccessBrush"] = Brush("#50FA7B"),
            ["WarningBrush"] = Brush("#FFB86C"),
            ["ErrorBrush"] = Brush("#FF5555"),
            ["InfoBrush"] = Brush("#8BE9FD")
        };

        ThemeService.ApplyPaletteBridgeResources(resources);

        AssertBrush("#282A36", resources["ApplicationBackgroundBrush"]);
        AssertBrush("#44475A", resources["CardBackgroundFillColorDefaultBrush"]);
        AssertBrush("#F8F8F2", resources["TextFillColorPrimaryBrush"]);
        AssertBrush("#B3BBD6", resources["TextFillColorSecondaryBrush"]);
        AssertBrush("#BD93F9", resources["SystemAccentColorPrimaryBrush"]);
        AssertBrush("#40BD93F9", resources["DataGridSelectedRowBackgroundBrush"]);
        AssertBrush("#282A36", resources["TextOnAccentFillColorPrimaryBrush"]);
        AssertBrush("#BD93F9", resources["PrimaryHueLightBrush"]);
        AssertBrush("#282A36", resources["BadgePrimaryForegroundBrush"]);
        Assert.Equal(Color("#BD93F9"), resources["SystemAccentColor"]);
    }

    [Fact]
    public void ClearPaletteBridgeResources_RemovesOnlyBridgeTargets()
    {
        ResourceDictionary resources = new ResourceDictionary
        {
            ["BackgroundBrush"] = Brush("#282A36"),
            ["ApplicationBackgroundBrush"] = Brush("#282A36"),
            ["TextFillColorPrimaryBrush"] = Brush("#F8F8F2"),
            ["SystemAccentColor"] = Color("#BD93F9")
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
        int applyCount = 0;
        ThemeService service = CreateService(() => applyCount++);

        service.ReapplyHighContrastIfEnabled(isHighContrastEnabled: true);

        Assert.Equal(1, applyCount);
    }

    [Fact]
    public void ReapplyHighContrastIfEnabled_WhenDisabled_DoesNotInvokeHighContrastAction()
    {
        int applyCount = 0;
        ThemeService service = CreateService(() => applyCount++);

        service.ReapplyHighContrastIfEnabled(isHighContrastEnabled: false);

        Assert.Equal(0, applyCount);
    }

    [Fact]
    public void ApplyTheme_WithHighContrastDictionary_ReappliesHighContrastOnThemeApply()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            scope.AddHighContrastDictionaryMarker();
            int applyCount = 0;
            ThemeService service = CreateService(() => applyCount++);

            service.ApplyTheme(ThemeNames.Drakul);

            Assert.Equal(ThemeNames.Drakul, service.CurrentTheme);
            Assert.Equal(1, service.ThemeRevision);
            Assert.Equal(1, applyCount);
        });
    }

    [Fact]
    public void ApplyTheme_SameThemeWithHighContrastDictionary_ReappliesHighContrastOnEarlyReturn()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            int applyCount = 0;
            ThemeService service = CreateService(() => applyCount++);

            service.ApplyTheme(ThemeNames.Drakul);
            int revisionAfterInitialApply = service.ThemeRevision;
            scope.AddHighContrastDictionaryMarker();
            service.ApplyTheme(ThemeNames.Drakul);

            Assert.Equal(ThemeNames.Drakul, service.CurrentTheme);
            Assert.Equal(revisionAfterInitialApply, service.ThemeRevision);
            Assert.Equal(1, applyCount);
        });
    }

    private static ThemeService CreateService()
    {
        return CreateService(() => { });
    }

    private static ThemeService CreateService(Action applyHighContrastMode)
    {
        return new ThemeService(new MockAppSettingsService(), applyHighContrastMode);
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
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color(expectedColor), brush.Color);
    }
}
