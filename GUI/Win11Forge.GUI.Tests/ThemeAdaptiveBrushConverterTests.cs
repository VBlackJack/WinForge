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
using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Tests.TestInfrastructure;
using Wpf.Ui.Appearance;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for theme-aware brush conversion.
/// </summary>
[Collection("WpfApplication")]
public class ThemeAdaptiveBrushConverterTests
{
    [Fact]
    public void Convert_DraculaActiveAndAccentBrushAvailable_ReturnsAccentBrush()
    {
        RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            Application app = scope.Application;
            SolidColorBrush expectedBrush = new SolidColorBrush(Colors.MediumPurple);
            app.Resources["AccentBrush"] = expectedBrush;

            MockThemeService themeService = new MockThemeService();
            themeService.ApplyTheme(ThemeNames.Drakul);
            ThemeAdaptiveBrushConverter converter = new ThemeAdaptiveBrushConverter();

            object result = converter.Convert(
                themeService,
                typeof(Brush),
                null!,
                CultureInfo.InvariantCulture);

            Assert.Same(expectedBrush, result);
        });
    }

    [Fact]
    public void Convert_ThemeForgeActiveAndAccentBrushMissing_ReturnsFluentFallback()
    {
        RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ApplicationThemeManager.Apply(ApplicationTheme.Light);

            Application app = scope.Application;
            SolidColorBrush expectedBrush = new SolidColorBrush(Colors.MediumSeaGreen);
            app.Resources["PrimaryHueMidBrush"] = expectedBrush;

            MockThemeService themeService = new MockThemeService();
            themeService.ApplyTheme(ThemeNames.Folio);
            ThemeAdaptiveBrushConverter converter = new ThemeAdaptiveBrushConverter();

            object result = converter.Convert(
                themeService,
                typeof(Brush),
                null!,
                CultureInfo.InvariantCulture);

            Assert.Same(expectedBrush, result);
        });
    }

    [Fact]
    public void Convert_ServiceUnavailable_FallsThroughToLegacyPath()
    {
        RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            Application app = scope.Application;
            SolidColorBrush expectedBrush = new SolidColorBrush(Colors.Gold);
            app.Resources["SecondaryHueMidBrush"] = expectedBrush;

            ThemeAdaptiveBrushConverter converter = new ThemeAdaptiveBrushConverter();

            object result = converter.Convert(
                null!,
                typeof(Brush),
                null!,
                CultureInfo.InvariantCulture);

            Assert.False(App.IsServicesInitialized);
            Assert.Same(expectedBrush, result);
        });
    }

    private static void RunOnStaThread(Action action) => WpfApplicationScope.RunOnStaThread(action);
}
