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
using System.Text.Json;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for legacy settings theme migration.
/// </summary>
public class AppSettingsServiceMigrationTests
{
    [Fact]
    public void LoadSettings_LegacyDarkThemeTrue_MigratesToDraculaPro()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: true, ThemeNames.DraculaPro);
    }

    [Fact]
    public void LoadSettings_LegacyDarkThemeFalse_MigratesToLight()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: false, ThemeNames.Light);
    }

    [Fact]
    public void LoadSettings_PascalCaseLegacyKey_StillMigrates()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: true, ThemeNames.DraculaPro, "IsDarkTheme");
    }

    [Fact]
    public void LoadSettings_AfterMigration_DoesNotReMigrate()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            """
            {"themeName": "Light", "isDarkTheme": true, "languageCode": "en"}
            """);

        try
        {
            var settings = new AppSettingsService(settingsPath).LoadSettings();
            var reloaded = new AppSettingsService(settingsPath).LoadSettings();

            Assert.Equal(ThemeNames.Light, settings.ThemeName);
            Assert.Equal(ThemeNames.Light, reloaded.ThemeName);

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("themeName", out var themeName));
            Assert.Equal(ThemeNames.Light, themeName.GetString());
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    [Fact]
    public void SaveAndLoadSettings_WindowPlacement_RoundTrips()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            var service = new AppSettingsService(settingsPath);
            var settings = new AppSettings
            {
                ThemeName = ThemeNames.Light,
                MainWindowPlacement = new WindowPlacementSettings
                {
                    Left = -1200,
                    Top = 80,
                    Width = 1500,
                    Height = 900,
                    WindowState = "Maximized"
                }
            };

            Assert.True(service.SaveSettings(settings));

            var reloaded = new AppSettingsService(settingsPath).LoadSettings();

            Assert.NotNull(reloaded.MainWindowPlacement);
            Assert.Equal(-1200, reloaded.MainWindowPlacement.Left);
            Assert.Equal(80, reloaded.MainWindowPlacement.Top);
            Assert.Equal(1500, reloaded.MainWindowPlacement.Width);
            Assert.Equal(900, reloaded.MainWindowPlacement.Height);
            Assert.Equal("Maximized", reloaded.MainWindowPlacement.WindowState);

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("mainWindowPlacement", out var placement));
            Assert.True(placement.TryGetProperty("windowState", out var windowState));
            Assert.Equal("Maximized", windowState.GetString());
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    private static void VerifyLegacyMigration(
        bool legacyIsDarkTheme,
        string expectedThemeName,
        string legacyPropertyName = "isDarkTheme")
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            $$"""
            {"{{legacyPropertyName}}": {{legacyIsDarkTheme.ToString().ToLowerInvariant()}}, "languageCode": "en"}
            """);

        try
        {
            var service = new AppSettingsService(settingsPath);

            var settings = service.LoadSettings();

            Assert.Equal(expectedThemeName, settings.ThemeName);
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("themeName", out var themeName));
            Assert.Equal(expectedThemeName, themeName.GetString());
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }
}
