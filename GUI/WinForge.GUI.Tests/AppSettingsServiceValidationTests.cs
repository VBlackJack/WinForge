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
using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

/// <summary>
/// The AppSettings constraints used to be declarative only - nothing executed them, so
/// any value that survived JSON deserialization was accepted verbatim.
/// </summary>
public class AppSettingsServiceValidationTests : IDisposable
{
    private readonly string _settingsPath;

    public AppSettingsServiceValidationTests()
    {
        _settingsPath = Path.Combine(
            Path.GetTempPath(), $"winforge-settings-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void LoadSettings_ResetsOutOfRangeValuesToDefaults()
    {
        File.WriteAllText(_settingsPath, """
        {
          "maxParallelInstalls": 5000,
          "maxParallelScans": -3,
          "updateScanTimeoutMinutes": 999
        }
        """);

        AppSettings settings = new AppSettingsService(_settingsPath).LoadSettings();
        AppSettings defaults = new AppSettings();

        Assert.Equal(defaults.MaxParallelInstalls, settings.MaxParallelInstalls);
        Assert.Equal(defaults.MaxParallelScans, settings.MaxParallelScans);
        Assert.Equal(defaults.UpdateScanTimeoutMinutes, settings.UpdateScanTimeoutMinutes);
    }

    [Fact]
    public void LoadSettings_ResetsMalformedLanguageCode()
    {
        File.WriteAllText(_settingsPath, """
        {
          "languageCode": "not-a-language-code"
        }
        """);

        AppSettings settings = new AppSettingsService(_settingsPath).LoadSettings();

        Assert.Equal(new AppSettings().LanguageCode, settings.LanguageCode);
    }

    [Fact]
    public void LoadSettings_KeepsValidValuesUntouched()
    {
        File.WriteAllText(_settingsPath, """
        {
          "languageCode": "fr",
          "maxParallelInstalls": 3,
          "maxParallelScans": 8
        }
        """);

        AppSettings settings = new AppSettingsService(_settingsPath).LoadSettings();

        Assert.Equal("fr", settings.LanguageCode);
        Assert.Equal(3, settings.MaxParallelInstalls);
        Assert.Equal(8, settings.MaxParallelScans);
    }

    [Fact]
    public void LoadSettings_DoesNotResetOnTheCrossFieldPerformanceHint()
    {
        // AppSettings.Validate flags MaxParallelInstalls > MaxParallelScans as a
        // performance hint, not an error. A hint must not discard the user's choice.
        File.WriteAllText(_settingsPath, """
        {
          "maxParallelInstalls": 9,
          "maxParallelScans": 2
        }
        """);

        AppSettings settings = new AppSettingsService(_settingsPath).LoadSettings();

        Assert.Equal(9, settings.MaxParallelInstalls);
        Assert.Equal(2, settings.MaxParallelScans);
    }

    [Fact]
    public async Task LoadSettingsAsync_AppliesTheSameNormalization()
    {
        File.WriteAllText(_settingsPath, """
        {
          "maxParallelInstalls": 5000
        }
        """);

        AppSettings settings = await new AppSettingsService(_settingsPath).LoadSettingsAsync();

        Assert.Equal(new AppSettings().MaxParallelInstalls, settings.MaxParallelInstalls);
    }
}
