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
using WinForge.GUI.Resources;
using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for legacy settings theme migration.
/// </summary>
public class AppSettingsServiceMigrationTests
{
    [Fact]
    public void LoadSettings_LegacyDarkThemeTrue_MigratesToDrakul()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: true, ThemeNames.Drakul);
    }

    [Fact]
    public void LoadSettings_LegacyDarkThemeFalse_MigratesToFolio()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: false, ThemeNames.Folio);
    }

    [Fact]
    public void LoadSettings_PascalCaseLegacyKey_StillMigrates()
    {
        VerifyLegacyMigration(legacyIsDarkTheme: true, ThemeNames.Drakul, "IsDarkTheme");
    }

    [Fact]
    public void LoadSettings_AfterMigration_DoesNotReMigrate()
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            """
            {"themeName": "Light", "isDarkTheme": true, "languageCode": "en"}
            """);

        try
        {
            AppSettings settings = new AppSettingsService(settingsPath).LoadSettings();
            AppSettings reloaded = new AppSettingsService(settingsPath).LoadSettings();

            Assert.Equal(ThemeNames.Folio, settings.ThemeName);
            Assert.Equal(ThemeNames.Folio, reloaded.ThemeName);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("themeName", out JsonElement themeName));
            Assert.Equal(ThemeNames.Folio, themeName.GetString());
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
        string settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            AppSettingsService service = new AppSettingsService(settingsPath);
            AppSettings settings = new AppSettings
            {
                ThemeName = ThemeNames.Folio,
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

            AppSettings reloaded = new AppSettingsService(settingsPath).LoadSettings();

            Assert.NotNull(reloaded.MainWindowPlacement);
            Assert.Equal(-1200, reloaded.MainWindowPlacement.Left);
            Assert.Equal(80, reloaded.MainWindowPlacement.Top);
            Assert.Equal(1500, reloaded.MainWindowPlacement.Width);
            Assert.Equal(900, reloaded.MainWindowPlacement.Height);
            Assert.Equal("Maximized", reloaded.MainWindowPlacement.WindowState);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("mainWindowPlacement", out JsonElement placement));
            Assert.True(placement.TryGetProperty("windowState", out JsonElement windowState));
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

    [Theory]
    [InlineData(ThemeNames.Light, ThemeNames.Folio)]
    [InlineData(ThemeNames.Alucard, ThemeNames.Parchment)]
    [InlineData(ThemeNames.DraculaPro, ThemeNames.Drakul)]
    [InlineData(ThemeNames.Blade, ThemeNames.Drakul)]
    [InlineData("UnknownTheme", ThemeNames.Drakul)]
    public void LoadSettings_LegacyThemeName_MigratesToThemeForgeTheme(string legacyTheme, string expectedTheme)
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            $$"""
            {"themeName": "{{legacyTheme}}", "languageCode": "en"}
            """);

        try
        {
            AppSettings settings = new AppSettingsService(settingsPath).LoadSettings();

            Assert.Equal(expectedTheme, settings.ThemeName);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.Equal(expectedTheme, document.RootElement.GetProperty("themeName").GetString());
            Assert.Equal(ThemeNames.DefaultAccentTint, document.RootElement.GetProperty("accentTintName").GetString());
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
    public void LoadSettings_InvalidAccentTint_MigratesToDefaultAccentTint()
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            """
            {"themeName": "Drakul", "accentTintName": "Infrared", "languageCode": "en"}
            """);

        try
        {
            AppSettings settings = new AppSettingsService(settingsPath).LoadSettings();

            Assert.Equal(ThemeNames.Drakul, settings.ThemeName);
            Assert.Equal(ThemeNames.DefaultAccentTint, settings.AccentTintName);
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
        string settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(
            settingsPath,
            $$"""
            {"{{legacyPropertyName}}": {{legacyIsDarkTheme.ToString().ToLowerInvariant()}}, "languageCode": "en"}
            """);

        try
        {
            AppSettingsService service = new AppSettingsService(settingsPath);

            AppSettings settings = service.LoadSettings();

            Assert.Equal(expectedThemeName, settings.ThemeName);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.True(document.RootElement.TryGetProperty("themeName", out JsonElement themeName));
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
